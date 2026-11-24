using Microsoft.Xna.Framework;
using OpenGothic;
using System.Collections.Generic;
using Xunit;

namespace OpenGothic.Tests;

public class WaypointPathTests
{
	private static WorldGrid CreateLine()
	{
		var grid = new WorldGrid();
		grid.WaypointList.Add(new Waypoint(new Vector3(0, 0, 0), Vector3.UnitZ, false, "A"));
		grid.WaypointList.Add(new Waypoint(new Vector3(100, 0, 0), Vector3.UnitZ, false, "B"));
		grid.WaypointList.Add(new Waypoint(new Vector3(200, 0, 0), Vector3.UnitZ, false, "C"));
		grid.WaypointIndices["A"] = 0;
		grid.WaypointIndices["B"] = 1;
		grid.WaypointIndices["C"] = 2;
		grid.WaypointNeighbors = new[]
		{
			new List<int> { 1 },
			new List<int> { 0, 2 },
			new List<int> { 1 }
		};
		return grid;
	}

	[Fact]
	public void FindWaypointPath_FollowsEdges()
	{
		var grid = CreateLine();

		var path = grid.FindWaypointPath(0, 2);

		Assert.Equal(new[] { 0, 1, 2 }, path);
	}

	[Fact]
	public void FindWaypointPath_ReturnsNullWhenDisconnected()
	{
		var grid = CreateLine();
		grid.WaypointNeighbors[1].Clear();
		grid.WaypointNeighbors[2].Clear();

		Assert.Null(grid.FindWaypointPath(0, 2));
	}

	[Fact]
	public void FindNearestWaypoint_UsesPosition()
	{
		var grid = CreateLine();

		Assert.Equal(1, grid.FindNearestWaypoint(new Vector3(95, 0, 10)));
		Assert.Equal(-1, new WorldGrid().FindNearestWaypoint(Vector3.Zero));
	}
}
