using Microsoft.Xna.Framework;
using OpenGothic.Gameplay;
using Xunit;

namespace OpenGothic.Tests.Gameplay;

public class PlayerTests
{
	private const float Tolerance = 1e-3f;

	[Fact]
	public void MoveIgnoresZeroDirection()
	{
		var player = new Player { Position = new Vector3(1.0f, 2.0f, 3.0f), Yaw = 0.5f };

		player.Move(Vector3.Zero, 1.0f, false);

		Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), player.Position);
		Assert.Equal(0.5f, player.Yaw, Tolerance);
	}

	[Fact]
	public void MoveRunsFasterThanWalking()
	{
		var walker = new Player { Position = Vector3.Zero };
		var runner = new Player { Position = Vector3.Zero };

		walker.Move(Vector3.UnitX, 1.0f, false);
		runner.Move(Vector3.UnitX, 1.0f, true);

		Assert.Equal(walker.WalkSpeed, walker.Position.X, Tolerance);
		Assert.Equal(runner.RunSpeed, runner.Position.X, Tolerance);
		Assert.True(runner.Position.X > walker.Position.X);
	}

	[Fact]
	public void MoveNormalizesDiagonalDirection()
	{
		var player = new Player { Position = Vector3.Zero };

		player.Move(Vector3.UnitX + Vector3.UnitZ, 1.0f, false);

		// A diagonal must not be faster than a straight line: the length, not the sum of the
		// axes, has to equal the walk speed.
		Assert.Equal(player.WalkSpeed, player.Position.Length(), 0.01f);
	}

	[Fact]
	public void MoveChangesYawToFaceTheDirectionOfTravel()
	{
		var player = new Player { Position = Vector3.Zero };

		player.Move(Vector3.UnitZ, 1.0f, false);

		// Gothic models face -Z, so moving along +Z turns the avatar around.
		Assert.Equal(MathHelper.Pi, System.Math.Abs(player.Yaw), Tolerance);
	}

	[Fact]
	public void ApplyGravitySnapsToGroundWhenWithinReach()
	{
		var player = new Player();
		var ground = 100.0f;
		player.Position = new Vector3(0.0f, ground + Player.HalfHeight + 10.0f, 0.0f);

		player.ApplyGravity(1.0f / 60.0f, ground);

		Assert.True(player.Grounded);
		Assert.Equal(ground + Player.HalfHeight, player.Position.Y, Tolerance);
	}

	[Fact]
	public void ApplyGravityFallsWhenThereIsNoGround()
	{
		var player = new Player { Position = new Vector3(0.0f, 1000.0f, 0.0f) };

		player.ApplyGravity(0.5f, null);

		Assert.False(player.Grounded);
		Assert.True(player.Position.Y < 1000.0f);
	}

	[Fact]
	public void ApplyGravityIgnoresGroundThatIsTooHighToStepOnto()
	{
		var player = new Player { Position = new Vector3(0.0f, 1000.0f, 0.0f) };
		var feet = player.Position.Y - Player.HalfHeight;

		// A ledge above the step height must not teleport the player up onto it.
		player.ApplyGravity(1.0f / 60.0f, feet + Player.StepHeight + 1.0f);

		Assert.False(player.Grounded);
	}

	[Fact]
	public void ApplyGravityIgnoresGroundFarBelowTheSnapDistance()
	{
		var player = new Player { Position = new Vector3(0.0f, 5000.0f, 0.0f) };

		player.ApplyGravity(1.0f / 60.0f, 0.0f);

		Assert.False(player.Grounded);
		Assert.True(player.Position.Y < 5000.0f);
	}
}
