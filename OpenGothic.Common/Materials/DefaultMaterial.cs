using AssetManagementBase;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Nursia;
using Nursia.Materials;
using Nursia.Rendering;
using System.Collections.Generic;
using System.ComponentModel;

namespace OpenGothic.Materials
{
	public class DefaultMaterial : BaseMaterial
	{
		private static readonly EffectBinding[] _shadowBindings = new EffectBinding[4];
		private static readonly EffectBinding[] _colorBindings = new EffectBinding[128];

		private MaterialFlags _flags = MaterialFlags.AcceptsLight | MaterialFlags.CastsShadows | MaterialFlags.AcceptsShadows;

		public string Id { get; set; }

		[Browsable(false)]
		[JsonIgnore]
		public override MaterialFlags Flags
		{
			get => _flags;
		}

		[Category("Behavior")]
		[DefaultValue(true)]
		public bool CastsShadows
		{
			get => _flags.HasFlag(MaterialFlags.CastsShadows);

			set
			{
				if (value)
				{
					_flags |= MaterialFlags.CastsShadows;
				}
				else
				{
					_flags &= ~MaterialFlags.CastsShadows;
				}
			}
		}

		[Category("Behavior")]
		[DefaultValue(true)]
		public bool AcceptsShadows
		{
			get => _flags.HasFlag(MaterialFlags.AcceptsShadows);

			set
			{
				if (value)
				{
					_flags |= MaterialFlags.AcceptsShadows;
				}
				else
				{
					_flags &= ~MaterialFlags.AcceptsShadows;
				}
			}
		}

		[Category("Appearance")]
		public Color AmbientColor { get; set; } = Color.Black;

		[Category("Appearance")]
		public Color DiffuseColor { get; set; } = Color.White;

		[Category("Appearance")]
		public Color EmissiveColor { get; set; } = Color.Black;

		[Category("Appearance")]
		[JsonIgnore]
		public Texture2D DiffuseTexture { get; set; }

		[Browsable(false)]
		public string DiffuseTexturePath { get; set; }

		public void Load(AssetManager assetManager)
		{
			if (!string.IsNullOrEmpty(DiffuseTexturePath))
			{
				DiffuseTexture = assetManager.LoadTexture2D(Nrs.GraphicsDevice, DiffuseTexturePath);
			}
		}

		public override IMaterial Clone()
		{
			return new DefaultMaterial
			{
				Id = Id,
				BlendState = BlendState,
				DepthStencilState = DepthStencilState,
				RasterizerState = RasterizerState,
				CastsShadows = CastsShadows,
				AcceptsShadows = AcceptsShadows,
				AmbientColor = AmbientColor,
				DiffuseColor = DiffuseColor,
				EmissiveColor = EmissiveColor,
				DiffuseTexture = DiffuseTexture,
				DiffuseTexturePath = DiffuseTexturePath
			};
		}

		private static EffectBinding InternalGetBinding(LightTechnique lightTechnique, bool shadow, bool skinning, bool clipPlane, bool instanced)
		{
			var key = 0;

			if (lightTechnique == LightTechnique.Point)
			{
				key |= 1;
			}

			if (lightTechnique == LightTechnique.Spot)
			{
				key |= 2;
			}

			if (shadow)
			{
				if (Nrs.GraphicsSettings.ShadowType == ShadowType.Simple)
				{
					key |= 4;
				}

				if (Nrs.GraphicsSettings.ShadowType == ShadowType.PCF)
				{
					key |= 8;
				}
			}

			if (skinning)
			{
				key |= 16;
			}

			if (clipPlane)
			{
				key |= 32;
			}

			if (instanced)
			{
				key |= 64;
			}

			var binding = _colorBindings[key];
			if (binding != null)
			{
				return binding;
			}

			var defines = new Dictionary<string, string>();
			switch (lightTechnique)
			{
				case LightTechnique.Point:
					defines["POINTLIGHT"] = "1";
					break;
				case LightTechnique.Spot:
					defines["SPOTLIGHT"] = "1";
					break;
				default:
					defines["DIRLIGHT"] = "1";
					break;
			}

			if (shadow)
			{
				defines["SHADOW"] = "1";

				if (Nrs.GraphicsSettings.ShadowType == ShadowType.PCF)
				{
					defines["PCFSHADOW"] = "1";
				}
				else if (Nrs.GraphicsSettings.ShadowType == ShadowType.Simple)
				{
					defines["SIMPLESHADOW"] = "1";
				}
			}

			if (skinning)
			{
				defines["SKINNED"] = "1";
			}

			if (clipPlane)
			{
				defines["CLIPPLANE"] = "1";
			}

			if (instanced)
			{
				defines["INSTANCED"] = "1";
			}

			var effect = Resources.GetEffect("Default", defines);
			binding = new EffectBinding(effect);

			binding.AddMaterialLevelSetter<DefaultMaterial>("cMatAmbientColor", (m, p) => p.SetValue(m.AmbientColor.ToVector3()));
			binding.AddMaterialLevelSetter<DefaultMaterial>("cMatDiffColor", (m, p) => p.SetValue(m.DiffuseColor.ToVector4()));
			binding.AddMaterialLevelSetter<DefaultMaterial>("cMatEmissiveColor", (m, p) => p.SetValue(m.EmissiveColor.ToVector3()));

			binding.AddMaterialLevelSetter<DefaultMaterial>("DiffMap", (m, p) => p.SetValue(m.DiffuseTexture ?? Resources.White));

			_colorBindings[key] = binding;

			return binding;
		}

		public override EffectBinding GetShadowTechnique(DrMeshPart mesh, bool instancing)
		{
			var key = 0;

			if (mesh != null && mesh.Skin != null)
			{
				key |= 1;
			}

			if (instancing)
			{
				key |= 2;
			}

			if (_shadowBindings[key] != null)
			{
				return _shadowBindings[key];
			}

			var defines = new Dictionary<string, string>();
			if (mesh != null && mesh.Skin != null)
			{
				defines["SKINNED"] = "1";
			}

			if (instancing)
			{
				defines["INSTANCED"] = "1";
			}

			var effect = Resources.GetEffect("Shadow", defines);
			var binding = new EffectBinding(effect);

			binding.AddMaterialLevelSetter<DefaultMaterial>("DiffMap", (m, p) => p.SetValue(m.DiffuseTexture ?? Resources.White));

			_shadowBindings[key] = binding;

			return binding;
		}

		public override EffectBinding GetColorTechnique(DrMeshPart mesh, LightTechnique technique, bool shadow, bool translucent, bool clipPlane, bool instancing)
		{
			return InternalGetBinding(technique, shadow, mesh != null && mesh.Skin != null, clipPlane, instancing);
		}
	}
}
