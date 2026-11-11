using System;
using System.IO;

namespace OpenGothic.Tools;

internal static class Program
{
	private const string DefaultWorld = "NEWWORLD.ZEN";

	private static int Main(string[] args)
	{
		if (args.Length < 1)
		{
			WriteUsage();
			return 1;
		}

		var folder = Path.GetFullPath(args[0]);
		if (!Directory.Exists(Path.Combine(folder, "Data")))
		{
			Console.Error.WriteLine($"error: '{folder}' does not look like a Gothic II installation (no 'Data' folder).");
			return 2;
		}

		var worldName = args.Length > 1 ? args[1] : DefaultWorld;

		using (var loader = new WorldLoader(folder, worldName))
		{
			try
			{
				loader.Run();
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
				return 3;
			}

			return loader.Succeeded ? 0 : 4;
		}
	}

	private static void WriteUsage()
	{
		Console.WriteLine("Usage: OpenGothic.Console <gothic-folder> [world.zen]");
		Console.WriteLine();
		Console.WriteLine("  <gothic-folder>  Path to the Gothic II installation (must contain 'Data').");
		Console.WriteLine($"  [world.zen]      World to load. Defaults to {DefaultWorld}.");
	}
}
