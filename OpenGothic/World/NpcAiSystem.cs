using Microsoft.Xna.Framework;
using OpenGothic.Physics;
using OpenGothic.Scripts;
using OpenGothic.Vobs;
using System;
using System.Collections.Generic;
using ZenKit.Daedalus;

namespace OpenGothic;

/// <summary>
/// Runs script AI states (<c>ZS_*</c>). A state comes from the routine's <c>action</c> or from
/// an <c>AI_StartState</c> call; starting it calls the entry function once, then the
/// <c>_Loop</c> sibling on a fixed interval, and the <c>_End</c> sibling when it is replaced.
/// The reference does the same in <c>Npc::tickRoutine</c> and <c>Npc::clearState</c>.
/// <para>
/// It also backs the script queries and actions the states call (<c>NPC_GetDistToWP</c>,
/// <c>AI_GotoWP</c>, <c>AI_PlayAni</c>, …), because those need the actor list and the world.
/// </para>
/// </summary>
internal sealed class NpcAiSystem
{
	private const double LoopInterval = 0.5;
	private const int MaxStartLogs = 40;
	private const int MaxEndLogs = 40;
	private const int MaxQueueDrain = 64;
	private const float WaypointReach = 165f;
	private const float MobSearchDistanceSquared = 1000f * 1000f;
	private const double RoutineRestartSeconds = 1.0;
	private const int PercAssessPlayer = 1;
	private const int SenseSee = 1;
	private const int MaxPerceptionLogs = 20;
	private const float EyeHeight = 120f;
	private const float PlayerCenterHeight = 100f;
	private const float NormalDistance = 3000f;
	private const float FarDistance = 6000f;
	private const double FarLoopInterval = 3.0;

	// The reference's view test accepts an offset of up to 80 degrees from the facing
	// (canRayHitPoint compares cos(180 - angle) against cos(100 degrees)).
	private const float ViewConeCos = 0.17364818f;

	private readonly ScriptRuntime _scripts;
	private readonly List<NpcActor> _actors;
	private readonly Dictionary<int, NpcActor> _bySymbol = new Dictionary<int, NpcActor>();
	private readonly WorldGrid _world;
	private readonly NpcWalker _walker;
	private readonly PhysicsWorld _physics;
	private readonly GameClock _clock;
	private readonly int[] _perceptionRanges = new int[NpcActor.PerceptionCount];
	private readonly HashSet<string> _reported = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

	// AI_StartState is called from inside a state function, so the request is queued instead of
	// re-entering the VM mid-call. Both lists are drained at the start of the next update.
	private readonly List<(NpcActor Actor, int Function, string Waypoint)> _starts =
		new List<(NpcActor, int, string)>();
	private readonly List<NpcActor> _ends = new List<NpcActor>();
	private readonly HashSet<NpcActor> _endedThisTick = new HashSet<NpcActor>();

	private double _time;
	private int _startLogs;
	private int _endLogs;
	private int _perceptionLogs;

	public NpcAiSystem(ScriptRuntime scripts, List<NpcActor> actors, WorldGrid world,
		PhysicsWorld physics, GameClock clock = null)
	{
		_scripts = scripts;
		_actors = actors ?? new List<NpcActor>();
		_world = world;
		_physics = physics;
		_clock = clock;
		_walker = new NpcWalker(world, physics);

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

	/// <summary>Whether an instance is a live script NPC (the hero counts as one).</summary>
	internal bool IsValidNpc(NpcInstance npc)
	{
		return npc != null &&
		       (_bySymbol.ContainsKey(npc.Index) || (_scripts?.Hero != null && npc.Index == _scripts.Hero.Index));
	}

	/// <summary>Backs <c>NPC_GetNearestWP</c>.</summary>
	internal string NearestWaypoint(NpcInstance npc)
	{
		var actor = Find(npc);
		if (actor == null || _world == null)
		{
			return string.Empty;
		}

		var index = _world.FindNearestWaypoint(actor.Position);
		return index >= 0 ? _world.WaypointList[index].Name : string.Empty;
	}

	/// <summary>Backs <c>NPC_GetDistToWP</c>; the reference returns whole world units.</summary>
	internal int DistanceToWaypoint(NpcInstance npc, string waypoint)
	{
		var actor = Find(npc);
		if (actor == null || string.IsNullOrEmpty(waypoint) || _world == null ||
		    !_world.Waypoints.TryGetValue(waypoint, out var point))
		{
			return int.MaxValue;
		}

		return (int)Vector3.Distance(actor.Position, point.Position);
	}

	/// <summary>Backs <c>NPC_GetDistToNpc</c>.</summary>
	internal int DistanceToNpc(NpcInstance a, NpcInstance b)
	{
		var first = Find(a);
		var second = Find(b);
		if (first == null || second == null)
		{
			return int.MaxValue;
		}

		return (int)Vector3.Distance(first.Position, second.Position);
	}

	/// <summary>Backs <c>NPC_GetBodyState</c>.</summary>
	internal int BodyState(NpcInstance npc)
	{
		return Find(npc)?.BodyState ?? NpcActor.BodyStateStand;
	}

	/// <summary>Backs <c>NPC_GetStateTime</c>; seconds since the state started.</summary>
	internal int StateSeconds(NpcInstance npc)
	{
		var state = Find(npc)?.State;
		return state == null ? 0 : (int)(_time - state.StartedAt);
	}

	/// <summary>
	/// Backs <c>NPC_IsOnFP</c>: the reference names the current waypoint, requires proximity
	/// and asks whether it is a free point.
	/// </summary>
	internal bool IsOnFreePoint(NpcInstance npc, string freePoint)
	{
		var actor = Find(npc);
		if (actor == null || string.IsNullOrEmpty(freePoint) || _world == null ||
		    !_world.Waypoints.TryGetValue(freePoint, out var point))
		{
			return false;
		}

		return point.Free &&
		       Vector3.DistanceSquared(actor.Position, point.Position) <= WaypointReach * WaypointReach;
	}

	/// <summary>Backs <c>WLD_IsMobAvailable</c>: a mob whose scheme matches exists nearby.</summary>
	internal bool IsMobAvailable(NpcInstance npc, string name)
	{
		var found = FindMob(npc, name) != null;
		if (DebugSettings.AiTrace)
		{
			Log.Info($"AI trace: mob '{name}' available: {found}.");
		}

		return found;
	}

	/// <summary>
	/// Finds the nearest interactive vob for a script "scheme" (e.g. <c>CHAIR</c>, <c>BEDHIGH</c>),
	/// like the reference's <c>WorldObjects::availableMob</c>. ZenKitCS does not expose the mob's
	/// scheme, so the vob's visual name is matched by prefix: <c>CHAIR_NW_NORMAL_01.ASC</c> is a
	/// <c>CHAIR</c>.
	/// </summary>
	private VobNode FindMob(NpcInstance npc, string scheme)
	{
		var actor = Find(npc);
		if (actor == null || _world == null || string.IsNullOrEmpty(scheme))
		{
			return null;
		}

		VobNode best = null;
		var bestDistance = MobSearchDistanceSquared;
		foreach (var vob in _world.Vobs.All)
		{
			// Beds are serialized as oCMobDoor by this data (ZenKitCS reports that type), so the
			// kind filter only excludes things the scheme prefix cannot name.
			if (vob.Kind != VobKind.Bed && vob.Kind != VobKind.Interactive && vob.Kind != VobKind.Door)
			{
				continue;
			}

			if (!MatchesScheme(vob.Name, scheme))
			{
				continue;
			}

			var distance = Vector3.DistanceSquared(actor.Position, vob.Position);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = vob;
			}
		}

		return best;
	}

	private static bool MatchesScheme(string name, string scheme)
	{
		if (string.IsNullOrEmpty(name))
		{
			return false;
		}

		if (string.Equals(name, scheme, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return name.Length > scheme.Length &&
		       name.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) &&
		       (name[scheme.Length] == '_' || name[scheme.Length] == '.');
	}

	/// <summary>
	/// Whether a vob visual name belongs to a mob scheme: <c>CHAIR_NW_NORMAL_01.ASC</c> is a
	/// <c>CHAIR</c>, while <c>CHAIRMAN.ASC</c> is not. ZenKitCS does not expose the mob's own
	/// scheme string, so this prefix rule stands in for it.
	/// </summary>
	internal static bool MatchesMobScheme(string name, string scheme)
	{
		return MatchesScheme(name, scheme);
	}

	/// <summary>Backs <c>WLD_IsFPAvailable</c>: the waynet has a free point with that name.</summary>
	internal bool IsFreePointAvailable(NpcInstance npc, string name)
	{
		return !string.IsNullOrEmpty(name) && _world != null &&
		       _world.Waypoints.TryGetValue(name, out var point) && point.Free;
	}

	/// <summary>Backs <c>NPC_SetPercTime</c>; the value is used by F3.3 perception.</summary>
	internal void SetPerceptionTime(NpcInstance npc, float seconds)
	{
		var actor = Find(npc);
		if (actor != null && seconds > 0f)
		{
			actor.PerceptionSeconds = seconds;
		}
	}

	/// <summary>Backs <c>NPC_PercEnable</c>: stores the handler for one perception type.</summary>
	internal void SetPerception(NpcInstance npc, int type, int function)
	{
		var actor = Find(npc);
		if (actor == null || type < 0 || type >= NpcActor.PerceptionCount)
		{
			return;
		}

		actor.PerceptionFunctions[type] = function;

		if (DebugSettings.AiTrace)
		{
			Log.Info($"AI trace: '{actor.Vob.Name}' perception {type} -> {function}.");
		}
	}

	/// <summary>Backs <c>NPC_PercDisable</c>.</summary>
	internal void ClearPerception(NpcInstance npc, int type)
	{
		var actor = Find(npc);
		if (actor != null && type >= 0 && type < NpcActor.PerceptionCount)
		{
			actor.PerceptionFunctions[type] = -1;
		}
	}

	/// <summary>Backs <c>WLD_GetDay</c>.</summary>
	internal int Day => _clock?.Day ?? 1;

	/// <summary>
	/// Backs <c>WLD_IsTime</c>: inclusive start, exclusive end, with a range that may cross
	/// midnight (reference <c>wld_istime</c>).
	/// </summary>
	internal bool IsTime(int startHour, int startMinute, int endHour, int endMinute)
	{
		if (_clock == null)
		{
			return false;
		}

		var now = _clock.Hour;
		var start = startHour + startMinute / 60f;
		var end = endHour + endMinute / 60f;

		return start <= end
			? now >= start && now < end
			: now >= start || now < end;
	}

	/// <summary>Backs <c>Perc_SetRange</c>: a per-type range override, 0 keeps senses_range.</summary>
	internal void SetPerceptionRange(int type, int distance)
	{
		if (type >= 0 && type < NpcActor.PerceptionCount && distance > 0)
		{
			_perceptionRanges[type] = distance;
		}
	}

	/// <summary>
	/// Backs <c>NPC_GetAttitude</c>: the player's attitude comes from the NPC's stored value
	/// when present, otherwise both sides use their guilds (reference <c>personAttitude</c>).
	/// </summary>
	internal int GetAttitude(NpcInstance first, NpcInstance second)
	{
		if (first == null || second == null)
		{
			return ScriptRuntime.AttitudeNeutral;
		}

		var firstGuild = GuildOf(first);
		var secondGuild = GuildOf(second);
		if (firstGuild < 0 || secondGuild < 0)
		{
			return ScriptRuntime.AttitudeNeutral;
		}

		return _scripts?.GuildAttitude(firstGuild, secondGuild) ?? ScriptRuntime.AttitudeNeutral;
	}

	private int GuildOf(NpcInstance npc)
	{
		if (npc == null)
		{
			return -1;
		}

		if (_scripts?.Hero != null && npc.Index == _scripts.Hero.Index)
		{
			return _scripts.Hero.Guild;
		}

		return Find(npc)?.Instance?.Guild ?? npc.Guild;
	}

	/// <summary>Backs <c>NPC_GetHeightToNpc</c>.</summary>
	internal int GetHeightToNpc(NpcInstance first, NpcInstance second)
	{
		var a = Find(first);
		var b = Find(second);
		if (a == null || b == null)
		{
			return 0;
		}

		return (int)MathF.Abs(a.Position.Y - b.Position.Y);
	}

	/// <summary>Backs <c>NPC_IsInFightMode</c>; nobody carries a weapon yet, so only FMODE_NONE is true.</summary>
	internal bool IsInFightMode(NpcInstance npc, int mode)
	{
		return Find(npc) != null && mode == 0;
	}

	/// <summary>Backs <c>NPC_SetTarget</c>.</summary>
	internal void SetTarget(NpcInstance npc, NpcInstance other)
	{
		var actor = Find(npc);
		if (actor != null)
		{
			actor.TargetSymbol = other?.Index ?? -1;
		}
	}

	/// <summary>Backs <c>NPC_GetTarget</c>: writes the target into global OTHER.</summary>
	internal bool GetTarget(NpcInstance npc)
	{
		var actor = Find(npc);
		if (actor == null || actor.TargetSymbol <= 0 ||
		    !_bySymbol.TryGetValue(actor.TargetSymbol, out var target))
		{
			_scripts?.SetGlobalOther(null);
			return false;
		}

		_scripts?.SetGlobalOther(target.Instance);
		return true;
	}

	/// <summary>
	/// Samples the player for <c>PERC_ASSESSPLAYER</c> on the NPC's perception interval. The
	/// reference does the same in <c>Npc::perceptionProcess</c>: range, then the view cone,
	/// then a line-of-sight ray, then the enabled script handler with the player as OTHER.
	/// </summary>
	private void Perceive(NpcActor actor, Vector3 playerFeet)
	{
		if (actor.Instance == null || _scripts?.Hero == null)
		{
			return;
		}

		if (_time < actor.NextPerceptionAt)
		{
			return;
		}

		actor.NextPerceptionAt = _time + Math.Max(0.1f, actor.PerceptionSeconds);

		var handler = actor.PerceptionFunctions[PercAssessPlayer];
		var range = _perceptionRanges[PercAssessPlayer];
		if (range <= 0)
		{
			range = actor.Instance.SensesRange;
		}

		var distance = Vector3.Distance(actor.Position, playerFeet);
		var inRange = range > 0 && distance * distance <= range * range;

		var head = actor.Position + Vector3.UnitY * EyeHeight;
		var target = playerFeet + Vector3.UnitY * PlayerCenterHeight;
		var toTarget = target - head;
		var inCone = inRange && WithinViewCone(actor.Yaw, toTarget);

		var occluded = false;
		if (inCone && distance > 1f && _physics != null)
		{
			// Stop short of the player's own capsule so it is not treated as an occluder, and
			// ignore the NPC's own capsule, which the ray starts inside.
			var reach = Math.Max(1f, distance - 45f);
			occluded = _physics.RayCast(head, toTarget / toTarget.Length(), reach, false, out _,
				actor.Vob.PhysicsBody);
		}

		if (DebugSettings.AiTrace && _reported.Add($"perc:{actor.SymbolIndex}"))
		{
			Log.Info($"AI trace: '{actor.Vob.Name}' perception handler {handler}, " +
				$"senses {actor.Instance.Senses}, range {range}, distance {distance:0}, " +
				$"in cone {inCone}, occluded {occluded}.");
		}

		if (handler < 0 || (actor.Instance.Senses & SenseSee) == 0 || !inRange || !inCone || occluded)
		{
			return;
		}

		_scripts.InvokeState(actor.Instance, _scripts.Hero, null, handler);
		if (_perceptionLogs++ < MaxPerceptionLogs)
		{
			Log.Info($"NPC '{actor.Vob.Name}' perceives the player at {distance:0} units " +
				$"(PERC_ASSESSPLAYER, range {range}).");
		}
	}

	/// <summary>
	/// Whether a direction lies inside the NPC's view cone. The facing for a yaw in
	/// <see cref="NpcActor"/> is <c>(sin yaw, 0, -cos yaw)</c>; the reference accepts up to
	/// 80 degrees either side.
	/// </summary>
	internal static bool WithinViewCone(float yaw, Vector3 toTarget)
	{
		var length = MathF.Sqrt(toTarget.X * toTarget.X + toTarget.Z * toTarget.Z);
		if (length < 0.001f)
		{
			return true;
		}

		var dot = (MathF.Sin(yaw) * toTarget.X - MathF.Cos(yaw) * toTarget.Z) / length;
		return dot >= ViewConeCos;
	}

	/// <summary>Backs <c>AI_GotoWP</c>: the state walks there until it arrives.</summary>
	internal void RequestGoTo(NpcInstance npc, string waypoint)
	{
		var actor = Find(npc);
		if (actor == null || _world == null || string.IsNullOrEmpty(waypoint) ||
		    !_world.WaypointIndices.TryGetValue(waypoint, out var index))
		{
			return;
		}

		var point = _world.WaypointList[index];
		actor.AiDestination = new Vector3(point.Position.X, _world.WaypointGroundY(index),
			point.Position.Z);
		actor.HasAiMove = true;

		if (DebugSettings.AiTrace)
		{
			Log.Info($"AI trace: '{actor.Vob.Name}' walks to '{waypoint}'.");
		}
	}

	/// <summary>Backs <c>AI_AlignToWP</c>: face the state's waypoint.</summary>
	internal void AlignToWaypoint(NpcInstance npc)
	{
		var actor = Find(npc);
		if (actor == null || _world == null)
		{
			return;
		}

		var name = actor.Instance?.Wp;
		if (string.IsNullOrEmpty(name) || !_world.Waypoints.TryGetValue(name, out var point))
		{
			var index = _world.FindNearestWaypoint(actor.Position);
			if (index < 0)
			{
				return;
			}

			point = _world.WaypointList[index];
		}

		var yaw = MathF.Atan2(point.Direction.X, -point.Direction.Z);
		actor.SetPosition(actor.Position, yaw);
		_walker.Apply(actor);
	}

	/// <summary>Backs <c>AI_Standup</c>/<c>AI_StandupQuick</c>.</summary>
	internal void StandUp(NpcInstance npc)
	{
		var actor = Find(npc);
		if (actor == null)
		{
			return;
		}

		LeaveMob(actor);
		actor.HasAiMove = false;
		actor.PlayClip(actor.IdleClip);
	}

	/// <summary>Backs <c>AI_PlayAni</c>.</summary>
	internal void PlayAnimation(NpcInstance npc, string name)
	{
		var actor = Find(npc);
		if (actor == null || string.IsNullOrEmpty(name))
		{
			return;
		}

		if (!actor.PlayClip(name) && _reported.Add("anim:" + name))
		{
			Log.Warning($"NPC '{actor.Vob.Name}' has no clip '{name}'.");
		}
	}

	/// <summary>Backs <c>AI_PlayAnimBs</c>: the clip runs with the requested body state.</summary>
	internal void PlayAnimationWithState(NpcInstance npc, string name, int bodyState)
	{
		var actor = Find(npc);
		if (actor == null)
		{
			return;
		}

		actor.BodyState = bodyState;
		if (DebugSettings.AiTrace)
		{
			Log.Info($"AI trace: '{actor.Vob.Name}' plays '{name}' with body state {bodyState}.");
		}

		PlayAnimation(npc, name);
	}

	/// <summary>Backs <c>NPC_SetStateTime</c>: shifts the state's start so it reads as older.</summary>
	internal void SetStateSeconds(NpcInstance npc, int seconds)
	{
		var state = Find(npc)?.State;
		if (state != null)
		{
			state.StartedAt = _time - Math.Max(0, seconds);
		}
	}

	/// <summary>Backs <c>AI_SetWalkMode</c>; the weapon bit is stripped like the reference.</summary>
	internal void SetWalkMode(NpcInstance npc, int modeBits)
	{
		var actor = Find(npc);
		if (actor != null)
		{
			actor.WalkMode = modeBits & ~0x80;
		}
	}

	/// <summary>
	/// Backs <c>AI_UseMob</c>: a negative state releases the mob, anything else sits or lies on
	/// it. The reference plays a per-scheme animation chain (<c>T_&lt;scheme&gt;_STAND_2_S0</c>
	/// then <c>S_&lt;scheme&gt;_S0</c>); the clip's own <c>Next</c> data continues the chain.
	/// </summary>
	internal void UseMob(NpcInstance npc, string target, int state)
	{
		var actor = Find(npc);
		if (actor == null || string.IsNullOrEmpty(target))
		{
			return;
		}

		if (state < 0)
		{
			LeaveMob(actor);
			return;
		}

		EnterMob(actor, target, state);
	}

	private void EnterMob(NpcActor actor, string scheme, int state)
	{
		var bed = scheme.StartsWith("BED", StringComparison.OrdinalIgnoreCase) ||
		          scheme.StartsWith("SLEEP", StringComparison.OrdinalIgnoreCase);
		var seat = scheme.StartsWith("CHAIR", StringComparison.OrdinalIgnoreCase) ||
		           scheme.StartsWith("BENCH", StringComparison.OrdinalIgnoreCase) ||
		           scheme.StartsWith("SIT", StringComparison.OrdinalIgnoreCase);

		string played;
		if (bed)
		{
			// ZenKitCS does not expose the mob's scheme, so the bed's front/back variant is
			// unknown; try both, then fall back to sleeping on the ground. State 1 is the
			// lying pose the scripts use.
			played = PlayFirst(actor,
				$"T_{scheme}_FRONT_S0_2_S{state}",
				$"T_{scheme}_BACK_S0_2_S{state}",
				$"T_{scheme}_FRONT_STAND_2_S0",
				$"T_{scheme}_BACK_STAND_2_S0",
				"T_STAND_2_SLEEPGROUND",
				$"S_{scheme}_S{state}");
			actor.BodyState = NpcActor.BodyStateLie;
		}
		else if (seat)
		{
			// S_<scheme>_S0 is a one-frame placeholder (it looks standing); the seated pose is
			// S1, so state 1 plays the S0 -> S1 transition directly.
			played = PlayFirst(actor,
				$"T_{scheme}_S0_2_S{state}",
				$"T_{scheme}_STAND_2_S0",
				"T_STAND_2_SIT");
			actor.BodyState = NpcActor.BodyStateSit;
		}
		else
		{
			// Work mobs (stoves, anvils, ...) may have no NPC-side animation; leave the body
			// standing rather than forcing a chair pose.
			played = PlayFirst(actor, $"T_{scheme}_STAND_2_S0");
		}

		actor.MobScheme = scheme;
		actor.MobState = state;
		actor.MobClip = played;
		actor.HasAiMove = false;

		if (string.IsNullOrEmpty(played) && _reported.Add($"mobclip:{scheme}"))
		{
			Log.Warning($"NPC '{actor.Vob.Name}': no mob animation for '{scheme}'.");
		}

		if (_reported.Add($"mob:{actor.SymbolIndex}:{scheme}"))
		{
			Log.Info($"NPC '{actor.Vob.Name}' uses '{scheme}' state {state}.");
		}
	}

	/// <summary>Plays the first clip the body has and returns its name, or null.</summary>
	private static string PlayFirst(NpcActor actor, params string[] clips)
	{
		foreach (var clip in clips)
		{
			if (actor.PlayClip(clip))
			{
				return clip;
			}
		}

		return null;
	}

	/// <summary>
	/// Releases a mob and puts the body back on its feet. The entry transition is played
	/// backwards (the human set has no sit-down counterpart clip); when that fails the body
	/// returns to the idle clip.
	/// </summary>
	private void LeaveMob(NpcActor actor)
	{
		var hadMob = !string.IsNullOrEmpty(actor.MobScheme);
		var clip = actor.MobClip;
		actor.MobScheme = string.Empty;
		actor.MobState = -1;
		actor.MobClip = string.Empty;
		actor.BodyState = NpcActor.BodyStateStand;

		if (!hadMob)
		{
			return;
		}

		var reversed = !string.IsNullOrEmpty(clip) && actor.PlayClipReverse(clip);
		if (!reversed)
		{
			actor.PlayClip(actor.IdleClip);
		}

		if (DebugSettings.AiTrace)
		{
			Log.Info($"AI trace: '{actor.Vob.Name}' leaves the mob (reversed '{clip}': {reversed}).");
		}
	}

	/// <summary>Backs <c>AI_UseItemToState</c>; item animations arrive with the inventory.</summary>
	internal void UseItemToState(NpcInstance npc, int item, int state)
	{
		var actor = Find(npc);
		if (actor != null && _reported.Add($"item:{item}:{state}"))
		{
			Log.Info($"NPC '{actor.Vob.Name}' uses item {item} to state {state} (not animated yet).");
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
		RestartRoutineStates();

		if (_actors.Count == 0)
		{
			return;
		}

		// Only nearby NPCs tick fully; the reference picks AiNormal/AiFar/AiFar2 from the same
		// 3000/6000 unit radii. Far NPCs keep a slow state loop, the rest is skipped.
		foreach (var actor in _actors)
		{
			var distanceSquared = Vector3.DistanceSquared(actor.Position, center);
			var policy = PolicyFor(distanceSquared);
			actor.Policy = policy;

			if (policy == NpcProcessPolicy.AiFar2)
			{
				continue;
			}

			if (policy == NpcProcessPolicy.AiFar)
			{
				var farState = actor.State;
				if (farState != null && farState.TryRunLoop(_time, FarLoopInterval))
				{
					var farResult = _scripts.InvokeState(actor.Instance, null, null,
						farState.LoopFunction);
					if (NpcAiState.ShouldEnd(farResult))
					{
						ClearState(actor);
					}
				}

				continue;
			}

			// Script movement is suspended while the routine walker owns the body.
			if (actor.HasAiMove && !actor.Moving)
			{
				if (_walker.Step(actor, actor.AiDestination, dt, NpcWalker.DefaultSpeed))
				{
					actor.HasAiMove = false;
				}
			}

			Perceive(actor, center);

			var state = actor.State;
			if (state == null || !state.TryRunLoop(_time, LoopInterval))
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
	/// Picks the update budget for a squared distance to the player: normal within 3000 units,
	/// far (slow loops only) up to 6000, dormant beyond (reference <c>NpcProcessPolicy</c>).
	/// </summary>
	internal static NpcProcessPolicy PolicyFor(float distanceSquared)
	{
		if (distanceSquared <= NormalDistance * NormalDistance)
		{
			return NpcProcessPolicy.AiNormal;
		}

		return distanceSquared <= FarDistance * FarDistance
			? NpcProcessPolicy.AiFar
			: NpcProcessPolicy.AiFar2;
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

		if (state.EndFunction >= 0 && _scripts != null)
		{
			_scripts.InvokeState(actor.Instance, null, null, state.EndFunction);
		}

		actor.State = null;
		actor.HasAiMove = false;
		LeaveMob(actor);
		_endedThisTick.Add(actor);

		if (_endLogs++ < MaxEndLogs)
		{
			Log.Info($"NPC state '{actor.Vob.Name}': '{state.Name}' ended.");
		}
	}

	private NpcActor Find(NpcInstance npc)
	{
		return npc != null && _bySymbol.TryGetValue(npc.Index, out var actor) ? actor : null;
	}

	/// <summary>
	/// The reference's <c>Npc::tickRoutine</c> restarts the routine callback whenever no state
	/// is active, so a state that ended on its own (e.g. <c>ZS_SLEEP</c> at dawn) returns to
	/// <c>ZS_GOTOBED</c> and its script decides what happens next. The one-second guard keeps
	/// a state that ends immediately from spinning the VM every frame.
	/// </summary>
	private void RestartRoutineStates()
	{
		if (_endedThisTick.Count == 0)
		{
			return;
		}

		foreach (var actor in _endedThisTick)
		{
			if (actor.State != null || actor.Moving || actor.Routine == null ||
			    actor.Routine.Action <= 0 || _time < actor.NextRoutineStateAt ||
			    !IsAtRoutinePoint(actor))
			{
				continue;
			}

			actor.NextRoutineStateAt = _time + RoutineRestartSeconds;
			Start(actor, actor.Routine.Action, actor.Routine.Waypoint);
		}

		_endedThisTick.Clear();
	}

	/// <summary>Whether the actor is standing on the routine point it should restart at.</summary>
	private bool IsAtRoutinePoint(NpcActor actor)
	{
		if (_world == null || actor.Routine == null ||
		    string.IsNullOrEmpty(actor.Routine.Waypoint) ||
		    !_world.Waypoints.TryGetValue(actor.Routine.Waypoint, out var point))
		{
			return true;
		}

		const float reach = 100f;
		return Vector3.DistanceSquared(actor.Position, point.Position) <= reach * reach;
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
