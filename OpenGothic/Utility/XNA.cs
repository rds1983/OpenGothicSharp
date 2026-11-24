using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Graphics.Vertices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGothic.Utility;

internal static class XNA
{
	public static IndexBuffer CreateIndexBuffer(this ICollection<ushort> indices, GraphicsDevice device)
	{
		var result = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Count, BufferUsage.WriteOnly);
		result.SetData(indices.ToArray());

		return result;
	}

	public static IndexBuffer CreateIndexBuffer(this int[] indices, GraphicsDevice device)
	{
		var result = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
		result.SetData(indices);

		return result;
	}

	public static VertexBuffer CreateVertexBuffer<T>(this ICollection<T> vertices, GraphicsDevice device) where T : struct, IVertexType
	{
		var data = vertices.ToArray();
		var result = new VertexBuffer(device, new T().VertexDeclaration, data.Length, BufferUsage.WriteOnly);
		result.SetData(data);

		return result;
	}

	public static SurfaceFormat ToXNA(this ZenKit.TextureFormat textureFormat)
	{
		switch (textureFormat)
		{
			case ZenKit.TextureFormat.Dxt1:
				return SurfaceFormat.Dxt1;

			case ZenKit.TextureFormat.Dxt3:
				return SurfaceFormat.Dxt3;

			case ZenKit.TextureFormat.Dxt5:
				return SurfaceFormat.Dxt5;
		}

		throw new NotSupportedException($"Format {textureFormat} is not supported.");
	}

	/// <summary>
	/// Whether the format can be uploaded to the GPU as-is. Formats that cannot are
	/// decoded to RGBA by ZenKit instead. See <see cref="ToXNA"/>.
	/// </summary>
	public static bool IsCompressed(this ZenKit.TextureFormat textureFormat) =>
		textureFormat == ZenKit.TextureFormat.Dxt1 ||
		textureFormat == ZenKit.TextureFormat.Dxt3 ||
		textureFormat == ZenKit.TextureFormat.Dxt5;

	public static Color ToXna(this System.Drawing.Color color) => new Color(color.R, color.G, color.B, color.A);

	public static IEnumerable<Vector3> GetPositions(this IEnumerable<VertexPositionNormalTexture> vertices) => (from v in vertices select v.Position);
	public static IEnumerable<Vector3> GetPositions(this IEnumerable<VertexSkinned> vertices) => (from v in vertices select v.Position);

	public static BoundingBox BuildBoundingBox(this IEnumerable<VertexPositionNormalTexture> vertices) => BoundingBox.CreateFromPoints(vertices.GetPositions());
	public static BoundingBox BuildBoundingBox(this IEnumerable<VertexSkinned> vertices) => BoundingBox.CreateFromPoints(vertices.GetPositions());
	public static BoundingBox BuildBoundingBox(this IEnumerable<Vector3> vertices) => BoundingBox.CreateFromPoints(vertices);
}
