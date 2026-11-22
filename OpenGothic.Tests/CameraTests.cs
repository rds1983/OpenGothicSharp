using Microsoft.Xna.Framework;
using OpenGothic;
using System;
using Xunit;

namespace OpenGothic.Tests;

public class CameraTests
{
	private const float Epsilon = 1e-4f;

	private static bool Near(Vector3 a, Vector3 b, float epsilon = Epsilon) =>
		Math.Abs(a.X - b.X) <= epsilon &&
		Math.Abs(a.Y - b.Y) <= epsilon &&
		Math.Abs(a.Z - b.Z) <= epsilon;

	private static bool Near(float a, float b, float epsilon = Epsilon) => Math.Abs(a - b) <= epsilon;

	private static Camera CreateCamera()
	{
		var camera = new Camera { FieldOfView = 67.5f };
		camera.SetViewport(1920, 1080);
		return camera;
	}

	[Fact]
	public void Forward_AtZeroAngles_LooksDownNegativeZ()
	{
		var camera = CreateCamera();

		Assert.True(Near(camera.Forward, new Vector3(0, 0, -1)));
	}

	[Fact]
	public void Right_AtZeroAngles_IsPositiveX()
	{
		var camera = CreateCamera();

		Assert.True(Near(camera.Right, new Vector3(1, 0, 0)));
	}

	[Fact]
	public void Up_AtZeroAngles_IsPositiveY()
	{
		var camera = CreateCamera();

		Assert.True(Near(camera.Up, new Vector3(0, 1, 0)));
	}

	[Fact]
	public void Forward_Yaw90_TurnsToPositiveX()
	{
		var camera = CreateCamera();
		camera.Yaw = MathHelper.PiOver2;

		Assert.True(Near(camera.Forward, new Vector3(1, 0, 0)));
	}

	[Fact]
	public void Forward_PitchUp_LooksUpward()
	{
		var camera = CreateCamera();
		camera.Pitch = MathHelper.PiOver4;

		Assert.True(camera.Forward.Y > 0);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(0.5f)]
	[InlineData(1.5f)]
	public void Basis_IsOrthonormal(float yaw)
	{
		var camera = CreateCamera();
		camera.Yaw = yaw;
		camera.Pitch = 0.3f;

		var forward = camera.Forward;
		var right = camera.Right;
		var up = camera.Up;

		Assert.True(Near(forward.Length(), 1f));
		Assert.True(Near(right.Length(), 1f));
		Assert.True(Near(up.Length(), 1f));
		Assert.True(Near(Vector3.Dot(forward, right), 0f));
		Assert.True(Near(Vector3.Dot(forward, up), 0f));
		Assert.True(Near(Vector3.Dot(right, up), 0f));
	}

	[Fact]
	public void View_TransformsPositionToOrigin_AndTargetDownNegativeZ()
	{
		var camera = CreateCamera();
		camera.Position = new Vector3(100, 50, -200);

		var eye = Vector3.Transform(camera.Position, camera.View);
		var target = Vector3.Transform(camera.Target, camera.View);

		Assert.True(Near(eye, Vector3.Zero, 1e-3f));
		Assert.True(Near(target.X, 0f, 1e-3f));
		Assert.True(Near(target.Y, 0f, 1e-3f));
		Assert.True(target.Z < 0);
	}

	[Fact]
	public void Projection_AspectRatio_MatchesViewport()
	{
		var camera = new Camera();
		camera.SetViewport(1600, 900);

		Assert.Equal(1600f / 900f, camera.AspectRatio, 3);
	}

	[Theory]
	[InlineData(0, 1080)]
	[InlineData(1920, 0)]
	[InlineData(-1, 1080)]
	public void SetViewport_RejectsNonPositiveSize(int width, int height)
	{
		var camera = new Camera();

		Assert.Throws<ArgumentOutOfRangeException>(() => camera.SetViewport(width, height));
	}

	[Fact]
	public void Projection_UsesConfiguredNearAndFar()
	{
		var camera = CreateCamera();
		camera.NearPlane = 1f;
		camera.FarPlane = 1000f;

		var projection = camera.Projection;

		Assert.Equal(1f, camera.NearPlane);
		Assert.Equal(1000f, camera.FarPlane);
		Assert.NotEqual(0f, projection.M11);
	}

	[Fact]
	public void DefaultNearAndFar_AreSensible()
	{
		var camera = new Camera();

		Assert.True(camera.NearPlane > 0);
		Assert.True(camera.FarPlane > camera.NearPlane);
	}
}
