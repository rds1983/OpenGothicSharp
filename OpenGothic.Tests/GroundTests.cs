using Microsoft.Xna.Framework;
using Xunit;

namespace OpenGothic.Tests;

public class GroundTests
{
	private static WorldGrid CreateFlatWorld()
	{
		var grid = new WorldGrid
		{
			MeshBounds = new BoundingBox(Vector3.Zero, new Vector3(4000f, 1000f, 4000f))
		};

		var chunk = new CollisionChunk(
			new BoundingBox(Vector3.Zero, new Vector3(2000f, 1000f, 2000f)));
		chunk.Surfaces.AddTriangle(0.8f, false,
			new Vector3(900f, 0f, 900f),
			new Vector3(1100f, 0f, 900f),
			new Vector3(1000f, 0f, 1100f));
		grid.CollisionChunks[(0, 0)] = chunk;
		return grid;
	}

	[Fact]
	public void TryGetGround_DropsToTheFloorUnderTheWaypoint()
	{
		var grid = CreateFlatWorld();

		var found = grid.TryGetGround(new Vector3(1000f, 50f, 1000f), WorldGrid.GroundSnap,
			out var ground);

		Assert.True(found);
		Assert.Equal(0f, ground.Y);
		Assert.Equal(1000f, ground.X);
		Assert.Equal(1000f, ground.Z);
	}

	[Fact]
	public void TryGetGround_IgnoresFloorsBeyondTheSnapDistance()
	{
		var grid = CreateFlatWorld();

		Assert.False(grid.TryGetGround(new Vector3(1000f, 200f, 1000f), WorldGrid.GroundSnap,
			out _));
	}

	[Fact]
	public void TryGetGround_IgnoresSensors()
	{
		var grid = CreateFlatWorld();
		grid.CollisionChunks[(0, 0)].Surfaces.AddTriangle(0f, true,
			new Vector3(900f, 50f, 900f),
			new Vector3(1100f, 50f, 900f),
			new Vector3(1000f, 50f, 1100f));

		var found = grid.TryGetGround(new Vector3(1000f, 60f, 1000f), WorldGrid.GroundSnap,
			out var ground);

		Assert.True(found);
		Assert.Equal(0f, ground.Y);
	}

	[Fact]
	public void TryGetGround_ReturnsFalseOutsideTheFloor()
	{
		var grid = CreateFlatWorld();

		Assert.False(grid.TryGetGround(new Vector3(500f, 50f, 500f), WorldGrid.GroundSnap,
			out _));
		Assert.False(new WorldGrid().TryGetGround(Vector3.Zero, WorldGrid.GroundSnap, out _));
	}

	[Fact]
	public void WaypointGroundY_ReturnsTheFloorAndFallsBackToTheAuthoredHeight()
	{
		var grid = CreateFlatWorld();
		grid.WaypointList.Add(new Waypoint(new Vector3(1000f, 50f, 1000f), Vector3.UnitZ,
			false, "FLOOR"));
		grid.WaypointList.Add(new Waypoint(new Vector3(500f, 400f, 500f), Vector3.UnitZ,
			false, "AIR"));

		Assert.Equal(0f, grid.WaypointGroundY(0));
		Assert.Equal(400f, grid.WaypointGroundY(1));
		Assert.Equal(0f, grid.WaypointGroundY(-1));
	}
}
