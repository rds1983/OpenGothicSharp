using OpenGothic;
using Xunit;

namespace OpenGothic.Tests;

/// <summary>
/// Proves the test project is wired up correctly: it references the engine, MonoGame types
/// resolve, and xUnit discovers and runs tests. Real tests live in per-subsystem files.
/// </summary>
public class SmokeTests
{
	[Fact]
	public void EngineAssemblyLoadsAndWorldGridBuilds()
	{
		// Building a grid touches MonoGame's BoundingBox, so this fails loudly if the
		// project reference or the MonoGame package is missing.
		var grid = new WorldGrid();

		var width = grid.Cells.GetLength(0);
		var height = grid.Cells.GetLength(1);

		Assert.Equal(Constants.GridSize, width);
		Assert.Equal(Constants.GridSize, height);

		for (var x = 0; x < width; ++x)
		{
			for (var z = 0; z < height; ++z)
			{
				Assert.NotNull(grid.Cells[x, z]);
				Assert.Empty(grid.Cells[x, z].Children);
			}
		}
	}
}
