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

			Log.Info($"NoMenu: {commandLine.NoMenu}, DevMode: {commandLine.DevMode}");

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
