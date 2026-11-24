using DigitalRiseModel;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace OpenGothic.Scene;

public class InstancedMeshNode: SceneNode
{
	public DrMeshPart Mesh { get; set; }
	public List<Matrix> InstancesTransforms { get; } = new List<Matrix>();
	public List<bool> InstancesCollision { get; } = new List<bool>();

	public void AddInstance(Matrix transform, bool collision)
	{
		InstancesTransforms.Add(transform);
		InstancesCollision.Add(collision);
	}
}
