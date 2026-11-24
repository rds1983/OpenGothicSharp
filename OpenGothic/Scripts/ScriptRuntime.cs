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
/// Loads the compiled Daedalus game script and runs its startup entry points. G1 registers a
/// default external handler, so a script routine the engine does not implement yet logs once
/// and receives the VM's default return instead of aborting the startup.
/// </summary>
public sealed class ScriptRuntime
{
	private sealed class NpcBinding
	{
		public NpcInstance Instance;
		public NpcVob Vob;
		public string SpawnPoint;
	}

	private readonly HashSet<string> _unbound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<int, NpcBinding> _npcs = new Dictionary<int, NpcBinding>();
	private readonly Dictionary<int, ItemInstance> _items = new Dictionary<int, ItemInstance>();

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

	/// <summary>Last journal line added through the script's <c>LOG_ADDENTRY</c> external.</summary>
	public string LastLogEntry { get; private set; }

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
			var instance = Vm.InitInstance<NpcInstance>(symbol);
			if (instance == null)
			{
				return;
			}

			var binding = new NpcBinding
			{
				Instance = instance,
				SpawnPoint = spawnPoint,
				Vob = CreateNpcVob(instance, symbol.Name)
			};
			instance.UserData = binding;
			_npcs[symbolIndex] = binding;

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
			Log.Warning($"Unable to initialize script NPC '{symbol.Name}': {ex.Message}");
		}
	}

	private static NpcVob CreateNpcVob(NpcInstance instance, string symbolName)
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

		// The node is intentionally detached until the world/waypoint layer gives it a position.
		return (NpcVob)VobFactory.Create(source, null, Matrix.Identity, Matrix.Identity);
	}

	private void InsertItem(int symbolIndex, string spawnPoint)	{
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

	/// <summary>
	/// Diagnostic interaction for the script object bridge: call the real NPC routine
	/// <c>B_ASSESSTALK</c> with the first inserted NPC, then let the script add a journal
	/// entry. The journal external proves the Daedalus side effect reached the engine.
	/// </summary>
	public string TalkToFirstNpc()
	{
		if (Vm == null || _npcs.Count == 0)
		{
			return "no scripted NPCs";
		}

		NpcBinding target = null;
		foreach (var binding in _npcs.Values)
		{
			target = binding;
			break;
		}

		LastLogEntry = null;
		try
		{
			var assess = Vm.GetSymbolByName("B_ASSESSTALK");
			if (assess != null && Hero != null)
			{
				Vm.Call(assess.Index, target.Instance, Hero);
			}

			var log = Vm.GetSymbolByName("B_LOGENTRY");
			if (log != null)
			{
				Vm.Call(log.Index, $"You speak with {target.Vob.Name}.");
			}
		}
		catch (Exception ex)
		{
			return $"interaction failed: {ex.Message}";
		}

		return string.IsNullOrEmpty(LastLogEntry)
			? $"talked to '{target.Vob.Name}'; no journal side effect"
			: $"talked to '{target.Vob.Name}'; journal '{LastLogEntry}'";
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
		if (_unbound.Add(name))
		{
			Log.Info($"Daedalus external '{name}' has no implementation; using default return.");
		}
	}
}
