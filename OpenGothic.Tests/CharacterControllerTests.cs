using Microsoft.Xna.Framework;
using OpenGothic.Physics;
using System;
using Xunit;

namespace OpenGothic.Tests;

public class CharacterControllerTests
{
	private static PhysicsWorld CreateWorld(out CharacterController controller, float y = 85.5f)
	{
		var world = new PhysicsWorld();
		world.AddStaticBox(new Vector3(0f, -50f, 0f), new Vector3(4000f, 100f, 4000f),
			PhysicsMaterials.DefaultFriction, PhysicsSurfaceKind.Terrain);

		controller = new CharacterController(world)
		{
			Position = new Vector3(0f, y, 0f)
		};

		return world;
	}

	private static void Step(CharacterController controller, Vector3 wish, int steps = 120)
	{
		for (var i = 0; i < steps; ++i)
		{
			controller.Step(TimeSpan.FromSeconds(1f / 60f), wish);
		}
	}

	[Fact]
	public void WalksForwardOnFlatFloor()
	{
		using var world = CreateWorld(out var controller);

		Step(controller, Vector3.UnitZ);

		Assert.True(controller.Grounded);
		Assert.InRange(controller.Position.Y, 85f, 86f);
		Assert.True(controller.Position.Z > 250f, $"walked only {controller.Position.Z:0} units");
	}

	[Fact]
	public void DoesNotFallThroughTheFloor()
	{
		using var world = CreateWorld(out var controller);

		Step(controller, Vector3.UnitZ, 240);

		Assert.True(controller.Grounded);
		Assert.InRange(controller.Position.Y, 85f, 86f);
	}

	[Fact]
	public void StepsOntoALowBlock()
	{
		using var world = CreateWorld(out var controller);
		world.AddStaticBox(new Vector3(0f, 15f, 150f), new Vector3(300f, 30f, 300f),
			PhysicsMaterials.DefaultFriction, PhysicsSurfaceKind.Vob);

		controller.Position = new Vector3(0f, controller.Position.Y, -100f);

		var before = controller.Position.Y;
		var maxY = before;
		for (var i = 0; i < 120; ++i)
		{
			controller.Step(TimeSpan.FromSeconds(1f / 60f), Vector3.UnitZ);
			maxY = MathF.Max(maxY, controller.Position.Y);
		}

		Assert.True(controller.StepSuccesses > 0);
		Assert.InRange(maxY - before, 25f, 40f);
	}

	[Fact]
	public void SlidesAlongAWall()
	{
		using var world = CreateWorld(out var controller);
		world.AddStaticBox(new Vector3(0f, 100f, 0f), new Vector3(2000f, 200f, 20f),
			PhysicsMaterials.DefaultFriction, PhysicsSurfaceKind.Vob);

		controller.Position = new Vector3(-100f, controller.Position.Y, -100f);

		var start = controller.Position;
		var wish = new Vector3(1f, 0f, 1f);
		wish.Normalize();

		Step(controller, wish);

		Assert.True(controller.Position.X - start.X > 200f);
		Assert.True(-10f - controller.Position.Z >= CharacterController.DefaultRadius - 2f);
	}
}
