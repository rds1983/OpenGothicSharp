using Microsoft.Xna.Framework;

namespace OpenGothic.Utility
{
	internal static class Mathematics
	{
		public static Vector2 ToXna(this System.Numerics.Vector2 v) => new Vector2(v.X, v.Y);

		public static Vector3 ToXna(this System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
	}
}
