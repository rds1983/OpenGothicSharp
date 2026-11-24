using Microsoft.Xna.Framework;
using OpenGothic.Vobs;

namespace OpenGothic.Graphics;

/// <summary>
/// A renderable visual. Its transform is world-space: the vob tree owns the parent/child
/// hierarchy, and a visual is attached to exactly one vob (or to the level mesh). This mirrors
/// Try/OpenGothic, where <c>ObjVisual</c>/<c>MdlVisual</c> are members of a <c>Vob</c> and the
/// renderer works from flat visual collections.
/// </summary>
public class SceneNode
{
	public Matrix Transform { get; set; } = Matrix.Identity;

	public Matrix WorldTransform => Transform;

	/// <summary>The vob this visual belongs to, or null for level geometry and batched groups.</summary>
	public VobNode Vob { get; internal set; }
}
