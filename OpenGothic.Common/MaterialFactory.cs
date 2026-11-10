using DigitalRiseModel;

namespace OpenGothic;

using Material =  Engine.Material;

public static class MaterialFactory
{
	public static Material Create(DrMaterial material)
	{
		return new Material
		{
			DiffuseColor = material.DiffuseColor,
			DiffuseTexture = material.DiffuseTexture
		};
	}
}
