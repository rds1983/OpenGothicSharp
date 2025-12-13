using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace OpenGothic.Utility
{
	internal static class XNA
	{
		public static IndexBuffer CreateIndexBuffer(this ushort[] indices, GraphicsDevice device)
		{
			var result = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
			result.SetData(indices);

			return result;
		}

		public static IndexBuffer CreateIndexBuffer(this int[] indices, GraphicsDevice device)
		{
			var result = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
			result.SetData(indices);

			return result;
		}

		public static VertexBuffer CreateVertexBuffer<T>(this T[] vertices, GraphicsDevice device) where T : struct, IVertexType
		{
			var result = new VertexBuffer(device, new T().VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
			result.SetData(vertices);

			return result;
		}

		public static SurfaceFormat ToXNA(this ZenKit.TextureFormat textureFormat)
		{
			switch(textureFormat)
			{
				case ZenKit.TextureFormat.Dxt1:
					return SurfaceFormat.Dxt1;

				case ZenKit.TextureFormat.Dxt3:
					return SurfaceFormat.Dxt3;
			}

			throw new NotSupportedException($"Format {textureFormat} is not supported.");
		}

		public static Color ToXna(this System.Drawing.Color color) => new Color(color.R, color.G, color.B, color.A);
	}
}
