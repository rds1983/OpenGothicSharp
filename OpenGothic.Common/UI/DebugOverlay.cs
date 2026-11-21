/* Hand-written behaviour for the MyraPad layout in debugOverlay.xmmp. */
namespace OpenGothic.UI;

/// <summary>
/// Debug HUD in the top-left corner. It only displays values; the host decides which frame
/// statistics to report and when the overlay is visible.
/// </summary>
/// <remarks>
/// The layout itself is authored in <c>debugOverlay.xmmp</c> and generated into
/// <c>DebugOverlay.Generated.cs</c>. This file is not overwritten by <c>myra-export</c>,
/// so hand-written code belongs here.
/// </remarks>
public partial class DebugOverlay
{
	public DebugOverlay()
	{
		BuildUI();
	}

	/// <summary>
	/// Updates the reported frame statistics.
	/// </summary>
	/// <param name="fps">Frames per second, averaged over the last half second.</param>
	/// <param name="drawCalls">Draw calls issued for the current frame.</param>
	/// <param name="polygons">Triangles drawn for the current frame.</param>
	public void SetStats(int fps, int drawCalls, int polygons)
	{
		_labelFps.Text = $"FPS: {fps}";
		_labelDrawCalls.Text = $"Draw calls: {drawCalls}";
		_labelPolygons.Text = $"Polygons: {polygons}";
	}
}
