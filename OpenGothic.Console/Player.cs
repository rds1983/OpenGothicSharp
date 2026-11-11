using System;
using Microsoft.Xna.Framework;

namespace OpenGothic.Tools;

/// <summary>
/// Minimal player avatar: a capsule that collides with steep world geometry and is kept
/// on the ground by gravity. <see cref="Position"/> is the centre of the capsule.
/// </summary>
internal sealed class Player
{
	public const float Radius = 30.0f;

	/// <summary>Distance between the two sphere centres of the capsule.</summary>
	public const float CapsuleLength = 120.0f;

	/// <summary>Distance from the capsule's centre to its feet.</summary>
	public const float HalfHeight = CapsuleLength * 0.5f + Radius;

	/// <summary>Largest ledge the player climbs or steps down without falling.</summary>
	public const float StepHeight = 60.0f;

	private const float Gravity = 2000.0f;
	private const float MaxFallSpeed = 3000.0f;
	private const float SnapDistance = 400.0f;

	public Vector3 Position;
	public float Yaw;

	/// <summary>True while the player is standing on world geometry.</summary>
	public bool Grounded { get; private set; }

	private float _fallSpeed;

	/// <summary>Ground speed in world units per second.</summary>
	public float WalkSpeed = 400.0f;

	/// <summary>Speed while holding Shift.</summary>
	public float RunSpeed = 900.0f;

	public Matrix World => Matrix.CreateRotationY(Yaw) * Matrix.CreateTranslation(Position);

	/// <summary>Moves along <paramref name="direction"/>. Vertical movement is left to <see cref="ApplyGravity"/>.</summary>
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

	/// <summary>
	/// Snaps the player onto <paramref name="groundY"/> when it is close enough, otherwise
	/// lets gravity pull it down. A null value means there is no ground below the player.
	/// </summary>
	public void ApplyGravity(float deltaSeconds, float? groundY)
	{
		var feet = Position.Y - HalfHeight;

		if (groundY.HasValue
			&& groundY.Value <= feet + StepHeight
			&& groundY.Value >= feet - SnapDistance)
		{
			Position.Y = groundY.Value + HalfHeight;
			_fallSpeed = 0.0f;
			Grounded = true;
			return;
		}

		Grounded = false;
		_fallSpeed = MathF.Min(_fallSpeed + Gravity * deltaSeconds, MaxFallSpeed);
		Position.Y -= _fallSpeed * deltaSeconds;
	}
}
