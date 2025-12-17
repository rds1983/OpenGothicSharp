using DigitalRiseModel;
using Nursia.Materials;
using OpenGothic.Materials;

namespace OpenGothic
{
	public static class MaterialFactory
	{
		public static IMaterial Create(DrMaterial material)
		{
			return new DefaultMaterial
			{
				DiffuseColor = material.DiffuseColor,
				DiffuseTexture = material.DiffuseTexture
			};
		}
	}
}
