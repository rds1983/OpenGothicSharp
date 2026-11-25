using System;

namespace OpenGothic.ZenKit;

/// <summary>
/// Reads a material stored in a ZenGin BINARY archive. Mesh and multi-resolution mesh
/// materials share this layout: a material name string followed by a binary object whose
/// fields are positional, so the order below is the format.
/// </summary>
internal static class BinaryMaterialReader
{
	private const ushort MaterialVersionG1 = 17408;

	public static void ReadHeader(Read reader)
	{
		if (reader.ReadLine(true) != "ZenGin Archive")
		{
			throw new ZenArchiveException("Material archive: invalid signature.");
		}

		_ = reader.ReadLine(true); // ver 1
		_ = reader.ReadLine(true); // archiver
		if (reader.ReadLine(true) != "BINARY")
		{
			throw new ZenArchiveException("Material archive is not BINARY.");
		}

		_ = reader.ReadLine(true); // saveGame
		var optional = reader.ReadLine(true);
		if (optional.StartsWith("date ", StringComparison.Ordinal))
		{
			optional = reader.ReadLine(true);
		}

		if (optional.StartsWith("user ", StringComparison.Ordinal))
		{
			optional = reader.ReadLine(true);
		}

		if (optional != "END")
		{
			throw new ZenArchiveException("Material archive is missing its END marker.");
		}

		var objects = reader.ReadLine(true);
		if (!objects.StartsWith("objects ", StringComparison.Ordinal))
		{
			throw new ZenArchiveException("Material archive is missing its object count.");
		}

		if (reader.ReadLineThenIgnore("\n") != "END")
		{
			throw new ZenArchiveException("Material archive is missing its second END marker.");
		}
	}

	public static Material Read(Read reader)
	{
		_ = reader.ReadLine(); // material name written before the object

		var objectStart = reader.Position;
		var objectSize = reader.ReadUInt();
		var objectVersion = reader.ReadUShort();
		_ = reader.ReadUInt();
		_ = reader.ReadLine();
		var className = reader.ReadLine();
		if (className != "zCMaterial")
		{
			throw new ZenArchiveException($"Expected zCMaterial, got '{className}'.");
		}

		var gothic1 = objectVersion == MaterialVersionG1;
		var material = new Material
		{
			Name = reader.ReadLine(),
			Group = (MaterialGroup)reader.ReadUByte()
		};

		var b = reader.ReadUByte();
		var g = reader.ReadUByte();
		var r = reader.ReadUByte();
		var a = reader.ReadUByte();
		material.Color = new MaterialColor(a, r, g, b);
		material.SmoothAngle = reader.ReadFloat();
		material.Texture = reader.ReadLine();
		_ = reader.ReadLine(); // texture scale
		material.TextureAnimationFps = reader.ReadFloat();
		_ = reader.ReadUByte(); // animation mapping mode
		_ = reader.ReadLine(); // animation map direction
		material.DisableCollision = reader.ReadUByte() != 0;
		material.DisableLightmap = reader.ReadUByte() != 0;
		material.DontCollapse = reader.ReadUByte() != 0;
		_ = reader.ReadLine(); // detail object

		if (!gothic1)
		{
			_ = reader.ReadFloat(); // detail object scale
			_ = reader.ReadUByte(); // force occluder
			_ = reader.ReadUByte(); // environment mapping
			_ = reader.ReadFloat(); // environment mapping strength
			_ = reader.ReadUByte(); // wave mode
			_ = reader.ReadUByte(); // wave speed
			_ = reader.ReadFloat(); // wave max amplitude
			_ = reader.ReadFloat(); // wave grid size
			_ = reader.ReadUByte(); // ignore sun
			material.AlphaFunction = (AlphaFunction)reader.ReadUByte();
		}
		else
		{
			material.AlphaFunction = AlphaFunction.Default;
		}

		_ = reader.ReadFloat(); // default mapping x
		_ = reader.ReadFloat(); // default mapping y

		reader.Seek(objectStart + objectSize);
		return material;
	}
}
