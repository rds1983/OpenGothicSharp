using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;
using OpenGothic.UI;
using System;

namespace OpenGothic;

public class OgGame : Game
{
	private readonly GraphicsDeviceManager _graphics;
	private Renderer _renderer;
	private WorldRenderer _worldRenderer;
	private Assets _assets;
	private WorldGrid _world;
	private Desktop _desktop;
	private Hud _hud;
	private KeyboardState _previousKeyboard;

	/// <summary>Toggleable debug views drawn on top of the world.</summary>
	public DebugDraw DebugDraw { get; } = new DebugDraw();

	public Camera Camera { get; } = new Camera();

	public OgGame()
	{
		_graphics = new GraphicsDeviceManager(this);
		IsMouseVisible = true;
		Window.Title = $"OpenGothicSharp {OG.Version}";
	}

	protected override void Initialize()
	{
		base.Initialize();

		OG.GraphicsDevice = GraphicsDevice;
		_renderer = new Renderer(GraphicsDevice);
		_worldRenderer = new WorldRenderer(GraphicsDevice);

		MyraEnvironment.Game = this;
		_desktop = new Desktop();
		_hud = new Hud();
		_desktop.Root = _hud;

		Camera.FieldOfView = OG.Settings?.CameraFov ?? Camera.FieldOfView;
		Camera.SetViewport(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

		LoadWorld();
	}

	private void LoadWorld()
	{
		var gothicPath = CommandLine.Instance?.GothicPath;
		if (string.IsNullOrEmpty(gothicPath))
		{
			return;
		}

		if (CommandLine.Instance.NoMenu)
		{
			// Placeholder until the menu exists (milestone I): keep the camera at the
			// world origin, looking straight ahead.
			Camera.Position = Vector3.Zero;
		}

		var worldName = CommandLine.Instance.World ?? "newworld.zen";

		try
		{
			_assets = new Assets(gothicPath);
			_world = _assets.GetWorld(GraphicsDevice, worldName);
			Log.Info($"World '{worldName}' loaded.");
		}
		catch (Exception ex)
		{
			Log.Error($"Unable to load world '{worldName}': {ex.Message}");
		}
	}

	protected override void Update(GameTime gameTime)
	{
		var keyboard = Keyboard.GetState();

		if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
			keyboard.IsKeyDown(Keys.Escape))
		{
			Exit();
		}

		// Edge-detect so holding the key toggles once rather than every frame.
		if (keyboard.IsKeyDown(Keys.F1) && _previousKeyboard.IsKeyUp(Keys.F1))
		{
			_hud.ToggleDebugOverlay();
		}

		if (keyboard.IsKeyDown(Keys.F2) && _previousKeyboard.IsKeyUp(Keys.F2))
		{
			DebugDraw.Wireframe = !DebugDraw.Wireframe;
		}

		if (keyboard.IsKeyDown(Keys.F3) && _previousKeyboard.IsKeyUp(Keys.F3))
		{
			DebugDraw.Bounds = !DebugDraw.Bounds;
		}

		_previousKeyboard = keyboard;

		base.Update(gameTime);
	}

	protected override void Draw(GameTime gameTime)
	{
		_worldRenderer.Draw(_renderer, Camera, _world, DebugDraw);

		if (_hud.DebugOverlay.Visible)
		{
			// Read the counters after the world draw: BeginFrame resets them per frame.
			_hud.DebugOverlay.Update(gameTime, Camera, _renderer, DebugDraw);
		}

		_desktop.Render();

		base.Draw(gameTime);
	}

	protected override void UnloadContent()
	{
		_renderer?.Dispose();
		_renderer = null;

		_worldRenderer?.Dispose();
		_worldRenderer = null;

		base.UnloadContent();
	}
}
