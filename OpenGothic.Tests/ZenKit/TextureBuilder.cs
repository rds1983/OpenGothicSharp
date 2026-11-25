using OpenGothic.ZenKit;
using System;
using System.IO;
using System.Text;

namespace OpenGothic.Tests.ZenKit;

/// <summary>Builds a ZTEX record in memory for the managed texture reader tests.</summary>
internal static class TextureBuilder
{
	public static byte[] Build(TextureFormat format, int width, int height, params byte[][] mipmaps) =>
		BuildCore(format, width, height, null, mipmaps);

	public static byte[] BuildPalettized(int width, int height, byte[] palette, params byte[][] mipmaps) =>
		BuildCore(TextureFormat.P8, width, height, palette, mipmaps);

	private static byte[] BuildCore(TextureFormat format, int width, int height, byte[] palette,
		byte[][] mipmaps)
	{
		if (mipmaps.Length == 0)
		{
			throw new ArgumentException("At least one mipmap is required.", nameof(mipmaps));
		}

		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream);

		writer.Write(Encoding.ASCII.GetBytes("ZTEX"));
		writer.Write(0u);
		writer.Write((uint)format);
		writer.Write((uint)width);
		writer.Write((uint)height);
		writer.Write((uint)mipmaps.Length);
		writer.Write((uint)width);
		writer.Write((uint)height);
		writer.Write(0u);

		if (format == TextureFormat.P8)
		{
			if (palette == null || palette.Length != 256 * 4)
			{
				throw new ArgumentException("P8 textures need a 256-entry BGRA palette.", nameof(palette));
			}

			writer.Write(palette);
		}

		// ZTEX stores the smallest mipmap first.
		for (var level = mipmaps.Length - 1; level >= 0; --level)
		{
			writer.Write(mipmaps[level]);
		}

		return stream.ToArray();
	}
}
