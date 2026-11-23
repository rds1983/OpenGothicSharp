using DigitalRiseModel;
using DigitalRiseModel.Animation;
using OpenGothic.Utility;
using System;

namespace OpenGothic.Scene;

/// <summary>
/// A placed model whose skeleton is driven by its script's clips. The node owns the
/// <see cref="AnimationController"/> that samples and blends the pose, and the only state the
/// controller cannot hold: which clip the script asked for, and whether a one-shot has already
/// handed over.
/// </summary>
/// <remarks>
/// <para>
/// The playback parameters of a clip live on <see cref="AnimationClip.Tag"/> as an
/// <see cref="AnimationClipTag"/>, so a clip and an alias of it are both ordinary entries in
/// <c>DrModel.Animations</c>; the alias shares the target's channels and carries its own
/// information.
/// </para>
/// <para>
/// Reverse playback is <c>PlayBackwards</c> plus one frame period of
/// <see cref="AnimationBlendLayer.TimeOffset"/>. The flag samples <c>duration - time</c>,
/// which starts a reverse clip on frame 0; the game's reverse mirrors the sample walk and so
/// starts on the last stored frame. Shifting the layer rather than the controller's clock
/// keeps <see cref="AnimationController.Time"/> equal to game time, which is what
/// <c>HasFinished</c> and the cross-fade progress are measured against.
/// </para>
/// </remarks>
public sealed class AnimatedModelNode : ModelNode
{
	/// <summary>Longest cross-fade a script can ask for; the shipped data tops out at 0.3 s.</summary>
	private const float MaxBlendSeconds = 2f;

	private AnimationClip _clip;

	/// <summary>
	/// Set once a one-shot's end has been acted on. `AnimationController.HasFinished` stays
	/// true afterwards, so it cannot say whether the hand-over already happened.
	/// </summary>
	private bool _endHandled;

	public AnimatedModelNode(DrModelInstance instance)
	{
		Instance = instance ?? throw new ArgumentNullException(nameof(instance));
		Controller = new AnimationController(instance);
	}

	/// <summary>Drives the skeleton; one per node, because playback state is per instance.</summary>
	public AnimationController Controller { get; }

	/// <summary>Name of the clip being played, or null when idle.</summary>
	public string CurrentClip => _clip?.Name;

	/// <summary>Playback parameters of the current clip, or null when idle.</summary>
	public ModelClipInfo CurrentInfo => _clip.GetPlaybackInfo();

	/// <summary>Whether the current clip cycles.</summary>
	public bool IsLooping => CurrentInfo?.Loop ?? false;

	/// <summary>Time the current clip runs for, or zero when idle.</summary>
	public TimeSpan Duration => CurrentInfo?.Duration ?? TimeSpan.Zero;

	/// <summary>
	/// Starts a clip or an alias. Starting the clip that is already playing restarts it.
	/// Returns false when the set has no such name or the clip stores no channels.
	/// </summary>
	public bool Play(string name, bool blend = true)
	{
		var clips = Instance.Model?.Animations;
		if (string.IsNullOrEmpty(name) ||
		    clips == null ||
		    !clips.TryGetValue(name, out var clip) ||
		    clip.Channels.Length == 0)
		{
			return false;
		}

		var info = clip.GetPlaybackInfo();
		var node = BuildNode(clip, info);

		// The reference ramps over the longer of the outgoing clip's blend-out and the
		// incoming clip's blend-in, and switches immediately when both are zero. Most declared
		// transitions are in that zero case (917 of 1,253), so inventing a default ramp would
		// soften most of the game's clip changes.
		var fade = 0f;
		if (blend && _clip != null && !ReferenceEquals(_clip, clip))
		{
			var outgoing = CurrentInfo;
			fade = Math.Max(outgoing?.BlendOut ?? 0f, info?.BlendIn ?? 0f);
			fade = fade > 0.0001f ? Math.Clamp(fade, 0f, MaxBlendSeconds) : 0f;
		}

		// A zero-length fade has to snap: the controller divides by the fade length to derive
		// progress, so handing it a zero would evaluate a NaN weight.
		if (fade > 0f && Controller.IsPlaying)
		{
			Controller.CrossfadeToClip(node, TimeSpan.FromSeconds(fade));
		}
		else
		{
			Controller.StartClip(node);
		}

		_clip = clip;
		_endHandled = false;

		return true;
	}

	/// <summary>Stops playback and restores the model's default pose.</summary>
	public void Stop()
	{
		_clip = null;
		_endHandled = false;

		Controller.StopClip();
	}

	/// <summary>
	/// Pins the current clip to an absolute time, whatever its duration. This is what a
	/// captured pose uses: the result depends on <paramref name="time"/> alone, not on how
	/// long the process has been running. A looping clip wraps the time; a one-shot clamps.
	/// </summary>
	public void Seek(TimeSpan time)
	{
		var info = CurrentInfo;
		if (_clip == null || info == null)
		{
			return;
		}

		var seconds = Math.Max(0.0, time.TotalSeconds);
		var duration = info.Duration.TotalSeconds;

		if (info.Loop && duration > 0)
		{
			seconds %= duration;
		}
		else if (duration > 0 && seconds > duration)
		{
			seconds = duration;
		}

		// The controller ignores a time it already holds, and the pose it already holds is the
		// one that was asked for, so a repeated Seek needs no work.
		var target = TimeSpan.FromSeconds(seconds);
		if (!Controller.Time.Equals(target))
		{
			Controller.Time = target;
		}
	}

	/// <summary>
	/// Advances the clip. A looping clip wraps; a one-shot clip holds its last frame and then
	/// either chains to the clip its script names or stops.
	/// </summary>
	public void Update(TimeSpan elapsed)
	{
		if (_clip == null || _endHandled)
		{
			return;
		}

		Controller.Update(elapsed);

		var info = CurrentInfo;
		var duration = (float)(info?.Duration.TotalSeconds ?? 0);
		if (info == null || info.Loop || duration <= 0f)
		{
			return;
		}

		// The controller clamps a one-shot to its duration, so reaching it is the end.
		if ((float)Controller.Time.TotalSeconds < duration)
		{
			return;
		}

		_endHandled = true;

		var next = info.PlaybackNext;
		if (next != null && !string.Equals(next, _clip.Name, StringComparison.OrdinalIgnoreCase))
		{
			// The script chains this clip into another one; start it from the top with the
			// usual cross-fade so the hand-over reads as motion. Logged because a clip change
			// is a rare event, unlike the per-frame sampling around it.
			Log.Info($"Animation '{_clip.Name}' finished; continuing with '{next}'.");
			Play(next);
		}
	}

	/// <summary>
	/// Builds the node the controller plays: a bare clip when it runs forward, and a one-layer
	/// blend whose layer carries the reverse offset when it does not.
	/// </summary>
	private static AnimationTreeNode BuildNode(AnimationClip clip, ModelClipInfo info)
	{
		var loop = info?.Loop ?? false;
		var flags = loop ? AnimationFlags.Looped : AnimationFlags.None;

		if (info?.Reverse != true)
		{
			return new AnimationClipNode(clip, flags);
		}

		// A reverse clip starts on its last stored frame. The library mirrors time about the
		// clip's duration, which lands on frame 0 for a looping clip because its closing key
		// repeats frame 0 at exactly the duration; one frame period of offset corrects it.
		var period = info.FrameCount > 0 ? info.Duration.TotalSeconds / info.FrameCount : 0.0;

		// The blend node must not carry PlayBackwards itself, or the parent would reverse a
		// time the layer has already reversed.
		var node = new AnimationBlendNode(flags);
		var layer = node.AddLayer(clip, 1.0f, flags | AnimationFlags.PlayBackwards);
		layer.TimeOffset = TimeSpan.FromSeconds(period);
		return node;
	}
}
