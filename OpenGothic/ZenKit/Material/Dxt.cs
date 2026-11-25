using System;

namespace OpenGothic.ZenKit;

/// <summary>
/// S3TC block decompression for the texture formats Gothic stores. Ported from the DXT
/// specification; ZenKit uses squish for the same conversion, and the output is plain RGBA.
/// </summary>
internal static class DxtDecoder
{
	public static void Decode(byte[] source, int width, int height, TextureFormat format, byte[] destination)
	{
		var dxt1 = format == TextureFormat.Dxt1;
		var blockSize = dxt1 ? 8 : 16;
		var blocksX = Math.Max(1, width / 4);
		var blocksY = Math.Max(1, height / 4);

		for (var blockY = 0; blockY < blocksY; ++blockY)
		{
			for (var blockX = 0; blockX < blocksX; ++blockX)
			{
				var offset = (blockY * blocksX + blockX) * blockSize;
				if (offset + blockSize > source.Length)
				{
					return;
				}

				var colorOffset = dxt1 ? offset : offset + 8;
				var colors = ReadColors(source, colorOffset, dxt1);
				var indices = source[colorOffset + 4] | (source[colorOffset + 5] << 8) |
				              (source[colorOffset + 6] << 16) | (source[colorOffset + 7] << 24);

				for (var pixelY = 0; pixelY < 4; ++pixelY)
				{
					var y = blockY * 4 + pixelY;
					if (y >= height)
					{
						continue;
					}

					for (var pixelX = 0; pixelX < 4; ++pixelX)
					{
						var x = blockX * 4 + pixelX;
						if (x >= width)
						{
							continue;
						}

						var pixel = pixelY * 4 + pixelX;
						var index = (indices >> (pixel * 2)) & 3;
						var color = colors[index];
						var alpha = color.Alpha;
						if (!dxt1 && format == TextureFormat.Dxt3)
						{
							var nibble = source[offset + pixel / 2];
							alpha = (byte)(((pixel & 1) == 0 ? nibble & 0x0F : nibble >> 4) * 17);
						}
						else if (!dxt1 && format == TextureFormat.Dxt5)
						{
							alpha = ReadDxt5Alpha(source, offset, pixel);
						}

						var destinationIndex = (y * width + x) * 4;
						destination[destinationIndex + 0] = color.Red;
						destination[destinationIndex + 1] = color.Green;
						destination[destinationIndex + 2] = color.Blue;
						destination[destinationIndex + 3] = alpha;
					}
				}
			}
		}
	}

	private static Rgba[] ReadColors(byte[] source, int offset, bool dxt1)
	{
		var first = source[offset] | (source[offset + 1] << 8);
		var second = source[offset + 2] | (source[offset + 3] << 8);

		var colors = new Rgba[4];
		colors[0] = Expand565(first);
		colors[1] = Expand565(second);

		if (!dxt1 || first > second)
		{
			colors[2] = new Rgba(
				(byte)((2 * colors[0].Red + colors[1].Red) / 3),
				(byte)((2 * colors[0].Green + colors[1].Green) / 3),
				(byte)((2 * colors[0].Blue + colors[1].Blue) / 3),
				255);
			colors[3] = new Rgba(
				(byte)((colors[0].Red + 2 * colors[1].Red) / 3),
				(byte)((colors[0].Green + 2 * colors[1].Green) / 3),
				(byte)((colors[0].Blue + 2 * colors[1].Blue) / 3),
				255);
		}
		else
		{
			colors[2] = new Rgba(
				(byte)((colors[0].Red + colors[1].Red) / 2),
				(byte)((colors[0].Green + colors[1].Green) / 2),
				(byte)((colors[0].Blue + colors[1].Blue) / 2),
				255);
			colors[3] = new Rgba(0, 0, 0, 0);
		}

		return colors;
	}

	private static Rgba Expand565(int value)
	{
		var red = (value >> 11) & 0x1F;
		var green = (value >> 5) & 0x3F;
		var blue = value & 0x1F;
		return new Rgba(
			(byte)((red << 3) | (red >> 2)),
			(byte)((green << 2) | (green >> 4)),
			(byte)((blue << 3) | (blue >> 2)),
			255);
	}

	private static byte ReadDxt5Alpha(byte[] source, int offset, int pixel)
	{
		var first = source[offset];
		var second = source[offset + 1];

		ulong bits = 0;
		for (var i = 0; i < 6; ++i)
		{
			bits |= (ulong)source[offset + 2 + i] << (8 * i);
		}

		var index = (int)((bits >> (pixel * 3)) & 7);

		if (first > second)
		{
			return index switch
			{
				0 => first,
				1 => second,
				_ => (byte)(((8 - index) * first + (index - 1) * second) / 7)
			};
		}

		return index switch
		{
			0 => first,
			1 => second,
			6 => 0,
			7 => 255,
			_ => (byte)(((6 - index) * first + (index - 1) * second) / 5)
		};
	}

	private readonly struct Rgba
	{
		public Rgba(byte red, byte green, byte blue, byte alpha)
		{
			Red = red;
			Green = green;
			Blue = blue;
			Alpha = alpha;
		}

		public byte Red { get; }

		public byte Green { get; }

		public byte Blue { get; }

		public byte Alpha { get; }
	}
}
