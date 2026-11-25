using Microsoft.Xna.Framework;
using OpenGothic.ZenKit;
using System;

namespace OpenGothic.Utility;

internal static class Mathematics
{
	/// <summary>
	/// The value for which all absolute numbers smaller than are considered equal to zero.
	/// </summary>
	public const float ZeroTolerance = 1e-6f;

	private static readonly Matrix LTR = new Matrix(
			1, 0, 0, 0,
			0, 1, 0, 0,
			0, 0, -1, 0,
			0, 0, 0, 1);

	/// <summary>
	/// Compares two floating point numbers based on an epsilon zero tolerance.
	/// </summary>
	/// <param name="left">The first number to compare.</param>
	/// <param name="right">The second number to compare.</param>
	/// <param name="epsilon">The epsilon value to use for zero tolerance.</param>
	/// <returns><c>true</c> if <paramref name="left"/> is within epsilon of <paramref name="right"/>; otherwise, <c>false</c>.</returns>
	public static bool EpsilonEquals(this float left, float right, float epsilon = ZeroTolerance)
	{
		return Math.Abs(left - right) <= epsilon;
	}

	public static bool EpsilonEquals(this Vector2 a, Vector2 b, float epsilon = ZeroTolerance)
	{
		return a.X.EpsilonEquals(b.X, epsilon) &&
			a.Y.EpsilonEquals(b.Y, epsilon);
	}

	public static bool EpsilonEquals(this Vector3 a, Vector3 b, float epsilon = ZeroTolerance)
	{
		return a.X.EpsilonEquals(b.X, epsilon) &&
			a.Y.EpsilonEquals(b.Y, epsilon) &&
			a.Z.EpsilonEquals(b.Z, epsilon);
	}

	public static bool EpsilonEquals(this Vector4 a, Vector4 b, float epsilon = ZeroTolerance)
	{
		return a.X.EpsilonEquals(b.X, epsilon) &&
			a.Y.EpsilonEquals(b.Y, epsilon) &&
			a.Z.EpsilonEquals(b.Z, epsilon) &&
			a.W.EpsilonEquals(b.W, epsilon);
	}

	public static bool EpsilonEquals(this Quaternion a, Quaternion b, float epsilon = ZeroTolerance)
	{
		return a.X.EpsilonEquals(b.X, epsilon) &&
			a.Y.EpsilonEquals(b.Y, epsilon) &&
			a.Z.EpsilonEquals(b.Z, epsilon) &&
			a.W.EpsilonEquals(b.W, epsilon);
	}

	public static bool IsZero(this float a, float epsilon = ZeroTolerance)
	{
		return a.EpsilonEquals(0.0f, epsilon);
	}

	public static bool IsZero(this Vector2 a, float epsilon = ZeroTolerance)
	{
		return a.EpsilonEquals(Vector2.Zero, epsilon);
	}

	public static bool IsZero(this Vector3 a, float epsilon = ZeroTolerance)
	{
		return a.EpsilonEquals(Vector3.Zero, epsilon);
	}

	public static bool IsZero(this Vector4 a, float epsilon = ZeroTolerance)
	{
		return a.EpsilonEquals(Vector4.Zero, epsilon);
	}

	public static Vector2 ToXna(this System.Numerics.Vector2 v) => new Vector2(v.X, v.Y);

	/// <summary>
	/// Converts Gothic's left-handed game data to MonoGame's right-handed space by mirroring Z.
	/// This is the single loading-time conversion point: everything past asset loading is RHS.
	/// </summary>
	/// <param name="v"></param>
	/// <returns></returns>
	public static Vector3 ToXna(this System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, -v.Z);

	/// <summary>
	/// Converts Gothic's left-handed quaternion to right-handed. See <see cref="ToXna(System.Numerics.Vector3)"/>.
	/// </summary>
	public static Quaternion ToXna(this System.Numerics.Quaternion v)
	{
		var result = new Quaternion(v.X, v.Y, -v.Z, v.W);

		return result;
	}

	/// <summary>
	/// Converts Gothic's left-handed matrix to right-handed. See <see cref="ToXna(System.Numerics.Vector3)"/>.
	/// </summary>
	public static Matrix ToXna(this System.Numerics.Matrix4x4 m)
	{
		var result = new Matrix(m.M11, m.M12, m.M13, m.M14,
			m.M21, m.M22, m.M23, m.M24,
			m.M31, m.M32, m.M33, m.M34,
			m.M41, m.M42, m.M43, m.M44);

		result = Matrix.Transpose(result);

		return LTR * result * LTR;
	}

	public static Matrix ToXna(this Matrix3x3 m)
	{
		var result = new Matrix(m.M11, m.M12, m.M13, 0,
			m.M21, m.M22, m.M23, 0,
			m.M31, m.M32, m.M33, 0,
			0, 0, 0, 1);

		// ZenKit stores the vob rotation as columns; XNA transforms row vectors. Transpose,
		// then mirror Z like every other position conversion.
		result = Matrix.Transpose(result);
		return LTR * result * LTR;
	}

	public static int Clamp(this int value, int min, int max)
	{
		if (value < min)
		{
			value = min;
		}

		if (value > max)
		{
			value = max;
		}

		return value;
	}

	public static Vector3 CalculateCenter(this BoundingBox source)
	{
		return new Vector3(
			source.Min.X + (source.Max.X - source.Min.X) / 2,
			source.Min.Y + (source.Max.Y - source.Min.Y) / 2,
			source.Min.Z + (source.Max.Z - source.Min.Z) / 2);
	}

	/// <summary>
	/// Bounding box of this box transformed by <paramref name="matrix"/>. For an affine
	/// matrix this equals transforming all eight corners and taking their extents, but
	/// without materialising the corners: the centre moves as a point and each extent
	/// component scales by the summed absolute basis rows.
	/// </summary>
	public static BoundingBox Transform(this BoundingBox source, ref Matrix matrix)
	{
		var center = (source.Min + source.Max) * 0.5f;
		var extent = (source.Max - source.Min) * 0.5f;

		var newCenter = Vector3.Transform(center, matrix);
		var newExtent = new Vector3(
			Math.Abs(matrix.M11) * extent.X + Math.Abs(matrix.M21) * extent.Y + Math.Abs(matrix.M31) * extent.Z,
			Math.Abs(matrix.M12) * extent.X + Math.Abs(matrix.M22) * extent.Y + Math.Abs(matrix.M32) * extent.Z,
			Math.Abs(matrix.M13) * extent.X + Math.Abs(matrix.M23) * extent.Y + Math.Abs(matrix.M33) * extent.Z);

		return new BoundingBox(newCenter - newExtent, newCenter + newExtent);
	}
}
