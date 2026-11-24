using DigitalRiseModel;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace OpenGothic.Graphics;

/// <summary>
/// A placed model: its instance, the models attached to its bones, and optional clip playback.
/// A node that never gets <see cref="AnimationData"/> simply keeps the bind pose.
/// </summary>
public class ModelNode : SceneNode
{
	/// <summary>The model instance and the per-mesh-part draw materials.</summary>
	public GothicModel Model { get; } = new GothicModel();

	/// <summary>Forwarding shortcut for <see cref="GothicModel.Instance"/>.</summary>
	public DrModelInstance Instance
	{
		get => Model.Instance;
		set => Model.Instance = value;
	}

	/// <summary>Whether the vob asks for collision; the reference uses its <c>cd_dynamic</c> flag.</summary>
	public bool Collision { get; set; } = true;

	/// <summary>Forwarding shortcut for <see cref="GothicModel.Materials"/>.</summary>
	public Material[][] Materials => Model.Materials;

	/// <summary>Re-reads the default materials from the current model instance.</summary>
	public void SetMaterialsFromModel() => Model.SetMaterialsFromModel();

	/// <summary>
	/// Models drawn on this model's bones — armour, weapons, accessories. Each is placed by the
	/// bone's current transform, so it follows the pose without being a scene node of its own.
	/// </summary>
	public List<ModelBoneAttachment> BonesAttachments { get; } = new List<ModelBoneAttachment>();

	/// <summary>Clip playback for an animated model, or null for one that only renders.</summary>
	public AnimationData AnimationData { get; set; }

	public override bool RayTest(Ray ray, out float distance)
	{
		distance = float.MaxValue;
		var instance = Instance;
		var model = instance?.Model;
		if (model == null)
		{
			return false;
		}

		var world = WorldTransform;
		foreach (var mesh in model.Meshes)
		{
			var bone = mesh.ParentBone;
			var boneWorld = bone != null
				? instance.GetBoneGlobalTransform(bone.Index) * world
				: world;

			foreach (var part in mesh.MeshParts)
			{
				// A skinned part is already in model space; a static part is positioned by its bone.
				var transform = part.Skin != null ? world : boneWorld;
				RayTestBox(ray, part.BoundingBox, ref transform, ref distance);
			}
		}

		return distance < float.MaxValue;
	}
}
