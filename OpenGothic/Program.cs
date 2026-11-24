using AssetManagementBase;
using System;
using System.IO;

namespace OpenGothic;

internal static class Program
{
	static int Main(string[] args)
	{
		Log.Initialize();

		try
		{
			AMBConfiguration.Logger = Log.Info;

			Log.Info($"OpenGothicSharp {OG.Version} starting");

			var commandLine = CommandLine.Parse(args);
			if (!commandLine.Validate())
			{
				return 1;
			}

			Log.Info($"Gothic path: {commandLine.GothicPath}");

			if (commandLine.ModFile != null)
			{
				Log.Info($"Mod: {commandLine.ModFile}");
			}

			if (commandLine.World != null)
			{
				Log.Info($"World: {commandLine.World}");
			}

			commandLine.Apply();

			Log.Info($"NoMenu: {commandLine.NoMenu}, DevMode: {commandLine.DevMode}");
			Log.Info($"Cull: {DebugSettings.CullEnabled}, Models: {DebugSettings.ModelsEnabled}, Animations: {DebugSettings.AnimationsEnabled}, Collision: {DebugSettings.CollisionEnabled}");
			Log.Info($"Screenshot: {commandLine.ScreenshotPath ?? "(off)"}, frame: {commandLine.ScreenshotFrame?.ToString() ?? "(first)"}");
			Log.Info($"Camera: {commandLine.CameraPosition?.ToString() ?? "(default)"}, look at: {commandLine.LookAtTarget?.ToString() ?? "(default)"}");
			Log.Info($"Wireframe: {commandLine.Wireframe?.ToString() ?? "(off)"}, bounds: {commandLine.Bounds?.ToString() ?? "(off)"}, physics: {commandLine.Physics}, walk: {commandLine.StartWalking} (flag {commandLine.Walk}, no-walk {commandLine.NoWalk}), third person: {commandLine.ThirdPerson}");
			Log.Info($"Spawn model: {commandLine.SpawnModel ?? "(none)"}, distance: {commandLine.SpawnDistance?.ToString() ?? "(default)"}, offset: {commandLine.SpawnOffset?.ToString() ?? "(none)"}, yaw: {commandLine.SpawnYaw?.ToString() ?? "(none)"}");
			Log.Info($"Player start: {commandLine.PlayerPosition?.ToString() ?? "(default plaza)"}, yaw: {commandLine.PlayerYaw?.ToString() ?? "(camera)"}");
			Log.Info($"Walk test: {commandLine.WalkTestSeconds?.ToString() ?? "(off)"}");
			Log.Info($"Compose: {commandLine.ComposedSkeleton ?? "(none)"} <- {(commandLine.ComposedMeshes == null ? "(none)" : string.Join(",", commandLine.ComposedMeshes))}");
			Log.Info($"Attach: {commandLine.AttachPoint ?? "(none)"} <- {commandLine.AttachVisual ?? "(none)"}");
			Log.Info($"Animation dump: {commandLine.AnimationDump ?? "(off)"}, clip: {commandLine.AnimationClip ?? "(none)"}");
			Log.Info($"Play: {commandLine.PlayAnimation ?? "(none)"}, overlay: {commandLine.OverlayAnimation ?? "(none)"}, freeze: {commandLine.FrozenTime?.ToString() ?? "(off)"}, time: {commandLine.PlayTime?.ToString() ?? "(live)"}");

			var settings = new Settings();
			settings.Load(commandLine.GothicPath, Directory.GetCurrentDirectory());
			OG.Settings = settings;

			Log.Info($"Music volume: {settings.MusicVolume}, sound volume: {settings.SoundVolume}");
			Log.Info($"Camera FOV: {settings.CameraFov}, FPS limit: {settings.FpsLimit}");

			using (var game = new OgGame())
			{
				game.Run();
			}

			return 0;
		}
		catch (Exception ex)
		{
			Log.Error($"Fatal error: {ex}");
			throw;
		}
		finally
		{
			Log.Shutdown();
		}
	}
}
