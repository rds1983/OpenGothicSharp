using DigitalRiseModel;

namespace OpenGothic.Scene;

public class MeshNode: SceneNode
{
	public DrMeshPart Mesh { get; set; }

	/// <summary>Whether this mesh contributes collision; level parts always do, a vob part follows <c>cd_dynamic</c>.</summary>
	public bool Collision { get; set; } = true;
}
