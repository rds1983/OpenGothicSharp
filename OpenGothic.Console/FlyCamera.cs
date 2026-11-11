using Microsoft.Xna.Framework;

namespace OpenGothic.Tools;

/// <summary>
/// Free-fly camera driven by keyboard movement and mouse look.
/// </summary>
internal sealed class FlyCamera
{
	private const float MaxPitch = MathHelper.PiOver2 - 0.01f;

	public Vector3 Position;
	public float Yaw;
	public float Pitch;

	/// <summary>Movement speed in world units per second.</summary>
	public float MoveSpeed = 1500.0f;

	/// <summary>Mouse sensitivity in radians per pixel.</summary>
	public float LookSensitivity = 0.0022f;

	public Vector3 Forward => Vector3.Transform(Vector3.Forward, Matrix.CreateFromYawPitchRoll(Yaw, Pitch, 0.0f));

	public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Vector3.Up));

	public Matrix View => Matrix.CreateLookAt(Position, Position + Forward, Vector3.Up);

	public void Look(float deltaX, float deltaY)
	{
		Yaw -= deltaX * LookSensitivity;
		Pitch -= deltaY * LookSensitivity;
		Pitch = MathHelper.Clamp(Pitch, -MaxPitch, MaxPitch);
	}

	public void Move(Vector3 direction, float deltaSeconds, bool fast)
	{
		if (direction == Vector3.Zero)
		{
			return;
		}

		var speed = MoveSpeed * (fast ? 5.0f : 1.0f);
		Position += Vector3.Normalize(direction) * speed * deltaSeconds;
	}

	public void Zoom(float wheelDelta)
	{
		if (wheelDelta == 0.0f)
		{
			return;
		}

		MoveSpeed = MathHelper.Clamp(MoveSpeed * (wheelDelta > 0.0f ? 1.2f : 1.0f / 1.2f), 100.0f, 60000.0f);
	}
}
