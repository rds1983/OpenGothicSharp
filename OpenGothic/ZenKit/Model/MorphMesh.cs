using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenGothic.ZenKit;

public sealed class MorphSource
{
	public DateTime? FileDate { get; internal set; }

	public string FileName { get; internal set; } = string.Empty;
}

public sealed class MorphAnimation
{
	public string Name { get; internal set; } = string.Empty;

	public int Layer { get; internal set; }

	public float BlendIn { get; internal set; }

	public float BlendOut { get; internal set; }

	public float Duration { get; internal set; }

	public float Speed { get; internal set; }

	public byte Flags { get; internal set; }

	public uint FrameCount { get; internal set; }

	public List<uint> Vertices { get; } = new List<uint>();

	public List<Vector3> Samples { get; } = new List<Vector3>();
}

/// <summary>Managed morph mesh reader (`.MMB`). Ported from ZenKit's <c>MorphMesh</c>.</summary>
public sealed class MorphMesh
{
	private const ushort ChunkSources = 0xE010;
	private const ushort ChunkHeader = 0xE020;
	private const ushort ChunkAnimations = 0xE030;
	private const ushort ChunkProto = 0xB100;
	private const ushort ChunkMorph = 0xB1FF;

	public string Name { get; private set; } = string.Empty;

	public MultiResolutionMesh Mesh { get; private set; } = new MultiResolutionMesh();

	public List<Vector3> MorphPositions { get; } = new List<Vector3>();

	public List<MorphAnimation> Animations { get; } = new List<MorphAnimation>();

	public List<MorphSource> Sources { get; } = new List<MorphSource>();

	public static MorphMesh Load(Read reader)
	{
		var mesh = new MorphMesh();
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
				case ChunkSources:
				{
					var count = reader.ReadUShort();
					Sources.Clear();
					for (var i = 0; i < count; ++i)
					{
						reader.Skip(16);
						Sources.Add(new MorphSource { FileName = reader.ReadLine(true) });
					}

					break;
				}

				case ChunkHeader:
					_ = reader.ReadUInt();
					Name = reader.ReadLine(true);
					break;

				case ChunkProto:
					Mesh = MultiResolutionMesh.LoadFromSection(reader);
					MorphPositions.Clear();
					for (var i = 0; i < Mesh.Positions.Count; ++i)
					{
						MorphPositions.Add(Vector3.Zero);
					}

					break;

				case ChunkMorph:
					for (var i = 0; i < MorphPositions.Count; ++i)
					{
						MorphPositions[i] = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
					}

					break;

				case ChunkAnimations:
				{
					var count = reader.ReadUShort();
					Animations.Clear();
					for (var i = 0; i < count; ++i)
					{
						var animation = new MorphAnimation
						{
							Name = reader.ReadLine(),
							BlendIn = reader.ReadFloat(),
							BlendOut = reader.ReadFloat(),
							Duration = reader.ReadFloat(),
							Layer = reader.ReadInt(),
							Speed = reader.ReadFloat(),
							Flags = reader.ReadUByte()
						};

						var vertexCount = reader.ReadUInt();
						animation.FrameCount = reader.ReadUInt();

						animation.Vertices.Capacity = (int)vertexCount;
						for (var j = 0; j < vertexCount; ++j)
						{
							animation.Vertices.Add(reader.ReadUInt());
						}

						animation.Samples.Capacity = (int)(vertexCount * animation.FrameCount);
						for (var j = 0; j < vertexCount * animation.FrameCount; ++j)
						{
							animation.Samples.Add(new Vector3(reader.ReadFloat(), reader.ReadFloat(),
								reader.ReadFloat()));
						}

						Animations.Add(animation);
					}

					break;
				}
			}

			reader.Seek(target);
		}
	}
}
