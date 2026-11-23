using Microsoft.Xna.Framework;
using System;
using System.Globalization;
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

	/// <summary>
	/// Whether per-part frustum culling is on. Null when the flag was not passed, so
	/// <see cref="Apply"/> leaves <see cref="DebugSettings"/> alone rather than silently
	/// resetting a value something else chose.
	/// </summary>
	public bool? Cull { get; private set; }

	/// <summary>
	/// Whether ZEN vobs that carry a model (as opposed to a static mesh) are instantiated.
	/// Null when the flag was not passed. Useful for telling a model-loading problem apart
	/// from a static-world one.
	/// </summary>
	public bool? Models { get; private set; }

	/// <summary>
	/// Where to write a PNG of the rendered frame, or null to not capture one. The image is
	/// read straight from the backbuffer, so it works where an OS screenshot of the window
	/// yields black for hardware-accelerated content.
	/// </summary>
	public string ScreenshotPath { get; private set; }

	/// <summary>
	/// Which frame to capture, counting from one, so a capture can wait for the world to
	/// load and the first frames to settle. Null when not specified.
	/// </summary>
	public int? ScreenshotFrame { get; private set; }

	/// <summary>
	/// Camera position as <c>x,y,z</c>, or null to keep the default. Together with
	/// <see cref="LookAtTarget"/> this parks the view on a known object, which is the
	/// quickest way to inspect a specific piece of the world.
	/// </summary>
	public Vector3? CameraPosition { get; private set; }

	/// <summary>Point the camera looks at as <c>x,y,z</c>, or null to keep the default.</summary>
	public Vector3? LookAtTarget { get; private set; }

	/// <summary>Whether to start with the wireframe view on. Null leaves it at its default.</summary>
	public bool? Wireframe { get; private set; }

	/// <summary>Whether to start with the bounding-box view on. Null leaves it at its default.</summary>
	public bool? Bounds { get; private set; }

	/// <summary>
	/// A model to place in front of the camera, with or without an extension, or null to
	/// place none. This is how a model that no world vob uses can still be inspected.
	/// </summary>
	public string SpawnModel { get; private set; }

	/// <summary>
	/// Whether to attach animation clips to models. Null when the flag was not passed, so
	/// <see cref="Apply"/> leaves <see cref="DebugSettings"/> alone.
	/// </summary>
	public bool? Animations { get; private set; }

	/// <summary>
	/// Name of an animation script to report on at startup, or null to report on none.
	/// Prints the clip list, frame rate and the first frame of a clip's bone tracks, which is
	/// the quickest way to see whether clips are reachable and readable.
	/// </summary>
	public string AnimationDump { get; private set; }

	/// <summary>
	/// Clip to print the first frame of, inside <see cref="AnimationDump"/>. Null prints only
	/// the clip list.
	/// </summary>
	public string AnimationClip { get; private set; }

	/// <summary>
	/// Clip to play on the model placed by <see cref="SpawnModel"/>, or null to leave the model
	/// in its bind pose. Resolved through the script's aliases, so an alias name works too.
	/// </summary>
	public string PlayAnimation { get; private set; }

	/// <summary>
	/// Freezes clip playback at a fixed pose, which makes a capture reproducible: the frame
	/// depends on <see cref="PlayTime"/> rather than on how long the process has been running.
	/// </summary>
	public bool? FrozenTime { get; private set; }

	/// <summary>Playback position to use with <see cref="FrozenTime"/>; defaults to zero.</summary>
	public float? PlayTime { get; private set; }

	/// <summary>
	/// Distance in front of the camera to place <see cref="SpawnModel"/>. Defaults to 400
	/// world units, which fits an actor-sized model in view.
	/// </summary>
	public float? SpawnDistance { get; private set; }

	/// <summary>
	/// Offset in camera space — right, up, forward — added when placing
	/// <see cref="SpawnModel"/>. Lets a capture put the model at the edge of the view, where
	/// culling mistakes show.
	/// </summary>
	public Vector3? SpawnOffset { get; private set; }

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

				case "-nocull":
					result.Cull = false;
					break;

				case "-nomodels":
					result.Models = false;
					break;

				case "-noanims":
					result.Animations = false;
					break;

				case "-freeze":
					result.FrozenTime = true;
					break;

				case "-wireframe":
					result.Wireframe = true;
					break;

				case "-bounds":
					result.Bounds = true;
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
					else if (arg.StartsWith("-cull:", StringComparison.OrdinalIgnoreCase))
					{
						result.Cull = result.ParseBool(arg.Substring("-cull:".Length), arg);
					}
					else if (arg.StartsWith("-models:", StringComparison.OrdinalIgnoreCase))
					{
						result.Models = result.ParseBool(arg.Substring("-models:".Length), arg);
					}
					else if (arg.StartsWith("-anims:", StringComparison.OrdinalIgnoreCase))
					{
						result.Animations = result.ParseBool(arg.Substring("-anims:".Length), arg);
					}
					else if (arg.StartsWith("-play:", StringComparison.OrdinalIgnoreCase))
					{
						var clip = arg.Substring("-play:".Length);
						if (string.IsNullOrEmpty(clip))
						{
							// Leave the property null so "not specified" stays distinguishable
							// from "specified but empty", like the other value options.
							Log.Error("Option '-play:' requires a clip name.");
							result._invalid = true;
						}
						else
						{
							result.PlayAnimation = clip;
						}
					}
					else if (arg.StartsWith("-animtime:", StringComparison.OrdinalIgnoreCase))
					{
						if (float.TryParse(arg.Substring("-animtime:".Length),
							System.Globalization.NumberStyles.Float,
							System.Globalization.CultureInfo.InvariantCulture,
							out var animTime))
						{
							result.PlayTime = animTime;
						}
						else
						{
							Log.Error("Option '-animtime:' requires a number of seconds.");
							result._invalid = true;
						}
					}
					else if (arg.StartsWith("-animdump:", StringComparison.OrdinalIgnoreCase))
					{
						result.AnimationDump = arg.Substring("-animdump:".Length);
						if (string.IsNullOrEmpty(result.AnimationDump))
						{
							Log.Error("Option '-animdump:' requires a script name.");
							result._invalid = true;
						}
					}
					else if (arg.StartsWith("-animclip:", StringComparison.OrdinalIgnoreCase))
					{
						result.AnimationClip = arg.Substring("-animclip:".Length);
						if (string.IsNullOrEmpty(result.AnimationClip))
						{
							Log.Error("Option '-animclip:' requires a clip name.");
							result._invalid = true;
						}
					}
					else if (arg.StartsWith("-screenshot:", StringComparison.OrdinalIgnoreCase))
					{
						result.ScreenshotPath = arg.Substring("-screenshot:".Length);
						if (string.IsNullOrEmpty(result.ScreenshotPath))
						{
							Log.Error("Option '-screenshot:' requires a file path.");
							result._invalid = true;
						}
					}
					else if (arg.StartsWith("-screenshotframe:", StringComparison.OrdinalIgnoreCase))
					{
						var value = arg.Substring("-screenshotframe:".Length);
						if (int.TryParse(value, out var frame) && frame > 0)
						{
							result.ScreenshotFrame = frame;
						}
						else
						{
							Log.Error($"Option '-screenshotframe:' requires a positive frame number, got '{value}'.");
							result._invalid = true;
						}
					}
					else if (arg.StartsWith("-cam:", StringComparison.OrdinalIgnoreCase))
					{
						result.CameraPosition = result.ParseVector(arg.Substring("-cam:".Length), arg);
					}
					else if (arg.StartsWith("-lookat:", StringComparison.OrdinalIgnoreCase))
					{
						result.LookAtTarget = result.ParseVector(arg.Substring("-lookat:".Length), arg);
					}
					else if (arg.StartsWith("-wireframe:", StringComparison.OrdinalIgnoreCase))
					{
						result.Wireframe = result.ParseBool(arg.Substring("-wireframe:".Length), arg);
					}
					else if (arg.StartsWith("-bounds:", StringComparison.OrdinalIgnoreCase))
					{
						result.Bounds = result.ParseBool(arg.Substring("-bounds:".Length), arg);
					}
					else if (arg.StartsWith("-model:", StringComparison.OrdinalIgnoreCase))
					{
						result.SpawnModel = arg.Substring("-model:".Length);
						if (string.IsNullOrEmpty(result.SpawnModel))
						{
							Log.Error("Option '-model:' requires a model name.");
							result._invalid = true;
						}
					}
					else if (arg.StartsWith("-modeldist:", StringComparison.OrdinalIgnoreCase))
					{
						var value = arg.Substring("-modeldist:".Length);
						if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var distance) && distance > 0f)
						{
							result.SpawnDistance = distance;
						}
						else
						{
							Log.Error($"Option '-modeldist:' requires a positive distance, got '{value}'.");
							result._invalid = true;
						}
					}
					else if (arg.StartsWith("-modeloffset:", StringComparison.OrdinalIgnoreCase))
					{
						result.SpawnOffset = result.ParseVector(arg.Substring("-modeloffset:".Length), arg);
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

	/// <summary>Pushes the parsed debug switches into <see cref="DebugSettings"/>.</summary>
	public void Apply()
	{
		if (Cull.HasValue)
		{
			DebugSettings.CullEnabled = Cull.Value;
		}

		if (Models.HasValue)
		{
			DebugSettings.ModelsEnabled = Models.Value;
		}

		if (Animations.HasValue)
		{
			DebugSettings.AnimationsEnabled = Animations.Value;
		}
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
		"Usage: -g <path> [-game:<modfile.ini>] [-w <world.zen>] [-nomenu] [-devmode] [-nocull] [-nomodels] [-noanims] " +
		"[-screenshot:<file.png>] [-screenshotframe:<n>] [-cam:<x,y,z>] [-lookat:<x,y,z>] [-wireframe] [-bounds] " +
		"[-model:<name>] [-modeldist:<n>] [-modeloffset:<x,y,z>] [-anims:true|false] [-animdump:<script>] [-animclip:<clip>] " +
		"[-play:<clip>] [-freeze] [-animtime:<seconds>]";

	private Vector3? ParseVector(string value, string option)
	{
		var parts = value.Split(',');
		if (parts.Length == 3 &&
			float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
			float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
			float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
		{
			return new Vector3(x, y, z);
		}

		Log.Error($"Option '{option}' requires three comma-separated numbers, got '{value}'.");
		_invalid = true;
		return null;
	}

	private bool? ParseBool(string value, string option)
	{		if (bool.TryParse(value, out var result))
		{
			return result;
		}

		if (value == "1" || value == "0")
		{
			return value == "1";
		}

		Log.Error($"Option '{option}' requires true or false.");
		_invalid = true;
		return null;
	}

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
