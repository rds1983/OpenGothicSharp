using DigitalRiseModel.Animation;
using OpenGothic.Scene;

namespace OpenGothic
{
	public record DrMeshPartTag(Material material);

	/// <summary>
	/// The script-side playback parameters of a clip, attached to <c>AnimationClip.Tag</c>.
	/// An alias carries its own tag while sharing the target's channels.
	/// </summary>
	public record AnimationClipTag(ModelClipInfo info);

	public static class AnimationClipExtensions
	{
		/// <summary>Playback parameters the model script stores for a clip, or null.</summary>
		public static ModelClipInfo GetPlaybackInfo(this AnimationClip clip) =>
			(clip?.Tag as AnimationClipTag)?.info;
	}
}
