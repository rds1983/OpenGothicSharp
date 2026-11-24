using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuPhysics.Trees;
using BepuUtilities;
using BepuUtilities.Memory;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using OpenGothic.Graphics;
using OpenGothic.Utility;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BoundingBox = Microsoft.Xna.Framework.BoundingBox;
using Matrix = Microsoft.Xna.Framework.Matrix;
using Num = System.Numerics;

namespace OpenGothic.Physics;

/// <summary>What a collidable is, for filtering and diagnostics.</summary>
public enum PhysicsSurfaceKind : byte
{
	Terrain,
	Vob,
	Sensor,
	Dynamic,
}

/// <summary>Per-collidable material state. Bepu has no built-in notion of one.</summary>
public struct PhysicsSurface
{
	public float Friction;
	public PhysicsSurfaceKind Kind;
}

public struct PhysicsRayHit
{
	public bool Hit;
	public float Distance;
	public Vector3 Position;
	public Vector3 Normal;
	public PhysicsSurfaceKind Kind;
}

/// <summary>
/// The static world as Bepu sees it: triangle meshes for the level and for static vobs, plus
/// the bodies the game adds later. Units are the game's centimetres, so gravity is 9.81 m/s²
/// expressed in those units.
/// </summary>
public sealed class PhysicsWorld : IDisposable
{
	private const float Gravity = -981f;
	private const float CapsuleRadius = 30f;
	private const float CapsuleLength = 110f;
	private const float StepSeconds = 1f / 60f;
	private const float StreamStep = 256f;
	private const float UnloadMargin = 500f;

	private readonly BufferPool _bufferPool = new();
	private readonly List<CollisionSource> _sources = new();
	private readonly Dictionary<long, LoadedChunk> _loadedChunks = new();
	private readonly Dictionary<CollisionSource, List<StaticCollider>> _loadedSources = new();
	private readonly List<long> _unloadChunks = new();
	private readonly List<CollisionSource> _unloadSources = new();

	private WorldGrid _world;
	private Vector3 _streamCenter;
	private float _loadRadius = 3000f;
	private bool _streaming;
	private int _staticCount;

	public Simulation Simulation { get; }

	/// <summary>Pool the sweeps and ray tests borrow their scratch buffers from.</summary>
	public BufferPool BufferPool => _bufferPool;

	/// <summary>Material state per collidable, looked up by the contact callback.</summary>
	public CollidableProperty<PhysicsSurface> Surfaces { get; }

	public int TerrainTriangles { get; private set; }
	public int VobTriangles { get; private set; }
	public int StaticBodyCount => _staticCount;

	public PhysicsWorld()
	{
		Surfaces = new CollidableProperty<PhysicsSurface>(_bufferPool);

		// The narrow-phase callback initializes Surfaces once the simulation exists; the
		// property must not be initialized twice.
		Simulation = Simulation.Create(
			_bufferPool,
			new NarrowPhaseCallbacks(Surfaces),
			new PoseIntegratorCallbacks(new Num.Vector3(0f, Gravity, 0f)),
			new SolveDescription(8, 1));
	}

	/// <summary>
	/// Collects the static vob geometry once and loads the level and vob neighbourhood around
	/// <paramref name="center"/>. <see cref="Update"/> keeps it centred as the player moves.
	/// A vob is collidable only where its model has rigid geometry: a skinned mesh deforms
	/// with its skeleton and is left out, matching the reference.
	/// </summary>
	public void Initialize(WorldGrid world, Vector3 center, float radius)
	{
		_world = world ?? throw new ArgumentNullException(nameof(world));
		_loadRadius = radius;

		CollectSources(world);
		_streamCenter = center;
		Update(center, true);

		Log.Info(
			$"Physics: {TerrainTriangles:N0} terrain triangles, {VobTriangles:N0} vob triangles " +
			$"near ({center.X:0.#}, {center.Z:0.#}), {_staticCount} static bodies; " +
			$"{_sources.Count} vob sources known.");
	}

	/// <summary>
	/// Loads and unloads collision when the player has moved far enough from the last
	/// streaming centre. The update is skipped while the player stays inside a small step, so
	/// walking does not rebuild anything every frame.
	/// </summary>
	public void Update(Vector3 center)
	{
		if (!_streaming)
		{
			return;
		}

		var dx = center.X - _streamCenter.X;
		var dz = center.Z - _streamCenter.Z;
		if (dx * dx + dz * dz < StreamStep * StreamStep)
		{
			return;
		}

		Update(center, false);
	}

	private void Update(Vector3 center, bool force)
	{
		_streaming = true;
		_streamCenter = center;

		var loadArea = Area(center, _loadRadius);
		var unloadArea = Area(center, _loadRadius + UnloadMargin);
		var loaded = 0;
		var unloaded = 0;

		// Terrain chunks whose bounds cover the load area. Chunks are laid out from the level
		// mesh's minimum corner, the same origin the triangles were bucketed with.
		var size = WorldGrid.CollisionChunkSize;
		var origin = _world.MeshBounds.Min;
		var minX = (int)MathF.Floor((loadArea.Min.X - origin.X) / size);
		var maxX = (int)MathF.Floor((loadArea.Max.X - origin.X) / size);
		var minZ = (int)MathF.Floor((loadArea.Min.Z - origin.Z) / size);
		var maxZ = (int)MathF.Floor((loadArea.Max.Z - origin.Z) / size);

		for (var x = minX; x <= maxX; ++x)
		{
			for (var z = minZ; z <= maxZ; ++z)
			{
				if (!_world.CollisionChunks.TryGetValue((x, z), out var chunk))
				{
					continue;
				}

				var key = ChunkKey(x, z);
				if (_loadedChunks.ContainsKey(key))
				{
					continue;
				}

				_loadedChunks[key] = new LoadedChunk(chunk, AddChunk(chunk));
				TerrainTriangles += chunk.Surfaces.TriangleCount;
				++loaded;
			}
		}

		_unloadChunks.Clear();
		foreach (var pair in _loadedChunks)
		{
			if (!pair.Value.Chunk.Bounds.Intersects(unloadArea))
			{
				_unloadChunks.Add(pair.Key);
			}
		}

		foreach (var key in _unloadChunks)
		{
			var loadedChunk = _loadedChunks[key];
			RemoveColliders(loadedChunk.Colliders);
			TerrainTriangles -= loadedChunk.Chunk.Surfaces.TriangleCount;
			_loadedChunks.Remove(key);
			++unloaded;
		}

		// Static vobs. Scanning the pre-collected sources is cheap; the expensive part is
		// building the Bepu mesh, which happens only for a source that crossed the radius.
		foreach (var source in _sources)
		{
			if (_loadedSources.ContainsKey(source) || !source.Bounds.Intersects(loadArea))
			{
				continue;
			}

			// Never drop a static around the player: a source that is being loaded because it
			// just entered the radius must not enclose the capsule, or every sweep would start
			// inside it. It is retried after the player moves.
			if (OverlapsPlayer(source.Bounds, center))
			{
				continue;
			}

			var colliders = AddSource(source);
			if (colliders.Count == 0)
			{
				continue;
			}

			_loadedSources[source] = colliders;
			VobTriangles += source.TriangleCount;
			++loaded;
		}

		_unloadSources.Clear();
		foreach (var pair in _loadedSources)
		{
			if (!pair.Key.Bounds.Intersects(unloadArea))
			{
				_unloadSources.Add(pair.Key);
			}
		}

		foreach (var source in _unloadSources)
		{
			RemoveColliders(_loadedSources[source]);
			VobTriangles -= source.TriangleCount;
			_loadedSources.Remove(source);
			++unloaded;
		}

		if (force || loaded > 0 || unloaded > 0)
		{
			Log.Info(
				$"Physics stream: {_loadedChunks.Count} chunks, {_loadedSources.Count} vob sources, " +
				$"{TerrainTriangles:N0} terrain + {VobTriangles:N0} vob triangles, {_staticCount} static bodies.");
		}
	}

	private static BoundingBox Area(Vector3 center, float radius)
	{
		return new BoundingBox(
			center - new Vector3(radius),
			center + new Vector3(radius));
	}

	/// <summary>Whether a source's box intersects the capsule the player occupies.</summary>
	private static bool OverlapsPlayer(BoundingBox bounds, Vector3 center)
	{
		var halfHeight = CharacterController.DefaultRadius + CharacterController.DefaultLength * 0.5f;
		var half = new Vector3(CharacterController.DefaultRadius, halfHeight, CharacterController.DefaultRadius);
		return bounds.Intersects(new BoundingBox(center - half, center + half));
	}

	private static long ChunkKey(int x, int z) => ((long)x << 32) | (uint)z;

	/// <summary>
	/// Turns the scene's static geometry into a flat list of sources once. A model is one
	/// source; an instanced mesh becomes one per instance so a large group does not load
	/// because one of its members is close.
	/// </summary>
	private void CollectSources(WorldGrid world)
	{
		_sources.Clear();

		foreach (var cell in world.Cells)
		{
			foreach (var node in cell.Children)
			{
				switch (node)
				{
					case MeshNode mesh:
						if (mesh.Collision)
						{
							AddPartSource(mesh.Mesh, mesh.WorldTransform);
						}

						break;

					case InstancedMeshNode instanced:
						for (var i = 0; i < instanced.InstancesTransforms.Count; ++i)
						{
							if (instanced.InstancesCollision[i])
							{
								AddPartSource(instanced.Mesh, instanced.InstancesTransforms[i]);
							}
						}

						break;

					case ModelNode model:
						if (model.Collision)
						{
							AddModelSource(model);
						}

						break;
				}
			}
		}
	}

	private void AddPartSource(DrMeshPart part, Matrix transform)
	{
		if (!HasCollision(part))
		{
			return;
		}

		var bounds = part.BoundingBox.Transform(ref transform);
		var tag = (DrMeshPartTag)part.Tag;
		_sources.Add(new CollisionSource
		{
			Bounds = bounds,
			Transform = transform,
			Part = part,
			TriangleCount = tag.Collision.TriangleCount
		});
	}

	private void AddModelSource(ModelNode node)
	{
		var instance = node.Instance;
		var modelWorld = node.WorldTransform;
		var triangles = 0;

		foreach (var mesh in instance.Model.Meshes)
		{
			if (mesh.ParentBone == null)
			{
				continue;
			}

			foreach (var part in mesh.MeshParts)
			{
				if (part.Skin == null && HasCollision(part))
				{
					triangles += ((DrMeshPartTag)part.Tag).Collision.TriangleCount;
				}
			}
		}

		if (triangles == 0)
		{
			return;
		}

		var bounds = instance.BoundingBox?.Transform(ref modelWorld) ??
		             new BoundingBox(modelWorld.Translation, modelWorld.Translation);

		_sources.Add(new CollisionSource
		{
			Bounds = bounds,
			Model = node,
			TriangleCount = triangles
		});
	}

	private static bool HasCollision(DrMeshPart part)
	{
		return part.Tag is DrMeshPartTag tag && tag.Collision != null && tag.Friction > 0f;
	}

	private List<StaticCollider> AddChunk(CollisionChunk chunk)
	{
		var result = new List<StaticCollider>();
		foreach (var surface in chunk.Surfaces.Surfaces)
		{
			AddMesh(surface.Vertices, Matrix.Identity, surface.Friction, surface.Sensor,
				PhysicsSurfaceKind.Terrain, result);
		}

		return result;
	}

	private List<StaticCollider> AddSource(CollisionSource source)
	{
		var result = new List<StaticCollider>();

		if (source.Part != null)
		{
			var tag = (DrMeshPartTag)source.Part.Tag;
			AddMesh(tag.Collision.Vertices, source.Transform, tag.Friction, tag.Sensor,
				PhysicsSurfaceKind.Vob, result);
			return result;
		}

		if (source.Model == null)
		{
			return result;
		}

		var instance = source.Model.Instance;
		var modelWorld = source.Model.WorldTransform;

		foreach (var mesh in instance.Model.Meshes)
		{
			if (mesh.ParentBone == null)
			{
				continue;
			}

			// The same placement the renderer uses for a static part.
			var transform = instance.GetBoneGlobalTransform(mesh.ParentBone.Index) * modelWorld;

			foreach (var part in mesh.MeshParts)
			{
				if (part.Skin != null || !HasCollision(part))
				{
					continue;
				}

				var tag = (DrMeshPartTag)part.Tag;
				AddMesh(tag.Collision.Vertices, transform, tag.Friction, tag.Sensor,
					PhysicsSurfaceKind.Vob, result);
			}
		}

		return result;
	}

	/// <summary>
	/// Builds one Bepu mesh from a triangle soup and registers it as a static. The triangles
	/// are duplicated with the opposite winding because Bepu meshes are one-sided and Gothic's
	/// geometry has no consistent winding.
	/// </summary>
	private void AddMesh(IReadOnlyList<Vector3> vertices, Matrix transform, float friction, bool sensor,
		PhysicsSurfaceKind kind, List<StaticCollider> target)
	{
		var source = 0;
		for (var i = 0; i + 2 < vertices.Count; i += 3)
		{
			if (!IsDegenerate(vertices[i], vertices[i + 1], vertices[i + 2]))
			{
				++source;
			}
		}

		if (source == 0)
		{
			return;
		}

		_bufferPool.Take<Triangle>(source * 2, out var triangles);

		var write = 0;
		for (var i = 0; i + 2 < vertices.Count; i += 3)
		{
			var a = Vector3.Transform(vertices[i], transform);
			var b = Vector3.Transform(vertices[i + 1], transform);
			var c = Vector3.Transform(vertices[i + 2], transform);
			if (IsDegenerate(a, b, c))
			{
				continue;
			}

			triangles[write++] = new Triangle(ToNumerics(a), ToNumerics(b), ToNumerics(c));
			triangles[write++] = new Triangle(ToNumerics(a), ToNumerics(c), ToNumerics(b));
		}

		var mesh = new Mesh(triangles, Num.Vector3.One, _bufferPool);
		var shape = Simulation.Shapes.Add(mesh);
		var handle = Simulation.Statics.Add(new StaticDescription(new Num.Vector3(), Num.Quaternion.Identity, shape));

		Surfaces.Allocate(handle) = new PhysicsSurface
		{
			Friction = friction,
			Kind = sensor ? PhysicsSurfaceKind.Sensor : kind
		};

		++_staticCount;
		target.Add(new StaticCollider(handle, shape));
	}

	private void RemoveColliders(List<StaticCollider> colliders)
	{
		foreach (var collider in colliders)
		{
			Simulation.Statics.Remove(collider.Handle);
			Simulation.Shapes.RemoveAndDispose(collider.Shape, _bufferPool);
			--_staticCount;
		}
	}

	private static bool IsDegenerate(Vector3 a, Vector3 b, Vector3 c)
	{
		var normal = Vector3.Cross(b - a, c - a);
		return normal.LengthSquared() < 1e-6f;
	}

	private readonly struct StaticCollider
	{
		public readonly StaticHandle Handle;
		public readonly TypedIndex Shape;

		public StaticCollider(StaticHandle handle, TypedIndex shape)
		{
			Handle = handle;
			Shape = shape;
		}
	}

	private sealed class CollisionSource
	{
		public BoundingBox Bounds;
		public Matrix Transform;
		public DrMeshPart Part;
		public ModelNode Model;
		public int TriangleCount;
	}

	private sealed class LoadedChunk
	{
		public CollisionChunk Chunk { get; }
		public List<StaticCollider> Colliders { get; }

		public LoadedChunk(CollisionChunk chunk, List<StaticCollider> colliders)
		{
			Chunk = chunk;
			Colliders = colliders;
		}
	}

	/// <summary>
	/// Casts a ray against the statics and returns the closest hit. Sensors are skipped
	/// unless <paramref name="includeSensors"/> asks for them.
	/// </summary>
	public bool RayCast(Vector3 origin, Vector3 direction, float maxDistance, bool includeSensors, out PhysicsRayHit hit, BodyHandle? ignoreBody = null)
	{
		var handler = new RayHitHandler
		{
			Surfaces = Surfaces,
			IncludeSensors = includeSensors,
			IgnoreBody = ignoreBody ?? default,
			HasBodyToIgnore = ignoreBody.HasValue
		};

		Simulation.RayCast(ToNumerics(origin), Num.Vector3.Normalize(ToNumerics(direction)), maxDistance, ref handler, 0);

		if (!handler.HasHit)
		{
			hit = default;
			return false;
		}

		var point = origin + direction * handler.Distance;
		hit = new PhysicsRayHit
		{
			Hit = true,
			Distance = handler.Distance,
			Position = point,
			Normal = ToXna(handler.Normal),
			Kind = Surfaces[handler.Collidable].Kind
		};

		return true;
	}

	/// <summary>Adds a solid static box, used by the step self-check.</summary>
	public void AddStaticBox(Vector3 center, Vector3 size, float friction, PhysicsSurfaceKind kind)
	{
		var box = new Box(size.X, size.Y, size.Z);
		var shape = Simulation.Shapes.Add(box);
		var handle = Simulation.Statics.Add(new StaticDescription(ToNumerics(center), shape));

		Surfaces.Allocate(handle) = new PhysicsSurface
		{
			Friction = friction,
			Kind = kind
		};

		++_staticCount;
	}

	/// <summary>
	/// Adds a kinematic capsule for a script NPC. The player's character controller sweeps
	/// against it, so an NPC blocks the player even before F2 gives it movement. The pose is
	/// updated later by moving the body; for now it stays at its spawn point.
	/// </summary>
	public BodyHandle AddNpcCapsule(Vector3 feetPosition)
	{
		var shape = Simulation.Shapes.Add(new Capsule(
			CharacterController.DefaultRadius, CharacterController.DefaultLength));
		var center = feetPosition + Vector3.UnitY * CharacterController.CenterOffset;

		var handle = Simulation.Bodies.Add(BodyDescription.CreateKinematic(
			new RigidPose(ToNumerics(center)),
			new CollidableDescription(shape, 0.1f),
			new BodyActivityDescription(0.01f, 32)));

		Surfaces.Allocate(handle) = new PhysicsSurface
		{
			Friction = 0.8f,
			Kind = PhysicsSurfaceKind.Dynamic
		};

		return handle;
	}

	/// <summary>Moves an NPC's kinematic capsule and refreshes its broad-phase bounds.</summary>
	public void SetNpcCapsulePosition(BodyHandle handle, Vector3 feetPosition)
	{
		var body = Simulation.Bodies[handle];
		body.Pose.Position = ToNumerics(feetPosition + Vector3.UnitY * CharacterController.CenterOffset);
		body.UpdateBounds();
	}

	/// <summary>
	/// Walks a capsule across the local terrain and reports whether it stayed on the surface.
	/// This is the D2 self-check: it exercises slopes, ground following and long walks without
	/// a renderer or input.
	/// </summary>
	public void RunWalkTest(Vector3 start)
	{
		if (!RayCast(start + Vector3.UnitY * 1000f, -Vector3.UnitY, 2000f, false, out var ground))
		{
			Log.Warning("Physics walk: no ground below the start.");
			return;
		}

		var controller = new CharacterController(this)
		{
			Position = ground.Position + Vector3.UnitY * CharacterController.CenterOffset
		};

		// Walk toward the most open direction rather than into the nearest building. A fixed
		// circle spent most of its time sliding along one wall, which says little about
		// walking over terrain.
		var wish = Vector3.UnitZ;
		var openDistance = 0f;
		for (var i = 0; i < 16; ++i)
		{
			var angle = MathF.Tau * i / 16f;
			var direction = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
			var distance = RayCast(controller.Position, direction, 2000f, false, out var hit)
				? hit.Distance
				: 2000f;

			if (distance > openDistance)
			{
				openDistance = distance;
				wish = direction;
			}
		}

		const int steps = 480;
		const float dt = 1f / 60f;

		var previous = controller.Position;
		var distanceWalked = 0f;
		var minClearance = float.MaxValue;
		var maxClearance = -float.MaxValue;
		var maxRise = 0f;
		var groundedSteps = 0;
		var fallThroughs = 0;

		for (var i = 0; i < steps; ++i)
		{
			controller.Step(TimeSpan.FromSeconds(dt), wish);
			Update(controller.Position);

			var position = controller.Position;
			var moved = new Vector3(position.X - previous.X, 0f, position.Z - previous.Z);
			distanceWalked += moved.Length();
			maxRise = MathF.Max(maxRise, MathF.Abs(position.Y - previous.Y));
			previous = position;

			var feet = position - Vector3.UnitY * CharacterController.CenterOffset;
			if (RayCast(feet + Vector3.UnitY * 2f, -Vector3.UnitY, 100f, false, out var support))
			{
				var clearance = feet.Y - support.Position.Y;
				minClearance = MathF.Min(minClearance, clearance);
				maxClearance = MathF.Max(maxClearance, clearance);
				if (clearance < -2f)
				{
					++fallThroughs;
				}
			}
			else
			{
				++fallThroughs;
			}

			if (controller.Grounded)
			{
				++groundedSteps;
			}
		}

		var seconds = steps * dt;
		var grounded = groundedSteps * 100f / steps;
		var walks = fallThroughs == 0 && grounded >= 80f && maxRise < 60f;

		Log.Info(
			$"Physics walk: {seconds:0.0} s toward ({wish.X:0.##}, {wish.Z:0.##}) with {openDistance:0} units open, " +
			$"{distanceWalked:N0} units walked, grounded {grounded:0.#}%, " +
			$"clearance {minClearance:0.##}..{maxClearance:0.##}, max rise {maxRise:0.##}, " +
			$"fall-throughs {fallThroughs} → {(walks ? "walks" : "FAILED")}.");
	}

	/// <summary>
	/// Walks over a 30-unit block on a flat floor. The chest capsule is above the block, so
	/// this is a ground-ray snap rather than a step assist: the feet rise to the block's top.
	/// </summary>
	public void RunStepTest()
	{
		using var world = new PhysicsWorld();

		// A floor whose top is y = 0, and a 30-unit step 100 units ahead.
		world.AddStaticBox(new Vector3(0f, -50f, 0f), new Vector3(2000f, 100f, 2000f),
			PhysicsMaterials.DefaultFriction, PhysicsSurfaceKind.Terrain);
		world.AddStaticBox(new Vector3(0f, 15f, 150f), new Vector3(300f, 30f, 300f),
			PhysicsMaterials.DefaultFriction, PhysicsSurfaceKind.Vob);

		var controller = new CharacterController(world)
		{
			Position = new Vector3(0f, CharacterController.CenterOffset, -100f)
		};

		var before = controller.FeetPosition.Y;
		var maxY = before;
		for (var i = 0; i < 120; ++i)
		{
			controller.Step(TimeSpan.FromSeconds(1f / 60f), Vector3.UnitZ);
			maxY = MathF.Max(maxY, controller.FeetPosition.Y);
		}

		var climbed = maxY - before;
		var stepped = controller.Grounded && climbed >= 25f && climbed <= 40f;

		Log.Info(
			$"Physics step: walked over a 30-unit block, feet climbed {climbed:0.#} → " +
			$"{(stepped ? "steps" : "FAILED")}.");
	}

	/// <summary>
	/// Walks into a wall, then reverses. The reference stops at a blocked substep instead of
	/// projecting the move along the surface, so the capsule must not drift sideways when it
	/// backs away — the bug reported at the harbour wall.
	/// </summary>
	public void RunWallTest()
	{
		using var world = new PhysicsWorld();
		world.AddStaticBox(new Vector3(0f, -50f, 0f), new Vector3(2000f, 100f, 2000f),
			PhysicsMaterials.DefaultFriction, PhysicsSurfaceKind.Terrain);
		world.AddStaticBox(new Vector3(0f, 100f, 0f), new Vector3(2000f, 200f, 20f),
			PhysicsMaterials.DefaultFriction, PhysicsSurfaceKind.Vob);

		var controller = new CharacterController(world)
		{
			Position = new Vector3(-100f, CharacterController.CenterOffset, -200f)
		};

		var start = controller.Position;
		for (var i = 0; i < 120; ++i)
		{
			controller.Step(TimeSpan.FromSeconds(1f / 60f), Vector3.UnitZ);
		}

		var wallGap = -10f - controller.FeetPosition.Z; // the near face is at z = -10
		var stopped = wallGap >= CharacterController.DefaultRadius - 2f &&
		              wallGap <= CharacterController.DefaultRadius + 3f;

		for (var i = 0; i < 60; ++i)
		{
			controller.Step(TimeSpan.FromSeconds(1f / 60f), -Vector3.UnitZ);
		}

		var backed = controller.FeetPosition.Z - start.Z < -60f;
		var sideways = MathF.Abs(controller.Position.X - start.X);
		var passes = stopped && backed && sideways < 1f;

		Log.Info(
			$"Physics wall: held {wallGap:0.#} from the face (radius {CharacterController.DefaultRadius:0}), " +
			$"backed {start.Z - controller.FeetPosition.Z:0.#} units with {sideways:0.##} sideways drift → " +
			$"{(passes ? "stops without sliding" : "FAILED")}.");
	}

	/// <summary>
	/// Drops a test capsule on the ground under <paramref name="eye"/>, checks that it comes
	/// to rest, then pushes it into the nearest wall and checks that it stays outside. This
	/// is the D1 self-check: the log states the rest offset and the wall separation, so a
	/// regression is visible without a physics debug renderer.
	/// </summary>
	public void RunCapsuleTest(Vector3 eye)
	{
		var halfHeight = CapsuleRadius + CapsuleLength * 0.5f;

		// Find the ground below the eye. The camera may sit on it or slightly above it.
		if (!RayCast(eye + Vector3.UnitY * 1000f, -Vector3.UnitY, 2000f, false, out var ground))
		{
			Log.Warning("Physics test: no ground below the camera.");
			return;
		}

		var start = ground.Position + Vector3.UnitY * 200f;
		var capsule = new Capsule(CapsuleRadius, CapsuleLength);
		var description = BodyDescription.CreateDynamic(
			new RigidPose(ToNumerics(start)),
			capsule.ComputeInertia(70f),
			new CollidableDescription(Simulation.Shapes.Add(capsule), 0.1f),
			new BodyActivityDescription(0.01f, 32));
		var bodyHandle = Simulation.Bodies.Add(description);

		Surfaces.Allocate(bodyHandle) = new PhysicsSurface
		{
			Friction = PhysicsMaterials.DefaultFriction,
			Kind = PhysicsSurfaceKind.Dynamic
		};

		for (var i = 0; i < 240; ++i)
		{
			Simulation.Timestep(StepSeconds);
		}

		var body = Simulation.Bodies[bodyHandle];
		var rested = ToXna(body.Pose.Position);
		var speed = body.Velocity.Linear.Length();

		// Ask again from the capsule to name what it is standing on. The capsule itself is
		// excluded from this ray.
		var supportRayOrigin = rested + Vector3.UnitY * (halfHeight + 10f);
		var hasSupport = RayCast(supportRayOrigin, -Vector3.UnitY, halfHeight + 100f, false, out var support, bodyHandle);
		var supportY = hasSupport ? support.Position.Y : float.NaN;
		var aboveSupport = rested.Y - supportY;

		// A free capsule may end up on its side; the distance from its center to the ground is
		// then the radius rather than half the capsule height. Either way it must be off the
		// surface and slow.
		var rests = hasSupport && aboveSupport >= CapsuleRadius - 3f &&
		            aboveSupport <= halfHeight + 3f && speed < 5f;

		Log.Info(
			$"Physics test: capsule dropped at ({start.X:0.#}, {start.Y:0.#}, {start.Z:0.#}) and " +
			$"rested at ({rested.X:0.#}, {rested.Y:0.#}, {rested.Z:0.#}) on " +
			$"{(hasSupport ? support.Kind.ToString() : "nothing")} at y={supportY:0.#}; " +
			$"center {aboveSupport:0.##} above it (radius {CapsuleRadius:0.#}, upright {halfHeight:0.#}), " +
			$"speed {speed:0.##} → {(rests ? "rests" : "FAILED")}.");

		// Find a wall around the resting point, preferring a static vob over sloped terrain.
		PhysicsRayHit wall = default;
		var wallDistance = float.MaxValue;
		var wallDirection = Vector3.Zero;
		var fallback = default(PhysicsRayHit);
		var fallbackDistance = float.MaxValue;
		var fallbackDirection = Vector3.Zero;

		for (var i = 0; i < 16; ++i)
		{
			var angle = MathF.Tau * i / 16f;
			var direction = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
			if (!RayCast(rested, direction, 250f, false, out var hit, bodyHandle))
			{
				continue;
			}

			if (hit.Distance < CapsuleRadius + 5f)
			{
				continue;
			}

			if (hit.Distance < CapsuleRadius + 5f)
			{
				continue;
			}

			if (hit.Kind == PhysicsSurfaceKind.Vob && hit.Distance < wallDistance)
			{
				wall = hit;
				wallDistance = hit.Distance;
				wallDirection = direction;
			}
			else if (hit.Distance < fallbackDistance)
			{
				fallback = hit;
				fallbackDistance = hit.Distance;
				fallbackDirection = direction;
			}
		}

		if (wallDistance == float.MaxValue && fallbackDistance < float.MaxValue)
		{
			wall = fallback;
			wallDistance = fallbackDistance;
			wallDirection = fallbackDirection;
		}

		if (wallDistance == float.MaxValue)
		{
			Log.Warning("Physics test: no wall within 250 units; the wall check was skipped.");
			return;
		}

		// Stand the capsule in front of the wall and drive it into the surface. The direction
		// from the wall to the resting point is the outward normal, whatever the triangle's
		// winding is.
		var outward = rested - wall.Position;
		outward.Y = 0f;
		if (outward.LengthSquared() < 1e-4f)
		{
			outward = -wallDirection;
		}

		outward.Normalize();
		var stand = wall.Position + outward * (CapsuleRadius + 10f);
		stand.Y = rested.Y;

		body = Simulation.Bodies[bodyHandle];
		body.Pose.Position = ToNumerics(stand);
		body.Velocity.Linear = ToNumerics(-outward * 300f);
		body.Awake = true;
		body.UpdateBounds();

		for (var i = 0; i < 60; ++i)
		{
			Simulation.Timestep(StepSeconds);
		}

		body = Simulation.Bodies[bodyHandle];
		var final = ToXna(body.Pose.Position);
		var separation = Vector3.Dot(final - wall.Position, outward);
		var blocked = separation >= CapsuleRadius - 2f;

		Log.Info(
			$"Physics test: pushed into a {wall.Kind} wall {wallDistance:0.#} units away at " +
			$"{wall.Position}; final center {separation:0.#} from the surface " +
			$"(capsule radius {CapsuleRadius:0.#}) → {(blocked ? "blocked" : "PASSED THROUGH")}.");
	}

	private static Num.Vector3 ToNumerics(Vector3 value) => new(value.X, value.Y, value.Z);

	private static Vector3 ToXna(Num.Vector3 value) => new(value.X, value.Y, value.Z);

	public void Dispose()
	{
		Simulation.Dispose();
		Surfaces.Dispose();
		_bufferPool.Clear();
	}

	private struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
	{
		public CollidableProperty<PhysicsSurface> Surfaces;

		public NarrowPhaseCallbacks(CollidableProperty<PhysicsSurface> surfaces) : this()
		{
			Surfaces = surfaces;
		}

		public void Initialize(Simulation simulation) => Surfaces.Initialize(simulation);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
		{
			// A sensor reports overlaps but must not push anything; water is the only one so
			// far, and it is handled as a non-contact surface.
			if (Surfaces[a].Kind == PhysicsSurfaceKind.Sensor || Surfaces[b].Kind == PhysicsSurfaceKind.Sensor)
			{
				return false;
			}

			return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold,
			out PairMaterialProperties pairMaterial)
			where TManifold : unmanaged, IContactManifold<TManifold>
		{
			var frictionA = MathF.Max(Surfaces[pair.A].Friction, 0f);
			var frictionB = MathF.Max(Surfaces[pair.B].Friction, 0f);

			pairMaterial.FrictionCoefficient = MathF.Sqrt(frictionA * frictionB);
			pairMaterial.MaximumRecoveryVelocity = 2f;
			pairMaterial.SpringSettings = new SpringSettings(30f, 1f);
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB,
			ref ConvexContactManifold manifold) => true;

		public void Dispose()
		{
		}
	}

	private struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
	{
		public Num.Vector3 Gravity;

		private Vector3Wide _gravityWideDt;

		public PoseIntegratorCallbacks(Num.Vector3 gravity) : this()
		{
			Gravity = gravity;
		}

		public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
		public readonly bool AllowSubstepsForUnconstrainedBodies => false;
		public readonly bool IntegrateVelocityForKinematics => false;

		public void Initialize(Simulation simulation)
		{
		}

		public void PrepareForIntegration(float dt) => _gravityWideDt = Vector3Wide.Broadcast(Gravity * dt);

		public void IntegrateVelocity(Num.Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation,
			BodyInertiaWide localInertia, Num.Vector<int> integrationMask, int workerIndex, Num.Vector<float> dt,
			ref BodyVelocityWide velocity)
		{
			velocity.Linear += _gravityWideDt;
		}
	}

	private struct RayHitHandler : IRayHitHandler
	{
		public CollidableProperty<PhysicsSurface> Surfaces;
		public bool IncludeSensors;
		public bool HasBodyToIgnore;
		public BodyHandle IgnoreBody;

		public bool HasHit;
		public float Distance;
		public Num.Vector3 Normal;
		public CollidableReference Collidable;

		public bool AllowTest(CollidableReference collidable)
		{
			if (!IncludeSensors && Surfaces[collidable].Kind == PhysicsSurfaceKind.Sensor)
			{
				return false;
			}

			if (HasBodyToIgnore &&
			    collidable.Mobility is CollidableMobility.Dynamic or CollidableMobility.Kinematic &&
			    collidable.BodyHandle == IgnoreBody)
			{
				return false;
			}

			return true;
		}

		public bool AllowTest(CollidableReference collidable, int childIndex) => AllowTest(collidable);

		public void OnRayHit(in RayData ray, ref float maximumT, float t, in Num.Vector3 normal,
			CollidableReference collidable, int childIndex)
		{
			HasHit = true;
			Distance = t;
			Normal = normal;
			Collidable = collidable;
			maximumT = t;
		}
	}
}
