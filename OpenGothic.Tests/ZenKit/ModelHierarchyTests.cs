using OpenGothic.ZenKit;
using System;
using System.IO;
using System.Numerics;
using System.Text;
using Xunit;

namespace OpenGothic.Tests.ZenKit;

public class ModelHierarchyTests
{
	[Fact]
	public void ReadsNodesAndBindTransforms()
	{
		var data = BuildHierarchy();
		var hierarchy = ModelHierarchy.Load(new MemoryRead(data));

		Assert.Equal(2, hierarchy.Nodes.Count);
		Assert.Equal("root", hierarchy.Nodes[0].Name);
		Assert.Equal(-1, hierarchy.Nodes[0].ParentIndex);
		Assert.Equal("child", hierarchy.Nodes[1].Name);
		Assert.Equal(0, hierarchy.Nodes[1].ParentIndex);

		Assert.Equal(1f, hierarchy.Nodes[0].Transform.M11);
		Assert.Equal(1f, hierarchy.Nodes[0].Transform.M22);
		Assert.Equal(1f, hierarchy.Nodes[0].Transform.M33);
		Assert.Equal(1f, hierarchy.Nodes[0].Transform.M44);

		Assert.Equal(10f, hierarchy.Nodes[1].Transform.M41);
		Assert.Equal(20f, hierarchy.Nodes[1].Transform.M42);
		Assert.Equal(30f, hierarchy.Nodes[1].Transform.M43);
		Assert.Equal(1, hierarchy.FindNode("child"));
		Assert.Equal(-1, hierarchy.FindNode("missing"));

		Assert.Equal(new Vector3(-1, -2, -3), hierarchy.BoundingBox.Min);
		Assert.Equal(new Vector3(1, 2, 3), hierarchy.BoundingBox.Max);
		Assert.Equal(new Vector3(4, 5, 6), hierarchy.RootTranslation);
		Assert.Equal(0x12345678u, hierarchy.Checksum);
	}

	private static byte[] BuildHierarchy()
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.ASCII, true);

		WriteChunk(writer, 0xD100, chunk =>
		{
			chunk.Write(3u);
			chunk.Write((ushort)2);
			WriteNode(chunk, "root", -1, Matrix4x4.Identity);
			WriteNode(chunk, "child", 0, Matrix4x4.CreateTranslation(10, 20, 30));
			WriteBox(chunk, new Vector3(-1, -2, -3), new Vector3(1, 2, 3));
			WriteBox(chunk, new Vector3(-4, -5, -6), new Vector3(4, 5, 6));
			chunk.Write(4f);
			chunk.Write(5f);
			chunk.Write(6f);
			chunk.Write(0x12345678u);
		});

		WriteChunk(writer, 0xD110, chunk =>
		{
			chunk.Write(new byte[16]);
			WriteCString(chunk, "test.msh");
		});

		WriteChunk(writer, 0xD120, _ => { });
		return stream.ToArray();
	}

	private static void WriteNode(BinaryWriter writer, string name, short parent, Matrix4x4 transform)
	{
		WriteCString(writer, name);
		writer.Write(parent);
		writer.Write(transform.M11);
		writer.Write(transform.M12);
		writer.Write(transform.M13);
		writer.Write(transform.M14);
		writer.Write(transform.M21);
		writer.Write(transform.M22);
		writer.Write(transform.M23);
		writer.Write(transform.M24);
		writer.Write(transform.M31);
		writer.Write(transform.M32);
		writer.Write(transform.M33);
		writer.Write(transform.M34);
		writer.Write(transform.M41);
		writer.Write(transform.M42);
		writer.Write(transform.M43);
		writer.Write(transform.M44);
	}

	private static void WriteBox(BinaryWriter writer, Vector3 min, Vector3 max)
	{
		writer.Write(min.X);
		writer.Write(min.Y);
		writer.Write(min.Z);
		writer.Write(max.X);
		writer.Write(max.Y);
		writer.Write(max.Z);
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
