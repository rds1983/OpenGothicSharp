using Microsoft.Xna.Framework;

namespace OpenGothic.Graphics;

/// <summary>
/// Which debug views are currently enabled. Independent of the text overlay: these change
/// how the world is drawn, the overlay only reports numbers.
/// </summary>
public class DebugDraw
{
	public bool Wireframe { get; set; }

	public bool Bounds { get; set; }

	/// <summary>Colour of a world-grid cell's bounding box.</summary>
	public Color CellBoundsColor { get; } = new Color(90, 160, 255);

	/// <summary>Colour of a single object's bounding box.</summary>
	public Color ObjectBoundsColor { get; } = new Color(255, 200, 80);

	/// <summary>Colour of wireframe edges.</summary>
	public Color WireframeColor { get; } = new Color(120, 255, 160);

	/// <summary>Colour of the box drawn around the vob under the crosshair.</summary>
	public Color FocusColor { get; } = new Color(80, 255, 210);
}
