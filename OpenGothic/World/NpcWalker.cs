using Microsoft.Xna.Framework;
using OpenGothic.Physics;
using System;

namespace OpenGothic;

/// <summary>
/// Moves an NPC actor and keeps everything that follows it in sync: the vob transform and
/// bounds, the grid cell it renders in, and its kinematic capsule. Shared by the routine walker
/// and script-driven movement (<c>AI_GotoWP</c>).
/// </summary>
internal sealed class NpcWalker
{
	public const float DefaultSpeed = 300f;
	public const float ArriveDistance = 16f;

	/// <summary>How far up a step the ground ray may find a floor.</summary>
	private const float StepHeight = 60f;

	private readonly WorldGrid _world;
	private readonly PhysicsWorld _physics;

	public NpcWalker(WorldGrid world, PhysicsWorld physics)
	{
		_world = world;
		_physics = physics;
	}

	/// <summary>
	/// Moves toward <paramref name="target"/> for at most <c>speed * dt</c> units and returns
	/// true once the actor is within <see cref="ArriveDistance"/> of it.
	/// </summary>
	public bool Step(NpcActor actor, Vector3 target, float dt, float speed)
	{
		var moved = MoveTowards(actor, target, speed * dt, out var arrived);

		if (arrived)
		{
			actor.SetMovingClip(false);
		}
		else if (moved > 0.0001f)
		{
			actor.SetMovingClip(true);
		}

		return arrived;
	}

	/// <summary>
	/// Moves up to <paramref name="maxStep"/> units toward <paramref name="target"/>, sweeping
	/// the capsule so walls and other bodies stop the body, then snaps it onto the floor under
	/// the new position. Returns the distance actually travelled and whether the target was
	/// reached; a blocked move reports 0 so callers stop instead of grinding into the obstacle.
	/// </summary>
	public float MoveTowards(NpcActor actor, Vector3 target, float maxStep, out bool arrived)
	{
		arrived = false;

		var delta = target - actor.Position;
		var distance = delta.Length();
		if (distance <= ArriveDistance)
		{
			arrived = true;
			return 0f;
		}

		var step = MathF.Min(maxStep, distance);
		if (step <= 0f)
		{
			return 0f;
		}

		var direction = delta / distance;
		var move = new Vector3(direction.X, 0f, direction.Z) * step;

		if (_physics != null &&
		    _physics.SweepNpcCapsule(actor.Position, move, actor.Vob.PhysicsBody,
			    out var t, out _))
		{
			// Keep a small gap so the next sweep does not start inside the surface.
			move *= MathF.Max(0f, t - 0.02f);
			if (move.LengthSquared() < 1f)
			{
				return 0f;
			}
		}

		var next = actor.Position + move;
		next.Y = GroundY(actor, next);

		var yaw = actor.Yaw;
		var horizontal = new Vector2(move.X, move.Z).Length();
		if (horizontal > 0.0001f)
		{
			yaw = MathF.Atan2(move.X, -move.Z);
		}

		actor.SetPosition(next, yaw);
		Apply(actor);

		arrived = Vector3.Distance(next, target) <= ArriveDistance;
		return move.Length();
	}

	/// <summary>
	/// Height of the floor under <paramref name="position"/>. The physics ray sees vob floors
	/// and steps near the player; the terrain triangles are the fallback outside the streamed
	/// area. The actor's own capsule is ignored.
	/// </summary>
	private float GroundY(NpcActor actor, Vector3 position)
	{
		if (_physics != null &&
		    _physics.RayCast(position + Vector3.UnitY * StepHeight, -Vector3.UnitY,
			    StepHeight + WorldGrid.GroundSnap, false, out var hit, actor.Vob.PhysicsBody))
		{
			return hit.Position.Y;
		}

		if (_world != null && _world.TryGetGround(position, WorldGrid.GroundSnap, out var ground))
		{
			return ground.Y;
		}

		return position.Y;
	}

	/// <summary>Writes the actor's transform into its vob, grid cell and capsule.</summary>
	public void Apply(NpcActor actor)
	{
		var vob = actor.Vob;
		var oldBounds = vob.Bounds;

		vob.World = Matrix.CreateScale(actor.Scale) *
		            Matrix.CreateRotationY(-actor.Yaw) *
		            Matrix.CreateTranslation(actor.Position);
		vob.Local = vob.World;

		var newBounds = actor.WorldBounds;
		vob.Bounds = newBounds;
		_world?.UpdateVobBounds(vob, oldBounds, newBounds);

		if (_physics != null && vob.PhysicsBody.HasValue)
		{
			_physics.SetNpcCapsulePosition(vob.PhysicsBody.Value, actor.Position);
		}
	}
}
