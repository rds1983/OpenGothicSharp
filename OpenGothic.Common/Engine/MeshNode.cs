using DigitalRiseModel;

namespace OpenGothic.Engine;

public class MeshNode: SceneNode
{
	public DrMeshPart Mesh { get; set; }
	public Material Material { get; set; }
}
