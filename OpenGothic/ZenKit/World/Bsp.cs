using System;
using System.Collections.Generic;

namespace OpenGothic.ZenKit;

/// <summary>
/// Minimal BSP reader. The renderer does its own grid culling, but the mesh triangulation
/// needs the BSP's leaf-polygon list to match the reference's visible polygon set.
/// </summary>
public sealed class BspTree
{
	private const ushort ChunkHeader = 0xC000;
	private const ushort ChunkPolygons = 0xC010;
	private const ushort ChunkTree = 0xC040;
	private const ushort ChunkEnd = 0xC0FF;

	private const uint VersionG1 = 0x2090000;

	private readonly List<uint> _polygonIndices = new List<uint>();
	private readonly List<uint> _leafPolygons = new List<uint>();

	public IReadOnlyList<uint> LeafPolygons => _leafPolygons;

	public IReadOnlyList<uint> PolygonIndices => _polygonIndices;

	public static BspTree Load(Read reader, uint version)
	{
		var tree = new BspTree();
		tree.ReadChunks(reader, version);
		return tree;
	}

	private void ReadChunks(Read reader, uint version)
	{
		while (!reader.Eof)
		{
			var type = reader.ReadUShort();
			var size = reader.ReadUInt();
			var target = reader.Position + size;

			switch (type)
			{
				case ChunkHeader:
					_ = reader.ReadUShort();
					_ = reader.ReadUInt(); // tree mode
					break;

				case ChunkPolygons:
				{
					var count = reader.ReadUInt();
					_polygonIndices.Clear();
					for (var i = 0; i < count; ++i)
					{
						_polygonIndices.Add(reader.ReadUInt());
					}

					break;
				}

				case ChunkTree:
				{
					var nodeCount = reader.ReadUInt();
					_ = reader.ReadUInt(); // leaf count
					var nodes = new List<(uint PolygonIndex, uint PolygonCount)>();
					var leafNodes = new List<int>();

					void ParseNode(bool leaf)
					{
						var self = nodes.Count;

						reader.Skip(24); // bounding box
						var polygonIndex = reader.ReadUInt();
						var polygonCount = reader.ReadUInt();
						nodes.Add((polygonIndex, polygonCount));

						if (leaf)
						{
							leafNodes.Add(self);
							return;
						}

						var flags = reader.ReadUByte();
						_ = reader.ReadFloat(); // plane w
						_ = reader.ReadFloat(); // plane x
						_ = reader.ReadFloat(); // plane y
						_ = reader.ReadFloat(); // plane z
						if (version == VersionG1)
						{
							_ = reader.ReadUByte();
						}

						if ((flags & 0x01) != 0)
						{
							ParseNode((flags & 0x04) != 0);
						}

						if ((flags & 0x02) != 0)
						{
							ParseNode((flags & 0x08) != 0);
						}
					}

					ParseNode(nodeCount == 1);

					_leafPolygons.Clear();
					foreach (var leaf in leafNodes)
					{
						var node = nodes[leaf];
						for (var i = 0; i < node.PolygonCount; ++i)
						{
							var index = node.PolygonIndex + i;
							if (index < _polygonIndices.Count)
							{
								_leafPolygons.Add(_polygonIndices[(int)index]);
							}
						}
					}

					_leafPolygons.Sort();
					break;
				}

				case ChunkEnd:
					reader.Seek(target);
					return;
			}

			reader.Seek(target);
		}
	}
}
