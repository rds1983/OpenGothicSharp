using Microsoft.Xna.Framework;
using OpenGothic.Focus;
using OpenGothic.Physics;
using OpenGothic.Vobs;
using Xunit;

namespace OpenGothic.Tests;

public class FocusSystemTests
{
	private static WorldGrid WorldWith(params VobNode[] vobs)
	{
		var world = new WorldGrid();
		foreach (var vob in vobs)
		{
			world.Vobs.Add(vob);
		}

		return world;
	}

	private static VobNode Box(VobKind kind, Vector3 center, float halfSize = 10f, string name = "target")
	{
		var bounds = new BoundingBox(
			center - new Vector3(halfSize),
			center + new Vector3(halfSize));
		return new VobNode(kind, name, name, center, bounds);
	}

	private static Ray Forward => new Ray(Vector3.Zero, -Vector3.UnitZ);

	[Fact]
	public void FindsTheVobUnderTheRay()
	{
		var world = WorldWith(Box(VobKind.Interactive, new Vector3(0f, 0f, -50f)));
		var focus = new FocusSystem();

		focus.Find(world, null, Forward);

		Assert.True(focus.Target.HasValue);
		Assert.Equal("target", focus.Target.Name);
		Assert.InRange(focus.Target.Distance, 39f, 41f);
	}

	[Fact]
	public void IgnoresVobsThatCannotBeInteractedWith()
	{
		var world = WorldWith(Box(VobKind.Static, new Vector3(0f, 0f, -50f)));
		var focus = new FocusSystem();

		focus.Find(world, null, Forward);

		Assert.False(focus.Target.HasValue);
	}

	[Fact]
	public void PicksTheNearestCandidate()
	{
		var world = WorldWith(
			Box(VobKind.Door, new Vector3(0f, 0f, -120f), name: "far"),
			Box(VobKind.Item, new Vector3(0f, 0f, -60f), name: "near"));
		var focus = new FocusSystem();

		focus.Find(world, null, Forward);

		Assert.True(focus.Target.HasValue);
		Assert.Equal("near", focus.Target.Name);
	}

	[Fact]
	public void RespectsTheReach()
	{
		var world = WorldWith(Box(VobKind.Npc, new Vector3(0f, 0f, -250f)));
		var focus = new FocusSystem();

		focus.Find(world, null, Forward);

		Assert.False(focus.Target.HasValue);
	}

	[Fact]
	public void RequiresTheRayToHitTheBox()
	{
		var world = WorldWith(Box(VobKind.Container, new Vector3(100f, 0f, -50f)));
		var focus = new FocusSystem();

		focus.Find(world, null, Forward);

		Assert.False(focus.Target.HasValue);
	}

	[Fact]
	public void ClearingRemovesThePreviousTarget()
	{
		var world = WorldWith(Box(VobKind.Interactive, new Vector3(0f, 0f, -50f)));
		var focus = new FocusSystem();

		focus.Find(world, null, Forward);
		Assert.True(focus.Target.HasValue);

		focus.Find(world, null, new Ray(Vector3.Zero, Vector3.UnitZ));
		Assert.False(focus.Target.HasValue);
	}

	[Fact]
	public void RejectsTargetsBehindAStatic()
	{
		var world = WorldWith(Box(VobKind.Interactive, new Vector3(0f, 0f, -100f)));
		using var physics = new PhysicsWorld();
		physics.AddStaticBox(new Vector3(0f, 0f, -30f), new Vector3(10f, 10f, 10f),
			PhysicsMaterials.DefaultFriction, PhysicsSurfaceKind.Vob);

		var focus = new FocusSystem();
		focus.Find(world, physics, Forward);

		Assert.False(focus.Target.HasValue);
	}
}
