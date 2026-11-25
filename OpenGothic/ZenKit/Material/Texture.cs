using System;
using System.Collections.Generic;

namespace OpenGothic.ZenKit;

/// <summary>Texture formats used by the ZenGin; the values match the ZTEX header.</summary>
public enum TextureFormat : uint
{
	B8G8R8A8 = 0x0,
	R8G8B8A8 = 0x1,
	A8B8G8R8 = 0x2,
	A8R8G8B8 = 0x3,
	B8G8R8 = 0x4,
	R8G8B8 = 0x5,
	A4R4G4B4 = 0x6,
	A1R5G5B5 = 0x7,
	R5G6B5 = 0x8,
	P8 = 0x9,
	Dxt1 = 0xA,
	Dxt2 = 0xB,
	Dxt3 = 0xC,
	Dxt4 = 0xD,
	Dxt5 = 0xE
}

/// <summary>A palettized texture entry, in the order used by the ZTEX palette.</summary>
public readonly struct ArgbColor
{
	public ArgbColor(byte a, byte r, byte g, byte b)
	{
		A = a;
		R = r;
		G = g;
		B = b;
	}

	public byte A { get; }

	public byte R { get; }

	public byte G { get; }

	public byte B { get; }
}

public sealed class TextureFormatException : Exception
{
	public TextureFormatException(string message) : base(message)
	{
	}
}

/// <summary>
/// Managed port of ZenKit's <c>Texture</c>: reads a ZTEX record and exposes its mipmaps
/// either in their stored format or decoded to RGBA8.
/// </summary>
public sealed class Texture
{
	private const string ZtexSignature = "ZTEX";
	private const int PaletteEntries = 256;

	private readonly List<byte[]> _mipmaps = new List<byte[]>();
	private ArgbColor[] _palette;

	public Texture()
	{
	}

	public Texture(Read reader)
	{
		Load(reader);
	}

	public TextureFormat Format { get; private set; }

	public int Width { get; private set; }

	public int Height { get; private set; }

	public int ReferenceWidth { get; private set; }

	public int ReferenceHeight { get; private set; }

	public int MipmapCount { get; private set; }

	public uint AverageColor { get; private set; }

	public IReadOnlyList<ArgbColor> Palette => _palette ?? Array.Empty<ArgbColor>();

	public void Load(Read reader)
	{
		if (reader == null)
		{
			throw new ArgumentNullException(nameof(reader));
		}

		if (reader.ReadString(4) != ZtexSignature)
		{
			throw new TextureFormatException("Texture: invalid signature.");
		}

		if (reader.ReadUInt() != 0)
		{
			throw new TextureFormatException("Texture: unsupported version.");
		}

		Format = (TextureFormat)reader.ReadUInt();
		Width = ReadDimension(reader);
		Height = ReadDimension(reader);
		MipmapCount = Math.Max(1, ReadDimension(reader));
		ReferenceWidth = ReadDimension(reader);
		ReferenceHeight = ReadDimension(reader);
		AverageColor = reader.ReadUInt();

		_mipmaps.Clear();
		if (Format == TextureFormat.P8)
		{
			_palette = new ArgbColor[PaletteEntries];
			for (var i = 0; i < _palette.Length; ++i)
			{
				var b = reader.ReadUByte();
				var g = reader.ReadUByte();
				var r = reader.ReadUByte();
				var a = reader.ReadUByte();
				_palette[i] = new ArgbColor(a, r, g, b);
			}
		}
		else
		{
			_palette = null;
		}

		// ZTEX stores the smallest mipmap first; the last entry is level zero.
		for (var level = MipmapCount - 1; level >= 0; --level)
		{
			_mipmaps.Add(reader.ReadBytes(MipmapSize(Format, Width, Height, level)));
		}
	}

	public int GetWidth(int level) => Width >> level;

	public int GetHeight(int level) => Height >> level;

	/// <summary>Returns the mipmap bytes exactly as stored, level zero first.</summary>
	public byte[] GetMipmapRaw(int level) => _mipmaps[MipmapCount - 1 - level];

	/// <summary>Decodes a mipmap to RGBA8.</summary>
	public byte[] GetMipmapRgba(int level)
	{
		var width = GetWidth(level);
		var height = GetHeight(level);
		var source = GetMipmapRaw(level);
		var result = new byte[width * height * 4];

		switch (Format)
		{
			case TextureFormat.Dxt1:
			case TextureFormat.Dxt3:
			case TextureFormat.Dxt5:
				DxtDecoder.Decode(source, width, height, Format, result);
				break;

			case TextureFormat.B8G8R8A8:
				for (var i = 0; i < width * height; ++i)
				{
					result[i * 4 + 0] = source[i * 4 + 2];
					result[i * 4 + 1] = source[i * 4 + 1];
					result[i * 4 + 2] = source[i * 4 + 0];
					result[i * 4 + 3] = source[i * 4 + 3];
				}

				break;

			case TextureFormat.R8G8B8A8:
				Array.Copy(source, result, result.Length);
				break;

			case TextureFormat.A8B8G8R8:
				for (var i = 0; i < width * height; ++i)
				{
					result[i * 4 + 0] = source[i * 4 + 3];
					result[i * 4 + 1] = source[i * 4 + 2];
					result[i * 4 + 2] = source[i * 4 + 1];
					result[i * 4 + 3] = source[i * 4 + 0];
				}

				break;

			case TextureFormat.A8R8G8B8:
				for (var i = 0; i < width * height; ++i)
				{
					result[i * 4 + 0] = source[i * 4 + 1];
					result[i * 4 + 1] = source[i * 4 + 2];
					result[i * 4 + 2] = source[i * 4 + 3];
					result[i * 4 + 3] = source[i * 4 + 0];
				}

				break;

			case TextureFormat.B8G8R8:
				for (var i = 0; i < width * height; ++i)
				{
					result[i * 4 + 0] = source[i * 3 + 2];
					result[i * 4 + 1] = source[i * 3 + 1];
					result[i * 4 + 2] = source[i * 3 + 0];
					result[i * 4 + 3] = 0xFF;
				}

				break;

			case TextureFormat.R8G8B8:
				for (var i = 0; i < width * height; ++i)
				{
					result[i * 4 + 0] = source[i * 3 + 0];
					result[i * 4 + 1] = source[i * 3 + 1];
					result[i * 4 + 2] = source[i * 3 + 2];
					result[i * 4 + 3] = 0xFF;
				}

				break;

			case TextureFormat.R5G6B5:
				for (var i = 0; i < width * height; ++i)
				{
					var value = source[i * 2] | (source[i * 2 + 1] << 8);
					var r = (value >> 11) & 0x1F;
					var g = (value >> 5) & 0x3F;
					var b = value & 0x1F;
					result[i * 4 + 0] = (byte)(r * 8.225806452f);
					result[i * 4 + 1] = (byte)(g * 4.047619048f);
					result[i * 4 + 2] = (byte)(b * 8.225806452f);
					result[i * 4 + 3] = 0xFF;
				}

				break;

			case TextureFormat.A4R4G4B4:
				for (var i = 0; i < width * height; ++i)
				{
					var low = source[i * 2];
					var high = source[i * 2 + 1];
					result[i * 4 + 0] = (byte)((high & 0x0F) * 17);
					result[i * 4 + 1] = (byte)((low >> 4) * 17);
					result[i * 4 + 2] = (byte)((low & 0x0F) * 17);
					result[i * 4 + 3] = (byte)((high >> 4) * 17);
				}

				break;

			case TextureFormat.P8:
				for (var i = 0; i < width * height; ++i)
				{
					var entry = _palette[source[i]];
					result[i * 4 + 0] = entry.R;
					result[i * 4 + 1] = entry.G;
					result[i * 4 + 2] = entry.B;
					result[i * 4 + 3] = entry.A;
				}

				break;

			default:
				throw new NotSupportedException(
					$"Texture format {Format} cannot be converted to RGBA.");
		}

		return result;
	}

	/// <summary>All mipmaps in level order, exactly as stored.</summary>
	public IReadOnlyList<byte[]> AllMipmapsRaw
	{
		get
		{
			var result = new List<byte[]>(MipmapCount);
			for (var level = 0; level < MipmapCount; ++level)
			{
				result.Add(GetMipmapRaw(level));
			}

			return result;
		}
	}

	/// <summary>All mipmaps in level order, decoded to RGBA8.</summary>
	public IReadOnlyList<byte[]> AllMipmapsRgba
	{
		get
		{
			var result = new List<byte[]>(MipmapCount);
			for (var level = 0; level < MipmapCount; ++level)
			{
				result.Add(GetMipmapRgba(level));
			}

			return result;
		}
	}

	private static int ReadDimension(Read reader)
	{
		var value = reader.ReadUInt();
		if (value > int.MaxValue)
		{
			throw new TextureFormatException($"Texture: dimension {value} is too large.");
		}

		return (int)value;
	}

	/// <summary>
	/// Byte size of one mipmap level. Ported from ZenKit, including its DXT block rounding
	/// rule for tiny mipmaps.
	/// </summary>
	private static int MipmapSize(TextureFormat format, int width, int height, int level)
	{
		var x = Math.Max(1, width);
		var y = Math.Max(1, height);
		for (var i = 0; i < level; ++i)
		{
			if (x > 1)
			{
				x >>= 1;
			}

			if (y > 1)
			{
				y >>= 1;
			}
		}

		switch (format)
		{
			case TextureFormat.B8G8R8A8:
			case TextureFormat.R8G8B8A8:
			case TextureFormat.A8B8G8R8:
			case TextureFormat.A8R8G8B8:
				return x * y * 4;

			case TextureFormat.B8G8R8:
			case TextureFormat.R8G8B8:
				return x * y * 3;

			case TextureFormat.A4R4G4B4:
			case TextureFormat.A1R5G5B5:
			case TextureFormat.R5G6B5:
				return x * y * 2;

			case TextureFormat.P8:
				return x * y;

			case TextureFormat.Dxt1:
				return Math.Max(1, x / 4) * Math.Max(1, y / 4) * 8;

			case TextureFormat.Dxt2:
			case TextureFormat.Dxt3:
			case TextureFormat.Dxt4:
			case TextureFormat.Dxt5:
				return Math.Max(1, x / 4) * Math.Max(1, y / 4) * 16;

			default:
				return 0;
		}
	}
}
