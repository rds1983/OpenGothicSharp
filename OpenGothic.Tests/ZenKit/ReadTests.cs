using OpenGothic.ZenKit;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace OpenGothic.Tests.ZenKit;

public class ReadTests
{
	[Fact]
	public void ReadsLittleEndianPrimitives()
	{
		using var stream = new MemoryStream();
		using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
		{
			writer.Write((byte)0xFE);
			writer.Write((sbyte)-2);
			writer.Write((short)-1234);
			writer.Write((ushort)54321);
			writer.Write(-123456789);
			writer.Write(3456789012u);
			writer.Write(1.5f);
			writer.Write(2.25);
		}

		var read = new MemoryRead(stream.ToArray());

		Assert.Equal(0xFE, read.ReadUByte());
		Assert.Equal(-2, read.ReadByte());
		Assert.Equal(-1234, read.ReadShort());
		Assert.Equal(54321, read.ReadUShort());
		Assert.Equal(-123456789, read.ReadInt());
		Assert.Equal(3456789012u, read.ReadUInt());
		Assert.Equal(1.5f, read.ReadFloat());
		Assert.Equal(2.25, read.ReadDouble());
		Assert.True(read.Eof);
	}

	[Fact]
	public void ReadPastTheEndReturnsZeros()
	{
		var read = new MemoryRead(new byte[] { 1, 2 });

		Assert.Equal(new byte[] { 1, 2, 0, 0 }, read.ReadBytes(4));
		Assert.True(read.Eof);
	}

	[Fact]
	public void SeekIgnoresPositionsOutsideTheBuffer()
	{
		var read = new MemoryRead(new byte[] { 1, 2, 3, 4 });

		read.Seek(10);
		Assert.Equal(0, read.Position);

		read.Seek(2);
		read.Seek(-5, SeekOrigin.Current);
		Assert.Equal(2, read.Position);

		read.Seek(-1, SeekOrigin.End);
		Assert.Equal(3, read.Position);
	}

	[Fact]
	public void ReadStringKeepsTheWholeField()
	{
		var read = new MemoryRead(Encoding.ASCII.GetBytes("NAME      "));

		Assert.Equal("NAME      ", read.ReadString(10));
	}

	[Fact]
	public void ReadFixedStringTrimsTrailingPadding()
	{
		var read = new MemoryRead(Encoding.ASCII.GetBytes("NAME      "));

		Assert.Equal("NAME", read.ReadFixedString(10));
	}

	[Fact]
	public void ReadCStringStopsAtTheTerminator()
	{
		var read = new MemoryRead(Encoding.ASCII.GetBytes("hello\0rest"));

		Assert.Equal("hello", read.ReadCString());
		Assert.Equal(6, read.Position);
	}

	[Fact]
	public void ReadLineSkipsTheFollowingWhitespace()
	{
		var read = new MemoryRead(Encoding.ASCII.GetBytes("abc\r\n   123"));

		Assert.Equal("abc", read.ReadLine(true));
		Assert.Equal("123", read.ReadLine(true));
	}

	[Fact]
	public void MemoryReadHonoursTheSlice()
	{
		var read = new MemoryRead(new byte[] { 9, 1, 2, 3, 9 }, 1, 3);

		Assert.Equal(3, read.Length);
		Assert.Equal(1, read.ReadUByte());
		Assert.Equal(2, read.ReadUByte());
		Assert.Equal(3, read.ReadUByte());
		Assert.True(read.Eof);
	}
}
