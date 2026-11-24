using Microsoft.Xna.Framework;
using OpenGothic.Utility;
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

	/// <summary>
	/// Nearest hit of a world-space ray with this visual, or false when it has no geometry.
	/// A visual tests its mesh parts' bounds, which is accurate enough for focus and avoids
	/// keeping a second CPU copy of every triangle.
	/// </summary>
	public virtual bool RayTest(Ray ray, out float distance)
	{
		distance = float.MaxValue;
		return false;
	}

	/// <summary>Tests one part's local box after <paramref name="transform"/> and keeps the nearest hit.</summary>
	protected static bool RayTestBox(Ray ray, BoundingBox localBox, ref Matrix transform, ref float distance)
	{
		var worldBox = localBox.Transform(ref transform);
		var hit = worldBox.Intersects(ray);
		if (!hit.HasValue || hit.Value >= distance)
		{
			return false;
		}

		distance = hit.Value;
		return true;
	}
}
