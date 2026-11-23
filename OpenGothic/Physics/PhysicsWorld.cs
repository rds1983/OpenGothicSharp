using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuPhysics.Trees;
using BepuUtilities;
using BepuUtilities.Memory;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using OpenGothic.Scene;
using OpenGothic.Utility;
using System;
using System.Runtime.CompilerServices;
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

	private readonly BufferPool _bufferPool = new();
	private readonly CollisionSurfaceSet _vobSurfaces = new();

	private int _staticCount;

	public Simulation Simulation { get; }

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
	/// Adds the level's own triangles and every static vob mesh in the scene. A vob is
	/// collidable only where its model has rigid geometry: a skinned mesh deforms with its
	/// skeleton and is left out, matching the reference.
	/// </summary>
	public void Build(WorldGrid world)
	{
		foreach (var surface in world.StaticCollision.Surfaces)
		{
			AddSurface(surface, PhysicsSurfaceKind.Terrain);
			TerrainTriangles += surface.TriangleCount;
		}

		foreach (var cell in world.Cells)
		{
			foreach (var node in cell.Children)
			{
				AddSceneNode(node);
			}
		}

		foreach (var surface in _vobSurfaces.Surfaces)
		{
			AddSurface(surface, PhysicsSurfaceKind.Vob);
			VobTriangles += surface.TriangleCount;
		}

		Log.Info(
			$"Physics: {TerrainTriangles:N0} terrain triangles, {VobTriangles:N0} vob triangles, " +
			$"{_staticCount} static bodies.");
	}

	private void AddSceneNode(SceneNode node)
	{
		switch (node)
		{
			case MeshNode mesh:
				AddPart(mesh.Mesh, mesh.WorldTransform);
				break;

			case InstancedMeshNode instanced:
				foreach (var transform in instanced.InstancesTransforms)
				{
					AddPart(instanced.Mesh, transform);
				}

				break;

			case ModelNode model:
				var instance = model.Instance;
				var modelWorld = model.WorldTransform;

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
						AddPart(part, transform);
					}
				}

				break;
		}
	}

	private void AddPart(DrMeshPart part, Matrix transform)
	{
		if (part.Tag is not DrMeshPartTag tag || tag.Collision == null || tag.Friction <= 0f)
		{
			return;
		}

		_vobSurfaces.AddTriangles(tag.Friction, tag.Sensor, tag.Collision.Vertices, transform);
	}

	private void AddSurface(CollisionSurface surface, PhysicsSurfaceKind kind)
	{
		var vertices = surface.Vertices;

		// Count first, so the buffer holds exactly the triangles that are stored. Meshes
		// cannot have trailing unused entries: they would become degenerate triangles at the
		// origin.
		var count = 0;
		for (var i = 0; i + 2 < vertices.Count; i += 3)
		{
			if (!IsDegenerate(vertices[i], vertices[i + 1], vertices[i + 2]))
			{
				// Bepu triangle meshes are one-sided: only the winding that faces the body
				// generates contacts and ray hits. Gothic's meshes have no consistent winding
				// (the renderer draws without back-face culling), so each triangle is added in
				// both directions to make the surface solid from either side.
				count += 2;
			}
		}

		if (count == 0)
		{
			return;
		}

		_bufferPool.Take<Triangle>(count, out var triangles);

		var target = 0;
		for (var i = 0; i + 2 < vertices.Count; i += 3)
		{
			var a = vertices[i];
			var b = vertices[i + 1];
			var c = vertices[i + 2];
			if (IsDegenerate(a, b, c))
			{
				continue;
			}

			triangles[target++] = new Triangle(ToNumerics(a), ToNumerics(b), ToNumerics(c));
			triangles[target++] = new Triangle(ToNumerics(a), ToNumerics(c), ToNumerics(b));
		}

		var mesh = new Mesh(triangles, Num.Vector3.One, _bufferPool);
		var shape = Simulation.Shapes.Add(mesh);
		var handle = Simulation.Statics.Add(new StaticDescription(new Num.Vector3(), Num.Quaternion.Identity, shape));

		Surfaces.Allocate(handle) = new PhysicsSurface
		{
			Friction = surface.Friction,
			Kind = surface.Sensor ? PhysicsSurfaceKind.Sensor : kind
		};

		++_staticCount;
	}

	private static bool IsDegenerate(Vector3 a, Vector3 b, Vector3 c)
	{
		var normal = Vector3.Cross(b - a, c - a);
		return normal.LengthSquared() < 1e-6f;
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

			if (HasBodyToIgnore && collidable.Mobility == CollidableMobility.Dynamic &&
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
