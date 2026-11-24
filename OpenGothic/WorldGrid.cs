using Microsoft.Xna.Framework;
using OpenGothic.Physics;
using OpenGothic.Graphics;
using OpenGothic.Vobs;
using System;
using System.Collections.Generic;

namespace OpenGothic;

/// <summary>One waynet point in engine space, plus the authored facing.</summary>
public readonly struct Waypoint
{
	public Waypoint(Vector3 position, Vector3 direction, bool free, string name = "")
	{
		Position = position;
		Direction = direction;
		Free = free;
		Name = name ?? string.Empty;
	}

	public string Name { get; }

	public Vector3 Position { get; }

	public Vector3 Direction { get; }

	public bool Free { get; }
}

public class WorldGridCell
{
	public List<SceneNode> Children { get; } = new List<SceneNode>();

	/// <summary>
	/// World-space boxes of the virtual objects in this cell, one per vob. Kept separately
	/// from <see cref="Children"/> because a single vob becomes many mesh parts (one per
	/// material), and the debug view wants one box per object, not per part.
	/// </summary>
	public List<BoundingBox> ObjectBounds { get; } = new List<BoundingBox>();

	public BoundingBox BoundingBox { get; internal set; }

	public object Tag { get; set; }

	public override string ToString() => $"{Children.Count} meshes";
}

/// <summary>
/// One square of the level mesh's collision data. Chunking the terrain rather than using the
/// four render cells lets the physics world load only what is near the player.
/// </summary>
public sealed class CollisionChunk
{
	public BoundingBox Bounds { get; }

	public CollisionSurfaceSet Surfaces { get; } = new();

	public CollisionChunk(BoundingBox bounds)
	{
		Bounds = bounds;
	}
}

public class WorldGrid
{
	/// <summary>Side of one collision chunk, in game units.</summary>
	public const float CollisionChunkSize = 2000f;

	public WorldGridCell[,] Cells { get; } = new WorldGridCell[Constants.GridSize, Constants.GridSize];

	/// <summary>Bounds of the level mesh; collision chunks are laid out from its minimum.</summary>
	public BoundingBox MeshBounds { get; internal set; }

	/// <summary>
	/// Solid triangles of the level mesh, bucketed into <see cref="CollisionChunkSize"/>
	/// squares. Built while the level is read; vob geometry is collected separately when the
	/// physics world initializes.
	/// </summary>
	public Dictionary<(int X, int Z), CollisionChunk> CollisionChunks { get; } = new();

	/// <summary>Typed ZEN vob tree, built once while the world is loaded.</summary>
	public VobTree Vobs { get; } = new();

	/// <summary>Waynet points by name, in engine space; scripts use these as spawn points.</summary>
	public Dictionary<string, Waypoint> Waypoints { get; } =
		new Dictionary<string, Waypoint>(StringComparer.OrdinalIgnoreCase);

	/// <summary>All waynet points in file order; indices match <see cref="WaypointNeighbors"/>.</summary>
	public List<Waypoint> WaypointList { get; } = new List<Waypoint>();

	/// <summary>Named point index, used by routine pathfinding.</summary>
	public Dictionary<string, int> WaypointIndices { get; } =
		new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	/// <summary>Adjacency built from the waynet edges; null when the world has no waynet.</summary>
	public List<int>[] WaypointNeighbors { get; internal set; }

	/// <summary>Lazily sampled ground height per waynet point; NaN means not sampled yet.</summary>
	private float[] _waypointGround;

	public WorldGrid()
	{
		for (var i = 0; i < Constants.GridSize; ++i)
		{
			for (var j = 0; j < Constants.GridSize; ++j)
			{
				Cells[i, j] = new WorldGridCell();
			}
		}
	}

	public WorldGridCell FindCellByBox(BoundingBox box)
	{
		for (var i = 0; i < Constants.GridSize; ++i)
		{
			for (var j = 0; j < Constants.GridSize; ++j)
			{
				if (Cells[i, j].BoundingBox.Intersects(box))
				{
					return Cells[i, j];
				}
			}
		}

		return null;
	}

	/// <summary>Cell whose horizontal extent contains a world position, or null outside the mesh.</summary>
	public WorldGridCell FindCellByPoint(Vector3 position)
	{
		foreach (var cell in Cells)
		{
			if (cell.BoundingBox.Contains(position) != ContainmentType.Disjoint)
			{
				return cell;
			}
		}

		return null;
	}

	/// <summary>Index of the closest waynet point to a world position, or -1 without a waynet.</summary>
	public int FindNearestWaypoint(Vector3 position)
	{
		var best = -1;
		var bestDistance = float.MaxValue;
		for (var i = 0; i < WaypointList.Count; ++i)
		{
			var distance = Vector3.DistanceSquared(WaypointList[i].Position, position);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = i;
			}
		}

		return best;
	}

	/// <summary>
	/// Distance within which a surface under a waypoint counts as its floor. Waynet points
	/// are authored about 50 units above the ground; a larger gap means the point stands on
	/// vob geometry (a house's wooden floor) that the terrain mesh does not contain, so the
	/// authored height is kept instead of dropping to the street far below.
	/// </summary>
	public const float GroundSnap = 60f;

	/// <summary>
	/// Highest terrain surface under <paramref name="position"/> within
	/// <paramref name="maxDrop"/> below it, or false when the point is outside the level or
	/// over a hole. Waynet points are authored above the floor, so NPC placement needs this.
	/// </summary>
	public bool TryGetGround(Vector3 position, float maxDrop, out Vector3 ground)
	{
		const float above = 100f;
		var origin = MeshBounds.Min;
		const float size = CollisionChunkSize;
		var cx = (int)MathF.Floor((position.X - origin.X) / size);
		var cz = (int)MathF.Floor((position.Z - origin.Z) / size);
		var height = float.NegativeInfinity;

		// Triangles are owned by the chunk holding their centroid, so a point on a seam is
		// covered by a neighbour. Sensors (water) are never standing ground.
		for (var dx = -1; dx <= 1; ++dx)
		{
			for (var dz = -1; dz <= 1; ++dz)
			{
				if (!CollisionChunks.TryGetValue((cx + dx, cz + dz), out var chunk))
				{
					continue;
				}

				foreach (var surface in chunk.Surfaces.Surfaces)
				{
					if (surface.Sensor)
					{
						continue;
					}

					var vertices = surface.Vertices;
					for (var i = 0; i + 2 < vertices.Count; i += 3)
					{
						var y = TriangleHeight(vertices[i], vertices[i + 1], vertices[i + 2],
							position.X, position.Z);
						if (!float.IsNaN(y) && y > height &&
						    y <= position.Y + above && y >= position.Y - maxDrop)
						{
							height = y;
						}
					}
				}
			}
		}

		if (float.IsNegativeInfinity(height))
		{
			ground = position;
			return false;
		}

		ground = new Vector3(position.X, height, position.Z);
		return true;
	}

	/// <summary>
	/// Ground height of a waynet point, cached because the whole waynet sits above the floor
	/// and hundreds of NPCs share these points. Falls back to the authored height.
	/// </summary>
	public float WaypointGroundY(int index)
	{
		if (index < 0 || index >= WaypointList.Count)
		{
			return 0f;
		}

		_waypointGround ??= CreateGroundCache();
		var cached = _waypointGround[index];
		if (!float.IsNaN(cached))
		{
			return cached;
		}

		var point = WaypointList[index].Position;
		var y = TryGetGround(point, GroundSnap, out var ground) ? ground.Y : point.Y;
		_waypointGround[index] = y;
		return y;
	}

	/// <summary>
	/// Shortest waynet path between two point indices, or null when no path exists. The BFS
	/// runs only when an NPC changes routine, never per frame.
	/// </summary>
	public List<int> FindWaypointPath(int start, int goal)
	{
		var neighbors = WaypointNeighbors;
		if (neighbors == null || start < 0 || goal < 0 ||
		    start >= neighbors.Length || goal >= neighbors.Length)
		{
			return null;
		}

		if (start == goal)
		{
			return new List<int> { start };
		}

		var from = new int[neighbors.Length];
		Array.Fill(from, -1);
		var queue = new Queue<int>();
		from[start] = start;
		queue.Enqueue(start);

		while (queue.Count > 0)
		{
			var current = queue.Dequeue();
			foreach (var next in neighbors[current])
			{
				if (from[next] != -1)
				{
					continue;
				}

				from[next] = current;
				if (next == goal)
				{
					var path = new List<int>();
					for (var at = goal; at != start; at = from[at])
					{
						path.Add(at);
					}

					path.Add(start);
					path.Reverse();
					return path;
				}

				queue.Enqueue(next);
			}
		}

		return null;
	}

	/// <summary>
	/// Re-homes a vob visual and its debug box after a moving NPC crosses a cell boundary.
	/// A same-cell move only replaces the box.
	/// </summary>
	public void UpdateVobBounds(VobNode vob, BoundingBox oldBounds, BoundingBox newBounds)
	{
		var visual = vob?.SceneNode;
		if (visual == null)
		{
			return;
		}

		var oldCell = FindCellByBox(oldBounds);
		var newCell = FindCellByBox(newBounds) ?? FindCellByPoint(vob.Position);
		if (ReferenceEquals(oldCell, newCell))
		{
			if (newCell != null)
			{
				RemoveObjectBounds(newCell, oldBounds);
				newCell.ObjectBounds.Add(newBounds);
			}

			return;
		}

		if (oldCell != null)
		{
			oldCell.Children.Remove(visual);
			RemoveObjectBounds(oldCell, oldBounds);
		}

		if (newCell != null)
		{
			newCell.Children.Add(visual);
			newCell.ObjectBounds.Add(newBounds);
		}
	}

	/// <summary>
	/// Removes a vob's rendered geometry from its cell. A single-instance visual is a node of
	/// its own; a multi-resolution batch keeps the other instances and drops only this one.
	/// The vob stays in the typed tree and is skipped by focus once its owner marks it taken.
	/// </summary>
	public bool RemoveVobVisual(VobNode vob)
	{
		if (vob == null)
		{
			return false;
		}

		var visual = vob.SceneNode;
		if (visual != null)
		{
			for (var i = 0; i < Constants.GridSize; ++i)
			{
				for (var j = 0; j < Constants.GridSize; ++j)
				{
					var cell = Cells[i, j];
					if (!cell.Children.Remove(visual))
					{
						continue;
					}

					RemoveObjectBounds(cell, vob.Bounds);
					return true;
				}
			}
		}

		// Batched multi-resolution meshes have no per-vob SceneNode; find the batch they
		// were registered in and remove one instance.
		for (var i = 0; i < Constants.GridSize; ++i)
		{
			for (var j = 0; j < Constants.GridSize; ++j)
			{
				var cell = Cells[i, j];
				for (var k = 0; k < cell.Children.Count; ++k)
				{
					if (cell.Children[k] is not InstancedMeshNode batch || !batch.RemoveInstance(vob))
					{
						continue;
					}

					if (batch.InstancesVobs.Count == 0)
					{
						cell.Children.RemoveAt(k);
					}

					RemoveObjectBounds(cell, vob.Bounds);
					return true;
				}
			}
		}

		return false;
	}

	private static void RemoveObjectBounds(WorldGridCell cell, BoundingBox bounds)
	{
		for (var i = cell.ObjectBounds.Count - 1; i >= 0; --i)
		{
			if (cell.ObjectBounds[i] == bounds)
			{
				cell.ObjectBounds.RemoveAt(i);
				return;
			}
		}
	}

	/// <summary>Height where a vertical ray at (x,z) meets a triangle, or NaN outside it.</summary>
	private static float TriangleHeight(Vector3 a, Vector3 b, Vector3 c, float x, float z)
	{
		var d = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
		if (MathF.Abs(d) < 1e-6f)
		{
			return float.NaN;
		}

		var u = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / d;
		var v = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / d;
		var w = 1f - u - v;
		if (u < -1e-4f || v < -1e-4f || w < -1e-4f)
		{
			return float.NaN;
		}

		return u * a.Y + v * b.Y + w * c.Y;
	}

	/// <summary>Ground cache filled with NaN so an entry reads as "not sampled yet".</summary>
	private float[] CreateGroundCache()
	{
		var cache = new float[WaypointList.Count];
		Array.Fill(cache, float.NaN);
		return cache;
	}
}
