using DigitalRiseModel.Animation;
using System;
using System.Collections.Generic;

namespace OpenGothic.Scene;

/// <summary>
/// The clips a model can play, plus the per-clip metadata the model script carries but an
/// <see cref="AnimationClip"/> cannot hold.
/// </summary>
/// <remarks>
/// A clip's keys only depend on the mapping from the script skeleton's nodes to the target
/// model's bones, so one set is shared by every model with the same script and the same bone
/// sequence. That is what keeps the ~87 MB of shipped clips from being converted once per
/// model.
/// </remarks>
public sealed class ModelAnimationSet
{
	/// <summary>Script that lists the clips, e.g. <c>HUMANS</c>.</summary>
	public string ScriptName { get; }

	/// <summary>Skeleton the script's channel indices address, as the script names it.</summary>
	public string SkeletonName { get; }

	/// <summary>Number of nodes in that skeleton, or zero when no hierarchy file was found.</summary>
	public int SkeletonNodeCount { get; }

	/// <summary>Clip name to clip, in script order.</summary>
	public IReadOnlyDictionary<string, AnimationClip> Clips => ClipsByName;

	/// <summary>Clip name to the playback parameters from the script.</summary>
	public IReadOnlyDictionary<string, ModelClipInfo> Infos => InfosByName;

	/// <summary>
	/// Clip names in the order the script lists them. Includes clips that store no channels:
	/// they are enumerable but have no bone tracks to play.
	/// </summary>
	public IReadOnlyList<string> ClipNames { get; }

	/// <summary>Clips the script lists whose <c>.MAN</c> stores no channels at all.</summary>
	public int EmptyClips { get; private set; }

	/// <summary>Bone names by bone index, so a channel can be reported by name.</summary>
	public IReadOnlyList<string> BoneNames { get; }

	/// <summary>Channels that addressed a node the target model has no bone for and were dropped.</summary>
	public int UnmappedChannels { get; private set; }

	/// <summary>Channels whose node index was outside the script skeleton.</summary>
	public int OutOfRangeChannels { get; private set; }

	/// <summary>Total keyframes converted into clips.</summary>
	public long KeyframeCount { get; private set; }

	/// <summary>Clips the script lists whose <c>.MAN</c> file is missing.</summary>
	public int MissingClips { get; private set; }

	internal Dictionary<string, AnimationClip> ClipsByName { get; } = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

	internal Dictionary<string, ModelClipInfo> InfosByName { get; } = new Dictionary<string, ModelClipInfo>(StringComparer.OrdinalIgnoreCase);

	internal List<string> OrderedClipNames { get; } = new List<string>();

	internal ModelAnimationSet(string scriptName, string skeletonName, int skeletonNodeCount, IReadOnlyList<string> boneNames)
	{
		ScriptName = scriptName;
		SkeletonName = skeletonName;
		SkeletonNodeCount = skeletonNodeCount;
		BoneNames = boneNames;
		ClipNames = OrderedClipNames;
	}

	internal void Add(AnimationClip clip, ModelClipInfo info, long keyframes)
	{
		ClipsByName[clip.Name] = clip;
		InfosByName[clip.Name] = info;
		OrderedClipNames.Add(clip.Name);
		KeyframeCount += keyframes;
	}

	/// <summary>Records a clip that is listed but has no channels to convert.</summary>
	internal void AddEmpty(ModelClipInfo info)
	{
		InfosByName[info.Name] = info;
		OrderedClipNames.Add(info.Name);
		++EmptyClips;
	}

	internal void CountUnmapped(int count) => UnmappedChannels += count;

	internal void CountOutOfRange(int count) => OutOfRangeChannels += count;

	internal void CountMissing() => ++MissingClips;
}

/// <summary>
/// Playback parameters the model script stores per clip. The sample data in the clip's
/// <c>.MAN</c> file is the frames and bone tracks; everything here describes how to run them.
/// </summary>
public sealed class ModelClipInfo
{
	public string Name { get; init; }
	public string Next { get; init; }
	public int Layer { get; init; }
	public int Flags { get; init; }
	public float BlendIn { get; init; }
	public float BlendOut { get; init; }
	public float Speed { get; init; }

	/// <summary>Whether the clip plays backwards, from the script's direction.</summary>
	public bool Reverse { get; init; }

	/// <summary>First frame of the clip's slice of the script's shared frame timeline.</summary>
	public int FirstFrame { get; init; }

	/// <summary>Last frame of that slice. Not an index into the clip's own frames.</summary>
	public int LastFrame { get; init; }

	/// <summary>Frames actually stored, from the <c>.MAN</c> file.</summary>
	public int FrameCount { get; init; }

	/// <summary>Playback rate, from the <c>.MAN</c> file where it has one.</summary>
	public float Fps { get; init; }
}
