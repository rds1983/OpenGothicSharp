using AssetManagementBase;
using System;

namespace OpenGothic;

internal static class Program
{
	static void Main(string[] args)
	{
		Log.Initialize();

		try
		{
			AMBConfiguration.Logger = Log.Info;

			Log.Info($"OpenGothicSharp {OG.Version} starting");

			using (var game = new OgGame())
			{
				game.Run();
			}
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
