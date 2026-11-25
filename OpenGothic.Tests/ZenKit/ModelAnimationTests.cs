using OpenGothic.ZenKit;
using System;
using System.IO;
using System.Numerics;
using System.Text;
using Xunit;

namespace OpenGothic.Tests.ZenKit;

public class ModelAnimationTests
{
	[Fact]
	public void ReadsSamplesAndEvents()
	{
		var data = BuildAnimation();
		var animation = ModelAnimation.Load(new MemoryRead(data));

		Assert.Equal("S_RUN", animation.Name);
		Assert.Equal("S_RUN", animation.Next);
		Assert.Equal(1u, animation.Layer);
		Assert.Equal(2u, animation.FrameCount);
		Assert.Equal(1u, animation.NodeCount);
		Assert.Equal(10f, animation.Fps);
		Assert.Equal(new[] { 3u }, animation.NodeIndices);
		Assert.Equal(2, animation.Samples.Count);

		Assert.Equal(new Vector3(0, 100, 200), animation.Samples[0].Position);
		Assert.Equal(Quaternion.Identity, animation.Samples[0].Rotation);
		Assert.Equal(new Vector3(300, 400, 500), animation.Samples[1].Position);
		Assert.Equal(Quaternion.Identity, animation.Samples[1].Rotation);

		var animationEvent = Assert.Single(animation.Events);
		Assert.Equal(AnimationEventType.SoundEffect, animationEvent.Type);
		Assert.Equal(4u, animationEvent.Frame);
		Assert.Equal("sound", animationEvent.Tag);
	}

	[Fact]
	public void ReadsABinaryModelScript()
	{
		var data = BuildScript();
		var script = ModelScript.Load(new MemoryRead(data));

		Assert.Equal("HUMANS", script.SkeletonName);
		Assert.Equal(new[] { "BODY.ASC" }, script.Meshes);
		var animation = Assert.Single(script.Animations);
		Assert.Equal("S_RUN", animation.Name);
		Assert.Equal("S_RUN", animation.Next);
		Assert.Equal(1u, animation.Layer);
		Assert.Equal(AnimationFlags.Move, animation.Flags);
		Assert.Equal(AnimationDirection.Forward, animation.Direction);
		Assert.Equal(0, animation.FirstFrame);
		Assert.Equal(10, animation.LastFrame);
		Assert.Equal(25f, animation.Fps);
		var tag = Assert.Single(animation.EventTags);
		Assert.Equal(EventType.ComboWindow, tag.Type);
		Assert.Equal(5, tag.Frame);
		Assert.Equal("5 6", tag.Frames);

		var alias = Assert.Single(script.AnimationAliases);
		Assert.Equal("S_RUN_R", alias.Name);
		Assert.Equal("S_RUN", alias.Alias);
		Assert.Equal(AnimationDirection.Backward, alias.Direction);
	}

	private static byte[] BuildAnimation()
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.ASCII, true);

		WriteChunk(writer, 0xA020, chunk =>
		{
			chunk.Write((ushort)12);
			WriteCString(chunk, "S_RUN");
			chunk.Write(1u);
			chunk.Write(2u);
			chunk.Write(1u);
			chunk.Write(10f);
			chunk.Write(10f);
			chunk.Write(0f);
			chunk.Write(1f);
			WriteBox(chunk);
			WriteCString(chunk, "S_RUN");
		});

		WriteChunk(writer, 0xA030, chunk =>
		{
			chunk.Write(1u);
			chunk.Write((uint)AnimationEventType.SoundEffect);
			chunk.Write(4u);
			WriteCString(chunk, "sound");
			for (var i = 0; i < 4; ++i)
			{
				WriteCString(chunk, string.Empty);
			}

			for (var i = 0; i < 4; ++i)
			{
				chunk.Write(0f);
			}

			chunk.Write(1f);
		});

		WriteChunk(writer, 0xA090, chunk =>
		{
			chunk.Write(0x1234u);
			chunk.Write(3u);

			// Two samples: identity rotation, moving position.
			WriteSample(chunk, 0, 100, 200);
			WriteSample(chunk, 300, 400, 500);
		});

		return stream.ToArray();
	}

	private static void WriteSample(BinaryWriter writer, int x, int y, int z)
	{
		writer.Write((ushort)32767);
		writer.Write((ushort)32767);
		writer.Write((ushort)32767);
		writer.Write((ushort)x);
		writer.Write((ushort)y);
		writer.Write((ushort)z);
	}

	private static byte[] BuildScript()
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.ASCII, true);

		WriteChunk(writer, 0xF300, chunk =>
		{
			chunk.Write(0u);
			WriteCString(chunk, "HUMANS");
		});
		WriteChunk(writer, 0xF400, chunk => WriteCString(chunk, "BODY.ASC"));
		WriteChunk(writer, 0xF520, chunk =>
		{
			WriteCString(chunk, "S_RUN");
			chunk.Write(1u);
			WriteCString(chunk, "S_RUN");
			chunk.Write(0.1f);
			chunk.Write(0.2f);
			WriteCString(chunk, "M");
			WriteCString(chunk, string.Empty);
			WriteCString(chunk, "F");
			chunk.Write(0);
			chunk.Write(10);
			chunk.Write(25f);
			chunk.Write(1f);
			chunk.Write(1f);
		});
		WriteChunk(writer, 0xF5A3, chunk =>
		{
			chunk.Write(5);
			WriteCString(chunk, "DEF_WINDOW");
			WriteCString(chunk, "5 6");
		});
		WriteChunk(writer, 0xF530, chunk =>
		{
			WriteCString(chunk, "S_RUN_R");
			chunk.Write(1u);
			WriteCString(chunk, "S_RUN_R");
			chunk.Write(0.1f);
			chunk.Write(0.2f);
			WriteCString(chunk, string.Empty);
			WriteCString(chunk, "S_RUN");
			WriteCString(chunk, "R");
		});
		WriteChunk(writer, 0xFFFF, _ => { });

		return stream.ToArray();
	}

	private static void WriteBox(BinaryWriter writer)
	{
		for (var i = 0; i < 6; ++i)
		{
			writer.Write(0f);
		}
	}

	private static void WriteCString(BinaryWriter writer, string value)
	{
		writer.Write(Encoding.ASCII.GetBytes(value));
		writer.Write((byte)0);
	}

	private static void WriteChunk(BinaryWriter writer, ushort type, Action<BinaryWriter> body)
	{
		using var payload = new MemoryStream();
		using (var payloadWriter = new BinaryWriter(payload, Encoding.ASCII, true))
		{
			body(payloadWriter);
		}

		writer.Write(type);
		writer.Write((uint)payload.Length);
		writer.Write(payload.ToArray());
	}
}
