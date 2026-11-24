using DigitalRiseModel;
using System.Collections.Generic;

namespace OpenGothic.Scene;

/// <summary>
/// A placed model: its instance, the models attached to its bones, and optional clip playback.
/// A node that never gets <see cref="AnimationData"/> simply keeps the bind pose.
/// </summary>
public class ModelNode : SceneNode
{
	public DrModelInstance Instance { get; set; }

	/// <summary>Whether the vob asks for collision; the reference uses its <c>cd_dynamic</c> flag.</summary>
	public bool Collision { get; set; } = true;

	/// <summary>
	/// Models drawn on this model's bones — armour, weapons, accessories. Each is placed by the
	/// bone's current transform, so it follows the pose without being a scene node of its own.
	/// </summary>
	public List<ModelBoneAttachment> BonesAttachments { get; } = new List<ModelBoneAttachment>();

	/// <summary>Clip playback for an animated model, or null for one that only renders.</summary>
	public AnimationData AnimationData { get; set; }
}
