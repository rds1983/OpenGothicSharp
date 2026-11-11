using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGothic.Engine;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenGothic.Tools;

/// <summary>
/// Interactive world viewer: loads the requested .ZEN, then draws every mesh with
/// stock MonoGame effects (BasicEffect) while the camera flies through the scene.
/// The mesh loader needs a live GraphicsDevice, which is why this is a Game host.
/// </summary>
internal sealed class WorldViewer : Game
{
	private const float FieldOfView = MathHelper.Pi / 3.0f;
	private const float NearPlane = 5.0f;
	private const float FarPlane = 90000.0f;

	private readonly GraphicsDeviceManager _graphics;
	private readonly string _folder;
	private readonly string _worldName;
	private readonly FlyCamera _camera = new FlyCamera();
	private readonly Dictionary<DrMeshPart, BoundingSphere> _boundsCache = new Dictionary<DrMeshPart, BoundingSphere>();

	private WorldGrid _world;
	private BasicEffect _effect;
	private Matrix _projection = Matrix.Identity;
	private int _viewportWidth;
	private int _viewportHeight;

	private bool _succeeded;
	private int _lastScrollValue;
	private double _titleUpdateTimer;

	/// <summary>True once the world has been loaded and shown.</summary>
	public bool Succeeded => _succeeded;

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

			Console.WriteLine($"OpenGothicSharp {OG.Version}");
			Console.WriteLine($"Game folder: {_folder}");
			Console.WriteLine($"World:       {_worldName}");
			Console.WriteLine();

			var assets = new Assets(_folder);
			Console.WriteLine();
			Console.WriteLine($"Indexed {assets.Keys.Length} files.");

			var stopwatch = Stopwatch.StartNew();
			_world = assets.GetWorld(GraphicsDevice, _worldName);
			stopwatch.Stop();

			PrintWorld(_world);
			Console.WriteLine($"World loaded in {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
			Console.WriteLine();
			Console.WriteLine("Controls:");
			Console.WriteLine("  W/A/S/D         move");
			Console.WriteLine("  Space / L-Ctrl  up / down");
			Console.WriteLine("  Left Shift      fast move");
			Console.WriteLine("  Mouse           look");
			Console.WriteLine("  Mouse wheel     movement speed");
			Console.WriteLine("  Escape          quit");
			Console.WriteLine();

			_effect = new BasicEffect(GraphicsDevice)
			{
				TextureEnabled = true,
				LightingEnabled = true,
				PreferPerPixelLighting = false,
				DiffuseColor = Vector3.One,
				SpecularColor = Vector3.Zero,
			};

			_effect.EnableDefaultLighting();
			_effect.AmbientLightColor = new Vector3(0.45f, 0.45f, 0.45f);
			_effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.4f, -1.0f, -0.3f));

			_effect.FogEnabled = true;
			_effect.FogColor = SkyColor.ToVector3();
			_effect.FogStart = Constants.MaxShadowDistance;
			_effect.FogEnd = FarPlane * 0.8f;

			var viewport = GraphicsDevice.Viewport;
			_viewportWidth = viewport.Width;
			_viewportHeight = viewport.Height;
			_projection = Matrix.CreatePerspectiveFieldOfView(FieldOfView, (float)Math.Max(1, _viewportWidth) / Math.Max(1, _viewportHeight), NearPlane, FarPlane);

			PlaceCamera(_world);
			CenterMouse();

			_succeeded = true;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Failed to load '{_worldName}':");
			Console.Error.WriteLine(ex);
			_succeeded = false;
			Exit();
		}
	}

	private static Color SkyColor => new Color(92, 118, 148);

	protected override void Update(GameTime gameTime)
	{
		base.Update(gameTime);

		var keyboard = Keyboard.GetState();
		if (keyboard.IsKeyDown(Keys.Escape))
		{
			Exit();
			return;
		}

		UpdateMouseLook();

		var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
		var direction = Vector3.Zero;

		if (keyboard.IsKeyDown(Keys.W)) direction += _camera.Forward;
		if (keyboard.IsKeyDown(Keys.S)) direction -= _camera.Forward;
		if (keyboard.IsKeyDown(Keys.D)) direction += _camera.Right;
		if (keyboard.IsKeyDown(Keys.A)) direction -= _camera.Right;
		if (keyboard.IsKeyDown(Keys.Space)) direction += Vector3.Up;
		if (keyboard.IsKeyDown(Keys.LeftControl)) direction -= Vector3.Up;

		_camera.Move(direction, delta, keyboard.IsKeyDown(Keys.LeftShift));
	}

	private void UpdateMouseLook()
	{
		var mouse = Mouse.GetState();
		var center = new Point(_viewportWidth > 0 ? _viewportWidth / 2 : 640, _viewportHeight > 0 ? _viewportHeight / 2 : 360);

		_camera.Look(mouse.X - center.X, mouse.Y - center.Y);

		var wheel = mouse.ScrollWheelValue;
		if (wheel != _lastScrollValue)
		{
			_camera.Zoom(Math.Sign(wheel - _lastScrollValue));
			_lastScrollValue = wheel;
		}

		Mouse.SetPosition(center.X, center.Y);
	}

	protected override void Draw(GameTime gameTime)
	{
		var device = GraphicsDevice;
		var viewport = device.Viewport;

		if (viewport.Width != _viewportWidth || viewport.Height != _viewportHeight)
		{
			_viewportWidth = viewport.Width;
			_viewportHeight = viewport.Height;
			var aspect = (float)Math.Max(1, viewport.Width) / Math.Max(1, viewport.Height);
			_projection = Matrix.CreatePerspectiveFieldOfView(FieldOfView, aspect, NearPlane, FarPlane);
			_lastScrollValue = Mouse.GetState().ScrollWheelValue;
		}

		device.Clear(SkyColor);
		device.DepthStencilState = DepthStencilState.Default;
		device.BlendState = BlendState.Opaque;
		device.RasterizerState = RasterizerState.CullCounterClockwise;
		device.SamplerStates[0] = SamplerState.LinearWrap;

		if (_world == null)
		{
			base.Draw(gameTime);
			return;
		}

		_effect.View = _camera.View;
		_effect.Projection = _projection;

		var frustum = new BoundingFrustum(_camera.View * _projection);
		var drawCalls = 0;

		foreach (var cell in _world.Cells)
		{
			if (cell.Children.Count == 0 || !frustum.Intersects(cell.BoundingBox))
			{
				continue;
			}

			foreach (var node in cell.Children)
			{
				switch (node)
				{
					case MeshNode mesh:
						if (mesh.Mesh != null && IsVisible(mesh.Mesh, ref mesh.Transform, frustum))
						{
							DrawPart(mesh.Mesh, mesh.Material, mesh.Transform);
							++drawCalls;
						}
						break;

					case InstancedMeshNode instanced:
						if (instanced.Mesh == null)
						{
							break;
						}

						for (var i = 0; i < instanced.InstancesTransforms.Count; ++i)
						{
							var world = instanced.InstancesTransforms[i];
							if (IsVisible(instanced.Mesh, ref world, frustum))
							{
								DrawPart(instanced.Mesh, instanced.Material, world);
								++drawCalls;
							}
						}
						break;
				}
			}
		}

		_titleUpdateTimer += gameTime.ElapsedGameTime.TotalSeconds;
		if (_titleUpdateTimer >= 0.25)
		{
			_titleUpdateTimer = 0.0;
			Window.Title = $"OpenGothic viewer | {_worldName} | {drawCalls} draws | pos {_camera.Position.X:0},{_camera.Position.Y:0},{_camera.Position.Z:0} | speed {_camera.MoveSpeed:0}";
		}

		base.Draw(gameTime);
	}

	private void DrawPart(DrMeshPart part, Material material, Matrix world)
	{
		var texture = material?.DiffuseTexture;

		_effect.World = world;
		_effect.Texture = texture;
		_effect.TextureEnabled = texture != null;
		_effect.DiffuseColor = material.DiffuseColor.ToVector3();
		_effect.CurrentTechnique.Passes[0].Apply();

		var device = GraphicsDevice;
		device.SetVertexBuffer(part.VertexBuffer);
		device.Indices = part.IndexBuffer;
		device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, part.PrimitiveCount);
	}

	private bool IsVisible(DrMeshPart part, ref Matrix world, BoundingFrustum frustum)
	{
		if (!_boundsCache.TryGetValue(part, out var sphere))
		{
			var box = part.BoundingBox;
			var center = (box.Min + box.Max) * 0.5f;
			sphere = new BoundingSphere(center, (box.Max - box.Min).Length() * 0.5f);
			_boundsCache[part] = sphere;
		}

		sphere.Transform(ref world, out var transformed);
		return frustum.Intersects(transformed);
	}

	private void PlaceCamera(WorldGrid world)
	{
		BoundingBox? bounds = null;
		foreach (var cell in world.Cells)
		{
			if (cell.Children.Count == 0)
			{
				continue;
			}

			bounds = bounds.HasValue ? BoundingBox.CreateMerged(bounds.Value, cell.BoundingBox) : cell.BoundingBox;
		}

		var box = bounds ?? new BoundingBox(Vector3.Zero, Vector3.Zero);
		var center = (box.Min + box.Max) * 0.5f;

		_camera.Position = new Vector3(center.X, box.Max.Y + 1500.0f, center.Z);
		_camera.Yaw = 0.0f;
		_camera.Pitch = -0.45f;
	}

	private void CenterMouse()
	{
		var viewport = GraphicsDevice.Viewport;
		Mouse.SetPosition(viewport.Width / 2, viewport.Height / 2);
		_lastScrollValue = Mouse.GetState().ScrollWheelValue;
	}

	private static void PrintWorld(WorldGrid world)
	{
		var usedCells = 0;
		var meshNodes = 0;
		var instancedNodes = 0;
		var instances = 0;
		BoundingBox? bounds = null;

		foreach (var cell in world.Cells)
		{
			if (cell.Children.Count == 0)
			{
				continue;
			}

			++usedCells;
			bounds = bounds.HasValue ? BoundingBox.CreateMerged(bounds.Value, cell.BoundingBox) : cell.BoundingBox;

			foreach (var node in cell.Children)
			{
				switch (node)
				{
					case InstancedMeshNode instanced:
						++instancedNodes;
						instances += instanced.InstancesTransforms.Count;
						break;

					case MeshNode:
						++meshNodes;
						break;
				}
			}
		}

		Console.WriteLine($"Grid:            {Constants.GridSize}x{Constants.GridSize} ({usedCells} non-empty cells)");
		Console.WriteLine($"Mesh nodes:      {meshNodes}");
		Console.WriteLine($"Instanced nodes: {instancedNodes} ({instances} instances)");
		Console.WriteLine($"Scene nodes:     {meshNodes + instances}");

		if (bounds.HasValue)
		{
			var b = bounds.Value;
			Console.WriteLine($"Bounds:          min {Format(b.Min)}, max {Format(b.Max)}");
		}
	}

	private static string Format(Vector3 v) => $"({v.X:0.##}, {v.Y:0.##}, {v.Z:0.##})";
}
