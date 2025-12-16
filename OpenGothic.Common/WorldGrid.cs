using Microsoft.Xna.Framework;
using Nursia.SceneGraph;

namespace OpenGothic
{
	public class WorldGridCell
	{
		public SceneNode Root { get; } = new SceneNode();
		public BoundingBox BoundingBox { get; internal set; }

		public override string ToString() => $"{Root.Children.Count} meshes";
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
	}
}
