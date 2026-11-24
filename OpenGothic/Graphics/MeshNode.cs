using DigitalRiseModel;
using Microsoft.Xna.Framework;

namespace OpenGothic.Graphics;

public class MeshNode: SceneNode
{
	public DrMeshPart Mesh { get; set; }

	/// <summary>Whether this mesh contributes collision; level parts always do, a vob part follows <c>cd_dynamic</c>.</summary>
	public bool Collision { get; set; } = true;

	public override bool RayTest(Ray ray, out float distance)
	{
		distance = float.MaxValue;
		if (Mesh == null)
		{
			return false;
		}

		var transform = WorldTransform;
		return RayTestBox(ray, Mesh.BoundingBox, ref transform, ref distance);
	}
}
