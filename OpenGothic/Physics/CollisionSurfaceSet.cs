using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace OpenGothic.Physics;

/// <summary>
/// One group of static triangles that share a friction (and whether they are a sensor).
/// Vertices are in world space, three consecutive corners per triangle.
/// </summary>
public sealed class CollisionSurface
{
	public float Friction { get; }

	/// <summary>
	/// Whether the surface only reports overlaps, like water: a sensor never pushes a body.
	/// </summary>
	public bool Sensor { get; }

	public List<Vector3> Vertices { get; } = new();

	public int TriangleCount => Vertices.Count / 3;

	internal CollisionSurface(float friction, bool sensor)
	{
		Friction = friction;
		Sensor = sensor;
	}
}

/// <summary>
/// Static triangles of a level, grouped by the friction they present. Terrain is added while
/// the level is read; vobs are added when the physics world is built.
/// </summary>
public sealed class CollisionSurfaceSet
{
	private readonly Dictionary<(float Friction, bool Sensor), CollisionSurface> _byKey = new();
	private readonly List<CollisionSurface> _surfaces = new();

	public IReadOnlyList<CollisionSurface> Surfaces => _surfaces;

	public int TriangleCount { get; private set; }

	/// <summary>
	/// Gets or creates the group for a friction and a sensor flag.
	/// </summary>
	public CollisionSurface Get(float friction, bool sensor)
	{
		var key = (friction, sensor);
		if (!_byKey.TryGetValue(key, out var surface))
		{
			surface = new CollisionSurface(friction, sensor);
			_byKey[key] = surface;
			_surfaces.Add(surface);
		}

		return surface;
	}

	/// <summary>Adds a triangle soup — three corners per triangle — in another space.</summary>
	public void AddTriangles(float friction, bool sensor, IReadOnlyList<Vector3> vertices, Matrix transform)
	{
		var surface = Get(friction, sensor);
		for (var i = 0; i + 2 < vertices.Count; i += 3)
		{
			surface.Vertices.Add(Vector3.Transform(vertices[i], transform));
			surface.Vertices.Add(Vector3.Transform(vertices[i + 1], transform));
			surface.Vertices.Add(Vector3.Transform(vertices[i + 2], transform));
			++TriangleCount;
		}
	}

	public void AddTriangle(float friction, bool sensor, Vector3 a, Vector3 b, Vector3 c)
	{
		var surface = Get(friction, sensor);
		surface.Vertices.Add(a);
		surface.Vertices.Add(b);
		surface.Vertices.Add(c);
		++TriangleCount;
	}
}
