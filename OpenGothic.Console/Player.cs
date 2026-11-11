using System;
using Microsoft.Xna.Framework;

namespace OpenGothic.Tools;

/// <summary>
/// Minimal player avatar: a capsule that moves without collision.
/// <see cref="Position"/> is the centre of the capsule.
/// </summary>
internal sealed class Player
{
	public const float Radius = 30.0f;

	/// <summary>Distance between the two sphere centres of the capsule.</summary>
	public const float CapsuleLength = 120.0f;

	public Vector3 Position;
	public float Yaw;

	/// <summary>Ground speed in world units per second.</summary>
	public float WalkSpeed = 400.0f;

	/// <summary>Speed while holding Shift.</summary>
	public float RunSpeed = 900.0f;

	public Matrix World => Matrix.CreateRotationY(Yaw) * Matrix.CreateTranslation(Position);

	/// <summary>Moves along <paramref name="direction"/>; height is free until collision exists.</summary>
	public void Move(Vector3 direction, float deltaSeconds, bool run)
	{
		if (direction == Vector3.Zero)
		{
			return;
		}

		direction.Normalize();
		Position += direction * (run ? RunSpeed : WalkSpeed) * deltaSeconds;

		if (direction.X != 0.0f || direction.Z != 0.0f)
		{
			Yaw = MathF.Atan2(-direction.X, -direction.Z);
		}
	}
}
