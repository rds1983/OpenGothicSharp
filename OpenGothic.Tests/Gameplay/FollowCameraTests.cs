using Microsoft.Xna.Framework;
using OpenGothic.Gameplay;
using Xunit;

namespace OpenGothic.Tests.Gameplay;

public class FollowCameraTests
{
	private const float Tolerance = 1e-4f;

	[Fact]
	public void LookMovesYawAndPitchInOppositeDirectionsToTheDrag()
	{
		var camera = new FollowCamera { Yaw = 0.0f, Pitch = 0.0f };

		camera.Look(10.0f, 10.0f);

		// Dragging right turns the camera left and vice versa, which is what the viewer expects
		// when the mouse is captured and recentred.
		Assert.True(camera.Yaw < 0.0f);
		Assert.True(camera.Pitch < 0.0f);
	}

	[Fact]
	public void LookClampsPitchBelowStraightUpAndDown()
	{
		var camera = new FollowCamera { Pitch = 0.0f };

		camera.Look(0.0f, -100000.0f);
		Assert.True(camera.Pitch < MathHelper.PiOver2);

		camera.Look(0.0f, 200000.0f);
		Assert.True(camera.Pitch > -MathHelper.PiOver2);
	}

	[Fact]
	public void ZoomStaysWithinLimits()
	{
		var camera = new FollowCamera { Distance = 450.0f };

		for (var i = 0; i < 100; ++i)
		{
			camera.Zoom(1.0f);
		}

		Assert.Equal(150.0f, camera.Distance, Tolerance);

		for (var i = 0; i < 100; ++i)
		{
			camera.Zoom(-1.0f);
		}

		Assert.Equal(5000.0f, camera.Distance, Tolerance);
	}

	[Fact]
	public void ZoomIgnoresZeroDelta()
	{
		var camera = new FollowCamera { Distance = 300.0f };

		camera.Zoom(0.0f);

		Assert.Equal(300.0f, camera.Distance, Tolerance);
	}
}
