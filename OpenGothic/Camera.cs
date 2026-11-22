using Microsoft.Xna.Framework;
using System;

namespace OpenGothic;

/// <summary>
/// Position and orientation of the viewer.
/// World space is right-handed, Y-up: game data goes through
/// <c>Mathematics.ToXna</c>, which negates Z. Yaw 0 looks down -Z.
/// </summary>
public class Camera
{
	private int _viewportWidth = 1;
	private int _viewportHeight = 1;

	public Vector3 Position { get; set; }

	public float Yaw { get; set; }

	public float Pitch { get; set; }

	/// <summary>Vertical field of view in degrees.</summary>
	public float FieldOfView { get; set; } = 67.5f;

	public float NearPlane { get; set; } = Constants.NearPlane;

	public float FarPlane { get; set; } = Constants.FarPlane;

	public int ViewportWidth => _viewportWidth;

	public int ViewportHeight => _viewportHeight;

	public float AspectRatio => (float)_viewportWidth / _viewportHeight;

	public Vector3 Forward => new Vector3(
		MathF.Sin(Yaw) * MathF.Cos(Pitch),
		MathF.Sin(Pitch),
		-MathF.Cos(Yaw) * MathF.Cos(Pitch));

	public Vector3 Right => new Vector3(MathF.Cos(Yaw), 0, MathF.Sin(Yaw));

	public Vector3 Up => Vector3.Cross(Right, Forward);

	public Vector3 Target => Position + Forward;

	public Matrix View => Matrix.CreateLookAt(Position, Target, Vector3.Up);

	public Matrix Projection => Matrix.CreatePerspectiveFieldOfView(
		MathHelper.ToRadians(FieldOfView), AspectRatio, NearPlane, FarPlane);

	public Matrix ViewProjection => View * Projection;

	public void SetViewport(int width, int height)
	{
		if (width <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(width), width, "Viewport width must be positive.");
		}

		if (height <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(height), height, "Viewport height must be positive.");
		}

		_viewportWidth = width;
		_viewportHeight = height;
	}
}
