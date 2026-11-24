using Microsoft.Xna.Framework;
using OpenGothic.Physics;
using System;
using Xunit;

namespace OpenGothic.Tests;

public class CharacterControllerTests
{
	private static PhysicsWorld CreateWorld(out CharacterController controller, float y = CharacterController.CenterOffset)
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

	private static void Step(CharacterController controller, Vector3 wish, int steps = 120,
		Vector3 facing = default)
	{
		for (var i = 0; i < steps; ++i)
		{
			controller.Step(TimeSpan.FromSeconds(1f / 60f), wish, facing);
		}
	}

	[Fact]
	public void WalksForwardOnFlatFloor()
	{
		using var world = CreateWorld(out var controller);

		Step(controller, Vector3.UnitZ);

		Assert.True(controller.Grounded);
		Assert.InRange(controller.Position.Y, CharacterController.CenterOffset - 0.5f,
			CharacterController.CenterOffset + 0.5f);
		Assert.True(controller.Position.Z > 250f, $"walked only {controller.Position.Z:0} units");
	}

	[Fact]
	public void WalkModeMovesSlowerThanRunning()
	{
		using var runWorld = CreateWorld(out var runner);
		using var walkWorld = CreateWorld(out var walker);
		walker.Speed = CharacterController.WalkSpeed;

		Step(runner, Vector3.UnitZ, 60);
		Step(walker, Vector3.UnitZ, 60);

		Assert.True(walker.Position.Z < runner.Position.Z - 50f,
			$"walked {walker.Position.Z:0} vs ran {runner.Position.Z:0}");
	}

	[Fact]
	public void DoesNotFallThroughTheFloor()
	{
		using var world = CreateWorld(out var controller);

		Step(controller, Vector3.UnitZ, 240);

		Assert.True(controller.Grounded);
		Assert.InRange(controller.Position.Y, CharacterController.CenterOffset - 0.5f,
			CharacterController.CenterOffset + 0.5f);
	}

	[Fact]
	public void StepsOntoALowBlock()
	{
		using var world = CreateWorld(out var controller);
		world.AddStaticBox(new Vector3(0f, 15f, 150f), new Vector3(300f, 30f, 300f),
			PhysicsMaterials.DefaultFriction, PhysicsSurfaceKind.Vob);

		controller.Position = new Vector3(0f, controller.Position.Y, -100f);

		var before = controller.FeetPosition.Y;
		var maxY = before;
		for (var i = 0; i < 120; ++i)
		{
			controller.Step(TimeSpan.FromSeconds(1f / 60f), Vector3.UnitZ);
			maxY = MathF.Max(maxY, controller.FeetPosition.Y);
		}

		Assert.True(controller.Grounded);
		Assert.InRange(maxY - before, 25f, 40f);
	}

	[Fact]
	public void StopsAtAWallWithoutSliding()
	{
		using var world = CreateWorld(out var controller);
		world.AddStaticBox(new Vector3(0f, 100f, 0f), new Vector3(2000f, 200f, 20f),
			PhysicsMaterials.DefaultFriction, PhysicsSurfaceKind.Vob);

		controller.Position = new Vector3(-100f, controller.Position.Y, -200f);

		var start = controller.Position;
		var diagonal = new Vector3(1f, 0f, 1f);
		diagonal.Normalize();

		Step(controller, diagonal);

		var gap = -10f - controller.FeetPosition.Z; // the near face is at z = -10
		Assert.True(gap >= CharacterController.DefaultRadius - 2f,
			$"walked into the wall (gap {gap:0.#})");

		// The diagonal path advances X until the wall stops it. A projected slide would keep
		// increasing X while Z stayed at the wall.
		var forward = controller.FeetPosition.Z - start.Z;
		Assert.True(controller.Position.X - start.X <= forward + 5f,
			$"slid {controller.Position.X - start.X:0.#} units sideways after {forward:0.#} forward");
	}

	[Fact]
	public void BacksAwayFromAWallWithoutSidewaysDrift()
	{
		using var world = CreateWorld(out var controller);
		world.AddStaticBox(new Vector3(0f, 100f, 0f), new Vector3(2000f, 200f, 20f),
			PhysicsMaterials.DefaultFriction, PhysicsSurfaceKind.Vob);

		controller.Position = new Vector3(0f, controller.Position.Y, -120f);

		var start = controller.Position;

		// Face the wall and press S: the reference never bounces a backward move.
		Step(controller, -Vector3.UnitZ, 120, Vector3.UnitZ);

		Assert.True(controller.Position.Z - start.Z < -200f,
			$"backed only {start.Z - controller.Position.Z:0.#} units");
		Assert.True(MathF.Abs(controller.Position.X - start.X) < 1f,
			$"drifted {controller.Position.X - start.X:0.##} units sideways");
	}

	[Fact]
	public void WalksOverALowObstacle()
	{
		using var world = CreateWorld(out var controller);

		// Roughly a plank pile: below the chest capsule, so it must not catch the player.
		world.AddStaticBox(new Vector3(0f, 20f, 150f), new Vector3(300f, 40f, 300f),
			PhysicsMaterials.DefaultFriction, PhysicsSurfaceKind.Vob);

		controller.Position = new Vector3(0f, controller.Position.Y, -100f);

		var maxY = controller.FeetPosition.Y;
		for (var i = 0; i < 120; ++i)
		{
			controller.Step(TimeSpan.FromSeconds(1f / 60f), Vector3.UnitZ);
			maxY = MathF.Max(maxY, controller.FeetPosition.Y);
		}

		Assert.True(controller.Position.Z > 250f,
			$"stuck at z = {controller.Position.Z:0.#}");
		Assert.True(maxY > 30f, $"feet only reached {maxY:0.#}");
	}

	[Fact]
	public void PushesOutOfAnOverlappingStatic()
	{
		using var world = CreateWorld(out var controller);

		// A small box loaded around the capsule: every sweep would otherwise start inside it.
		world.AddStaticBox(new Vector3(0f, controller.Position.Y, 0f), new Vector3(40f, 40f, 40f),
			PhysicsMaterials.DefaultFriction, PhysicsSurfaceKind.Vob);

		var start = controller.Position;
		Step(controller, Vector3.UnitZ, 60);

		Assert.True((controller.Position - start).Length() > 10f);
	}
}
