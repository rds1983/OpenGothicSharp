using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using Microsoft.Xna.Framework;
using OpenGothic.Physics;
using System;
using Num = System.Numerics;

namespace OpenGothic.Physics;

/// <summary>
/// A kinematic capsule that walks the static world. Movement is swept and slid against the
/// collision set rather than solved as a dynamic body: that keeps the pose upright and makes
/// slopes, wall slide and step-up explicit instead of emerging from contact jitter.
/// </summary>
/// <remarks>
/// Positions are the capsule's centre, in the game's centimetre units. The capsule is always
/// upright, so the bottom is <see cref="HalfHeight"/> below the centre.
/// </remarks>
public sealed class CharacterController
{
	public const float DefaultRadius = 30f;
	public const float DefaultLength = 110f;

	private const float Gravity = -981f;
	private const float WalkSpeed = 300f;
	private const float GroundAcceleration = 3000f;
	private const float AirAcceleration = 800f;
	private const float StepHeight = 35f;
	private const float SnapDistance = 12f;
	private const float Skin = 0.5f;

	/// <summary>Gap kept between the capsule's bottom and the ground it stands on.</summary>
	public const float GroundClearance = 0.5f;

	/// <summary>Normal Y above which a surface counts as ground rather than a wall.</summary>
	private const float MaxSlopeCos = 0.55f;

	/// <summary>Normal Y below which a vertical obstacle is worth stepping over.</summary>
	private const float WallNormalY = 0.7f;

	private readonly PhysicsWorld _world;
	private readonly Capsule _shape;

	public CharacterController(PhysicsWorld world)
	{
		_world = world ?? throw new ArgumentNullException(nameof(world));
		_shape = new Capsule(DefaultRadius, DefaultLength);
	}

	/// <summary>Capsule centre in world space.</summary>
	public Vector3 Position;

	public Vector3 Velocity;

	public bool Grounded { get; private set; }

	/// <summary>Number of successful steps, for the self-check log.</summary>
	public int StepSuccesses { get; private set; }

	/// <summary>Why the last step attempt failed, for the self-check log.</summary>
	public StepFailure LastStepFailure { get; private set; }

	public enum StepFailure
	{
		None,
		NoGround,
		TooSteep,
		TooLow,
		TooHigh,
		NoHeadroom,
	}

	public float Radius => DefaultRadius;

	public float Length => DefaultLength;

	public float HalfHeight => Radius + Length * 0.5f;

	/// <summary>
	/// Advances one frame. <paramref name="wishDirection"/> is a world-space horizontal
	/// direction; its length is ignored.
	/// </summary>
	public void Step(TimeSpan elapsed, Vector3 wishDirection)
	{
		var dt = MathF.Min((float)elapsed.TotalSeconds, 0.05f);
		if (dt <= 0f)
		{
			return;
		}

		// A loaded static can land around the player; resolve that before doing any movement so
		// the capsule does not need a key press to escape an overlap.
		if (TryUnstick(Vector3.Zero))
		{
			Log.Info($"Unstuck player at ({Position.X:0.#}, {Position.Y:0.#}, {Position.Z:0.#}).");
		}

		Grounded = ProbeGround(out _);

		// A grounded body is pressed into the ground so slopes are followed without
		// accumulating gravity every frame; otherwise gravity applies normally.
		Velocity.Y = Grounded ? -2f : Velocity.Y + Gravity * dt;

		var wish = wishDirection;
		wish.Y = 0f;
		if (wish.LengthSquared() > 1e-6f)
		{
			wish.Normalize();
		}

		var acceleration = Grounded ? GroundAcceleration : AirAcceleration;
		Velocity.X = MoveTowards(Velocity.X, wish.X * WalkSpeed, acceleration * dt);
		Velocity.Z = MoveTowards(Velocity.Z, wish.Z * WalkSpeed, acceleration * dt);

		// A grounded body moves horizontally and then follows the ground. Integrating gravity
		// while grounded would project the downward step onto a slope and make the capsule
		// creep downhill with no input; the snap below is what keeps it on the surface.
		if (Grounded)
		{
			Velocity.Y = 0f;
			MoveWithSlide(new Vector3(Velocity.X, 0f, Velocity.Z) * dt);
		}
		else
		{
			Velocity.Y += Gravity * dt;
			MoveWithSlide(Velocity * dt);
		}

		// Follow the ground after the horizontal move. Only snap downward: going up is what
		// the step assist is for, and a ray should not teleport the capsule onto a ledge.
		if (Grounded)
		{
			if (!ProbeGround(out var support))
			{
				Grounded = false;
			}
			else
			{
				var targetY = support.Position.Y + HalfHeight + GroundClearance;
				var difference = targetY - Position.Y;

				// Follow the ground down a slope, and correct the small sinking caused by the
				// horizontal slide. A rise is limited to the clearance: a real step is the
				// step assist's job.
				if (difference <= GroundClearance + 1f && difference >= -SnapDistance)
				{
					Position.Y = targetY;
					if (Velocity.Y < 0f)
					{
						Velocity.Y = 0f;
					}
				}
				else if (difference < -SnapDistance)
				{
					Grounded = false;
				}
			}
		}
	}

	private bool ProbeGround(out PhysicsRayHit hit)
	{
		var origin = Position - Vector3.UnitY * HalfHeight + Vector3.UnitY * 2f;
		return _world.RayCast(origin, -Vector3.UnitY, SnapDistance + 5f, false, out hit) &&
		       hit.Normal.Y >= MaxSlopeCos;
	}

	/// <summary>
	/// Sweeps the frame's displacement, sliding along whatever it hits and trying a step when
	/// a wall blocks a grounded body. Four iterations cover a corner without turning the frame
	/// into a search.
	/// </summary>
	private void MoveWithSlide(Vector3 delta)
	{
		var canStep = Grounded;

		for (var iteration = 0; iteration < 6; ++iteration)
		{
			var length = delta.Length();
			if (length < 1e-4f)
			{
				return;
			}

			if (!Sweep(delta, out var t, out var normal))
			{
				Position += delta;
				return;
			}

			if (t <= 0f)
			{
				// The shape starts inside something. A sweep cannot say which way is out, so
				// resolve the overlap with short rays and retry instead of treating the hit as
				// a ground contact and freezing the capsule.
				if (!TryUnstick(delta))
				{
					return;
				}

				continue;
			}

			// Stop just short of the surface; starting the next sweep exactly on it would
			// report a zero-time hit every frame.
			var safeT = MathF.Max(0f, t - Skin / length);
			Position += delta * safeT;

			if (normal.Y >= MaxSlopeCos && delta.Y <= 0f)
			{
				Grounded = true;
				Velocity.Y = 0f;
			}

			if (canStep && normal.Y < WallNormalY && TryStep(delta * (1f - t)))
			{
				return;
			}

			var remaining = delta * (1f - t);
			var into = Vector3.Dot(remaining, normal);
			if (into < 0f)
			{
				remaining -= normal * into;
			}

			delta = remaining;
			canStep = false;
		}
	}

	/// <summary>
	/// Pushes the capsule out of a static it overlaps. When a streaming load or a seam puts
	/// geometry around the player, every sweep starts at zero time and the controller cannot
	/// move; probing the four sides of the lower sphere finds the shallowest exit. If nothing
	/// is actually penetrating, step back along the attempted movement and try again.
	/// </summary>
	private bool TryUnstick(Vector3 delta)
	{
		var lower = Position - Vector3.UnitY * (HalfHeight - Radius);

		if (TryPush(lower, Vector3.UnitX) ||
		    TryPush(lower, -Vector3.UnitX) ||
		    TryPush(lower, Vector3.UnitZ) ||
		    TryPush(lower, -Vector3.UnitZ) ||
		    TryPush(Position, Vector3.UnitX) ||
		    TryPush(Position, -Vector3.UnitX) ||
		    TryPush(Position, Vector3.UnitZ) ||
		    TryPush(Position, -Vector3.UnitZ))
		{
			Velocity = Vector3.Zero;
			return true;
		}

		var length = delta.Length();
		if (length > 1e-4f)
		{
			Position -= delta / length * Skin;
			Velocity = Vector3.Zero;
			return true;
		}

		return false;
	}

	private bool TryPush(Vector3 origin, Vector3 direction)
	{
		var reach = Radius + 1f;
		if (!_world.RayCast(origin, direction, reach, false, out var hit) ||
		    hit.Distance >= Radius - 0.01f)
		{
			return false;
		}

		Position -= direction * (Radius - hit.Distance + Skin);
		return true;
	}

	/// <summary>
	/// Tries to climb an obstacle by probing for a walkable surface within
	/// <see cref="StepHeight"/> just past the capsule's front. Shape sweeps cannot do this
	/// reliably: the lower sphere grazes the step's top corner on the way up.
	/// </summary>
	private bool TryStep(Vector3 horizontal)
	{
		var direction = horizontal;
		direction.Y = 0f;
		if (direction.LengthSquared() < 1e-4f)
		{
			return false;
		}

		direction.Normalize();

		var foot = Position - Vector3.UnitY * HalfHeight;
		var probe = new Vector3(
			foot.X + direction.X * (Radius + 4f),
			foot.Y + StepHeight + 6f,
			foot.Z + direction.Z * (Radius + 4f));

		if (!_world.RayCast(probe, -Vector3.UnitY, StepHeight + 12f, false, out var hit))
		{
			LastStepFailure = StepFailure.NoGround;
			return false;
		}

		if (hit.Normal.Y < MaxSlopeCos)
		{
			LastStepFailure = StepFailure.TooSteep;
			return false;
		}

		var rise = hit.Position.Y - foot.Y;
		if (rise < 2f)
		{
			LastStepFailure = StepFailure.TooLow;
			return false;
		}

		if (rise > StepHeight)
		{
			LastStepFailure = StepFailure.TooHigh;
			return false;
		}

		var saved = Position;
		Position = new Vector3(probe.X, hit.Position.Y + HalfHeight + GroundClearance, probe.Z);

		// The capsule must fit above the step; a low ceiling is not a stair.
		if (Sweep(Vector3.UnitY * (StepHeight + 5f), out _, out _))
		{
			Position = saved;
			LastStepFailure = StepFailure.NoHeadroom;
			return false;
		}

		LastStepFailure = StepFailure.None;
		++StepSuccesses;
		Grounded = true;
		Velocity.Y = 0f;
		return true;
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
