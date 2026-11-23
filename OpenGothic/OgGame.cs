using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;
using OpenGothic.Scene;
using OpenGothic.Physics;
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
	private PhysicsWorld _physics;
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
		DumpRequestedAnimations();
	}

	/// <summary>
	/// Runs <c>-animdump</c> once the assets are loaded, so a clip can be inspected without
	/// rendering anything. The model the dump is built for is the script's own skeleton, which
	/// is the only model guaranteed to pose every channel.
	/// </summary>
	private void DumpRequestedAnimations()
	{
		var script = CommandLine.Instance?.AnimationDump;
		if (string.IsNullOrEmpty(script) || _assets == null)
		{
			return;
		}

		try
		{
			_assets.DumpAnimations(GraphicsDevice, script.ToUpper(), CommandLine.Instance.AnimationClip);
		}
		catch (Exception ex)
		{
			Log.Error($"Unable to dump animations for '{script}': {ex.Message}");
		}
	}

	/// <summary>
	/// Places the model named by <c>-model</c> straight in front of the camera so it can be
	/// inspected without hunting for a vob that uses it. The scene is a plain list of nodes
	/// per grid cell, so the node is attached to the cell the camera stands in.
	/// </summary>
	private void SpawnRequestedModel()
	{
		var name = CommandLine.Instance?.SpawnModel;
		var skeleton = CommandLine.Instance?.ComposedSkeleton;
		var meshes = CommandLine.Instance?.ComposedMeshes;

		if ((string.IsNullOrEmpty(name) && string.IsNullOrEmpty(skeleton)) || _assets == null || _world == null)
		{
			return;
		}

		var label = string.IsNullOrEmpty(skeleton) ? name : $"compose:{skeleton}:{string.Join(",", meshes)}";

		DrModel model;
		try
		{
			model = string.IsNullOrEmpty(skeleton)
				? _assets.GetModel(GraphicsDevice, name)
				: _assets.GetComposedModel(GraphicsDevice, skeleton, meshes);
		}
		catch (Exception ex)
		{
			Log.Error($"Unable to load model '{label}': {ex.Message}");
			return;
		}

		var instance = new DrModelInstance(model);

		var distance = CommandLine.Instance.SpawnDistance ?? 400f;
		var offset = CommandLine.Instance.SpawnOffset ?? Vector3.Zero;
		var yaw = CommandLine.Instance.SpawnYaw ?? 0f;
		var position = Camera.Position
			+ Camera.Right * offset.X
			+ Camera.Up * offset.Y
			+ Camera.Forward * (distance + offset.Z);
		var transform = Matrix.CreateRotationY(MathHelper.ToRadians(yaw)) * Matrix.CreateTranslation(position);

		// One node type serves props and actors alike; a model with no clips simply never plays.
		var node = new ModelNode { Instance = instance };
		node.Transform = transform;

		var hasClips = model.Animations != null && model.Animations.Count > 0;

		// A model placed this way has no vob, so it is also the only place a clip can be
		// started from the command line.
		var clipName = CommandLine.Instance.PlayAnimation;
		var overlayName = CommandLine.Instance.OverlayAnimation;

		if (hasClips && (!string.IsNullOrEmpty(clipName) || !string.IsNullOrEmpty(overlayName)))
		{
			var animation = new AnimationData(instance);
			var used = false;

			if (!string.IsNullOrEmpty(clipName))
			{
				if (animation.Play(clipName, blend: false))
				{
					used = true;
					Log.Info($"Playing '{clipName}' on '{label}' ({animation.CurrentClip}); {DescribeClip(animation.CurrentInfo)}.");
				}
				else
				{
					Log.Error($"Model '{label}' has no playable clip '{clipName}'.");
				}
			}

			if (!string.IsNullOrEmpty(overlayName))
			{
				if (animation.PlayLayer(overlayName))
				{
					used = true;
					Log.Info($"Overlay '{overlayName}' on '{label}'.");
				}
				else
				{
					Log.Error($"Model '{label}' has no playable overlay clip '{overlayName}'.");
				}
			}

			if (used)
			{
				node.AnimationData = animation;
			}
		}
		else if (hasClips)
		{
			Log.Info($"Model '{label}' has clips but none was requested; add -play:<clip> to start one.");
		}

		var bounds = (instance.BoundingBox ?? new BoundingBox(position, position)).Transform(ref transform);
		var cell = _world.FindCellByBox(bounds);
		if (cell == null)
		{
			Log.Warning($"Unable to place spawn model '{label}': no grid cell contains it.");
			return;
		}

		cell.ObjectBounds.Add(bounds);
		cell.Children.Add(node);

		var attachPoint = CommandLine.Instance.AttachPoint;
		var attachVisual = CommandLine.Instance.AttachVisual;
		if (!string.IsNullOrEmpty(attachPoint) && !string.IsNullOrEmpty(attachVisual))
		{
			AttachVisual(node, attachPoint, attachVisual);
		}

		Log.Info($"Spawned model '{label}' at {position} ({distance} units ahead).");
	}

	/// <summary>
	/// Hangs a model on one of the spawned model's bones, which is how an actor wears armour or
	/// holds a weapon. The attachment is drawn with the parent and placed by the bone's current
	/// transform, so it follows the pose without becoming a scene node of its own.
	/// </summary>
	private void AttachVisual(ModelNode parent, string attachPoint, string visual)
	{
		var bone = parent.Instance.Model.FindBoneByName(attachPoint);
		if (bone == null)
		{
			Log.Error($"Model '{parent.Instance.Model}' has no attach point '{attachPoint}'.");
			return;
		}

		DrModel model = null;
		try
		{
			model = _assets.GetModel(GraphicsDevice, visual);
		}
		catch (Exception)
		{
			// Not a model; it may be a multi-resolution mesh, which wraps into one below.
		}

		if (model == null)
		{
			try
			{
				model = ModelBoneAttachment.WrapMesh(_assets.GetMultiMesh(GraphicsDevice, visual));
			}
			catch (Exception ex)
			{
				Log.Error($"Unable to load attach visual '{visual}': {ex.Message}");
				return;
			}
		}

		parent.BonesAttachments.Add(new ModelBoneAttachment(model) { Bone = bone });
		Log.Info($"Attached '{visual}' to '{attachPoint}'.");
	}

	/// <summary>One-line description of a clip's playback parameters, for the log.</summary>
	private static string DescribeClip(ModelClipInfo info)
	{
		if (info == null)
		{
			return "no metadata";
		}

		var traits = info.Loop ? "loop" : info.PlaybackNext != null ? $"one-shot -> {info.PlaybackNext}" : "one-shot";
		if (info.Reverse)
		{
			traits += ", reversed";
		}

		return $"{traits}, {info.FrameCount} frames at {info.Fps:0.##} fps, {info.Duration.TotalSeconds:0.###}s";
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

		if (_world != null && CommandLine.Instance.Physics)
		{
			try
			{
				_physics = new PhysicsWorld();

				// The default camera sits over the harbour basin; the model spawn point ahead
				// is on the plaza, which is a stable place to start the self-checks. Until
				// collision streams, only this neighbourhood is built.
				var center = Camera.Position + Camera.Forward * 260f;
				_physics.Build(_world, center, 3000f);
				_physics.RunWalkTest(center);
				_physics.RunStepTest();
				_physics.RunWallSlideTest();
				_physics.RunCapsuleTest(center);
			}
			catch (Exception ex)
			{
				Log.Error($"Unable to build the physics world: {ex.Message}");
			}
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

		UpdateAnimations(gameTime);

		base.Update(gameTime);
	}

	/// <summary>
	/// Advances every animating model in the world. <c>-freeze</c> pins the pose to
	/// <c>-animtime</c> instead, so a capture does not depend on how long the process has run.
	/// </summary>
	private void UpdateAnimations(GameTime gameTime)
	{
		if (_world == null)
		{
			return;
		}

		var frozen = CommandLine.Instance?.FrozenTime == true;
		var poseTime = TimeSpan.FromSeconds(CommandLine.Instance?.PlayTime ?? 0f);
		var elapsed = gameTime.ElapsedGameTime;

		foreach (var cell in _world.Cells)
		{
			foreach (var node in cell.Children)
			{
				if (node is not ModelNode model)
				{
					continue;
				}

				if (frozen)
				{
					model.AnimationData?.Seek(poseTime);
				}
				else
				{
					model.AnimationData?.Update(elapsed);
				}
			}
		}
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
		_physics?.Dispose();
		_physics = null;

		_renderer?.Dispose();
		_renderer = null;

		_worldRenderer?.Dispose();
		_worldRenderer = null;

		base.UnloadContent();
	}
}
