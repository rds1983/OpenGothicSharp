using DigitalRiseModel;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Graphics;
using System;
using System.Globalization;

namespace OpenGothic;

partial class Assets
{
	/// <summary>
	/// Replaces the entries of <paramref name="materials"/> that select an NPC body/armour
	/// texture variant. The row/column layout is the same as the model's mesh/part layout;
	/// the default material and the shared geometry are left untouched.
	/// </summary>
	public void ApplyMaterialVariants(GraphicsDevice device, DrModel model, int textureVariant,
		int teethVariant, int color, Material[][] materials)
	{
		if (model == null || materials == null)
		{
			return;
		}

		var meshes = model.Meshes;
		for (var i = 0; i < meshes.Length && i < materials.Length; ++i)
		{
			var parts = meshes[i].MeshParts;
			var row = materials[i];
			if (row == null)
			{
				continue;
			}

			for (var j = 0; j < parts.Count && j < row.Length; ++j)
			{
				var variant = CreateVariantMaterial(device, parts[j], textureVariant, teethVariant, color);
				if (variant != null)
				{
					row[j] = variant;
				}
			}
		}
	}

	/// <summary>Head, teeth and mouth are a standalone morph mesh rather than a composed model.</summary>
	public void ApplyMaterialVariants(GraphicsDevice device, DrMesh mesh, int textureVariant,
		int teethVariant, int color, Material[][] materials)
	{
		if (mesh == null || materials == null || materials.Length == 0 || materials[0] == null)
		{
			return;
		}

		var parts = mesh.MeshParts;
		var row = materials[0];
		for (var j = 0; j < parts.Count && j < row.Length; ++j)
		{
			var variant = CreateVariantMaterial(device, parts[j], textureVariant, teethVariant, color);
			if (variant != null)
			{
				row[j] = variant;
			}
		}
	}

	private Material CreateVariantMaterial(GraphicsDevice device, DrMeshPart part, int textureVariant,
		int teethVariant, int color)
	{
		if (part.Tag is not DrMeshPartTag tag || tag.material == null)
		{
			return null;
		}

		var source = tag.material;
		var name = source.SourceTexture;
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}

		// Teeth and mouth carry the teeth variant in the reference; every other texture uses
		// the body/head variant. The colour tag is applied to all of them.
		var variant = name.Contains("HUM_TEETH_", StringComparison.OrdinalIgnoreCase) ||
		              name.Contains("HUM_MOUTH_", StringComparison.OrdinalIgnoreCase)
			? teethVariant
			: textureVariant;

		var variantName = ReplaceTag(name, 'V', variant);
		variantName = ReplaceTag(variantName, 'C', color);

		if (string.Equals(variantName, name, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		var texture = GetTexture(device, variantName);
		if (texture == null)
		{
			return null;
		}

		return new Material
		{
			DiffuseColor = source.DiffuseColor,
			DiffuseTexture = texture,
			SourceTexture = variantName,
			CastsShadow = source.CastsShadow,
			LightingEnabled = source.LightingEnabled,
			FogEnabled = source.FogEnabled,
			DepthStencilState = source.DepthStencilState,
			RasterizerState = source.RasterizerState
		};
	}

	/// <summary>
	/// Replaces the single <c>_V0</c>/<c>_C0</c> tag with a concrete number, mirroring
	/// <c>emplaceTag</c> in the reference: only the zero after the tag is rewritten, and
	/// only the first occurrence.
	/// </summary>
	internal static string ReplaceTag(string name, char tag, int value)
	{
		if (string.IsNullOrEmpty(name))
		{
			return name;
		}

		for (var i = 1; i + 1 < name.Length; ++i)
		{
			if (name[i] != tag || name[i - 1] != '_' || name[i + 1] != '0')
			{
				continue;
			}

			return name.Substring(0, i + 1) +
			       value.ToString(CultureInfo.InvariantCulture) +
			       name.Substring(i + 2);
		}

		return name;
	}
}
