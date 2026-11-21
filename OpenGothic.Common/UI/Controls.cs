/* Hand-written behaviour for the MyraPad layout in controls.xmmp. */
using Microsoft.Xna.Framework.Input;

namespace OpenGothic.UI;

/// <summary>
/// Bottom-right block listing the keys the viewer reacts to. The layout is authored in
/// <c>controls.xmmp</c>; this file is hand-written and survives <c>myra-export</c>.
/// </summary>
public partial class Controls
{
	/// <summary>Key that toggles the debug overlay.</summary>
	public const Keys ToggleDebugKey = Keys.F1;

	public Controls()
	{
		BuildUI();

		// Show the real binding instead of a hard-coded copy, so the two cannot drift apart.
		_labelToggleDebugKey.Text = ToggleDebugKey.ToString();
	}
}
