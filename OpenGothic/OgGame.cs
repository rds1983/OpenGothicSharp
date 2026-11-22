using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;
using OpenGothic.Scene;
using OpenGothic.Utility;
using OpenGothic.UI;
using System;
using System.IO;

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

	/// <summary>Frames drawn so far, used to time a <c>-screenshot</c> capture.</summary>
	private int _framesDrawn;

	/// <summary>Set once the requested screenshot has been written or abandoned.</summary>
	private bool _screenshotDone;

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
		SpawnRequestedModel();
	}

	/// <summary>
	/// Places the model named by <c>-model</c> straight in front of the camera so it can be
	/// inspected without hunting for a vob that uses it. The scene is a plain list of nodes
	/// per grid cell, so the node is attached to the cell the camera stands in.
	/// </summary>
	private void SpawnRequestedModel()
	{
		var name = CommandLine.Instance?.SpawnModel;
		if (string.IsNullOrEmpty(name) || _assets == null || _world == null)
		{
			return;
		}

		DrModel model;
		try
		{
			model = _assets.GetModel(GraphicsDevice, name);
		}
		catch (Exception ex)
		{
			Log.Error($"Unable to load model '{name}': {ex.Message}");
			return;
		}

		var instance = new DrModelInstance(model);

		var distance = CommandLine.Instance.SpawnDistance ?? 400f;
		var position = Camera.Position + Camera.Forward * distance;
		var transform = Matrix.CreateTranslation(position);

		var node = new ModelNode
		{
			Instance = instance,
			Transform = transform
		};

		var bounds = (instance.BoundingBox ?? new BoundingBox(position, position)).Transform(ref transform);
		var cell = _world.FindCellByBox(bounds);
		if (cell == null)
		{
			Log.Warning($"Unable to place spawn model '{name}': no grid cell contains it.");
			return;
		}

		cell.ObjectBounds.Add(bounds);
		cell.Children.Add(node);

		Log.Info($"Spawned model '{name}' at {position} ({distance} units ahead).");
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

		// Diagnostic overrides: park the view on a known object instead of moving there.
		if (CommandLine.Instance.CameraPosition.HasValue)
		{
			Camera.Position = CommandLine.Instance.CameraPosition.Value;
		}

		if (CommandLine.Instance.LookAtTarget.HasValue)
		{
			Camera.LookAt(CommandLine.Instance.LookAtTarget.Value);
		}

		// Debug views are usually toggled with F2/F3; starting them on makes a capture
		// self-describing without needing synthetic keystrokes.
		if (CommandLine.Instance.Wireframe.HasValue)
		{
			DebugDraw.Wireframe = CommandLine.Instance.Wireframe.Value;
		}

		if (CommandLine.Instance.Bounds.HasValue)
		{
			DebugDraw.Bounds = CommandLine.Instance.Bounds.Value;
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

		_framesDrawn++;
		CaptureScreenshotIfDue();

		base.Draw(gameTime);
	}

	/// <summary>
	/// Writes the rendered frame to the path given by <c>-screenshot</c> once the requested
	/// frame has been drawn, then exits. Reading the backbuffer directly is the only capture
	/// that works here: an OS-level screenshot of the window comes back black because the
	/// hardware-accelerated surface never passes through the desktop compositor.
	/// </summary>
	private void CaptureScreenshotIfDue()
	{
		var path = CommandLine.Instance?.ScreenshotPath;
		if (path == null || _screenshotDone)
		{
			return;
		}

		var frame = CommandLine.Instance.ScreenshotFrame ?? 1;
		if (_framesDrawn < frame)
		{
			return;
		}

		// Set first so a failed capture is not retried for the rest of the session.
		_screenshotDone = true;

		var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
		var height = GraphicsDevice.PresentationParameters.BackBufferHeight;

		try
		{
			var colors = new Color[width * height];
			GraphicsDevice.GetBackBufferData(colors);

			using (var texture = new Texture2D(GraphicsDevice, width, height))
			{
				texture.SetData(colors);

				var directory = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(directory))
				{
					Directory.CreateDirectory(directory);
				}

				using (var stream = File.Create(path))
				{
					texture.SaveAsPng(stream, width, height);
				}
			}

			Log.Info($"Screenshot written to '{path}' (frame {_framesDrawn}).");
		}
		catch (Exception ex)
		{
			Log.Error($"Unable to write screenshot '{path}': {ex.Message}");
		}

		Exit();
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
