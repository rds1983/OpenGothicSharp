using Microsoft.Xna.Framework;
using OpenGothic.Scripts;
using System.Collections.Generic;
using ZenKit.Daedalus;

namespace OpenGothic;

/// <summary>
/// Runs script AI states (<c>ZS_*</c>). A state comes from the routine's <c>action</c> or from
/// an <c>AI_StartState</c> call; starting it calls the entry function once, then the
/// <c>_Loop</c> sibling on a fixed interval, and the <c>_End</c> sibling when it is replaced.
/// The reference does the same in <c>Npc::tickRoutine</c> and <c>Npc::clearState</c>.
/// </summary>
internal sealed class NpcAiSystem
{
	private const double LoopInterval = 0.5;
	private const float NearDistance = 3000f;
	private const int MaxStartLogs = 40;
	private const int MaxEndLogs = 40;
	private const int MaxQueueDrain = 64;

	private readonly ScriptRuntime _scripts;
	private readonly List<NpcActor> _actors;
	private readonly Dictionary<int, NpcActor> _bySymbol = new Dictionary<int, NpcActor>();

	// AI_StartState is called from inside a state function, so the request is queued instead of
	// re-entering the VM mid-call. Both lists are drained at the start of the next update.
	private readonly List<(NpcActor Actor, int Function, string Waypoint)> _starts =
		new List<(NpcActor, int, string)>();
	private readonly List<NpcActor> _ends = new List<NpcActor>();

	private double _time;
	private int _startLogs;
	private int _endLogs;

	public NpcAiSystem(ScriptRuntime scripts, List<NpcActor> actors)
	{
		_scripts = scripts;
		_actors = actors ?? new List<NpcActor>();

		foreach (var actor in _actors)
		{
			if (actor.SymbolIndex > 0)
			{
				_bySymbol[actor.SymbolIndex] = actor;
			}
		}
	}

	/// <summary>Backs <c>NPC_IsInState</c>: whether the NPC currently runs this state function.</summary>
	internal bool IsInState(NpcInstance npc, int stateFunction)
	{
		if (npc == null || !_bySymbol.TryGetValue(npc.Index, out var actor))
		{
			return false;
		}

		return actor.State != null && actor.State.Function == stateFunction;
	}

	/// <summary>Backs <c>AI_StartState</c>; the state starts on the next AI update.</summary>
	internal void RequestStart(NpcInstance npc, int function, string waypoint)
	{
		if (npc == null || function <= 0 || !_bySymbol.TryGetValue(npc.Index, out var actor))
		{
			return;
		}

		_starts.Add((actor, function, waypoint ?? string.Empty));
	}

	/// <summary>Starts the routine's action once the NPC has reached its routine point.</summary>
	public void StartRoutine(NpcActor actor, int function, string waypoint)
	{
		if (actor == null || function <= 0)
		{
			return;
		}

		_starts.Add((actor, function, waypoint ?? string.Empty));
	}

	/// <summary>Ends the actor's state on the next update (the routine changed).</summary>
	public void End(NpcActor actor)
	{
		if (actor != null)
		{
			_ends.Add(actor);
		}
	}

	public void Update(float dt, Vector3 center)
	{
		if (dt <= 0f)
		{
			return;
		}

		_time += dt;

		DrainQueues();

		if (_actors.Count == 0)
		{
			return;
		}

		// Only nearby NPCs tick: 356 states polling scripts every half second would spend the
		// frame in the VM. F3.4 replaces this with the reference's process policies.
		var nearSquared = NearDistance * NearDistance;
		foreach (var actor in _actors)
		{
			var state = actor.State;
			if (state == null ||
			    Vector3.DistanceSquared(actor.Position, center) > nearSquared ||
			    !state.TryRunLoop(_time, LoopInterval))
			{
				continue;
			}

			var result = _scripts.InvokeState(actor.Instance, null, null, state.LoopFunction);
			if (NpcAiState.ShouldEnd(result))
			{
				ClearState(actor);
			}
		}
	}

	/// <summary>
	/// Applies queued end/start requests. A state's entry function can call
	/// <c>AI_StartState</c> again (e.g. <c>ZS_GotoBed</c> starting <c>ZS_Sleep</c>), so the
	/// queue is drained until empty; the guard breaks a script that keeps restarting itself.
	/// </summary>
	private void DrainQueues()
	{
		for (var i = 0; i < MaxQueueDrain; ++i)
		{
			if (_ends.Count > 0)
			{
				var actor = _ends[0];
				_ends.RemoveAt(0);
				ClearState(actor);
				continue;
			}

			if (_starts.Count == 0)
			{
				return;
			}

			var start = _starts[0];
			_starts.RemoveAt(0);
			Start(start.Actor, start.Function, start.Waypoint);
		}
	}

	private void Start(NpcActor actor, int function, string waypoint)
	{
		if (_scripts == null ||
		    !_scripts.TryGetStateFunctions(function, out var name, out var loop, out var end))
		{
			return;
		}

		if (actor.State != null && actor.State.Function == function)
		{
			SetWaypoint(actor, waypoint);
			return;
		}

		ClearState(actor);

		actor.State = new NpcAiState
		{
			Function = function,
			LoopFunction = loop,
			EndFunction = end,
			Name = name,
			StartedAt = _time,
			NextLoopAt = _time + LoopInterval
		};

		SetWaypoint(actor, waypoint);

		if (_startLogs++ < MaxStartLogs)
		{
			var siblings = loop >= 0
				? (end >= 0 ? "loop and end functions" : "loop function")
				: (end >= 0 ? "end function" : "no loop function");
			Log.Info($"NPC state '{actor.Vob.Name}': '{name}' started ({siblings}).");
		}

		_scripts.InvokeState(actor.Instance, null, null, function);
	}

	/// <summary>Runs the end function, if any, and forgets the state.</summary>
	private void ClearState(NpcActor actor)
	{
		var state = actor.State;
		if (state == null)
		{
			return;
		}

		actor.State = null;

		if (state.EndFunction >= 0 && _scripts != null)
		{
			_scripts.InvokeState(actor.Instance, null, null, state.EndFunction);
		}

		if (_endLogs++ < MaxEndLogs)
		{
			Log.Info($"NPC state '{actor.Vob.Name}': '{state.Name}' ended.");
		}
	}

	/// <summary>
	/// The reference stores the waypoint on the instance as <c>self.wp</c> before the state
	/// function runs; states such as <c>ZS_*</c> read it instead of taking an argument.
	/// </summary>
	private static void SetWaypoint(NpcActor actor, string waypoint)
	{
		if (!string.IsNullOrEmpty(waypoint) && actor.Instance != null)
		{
			actor.Instance.Wp = waypoint;
		}
	}
}
