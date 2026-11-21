using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;
using System;

namespace OpenGothic.UI;

/// <summary>
/// Owns the in-game HUD: the Myra desktop, its widgets and the per-frame statistics they show.
/// Hosts create one <see cref="Hud"/>, call <see cref="Update"/> and <see cref="Render"/> each
/// frame; they never touch Myra's desktop or widgets directly.
/// </summary>
/// <remarks>
/// Widget layouts are authored in the <c>.xmmp</c> files next to this class and exported to C#
/// with <c>myra-export</c>. Everything in this file is hand-written and is not regenerated.
/// </remarks>
public sealed class Hud : IDisposable
{
	/// <summary>How often the FPS figure is refreshed, in seconds.</summary>
	private const double FpsUpdateInterval = 0.5;

	private readonly Desktop _desktop;
	private readonly DebugOverlay _debugOverlay;

	private double _fpsTimer;
	private int _fpsFrames;
	private int _fps;

	public Hud(Game game)
	{
		if (game == null)
		{
			throw new ArgumentNullException(nameof(game));
		}

		// Must be set before the first widget is constructed: widgets resolve their stylesheet
		// through the game's graphics device.
		MyraEnvironment.Game = game;

		// The HUD is display-only for now, so do not let widgets swap the system cursor. Hosts
		// that hide and recentre the mouse (the world viewer does) would otherwise fight Myra.
		MyraEnvironment.SetMouseCursorFromWidget = false;

		_debugOverlay = new DebugOverlay();
		var controls = new Controls();

		_desktop = new Desktop();
		_desktop.Widgets.Add(_debugOverlay);
		_desktop.Widgets.Add(controls);
	}

	/// <summary>Whether the debug overlay is currently shown.</summary>
	public bool DebugOverlayVisible
	{
		get => _debugOverlay.Visible;
		set => _debugOverlay.Visible = value;
	}

	/// <summary>Key that toggles the debug overlay, as listed in the controls block.</summary>
	public static Keys ToggleDebugKey => Controls.ToggleDebugKey;

	/// <summary>Shows the debug overlay when hidden and hides it when shown.</summary>
	public void ToggleDebugOverlay() => DebugOverlayVisible = !DebugOverlayVisible;

	/// <summary>
	/// Refreshes the displayed statistics and lays the widgets out for the current viewport.
	/// Call it after the 3D frame has been drawn, once the draw statistics are final.
	/// </summary>
	/// <param name="gameTime">Timing for the frame that was just drawn.</param>
	/// <param name="drawCalls">Draw calls issued for that frame.</param>
	/// <param name="polygons">Triangles drawn for that frame.</param>
	public void Update(GameTime gameTime, int drawCalls, int polygons)
	{
		++_fpsFrames;
		_fpsTimer += gameTime.ElapsedGameTime.TotalSeconds;
		if (_fpsTimer >= FpsUpdateInterval)
		{
			_fps = (int)Math.Round(_fpsFrames / _fpsTimer);
			_fpsFrames = 0;
			_fpsTimer = 0.0;
		}

		_debugOverlay.SetStats(_fps, drawCalls, polygons);
	}

	/// <summary>Draws the HUD over the current back buffer.</summary>
	public void Render() => _desktop.Render();

	public void Dispose() => _desktop.Dispose();
}
