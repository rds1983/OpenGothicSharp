using System;
using System.IO;

namespace OpenGothic;

public class CommandLine
{
	public static CommandLine Instance { get; private set; }

	public string GothicPath { get; private set; }
	public string ModFile { get; private set; }
	public string World { get; private set; }
	public bool NoMenu { get; private set; }
	public bool DevMode { get; private set; }

	private bool _invalid;

	public static CommandLine Parse(string[] args)
	{
		var result = new CommandLine();

		for (var i = 0; i < args.Length; ++i)
		{
			var arg = args[i];

			switch (arg)
			{
				case "-g":
					result.GothicPath = result.TakeValue(args, ref i, arg);
					break;

				case "-w":
					result.World = result.TakeValue(args, ref i, arg);
					break;

				case "-nomenu":
					result.NoMenu = true;
					break;

				case "-devmode":
					result.DevMode = true;
					break;

				default:
					if (arg.StartsWith("-game:", StringComparison.OrdinalIgnoreCase))
					{
						result.ModFile = arg.Substring("-game:".Length);
						if (string.IsNullOrEmpty(result.ModFile))
						{
							Log.Error("Option '-game:' requires a value.");
							result._invalid = true;
						}
					}
					else
					{
						Log.Warning($"Ignoring unknown command line argument '{arg}'.");
					}
					break;
			}
		}

		Instance = result;
		return result;
	}

	public bool Validate()
	{
		if (_invalid)
		{
			Log.Error(Usage);
			return false;
		}

		if (string.IsNullOrEmpty(GothicPath))
		{
			Log.Error($"Gothic installation path is not specified. Pass it with -g <path>.");
			Log.Error(Usage);
			return false;
		}

		if (!Directory.Exists(GothicPath))
		{
			Log.Error($"Gothic installation folder does not exist: '{GothicPath}'.");
			return false;
		}

		var dataFolder = Path.Combine(GothicPath, "Data");
		if (!Directory.Exists(dataFolder))
		{
			Log.Error($"'{GothicPath}' does not look like a Gothic installation (no 'Data' folder).");
			return false;
		}

		return true;
	}

	private const string Usage =
		"Usage: -g <path> [-game:<modfile.ini>] [-w <world.zen>] [-nomenu] [-devmode]";

	private string TakeValue(string[] args, ref int i, string option)
	{
		if (i + 1 >= args.Length || args[i + 1].StartsWith("-"))
		{
			Log.Error($"Option '{option}' requires a value.");
			_invalid = true;
			return null;
		}

		return args[++i];
	}
}
