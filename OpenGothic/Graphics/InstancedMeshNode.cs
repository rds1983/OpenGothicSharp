using DigitalRiseModel;
using Microsoft.Xna.Framework;
using OpenGothic.Vobs;
using System.Collections.Generic;

namespace OpenGothic.Graphics;

public class InstancedMeshNode: SceneNode
{
	public DrMeshPart Mesh { get; set; }
	public List<Matrix> InstancesTransforms { get; } = new List<Matrix>();
	public List<bool> InstancesCollision { get; } = new List<bool>();
	public List<VobNode> InstancesVobs { get; } = new List<VobNode>();

	public void AddInstance(Matrix transform, bool collision, VobNode vob)
	{
		InstancesTransforms.Add(transform);
		InstancesCollision.Add(collision);
		InstancesVobs.Add(vob);
	}

	/// <summary>Removes one vob's instance from this batch; the remaining instances stay batched.</summary>
	public bool RemoveInstance(VobNode vob)
	{
		for (var i = 0; i < InstancesVobs.Count; ++i)
		{
			if (!ReferenceEquals(InstancesVobs[i], vob))
			{
				continue;
			}

			InstancesTransforms.RemoveAt(i);
			InstancesCollision.RemoveAt(i);
			InstancesVobs.RemoveAt(i);
			return true;
		}

		return false;
	}

	public override bool RayTest(Ray ray, out float distance)
	{
		distance = float.MaxValue;
		if (Mesh == null)
		{
			return false;
		}

		for (var i = 0; i < InstancesTransforms.Count; ++i)
		{
			var transform = InstancesTransforms[i];
			RayTestBox(ray, Mesh.BoundingBox, ref transform, ref distance);
		}

		return distance < float.MaxValue;
	}
}
