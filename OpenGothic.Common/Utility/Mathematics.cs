using Microsoft.Xna.Framework;
using System;

namespace OpenGothic.Utility
{
	internal static class Mathematics
	{
		/// <summary>
		/// The value for which all absolute numbers smaller than are considered equal to zero.
		/// </summary>
		public const float ZeroTolerance = 1e-6f;

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

		public static Vector3 ToXna(this System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

		public static Matrix ToXna(this System.Numerics.Matrix4x4 m) =>
			new Matrix(m.M11, m.M12, m.M13, m.M14,
				m.M21, m.M22, m.M23, m.M24,
				m.M31, m.M32, m.M33, m.M34,
				m.M41, m.M42, m.M43, m.M44);
	}
}
