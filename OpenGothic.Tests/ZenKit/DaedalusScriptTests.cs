using OpenGothic.ZenKit;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace OpenGothic.Tests.ZenKit;

public class DaedalusScriptTests
{
	[Fact]
	public void ReadsASymbolAndItsCode()
	{
		var data = BuildScript();
		var script = DaedalusScript.Load(new MemoryRead(data));

		Assert.Equal(1u, script.Version);
		Assert.Single(script.Symbols);
		Assert.Equal(1, script.TextSize);

		var symbol = script.Symbols[0];
		Assert.Equal("FOO", symbol.Name);
		Assert.Equal(DaedalusDataType.Int, symbol.Type);
		Assert.Equal(1u, symbol.Count);
		Assert.Equal(42, symbol.GetInt());
		Assert.Equal(-1, symbol.Parent);
		Assert.Equal(0u, symbol.Index);
		Assert.False(symbol.IsMember);
		Assert.False(symbol.IsConst);

		Assert.Equal(0u, script.FindSymbolByName("foo").Index);
		Assert.Null(script.FindSymbolByName("missing"));
		Assert.Equal(DaedalusOpcode.Rsr, script.InstructionAt(0).Op);
	}

	private static byte[] BuildScript()
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.ASCII, true);

		writer.Write((byte)1); // version
		writer.Write(1u); // symbol count
		writer.Write(0u); // sort table

		writer.Write(1u); // name present
		WriteCString(writer, "FOO");
		writer.Write(0u); // vary
		const uint count = 1;
		const uint type = (uint)DaedalusDataType.Int;
		writer.Write(count | (type << 12)); // count + type, no flags
		writer.Write(0u); // file index
		writer.Write(1u); // line start
		writer.Write(1u); // line count
		writer.Write(0u); // char start
		writer.Write(3u); // char count
		writer.Write(42); // value
		writer.Write(-1); // parent

		writer.Write(1u); // text size
		writer.Write((byte)DaedalusOpcode.Rsr);

		return stream.ToArray();
	}

	private static void WriteCString(BinaryWriter writer, string value)
	{
		writer.Write(Encoding.ASCII.GetBytes(value));
		writer.Write((byte)0);
	}
}
