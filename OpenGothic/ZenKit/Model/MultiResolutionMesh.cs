using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace OpenGothic.ZenKit;

public readonly struct MeshTriangle
{
	public MeshTriangle(ushort wedge0, ushort wedge1, ushort wedge2)
	{
		Wedge0 = wedge0;
		Wedge1 = wedge1;
		Wedge2 = wedge2;
	}

	public ushort Wedge0 { get; }
	public ushort Wedge1 { get; }
	public ushort Wedge2 { get; }
}

public readonly struct MeshWedge
{
	public MeshWedge(Vector3 normal, Vector2 texture, ushort index)
	{
		Normal = normal;
		Texture = texture;
		Index = index;
	}

	public Vector3 Normal { get; }
	public Vector2 Texture { get; }
	public ushort Index { get; }
}

public readonly struct MeshPlane
{
	public MeshPlane(float distance, Vector3 normal)
	{
		Distance = distance;
		Normal = normal;
	}

	public float Distance { get; }
	public Vector3 Normal { get; }
}

public sealed class MultiResolutionSubMesh
{
	public Material Material { get; internal set; } = new Material();

	public List<MeshTriangle> Triangles { get; } = new List<MeshTriangle>();

	public List<MeshWedge> Wedges { get; } = new List<MeshWedge>();

	public List<float> Colors { get; } = new List<float>();

	public List<MeshPlane> TrianglePlanes { get; } = new List<MeshPlane>();

	public List<ushort> WedgeMap { get; } = new List<ushort>();
}

/// <summary>
/// Managed multi-resolution mesh (`MRM`) reader. Ported from ZenKit's
/// <c>MultiResolutionMesh</c>; mobs, items and other props use this format.
/// </summary>
public sealed class MultiResolutionMesh
{
	private const ushort ChunkMesh = 0xB100;
	private const ushort ChunkEnd = 0xB1FF;

	private const ushort VersionG2 = 0x905;
	private const ushort VersionG2Other = 0x805;

	private readonly struct Section
	{
		public Section(uint offset, uint size)
		{
			Offset = offset;
			Size = size;
		}

		public uint Offset { get; }
		public uint Size { get; }
	}

	public List<Vector3> Positions { get; } = new List<Vector3>();

	public List<Vector3> Normals { get; } = new List<Vector3>();

	public List<MultiResolutionSubMesh> SubMeshes { get; } = new List<MultiResolutionSubMesh>();

	public List<Material> Materials { get; } = new List<Material>();

	public bool AlphaTest { get; internal set; } = true;

	public Box3 BoundingBox { get; internal set; } = Box3.Zero;

	public static MultiResolutionMesh Load(Read reader)
	{
		var mesh = new MultiResolutionMesh();
		mesh.ReadChunks(reader);
		return mesh;
	}

	/// <summary>Reads a bare MRM section (the payload of a PROTO chunk).</summary>
	public static MultiResolutionMesh LoadFromSection(Read reader)
	{
		var mesh = new MultiResolutionMesh();
		mesh.ReadSection(reader);
		return mesh;
	}

	private void ReadChunks(Read reader)
	{
		while (!reader.Eof)
		{
			var type = reader.ReadUShort();
			var size = reader.ReadUInt();
			var target = reader.Position + size;

			switch (type)
			{
				case ChunkMesh:
					ReadSection(reader);
					break;

				case ChunkEnd:
					return;
			}

			reader.Seek(target);
		}
	}

	private void ReadSection(Read reader)
	{
		var version = reader.ReadUShort();
		var contentSize = reader.ReadUInt();
		var contentOffset = reader.Position;

		reader.Seek(contentSize, SeekOrigin.Current);
		var tableStart = reader.Position;

		var subMeshCount = reader.ReadUByte();
		var positionsOffset = reader.ReadUInt();
		var positionsCount = reader.ReadUInt();
		var normalsOffset = reader.ReadUInt();
		var normalsCount = reader.ReadUInt();

		var sections = new Section[subMeshCount, 10];
		for (var i = 0; i < subMeshCount; ++i)
		{
			for (var j = 0; j < 10; ++j)
			{
				sections[i, j] = new Section(reader.ReadUInt(), reader.ReadUInt());
			}
		}

		BinaryMaterialReader.ReadHeader(reader);
		Materials.Clear();
		for (var i = 0; i < subMeshCount; ++i)
		{
			Materials.Add(BinaryMaterialReader.Read(reader));
		}

		if (version == VersionG2 || version == VersionG2Other)
		{
			AlphaTest = reader.ReadUByte() != 0;
		}

		BoundingBox = ModelHierarchy.ReadBox(reader);
		SkipOrientedBox(reader);
		reader.Skip(16);

		var end = reader.Position;

		Positions.Clear();
		reader.Seek(contentOffset + positionsOffset);
		for (var i = 0; i < positionsCount; ++i)
		{
			Positions.Add(new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat()));
		}

		Normals.Clear();
		reader.Seek(contentOffset + normalsOffset);
		for (var i = 0; i < normalsCount; ++i)
		{
			Normals.Add(new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat()));
		}

		SubMeshes.Clear();
		for (var i = 0; i < subMeshCount; ++i)
		{
			var subMesh = new MultiResolutionSubMesh();
			ReadSubMesh(reader, contentOffset, sections, i, subMesh);
			subMesh.Material = Materials[i];
			SubMeshes.Add(subMesh);
		}

		reader.Seek(end);
	}

	private static void ReadSubMesh(Read reader, long contentOffset, Section[,] sections, int index,
		MultiResolutionSubMesh subMesh)
	{
		// 0: triangles (ushort wedge indices)
		SeekSection(reader, contentOffset, sections[index, 0]);
		var count = (int)sections[index, 0].Size;
		for (var i = 0; i < count; ++i)
		{
			subMesh.Triangles.Add(new MeshTriangle(reader.ReadUShort(), reader.ReadUShort(), reader.ReadUShort()));
		}

		// 1: wedges (normal, texture, position index, padding)
		SeekSection(reader, contentOffset, sections[index, 1]);
		count = (int)sections[index, 1].Size;
		for (var i = 0; i < count; ++i)
		{
			var normal = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
			var texture = new Vector2(reader.ReadFloat(), reader.ReadFloat());
			var vertex = reader.ReadUShort();
			_ = reader.ReadUShort();
			subMesh.Wedges.Add(new MeshWedge(normal, texture, vertex));
		}

		// 2: colors
		SeekSection(reader, contentOffset, sections[index, 2]);
		count = (int)sections[index, 2].Size;
		for (var i = 0; i < count; ++i)
		{
			subMesh.Colors.Add(reader.ReadFloat());
		}

		// 3: triangle plane indices (not needed by the renderer)
		// 4: triangle planes
		SeekSection(reader, contentOffset, sections[index, 4]);
		count = (int)sections[index, 4].Size;
		for (var i = 0; i < count; ++i)
		{
			var distance = reader.ReadFloat();
			var normal = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
			subMesh.TrianglePlanes.Add(new MeshPlane(distance, normal));
		}

		// 5: wedge map
		SeekSection(reader, contentOffset, sections[index, 5]);
		count = (int)sections[index, 5].Size;
		for (var i = 0; i < count; ++i)
		{
			subMesh.WedgeMap.Add(reader.ReadUShort());
		}
	}

	private static void SeekSection(Read reader, long contentOffset, Section section)
	{
		reader.Seek(contentOffset + section.Offset);
	}

	internal static void SkipOrientedBox(Read reader)
	{
		// center, three axes, half width
		reader.Skip(12 * 5);
		var children = reader.ReadUShort();
		for (var i = 0; i < children; ++i)
		{
			SkipOrientedBox(reader);
		}
	}
}
