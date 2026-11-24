using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGothic.Graphics;

public class Material
{
	public Color DiffuseColor { get; set; } = Color.White;
	public Texture2D DiffuseTexture { get; set; }

	/// <summary>
	/// Texture name the part was authored with, or null for a synthetic material. NPC body,
	/// armour and head variants are selected by rewriting the <c>_V0</c>/<c>_C0</c> tags in
	/// this name, so the original has to survive material creation.
	/// </summary>
	public string SourceTexture { get; set; }

	public bool CastsShadow { get; set; }

	public bool LightingEnabled { get; set; } = true;

	public bool FogEnabled { get; set; } = true;

	public DepthStencilState DepthStencilState { get; set; } = DepthStencilState.Default;

	public RasterizerState RasterizerState { get; set; } = RasterizerState.CullCounterClockwise;
}
