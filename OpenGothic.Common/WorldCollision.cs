using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace OpenGothic;

/// <summary>
/// CPU collision built from the world's static polygons. For now it only answers ground
/// queries: find the highest surface under a point, below a given height.
/// </summary>
public sealed class WorldCollision
{
	private const int Resolution = 256;

	private readonly struct Triangle
	{
		public readonly Vector3 A;
		public readonly Vector3 B;
		public readonly Vector3 C;

		public Triangle(Vector3 a, Vector3 b, Vector3 c)
		{
			A = a;
			B = b;
			C = c;
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
		// Vertical (or degenerate) triangles project to a line and can never be walked on.
		var area = (b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z);
		if (MathF.Abs(area) < 1e-4f)
		{
			return;
		}

		var minX = MathF.Min(a.X, MathF.Min(b.X, c.X));
		var maxX = MathF.Max(a.X, MathF.Max(b.X, c.X));
		var minZ = MathF.Min(a.Z, MathF.Min(b.Z, c.Z));
		var maxZ = MathF.Max(a.Z, MathF.Max(b.Z, c.Z));

		var x0 = CellIndex(minX, _bounds.Min.X, _cellWidth);
		var x1 = CellIndex(maxX, _bounds.Min.X, _cellWidth);
		var z0 = CellIndex(minZ, _bounds.Min.Z, _cellDepth);
		var z1 = CellIndex(maxZ, _bounds.Min.Z, _cellDepth);

		var triangle = new Triangle(a, b, c);
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
