using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGothic.Engine;

public class Material
{
	public Color DiffuseColor { get; set; }
	public Texture2D DiffuseTexture { get; set; }

	public bool CastsShadow { get; set; }
}
