using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGothic.Scene;

public class Material
{
	public Color DiffuseColor { get; set; } = Color.White;
	public Texture2D DiffuseTexture { get; set; }

	public bool CastsShadow { get; set; }

	public bool LightingEnabled { get; set; } = true;

	public bool FogEnabled { get; set; } = true;

	public DepthStencilState DepthStencilState { get; set; } = DepthStencilState.Default;

	public RasterizerState RasterizerState { get; set; } = RasterizerState.CullCounterClockwise;
}
