using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Graphics;
using OpenGothic.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using ZenKit;

namespace OpenGothic;

partial class Assets
{
	/// <summary>Model stem to script stem, indexed by the skeleton each script names.</summary>
	private Dictionary<string, string> _scriptBySkeleton;

	/// <summary>Model stem to script stem, indexed by the <c>.ASC</c> meshes a script drives.</summary>
	private Dictionary<string, string> _scriptByMesh;

	/// <summary>
	/// Converted clip sets by cache key — the script plus the exact bone sequence the channels
	/// were remapped onto. Sharing them is what stops every armour model from re-reading the
	/// same 784 <c>.MAN</c> files.
	/// </summary>
	private readonly Dictionary<string, ModelAnimationSet> _animationSets = new Dictionary<string, ModelAnimationSet>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Builds the model-to-script index. A model file carries no reference to a script, so the
	/// only way to find a model's clips is to read every script once and index them both by the
	/// skeleton they name and by the meshes they can drive.
	/// </summary>
	private void BuildScriptIndex()
	{
		if (_scriptBySkeleton != null)
		{
			return;
		}

		_scriptBySkeleton = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		_scriptByMesh = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		var scripts = 0;

		foreach (var name in Keys)
		{
			// Gothic 1 spells the script .MDS; Gothic 2 ships .MSB. Both parse the same way.
			if (!name.EndsWith(".MSB", StringComparison.OrdinalIgnoreCase) &&
				!name.EndsWith(".MDS", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var stem = Path.GetFileNameWithoutExtension(name);

			IModelScript script;
			try
			{
				script = LoadRecord(GetLastRecord(name).Node, r => new ModelScript(r));
			}
			catch (Exception ex)
			{
				Log.Warning($"Unable to read model script '{name}': {ex.Message}");
				continue;
			}

			++scripts;

			if (!string.IsNullOrEmpty(script.SkeletonName))
			{
				_scriptBySkeleton.TryAdd(script.SkeletonName, stem);
			}

			foreach (var mesh in script.Meshes)
			{
				var meshStem = Path.GetFileNameWithoutExtension(mesh);
				if (!string.IsNullOrEmpty(meshStem))
				{
					_scriptByMesh.TryAdd(meshStem, stem);
				}
			}
		}

		Log.Info($"Model script index: {scripts} scripts, {_scriptBySkeleton.Count} skeleton names, {_scriptByMesh.Count} meshes.");
	}

	/// <summary>
	/// Finds the model script that owns a model, or null when the model is driven by none
	/// (a hand-authored prop). The mesh list wins over the skeleton name because it is what
	/// reaches the armour models, which are not skeletons of their own.
	/// </summary>
	private string FindScriptForModel(string modelStem)
	{
		BuildScriptIndex();

		if (_scriptByMesh.TryGetValue(modelStem, out var byMesh))
		{
			return byMesh;
		}

		if (_scriptBySkeleton.TryGetValue(modelStem, out var bySkeleton))
		{
			return bySkeleton;
		}

		// A script whose own stem is the model stem drives it too; that is the last resort
		// before treating the model as prop-only.
		if (_allRecords.ContainsKey(modelStem + ".MSB") || _allRecords.ContainsKey(modelStem + ".MDS"))
		{
			return modelStem;
		}

		return null;
	}

	/// <summary>
	/// Reads a script's skeleton, for node names only. A clip's channel indices address this
	/// hierarchy, not the target model's, and the model may lack nodes this one has (the nine
	/// <c>ZS_*</c> attach nodes, for instance).
	/// </summary>
	private IModelHierarchy LoadScriptSkeleton(IModelScript script, string scriptStem)
	{
		// The script's own skeleton name does not always have a hierarchy file: HUMANS names
		// HUM_BODY_NAKED0, which ships only a .MDM. Fall back to the script's own stem, which
		// gives HUMANS.MDH.
		var candidates = new List<string>();

		if (!string.IsNullOrEmpty(script.SkeletonName))
		{
			var skeleton = script.SkeletonName.ToUpper();
			candidates.Add(skeleton + ".MDL");
			candidates.Add(skeleton + ".MDH");
		}

		candidates.Add(scriptStem.ToUpper() + ".MDL");
		candidates.Add(scriptStem.ToUpper() + ".MDH");

		foreach (var candidate in candidates)
		{
			if (!_allRecords.ContainsKey(candidate))
			{
				continue;
			}

			try
			{
				return LoadRecord(GetLastRecord(candidate).Node, r =>
					candidate.EndsWith(".MDL", StringComparison.OrdinalIgnoreCase)
						? KeepNativeOwner(new ZenKit.Model(r)).Hierarchy
						: new ModelHierarchy(r));
			}
			catch (Exception ex)
			{
				Log.Warning($"Unable to read script skeleton '{candidate}': {ex.Message}");
			}
		}

		return null;
	}

	/// <summary>
	/// Attaches the clips a model can play. The model keeps the converted clips; the shared set
	/// that produced them is cached so models of the same script and bone layout reuse them.
	/// </summary>
	private void AttachAnimations(DrModel model, string modelStem)
	{
		if (!DebugSettings.AnimationsEnabled)
		{
			return;
		}

		var scriptStem = FindScriptForModel(modelStem);
		if (scriptStem == null)
		{
			// A prop: benches, herbs, chests and the like carry no script, so they have no clips.
			return;
		}

		var boneNames = new string[model.Bones.Length];
		for (var i = 0; i < boneNames.Length; ++i)
		{
			boneNames[i] = model.Bones[i].Name;
		}

		var cacheKey = scriptStem + "\n" + string.Join("\n", boneNames);
		if (!_animationSets.TryGetValue(cacheKey, out var set))
		{
			set = BuildAnimationSet(scriptStem, model, boneNames);
			_animationSets[cacheKey] = set;
		}

		// Clips are immutable, so every model of a set shares the same instances. The model
		// keeps the name-to-clip map; the set itself stays in the assets' build cache.
		model.Animations = set.ClipsByName;
	}

	/// <summary>Whether the clip is a one-shot that must not cycle, from the script.</summary>
	private static bool IsOneShot(IAnimation entry)
	{
		// A clip with an interaction window is a one-shot even when it names itself: the
		// shipped attack animations name themselves in Next but must be interruptible at the
		// combo window, so the window wins over the self-reference.
		foreach (var tag in entry.EventTags)
		{
			if (tag.Type == EventType.ComboWindow)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Decides how a clip runs from the script's own fields, and where it goes next. The
	/// shipped data encodes looping by naming the clip itself in <c>Next</c>; every other case
	/// is a transition.
	/// </summary>
	private static ModelClipInfo MakeClipInfo(IAnimation entry, float fps, int frameCount,
		Dictionary<string, IAnimation> scriptByName, ISet<string> scriptClipNames)
	{
		var oneShot = IsOneShot(entry);

		var nextClip = entry.Next;
		if (string.IsNullOrEmpty(nextClip))
		{
			nextClip = null;
		}
		else if (scriptClipNames.Contains(nextClip))
		{
			// Keep the script's own spelling so the name matches the clip it created.
			foreach (var name in scriptClipNames)
			{
				if (string.Equals(name, entry.Next, StringComparison.OrdinalIgnoreCase))
				{
					nextClip = name;
					break;
				}
			}
		}
		else if (!scriptByName.ContainsKey(nextClip))
		{
			// The script pointed at a name that has no clip; playing it would dead-end, so
			// the clip holds its last frame instead. Recorded in NextClip for the audit.
			nextClip = null;
		}

		var loop = !oneShot && nextClip != null &&
		           string.Equals(nextClip, entry.Name, StringComparison.OrdinalIgnoreCase);

		return new ModelClipInfo
		{
			Name = entry.Name,
			Next = entry.Next,
			Layer = entry.Layer,
			Flags = entry.Flags,
			BlendIn = entry.BlendIn,
			BlendOut = entry.BlendOut,
			Speed = entry.Speed,
			Reverse = entry.Direction != AnimationDirection.Forward,
			FirstFrame = entry.FirstFrame,
			LastFrame = entry.LastFrame,
			FrameCount = frameCount,
			Fps = fps,
			Loop = loop,
			PlaybackNext = loop ? null : nextClip,
			NextClip = nextClip,
		};
	}

	/// <summary>
	/// Playback parameters for an alias. The frames and rate come from the clip the alias
	/// names; direction, next, layer and blend times come from the alias itself, because the
	/// script uses those to expose the same motion under a different name.
	/// </summary>
	private static ModelClipInfo MakeAliasInfo(IAnimationAlias alias, string targetName,
		ModelClipInfo target, ISet<string> scriptClipNames, ISet<string> aliasNames)
	{
		// An alias's Next addresses the script's names, which include the other aliases — a
		// looping alias names itself just like a looping clip does. Checking only the clip
		// list would demote every self-naming alias to a one-shot.
		var nextClip = alias.Next;
		if (string.IsNullOrEmpty(nextClip))
		{
			nextClip = null;
		}
		else if (!scriptClipNames.Contains(nextClip) && !aliasNames.Contains(nextClip))
		{
			// A name no clip or alias provides would dead-end; hold the last frame instead.
			nextClip = null;
		}

		var loop = nextClip != null &&
		           string.Equals(nextClip, alias.Name, StringComparison.OrdinalIgnoreCase);

		return new ModelClipInfo
		{
			Name = alias.Name,
			Next = alias.Next,
			Layer = alias.Layer,
			Flags = alias.Flags,
			BlendIn = alias.BlendIn,
			BlendOut = alias.BlendOut,
			Speed = target?.Speed ?? 0f,
			Reverse = alias.Direction != AnimationDirection.Forward,
			FirstFrame = target?.FirstFrame ?? 0,
			LastFrame = target?.LastFrame ?? 0,
			FrameCount = target?.FrameCount ?? 0,
			Fps = target?.Fps ?? 0f,
			Loop = loop,
			PlaybackNext = loop ? null : nextClip,
			NextClip = targetName,
		};
	}

	/// <summary>
	/// Converts a script's clips into <see cref="AnimationClip"/>s addressed by the target
	/// model's bone numbering, dropping channels the model cannot pose.
	/// </summary>
	private ModelAnimationSet BuildAnimationSet(string scriptStem, DrModel model, string[] boneNames)
	{
		var scriptName = scriptStem + ".MSB";
		if (!_allRecords.ContainsKey(scriptName))
		{
			scriptName = scriptStem + ".MDS";
		}

		var script = LoadRecord(GetLastRecord(scriptName).Node, r => new ModelScript(r));
		var skeleton = LoadScriptSkeleton(script, scriptStem);

		var result = new ModelAnimationSet(scriptStem, script.SkeletonName, skeleton?.Nodes.Count ?? 0, boneNames);

		// Skeleton node index to bone index, resolved by name. Indices cannot be used directly:
		// the clip addresses the script's skeleton, whose numbering differs from the model's
		// (ARMOR_PRISONER's "BIP01 L CLAVICLE" is node 7, the human skeleton's is node 8).
		var boneIndexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		for (var i = 0; i < model.Bones.Length; ++i)
		{
			boneIndexByName.TryAdd(model.Bones[i].Name, i);
		}

		var boneIndexByNode = new Dictionary<int, int>();
		if (skeleton != null)
		{
			var nodes = skeleton.Nodes;
			for (var i = 0; i < nodes.Count; ++i)
			{
				if (boneIndexByName.TryGetValue(nodes[i].Name, out var boneIndex))
				{
					boneIndexByNode[i] = boneIndex;
				}
			}
		}

		// Next is resolved against the script's own clip list, so build it before any clip is
		// converted: a clip can name a clip the loop has not reached yet.
		var scriptByName = new Dictionary<string, IAnimation>(StringComparer.OrdinalIgnoreCase);
		var scriptClipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var entry in script.Animations)
		{
			scriptByName.TryAdd(entry.Name, entry);
			scriptClipNames.Add(entry.Name);
		}

		foreach (var entry in script.Animations)
		{
			var manName = scriptStem + "-" + entry.Name + ".MAN";
			if (!_allRecords.ContainsKey(manName))
			{
				result.CountMissing();
				Log.Warning($"Animation '{scriptStem}/{entry.Name}' has no '{manName}'.");
				continue;
			}

			ModelAnimation man;
			try
			{
				man = LoadRecord(GetLastRecord(manName).Node, r => new ModelAnimation(r));
			}
			catch (Exception ex)
			{
				result.CountMissing();
				Log.Warning($"Unable to read animation '{manName}': {ex.Message}");
				continue;
			}

			// The .MAN frame rate is the authoritative one: 161 clips disagree with their script
			// and the file is the only source that also stores the frames. The 149 clips that
			// store a rate of 1 are single-frame poses, where any rate gives the same result.
			var fps = man.Fps > 0f ? man.Fps : entry.Fps;
			if (!(fps > 0f))
			{
				fps = 25f;
			}

			var nodeCount = man.NodeIndices.Count;
			var frameCount = man.FrameCount;
			var samples = man.Samples;

			var info = MakeClipInfo(entry, fps, frameCount, scriptByName, scriptClipNames);

			var channels = new List<AnimationChannel>();
			var keyframeCount = 0L;

			// Samples are frame-major: samples[frame * nodeCount + slot]. Check the count rather
			// than trusting it, so a malformed file costs one warning and not a crash.
			if (nodeCount > 0 && frameCount > 0 && samples.Count == nodeCount * frameCount)
			{
				// A looping clip gets one extra key that repeats frame 0 at the end of its
				// duration. That is what closes the cycle: sampling the final frame interval
				// interpolates the last frame back into the first instead of holding the last
				// frame and then snapping when the time wraps.
				var keyCount = info.Loop && frameCount > 1 ? frameCount + 1 : frameCount;

				var times = new TimeSpan[keyCount];
				for (var frame = 0; frame < frameCount; ++frame)
				{
					times[frame] = TimeSpan.FromSeconds(frame / (double)fps);
				}

				if (keyCount > frameCount)
				{
					times[frameCount] = TimeSpan.FromSeconds(frameCount / (double)fps);
				}

				for (var slot = 0; slot < nodeCount; ++slot)
				{
					var nodeIndex = man.NodeIndices[slot];

					// A channel addressing a node the model has no bone for has nothing to move.
					// That is expected rather than broken: the human skeleton has nine ZS_* attach
					// nodes that the armour models do not carry.
					if (!boneIndexByNode.TryGetValue(nodeIndex, out var boneIndex))
					{
						if (skeleton == null || nodeIndex < 0 || nodeIndex >= skeleton.Nodes.Count)
						{
							result.CountOutOfRange(1);
						}
						else
						{
							result.CountUnmapped(1);
						}

						continue;
					}

					var keyframes = new AnimationChannelKeyframe[keyCount];
					for (var frame = 0; frame < keyCount; ++frame)
					{
						// The closing key repeats the first frame rather than reading past the
						// end of the sample block.
						var source = frame == frameCount ? 0 : frame;
						var sample = samples[source * nodeCount + slot];
						var pose = new SrtTransform(sample.Position.ToXna(), sample.Rotation.ToXna(), Vector3.One);

						keyframes[frame] = new AnimationChannelKeyframe(times[frame], pose);
					}

					channels.Add(new AnimationChannel(boneIndex, keyframes));
					keyframeCount += keyCount;
				}
			}
			else if (nodeCount > 0 && frameCount > 0)
			{
				Log.Warning(
					$"Animation '{manName}' stores {samples.Count} samples but {nodeCount} nodes × {frameCount} frames; skipped.");
			}

			var duration = TimeSpan.FromSeconds(frameCount / (double)fps);

			// AnimationClip rejects an empty channel array. Those clips exist (a prop that only
			// toggles its own state, for instance), so they are still enumerated and carry their
			// metadata, but there is nothing to play and no clip object is created.
			if (channels.Count == 0)
			{
				result.AddEmpty(info);
				continue;
			}

			var clip = new AnimationClip(entry.Name, duration, channels.ToArray())
			{
				// The script side of the clip: what an AnimationClip itself cannot hold.
				Tag = new AnimationClipTag(info)
			};

			result.Add(clip, keyframeCount);
		}

		// An alias is a second name for a clip: it plays the target's samples but keeps its own
		// next/layer/blend/direction, so a backward alias of a forward clip is a reverse
		// animation. It becomes a clip of its own that shares the target's channels and carries
		// its own ModelClipInfo in Tag.
		var aliasNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var aliasTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var alias in script.AnimationAliases)
		{
			aliasNames.Add(alias.Name);

			if (!string.IsNullOrEmpty(alias.Name) && !string.IsNullOrEmpty(alias.Alias))
			{
				aliasTargets[alias.Name] = alias.Alias;
			}
		}

		// Targets are names too, so resolve through the alias list: a few scripts alias an
		// alias.
		string ResolveAliasTarget(string name)
		{
			for (var hops = 0; hops < 4; ++hops)
			{
				if (result.ClipsByName.ContainsKey(name))
				{
					return name;
				}

				if (!aliasTargets.TryGetValue(name, out var target))
				{
					return null;
				}

				name = target;
			}

			return result.ClipsByName.ContainsKey(name) ? name : null;
		}

		foreach (var alias in script.AnimationAliases)
		{
			if (string.IsNullOrEmpty(alias.Name) || string.IsNullOrEmpty(alias.Alias))
			{
				continue;
			}

			if (string.Equals(alias.Name, alias.Alias, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var targetName = ResolveAliasTarget(alias.Alias);
			if (targetName == null || !result.ClipsByName.TryGetValue(targetName, out var targetClip))
			{
				continue;
			}

			var aliasInfo = MakeAliasInfo(alias, alias.Alias, targetClip.GetPlaybackInfo(),
				scriptClipNames, aliasNames);

			result.AddAlias(new AnimationClip(alias.Name, targetClip.Duration, targetClip.Channels)
			{
				Tag = new AnimationClipTag(aliasInfo)
			});
		}

		Log.Info(
			$"Animation set '{scriptStem}' ({result.SkeletonNodeCount}-node skeleton) for {boneNames.Length} bones: " +
			$"{result.ClipNames.Count} clips ({result.EmptyClips} empty; {result.LoopingClips} loop, " +
			$"{result.TransitionClips} one-shot), {result.AliasCount} aliases, {result.KeyframeCount:N0} keyframes, " +
			$"{result.UnmappedChannels} channels the model cannot pose, {result.MissingClips} missing.");

		return result;
	}

	/// <summary>
	/// Reports a script's clips and the first frame of one clip's bone tracks. This is the
	/// self-check for clip loading: it shows that a clip is enumerable and that its channels
	/// address real bones with plausible transforms.
	/// </summary>
	public void DumpAnimations(GraphicsDevice device, string scriptStem, string clipName)
	{
		var record = scriptStem + ".MSB";
		if (!_allRecords.ContainsKey(record))
		{
			record = scriptStem + ".MDS";
		}

		if (!_allRecords.ContainsKey(record))
		{
			Log.Error($"Animation dump: no script '{scriptStem}'.");
			return;
		}

		var script = LoadRecord(GetLastRecord(record).Node, r => new ModelScript(r));
		var skeleton = LoadScriptSkeleton(script, scriptStem);

		Log.Info($"Animation dump for script '{scriptStem}':");
		Log.Info($"  skeleton: '{script.SkeletonName}' ({(skeleton?.Nodes.Count ?? 0)} nodes), meshes: {script.Meshes.Count}");
		Log.Info($"  clips: {script.Animations.Count}, aliases: {script.AnimationAliases.Count}, combines: {script.AnimationCombines.Count}");

		if (skeleton == null)
		{
			Log.Warning("  no skeleton hierarchy found; channel indices cannot be named.");
		}

		foreach (var entry in script.Animations)
		{
			var manName = scriptStem + "-" + entry.Name + ".MAN";
			if (!_allRecords.ContainsKey(manName))
			{
				Log.Info($"  {entry.Name}: .MAN missing");
				continue;
			}

			var man = LoadRecord(GetLastRecord(manName).Node, r => new ModelAnimation(r));
			var fps = man.Fps > 0f ? man.Fps : entry.Fps;
			var duration = fps > 0f ? man.FrameCount / fps : 0f;

			Log.Info(
				$"  {entry.Name}: nodes={man.NodeIndices.Count} frames={man.FrameCount} fps={fps} " +
				$"duration={duration:F2}s next='{entry.Next}' frames={entry.FirstFrame}..{entry.LastFrame} " +
				$"layer={entry.Layer} flags={entry.Flags}");
		}

		if (string.IsNullOrEmpty(clipName))
		{
			return;
		}

		var clipEntry = script.Animations.Find(a => string.Equals(a.Name, clipName, StringComparison.OrdinalIgnoreCase));
		if (clipEntry == null)
		{
			Log.Error($"Animation dump: script '{scriptStem}' has no clip '{clipName}'.");
			return;
		}

		var clipManName = scriptStem + "-" + clipEntry.Name + ".MAN";
		var data = LoadRecord(GetLastRecord(clipManName).Node, r => new ModelAnimation(r));
		var nodeCount = data.NodeIndices.Count;

		Log.Info($"  first frame of '{clipEntry.Name}' ({nodeCount} channels):");

		for (var slot = 0; slot < Math.Min(nodeCount, 8); ++slot)
		{
			var nodeIndex = data.NodeIndices[slot];
			var name = skeleton != null && nodeIndex >= 0 && nodeIndex < skeleton.Nodes.Count
				? skeleton.Nodes[nodeIndex].Name
				: "<unknown>";

			var sample = data.Samples[slot];
			Log.Info(
				$"    [{slot}] node {nodeIndex} '{name}' " +
				$"T=({sample.Position.X:F2}, {sample.Position.Y:F2}, {sample.Position.Z:F2}) " +
				$"R=({sample.Rotation.X:F3}, {sample.Rotation.Y:F3}, {sample.Rotation.Z:F3}, {sample.Rotation.W:F3})");
		}
	}
}
