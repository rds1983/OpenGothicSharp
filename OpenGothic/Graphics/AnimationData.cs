using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using OpenGothic.Utility;
using System;
using System.Collections.Generic;

namespace OpenGothic.Graphics;

/// <summary>
/// Clip playback for one model instance: a stack of layers sampled by one
/// <see cref="AnimationController"/>, plus the state the script adds on top of the library's
/// blending — which clips are running, when each started, and the <c>Next</c> chain.
/// </summary>
/// <remarks>
/// <para>
/// Every active clip becomes a layer of one <see cref="AnimationBlendNode"/>. The layer order is
/// the script's <c>Layer</c> value, and each layer's <see cref="AnimationBlendLayer.BoneFilter"/>
/// keeps only the bones no higher layer drives, so a higher layer overrides exactly the bones it
/// animates and a lower one stays authoritative everywhere else. Each layer also carries a
/// <see cref="AnimationBlendLayer.TimeOffset"/> that turns the controller's clock into that
/// clip's own time, which is what lets a walk keep looping while a separate gesture starts
/// mid-stride.
/// </para>
/// <para>
/// Reverse playback is <c>PlayBackwards</c> plus one frame period of layer offset. The flag
/// samples <c>duration - time</c>, which starts a reverse clip on frame 0; the game's reverse
/// mirrors the sample walk and so starts on the last stored frame.
/// </para>
/// </remarks>
public sealed class AnimationData
{
	/// <summary>Longest cross-fade a script can ask for; the shipped data tops out at 0.3 s.</summary>
	private const float MaxBlendSeconds = 2f;

	private readonly DrModelInstance _instance;
	private readonly List<Layer> _layers = new List<Layer>();

	/// <summary>The layer <see cref="Play"/> drives; the stack's lowest and the one its end chains.</summary>
	private Layer _base;

	private AnimationBlendNode _root;
	private double _elapsed;

	/// <summary>
	/// Set once a one-shot's end has been acted on. <c>AnimationController.HasFinished</c> stays
	/// true afterwards, so it cannot say whether the hand-over already happened.
	/// </summary>
	private bool _endHandled;

	/// <summary>The layer stack changed since the last alignment; the pose must be resampled.</summary>
	private bool _layerSetChanged;

	private int _nextSequence;

	public AnimationData(DrModelInstance instance)
	{
		_instance = instance ?? throw new ArgumentNullException(nameof(instance));
		Controller = new AnimationController(instance);
	}

	/// <summary>Samples and blends the layer tree.</summary>
	public AnimationController Controller { get; }

	/// <summary>Name of the base clip, or null when idle.</summary>
	public string CurrentClip => _base?.Clip.Name;

	/// <summary>Whether a clip is active on any layer, base included.</summary>
	public bool IsPlaying(string name)
	{
		foreach (var layer in _layers)
		{
			if (string.Equals(layer.Clip.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Raw root translation of the base clip at its current time, before blending. The reference
	/// measures mob root motion from the sequence itself (<c>Sequence::speed</c>), not from the
	/// blended pose, so an entry cross-fade cannot drag the actor along.
	/// </summary>
	public bool TryGetBaseRootTranslation(int boneIndex, out Vector3 translation)
	{
		translation = Vector3.Zero;
		if (_base == null || boneIndex < 0)
		{
			return false;
		}

		var duration = _base.Info.Duration.TotalSeconds;
		if (duration <= 0)
		{
			return false;
		}

		var local = _elapsed - _base.Start;
		if (_base.Info.Loop)
		{
			local %= duration;
			if (local < 0)
			{
				local += duration;
			}
		}
		else
		{
			local = Math.Clamp(local, 0, duration);
		}

		var time = TimeSpan.FromSeconds(local);
		foreach (var channel in _base.Clip.Channels)
		{
			if (channel.BoneIndex != boneIndex)
			{
				continue;
			}

			translation = SampleTranslation(channel, time);
			return true;
		}

		return false;
	}

	private static Vector3 SampleTranslation(AnimationChannel channel, TimeSpan time)
	{
		var keys = channel.Keyframes;
		if (keys.Length == 0)
		{
			return Vector3.Zero;
		}

		if (time <= keys[0].Time)
		{
			return keys[0].Pose.Translation;
		}

		var last = keys[keys.Length - 1];
		if (time >= last.Time)
		{
			return last.Pose.Translation;
		}

		for (var i = 1; i < keys.Length; ++i)
		{
			var b = keys[i];
			if (time > b.Time)
			{
				continue;
			}

			var a = keys[i - 1];
			var span = (b.Time - a.Time).TotalSeconds;
			var t = span > 0
				? (float)((time - a.Time).TotalSeconds / span)
				: 0f;
			return Vector3.Lerp(a.Pose.Translation, b.Pose.Translation, t);
		}

		return last.Pose.Translation;
	}

	/// <summary>
	/// Whether the base clip is being played backwards. <c>Sequence::speed</c> negates the
	/// forward delta for a reversed sequence instead of mirroring the sampled time.
	/// </summary>
	public bool IsBaseClipReversed => _base != null && (_base.Reverse || _base.Info.Reverse);

	/// <summary>Playback parameters of the base clip, or null when idle.</summary>
	public ModelClipInfo CurrentInfo => _base?.Info;

	/// <summary>Whether the base clip cycles.</summary>
	public bool IsLooping => CurrentInfo?.Loop ?? false;

	/// <summary>Time the base clip runs for, or zero when idle.</summary>
	public TimeSpan Duration => CurrentInfo?.Duration ?? TimeSpan.Zero;

	/// <summary>
	/// Starts a clip as the base, leaving overlays running. Starting the clip that is already
	/// playing restarts it. Returns false when the model has no animations, no such name, or the
	/// clip stores no channels.
	/// </summary>
	public bool Play(string name, bool blend = true, bool reverse = false)
	{
		if (!TryGetClip(name, out var clip, out var info))
		{
			return false;
		}

		// The reference ramps over the longer of the outgoing clip's blend-out and the
		// incoming clip's blend-in, and switches immediately when both are zero. Most declared
		// transitions are in that zero case (917 of 1,253), so inventing a default ramp would
		// soften most of the game's clip changes.
		var outgoing = CurrentInfo;
		var fade = 0f;
		if (blend && _base != null && !ReferenceEquals(_base.Clip, clip))
		{
			fade = Math.Max(outgoing.BlendOut, info?.BlendIn ?? 0f);
			fade = fade > 0.0001f ? Math.Clamp(fade, 0f, MaxBlendSeconds) : 0f;
		}

		if (_base == null)
		{
			_base = new Layer
			{
				Sequence = ++_nextSequence
			};

			_layers.Add(_base);
		}

		_base.Clip = clip;
		_base.Info = info;
		_base.Start = _elapsed;
		_base.Reverse = reverse;
		_base.Node = BuildNode(clip, info, reverse);

		_layers.Sort((a, b) =>
			a.Info.Layer != b.Info.Layer
				? a.Info.Layer.CompareTo(b.Info.Layer)
				: a.Sequence.CompareTo(b.Sequence));

		_endHandled = false;

		Rebuild(fade);

		// Merge this clip's poses into the parts' cull bounds before the first frame drawn with
		// it. Each clip is measured once per model; other models sharing it reuse the result.
		MeasureClip(clip);

		return true;
	}

	/// <summary>
	/// Plays a clip at the layer its script declares, like the reference's <c>Pose::startAnim</c>:
	/// a clip on the base clip's layer replaces the base, a clip on another layer runs as an
	/// overlay and leaves every bone it does not animate to the layers below. Script animation
	/// calls (<c>AI_PlayAni</c>) use this so an upper-body gesture does not stand a seated NPC up.
	/// </summary>
	public bool PlayAtScriptLayer(string name)
	{
		if (!TryGetClip(name, out var clip, out var info))
		{
			return false;
		}

		if (_base == null || info.Layer == _base.Info.Layer)
		{
			return Play(name);
		}

		return PlayLayer(name);
	}

	/// <summary>
	/// Runs a clip on top of the base at the layer the script gives it, replacing whatever
	/// occupies that layer. An upper-body clip leaves every bone it does not animate to the
	/// layers below.
	/// </summary>
	public bool PlayLayer(string name)
	{
		if (!TryGetClip(name, out var clip, out var info))
		{
			return false;
		}

		var layer = _layers.Find(l => l != _base && l.Info.Layer == info.Layer);
		if (layer == null)
		{
			layer = new Layer
			{
				Sequence = ++_nextSequence
			};

			_layers.Add(layer);
		}

		layer.Clip = clip;
		layer.Info = info;
		layer.Start = _elapsed;
		layer.Node = BuildNode(clip, info);

		_layers.Sort((a, b) =>
			a.Info.Layer != b.Info.Layer
				? a.Info.Layer.CompareTo(b.Info.Layer)
				: a.Sequence.CompareTo(b.Sequence));

		_endHandled = false;

		Rebuild(0f);
		MeasureClip(clip);

		return true;
	}

	/// <summary>Drops the layer at <paramref name="layer"/>.</summary>
	public void StopLayer(int layer)
	{
		if (_layers.RemoveAll(l => l != _base && l.Info.Layer == layer) == 0)
		{
			return;
		}

		_endHandled = false;
		Rebuild(0f);
	}

	/// <summary>Stops playback and restores the model's default pose.</summary>
	public void Stop()
	{
		_layers.Clear();
		_base = null;
		_root = null;
		_elapsed = 0;
		_endHandled = false;
		_layerSetChanged = false;

		Controller.StopClip();
	}

	/// <summary>
	/// Pins the stack to an absolute time. This is what a captured pose uses: the result depends
	/// on <paramref name="time"/> alone, not on how long the process has been running. A looping
	/// clip wraps the time; a one-shot clamps.
	/// </summary>
	public void Seek(TimeSpan time)
	{
		if (_layers.Count == 0)
		{
			return;
		}

		var seconds = Math.Max(0.0, time.TotalSeconds);
		if (!_layerSetChanged && Math.Abs(_elapsed - seconds) < 1e-9)
		{
			// The frozen path calls this every frame; the pose is already the one asked for.
			return;
		}

		_elapsed = seconds;
		Align();
	}

	/// <summary>
	/// Advances every layer. A looping clip wraps; a one-shot holds its last frame and then
	/// either chains to the clip its script names or stops.
	/// </summary>
	public void Update(TimeSpan elapsed)
	{
		if (_layers.Count == 0)
		{
			return;
		}

		var seconds = elapsed.TotalSeconds;
		if (seconds <= 0 && !_layerSetChanged)
		{
			return;
		}

		_elapsed += seconds;
		Align();
		RemoveFinishedLayers();

		if (_base == null || _base.Info.Loop || _endHandled)
		{
			return;
		}

		var local = _elapsed - _base.Start;
		var duration = _base.Info.Duration.TotalSeconds;
		if (duration <= 0 || local < duration)
		{
			return;
		}

		_endHandled = true;

		var next = _base.Info.PlaybackNext;
		if (next != null && !string.Equals(next, _base.Clip.Name, StringComparison.OrdinalIgnoreCase))
		{
			// The script chains this clip into another one; start it from the top with the
			// usual cross-fade so the hand-over reads as motion. Logged because a clip change
			// is a rare event, unlike the per-frame sampling around it.
			Log.Info($"Animation '{_base.Clip.Name}' finished; continuing with '{next}'.");
			Play(next);
		}
	}

	/// <summary>
	/// Drops overlay clips that ran once, like the reference's <c>Pose::processLayers</c> removing
	/// a finished sequence with no next. The base clip's own hand-over is handled by
	/// <see cref="Update"/>.
	/// </summary>
	private void RemoveFinishedLayers()
	{
		var removed = false;
		for (var i = _layers.Count - 1; i >= 0; --i)
		{
			var layer = _layers[i];
			if (ReferenceEquals(layer, _base) || layer.Info.Loop || layer.Info.PlaybackNext != null)
			{
				continue;
			}

			var duration = layer.Info.Duration.TotalSeconds;
			if (duration <= 0 || _elapsed - layer.Start < duration)
			{
				continue;
			}

			_layers.RemoveAt(i);
			removed = true;
		}

		if (removed)
		{
			Rebuild(0f);
		}
	}

	/// <summary>
	/// Resolves a name to its clip and playback parameters. A clip without a tag has no layer,
	/// loop rule or blend time, so it is not playable.
	/// </summary>
	private bool TryGetClip(string name, out AnimationClip clip, out ModelClipInfo info)
	{
		clip = null;
		info = null;

		var clips = _instance.Model?.Animations;
		if (string.IsNullOrEmpty(name) ||
		    clips == null ||
		    !clips.TryGetValue(name, out clip) ||
		    clip.Channels.Length == 0)
		{
			return false;
		}

		info = clip.GetPlaybackInfo();
		return info != null;
	}

	/// <summary>Builds one blend node over the current layers and hands it to the controller.</summary>
	private void Rebuild(float fade)
	{
		if (_layers.Count == 0)
		{
			_root = null;
			Controller.StopClip();
			return;
		}

		var root = new AnimationBlendNode(AnimationFlags.Looped);
		var filters = ComputeFilters();

		for (var i = 0; i < _layers.Count; ++i)
		{
			var blend = root.AddLayer(_layers[i].Node, 1.0f);
			blend.BoneFilter = filters[i];
			_layers[i].Blend = blend;
		}

		_root = root;
		_layerSetChanged = true;

		// A zero-length fade has to snap: the controller divides by the fade length to derive
		// progress, so handing it a zero would evaluate a NaN weight.
		if (fade > 0f && Controller.IsPlaying)
		{
			Controller.CrossfadeToClip(root, TimeSpan.FromSeconds(fade));
		}
		else
		{
			Controller.StartClip(root);
		}

		Align();
	}

	/// <summary>
	/// For each layer, the bones it drives minus the bones any higher layer drives, so every bone
	/// is claimed by exactly one layer. Ties on the same script layer are won by the later entry.
	/// </summary>
	private HashSet<int>[] ComputeFilters()
	{
		var filters = new HashSet<int>[_layers.Count];
		var claimed = new HashSet<int>();

		for (var i = _layers.Count - 1; i >= 0; --i)
		{
			var bones = new HashSet<int>();
			foreach (var channel in _layers[i].Clip.Channels)
			{
				bones.Add(channel.BoneIndex);
			}

			bones.ExceptWith(claimed);
			filters[i] = bones;
			claimed.UnionWith(bones);
		}

		return filters;
	}

	/// <summary>
	/// Turns the controller's clock into each layer's own time. The controller wraps its time at
	/// the tree's longest layer, so a layer's offset grows by one cycle whenever the clock does;
	/// subtracting the wrapped clock keeps every layer's local time equal to
	/// <c>elapsed - start</c>.
	/// </summary>
	private void Align()
	{
		if (_root == null)
		{
			return;
		}

		var cycle = _root.Duration.TotalSeconds;
		var wrapped = cycle > 0 ? _elapsed % cycle : 0;

		for (var i = 0; i < _layers.Count; ++i)
		{
			_layers[i].Blend.TimeOffset = TimeSpan.FromSeconds((_elapsed - _layers[i].Start) - wrapped);
		}

		SetControllerTime(wrapped);
		_layerSetChanged = false;
	}

	/// <summary>Sets the controller's clock, forcing an evaluation when the value is unchanged.</summary>
	private void SetControllerTime(double seconds)
	{
		var target = TimeSpan.FromSeconds(seconds);
		if (Controller.Time.Equals(target))
		{
			// The setter skips an unchanged time, but the layer offsets may have moved. Nudge it
			// off the value first so the new offsets are sampled.
			Controller.Time = target + TimeSpan.FromTicks(1);
		}

		Controller.Time = target;
	}

	/// <summary>
	/// Widens every skinned part's box so it covers the clip's whole pose range, in model space.
	/// The clip is sampled on a throw-away instance, which leaves the node's own skeleton
	/// untouched and uses exactly the sampling playback would.
	/// </summary>
	private void MeasureClip(AnimationClip clip)
	{
		if (clip?.Tag is not AnimationClipTag clipTag)
		{
			return;
		}

		var info = clipTag.info;
		var model = _instance.Model;
		if (info == null || model == null || info.Fps <= 0f || info.FrameCount <= 0)
		{
			return;
		}

		// The measured boxes live on the model's parts, so "already measured" is a model-level
		// fact: every instance of the same model can reuse the union.
		if (model.Tag is not DrModelTag modelTag)
		{
			modelTag = new DrModelTag();
			model.Tag = modelTag;
		}

		if (!modelTag.MeasuredClips.Add(clipTag.Id))
		{
			return;
		}

		var probe = new DrModelInstance(model);
		var controller = new AnimationController(probe);
		controller.StartClip(new AnimationClipNode(clip, AnimationFlags.Looped));

		for (var frame = 0; frame < info.FrameCount; ++frame)
		{
			controller.Time = TimeSpan.FromSeconds(frame / (double)info.Fps);

			foreach (var mesh in model.Meshes)
			{
				foreach (var part in mesh.MeshParts)
				{
					if (part.Skin == null || part.Tag is not DrMeshPartTag tag)
					{
						continue;
					}

					// A blended vertex lies inside the hull of its joints' rigidly transformed
					// copies, so the union over the skin's joints is conservative. Interpolation
					// between two frames can bulge a hair outside both, which the 25 fps spacing
					// keeps negligible.
					var joints = probe.GetSkinTransforms(part.Skin.SkinIndex);
					var box = tag.CullBounds ?? part.BoundingBox;

					foreach (var joint in joints)
					{
						var matrix = joint;
						box = BoundingBox.CreateMerged(box, part.BoundingBox.Transform(ref matrix));
					}

					tag.CullBounds = box;
				}
			}
		}
	}

	/// <summary>
	/// Builds the node a layer plays: a bare clip when it runs forward, and a one-layer blend
	/// whose layer carries the reverse offset when it does not.
	/// </summary>
	private static AnimationTreeNode BuildNode(AnimationClip clip, ModelClipInfo info, bool reverse = false)
	{
		var loop = info?.Loop ?? false;
		var flags = loop ? AnimationFlags.Looped : AnimationFlags.None;

		if (info == null)
		{
			return new AnimationClipNode(clip, flags);
		}

		if (!reverse && !info.Reverse)
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

	/// <summary>One active clip and the blend layer that plays it.</summary>
	private sealed class Layer
	{
		public AnimationClip Clip;
		public ModelClipInfo Info;
		public double Start;
		public AnimationTreeNode Node;
		public AnimationBlendLayer Blend;
		public int Sequence;
		public bool Reverse;
	}
}
