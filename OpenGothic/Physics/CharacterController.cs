using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using Microsoft.Xna.Framework;
using OpenGothic.Physics;
using System;
using Num = System.Numerics;

namespace OpenGothic.Physics;

/// <summary>
/// The player's kinematic capsule, ported from OpenGothic's NPC movement. The capsule is a
/// non-solid "ghost" shape placed at chest height: the reference deliberately keeps it off the
/// ground so small props and terrain bumps cannot trap the player's legs. Movement is a
/// substepped series of position tests; a blocked step stops the move instead of projecting it
/// sideways. Ground following is a downward ray plus a step-height snap, not a gravity sweep.
/// </summary>
/// <remarks>
/// <see cref="Position"/> is the capsule's centre. It sits <see cref="CenterOffset"/> above the
/// logical feet position, which is what the game, the camera and <c>-player:</c> all reason
/// about. The capsule itself is only the chest/head volume, matching the reference's
/// <c>HumShape</c> for a human-sized actor.
/// </remarks>
public sealed class CharacterController
{
	public const float DefaultRadius = 30f;
	public const float DefaultLength = 30f;

	/// <summary>Distance from the logical feet to the centre of the collision capsule.</summary>
	public const float CenterOffset = 150f;

	private const float Gravity = -981f;
	private const float GroundAcceleration = 3000f;
	private const float AirAcceleration = 800f;

	/// <summary>Full running speed, the reference's default when walk mode is off.</summary>
	public const float RunSpeed = 300f;

	/// <summary>Speed with walk mode on, half the run speed.</summary>
	public const float WalkSpeed = 150f;

	/// <summary>Largest horizontal substep; the reference uses the capsule radius.</summary>
	private const float StepSize = DefaultRadius;

	/// <summary>Largest rise or drop treated as ground following instead of a fall.</summary>
	private const float StepHeight = 50f;

	/// <summary>OpenGothic's default guild step height is 50; keep the same value here.</summary>
	private const float Skin = 0.5f;
	private const float MinMove = 0.01f;
	private const float TerminalVelocity = -4000f;

	private const float GroundProbeDistance = CenterOffset + 100f;

	private readonly PhysicsWorld _world;
	private readonly Capsule _shape;

	public CharacterController(PhysicsWorld world)
	{
		_world = world ?? throw new ArgumentNullException(nameof(world));
		_shape = new Capsule(DefaultRadius, DefaultLength);
	}

	/// <summary>Capsule centre in world space, <see cref="CenterOffset"/> above the feet.</summary>
	public Vector3 Position;

	public Vector3 Velocity;

	/// <summary>
	/// Top speed for the current walk mode. The reference toggles <c>WM_Walk</c> and moves at
	/// a slower rate without changing the movement algorithm itself.
	/// </summary>
	public float Speed { get; set; } = RunSpeed;

	/// <summary>Logical feet position, which is what scripts and the camera use.</summary>
	public Vector3 FeetPosition => Position - Vector3.UnitY * CenterOffset;

	public bool Grounded { get; private set; }

	/// <summary>Normal of the surface that blocked the last <see cref="TryMove"/>.</summary>
	public Vector3 LastHitNormal { get; private set; }

	public float Radius => DefaultRadius;

	public float Length => DefaultLength;

	/// <summary>Half the capsule's total height, including both end spheres.</summary>
	public float HalfHeight => Radius + Length * 0.5f;

	public enum MoveResult
	{
		Fail,
		Ok,
		Skip,
		Partial,
	}

	/// <summary>
	/// Advances one frame. <paramref name="wishDirection"/> is a world-space horizontal
	/// direction; its length is ignored. <paramref name="facingDirection"/> is the direction
	/// the player faces and is only used for the reference's forward-only wall bounce.
	/// </summary>
	public void Step(TimeSpan elapsed, Vector3 wishDirection, Vector3 facingDirection = default)
	{
		var dt = MathF.Min((float)elapsed.TotalSeconds, 0.05f);
		if (dt <= 0f)
		{
			return;
		}

		// A streamed static or a spawn can leave the capsule inside geometry. The reference
		// ignores collisions that were already present for the rest of the move; probing the
		// sides is the closest equivalent Bepu allows and also covers geometry that appears
		// without any input.
		if (TryUnstick())
		{
			Log.Info($"Unstuck player at ({Position.X:0.#}, {Position.Y:0.#}, {Position.Z:0.#}).");
		}

		var wish = wishDirection;
		wish.Y = 0f;
		if (wish.LengthSquared() > 1e-6f)
		{
			wish.Normalize();
		}

		var acceleration = Grounded ? GroundAcceleration : AirAcceleration;
		Velocity.X = MoveTowards(Velocity.X, wish.X * Speed, acceleration * dt);
		Velocity.Z = MoveTowards(Velocity.Z, wish.Z * Speed, acceleration * dt);

		var horizontal = new Vector3(Velocity.X, 0f, Velocity.Z) * dt;
		if (horizontal.LengthSquared() > 1e-8f && TryMove(horizontal) != MoveResult.Ok &&
		    IsForward(horizontal, facingDirection))
		{
			TryBounce(horizontal);
		}

		FollowGround(dt);
	}

	/// <summary>
	/// Moves by <paramref name="delta"/> in substeps no larger than <see cref="StepSize"/>.
	/// Each substep is a sweep; a blocked substep stops at the contact and the remaining
	/// displacement is dropped. That is the reference's <c>NpcItem::tryMove</c> behavior and
	/// is what keeps a wall from turning "backwards" into a sideways slide.
	/// </summary>
	public MoveResult TryMove(Vector3 delta)
	{
		var length = delta.Length();
		if (length < MinMove)
		{
			return MoveResult.Skip;
		}

		var start = Position;
		var count = 1;
		var horizontal = new Vector2(delta.X, delta.Z).Length();

		if (horizontal > StepSize || MathF.Abs(delta.Y) > HalfHeight)
		{
			var countXZ = (int)MathF.Ceiling(horizontal / StepSize);
			var countY = (int)MathF.Ceiling(MathF.Abs(delta.Y) / HalfHeight);
			count = Math.Max(countXZ, countY);
		}

		for (var i = 1; i <= count; ++i)
		{
			var target = start + delta * (i / (float)count);
			var step = target - Position;
			if (step.LengthSquared() < 1e-10f)
			{
				continue;
			}

			if (!Sweep(step, out var t, out var normal))
			{
				Position = target;
				continue;
			}

			if (t <= 0f)
			{
				// The capsule starts inside a collider. Bepu has no public destination-overlap
				// query, so the caller resolves it with TryUnstick on the next frame.
				LastHitNormal = Vector3.UnitY;
				return MoveResult.Fail;
			}

			// Stop just short of the surface. The step is not projected along the surface:
			// that is the slide the reference avoids at the movement layer.
			var safeT = MathF.Max(0f, t - Skin / step.Length());
			Position += step * safeT;
			LastHitNormal = normal;

			var moved = (Position - start).LengthSquared();
			return moved > MinMove * MinMove ? MoveResult.Partial : MoveResult.Fail;
		}

		return MoveResult.Ok;
	}

	/// <summary>
	/// Follows the ground with a downward ray from chest height, then snaps the feet onto it
	/// within <see cref="StepHeight"/>. A step up is just a snap: the chest capsule passes
	/// over anything below it, exactly like the reference's <c>HumShape</c>. Nothing below the
	/// step height is a fall.
	/// </summary>
	private void FollowGround(float dt)
	{
		var feetY = Position.Y - CenterOffset;
		var origin = new Vector3(Position.X, Position.Y + 1f, Position.Z);

		if (_world.RayCast(origin, -Vector3.UnitY, GroundProbeDistance, false, out var hit) &&
		    feetY - hit.Position.Y <= StepHeight)
		{
			var difference = feetY - hit.Position.Y;
			if (MathF.Abs(difference) > 1e-3f)
			{
				TryMove(new Vector3(0f, -difference, 0f));
			}

			Grounded = true;
			Velocity.Y = 0f;
			return;
		}

		Grounded = false;
		Velocity.Y = MathF.Max(Velocity.Y + Gravity * dt, TerminalVelocity);
		if (Velocity.Y * dt < 0f &&
		    TryMove(new Vector3(0f, Velocity.Y * dt, 0f)) != MoveResult.Ok)
		{
			Grounded = true;
			Velocity.Y = 0f;
		}
	}

	/// <summary>
	/// Port of the reference's <c>onMoveFailed</c> bounce: when a forward move is blocked by
	/// an oblique surface, try headings rotated by up to 35 degrees before giving up. Backward
	/// and strafing moves never bounce, so pressing S cannot turn into a strafe.
	/// </summary>
	private void TryBounce(Vector3 horizontal)
	{
		var direction = horizontal;
		direction.Y = 0f;
		if (direction.LengthSquared() < 1e-6f)
		{
			return;
		}

		direction.Normalize();
		var ortho = Vector3.Cross(direction, Vector3.UnitY);
		if (MathF.Abs(Vector3.Dot(ortho, LastHitNormal)) < 0.4f)
		{
			return;
		}

		var saved = Position;
		for (var degrees = 5; degrees <= 35; degrees += 5)
		{
			for (var side = -1; side <= 1; side += 2)
			{
				Position = saved;
				var radians = MathF.PI / 180f * degrees * side;
				var rotated = RotateY(horizontal, radians);

				if (TryMove(rotated) is MoveResult.Ok or MoveResult.Partial)
				{
					return;
				}
			}
		}

		Position = saved;
	}

	private static bool IsForward(Vector3 movement, Vector3 facing)
	{
		if (facing.LengthSquared() < 1e-6f)
		{
			return false;
		}

		facing.Y = 0f;
		facing.Normalize();

		movement.Y = 0f;
		if (movement.LengthSquared() < 1e-6f)
		{
			return false;
		}

		movement.Normalize();
		return Vector3.Dot(movement, facing) > 0.8f;
	}

	private static Vector3 RotateY(Vector3 value, float radians)
	{
		var cos = MathF.Cos(radians);
		var sin = MathF.Sin(radians);
		return new Vector3(
			value.X * cos + value.Z * sin,
			value.Y,
			-value.X * sin + value.Z * cos);
	}

	/// <summary>
	/// Pushes the capsule out of a static it overlaps. Probing the capsule's sides with short
	/// rays finds the shallowest exit; the streaming loader also defers a source whose bounds
	/// would enclose the capsule, so this is a last resort.
	/// </summary>
	private bool TryUnstick()
	{
		const float reach = DefaultRadius + 1f;

		var bestPush = float.MaxValue;
		var bestDirection = Vector3.Zero;

		ConsiderPenetration(Position, Vector3.UnitX, reach, ref bestPush, ref bestDirection);
		ConsiderPenetration(Position, -Vector3.UnitX, reach, ref bestPush, ref bestDirection);
		ConsiderPenetration(Position, Vector3.UnitZ, reach, ref bestPush, ref bestDirection);
		ConsiderPenetration(Position, -Vector3.UnitZ, reach, ref bestPush, ref bestDirection);

		if (bestPush >= float.MaxValue)
		{
			return false;
		}

		Position += bestDirection * bestPush;
		Velocity = Vector3.Zero;
		return true;
	}

	private void ConsiderPenetration(Vector3 origin, Vector3 direction, float reach, ref float bestPush,
		ref Vector3 bestDirection)
	{
		if (!_world.RayCast(origin, direction, reach, false, out var hit) ||
		    hit.Distance >= DefaultRadius - 0.01f)
		{
			return;
		}

		var push = DefaultRadius - hit.Distance + Skin;
		if (push < bestPush)
		{
			bestPush = push;
			bestDirection = -direction;
		}
	}

	/// <summary>
	/// Sweeps the capsule along <paramref name="delta"/>, which is the whole displacement for
	/// this call (the sweep runs for one unit of time at the delta's velocity).
	/// </summary>
	private bool Sweep(Vector3 delta, out float t, out Vector3 normal)
	{
		t = 1f;
		normal = Vector3.UnitY;

		if (delta.LengthSquared() < 1e-8f)
		{
			return false;
		}

		var handler = new SweepHitHandler
		{
			Surfaces = _world.Surfaces,
			Normal = Num.Vector3.UnitY
		};

		var pose = new RigidPose(ToNumerics(Position));
		var velocity = new BodyVelocity { Linear = ToNumerics(delta) };
		_world.Simulation.Sweep(_shape, pose, velocity, 1f, _world.BufferPool, ref handler);

		if (!handler.Hit)
		{
			return false;
		}

		t = Math.Clamp(handler.T, 0f, 1f);
		normal = handler.T <= 0f ? Vector3.UnitY : ToXna(handler.Normal);
		return true;
	}

	private static float MoveTowards(float current, float target, float maxDelta)
	{
		var delta = target - current;
		if (MathF.Abs(delta) <= maxDelta)
		{
			return target;
		}

		return current + MathF.Sign(delta) * maxDelta;
	}

	private static Num.Vector3 ToNumerics(Vector3 value) => new(value.X, value.Y, value.Z);

	private static Vector3 ToXna(Num.Vector3 value) => new(value.X, value.Y, value.Z);

	private struct SweepHitHandler : ISweepHitHandler
	{
		public CollidableProperty<PhysicsSurface> Surfaces;
		public bool Hit;
		public float T;
		public Num.Vector3 Normal;

		public bool AllowTest(CollidableReference collidable) =>
			Surfaces[collidable].Kind != PhysicsSurfaceKind.Sensor;

		public bool AllowTest(CollidableReference collidable, int child) => AllowTest(collidable);

		public void OnHit(ref float maximumT, float t, in Num.Vector3 hitLocation, in Num.Vector3 hitNormal,
			CollidableReference collidable)
		{
			if (t < maximumT)
			{
				maximumT = t;
			}

			if (!Hit || t < T)
			{
				Hit = true;
				T = t;
				Normal = hitNormal;
			}
		}

		public void OnHitAtZeroT(ref float maximumT, CollidableReference collidable)
		{
			maximumT = 0f;
			Hit = true;
			T = 0f;
		}
	}
}
