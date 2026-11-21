using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Engine;
using OpenGothic.Rendering;
using OpenGothic.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenGothic.Gameplay;

/// <summary>
/// The engine-side game session: assets, the loaded world, the hero, the player, the camera and
/// the renderer. Hosts create one, feed it <see cref="GameInput"/> each frame and call
/// <see cref="Draw"/>; they never touch assets, culling or effects themselves.
/// </summary>
/// <remarks>
/// This is deliberately not a MonoGame <see cref="Microsoft.Xna.Framework.Game"/>: that type is
/// the host's, and a future game executable must be able to drive the same <see cref="GameCore"/>.
/// Window creation, input polling and mouse capture stay in the host.
/// </remarks>
public sealed class GameCore : IDisposable
{
	private const float FieldOfView = MathHelper.Pi / 3.0f;
	private const float NearPlane = 5.0f;
	private const float FarPlane = 90000.0f;

	// The hero's starting outfit. In Gothic's naming S_RUN is the standing idle and
	// S_RUNL is the actual run cycle.
	private const string HeroIdleClip = "S_RUN";
	private const string HeroRunClip = "S_RUNL";

	private readonly GraphicsDevice _device;
	private readonly string _folder;
	private readonly string _worldName;
	private readonly FollowCamera _camera = new FollowCamera();
	private readonly Player _player = new Player();
	private readonly Dictionary<DrMeshPart, BoundingSphere> _boundsCache = new Dictionary<DrMeshPart, BoundingSphere>();

	private WorldGrid _world;
	private DrModel _hero;
	private DrModelInstance _heroInstance;
	private AnimationController _heroAnimations;
	private DrMesh _heroHead;
	private DrModelBone _heroHeadBone;
	private DrModelBone _heroRootBone;
	private bool _heroMoving;
	private ForwardRenderer _renderer;
	private Hud _hud;
	private Matrix _projection = Matrix.Identity;
	private int _viewportWidth;
	private int _viewportHeight;

	private bool _loaded;
	private double _titleUpdateTimer;

	/// <param name="host">
	/// The MonoGame game owning the graphics device. Myra resolves widget styles through it, so
	/// the engine needs it for the HUD even though it never drives the game loop.
	/// </param>
	/// <param name="folder">Gothic II installation folder.</param>
	/// <param name="worldName">World file to load, e.g. <c>NEWWORLD.ZEN</c>.</param>
	public GameCore(Microsoft.Xna.Framework.Game host, string folder, string worldName)
	{
		if (host == null)
		{
			throw new ArgumentNullException(nameof(host));
		}

		if (string.IsNullOrEmpty(folder))
		{
			throw new ArgumentException("A Gothic installation folder is required.", nameof(folder));
		}

		if (string.IsNullOrEmpty(worldName))
		{
			throw new ArgumentException("A world name is required.", nameof(worldName));
		}

		_device = host.GraphicsDevice;
		_folder = folder;
		_worldName = worldName;
		Host = host;
	}

	/// <summary>The host that owns the window and graphics device.</summary>
	public Microsoft.Xna.Framework.Game Host { get; }

	/// <summary>Colour the host should clear the back buffer with.</summary>
	public static Color SkyColor => new Color(92, 118, 148);

	/// <summary>The loaded world, or <c>null</c> before <see cref="Load"/> completes.</summary>
	public WorldGrid World => _world;

	/// <summary>The player avatar. Valid once <see cref="Load"/> completes.</summary>
	public Player Player => _player;

	/// <summary>The orbit camera. Valid once <see cref="Load"/> completes.</summary>
	public FollowCamera Camera => _camera;

	/// <summary>Name of the world being viewed, for window titles and logs.</summary>
	public string WorldName => _worldName;

	/// <summary>
	/// Loads assets, the world and the hero, and places the player. Throws on failure so the host
	/// can decide how to report it.
	/// </summary>
	public void Load()
	{
		var stopwatch = Stopwatch.StartNew();

		var assets = new Assets(_folder);
		OG.LogInfo($"Indexed {assets.Keys.Length} files.");

		_world = assets.GetWorld(_device, _worldName);
		stopwatch.Stop();

		PrintWorld(_world);
		OG.LogInfo($"World loaded in {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");

		_hud = new Hud(Host);
		_hud.DebugOverlayVisible = true;

		_renderer = new ForwardRenderer(_device, SkyColor)
		{
			FogStart = Constants.MaxShadowDistance,
			// FarPlane is the projection's far distance; fog fades in well before geometry
			// is clipped so the horizon does not pop.
			FogEnd = FarPlane * 0.8f,
		};

		LoadHero(assets);

		var viewport = _device.Viewport;
		_viewportWidth = viewport.Width;
		_viewportHeight = viewport.Height;
		_projection = Matrix.CreatePerspectiveFieldOfView(
			FieldOfView,
			(float)Math.Max(1, _viewportWidth) / Math.Max(1, _viewportHeight),
			NearPlane,
			FarPlane);

		PlacePlayer(_world);

		_loaded = true;
	}

	private void LoadHero(Assets assets)
	{
		_hero = assets.GetModel(_device, "HUMANS.MSB", "ARMOR_PRISONER.MDM", HeroIdleClip, HeroRunClip);
		_heroHead = assets.GetMorphMesh(_device, "HUM_HEAD_PONY.MMB");
		_heroHeadBone = _hero.FindBoneByName("BIP01 HEAD");

		foreach (var bone in _hero.Bones)
		{
			if (bone.Parent == _hero.Root && bone.Mesh == null)
			{
				_heroRootBone = bone;
				break;
			}
		}

		_heroInstance = new DrModelInstance(_hero);
		_heroAnimations = new AnimationController(_heroInstance);
		_heroAnimations.StartClip(HeroIdleClip, AnimationFlags.Looped);

		OG.LogInfo($"Hero model:      {_hero.Meshes.Length} meshes, {_hero.Bones.Length} bones, head {(_heroHeadBone != null ? "attached" : "missing")}");
		OG.LogInfo($"Hero clips:      {string.Join(", ", _hero.Animations.Keys)}");
	}

	/// <summary>
	/// Advances the simulation by one frame: camera look, movement, collision, gravity and hero
	/// animation. Does nothing before <see cref="Load"/> has completed.
	/// </summary>
	public void Update(GameTime gameTime, GameInput input)
	{
		if (!_loaded)
		{
			return;
		}

		if (input.ToggleDebugOverlay)
		{
			_hud.ToggleDebugOverlay();
		}

		_camera.Look(input.LookDeltaX, input.LookDeltaY);
		_camera.Zoom(input.WheelDelta);

		var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
		var direction = _camera.PlanarForward * input.MoveForward + _camera.PlanarRight * input.MoveRight;

		UpdateHeroAnimation(delta, input.MoveForward != 0.0f || input.MoveRight != 0.0f);

		_player.Move(direction, delta, input.Run);
		ResolveCollision();
		ApplyGravity(delta);
	}

	private void UpdateHeroAnimation(float delta, bool moving)
	{
		if (_heroAnimations == null)
		{
			return;
		}

		if (moving != _heroMoving)
		{
			_heroMoving = moving;
			_heroAnimations.CrossfadeToClip(moving ? HeroRunClip : HeroIdleClip, TimeSpan.FromSeconds(0.25), AnimationFlags.Looped);
		}

		_heroAnimations.Update(TimeSpan.FromSeconds(delta));

		// Gothic run clips animate the root bone forward (S_RUNL moves it hundreds of units).
		// The player position is authoritative, so keep the root at its default pose.
		if (_heroRootBone != null)
		{
			_heroInstance.SetBoneLocalTransform(_heroRootBone.Index, _heroInstance.GetDefaultPose(_heroRootBone.Index).ToMatrix());
		}
	}

	private void ResolveCollision()
	{
		if (_world?.Collision == null)
		{
			return;
		}

		var feet = _player.Position.Y - Player.HalfHeight;
		_world.Collision.ResolveWalls(ref _player.Position, Player.Radius, feet, feet + Player.HalfHeight * 2.0f, Player.StepHeight);
	}

	private void ApplyGravity(float delta)
	{
		var feet = _player.Position.Y - Player.HalfHeight;
		float? groundY = null;

		if (_world?.Collision != null
			&& _world.Collision.TryGetGround(_player.Position.X, _player.Position.Z, feet + Player.StepHeight, out var ground))
		{
			groundY = ground;
		}

		_player.ApplyGravity(delta, groundY);
	}

	/// <summary>
	/// Draws the visible world and the hero, then the HUD. The host is responsible for clearing
	/// the back buffer first (using <see cref="SkyColor"/>).
	/// </summary>
	public void Draw(GameTime gameTime)
	{
		if (!_loaded)
		{
			return;
		}

		var viewport = _device.Viewport;
		if (viewport.Width != _viewportWidth || viewport.Height != _viewportHeight)
		{
			_viewportWidth = viewport.Width;
			_viewportHeight = viewport.Height;
			var aspect = (float)Math.Max(1, viewport.Width) / Math.Max(1, viewport.Height);
			_projection = Matrix.CreatePerspectiveFieldOfView(FieldOfView, aspect, NearPlane, FarPlane);
		}

		var view = _camera.GetView(_player.Position);
		_renderer.View = view;
		_renderer.Projection = _projection;
		_renderer.BeginFrame();

		var frustum = new BoundingFrustum(view * _projection);

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
							_renderer.DrawMeshPart(mesh.Mesh, mesh.Material, mesh.Transform);
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
								_renderer.DrawMeshPart(instanced.Mesh, instanced.Material, world);
							}
						}
						break;
				}
			}
		}

		DrawHero();

		_hud.Update(gameTime, _renderer.DrawCalls, _renderer.DrawnTriangles);
		_hud.Render();
	}

	private void DrawHero()
	{
		if (_hero == null)
		{
			return;
		}

		var feet = _player.Position - Vector3.Up * Player.HalfHeight;
		var world = Matrix.CreateRotationY(_player.Yaw) * Matrix.CreateTranslation(feet);

		foreach (var mesh in _hero.Meshes)
		{
			foreach (var part in mesh.MeshParts)
			{
				if (part.Skin == null)
				{
					continue;
				}

				// Skinned vertices are already baked into the model's bind pose, so only the hero
				// transform is needed here; the skeleton comes from the animated bone matrices.
				_renderer.DrawMeshPart(part, null, world, _heroInstance.GetSkinTransforms(part.Skin.SkinIndex));
			}
		}

		if (_heroHead != null)
		{
			// The head is a morph mesh without morphing for now; its vertices live in head-bone space.
			var headWorld = _heroHeadBone != null
				? _heroInstance.GetBoneGlobalTransform(_heroHeadBone.Index) * world
				: world;

			foreach (var part in _heroHead.MeshParts)
			{
				_renderer.DrawMeshPart(part, null, headWorld);
			}
		}
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

	private void PlacePlayer(WorldGrid world)
	{
		_camera.Yaw = 0.0f;
		_camera.Pitch = -0.25f;
		_camera.Distance = 250.0f;
		_player.Yaw = 0.0f;

		// The world's zCVobStartpoint marks where a new game begins (Xardas' tower in NEWWORLD).
		if (world.PlayerStart is Vector3 start)
		{
			// The marker sits on the floor, while the capsule is centered on its position.
			_player.Position = start + Vector3.Up * Player.HalfHeight;
			OG.LogInfo($"Player start:    {Format(start)}");
			return;
		}

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

		_player.Position = new Vector3(center.X, box.Max.Y + 1500.0f, center.Z);
		OG.LogInfo("Player start:    no startpoint in world, using world centre");
	}

	/// <summary>Formats the camera and draw statistics for a window title.</summary>
	public string GetTitleText()
	{
		return $"OpenGothic viewer | {_worldName} | {_renderer.DrawCalls} draws | {_renderer.DrawnTriangles} tris | player {_player.Position.X:0},{_player.Position.Y:0},{_player.Position.Z:0} | camera {_camera.Distance:0}";
	}

	/// <summary>
	/// Whether the host should refresh its window title this frame. Throttles to four updates a
	/// second so the title does not churn every frame.
	/// </summary>
	public bool ShouldUpdateTitle(GameTime gameTime)
	{
		_titleUpdateTimer += gameTime.ElapsedGameTime.TotalSeconds;
		if (_titleUpdateTimer < 0.25)
		{
			return false;
		}

		_titleUpdateTimer = 0.0;
		return true;
	}

	public void Dispose()
	{
		_hud?.Dispose();
		_hud = null;

		_renderer?.Dispose();
		_renderer = null;
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

		OG.LogInfo($"Grid:            {Constants.GridSize}x{Constants.GridSize} ({usedCells} non-empty cells)");
		OG.LogInfo($"Mesh nodes:      {meshNodes}");
		OG.LogInfo($"Instanced nodes: {instancedNodes} ({instances} instances)");
		OG.LogInfo($"Scene nodes:     {meshNodes + instances}");

		if (bounds.HasValue)
		{
			var b = bounds.Value;
			OG.LogInfo($"Bounds:          min {Format(b.Min)}, max {Format(b.Max)}");
		}
	}

	private static string Format(Vector3 v) => $"({v.X:0.##}, {v.Y:0.##}, {v.Z:0.##})";
}
