using Microsoft.Xna.Framework;
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

public class WorldGrid
{
	public WorldGridCell[,] Cells { get; } = new WorldGridCell[Constants.GridSize, Constants.GridSize];

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
