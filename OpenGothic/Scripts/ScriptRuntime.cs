using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using OpenGothic.Vobs;
using ZenKit;
using ZenKit.Daedalus;
using ZenKit.Util;
using ZenKit.Vobs;

namespace OpenGothic.Scripts;

/// <summary>
/// Loads the compiled Daedalus game script and runs its startup entry points. Unimplemented
/// externals log once and receive the VM's default return instead of aborting the startup.
/// The same runtime owns the script object store and the dialog engine.
/// </summary>
public sealed class ScriptRuntime
{
	private readonly HashSet<string> _unbound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<int, NpcBinding> _npcs = new Dictionary<int, NpcBinding>();
	private readonly Dictionary<int, ItemInstance> _items = new Dictionary<int, ItemInstance>();
	private readonly Dictionary<int, ItemInstance> _itemInstances = new Dictionary<int, ItemInstance>();
	private readonly Dictionary<int, NpcVisual> _pendingVisuals = new Dictionary<int, NpcVisual>();

	private Dictionary<int, List<InfoInstance>> _infosByNpc;
	private Dictionary<int, InfoInstance> _infosBySymbol;
	private Dictionary<string, string> _dialogText;
	private readonly Dictionary<int, List<DialogChoice>> _storedChoices = new Dictionary<int, List<DialogChoice>>();
	private readonly HashSet<(int Npc, int Info)> _knownInfos = new HashSet<(int Npc, int Info)>();
	private readonly HashSet<int> _conditionFailures = new HashSet<int>();

	private DialogSession _session;
	private bool _endRequested;
	private string _traceState;

	public DaedalusVm Vm { get; private set; }

	/// <summary>The script-side player NPC instance, or null if scripts are disabled/failed.</summary>
	public NpcInstance Hero { get; private set; }

	/// <summary>Dialog/output-unit cutscene library from <c>Ou.dat</c>, or null if absent.</summary>
	public CutsceneLibrary Cutscenes { get; private set; }

	public bool Booted { get; private set; }

	/// <summary>Number of calls that reached the default external handler.</summary>
	public int UnboundCalls { get; private set; }

	/// <summary>NPCs inserted by <c>WLD_INSERTNPC</c> during boot.</summary>
	public int InsertedNpcCount => _npcs.Count;

	/// <summary>Items inserted by <c>WLD_INSERTITEM</c> during boot.</summary>
	public int InsertedItemCount => _items.Count;

	/// <summary>Number of C_INFO instances prepared for dialogs.</summary>
	public int DialogInfoCount => _infosBySymbol?.Count ?? 0;

	/// <summary>Last journal line added through the script's <c>LOG_ADDENTRY</c> external.</summary>
	public string LastLogEntry { get; private set; }

	/// <summary>Whether any script NPC can be talked to.</summary>
	public bool HasScriptNpcs => _npcs.Count > 0;

	/// <summary>The script NPCs, for the world spawner.</summary>
	internal IEnumerable<NpcBinding> Npcs => _npcs.Values;

	/// <summary>
	/// NPC named by an <c>AI_ProcessInfos</c> call made outside a conversation. The game UI
	/// consumes this to open the dialog, mirroring the reference's AI queue request.
	/// </summary>
	public NpcVob RequestedDialogNpc { get; private set; }

	/// <summary>
	/// The AI system that owns actors, states and script actions. Set once the NPCs are placed;
	/// the AI externals below forward to it.
	/// </summary>
	internal NpcAiSystem AiSystem { get; set; }

	/// <summary>
	/// Writes the global OTHER slot during a script call, used by queries such as
	/// <c>NPC_GetTarget</c> that return their result through the VM rather than as an int.
	/// </summary>
	internal void SetGlobalOther(NpcInstance other)
	{
		if (Vm != null)
		{
			Vm.GlobalOther = other;
		}
	}

	/// <summary>Reference attitude values (constants.h): hostile 0, neutral 2, friendly 3.</summary>
	internal const int AttitudeHostile = 0;
	internal const int AttitudeNeutral = 2;

	private int[] _guildAttitudes;
	private int _guildCount;

	/// <summary>
	/// Reads the <c>GIL_ATTITUDES</c> table like <c>GameScript::initialize</c>: a
	/// <c>GIL_MAX</c>-wide matrix whose values come from a <c>TAB_ANZAHL</c>-entry constant.
	/// </summary>
	private void LoadGuildAttitudes()
	{
		if (Vm == null)
		{
			return;
		}

		var maxSymbol = Vm.GetSymbolByName("GIL_MAX");
		var countSymbol = Vm.GetSymbolByName("TAB_ANZAHL");
		var table = Vm.GetSymbolByName("GIL_ATTITUDES");
		if (maxSymbol == null || countSymbol == null || table == null)
		{
			return;
		}

		var guilds = maxSymbol.GetInt(0);
		var tableSize = (int)MathF.Sqrt(countSymbol.GetInt(0));
		if (guilds <= 0 || tableSize <= 0)
		{
			return;
		}

		_guildCount = guilds;
		_guildAttitudes = new int[guilds * guilds];
		for (var row = 0; row < tableSize; ++row)
		{
			for (var column = 0; column < tableSize; ++column)
			{
				_guildAttitudes[row * guilds + column] =
					table.GetInt((ushort)(row * tableSize + column));
			}
		}

		Log.Info($"Guild attitudes: {guilds} guilds from 'GIL_ATTITUDES'.");
	}

	/// <summary>Backs <c>WLD_ExchangeGuildAttitudes</c>.</summary>
	internal void ExchangeGuildAttitudes(string tableName)
	{
		if (Vm == null || string.IsNullOrEmpty(tableName))
		{
			return;
		}

		var maxSymbol = Vm.GetSymbolByName("GIL_MAX");
		var countSymbol = Vm.GetSymbolByName("TAB_ANZAHL");
		var table = Vm.GetSymbolByName(tableName);
		if (maxSymbol == null || countSymbol == null || table == null)
		{
			return;
		}

		var guilds = maxSymbol.GetInt(0);
		var tableSize = (int)MathF.Sqrt(countSymbol.GetInt(0));
		if (guilds <= 0 || tableSize <= 0)
		{
			return;
		}

		_guildCount = guilds;
		_guildAttitudes = new int[guilds * guilds];
		for (var row = 0; row < tableSize; ++row)
		{
			for (var column = 0; column < tableSize; ++column)
			{
				_guildAttitudes[row * guilds + column] =
					table.GetInt((ushort)(row * tableSize + column));
			}
		}
	}

	/// <summary>Backs <c>WLD_GetGuildAttitude</c>.</summary>
	internal int GuildAttitude(int first, int second)
	{
		if (_guildAttitudes == null)
		{
			return AttitudeNeutral;
		}

		if (first < 0 || second < 0 || first >= _guildCount || second >= _guildCount)
		{
			return AttitudeHostile;
		}

		return _guildAttitudes[first * _guildCount + second];
	}

	/// <summary>Backs <c>WLD_SetGuildAttitude</c>; the reference argument order is (g1, att, g2).</summary>
	internal void SetGuildAttitude(int first, int attitude, int second)
	{
		if (_guildAttitudes == null ||
		    first < 0 || second < 0 || first >= _guildCount || second >= _guildCount)
		{
			return;
		}

		_guildAttitudes[first * _guildCount + second] = attitude;
	}

	public void LoadAndBoot(string gothicFolder, string worldName)
	{
		if (string.IsNullOrEmpty(gothicFolder))
		{
			throw new ArgumentNullException(nameof(gothicFolder));
		}

		var scriptFolder = Path.Combine(gothicFolder, "_work", "Data", "Scripts");
		var datPath = Path.Combine(scriptFolder, "_compiled", "GOTHIC.DAT");
		if (!File.Exists(datPath))
		{
			Log.Warning($"Daedalus script not found: '{datPath}'.");
			return;
		}

		try
		{
			Vm = new DaedalusVm(datPath);
		}
		catch (Exception ex)
		{
			Log.Error($"Unable to load Daedalus script: {ex.Message}");
			return;
		}

		Vm.RegisterExternalDefault(OnUnboundExternal);
		RegisterScriptObjects();
		LoadCutscenes(Path.Combine(scriptFolder, "CONTENT", "CUTSCENE", "Ou.dat"));

		Log.Info($"Daedalus script loaded: {Vm.SymbolCount:N0} symbols, {Vm.Symbols.Count:N0} listed.");

		Boot(worldName);
		LoadGuildAttitudes();

		if (Booted)
		{
			try
			{
				LoadDialogInfos();
			}
			catch (Exception ex)
			{
				Log.Warning($"Unable to prepare dialog data: {ex.Message}");
			}
		}
	}

	private void RegisterScriptObjects()
	{
		Vm.RegisterExternal("wld_insertnpc", (int symbolIndex, string spawnPoint) =>
		{
			InsertNpc(symbolIndex, spawnPoint);
		});
		Vm.RegisterExternal("wld_insertitem", (int symbolIndex, string spawnPoint) =>
		{
			InsertItem(symbolIndex, spawnPoint);
		});
		Vm.RegisterExternal("hlp_getnpc", (int symbolIndex) => GetNpc(symbolIndex));
		Vm.RegisterExternal("log_addentry", (string text) =>
		{
			LastLogEntry = text;
			Log.Info($"Daedalus journal: {text}");
		});

		// Dialog data and output. AI_Output lines are the spoken text; the output unit also
		// names the voice file in the reference.
		Vm.RegisterExternal("ai_output", (NpcInstance self, NpcInstance target, string unit) =>
		{
			OnAiOutput(self, target, unit);
		});
		Vm.RegisterExternal("ai_outputsvm", (NpcInstance self, NpcInstance target, string unit) =>
		{
			OnAiOutput(self, target, unit);
		});
		Vm.RegisterExternal("ai_outputsvm_overlay", (NpcInstance self, NpcInstance target, string unit) =>
		{
			OnAiOutput(self, target, unit);
		});
		Vm.RegisterExternal("info_addchoice", (int info, string text, int function) =>
		{
			AddStoredChoice(info, text, function);
		});
		Vm.RegisterExternal("info_clearchoices", (int info) => ClearStoredChoices(info));
		Vm.RegisterExternal("infomanager_hasfinished", () => _session == null ? 1 : 0);
		Vm.RegisterExternal("ai_processinfos", (NpcInstance npc) => RequestDialog(npc));
		Vm.RegisterExternal("ai_stopprocessinfos", (NpcInstance npc) =>
		{
			if (_session != null)
			{
				_endRequested = true;
			}
		});
		Vm.RegisterExternal("npc_knowsinfo", (NpcInstance npc, int info) => Knows(npc, info) ? 1 : 0);
		Vm.RegisterExternal("npc_giveinfo", (NpcInstance npc, int info) =>
		{
			MarkKnown(npc, info);
			return 1;
		});
		Vm.RegisterExternal("npc_checkinfo", (NpcInstance npc, int important) => CheckInfo(npc, important));
		Vm.RegisterExternal("npc_isplayer", (NpcInstance npc) =>
			npc != null && Hero != null && npc.Index == Hero.Index ? 1 : 0);
		Vm.RegisterExternal("npc_refusetalk", (NpcInstance npc) => 0);
		Vm.RegisterExternal("npc_setrefusetalk", (NpcInstance npc, int seconds) => { });
		Vm.RegisterExternal("ai_startstate",
			(NpcInstance self, int function, int state, string waypoint) =>
			{
				AiSystem?.RequestStart(self, function, waypoint);
			});
		Vm.RegisterExternal("npc_isinstate", (NpcInstance npc, int stateFunction) =>
			AiSystem != null && AiSystem.IsInState(npc, stateFunction) ? 1 : 0);

		// Queries and actions the shipped ZS_* states call; the set comes from -aitrace.
		Vm.RegisterExternal("hlp_isvalidnpc", (NpcInstance npc) =>
			AiSystem != null && AiSystem.IsValidNpc(npc) ? 1 : 0);
		Vm.RegisterExternal("hlp_getinstanceid", (DaedalusInstance instance) =>
			instance?.Index ?? -1);
		Vm.RegisterExternal("npc_getattitude", (NpcInstance a, NpcInstance b) =>
			AiSystem?.GetAttitude(a, b) ?? 2);
		Vm.RegisterExternal("npc_getpermattitude", (NpcInstance a, NpcInstance b) =>
			AiSystem?.GetAttitude(a, b) ?? 2);
		Vm.RegisterExternal("npc_getheighttonpc", (NpcInstance a, NpcInstance b) =>
			AiSystem?.GetHeightToNpc(a, b) ?? 0);
		Vm.RegisterExternal("npc_isinfightmode", (NpcInstance npc, int mode) =>
			AiSystem != null && AiSystem.IsInFightMode(npc, mode) ? 1 : 0);
		Vm.RegisterExternal("npc_settarget", (NpcInstance npc, NpcInstance other) =>
			AiSystem?.SetTarget(npc, other));
		Vm.RegisterExternal("npc_gettarget", (NpcInstance npc) =>
			AiSystem != null && AiSystem.GetTarget(npc) ? 1 : 0);
		Vm.RegisterExternal("npc_clearaiqueue", (NpcInstance npc) => { });
		Vm.RegisterExternal("wld_getguildattitude", (int first, int second) =>
			GuildAttitude(first, second));
		Vm.RegisterExternal("wld_setguildattitude", (int first, int attitude, int second) =>
			SetGuildAttitude(first, attitude, second));
		Vm.RegisterExternal("wld_exchangeguildattitudes", (string table) =>
			ExchangeGuildAttitudes(table));
		Vm.RegisterExternal("wld_getplayerportalguild", () => 0);
		Vm.RegisterExternal("wld_getday", () => AiSystem?.Day ?? 1);
		Vm.RegisterExternal("wld_istime", (int h0, int m0, int h1, int m1) =>
			AiSystem != null && AiSystem.IsTime(h0, m0, h1, m1) ? 1 : 0);
		Vm.RegisterExternal("hlp_strcmp", (string a, string b) =>
			string.Equals(a, b, StringComparison.Ordinal) ? 1 : 0);
		Vm.RegisterExternal("npc_getnearestwp", (NpcInstance npc) =>
			AiSystem?.NearestWaypoint(npc) ?? string.Empty);
		Vm.RegisterExternal("npc_getdisttowp", (NpcInstance npc, string waypoint) =>
			AiSystem?.DistanceToWaypoint(npc, waypoint) ?? int.MaxValue);
		Vm.RegisterExternal("npc_getdisttonpc", (NpcInstance a, NpcInstance b) =>
			AiSystem?.DistanceToNpc(a, b) ?? int.MaxValue);
		Vm.RegisterExternal("npc_getbodystate", (NpcInstance npc) =>
			AiSystem?.BodyState(npc) ?? NpcActor.BodyStateStand);
		Vm.RegisterExternal("npc_getstatetime", (NpcInstance npc) =>
			AiSystem?.StateSeconds(npc) ?? 0);
		Vm.RegisterExternal("npc_isonfp", (NpcInstance npc, string freePoint) =>
			AiSystem != null && AiSystem.IsOnFreePoint(npc, freePoint) ? 1 : 0);
		Vm.RegisterExternal("npc_hasitems", (NpcInstance npc, int item) => 0);
		Vm.RegisterExternal("npc_getlookattarget", (NpcInstance npc) => (NpcInstance)null);
		Vm.RegisterExternal("npc_setperctime", (NpcInstance npc, float seconds) =>
			AiSystem?.SetPerceptionTime(npc, seconds));
		Vm.RegisterExternal("npc_percenable", (NpcInstance npc, int type, int function) =>
			AiSystem?.SetPerception(npc, type, function));
		Vm.RegisterExternal("npc_percdisable", (NpcInstance npc, int type) =>
			AiSystem?.ClearPerception(npc, type));
		Vm.RegisterExternal("perc_setrange", (int type, int distance) =>
			AiSystem?.SetPerceptionRange(type, distance));
		Vm.RegisterExternal("wld_ismobavailable", (NpcInstance npc, string name) =>
			AiSystem != null && AiSystem.IsMobAvailable(npc, name) ? 1 : 0);
		Vm.RegisterExternal("wld_isfpavailable", (NpcInstance npc, string name) =>
			AiSystem != null && AiSystem.IsFreePointAvailable(npc, name) ? 1 : 0);
		Vm.RegisterExternal("ai_gotowp", (NpcInstance npc, string waypoint) =>
			AiSystem?.RequestGoTo(npc, waypoint));
		Vm.RegisterExternal("ai_aligntowp", (NpcInstance npc) => AiSystem?.AlignToWaypoint(npc));
		Vm.RegisterExternal("ai_standup", (NpcInstance npc) => AiSystem?.StandUp(npc));
		Vm.RegisterExternal("ai_standupquick", (NpcInstance npc) => AiSystem?.StandUp(npc));
		Vm.RegisterExternal("ai_playani", (NpcInstance npc, string name) =>
			AiSystem?.PlayAnimation(npc, name));
		Vm.RegisterExternal("ai_playanibs", (NpcInstance npc, string name, int bodyState) =>
			AiSystem?.PlayAnimationWithState(npc, name, bodyState));
		Vm.RegisterExternal("npc_setstatetime", (NpcInstance npc, int seconds) =>
			AiSystem?.SetStateSeconds(npc, seconds));
		Vm.RegisterExternal("npc_isinplayersroom", (NpcInstance npc) => 0);
		Vm.RegisterExternal("ai_setwalkmode", (NpcInstance npc, int mode) =>
			AiSystem?.SetWalkMode(npc, mode));
		Vm.RegisterExternal("ai_removeweapon", (NpcInstance npc) => { });
		Vm.RegisterExternal("ai_usemob", (NpcInstance npc, string target, int state) =>
		{
			AiSystem?.UseMob(npc, target, state);
			return 0;
		});
		Vm.RegisterExternal("ai_useitemtostate", (NpcInstance npc, int item, int state) =>
			AiSystem?.UseItemToState(npc, item, state));

		// Visual parameters are collected while the C_NPC constructor runs, before the
		// binding exists; the spawner builds the model after boot.
		Vm.RegisterExternal("mdl_setvisualbody",
			(NpcInstance npc, string body, int bodyTexNr, int bodyTexColor, string head, int headTexNr,
				int teethTexNr, int armor) =>
			{
				SetVisualBody(npc, body, bodyTexNr, bodyTexColor, head, headTexNr, teethTexNr, armor);
			});
		Vm.RegisterExternal("mdl_setvisual", (NpcInstance npc, string visual) =>
		{
			var data = GetVisual(npc);
			if (data != null)
			{
				data.ModelOverride = visual ?? string.Empty;
			}
		});
		Vm.RegisterExternal("mdl_setmodelscale", (NpcInstance npc, float x, float y, float z) =>
		{
			var data = GetVisual(npc);
			if (data != null)
			{
				data.Scale = new Vector3(x, y, z);
			}
		});
		Vm.RegisterExternal("ta_min",
			(NpcInstance npc, int startHour, int startMinute, int stopHour, int stopMinute,
				int action, string waypoint) =>
			{
				AddRoutine(npc, startHour, startMinute, stopHour, stopMinute, action, waypoint);
			});

		var heroSymbol = Vm.GetSymbolByName("PC_HERO");
		if (heroSymbol != null)
		{
			try
			{
				Hero = Vm.InitInstance<NpcInstance>(heroSymbol);
				Vm.GlobalHero = Hero;
			}
			catch (Exception ex)
			{
				Log.Warning($"Unable to initialize the script hero: {ex.Message}");
			}
		}
	}

	private void InsertNpc(int symbolIndex, string spawnPoint)
	{
		var symbol = Vm.GetSymbolByIndex(symbolIndex);
		if (symbol == null)
		{
			return;
		}

		try
		{
			// The C_NPC constructor calls MDL_SetVisualBody before the binding exists, so
			// collect its visual parameters under the symbol and hand them to the new vob.
			_npcs.Remove(symbolIndex);
			var visual = new NpcVisual();
			_pendingVisuals[symbolIndex] = visual;

			var instance = Vm.InitInstance<NpcInstance>(symbol);
			if (instance == null)
			{
				_pendingVisuals.Remove(symbolIndex);
				return;
			}

			var binding = new NpcBinding
			{
				SymbolIndex = symbolIndex,
				Instance = instance,
				SpawnPoint = spawnPoint,
				Vob = CreateNpcVob(instance, symbol.Name, visual)
			};
			instance.UserData = binding;
			_npcs[symbolIndex] = binding;
			_pendingVisuals.Remove(symbolIndex);

			CallDailyRoutine(instance);

			if (_npcs.Count <= 8)
			{
				var vob = binding.Vob;
				Log.Info(
					$"Script NPC '{vob.Name}' ({symbol.Name}) at '{spawnPoint}', " +
					$"guild {vob.Guild}, level {vob.Level}, hp {vob.Hp}/{vob.MaxHp}.");
			}
		}
		catch (Exception ex)
		{
			_pendingVisuals.Remove(symbolIndex);
			Log.Warning($"Unable to initialize script NPC '{symbol.Name}': {ex.Message}");
		}
	}

	private static NpcVob CreateNpcVob(NpcInstance instance, string symbolName, NpcVisual visual)
	{
		var name = instance.GetName(NpcNameSlot.Slot0);
		if (string.IsNullOrEmpty(name))
		{
			name = symbolName;
		}

		var maxHp = Math.Max(1, instance.GetAttribute(NpcAttribute.HitPointsMax));
		var hp = Math.Max(1, instance.GetAttribute(NpcAttribute.HitPoints));
		var source = new Npc
		{
			Name = name,
			NpcInstance = symbolName,
			Guild = instance.Guild,
			Level = instance.Level,
			ShowVisual = false,
			Position = System.Numerics.Vector3.Zero,
			Rotation = new Matrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 1),
			Attributes = new List<int> { hp, maxHp }
		};

		// The node is intentionally detached until the waypoint layer gives it a position.
		var vob = (NpcVob)VobFactory.Create(source, null, Matrix.Identity, Matrix.Identity);
		vob.Visual = visual ?? new NpcVisual();
		return vob;
	}

	private void InsertItem(int symbolIndex, string spawnPoint)
	{
		var symbol = Vm.GetSymbolByIndex(symbolIndex);
		if (symbol == null)
		{
			return;
		}

		try
		{
			var instance = Vm.InitInstance<ItemInstance>(symbol);
			if (instance != null)
			{
				_items[symbolIndex] = instance;
			}
		}
		catch (Exception ex)
		{
			Log.Warning($"Unable to initialize script item '{symbol.Name}': {ex.Message}");
		}
	}

	private NpcInstance GetNpc(int symbolIndex)
	{
		return _npcs.TryGetValue(symbolIndex, out var binding) ? binding.Instance : null;
	}

	private NpcVisual GetVisual(NpcInstance npc)
	{
		if (npc == null)
		{
			return null;
		}

		if (_npcs.TryGetValue(npc.Index, out var binding))
		{
			return binding.Vob.Visual;
		}

		if (!_pendingVisuals.TryGetValue(npc.Index, out var visual))
		{
			visual = new NpcVisual();
			_pendingVisuals[npc.Index] = visual;
		}

		return visual;
	}

	private void AddRoutine(NpcInstance npc, int startHour, int startMinute, int stopHour,
		int stopMinute, int action, string waypoint)
	{
		if (npc == null || !_npcs.TryGetValue(npc.Index, out var binding))
		{
			return;
		}

		binding.Vob.Routines.Add(new NpcRoutine
		{
			Start = startHour + startMinute / 60f,
			End = stopHour + stopMinute / 60f,
			Action = action,
			Waypoint = waypoint ?? string.Empty
		});
	}

	/// <summary>
	/// Runs the NPC's <c>daily_routine</c> entry point after the binding exists, so its
	/// <c>TA_MIN</c> calls can attach the routine list to the vob. This mirrors
	/// <c>initializeInstanceNpc</c> in the reference.
	/// </summary>
	private void CallDailyRoutine(NpcInstance instance)
	{
		if (instance == null || instance.DailyRoutine == 0)
		{
			return;
		}

		var symbol = Vm.GetSymbolByIndex(instance.DailyRoutine);
		if (symbol == null)
		{
			return;
		}

		var self = Vm.GlobalSelf;
		var other = Vm.GlobalOther;
		try
		{
			Vm.GlobalSelf = instance;
			Vm.GlobalOther = Hero;
			Vm.Call(symbol.Index);
		}
		catch (Exception ex)
		{
			Log.Warning($"NPC daily routine '{symbol.Name}' failed: {ex.Message}");
		}
		finally
		{
			Vm.GlobalSelf = self;
			Vm.GlobalOther = other;
		}
	}

	/// <summary>
	/// Resolves an AI state function and its optional siblings, exactly like the reference's
	/// <c>AiState</c> (<c>game/aistate.cpp</c>): <c>_Loop</c> and <c>_End</c> are looked up by
	/// name, and a missing sibling stays -1. The shipped data spells them upper case, and the
	/// VM's symbol lookup is case-insensitive.
	/// </summary>
	internal bool TryGetStateFunctions(int function, out string name, out int loop, out int end)
	{
		name = string.Empty;
		loop = -1;
		end = -1;

		if (Vm == null || function <= 0)
		{
			return false;
		}

		var symbol = Vm.GetSymbolByIndex(function);
		if (symbol == null)
		{
			return false;
		}

		name = symbol.Name;
		var loopSymbol = Vm.GetSymbolByName(name + "_Loop");
		if (loopSymbol != null)
		{
			loop = loopSymbol.Index;
		}

		var endSymbol = Vm.GetSymbolByName(name + "_End");
		if (endSymbol != null)
		{
			end = endSymbol.Index;
		}

		return true;
	}

	/// <summary>
	/// Calls a script AI state with SELF/OTHER/VICTIM set and returns its int result, like the
	/// reference's <c>GameScript::invokeState</c>. Invalid functions and script errors are
	/// logged and treated as "no result" so one broken state cannot stop the frame.
	/// </summary>
	internal int InvokeState(NpcInstance self, NpcInstance other, NpcInstance victim, int function)
	{
		if (Vm == null || function <= 0)
		{
			return 0;
		}

		var symbol = Vm.GetSymbolByIndex(function);
		if (symbol == null)
		{
			return 0;
		}

		var previousSelf = Vm.GlobalSelf;
		var previousOther = Vm.GlobalOther;
		var previousVictim = Vm.GlobalVictim;
		var previousTrace = _traceState;
		try
		{
			Vm.GlobalSelf = self;
			Vm.GlobalOther = other;
			Vm.GlobalVictim = victim;
			_traceState = symbol.Name;

			if (symbol.HasReturn)
			{
				return Vm.Call<int>(function);
			}

			Vm.Call(function);
			return 0;
		}
		catch (Exception ex)
		{
			Log.Warning($"Daedalus state '{symbol.Name}' failed: {ex.Message}");
			return 0;
		}
		finally
		{
			Vm.GlobalSelf = previousSelf;
			Vm.GlobalOther = previousOther;
			Vm.GlobalVictim = previousVictim;
			_traceState = previousTrace;
		}
	}

	private void SetVisualBody(NpcInstance npc, string body, int bodyTexNr, int bodyTexColor,
		string head, int headTexNr, int teethTexNr, int armor)
	{
		var visual = GetVisual(npc);
		if (visual == null)
		{
			return;
		}

		visual.Body = body ?? string.Empty;
		visual.BodyTexture = bodyTexNr;
		visual.BodyColor = bodyTexColor;
		visual.Head = head ?? string.Empty;
		visual.HeadTexture = headTexNr;
		visual.TeethTexture = teethTexNr;
		visual.ArmorModel = string.Empty;

		if (armor > 0)
		{
			var item = GetItemInstance(armor);
			visual.ArmorModel = item?.VisualChange ?? string.Empty;
		}
	}

	private ItemInstance GetItemInstance(int symbolIndex)
	{
		if (_items.TryGetValue(symbolIndex, out var item))
		{
			return item;
		}

		if (_itemInstances.TryGetValue(symbolIndex, out item))
		{
			return item;
		}

		var symbol = Vm.GetSymbolByIndex(symbolIndex);
		if (symbol == null)
		{
			return null;
		}

		try
		{
			item = Vm.InitInstance<ItemInstance>(symbol);
			_itemInstances[symbolIndex] = item;
		}
		catch (Exception ex)
		{
			Log.Warning($"Unable to initialize item '{symbol.Name}' for an NPC visual: {ex.Message}");
		}

		return item;
	}

	/// <summary>
	/// Runs a complete diagnostic conversation with the named script NPC (or the first
	/// inserted one when <paramref name="npcInstance"/> is null), choosing the first available
	/// entry until the script ends the dialog. Every line is logged as it is spoken.
	/// </summary>
	public string RunDialogToCompletion(string npcInstance)
	{
		if (Vm == null)
		{
			return "no Daedalus VM";
		}

		var session = StartDialog(npcInstance);
		if (session == null)
		{
			return string.IsNullOrEmpty(npcInstance)
				? "no scripted NPCs"
				: $"no scripted NPC named '{npcInstance}'";
		}

		var result = session.RunToCompletion();
		CloseDialog();
		return result;
	}

	public DialogSession StartDialog(string npcInstance)
	{
		if (string.IsNullOrEmpty(npcInstance))
		{
			return StartDialogWithFirstNpc();
		}

		foreach (var binding in _npcs.Values)
		{
			if (string.Equals(binding.Vob.NpcInstance, npcInstance, StringComparison.OrdinalIgnoreCase))
			{
				return StartDialog(binding);
			}
		}

		foreach (var binding in _npcs.Values)
		{
			if (string.Equals(binding.Vob.Name, npcInstance, StringComparison.OrdinalIgnoreCase))
			{
				return StartDialog(binding);
			}
		}

		return null;
	}

	public DialogSession StartDialogWithFirstNpc()
	{
		foreach (var binding in _npcs.Values)
		{
			return StartDialog(binding);
		}

		return null;
	}

	public DialogSession StartDialog(NpcVob vob)
	{
		if (vob == null)
		{
			return null;
		}

		foreach (var binding in _npcs.Values)
		{
			if (ReferenceEquals(binding.Vob, vob))
			{
				return StartDialog(binding);
			}
		}

		return null;
	}

	private DialogSession StartDialog(NpcBinding npc)
	{
		if (Vm == null || Hero == null || _infosByNpc == null ||
		    !_infosByNpc.ContainsKey(npc.SymbolIndex))
		{
			return null;
		}

		CloseDialog();
		var session = new DialogSession(this, npc);
		_session = session;
		session.Begin();
		return session;
	}

	public void CloseDialog()
	{
		_session = null;
		_endRequested = false;
		_storedChoices.Clear();
	}

	/// <summary>Returns and clears an <c>AI_ProcessInfos</c> request, or null.</summary>
	public NpcVob ConsumeDialogRequest()
	{
		var vob = RequestedDialogNpc;
		RequestedDialogNpc = null;
		return vob;
	}

	internal List<DialogChoice> BuildChoices(DialogSession session, bool includeImportant)
	{
		var result = new List<DialogChoice>();
		if (_infosByNpc == null || !_infosByNpc.TryGetValue(session.Binding.SymbolIndex, out var infos))
		{
			return result;
		}

		for (var important = includeImportant ? 1 : 0; important >= 0; --important)
		{
			foreach (var info in infos)
			{
				if (info.Important != important)
				{
					continue;
				}

				// A non-permanent info is spent once the player has heard it.
				if (Knows(Hero, info.Index) && info.Permanent == 0)
				{
					continue;
				}

				// Important permanent entries are not repeated in the same conversation.
				if (info.Important != 0 && info.Permanent != 0 && session.Except.Contains(info.Information))
				{
					continue;
				}

				if (!CheckCondition(session.Binding, info))
				{
					continue;
				}

				result.Add(new DialogChoice
				{
					Text = info.Description ?? string.Empty,
					Function = info.Information,
					Sort = info.Nr,
					IsTrade = info.Trade != 0,
					Info = info
				});
			}

			if (result.Count > 0)
			{
				result.Sort(CompareChoices);
				return result;
			}
		}

		return result;
	}

	internal List<DialogChoice> TakeStoredChoices(InfoInstance info)
	{
		if (info == null || !_storedChoices.TryGetValue(info.Index, out var stored) || stored.Count == 0)
		{
			return new List<DialogChoice>();
		}

		var result = new List<DialogChoice>(stored);
		result.Sort(CompareChoices);
		return result;
	}

	internal bool ExecuteInfo(NpcBinding npc, DialogChoice choice)
	{
		_endRequested = false;
		var info = choice.Info;
		if (info == null || choice.Function <= 0)
		{
			Log.Warning($"Dialog choice '{choice.Text}' has no information function.");
			return false;
		}

		if (info.Information == choice.Function)
		{
			MarkKnown(Hero, info.Index);
		}
		else
		{
			RemoveStoredChoice(info.Index, choice.Function);
		}

		try
		{
			Vm.GlobalSelf = npc.Instance;
			Vm.GlobalOther = Hero;
			Vm.Call(choice.Function);
		}
		catch (Exception ex)
		{
			Log.Warning($"Dialog function '{choice.Function}' failed: {ex.Message}");
		}

		return _endRequested;
	}

	private void OnAiOutput(NpcInstance self, NpcInstance target, string unit)
	{
		var text = ResolveDialogText(unit);
		var isPlayer = self != null && Hero != null && self.Index == Hero.Index;

		string speaker;
		if (isPlayer)
		{
			speaker = "Hero";
		}
		else if (self != null && _npcs.TryGetValue(self.Index, out var binding))
		{
			speaker = binding.Vob.Name;
		}
		else
		{
			speaker = self == null ? "?" : $"NPC {self.Index}";
		}

		if (_session == null)
		{
			// Ambient and AI output outside a conversation has no menu to display it.
			Log.Info($"Daedalus output {speaker}: {(string.IsNullOrEmpty(text) ? $"<{unit}>" : text)}");
			return;
		}

		_session.AddLine(new DialogLine
		{
			Speaker = speaker,
			Text = text,
			Unit = unit,
			IsPlayer = isPlayer
		});
	}

	private string ResolveDialogText(string unit)
	{
		if (_dialogText != null && !string.IsNullOrEmpty(unit) && _dialogText.TryGetValue(unit, out var text))
		{
			return text ?? string.Empty;
		}

		return string.Empty;
	}

	private void AddStoredChoice(int infoId, string text, int function)
	{
		if (_infosBySymbol == null || !_infosBySymbol.TryGetValue(infoId, out var info))
		{
			return;
		}

		if (!_storedChoices.TryGetValue(infoId, out var list))
		{
			list = new List<DialogChoice>();
			_storedChoices[infoId] = list;
		}

		list.Add(new DialogChoice
		{
			Text = text ?? string.Empty,
			Function = function,
			Sort = list.Count,
			Info = info
		});
	}

	private void ClearStoredChoices(int infoId)
	{
		if (_storedChoices.TryGetValue(infoId, out var list))
		{
			list.Clear();
		}
	}

	private void RemoveStoredChoice(int infoId, int function)
	{
		if (!_storedChoices.TryGetValue(infoId, out var list))
		{
			return;
		}

		for (var i = 0; i < list.Count; ++i)
		{
			if (list[i].Function == function)
			{
				list.RemoveAt(i);
				return;
			}
		}
	}

	private bool CheckCondition(NpcBinding npc, InfoInstance info)
	{
		if (info.Condition == 0)
		{
			return true;
		}

		var symbol = Vm.GetSymbolByIndex(info.Condition);
		if (symbol == null)
		{
			return false;
		}

		try
		{
			Vm.GlobalSelf = npc.Instance;
			Vm.GlobalOther = Hero;
			return Vm.Call<int>(symbol.Index) != 0;
		}
		catch (Exception ex)
		{
			if (_conditionFailures.Add(info.Index))
			{
				Log.Warning($"Dialog condition '{info.Description}' failed: {ex.Message}");
			}

			return false;
		}
	}

	private int CheckInfo(NpcInstance npcRef, int important)
	{
		if (npcRef == null || !_npcs.TryGetValue(npcRef.Index, out var npc) ||
			_infosByNpc == null || !_infosByNpc.TryGetValue(npc.SymbolIndex, out var infos))
		{
			return 0;
		}

		foreach (var info in infos)
		{
			if (info.Important != important)
			{
				continue;
			}

			if (Knows(Hero, info.Index) && info.Permanent == 0)
			{
				continue;
			}

			if (CheckCondition(npc, info))
			{
				return 1;
			}
		}

		return 0;
	}

	private void MarkKnown(NpcInstance npc, int info)
	{
		if (npc != null)
		{
			_knownInfos.Add((npc.Index, info));
		}
	}

	private bool Knows(NpcInstance npc, int info)
	{
		return npc != null && _knownInfos.Contains((npc.Index, info));
	}

	private void RequestDialog(NpcInstance npc)
	{
		if (_session != null || npc == null)
		{
			return;
		}

		if (_npcs.TryGetValue(npc.Index, out var binding))
		{
			RequestedDialogNpc = binding.Vob;
		}
	}

	private static int CompareChoices(DialogChoice left, DialogChoice right)
	{
		var bySort = left.Sort.CompareTo(right.Sort);
		return bySort != 0 ? bySort : left.Function.CompareTo(right.Function);
	}

	private void LoadDialogInfos()
	{
		var started = DateTime.UtcNow;
		_infosByNpc = new Dictionary<int, List<InfoInstance>>();
		_infosBySymbol = new Dictionary<int, InfoInstance>();

		foreach (var symbol in Vm.GetInstanceSymbols("C_INFO"))
		{
			try
			{
				var info = Vm.InitInstance<InfoInstance>(symbol);
				if (info == null)
				{
					continue;
				}

				_infosBySymbol[info.Index] = info;
				if (!_infosByNpc.TryGetValue(info.Npc, out var list))
				{
					list = new List<InfoInstance>();
					_infosByNpc[info.Npc] = list;
				}

				list.Add(info);
			}
			catch (Exception ex)
			{
				Log.Warning($"Unable to initialize dialog info '{symbol.Name}': {ex.Message}");
			}
		}

		var elapsed = DateTime.UtcNow - started;
		Log.Info(
			$"Daedalus dialogs: {_infosBySymbol.Count:N0} C_INFO instances for " +
			$"{_infosByNpc.Count:N0} NPC symbols in {elapsed.TotalSeconds:0.###}s.");
	}

	private void LoadCutscenes(string path)
	{
		if (!File.Exists(path))
		{
			Log.Warning($"Cutscene library not found: '{path}'.");
			return;
		}

		try
		{
			Cutscenes = new CutsceneLibrary(path);
			_dialogText = new Dictionary<string, string>(Cutscenes.Blocks.Count, StringComparer.OrdinalIgnoreCase);
			foreach (var block in Cutscenes.Blocks)
			{
				if (!string.IsNullOrEmpty(block.Name))
				{
					_dialogText[block.Name] = block.Message?.Text ?? string.Empty;
				}
			}

			Log.Info($"Cutscene library loaded: {Cutscenes.Blocks.Count:N0} blocks.");
		}
		catch (Exception ex)
		{
			Log.Warning($"Unable to load cutscene library: {ex.Message}");
		}
	}

	private void Boot(string worldName)
	{
		var world = Path.GetFileNameWithoutExtension(worldName ?? string.Empty).ToLowerInvariant();
		if (string.IsNullOrEmpty(world))
		{
			world = "newworld";
		}

		var started = DateTime.UtcNow;
		try
		{
			CallIfPresent("startup_global");
			CallIfPresent("startup_" + world);
			CallIfPresent("init_global");
			CallIfPresent("init_" + world);

			Booted = true;
			var elapsed = DateTime.UtcNow - started;
			Log.Info(
				$"Daedalus boot complete in {elapsed.TotalSeconds:0.###}s; " +
				$"{_npcs.Count:N0} NPCs, {_items.Count:N0} items, " +
				$"{UnboundCalls:N0} external calls used the default handler ({_unbound.Count:N0} distinct).");
		}
		catch (Exception ex)
		{
			Log.Error($"Daedalus boot failed: {ex.Message}");
		}
	}

	private void CallIfPresent(string name)
	{
		if (Vm.GetSymbolByName(name) == null)
		{
			Log.Info($"Daedalus entry point '{name}' is absent.");
			return;
		}

		Log.Info($"Daedalus calling '{name}'.");
		Vm.Call(name);
	}

	private void OnUnboundExternal(DaedalusVm vm, DaedalusSymbol symbol)
	{
		++UnboundCalls;
		var name = symbol?.Name ?? "?";
		if (DebugSettings.AiTrace && _traceState != null)
		{
			Log.Info($"AI trace: '{_traceState}' calls '{name}'.");
		}

		if (_unbound.Add(name))
		{
			Log.Info($"Daedalus external '{name}' has no implementation; using default return.");
		}
	}
}
