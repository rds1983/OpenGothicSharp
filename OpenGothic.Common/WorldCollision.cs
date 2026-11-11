using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace OpenGothic;

/// <summary>
/// CPU collision built from the world's static polygons and static vob meshes.
/// It answers ground queries (highest surface under a point) and pushes a vertical
/// cylinder out of walls and object sides.
/// </summary>
public sealed class WorldCollision
{
	private const int Resolution = 256;

	/// <summary>Triangles at least this flat are walkable and left to the gravity code.</summary>
	private const float WalkableNormalY = 0.5f;

	private const int ResolveIterations = 3;

	private readonly struct Triangle
	{
		public readonly Vector3 A;
		public readonly Vector3 B;
		public readonly Vector3 C;
		public readonly Vector3 Normal;
		public readonly float MinY;
		public readonly float MaxY;

		public Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
		{
			A = a;
			B = b;
			C = c;
			Normal = normal;
			MinY = MathF.Min(a.Y, MathF.Min(b.Y, c.Y));
			MaxY = MathF.Max(a.Y, MathF.Max(b.Y, c.Y));
		}
	}

	private readonly BoundingBox _bounds;
	private readonly float _cellWidth;
	private readonly float _cellDepth;
	private readonly List<Triangle>[] _cells = new List<Triangle>[Resolution * Resolution];

	public WorldCollision(BoundingBox bounds)
	{
		_bounds = bounds;
		_cellWidth = MathF.Max((bounds.Max.X - bounds.Min.X) / Resolution, 1.0f);
		_cellDepth = MathF.Max((bounds.Max.Z - bounds.Min.Z) / Resolution, 1.0f);
	}

	public void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
	{
		var normal = Vector3.Cross(b - a, c - a);
		var length = normal.Length();
		if (length < 1e-4f)
		{
			return;
		}

		normal /= length;

		var minX = MathF.Min(a.X, MathF.Min(b.X, c.X));
		var maxX = MathF.Max(a.X, MathF.Max(b.X, c.X));
		var minZ = MathF.Min(a.Z, MathF.Min(b.Z, c.Z));
		var maxZ = MathF.Max(a.Z, MathF.Max(b.Z, c.Z));

		var x0 = CellIndex(minX, _bounds.Min.X, _cellWidth);
		var x1 = CellIndex(maxX, _bounds.Min.X, _cellWidth);
		var z0 = CellIndex(minZ, _bounds.Min.Z, _cellDepth);
		var z1 = CellIndex(maxZ, _bounds.Min.Z, _cellDepth);

		var triangle = new Triangle(a, b, c, normal);
		for (var z = z0; z <= z1; ++z)
		{
			for (var x = x0; x <= x1; ++x)
			{
				var index = z * Resolution + x;
				(_cells[index] ??= new List<Triangle>()).Add(triangle);
			}
		}
	}

	/// <summary>Highest surface at (x, z) that is not above <paramref name="maxY"/>.</summary>
	public bool TryGetGround(float x, float z, float maxY, out float groundY)
	{
		groundY = float.MinValue;

		var cell = CellAt(x, z);
		if (cell == null)
		{
			return false;
		}

		var found = false;
		foreach (var triangle in cell)
		{
			if (!TryIntersectVerticalRay(triangle, x, z, maxY, out var y))
			{
				continue;
			}

			if (y > groundY)
			{
				groundY = y;
				found = true;
			}
		}

		return found;
	}

	/// <summary>
	/// Pushes <paramref name="position"/> (the centre of a vertical cylinder) out of steep
	/// triangles overlapping its height span. Flat surfaces are ignored because gravity
	/// keeps the cylinder standing on them, and anything shorter than a step is walked over.
	/// </summary>
	public void ResolveWalls(ref Vector3 position, float radius, float bottomY, float topY, float stepHeight)
	{
		var point = new Vector2(position.X, position.Z);

		for (var iteration = 0; iteration < ResolveIterations; ++iteration)
		{
			if (!PushOut(ref point, radius, bottomY, topY, stepHeight))
			{
				break;
			}
		}

		position.X = point.X;
		position.Z = point.Y;
	}

	private bool PushOut(ref Vector2 point, float radius, float bottomY, float topY, float stepHeight)
	{
		var minX = CellIndex(point.X - radius, _bounds.Min.X, _cellWidth);
		var maxX = CellIndex(point.X + radius, _bounds.Min.X, _cellWidth);
		var minZ = CellIndex(point.Y - radius, _bounds.Min.Z, _cellDepth);
		var maxZ = CellIndex(point.Y + radius, _bounds.Min.Z, _cellDepth);

		var pushed = false;
		for (var z = minZ; z <= maxZ; ++z)
		{
			for (var x = minX; x <= maxX; ++x)
			{
				var cell = _cells[z * Resolution + x];
				if (cell == null)
				{
					continue;
				}

				foreach (var triangle in cell)
				{
					if (triangle.Normal.Y > WalkableNormalY)
					{
						continue;
					}

					if (triangle.MaxY <= bottomY + stepHeight)
					{
						continue;
					}

					if (triangle.MinY >= topY)
					{
						continue;
					}

					if (PushOutOfTriangle(ref point, radius, triangle))
					{
						pushed = true;
					}
				}
			}
		}

		return pushed;
	}

	private static bool PushOutOfTriangle(ref Vector2 point, float radius, in Triangle triangle)
	{
		var a = new Vector2(triangle.A.X, triangle.A.Z);
		var b = new Vector2(triangle.B.X, triangle.B.Z);
		var c = new Vector2(triangle.C.X, triangle.C.Z);

		var area = Cross(b - a, c - a);
		var inside = MathF.Abs(area) > 1e-6f && PointInTriangle(point, a, b, c);

		if (inside)
		{
			// The cylinder centre projects inside the triangle: leave through the nearest edge.
			var centroid = (a + b + c) / 3.0f;
			var bestDistance = float.MaxValue;
			var bestClosest = point;

			AccumulateEdge(point, a, b, ref bestDistance, ref bestClosest);
			AccumulateEdge(point, b, c, ref bestDistance, ref bestClosest);
			AccumulateEdge(point, c, a, ref bestDistance, ref bestClosest);

			var outward = bestClosest - centroid;
			if (outward.LengthSquared() < 1e-6f)
			{
				outward = point - centroid;
			}

			outward.Normalize();
			point += outward * (bestDistance + radius);
			return true;
		}

		var closest = ClosestPointOnSegment(point, a, b);
		var distanceSquared = Vector2.DistanceSquared(point, closest);

		var onBC = ClosestPointOnSegment(point, b, c);
		var bcDistanceSquared = Vector2.DistanceSquared(point, onBC);
		if (bcDistanceSquared < distanceSquared)
		{
			closest = onBC;
			distanceSquared = bcDistanceSquared;
		}

		var onCA = ClosestPointOnSegment(point, c, a);
		var caDistanceSquared = Vector2.DistanceSquared(point, onCA);
		if (caDistanceSquared < distanceSquared)
		{
			closest = onCA;
			distanceSquared = caDistanceSquared;
		}

		var distance = MathF.Sqrt(distanceSquared);
		if (distance >= radius)
		{
			return false;
		}

		Vector2 direction;
		if (distance > 1e-4f)
		{
			direction = (point - closest) / distance;
		}
		else
		{
			// Centre sits exactly on the wall: push towards the side the face normal points at.
			direction = new Vector2(triangle.Normal.X, triangle.Normal.Z);
			if (direction.LengthSquared() < 1e-6f)
			{
				direction = new Vector2(1.0f, 0.0f);
			}
			else
			{
				direction.Normalize();
			}
		}

		point += direction * (radius - distance);
		return true;
	}

	private static void AccumulateEdge(Vector2 point, Vector2 a, Vector2 b, ref float bestDistance, ref Vector2 bestClosest)
	{
		var closest = ClosestPointOnSegment(point, a, b);
		var distance = Vector2.Distance(point, closest);
		if (distance >= bestDistance)
		{
			return;
		}

		bestDistance = distance;
		bestClosest = closest;
	}

	private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
	{
		var d1 = Cross(b - a, p - a);
		var d2 = Cross(c - b, p - b);
		var d3 = Cross(a - c, p - c);

		var hasNegative = d1 < 0.0f || d2 < 0.0f || d3 < 0.0f;
		var hasPositive = d1 > 0.0f || d2 > 0.0f || d3 > 0.0f;
		return !(hasNegative && hasPositive);
	}

	private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
	{
		var edge = b - a;
		var lengthSquared = edge.LengthSquared();
		if (lengthSquared < 1e-6f)
		{
			return a;
		}

		var t = Math.Clamp(Vector2.Dot(point - a, edge) / lengthSquared, 0.0f, 1.0f);
		return a + edge * t;
	}

	private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

	private List<Triangle> CellAt(float x, float z)
	{
		if (x < _bounds.Min.X || x > _bounds.Max.X || z < _bounds.Min.Z || z > _bounds.Max.Z)
		{
			return null;
		}

		var cx = CellIndex(x, _bounds.Min.X, _cellWidth);
		var cz = CellIndex(z, _bounds.Min.Z, _cellDepth);
		return _cells[cz * Resolution + cx];
	}

	private static int CellIndex(float value, float min, float size)
	{
		var index = (int)((value - min) / size);
		return Math.Clamp(index, 0, Resolution - 1);
	}

	private static bool TryIntersectVerticalRay(in Triangle triangle, float x, float z, float maxY, out float y)
	{
		y = 0.0f;

		// Barycentric coordinates in the XZ plane, then interpolate Y.
		var edge0X = triangle.B.X - triangle.A.X;
		var edge0Z = triangle.B.Z - triangle.A.Z;
		var edge1X = triangle.C.X - triangle.A.X;
		var edge1Z = triangle.C.Z - triangle.A.Z;
		var pointX = x - triangle.A.X;
		var pointZ = z - triangle.A.Z;

		var denominator = edge0X * edge1Z - edge1X * edge0Z;
		if (MathF.Abs(denominator) < 1e-6f)
		{
			return false;
		}

		var u = (pointX * edge1Z - edge1X * pointZ) / denominator;
		var v = (edge0X * pointZ - pointX * edge0Z) / denominator;
		if (u < 0.0f || v < 0.0f || u + v > 1.0f)
		{
			return false;
		}

		y = triangle.A.Y
			+ u * (triangle.B.Y - triangle.A.Y)
			+ v * (triangle.C.Y - triangle.A.Y);
		return y <= maxY;
	}
}
