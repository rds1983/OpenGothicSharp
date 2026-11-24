using System;
using System.Collections.Generic;
using System.IO;
using ZenKit;
using ZenKit.Daedalus;

namespace OpenGothic.Scripts;

/// <summary>
/// Loads the compiled Daedalus game script and runs its startup entry points. G1 registers a
/// default external handler, so a script routine the engine does not implement yet logs once
/// and receives the VM's default return instead of aborting the startup.
/// </summary>
public sealed class ScriptRuntime
{
	private readonly HashSet<string> _unbound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public DaedalusVm Vm { get; private set; }

	/// <summary>Dialog/output-unit cutscene library from <c>Ou.dat</c>, or null if absent.</summary>
	public CutsceneLibrary Cutscenes { get; private set; }

	public bool Booted { get; private set; }

	/// <summary>Number of calls that reached the default external handler.</summary>
	public int UnboundCalls { get; private set; }

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
		LoadCutscenes(Path.Combine(scriptFolder, "CONTENT", "CUTSCENE", "Ou.dat"));

		Log.Info($"Daedalus script loaded: {Vm.SymbolCount:N0} symbols, {Vm.Symbols.Count:N0} listed.");

		Boot(worldName);
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
