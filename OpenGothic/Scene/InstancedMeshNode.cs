using DigitalRiseModel;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace OpenGothic.Scene;

public class InstancedMeshNode: SceneNode
{
	public DrMeshPart Mesh { get; set; }
	public List<Matrix> InstancesTransforms { get; } = new List<Matrix>();
}
