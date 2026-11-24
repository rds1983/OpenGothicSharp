using DigitalRiseModel;
using Microsoft.Xna.Framework;
using System;

namespace OpenGothic.Graphics;

/// <summary>
/// A model instance plus the draw material for every mesh part. The materials start as the
/// model's own <see cref="DrMeshPartTag"/> materials and individual entries can be replaced
/// without copying the shared geometry (NPC texture variants do this).
/// </summary>
public class GothicModel
{
	private DrModelInstance _instance;

	public GothicModel()
	{
	}

	public GothicModel(DrModel model)
	{
		Model = model;
	}

	/// <summary>The instance that supplies bones, skin transforms and the model itself.</summary>
	public DrModelInstance Instance
	{
		get => _instance;
		set
		{
			if (ReferenceEquals(_instance, value))
			{
				return;
			}

			if (_instance != null)
			{
				_instance.ModelChanged -= OnModelChanged;
			}

			_instance = value;

			if (_instance != null)
			{
				_instance.ModelChanged += OnModelChanged;
			}

			SetMaterialsFromModel();
		}
	}

	/// <summary>The scene model, or null before one is assigned.</summary>
	public DrModel Model
	{
		get => _instance?.Model;
		set
		{
			if (_instance == null)
			{
				Instance = new DrModelInstance();
			}

			_instance.Model = value;
		}
	}

	/// <summary>Draw material per mesh and per part within the mesh.</summary>
	public Material[][] Materials { get; private set; }

	public BoundingBox? BoundingBox => _instance?.BoundingBox;

	public Material GetMaterial(int meshIndex, int meshPartIndex) => Materials[meshIndex][meshPartIndex];

	/// <summary>Re-reads the default materials from the current model.</summary>
	public void SetMaterialsFromModel()
	{
		Materials = CreateMaterials(_instance?.Model);
	}

	private void OnModelChanged(object sender, EventArgs e)
	{
		SetMaterialsFromModel();
	}

	/// <summary>
	/// Builds the default matrix from a model's mesh parts. A part without a
	/// <see cref="DrMeshPartTag"/> material stays null; the renderer then falls back to the
	/// part itself.
	/// </summary>
	internal static Material[][] CreateMaterials(DrModel model)
	{
		if (model == null || model.Meshes == null)
		{
			return null;
		}

		var result = new Material[model.Meshes.Length][];
		for (var i = 0; i < model.Meshes.Length; ++i)
		{
			var mesh = model.Meshes[i];
			var parts = mesh.MeshParts;
			var row = new Material[parts.Count];
			for (var j = 0; j < parts.Count; ++j)
			{
				row[j] = (parts[j].Tag as DrMeshPartTag)?.material;
			}

			result[i] = row;
		}

		return result;
	}
}
