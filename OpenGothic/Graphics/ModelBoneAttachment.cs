using DigitalRiseModel;
using Microsoft.Xna.Framework;
using System;

namespace OpenGothic.Graphics;

/// <summary>
/// A model attached to a bone of another model. It is placed by the bone's current transform,
/// so it follows the skeleton as the clip plays; accessories, armour and weapons use it.
/// </summary>
public class ModelBoneAttachment
{
	/// <summary>The attached model instance and its per-mesh-part draw materials.</summary>
	public GothicModel Model { get; } = new GothicModel();

	/// <summary>The attached model's own instance, independent of the parent's skeleton.</summary>
	public DrModelInstance Instance => Model.Instance;

	/// <summary>Bone of the parent model to follow, or null when unbound.</summary>
	public DrModelBone Bone { get; set; }

	/// <summary>Local transform applied relative to the bone; identity by default.</summary>
	public Matrix Transform { get; set; } = Matrix.Identity;

	/// <summary>Forwarding shortcut for <see cref="GothicModel.Materials"/>.</summary>
	public Material[][] Materials => Model.Materials;

	public ModelBoneAttachment(DrModel model)
	{
		Model.Model = model ?? throw new ArgumentNullException(nameof(model));
	}

	/// <summary>
	/// Wraps a standalone mesh in a one-bone model, so a multi-resolution mesh — a weapon, for
	/// instance — can be attached like any model. The mesh is shared, not copied, so the wrapper
	/// must not be disposed.
	/// </summary>
	public static DrModel WrapMesh(DrMesh mesh)
	{
		if (mesh == null)
		{
			throw new ArgumentNullException(nameof(mesh));
		}

		return new DrModel(new DrModelBone(mesh.Name ?? "mesh", mesh));
	}
}
