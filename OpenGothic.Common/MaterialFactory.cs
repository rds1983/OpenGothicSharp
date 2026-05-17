using DigitalRiseModel;
using Nursia.Materials;

namespace OpenGothic
{
	public static class MaterialFactory
	{
		public static IMaterial Create(DrMaterial material)
		{
			return new BlinnPhongMaterial
			{
				DiffuseColor = material.DiffuseColor,
				DiffuseTexture = material.DiffuseTexture
			};
		}
	}
}
