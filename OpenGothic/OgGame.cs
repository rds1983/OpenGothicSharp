using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;
using OpenGothic.Graphics;
using OpenGothic.Physics;
using OpenGothic.Focus;
using OpenGothic.Utility;
using OpenGothic.UI;
using OpenGothic.Vobs;
using System;
using System.Collections.Generic;
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
	private CharacterController _controller;
	private PlayerAvatar _avatar;
	private bool _walkMode;
	private bool _pendingInteract;
	private readonly FocusSystem _focus = new FocusSystem();
	private float _mouseYaw;
	private float _mousePitch;
	private bool _thirdPerson;
	private float _walkTestSeconds;
	private float _walkTestLogTimer;
	private bool _mouseLook = true;
	private bool _mouseNeedsCenter = true;
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

		// The model's bind already carries the hierarchy root translation, so an actor-like
		// placement is just the transform: the feet reach the origin without a second offset.
		// World props intentionally keep the same bind; their vob transform places them.

		// One node type serves props and actors alike; a model with no clips simply never plays.
		var node = new ModelNode { Instance = instance, Collision = false };
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

		if (_world != null && (CommandLine.Instance.Physics || CommandLine.Instance.StartWalking))
		{
			SetupPhysics();
		}

		if (_world != null)
		{
			LogVobQueries(_world);
		}
	}

	/// <summary>
	/// Diagnostic dump of the typed vob tree. <c>-vobstats</c> prints counts per kind, and
	/// <c>-vobfind:x,y,z</c> lists nodes near a point (radius from <c>-vobradius:</c>).
	/// </summary>
	private static void LogVobQueries(WorldGrid world)
	{
		var commandLine = CommandLine.Instance;
		if (commandLine.VobStats)
		{
			var counts = new Dictionary<VobKind, int>();
			foreach (var vob in world.Vobs.All)
			{
				counts.TryGetValue(vob.Kind, out var count);
				counts[vob.Kind] = count + 1;
			}

			var parts = new List<string>();
			foreach (VobKind kind in Enum.GetValues(typeof(VobKind)))
			{
				if (counts.TryGetValue(kind, out var count) && count > 0)
				{
					parts.Add($"{kind}={count}");
				}
			}

			Log.Info($"Vob tree: {world.Vobs.Count} nodes ({string.Join(", ", parts)}).");
		}

		if (!commandLine.VobFind.HasValue)
		{
			return;
		}

		var point = commandLine.VobFind.Value;
		var radius = commandLine.VobRadius ?? 1000f;
		var results = new List<VobNode>();
		world.Vobs.FindNear(point, radius, results);

		Log.Info($"Vobs near ({point.X:0.#}, {point.Y:0.#}, {point.Z:0.#}) within {radius:0}: {results.Count}.");
		for (var i = 0; i < results.Count && i < 50; ++i)
		{
			var vob = results[i];
			var position = vob.Position;
			var bounds = vob.Bounds;
			Log.Info(
				$"  {vob.Kind} '{vob.Name}' tag '{vob.Tag}' at " +
				$"({position.X:0.#}, {position.Y:0.#}, {position.Z:0.#}) " +
				$"bounds ({bounds.Min.X:0.#}, {bounds.Min.Y:0.#}, {bounds.Min.Z:0.#}).." +
				$"({bounds.Max.X:0.#}, {bounds.Max.Y:0.#}, {bounds.Max.Z:0.#})");
		}
	}

	/// <summary>
	/// Builds collision around the placeholder player start and, with <c>-walk</c>, creates the
	/// capsule the input drives. The physics self-checks are requested separately with
	/// <c>-physics</c>.
	/// </summary>
	private void SetupPhysics()
	{
		try
		{
			_physics = new PhysicsWorld();

			// TODO(K1): read the start point the game scripts select. Until the session exists,
			// the plaza ahead of the default camera is the stable standing place; -player:
			// overrides it for a diagnostic spot.
			var center = CommandLine.Instance.PlayerPosition ??
			             (Camera.Position + Camera.Forward * 260f);
			_physics.Initialize(_world, center, 3000f);

			if (CommandLine.Instance.StartWalking)
			{
				StartPlayer(center);
			}

			if (CommandLine.Instance.Physics)
			{
				_physics.RunWalkTest(center);
				_physics.RunStepTest();
				_physics.RunWallTest();
				_physics.RunCapsuleTest(center);
			}
		}
		catch (Exception ex)
		{
			Log.Error($"Unable to build the physics world: {ex.Message}");
		}
	}

	private void StartPlayer(Vector3 center)
	{
		// A diagnostic -player: point is an interior eye position; drop from just above it so a
		// roof does not become the ground. The placeholder plaza uses a long sky ray instead.
		var explicitStart = CommandLine.Instance.PlayerPosition.HasValue;
		var rayOrigin = explicitStart ? center + Vector3.UnitY * 10f : center + Vector3.UnitY * 1000f;
		var rayDistance = explicitStart ? 500f : 2000f;

		if (!_physics.RayCast(rayOrigin, -Vector3.UnitY, rayDistance, false, out var ground))
		{
			Log.Warning("Unable to find ground for the player; walking is disabled.");
			return;
		}

		_controller = new CharacterController(_physics)
		{
			Position = ground.Position + Vector3.UnitY * CharacterController.CenterOffset
		};

		try
		{
			_avatar = PlayerAvatar.Create(GraphicsDevice, _assets);
		}
		catch (Exception ex)
		{
			// The capsule must remain usable even when the placeholder body cannot load.
			Log.Warning($"Unable to create the player avatar: {ex.Message}");
		}

		_mouseYaw = CommandLine.Instance.PlayerYaw.HasValue
			? MathHelper.ToRadians(CommandLine.Instance.PlayerYaw.Value)
			: Camera.Yaw;
		_mousePitch = Camera.Pitch;
		_thirdPerson = CommandLine.Instance.ThirdPerson;
		_walkMode = CommandLine.Instance.WalkMode;
		_pendingInteract = CommandLine.Instance.Interact;
		_controller.Speed = _walkMode ? CharacterController.WalkSpeed : CharacterController.RunSpeed;
		_walkTestSeconds = CommandLine.Instance.WalkTestSeconds ?? 0f;
		// A fixed start yaw, a synthetic walk or a capture is a diagnostic run; keep the pointer
		// out of it so the result does not depend on where the OS cursor happened to be.
		_mouseLook = !(CommandLine.Instance.PlayerYaw.HasValue ||
		               CommandLine.Instance.WalkTestSeconds.HasValue ||
		               CommandLine.Instance.ScreenshotPath != null);
		_mouseNeedsCenter = true;
		Mouse.SetPosition(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);

		Log.Info($"Player spawned at feet ({_controller.FeetPosition.X:0.#}, {_controller.FeetPosition.Y:0.#}, {_controller.FeetPosition.Z:0.#}) on {ground.Kind}.");
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

		if (_controller != null)
		{
			UpdatePlayer(gameTime, keyboard);
			UpdateFocus(keyboard);
		}
		else
		{
			_focus.Target.Clear();
		}

		_previousKeyboard = keyboard;

		UpdateAnimations(gameTime);

		base.Update(gameTime);
	}

	/// <summary>
	/// Drives the capsule from WASD and mouse look, streams collision around it, and places the
	/// camera in first or third person. F5 switches the camera mode.
	/// </summary>
	private void UpdatePlayer(GameTime gameTime, KeyboardState keyboard)
	{
		if (keyboard.IsKeyDown(Keys.F5) && _previousKeyboard.IsKeyUp(Keys.F5))
		{
			_thirdPerson = !_thirdPerson;
		}

		// The reference toggles walk mode rather than requiring the key to be held.
		if ((keyboard.IsKeyDown(Keys.LeftShift) && _previousKeyboard.IsKeyUp(Keys.LeftShift)) ||
		    (keyboard.IsKeyDown(Keys.RightShift) && _previousKeyboard.IsKeyUp(Keys.RightShift)))
		{
			_walkMode = !_walkMode;
			_controller.Speed = _walkMode ? CharacterController.WalkSpeed : CharacterController.RunSpeed;
			Log.Info($"Walk mode {(_walkMode ? "on" : "off")}.");
		}

		var viewport = GraphicsDevice.Viewport;
		var centerX = viewport.Width / 2;
		var centerY = viewport.Height / 2;

		if (IsActive && _mouseLook)
		{
			var mouse = Mouse.GetState();
			var dx = mouse.X - centerX;
			var dy = mouse.Y - centerY;

			// The first read after centring can still report the old position, and a pointer
			// that left the window reports a huge jump. Both would spin the view in one frame.
			if (_mouseNeedsCenter)
			{
				_mouseNeedsCenter = false;
				Mouse.SetPosition(centerX, centerY);
			}
			else if (MathF.Abs(dx) <= 100 && MathF.Abs(dy) <= 100 && (dx != 0 || dy != 0))
			{
				_mouseYaw += dx * 0.0025f;
				_mousePitch = MathHelper.Clamp(_mousePitch - dy * 0.0025f, -1.4f, 1.4f);
				Mouse.SetPosition(centerX, centerY);
			}
			else if (MathF.Abs(dx) > 100 || MathF.Abs(dy) > 100)
			{
				Mouse.SetPosition(centerX, centerY);
			}
		}

		var forward = new Vector3(MathF.Sin(_mouseYaw), 0f, -MathF.Cos(_mouseYaw));
		var right = new Vector3(MathF.Cos(_mouseYaw), 0f, MathF.Sin(_mouseYaw));

		var wish = Vector3.Zero;
		if (keyboard.IsKeyDown(Keys.W)) wish += forward;
		if (keyboard.IsKeyDown(Keys.S)) wish -= forward;
		if (keyboard.IsKeyDown(Keys.D)) wish += right;
		if (keyboard.IsKeyDown(Keys.A)) wish -= right;

		var walkTestEnded = false;
		if (_walkTestSeconds != 0f)
		{
			var testDirection = MathF.Sign(_walkTestSeconds);
			wish += forward * testDirection;
			_walkTestSeconds -= (float)gameTime.ElapsedGameTime.TotalSeconds * testDirection;
			walkTestEnded = testDirection > 0f ? _walkTestSeconds <= 0f : _walkTestSeconds >= 0f;
			if (walkTestEnded)
			{
				_walkTestSeconds = 0f;
			}
		}

		_controller.Step(gameTime.ElapsedGameTime, wish, forward);
		_physics.Update(_controller.Position);

		if (_avatar != null)
		{
			var speed = new Vector2(_controller.Velocity.X, _controller.Velocity.Z).Length();
			_avatar.Update(gameTime.ElapsedGameTime, _controller.FeetPosition, _mouseYaw, speed, _walkMode);
		}

		if (_walkTestSeconds != 0f)
		{
			_walkTestLogTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
			if (_walkTestLogTimer <= 0f)
			{
				_walkTestLogTimer = 0.25f;
				Log.Info(
					$"Walk test: yaw {MathHelper.ToDegrees(_mouseYaw):0.#}, wish ({wish.X:0.##}, {wish.Z:0.##}), " +
					$"feet ({_controller.FeetPosition.X:0.#}, {_controller.FeetPosition.Y:0.#}, {_controller.FeetPosition.Z:0.#}).");
			}
		}

		if (walkTestEnded)
		{
			Log.Info($"Walk test ended at feet ({_controller.FeetPosition.X:0.#}, {_controller.FeetPosition.Y:0.#}, {_controller.FeetPosition.Z:0.#}).");
		}

		Camera.Yaw = _mouseYaw;
		Camera.Pitch = _mousePitch;

		var feet = _controller.FeetPosition;

		if (!_thirdPerson)
		{
			Camera.Position = feet + Vector3.UnitY * 160f;
			return;
		}

		var target = feet + Vector3.UnitY * 150f;
		var back = -Camera.Forward;
		var distance = 320f;
		if (_physics.RayCast(target, back, distance + 10f, false, out var hit))
		{
			distance = MathF.Max(70f, hit.Distance - 10f);
		}

		Camera.Position = target + back * distance;
	}

	/// <summary>
	/// Casts the crosshair ray from the player's eye and resolves the vob under it. The key
	/// only confirms the target for now; E3 turns that into an actual interaction. The ray
	/// starts at the capsule's centre rather than the chase camera, so both camera modes focus
	/// on what the player is looking at.
	/// </summary>
	private void UpdateFocus(KeyboardState keyboard)
	{
		if (_world == null)
		{
			_focus.Target.Clear();
			return;
		}

		var eye = _controller.FeetPosition + Vector3.UnitY * CharacterController.CenterOffset;
		_focus.Find(_world, _physics, new Ray(eye, Camera.Forward));

		var interact = _pendingInteract;
		_pendingInteract = false;
		if (interact || (keyboard.IsKeyDown(Keys.E) && _previousKeyboard.IsKeyUp(Keys.E)))
		{
			var target = _focus.Target;
			if (target.HasValue)
			{
				var position = target.Vob.Position;
				Log.Info($"Interact: {target.Describe()} at ({position.X:0.#}, {position.Y:0.#}, {position.Z:0.#}).");
			}
			else
			{
				Log.Info("Interact: no focus.");
			}
		}
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
		// In first person the camera sits inside the head, so the body is skipped rather than
		// drawn around it. The clip keeps running, ready for the next F5.
		var avatar = _thirdPerson ? _avatar?.Node : null;
		var highlight = _focus.Target.HasValue ? _focus.Target.Vob.Bounds : (BoundingBox?)null;
		_worldRenderer.Draw(_renderer, Camera, _world, DebugDraw, avatar, highlight);

		if (_hud.DebugOverlay.Visible)
		{
			// Read the counters after the world draw: BeginFrame resets them per frame.
			_hud.DebugOverlay.Update(gameTime, Camera, _renderer, DebugDraw, _focus.Target);
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
