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
	private readonly GameClock _clock;
	private readonly List<NpcActor> _actors;
	private readonly NpcWalker _walker;
	private readonly NpcAiSystem _ai;
	private readonly Func<string, bool> _hasWaypoint;
	private int _assignLogs;
	private int _arriveLogs;

	public NpcRoutineSystem(WorldGrid world, PhysicsWorld physics, GameClock clock,
		List<NpcActor> actors, NpcAiSystem ai = null)
	{
		_world = world;
		_clock = clock;
		_actors = actors;
		_walker = new NpcWalker(world, physics);
		_ai = ai;
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
			else if (!actor.HasAiMove && actor.BodyState == NpcActor.BodyStateStand)
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
		actor.RoutineStateStarted = false;
		actor.HasAiMove = false;
		actor.Path.Clear();
		actor.PathIndex = 0;

		if (changed)
		{
			// The previous routine's script state belongs to the old schedule.
			_ai?.End(actor);
		}

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
			var index = actor.Path[actor.PathIndex];
			var target = new Vector3(_world.WaypointList[index].Position.X,
				_world.WaypointGroundY(index), _world.WaypointList[index].Position.Z);

			var moved = _walker.MoveTowards(actor, target, remaining, out var arrived);
			remaining -= moved;

			if (arrived)
			{
				++actor.PathIndex;
				continue;
			}

			if (moved <= 0.0001f)
			{
				// Blocked by geometry or another body: keep the point and retry next frame.
				actor.SetMovingClip(false);
				return;
			}
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

			StartRoutineState(actor);
		}
	}

	/// <summary>
	/// Starts the routine's <c>action</c> script state once, after the NPC has reached its
	/// point. The reference starts it immediately and lets the state walk (<c>AI_GotoWP</c>
	/// arrives with F3.2); until then the F2 walker moves the body and the state runs on
	/// arrival.
	/// </summary>
	private void StartRoutineState(NpcActor actor)
	{
		if (actor.RoutineStateStarted)
		{
			return;
		}

		var routine = actor.Routine;
		if (routine == null || routine.Action <= 0)
		{
			return;
		}

		actor.RoutineStateStarted = true;
		_ai?.StartRoutine(actor, routine.Action, routine.Waypoint);
	}
}
