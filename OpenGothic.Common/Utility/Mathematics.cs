using Microsoft.Xna.Framework;

namespace OpenGothic.Utility
{
	internal static class Mathematics
	{
		public static Vector2 ToXna(this System.Numerics.Vector2 v) => new Vector2(v.X, v.Y);

		public static Vector3 ToXna(this System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

		public static Matrix ToXna(this System.Numerics.Matrix4x4 m) => 
			new Matrix(m.M11, m.M12, m.M13, m.M14,
				m.M21, m.M22, m.M23, m.M24,
				m.M31, m.M32, m.M33, m.M34,
				m.M41, m.M42, m.M43, m.M44);
	}
}
