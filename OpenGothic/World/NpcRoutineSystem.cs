using Microsoft.Xna.Framework;
using OpenGothic.Physics;
using OpenGothic.Vobs;
using System;
using System.Collections.Generic;

namespace OpenGothic;

/// <summary>
/// Selects each NPC's authored daily routine from the game clock and walks the actor along
/// the waynet. Movement is direct between connected waypoints; the capsule and vob bounds are
/// updated with the visual so focus/collision follow the body.
/// </summary>
internal sealed class NpcRoutineSystem
{
	private const float NearDistance = 3000f;
	private const float MoveSpeed = 300f;
	private const float ArriveDistance = 16f;
	private const int MaxLogs = 12;

	private readonly WorldGrid _world;
	private readonly PhysicsWorld _physics;
	private readonly GameClock _clock;
	private readonly List<NpcActor> _actors;
	private readonly Func<string, bool> _hasWaypoint;
	private int _assignLogs;
	private int _arriveLogs;

	public NpcRoutineSystem(WorldGrid world, PhysicsWorld physics, GameClock clock,
		List<NpcActor> actors)
	{
		_world = world;
		_physics = physics;
		_clock = clock;
		_actors = actors;
		_hasWaypoint = world != null
			? new Func<string, bool>(world.Waypoints.ContainsKey)
			: _ => false;
	}

	public void Update(float dt, Vector3 center)
	{
		if (_world == null || _clock == null || _actors == null || dt <= 0f)
		{
			return;
		}

		var nearSquared = NearDistance * NearDistance;
		foreach (var actor in _actors)
		{
			if (!actor.HasModel || actor.Vob.Routines.Count == 0 ||
			    Vector3.DistanceSquared(actor.Position, center) > nearSquared)
			{
				continue;
			}

			var routine = NpcRoutine.Select(actor.Vob.Routines, _clock.Hour, _hasWaypoint);
			if (routine == null)
			{
				continue;
			}

			if (!ReferenceEquals(actor.Routine, routine))
			{
				Assign(actor, routine);
			}

			if (actor.Moving)
			{
				Move(actor, dt);
			}
			else
			{
				actor.SetMovingClip(false);
			}
		}
	}

	private void Assign(NpcActor actor, NpcRoutine routine)
	{
		var changed = actor.Routine != null;
		actor.Routine = routine;
		actor.RoutineChanged = changed;
		actor.Path.Clear();
		actor.PathIndex = 0;

		if (!_world.WaypointIndices.TryGetValue(routine.Waypoint, out var goal))
		{
			actor.SetMovingClip(false);
			return;
		}

		var start = _world.FindNearestWaypoint(actor.Position);
		var path = start >= 0 ? _world.FindWaypointPath(start, goal) : null;
		if (path == null)
		{
			// No connected path from the current spot; walk straight to the routine point.
			path = new List<int> { goal };
		}

		actor.Path.AddRange(path);
		actor.SetMovingClip(true);

		if (changed && _assignLogs++ < MaxLogs)
		{
			Log.Info(
				$"NPC routine '{actor.Vob.Name}' ({_clock.Hour:00.00}h): " +
				$"'{routine.Waypoint}' via {path.Count} waypoint(s).");
		}
	}

	private void Move(NpcActor actor, float dt)
	{
		var remaining = MoveSpeed * dt;
		while (remaining > 0f && actor.PathIndex < actor.Path.Count)
		{
			var target = _world.WaypointList[actor.Path[actor.PathIndex]];
			var targetPosition = new Vector3(target.Position.X,
				_world.WaypointGroundY(actor.Path[actor.PathIndex]), target.Position.Z);
			var delta = targetPosition - actor.Position;
			var distance = delta.Length();

			if (distance <= ArriveDistance)
			{
				++actor.PathIndex;
				continue;
			}

			var step = MathF.Min(remaining, distance);
			var direction = delta / distance;
			var next = actor.Position + direction * step;
			next.Y = actor.Position.Y + (targetPosition.Y - actor.Position.Y) * (step / distance);

			var yaw = MathF.Atan2(direction.X, -direction.Z);
			actor.SetPosition(next, yaw);
			UpdateWorld(actor);
			remaining -= step;
		}

		if (actor.PathIndex >= actor.Path.Count)
		{
			actor.SetMovingClip(false);
			if (actor.RoutineChanged && _arriveLogs++ < MaxLogs)
			{
				actor.RoutineChanged = false;
				Log.Info(
					$"NPC routine '{actor.Vob.Name}' ({_clock.Hour:00.00}h): " +
					$"arrived at '{actor.Routine?.Waypoint}' " +
					$"(y {actor.Position.Y:0.#}).");
			}
		}
	}

	private void UpdateWorld(NpcActor actor)
	{
		var vob = actor.Vob;
		var oldBounds = vob.Bounds;

		vob.World = Matrix.CreateScale(actor.Scale) *
		            Matrix.CreateRotationY(-actor.Yaw) *
		            Matrix.CreateTranslation(actor.Position);
		vob.Local = vob.World;

		var newBounds = actor.WorldBounds;
		vob.Bounds = newBounds;
		_world.UpdateVobBounds(vob, oldBounds, newBounds);

		if (_physics != null && vob.PhysicsBody.HasValue)
		{
			_physics.SetNpcCapsulePosition(vob.PhysicsBody.Value, actor.Position);
		}
	}
}
