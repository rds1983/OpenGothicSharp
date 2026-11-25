using DigitalRiseModel.Animation;
using System;
using System.Collections.Generic;

namespace OpenGothic.Graphics;

/// <summary>
/// The clips a model can play, keyed by every name the script exposes — its own clips and its
/// aliases. The playback parameters that an <see cref="AnimationClip"/> cannot hold travel on
/// <see cref="AnimationClip.Tag"/> as an <see cref="AnimationClipTag"/>, which is why an alias
/// is a clip of its own: it shares the target's channels but carries its own information.
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

	/// <summary>
	/// Clip names in the order the script lists them. Includes clips that store no channels:
	/// they are enumerable but have no bone tracks to play. Aliases are not repeated here.
	/// </summary>
	public IReadOnlyList<string> ClipNames { get; }

	/// <summary>Clips the script lists whose <c>.MAN</c> stores no channels at all.</summary>
	public int EmptyClips { get; private set; }

	/// <summary>Names the script exposes as aliases of another clip.</summary>
	public int AliasCount { get; private set; }

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

	/// <summary>Clips that cycle, as derived from the script.</summary>
	public int LoopingClips { get; private set; }

	/// <summary>Clips that run once and hand over to another clip.</summary>
	public int TransitionClips { get; private set; }

	internal Dictionary<string, AnimationClip> ClipsByName { get; } = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

	internal List<string> OrderedClipNames { get; } = new List<string>();

	internal ModelAnimationSet(string scriptName, string skeletonName, int skeletonNodeCount, IReadOnlyList<string> boneNames)
	{
		ScriptName = scriptName;
		SkeletonName = skeletonName;
		SkeletonNodeCount = skeletonNodeCount;
		BoneNames = boneNames;
		ClipNames = OrderedClipNames;
	}

	internal void Add(AnimationClip clip, long keyframes)
	{
		ClipsByName[clip.Name] = clip;
		OrderedClipNames.Add(clip.Name);
		KeyframeCount += keyframes;
		CountMode(clip);
	}

	/// <summary>
	/// Records a name that reuses another clip's samples. Aliases share their keyframes with
	/// the target, so they are not counted again in <see cref="KeyframeCount"/>.
	/// </summary>
	internal void AddAlias(AnimationClip clip)
	{
		ClipsByName[clip.Name] = clip;
		++AliasCount;
	}

	/// <summary>Records a clip that is listed but has no channels to convert.</summary>
	internal void AddEmpty(ModelClipInfo info)
	{
		OrderedClipNames.Add(info.Name);
		++EmptyClips;

		if (info.Loop)
		{
			++LoopingClips;
		}
		else
		{
			++TransitionClips;
		}
	}

	internal void CountUnmapped(int count) => UnmappedChannels += count;

	internal void CountOutOfRange(int count) => OutOfRangeChannels += count;

	internal void CountMissing() => ++MissingClips;

	private void CountMode(AnimationClip clip)
	{
		if (clip.GetPlaybackInfo()?.Loop == true)
		{
			++LoopingClips;
		}
		else
		{
			++TransitionClips;
		}
	}
}

/// <summary>
/// Playback parameters the model script stores per clip. The sample data in the clip's
/// <c>.MAN</c> file is the frames and bone tracks; everything here describes how to run them.
/// </summary>
/// <remarks>
/// <see cref="Loop"/> and <see cref="PlaybackNext"/> are derived, not stored: the script's
/// <c>Next</c> names the clip itself for a looping animation and another clip for a
/// transition. A clip carrying an interaction window is always a one-shot even when it names
/// itself, which is how the shipped attack animations stay interruptible by the combo window.
/// </remarks>
public sealed class ModelClipInfo
{
	public string Name { get; init; }
	public string Next { get; init; }
	public int Layer { get; init; }
	public global::ZenKit.AnimationFlags Flags { get; init; }
	public float BlendIn { get; init; }
	public float BlendOut { get; init; }
	public float Speed { get; init; }

	/// <summary>Whether the clip plays backwards, from the script's direction.</summary>
	public bool Reverse { get; init; }

	/// <summary>
	/// First frame of the clip's slice of the script's shared frame timeline. It indexes that
	/// timeline, not this clip's samples, so it is only meaningful for timed events.
	/// </summary>
	public int FirstFrame { get; init; }

	/// <summary>Last frame of that slice. Not an index into the clip's own frames.</summary>
	public int LastFrame { get; init; }

	/// <summary>Frames actually stored, from the <c>.MAN</c> file.</summary>
	public int FrameCount { get; init; }

	/// <summary>Playback rate, from the <c>.MAN</c> file where it has one.</summary>
	public float Fps { get; init; }

	/// <summary>
	/// Whether the clip cycles. A clip that names itself in <c>Next</c> loops; one that names
	/// another clip runs once and hands over to it; one that names nothing just stops.
	/// </summary>
	public bool Loop { get; init; }

	/// <summary>
	/// Clip to start when this one finishes, or null to hold the last frame. Empty for a
	/// looping clip and for a clip whose script side was cleared because it has a window.
	/// </summary>
	public string PlaybackNext { get; init; }

	/// <summary>
	/// The script's raw <c>Next</c> the index resolved, or null when it named nothing or named
	/// a clip that does not exist.
	/// </summary>
	public string NextClip { get; init; }

	/// <summary>Time the clip runs for: <see cref="FrameCount"/> / <see cref="Fps"/>.</summary>
	public TimeSpan Duration =>
		Fps > 0f ? TimeSpan.FromSeconds(FrameCount / (double)Fps) : TimeSpan.Zero;

	/// <summary>
	/// Start time of the clip's slice of the script's shared frame timeline, in playback time.
	/// Used to convert an event frame to a moment within the clip.
	/// </summary>
	public TimeSpan FirstFrameTime =>
		Fps > 0f ? TimeSpan.FromSeconds(FirstFrame / (double)Fps) : TimeSpan.Zero;
}
