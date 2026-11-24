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
	/// Whether to build the static collision world and run the capsule self-check at startup,
	/// or null when the flag was not passed.
	/// </summary>
	public bool Physics { get; private set; }

	/// <summary>
	/// Whether to start in walking mode: a player capsule driven by WASD and mouse look, with
	/// collision streamed around it. Without it the camera is the static diagnostic view.
	/// </summary>
	/// <summary>
	/// Whether walking mode was explicitly requested. A normal launch walks without it; the
	/// flag forces walking alongside diagnostic options.
	/// </summary>
	public bool Walk { get; private set; }

	/// <summary>Force the static diagnostic camera even for a normal launch.</summary>
	public bool NoWalk { get; private set; }

	/// <summary>Whether walking mode starts in the third-person camera instead of first person.</summary>
	public bool ThirdPerson { get; private set; }

	/// <summary>
	/// Whether walk mode starts on. This is a diagnostic version of the in-game Shift toggle,
	/// useful when a capture has no keyboard input.
	/// </summary>
	public bool WalkMode { get; private set; }

	/// <summary>
	/// Resolve the focus target this many times on the first walking frames, as if the
	/// interact key had been pressed. Useful for captures, where synthetic key events are
	/// unreliable; consecutive actions are spaced to let the mob animation finish.
	/// </summary>
	public int Interact { get; private set; }

	/// <summary>
	/// Whether the game should start with the playable capsule. Explicit <see cref="Walk"/> or
	/// <see cref="ThirdPerson"/> wins; otherwise a plain launch walks, while a diagnostic launch
	/// (a spawned/composed/attached model, a camera override, a screenshot, a vob query or the
	/// physics self-check) keeps the fixed camera that the capture or inspection expects.
	/// </summary>
	public bool StartWalking =>
		!NoWalk &&
		(Walk || ThirdPerson || (!Physics &&
			string.IsNullOrEmpty(SpawnModel) &&
			string.IsNullOrEmpty(ComposedSkeleton) &&
			string.IsNullOrEmpty(AttachPoint) &&
			!CameraPosition.HasValue &&
			!LookAtTarget.HasValue &&
			!VobStats &&
			!VobFind.HasValue &&
			string.IsNullOrEmpty(ScreenshotPath)));

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
	/// Skeleton to build the spawn from when <see cref="ComposedMeshes"/> is set, or null.
	/// A composed model is one skeleton plus meshes that may each carry their own, which is
	/// how the <c>ZS_*</c> attach nodes become available.
	/// </summary>
	public string ComposedSkeleton { get; private set; }

	/// <summary>Meshes merged into the composed model, or null when none was requested.</summary>
	public string[] ComposedMeshes { get; private set; }

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
	/// Clip to play on a layer over <see cref="PlayAnimation"/>, or null. Its script layer
	/// decides which bones it overrides, so an upper-body clip leaves the legs alone.
	/// </summary>
	public string OverlayAnimation { get; private set; }

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

	/// <summary>
	/// World position to start the walking capsule at, or null for the placeholder plaza
	/// spot. The value is treated as the eye; the player is dropped to the ground below it.
	/// </summary>
	public Vector3? PlayerPosition { get; private set; }

	/// <summary>
	/// Seconds to hold the forward key automatically after spawning the player. Null leaves
	/// input to the developer; a value makes a diagnostic run walk on its own.
	/// </summary>
	public float? WalkTestSeconds { get; private set; }

	/// <summary>
	/// Facing yaw in degrees for the walking player, or null to use the camera's yaw. This is
	/// the reliable way to aim a diagnostic walk: <c>-lookat:</c> measures from the camera's
	/// own position, which is not the player.
	/// </summary>
	public float? PlayerYaw { get; private set; }

	/// <summary>Whether to log a count of the typed vob tree after the world loads.</summary>
	public bool VobStats { get; private set; }

	/// <summary>Point to query the typed vob tree around, or null for no query.</summary>
	public Vector3? VobFind { get; private set; }

	/// <summary>Radius of the <see cref="VobFind"/> query; null uses the default.</summary>
	public float? VobRadius { get; private set; }

	/// <summary>
	/// Yaw in degrees applied to the placed <see cref="SpawnModel"/> or composed model, or null
	/// for none. A model faces away from the default camera, so 180 shows its front.
	/// </summary>
	public float? SpawnYaw { get; private set; }

	/// <summary>
	/// Bone of the model placed by <see cref="SpawnModel"/> to hang <see cref="AttachVisual"/>
	/// on, or null.
	/// </summary>
	public string AttachPoint { get; private set; }

	/// <summary>Model or multi-resolution mesh placed on <see cref="AttachPoint"/>.</summary>
	public string AttachVisual { get; private set; }

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

				case "-physics":
					result.Physics = true;
					break;

				case "-walk":
					result.Walk = true;
					break;

				case "-nowalk":
					result.NoWalk = true;
					break;

				case "-thirdperson":
					result.ThirdPerson = true;
					break;
				case "-walkmode":
					result.WalkMode = true;
					break;
				case "-interact":
					result.Interact = 1;
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
					else if (arg.StartsWith("-overlay:", StringComparison.OrdinalIgnoreCase))
					{
						var clip = arg.Substring("-overlay:".Length);
						if (string.IsNullOrEmpty(clip))
						{
							Log.Error("Option '-overlay:' requires a clip name.");
							result._invalid = true;
						}
						else
						{
							result.OverlayAnimation = clip;
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
					else if (arg.StartsWith("-compose:", StringComparison.OrdinalIgnoreCase))
					{
						var value = arg.Substring("-compose:".Length);
						var separator = value.IndexOf(':');

						if (separator <= 0 || separator >= value.Length - 1)
						{
							Log.Error("Option '-compose:' requires <skeleton>:<mesh>[,<mesh>...].");
							result._invalid = true;
						}
						else
						{
							var meshes = value.Substring(separator + 1).Split(',');
							var valid = true;
							foreach (var mesh in meshes)
							{
								if (string.IsNullOrWhiteSpace(mesh))
								{
									valid = false;
									break;
								}
							}

							if (!valid)
							{
								Log.Error("Option '-compose:' requires a mesh name after each comma.");
								result._invalid = true;
							}
							else
							{
								result.ComposedSkeleton = value.Substring(0, separator);
								result.ComposedMeshes = meshes;
							}
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
					else if (arg.StartsWith("-player:", StringComparison.OrdinalIgnoreCase))
					{
						result.PlayerPosition = result.ParseVector(arg.Substring("-player:".Length), arg);
					}
					else if (arg.StartsWith("-yaw:", StringComparison.OrdinalIgnoreCase))
					{
						var value = arg.Substring("-yaw:".Length);
						if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var yaw))
						{
							result.PlayerYaw = yaw;
						}
						else
						{
							Log.Error($"Option '-yaw:' requires an angle in degrees, got '{value}'.");
							result._invalid = true;
						}
					}
					else if (arg.Equals("-vobstats", StringComparison.OrdinalIgnoreCase))
					{
						result.VobStats = true;
					}
					else if (arg.StartsWith("-vobfind:", StringComparison.OrdinalIgnoreCase))
					{
						result.VobFind = result.ParseVector(arg.Substring("-vobfind:".Length), arg);
					}
					else if (arg.StartsWith("-vobradius:", StringComparison.OrdinalIgnoreCase))
					{
						var value = arg.Substring("-vobradius:".Length);
						if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var radius) && radius > 0f)
						{
							result.VobRadius = radius;
						}
						else
						{
							Log.Error($"Option '-vobradius:' requires a positive radius, got '{value}'.");
							result._invalid = true;
						}
					}
					else if (arg.StartsWith("-interact:", StringComparison.OrdinalIgnoreCase))
					{
						var value = arg.Substring("-interact:".Length);
						if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0)
						{
							result.Interact = count;
						}
						else
						{
							Log.Error($"Option '-interact:' requires a positive count, got '{value}'.");
							result._invalid = true;
						}
					}
					else if (arg.StartsWith("-walktest:", StringComparison.OrdinalIgnoreCase))
					{
						var value = arg.Substring("-walktest:".Length);
						if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds != 0f)
						{
							result.WalkTestSeconds = seconds;
						}
						else
						{
							Log.Error($"Option '-walktest:' requires a non-zero duration, got '{value}'.");
							result._invalid = true;
						}
					}
					else if (arg.StartsWith("-modelrot:", StringComparison.OrdinalIgnoreCase))
					{
						var value = arg.Substring("-modelrot:".Length);
						if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var yaw))
						{
							result.SpawnYaw = yaw;
						}
						else
						{
							Log.Error($"Option '-modelrot:' requires a yaw in degrees, got '{value}'.");
							result._invalid = true;
						}
					}
					else if (arg.StartsWith("-attach:", StringComparison.OrdinalIgnoreCase))
					{
						var value = arg.Substring("-attach:".Length);
						var separator = value.IndexOf(':');

						if (separator <= 0 || separator >= value.Length - 1)
						{
							Log.Error("Option '-attach:' requires <point>:<model>.");
							result._invalid = true;
						}
						else
						{
							result.AttachPoint = value.Substring(0, separator);
							result.AttachVisual = value.Substring(separator + 1);
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

		if (Physics || StartWalking)
		{
			DebugSettings.CollisionEnabled = true;
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

		if (!string.IsNullOrEmpty(SpawnModel) && !string.IsNullOrEmpty(ComposedSkeleton))
		{
			Log.Error("Options '-model:' and '-compose:' both place a spawn model; use one.");
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
		"[-screenshot:<file.png>] [-screenshotframe:<n>] [-cam:<x,y,z>] [-lookat:<x,y,z>] [-wireframe] [-bounds] [-physics] [-walk] [-nowalk] [-thirdperson] [-walkmode] [-interact[:<count>]] " +
		"[-model:<name>] [-compose:<skeleton>:<mesh>[,<mesh>...]] [-modeldist:<n>] [-modeloffset:<x,y,z>] [-modelrot:<degrees>] [-attach:<point>:<model>] [-anims:true|false] [-animdump:<script>] [-animclip:<clip>] " +
		"[-player:<x,y,z>] [-yaw:<degrees>] [-walktest:<seconds>] [-vobstats] [-vobfind:<x,y,z>] [-vobradius:<n>] [-play:<clip>] [-overlay:<clip>] [-freeze] [-animtime:<seconds>]";

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
