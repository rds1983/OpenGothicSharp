using DigitalRiseModel;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace OpenGothic.Engine;

public class InstancedMeshNode: SceneNode
{
	public DrMeshPart Mesh { get; set; }
	public Material Material { get; set; }
	public List<Matrix> InstancesTransforms { get; } = new List<Matrix>();
}
