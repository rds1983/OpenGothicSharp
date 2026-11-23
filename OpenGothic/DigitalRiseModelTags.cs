using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using OpenGothic.Scene;
using System.Collections.Generic;

namespace OpenGothic
{
	public record DrMeshPartTag(Material material)
	{
		/// <summary>
		/// Union of the poses a played clip has put this part in, in model space, or null
		/// until a clip has been measured. It belongs to the model rather than to a node: the
		/// tag is shared by every instance, so the value only ever widens and no instance can
		/// read a box that is too small for the clip it plays.
		/// </summary>
		public BoundingBox? CullBounds { get; set; }
	}

	/// <summary>
	/// Model-level state that outlives a single instance. A model's clips depend only on its
	/// script and bone layout, so the same <c>DrModel</c> is shared by every model with that
	/// layout, and work done for one instance can be reused by the others.
	/// </summary>
	public record DrModelTag
	{
		/// <summary>
		/// Ids of the clips whose poses are already part of the parts'
		/// <see cref="DrMeshPartTag.CullBounds"/>. The bounds are stored per part, but the
		/// question "has this clip been measured?" is per model, so the answer lives here.
		/// Ids rather than clips: the set must not keep a clip alive once it is no longer
		/// referenced anywhere else.
		/// </summary>
		public HashSet<int> MeasuredClips { get; } = new();
	}

	/// <summary>
	/// The script-side playback parameters of a clip, attached to <c>AnimationClip.Tag</c>.
	/// An alias carries its own tag while sharing the target's channels.
	/// </summary>
	public record AnimationClipTag(ModelClipInfo info)
	{
		private static int _nextId;

		/// <summary>
		/// Unique id of this clip. Sets that need to remember clips without keeping them alive
		/// store this instead of a reference.
		/// </summary>
		public int Id { get; } = System.Threading.Interlocked.Increment(ref _nextId);
	}

	public static class DigitalRiseModelTagExtensions
	{
		/// <summary>Playback parameters the model script stores for a clip, or null.</summary>
		public static ModelClipInfo GetPlaybackInfo(this AnimationClip clip) =>
			(clip?.Tag as AnimationClipTag)?.info;

		/// <summary>
		/// The box to cull a mesh part with: the animated union once a clip has been measured,
		/// otherwise the box the part was built in.
		/// </summary>
		public static BoundingBox GetCullBounds(this DrMeshPart part) =>
			(part.Tag as DrMeshPartTag)?.CullBounds ?? part.BoundingBox;
	}
}
