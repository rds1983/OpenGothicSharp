using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenGothic.ZenKit;

public enum AnimationEventType : uint
{
	Unknown = 0,
	Tag = 0,
	SoundEffect = 1,
	SoundEffectGround = 2,
	Batch = 3,
	SwapMesh = 4,
	Heading = 5,
	ParticleEffect = 6,
	ParticleEffectGround = 7,
	ParticleEffectStop = 8,
	SetMesh = 9,
	MorphMeshAnimation = 10,
	CameraTremor = 11
}

public sealed class AnimationEvent
{
	public AnimationEventType Type { get; internal set; }

	public uint Frame { get; internal set; }

	public string Tag { get; internal set; } = string.Empty;

	public string[] Content { get; } = new string[4];

	public float[] Values { get; } = new float[4];

	public float Probability { get; internal set; }
}

public struct AnimationSample
{
	public Vector3 Position { get; internal set; }

	public Quaternion Rotation { get; internal set; }
}

/// <summary>
/// Managed model animation reader (`.MAN`). Ported from ZenKit's <c>ModelAnimation</c>.
/// Samples are stored frame-major; the caller indexes them by <c>frame * nodeCount + slot</c>.
/// </summary>
public sealed class ModelAnimation
{
	private const ushort ChunkMarker = 0xA000;
	private const ushort ChunkSource = 0xA010;
	private const ushort ChunkHeader = 0xA020;
	private const ushort ChunkEvents = 0xA030;
	private const ushort ChunkSamples = 0xA090;

	private const float RotationRange = 65535f;
	private const float RotationScale = 2.1f / RotationRange;
	private const ushort RotationMid = 32767;

	public string Name { get; private set; } = string.Empty;

	public string Next { get; private set; } = string.Empty;

	public uint Layer { get; private set; }

	public uint FrameCount { get; private set; }

	public uint NodeCount { get; private set; }

	public float Fps { get; private set; }

	public float FpsSource { get; private set; }

	public float SamplePositionMin { get; private set; }

	public float SamplePositionScale { get; private set; }

	public Box3 BoundingBox { get; private set; }

	public uint Checksum { get; private set; }

	public string SourcePath { get; private set; } = string.Empty;

	public string SourceScript { get; private set; } = string.Empty;

	public List<AnimationEvent> Events { get; } = new List<AnimationEvent>();

	public List<uint> NodeIndices { get; } = new List<uint>();

	public List<AnimationSample> Samples { get; } = new List<AnimationSample>();

	public static ModelAnimation Load(Read reader)
	{
		var animation = new ModelAnimation();
		animation.ReadChunks(reader);
		return animation;
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
				case ChunkSource:
					reader.Skip(16);
					SourcePath = reader.ReadLine();
					SourceScript = reader.ReadLine();
					break;

				case ChunkHeader:
					_ = reader.ReadUShort(); // version
					Name = reader.ReadLine();
					Layer = reader.ReadUInt();
					FrameCount = reader.ReadUInt();
					NodeCount = reader.ReadUInt();
					Fps = reader.ReadFloat();
					FpsSource = reader.ReadFloat();
					SamplePositionMin = reader.ReadFloat();
					SamplePositionScale = reader.ReadFloat();
					BoundingBox = ModelHierarchy.ReadBox(reader);
					Next = reader.ReadLine();
					break;

				case ChunkEvents:
				{
					var count = reader.ReadUInt();
					for (var i = 0; i < count; ++i)
					{
						var animationEvent = new AnimationEvent
						{
							Type = (AnimationEventType)reader.ReadUInt(),
							Frame = reader.ReadUInt(),
							Tag = reader.ReadLine(true)
						};

						for (var j = 0; j < 4; ++j)
						{
							animationEvent.Content[j] = reader.ReadLine(true);
						}

						for (var j = 0; j < 4; ++j)
						{
							animationEvent.Values[j] = reader.ReadFloat();
						}

						animationEvent.Probability = reader.ReadFloat();
						Events.Add(animationEvent);
					}

					break;
				}

				case ChunkSamples:
				{
					Checksum = reader.ReadUInt();

					NodeIndices.Clear();
					for (var i = 0; i < NodeCount; ++i)
					{
						NodeIndices.Add(reader.ReadUInt());
					}

					Samples.Clear();
					Samples.Capacity = (int)(NodeCount * FrameCount);
					for (var i = 0; i < NodeCount * FrameCount; ++i)
					{
						var sample = new AnimationSample
						{
							Rotation = ReadRotation(reader),
							Position = ReadPosition(reader)
						};
						Samples.Add(sample);
					}

					break;
				}
			}

			reader.Seek(target);
		}
	}

	private static Quaternion ReadRotation(Read reader)
	{
		var x = (reader.ReadUShort() - RotationMid) * RotationScale;
		var y = (reader.ReadUShort() - RotationMid) * RotationScale;
		var z = (reader.ReadUShort() - RotationMid) * RotationScale;
		var lengthSquared = x * x + y * y + z * z;
		float w;
		if (lengthSquared > 1f)
		{
			var scale = 1f / MathF.Sqrt(lengthSquared);
			x *= scale;
			y *= scale;
			z *= scale;
			w = 0f;
		}
		else
		{
			w = MathF.Sqrt(1f - lengthSquared);
		}

		return new Quaternion(x, y, z, w);
	}

	private Vector3 ReadPosition(Read reader)
	{
		return new Vector3(
			reader.ReadUShort() * SamplePositionScale + SamplePositionMin,
			reader.ReadUShort() * SamplePositionScale + SamplePositionMin,
			reader.ReadUShort() * SamplePositionScale + SamplePositionMin);
	}
}
