using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGothic.Gameplay;
using OpenGothic.UI;
using System;

namespace OpenGothic.Tools;

/// <summary>
/// Interactive world viewer. This type is only a host: it owns the window, polls input and
/// captures the mouse. Loading, simulation and drawing all live in <see cref="GameCore"/>.
/// </summary>
internal sealed class WorldViewer : Microsoft.Xna.Framework.Game
{
	private readonly GraphicsDeviceManager _graphics;
	private readonly string _folder;
	private readonly string _worldName;

	private GameCore _core;
	private bool _toggleWasDown;
	private int _lastScrollValue;

	/// <summary>True once the world has been loaded and shown.</summary>
	public bool Succeeded { get; private set; }

	public WorldViewer(string folder, string worldName)
	{
		_folder = folder;
		_worldName = worldName;

		_graphics = new GraphicsDeviceManager(this)
		{
			PreferredBackBufferWidth = 1280,
			PreferredBackBufferHeight = 720,
			PreferMultiSampling = true,
			SynchronizeWithVerticalRetrace = true,
		};

		IsFixedTimeStep = true;
		IsMouseVisible = false;
		Window.Title = "OpenGothic world viewer";
		Window.AllowUserResizing = true;
	}

	protected override void LoadContent()
	{
		base.LoadContent();

		try
		{
			OG.GraphicsDevice = GraphicsDevice;

			OG.LogInfo($"OpenGothicSharp {OG.Version}");
			OG.LogInfo($"Game folder: {_folder}");
			OG.LogInfo($"World:       {_worldName}");
			OG.LogInfo(string.Empty);

			_core = new GameCore(this, _folder, _worldName);
			_core.Load();

			PrintControls();
			CenterMouse();

			Succeeded = true;
		}
		catch (Exception ex)
		{
			OG.LogError($"Failed to load '{_worldName}':");
			OG.LogError(ex.ToString());
			Succeeded = false;
			Exit();
		}
	}

	private static void PrintControls()
	{
		OG.LogInfo(string.Empty);
		OG.LogInfo("Controls:");
		OG.LogInfo("  W/A/S/D         run");
		OG.LogInfo("  Left Shift      sprint");
		OG.LogInfo("  Mouse           orbit camera");
		OG.LogInfo("  Mouse wheel     camera distance");
		OG.LogInfo($"  {Hud.ToggleDebugKey,-15} toggle debug overlay");
		OG.LogInfo("  Escape          quit");
		OG.LogInfo(string.Empty);
	}

	protected override void Update(GameTime gameTime)
	{
		base.Update(gameTime);

		var keyboard = Keyboard.GetState();
		if (keyboard.IsKeyDown(Keys.Escape))
		{
			Exit();
			return;
		}

		if (_core == null)
		{
			return;
		}

		_core.Update(gameTime, ReadInput(keyboard));
	}

	/// <summary>
	/// Turns raw device state into engine intent. Mouse look is derived from the distance from the
	/// window centre, because the cursor is recentred every frame.
	/// </summary>
	private GameInput ReadInput(KeyboardState keyboard)
	{
		var mouse = Mouse.GetState();
		var center = new Point(
			GraphicsDevice.Viewport.Width / 2,
			GraphicsDevice.Viewport.Height / 2);

		var lookDeltaX = mouse.X - center.X;
		var lookDeltaY = mouse.Y - center.Y;

		// Wheel reports an absolute value, so track the previous frame to get a delta.
		var wheelDelta = mouse.ScrollWheelValue - _lastScrollValue;
		_lastScrollValue = mouse.ScrollWheelValue;

		// Rising edge only, otherwise holding the key would flicker the overlay.
		var toggleDown = keyboard.IsKeyDown(Hud.ToggleDebugKey);
		var toggle = toggleDown && !_toggleWasDown;
		_toggleWasDown = toggleDown;

		Mouse.SetPosition(center.X, center.Y);

		return new GameInput(
			MoveAxis(keyboard, Keys.W, Keys.S),
			MoveAxis(keyboard, Keys.D, Keys.A),
			keyboard.IsKeyDown(Keys.LeftShift),
			lookDeltaX,
			lookDeltaY,
			wheelDelta,
			toggle);
	}

	private static float MoveAxis(KeyboardState keyboard, Keys positive, Keys negative)
	{
		var value = 0.0f;
		if (keyboard.IsKeyDown(positive)) value += 1.0f;
		if (keyboard.IsKeyDown(negative)) value -= 1.0f;
		return value;
	}

	protected override void Draw(GameTime gameTime)
	{
		GraphicsDevice.Clear(GameCore.SkyColor);

		if (_core != null)
		{
			_core.Draw(gameTime);

			if (_core.ShouldUpdateTitle(gameTime))
			{
				Window.Title = _core.GetTitleText();
			}
		}

		base.Draw(gameTime);
	}

	protected override void UnloadContent()
	{
		base.UnloadContent();

		_core?.Dispose();
		_core = null;
	}

	private void CenterMouse()
	{
		var viewport = GraphicsDevice.Viewport;
		var center = new Point(viewport.Width / 2, viewport.Height / 2);
		Mouse.SetPosition(center.X, center.Y);
		_lastScrollValue = Mouse.GetState().ScrollWheelValue;
	}
}
