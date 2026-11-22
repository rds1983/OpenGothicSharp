using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace OpenGothic;

public class OgGame : Game
{
	private readonly GraphicsDeviceManager _graphics;
	private Renderer _renderer;
	private Assets _assets;
	private WorldGrid _world;

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
		if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
			Keyboard.GetState().IsKeyDown(Keys.Escape))
		{
			Exit();
		}

		base.Update(gameTime);
	}

	protected override void Draw(GameTime gameTime)
	{
		GraphicsDevice.Clear(Color.CornflowerBlue);

		_renderer.Draw(Camera, _world);

		base.Draw(gameTime);
	}

	protected override void UnloadContent()
	{
		_renderer?.Dispose();
		_renderer = null;

		base.UnloadContent();
	}
}
