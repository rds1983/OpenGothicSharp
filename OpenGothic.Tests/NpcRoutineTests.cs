using OpenGothic.Vobs;
using System.Collections.Generic;
using Xunit;

namespace OpenGothic.Tests;

public class NpcRoutineTests
{
	private static NpcRoutine Routine(float start, float end, string waypoint)
	{
		return new NpcRoutine
		{
			Start = start,
			End = end,
			Waypoint = waypoint
		};
	}

	[Fact]
	public void Select_ReturnsTheActiveRange()
	{
		var routines = new List<NpcRoutine>
		{
			Routine(8f, 22f, "day"),
			Routine(22f, 8f, "night")
		};

		var active = NpcRoutine.Select(routines, 10f, _ => true);

		Assert.Same(routines[0], active);
	}

	[Fact]
	public void Select_HandlesRangesAcrossMidnight()
	{
		var routines = new List<NpcRoutine>
		{
			Routine(8f, 22f, "day"),
			Routine(22f, 8f, "night")
		};

		var active = NpcRoutine.Select(routines, 2f, _ => true);

		Assert.Same(routines[1], active);
	}

	[Fact]
	public void Select_SkipsRoutinesWithMissingWaypoints()
	{
		var routines = new List<NpcRoutine>
		{
			Routine(8f, 22f, "missing"),
			Routine(8f, 22f, "present")
		};

		var active = NpcRoutine.Select(routines, 10f, name => name == "present");

		Assert.Same(routines[1], active);
	}

	[Fact]
	public void Select_FallsBackToTheClosestPreviousEnd()
	{
		var routines = new List<NpcRoutine>
		{
			Routine(2f, 4f, "early"),
			Routine(20f, 22f, "late")
		};

		var active = NpcRoutine.Select(routines, 23f, _ => true);

		Assert.Same(routines[1], active);
	}

	[Fact]
	public void Select_ReturnsNullWithoutUsableRoutines()
	{
		Assert.Null(NpcRoutine.Select(new List<NpcRoutine>(), 10f, _ => true));
		Assert.Null(NpcRoutine.Select(
			new List<NpcRoutine> { Routine(8f, 22f, "missing") }, 10f, _ => false));
	}
}
