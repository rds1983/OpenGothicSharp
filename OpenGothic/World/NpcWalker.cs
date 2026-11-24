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

	private readonly WorldGrid _world;
	private readonly PhysicsWorld _physics;

	public NpcWalker(WorldGrid world, PhysicsWorld physics)
	{
		_world = world;
		_physics = physics;
	}

	/// <summary>
	/// Moves toward <paramref name="target"/> for at most <c>speed * dt</c> units and returns
	/// true once the actor is within <see cref="ArriveDistance"/> of it. Y is interpolated
	/// toward the target, which callers ground when they resolve a waypoint.
	/// </summary>
	public bool Step(NpcActor actor, Vector3 target, float dt, float speed)
	{
		var delta = target - actor.Position;
		var distance = delta.Length();

		if (distance <= ArriveDistance)
		{
			actor.SetMovingClip(false);
			return true;
		}

		var step = MathF.Min(speed * dt, distance);
		var direction = delta / distance;
		var next = actor.Position + direction * step;
		next.Y = actor.Position.Y + (target.Y - actor.Position.Y) * (step / distance);

		var yaw = MathF.Atan2(direction.X, -direction.Z);
		actor.SetPosition(next, yaw);
		actor.SetMovingClip(true);
		Apply(actor);

		return distance - step <= ArriveDistance;
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
