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
	public Waypoint(Vector3 position, Vector3 direction, bool free)
	{
		Position = position;
		Direction = direction;
		Free = free;
	}

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
}
