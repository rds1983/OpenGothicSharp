using Microsoft.Xna.Framework;
using OpenGothic.Engine;
using System.Collections.Generic;

namespace OpenGothic;

public class WorldGridCell
{
	public List<SceneNode> Children { get; } = new List<SceneNode>();
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
