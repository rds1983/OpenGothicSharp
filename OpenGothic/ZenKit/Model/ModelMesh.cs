using System;
using System.Collections.Generic;

namespace OpenGothic.ZenKit;

/// <summary>
/// Managed model mesh reader (`MDM` and the embedded mesh of an `MDL`). Ported from ZenKit's
/// <c>ModelMesh</c>; it bundles soft-skin meshes and named static attachments.
/// </summary>
public sealed class ModelMesh
{
	private const ushort ChunkHeader = 0xD000;
	private const ushort ChunkSource = 0xD010;
	private const ushort ChunkNodes = 0xD020;
	private const ushort ChunkSoftSkins = 0xD030;
	private const ushort ChunkEnd = 0xD040;
	private const ushort ChunkProto = 0xB100;

	public List<SoftSkinMesh> Meshes { get; } = new List<SoftSkinMesh>();

	public Dictionary<string, MultiResolutionMesh> Attachments { get; } =
		new Dictionary<string, MultiResolutionMesh>(StringComparer.OrdinalIgnoreCase);

	public uint Checksum { get; private set; }

	public static ModelMesh Load(Read reader)
	{
		var mesh = new ModelMesh();
		mesh.ReadChunks(reader);
		return mesh;
	}

	private void ReadChunks(Read reader)
	{
		var attachmentNames = new List<string>();

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

				case ChunkSource:
					reader.Skip(16); // source date
					_ = reader.ReadLine();
					break;

				case ChunkNodes:
				{
					var count = reader.ReadUShort();
					attachmentNames.Clear();
					for (var i = 0; i < count; ++i)
					{
						attachmentNames.Add(reader.ReadLine(true));
					}

					break;
				}

				case ChunkProto:
				{
					var name = Attachments.Count < attachmentNames.Count
						? attachmentNames[Attachments.Count]
						: Attachments.Count.ToString();
					Attachments[name] = MultiResolutionMesh.LoadFromSection(reader);
					break;
				}

				case ChunkSoftSkins:
				{
					Checksum = reader.ReadUInt();

					var count = reader.ReadUShort();
					Meshes.Clear();
					for (var i = 0; i < count; ++i)
					{
						Meshes.Add(SoftSkinMesh.Load(reader));
					}

					// This chunk's stored length does not cover the soft-skin meshes, so the
					// next chunk starts where the meshes ended.
					target = reader.Position;
					break;
				}

				case ChunkEnd:
					return;
			}

			reader.Seek(target);
		}
	}
}

/// <summary>An `MDL`: a model hierarchy followed by its model mesh.</summary>
public sealed class Model
{
	public ModelHierarchy Hierarchy { get; private set; }

	public ModelMesh Mesh { get; private set; }

	public static Model Load(Read reader)
	{
		var hierarchy = ModelHierarchy.Load(reader);
		var mesh = ModelMesh.Load(reader);
		return new Model { Hierarchy = hierarchy, Mesh = mesh };
	}
}
