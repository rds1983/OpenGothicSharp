using Microsoft.Xna.Framework;
using OpenGothic.Physics;
using OpenGothic.Scene;
using System.Collections.Generic;

namespace OpenGothic;

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
}
