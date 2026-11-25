using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenGothic.ZenKit;

/// <summary>Alpha compositing modes stored on a material.</summary>
public enum AlphaFunction : byte
{
	Default = 0,
	None = 1,
	Blend = 2,
	Add = 3,
	Subtract = 4,
	Multiply = 5,
	MultiplyAlt = 6
}

/// <summary>General material type; controls friction and the water pass.</summary>
public enum MaterialGroup : byte
{
	Undefined = 0,
	Metal = 1,
	Stone = 2,
	Wood = 3,
	Earth = 4,
	Water = 5,
	Snow = 6,
	None = 0xFF
}

public readonly struct MaterialColor
{
	public MaterialColor(byte a, byte r, byte g, byte b)
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

/// <summary>A material of a world mesh.</summary>
public sealed class Material
{
	public string Name { get; internal set; } = string.Empty;

	public MaterialGroup Group { get; internal set; }

	public MaterialColor Color { get; internal set; }

	public float SmoothAngle { get; internal set; }

	public string Texture { get; internal set; } = string.Empty;

	public float TextureAnimationFps { get; internal set; }

	public bool DisableCollision { get; internal set; }

	public bool DisableLightmap { get; internal set; }

	public bool DontCollapse { get; internal set; }

	public AlphaFunction AlphaFunction { get; internal set; }

	public override string ToString() => string.IsNullOrEmpty(Texture) ? Name : Texture;
}

public readonly struct VertexFeature
{
	public VertexFeature(Vector2 texture, uint light, Vector3 normal)
	{
		Texture = texture;
		Light = light;
		Normal = normal;
	}

	public Vector2 Texture { get; }

	public uint Light { get; }

	public Vector3 Normal { get; }
}

/// <summary>One triangle of the triangulated world mesh.</summary>
public sealed class MeshPolygon
{
	public MeshPolygon(int[] positionIndices, int[] featureIndices, int materialIndex,
		bool isGhostOccluder, bool isPortal, bool isOutdoor)
	{
		PositionIndices = positionIndices;
		FeatureIndices = featureIndices;
		MaterialIndex = materialIndex;
		IsGhostOccluder = isGhostOccluder;
		IsPortal = isPortal;
		IsOutdoor = isOutdoor;
	}

	public int[] PositionIndices { get; }

	public int[] FeatureIndices { get; }

	public int MaterialIndex { get; }

	public bool IsGhostOccluder { get; }

	public bool IsPortal { get; }

	public bool IsOutdoor { get; }
}

/// <summary>
/// Managed ZEN mesh reader. Ported from ZenKit's <c>zenkit::Mesh</c>; the world stores the
/// mesh as length-prefixed chunks and a binary archive of materials.
/// </summary>
public sealed class Mesh
{
	private const ushort ChunkMarker = 0xB000;
	private const ushort ChunkBounds = 0xB010;
	private const ushort ChunkMaterial = 0xB020;
	private const ushort ChunkLightmaps = 0xB025;
	private const ushort ChunkLightmapsShared = 0xB026;
	private const ushort ChunkVertices = 0xB030;
	private const ushort ChunkFeatures = 0xB040;
	private const ushort ChunkPolygons = 0xB050;
	private const ushort ChunkEnd = 0xB060;

	private const ushort MeshVersionG2 = 265;
	private const ushort MaterialVersionG1 = 17408;

	public string Name { get; private set; } = string.Empty;

	public ushort Version { get; private set; }

	public Vector3[] Positions { get; private set; } = Array.Empty<Vector3>();

	public VertexFeature[] Features { get; private set; } = Array.Empty<VertexFeature>();

	public Material[] Materials { get; private set; } = Array.Empty<Material>();

	/// <summary>The mesh's source polygons, in file order; the renderer fan-triangulates them.</summary>
	public List<MeshPolygon> Polygons { get; } = new List<MeshPolygon>();

	public static Mesh Load(Read reader, bool forceWideIndices = false)
	{
		var mesh = new Mesh();
		mesh.ReadChunks(reader, forceWideIndices);
		return mesh;
	}

	private void ReadChunks(Read reader, bool forceWideIndices)
	{
		while (!reader.Eof)
		{
			var type = reader.ReadUShort();
			var size = reader.ReadUInt();
			var target = reader.Position + size;

			switch (type)
			{
				case ChunkMarker:
					Version = reader.ReadUShort();
					reader.Skip(16); // creation date
					Name = reader.ReadLine();
					break;

				case ChunkBounds:
					break;

				case ChunkMaterial:
					ReadMaterials(reader);
					break;

				case ChunkVertices:
				{
					var count = reader.ReadUInt();
					Positions = new Vector3[count];
					for (var i = 0; i < Positions.Length; ++i)
					{
						Positions[i] = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
					}

					break;
				}

				case ChunkFeatures:
				{
					var count = reader.ReadUInt();
					Features = new VertexFeature[count];
					for (var i = 0; i < Features.Length; ++i)
					{
						var texture = new Vector2(reader.ReadFloat(), reader.ReadFloat());
						var light = reader.ReadUInt();
						var normal = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
						Features[i] = new VertexFeature(texture, light, normal);
					}

					break;
				}

				case ChunkPolygons:
					ReadPolygons(reader, forceWideIndices);
					break;

				case ChunkLightmaps:
				case ChunkLightmapsShared:
					break;

				case ChunkEnd:
					return;
			}

			reader.Seek(target);
		}
	}

	private void ReadPolygons(Read reader, bool forceWideIndices)
	{
		var wideIndices = Version == MeshVersionG2 || forceWideIndices;
		var count = reader.ReadUInt();
		Polygons.Clear();

		for (var i = 0; i < count; ++i)
		{
			var material = reader.ReadUShort();
			_ = reader.ReadShort(); // lightmap index
			_ = reader.ReadFloat(); // plane distance
			_ = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());

			var isPortal = false;
			var isOutdoor = false;
			var isGhostOccluder = false;
			if (Version == MeshVersionG2)
			{
				var flags = reader.ReadUByte();
				isPortal = (flags & 0b00000011) != 0;
				isOutdoor = (flags & 0b00100000) != 0;
				isGhostOccluder = (flags & 0b01000000) != 0;
			}
			else
			{
				var flags1 = reader.ReadUByte();
				_ = reader.ReadUByte(); // normal axis
				isPortal = (flags1 & 0b00000011) != 0;
				isOutdoor = (flags1 & 0b00100000) != 0;
				isGhostOccluder = (flags1 & 0b01000000) != 0;
			}

			_ = reader.ReadShort(); // sector index

			var vertexCount = reader.ReadUByte();
			var vertexIndices = new int[vertexCount];
			var featureIndices = new int[vertexCount];
			for (var j = 0; j < vertexCount; ++j)
			{
				vertexIndices[j] = wideIndices ? (int)reader.ReadUInt() : reader.ReadUShort();
				featureIndices[j] = (int)reader.ReadUInt();
			}

			Polygons.Add(new MeshPolygon(vertexIndices, featureIndices, material, isGhostOccluder,
				isPortal, isOutdoor));
		}
	}

	private void ReadMaterials(Read reader)
	{
		ReadBinaryHeader(reader);
		var count = reader.ReadUInt();
		Materials = new Material[count];
		for (var i = 0; i < Materials.Length; ++i)
		{
			Materials[i] = ReadMaterial(reader);
		}

		if (Version == MeshVersionG2)
		{
			_ = reader.ReadUByte(); // alpha test
		}
	}

	private static Material ReadMaterial(Read reader)
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

	private static void ReadBinaryHeader(Read reader)
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
}
