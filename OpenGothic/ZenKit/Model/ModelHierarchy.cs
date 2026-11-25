using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenGothic.ZenKit;

public sealed class ModelHierarchyNode
{
	public short ParentIndex { get; internal set; }

	public string Name { get; internal set; } = string.Empty;

	public Matrix4x4 Transform { get; internal set; } = Matrix4x4.Identity;
}

/// <summary>
/// Managed model hierarchy reader (`MDH` and the hierarchy embedded in an `MDL`). Ported from
/// ZenKit's <c>ModelHierarchy</c>; the node transforms are the bind pose.
/// </summary>
public sealed class ModelHierarchy
{
	private const ushort ChunkHierarchy = 0xD100;
	private const ushort ChunkSource = 0xD110;
	private const ushort ChunkEnd = 0xD120;

	public List<ModelHierarchyNode> Nodes { get; } = new List<ModelHierarchyNode>();

	public Box3 BoundingBox { get; internal set; } = Box3.Zero;

	public Box3 CollisionBoundingBox { get; internal set; } = Box3.Zero;

	public Vector3 RootTranslation { get; internal set; }

	public uint Checksum { get; internal set; }

	public DateTime? SourceDate { get; internal set; }

	public string SourcePath { get; internal set; } = string.Empty;

	public static ModelHierarchy Load(Read reader)
	{
		var hierarchy = new ModelHierarchy();
		hierarchy.ReadChunks(reader);
		return hierarchy;
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
				case ChunkHierarchy:
				{
					var version = reader.ReadUInt();
					if (version != 3)
					{
						Log.Warning($"ModelHierarchy: unsupported version {version}.");
					}

					var count = reader.ReadUShort();
					Nodes.Clear();
					for (var i = 0; i < count; ++i)
					{
						var node = new ModelHierarchyNode
						{
							Name = reader.ReadLine(),
							ParentIndex = reader.ReadShort(),
							Transform = ReadMatrix4x4(reader)
						};
						Nodes.Add(node);
					}

					BoundingBox = ReadBox(reader);
					CollisionBoundingBox = ReadBox(reader);
					RootTranslation = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
					Checksum = reader.ReadUInt();
					break;
				}

				case ChunkSource:
					reader.Skip(16); // source date
					SourcePath = reader.ReadLine();
					break;

				case ChunkEnd:
					return;
			}

			reader.Seek(target);
		}
	}

	internal static Matrix4x4 ReadMatrix4x4(Read reader)
	{
		var values = new float[16];
		for (var i = 0; i < values.Length; ++i)
		{
			values[i] = reader.ReadFloat();
		}

		return new Matrix4x4(
			values[0], values[1], values[2], values[3],
			values[4], values[5], values[6], values[7],
			values[8], values[9], values[10], values[11],
			values[12], values[13], values[14], values[15]);
	}

	internal static Box3 ReadBox(Read reader)
	{
		var min = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
		var max = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
		return new Box3(min, max);
	}
}
