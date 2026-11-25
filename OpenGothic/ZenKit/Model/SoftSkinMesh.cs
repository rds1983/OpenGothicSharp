using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenGothic.ZenKit;

public sealed class SoftSkinWeight
{
	public float Weight { get; internal set; }

	public Vector3 Position { get; internal set; }

	public byte NodeIndex { get; internal set; }
}

public sealed class SoftSkinWedgeNormal
{
	public Vector3 Normal { get; internal set; }

	public uint Index { get; internal set; }
}

/// <summary>
/// Managed soft-skin mesh reader. Ported from ZenKit's <c>SoftSkinMesh</c>: the embedded MRM
/// plus per-vertex weights that bind it to hierarchy nodes.
/// </summary>
public sealed class SoftSkinMesh
{
	private const ushort ChunkHeader = 0xE100;
	private const ushort ChunkEnd = 0xE110;
	private const ushort ChunkProto = 0xB100;
	private const ushort ChunkNodes = 0xB1FF;

	public MultiResolutionMesh Mesh { get; private set; } = new MultiResolutionMesh();

	public List<List<SoftSkinWeight>> Weights { get; } = new List<List<SoftSkinWeight>>();

	public List<SoftSkinWedgeNormal> WedgeNormals { get; } = new List<SoftSkinWedgeNormal>();

	public List<int> Nodes { get; } = new List<int>();

	public static SoftSkinMesh Load(Read reader)
	{
		var mesh = new SoftSkinMesh();
		mesh.ReadChunks(reader);
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
				case ChunkHeader:
					_ = reader.ReadUInt();
					break;

				case ChunkProto:
					Mesh = MultiResolutionMesh.LoadFromSection(reader);
					break;

				case ChunkNodes:
					ReadNodes(reader);
					break;

				case ChunkEnd:
					return;
			}

			reader.Seek(target);
		}
	}

	private void ReadNodes(Read reader)
	{
		var weightBufferSize = reader.ReadUInt();
		var weightBufferEnd = reader.Position + weightBufferSize;

		Weights.Clear();
		for (var i = 0; i < Mesh.Positions.Count; ++i)
		{
			var count = reader.ReadUInt();
			var list = new List<SoftSkinWeight>((int)count);
			for (var j = 0; j < count; ++j)
			{
				list.Add(new SoftSkinWeight
				{
					Weight = reader.ReadFloat(),
					Position = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat()),
					NodeIndex = reader.ReadUByte()
				});
			}

			Weights.Add(list);
		}

		reader.Seek(weightBufferEnd);

		var normalCount = reader.ReadUInt();
		WedgeNormals.Clear();
		for (var i = 0; i < normalCount; ++i)
		{
			WedgeNormals.Add(new SoftSkinWedgeNormal
			{
				Normal = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat()),
				Index = reader.ReadUInt()
			});
		}

		var nodeCount = reader.ReadUShort();
		Nodes.Clear();
		for (var i = 0; i < nodeCount; ++i)
		{
			Nodes.Add(reader.ReadInt());
		}

		for (var i = 0; i < Nodes.Count; ++i)
		{
			MultiResolutionMesh.SkipOrientedBox(reader);
		}
	}
}
