using OpenGothic.ZenKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace OpenGothic.Tests.ZenKit;

public class DaedalusVmTests
{
	[Fact]
	public void RunsFunctionsMembersAndExternals()
	{
		var data = BuildScript();
		var vm = new DaedalusVm(new MemoryRead(data));
		vm.RegisterExternalDefault((machine, symbol) => { });

		vm.PushInt(2);
		vm.PushInt(3);
		Assert.Equal(5, vm.Call<int>("MAIN"));

		vm.Call("SET_RESULT");
		Assert.Equal(0, vm.GetSymbolByName("RESULT").GetInt());

		vm.RegisterExternal("EXTERNAL", (DaedalusVm.ExternalFunc<int, int>)(value => value * 2));
		vm.Call("SET_RESULT");
		Assert.Equal(10, vm.GetSymbolByName("RESULT").GetInt());

		var instance = vm.InitInstance<TestInstance>(vm.GetSymbolByName("TEST_INSTANCE"));
		Assert.Equal(42, instance.Value);
		Assert.Equal(9, instance.Index);
		Assert.Equal("C_TEST", instance.ClassName);
	}

	[Fact]
	public void UnboundExternalPopsItsArguments()
	{
		var vm = new DaedalusVm(new MemoryRead(BuildScript()));
		var calls = 0;
		vm.RegisterExternalDefault((machine, symbol) =>
		{
			++calls;
			Assert.Equal("EXTERNAL", symbol.Name);
		});

		vm.Call("SET_RESULT");
		Assert.Equal(1, calls);
		Assert.Equal(0, vm.GetSymbolByName("RESULT").GetInt());
	}

	private sealed class TestInstance : DaedalusInstance
	{
		internal override string ExpectedClassName => "C_TEST";

		public int Value
		{
			get => GetInt("VALUE");
			set => SetInt("VALUE", value);
		}
	}

	private static byte[] BuildScript()
	{
		const int mainAddress = 0;
		const int setResultAddress = mainAddress + 24;
		const int instanceAddress = setResultAddress + 17;

		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.ASCII, true);

		writer.Write((byte)1); // version
		writer.Write(10u); // symbol count
		writer.Write(new byte[10 * 4]); // sort table

		WriteFunction(writer, "MAIN", 2, DaedalusDataType.Int, mainAddress,
			DaedalusSymbolFlags.Const | DaedalusSymbolFlags.Return, -1);
		WriteInt(writer, "MAIN.P0", 0, -1);
		WriteInt(writer, "MAIN.P1", 0, -1);
		WriteFunction(writer, "SET_RESULT", 0, DaedalusDataType.Void, setResultAddress,
			DaedalusSymbolFlags.Const, -1);
		WriteFunction(writer, "EXTERNAL", 1, DaedalusDataType.Int, 1000,
			DaedalusSymbolFlags.External | DaedalusSymbolFlags.Return, -1);
		WriteInt(writer, "EXTERNAL.P0", 0, -1);
		WriteInt(writer, "RESULT", 10, -1);
		WriteClass(writer, "C_TEST", 7);
		WriteMember(writer, "C_TEST.VALUE", DaedalusDataType.Int, 1, 0, 7);
		WriteInstance(writer, "TEST_INSTANCE", instanceAddress, 7);

		var text = new List<byte>();
		Emit(text, DaedalusOpcode.PushV, 2u);
		Emit(text, DaedalusOpcode.MovI);
		Emit(text, DaedalusOpcode.PushV, 1u);
		Emit(text, DaedalusOpcode.MovI);
		Emit(text, DaedalusOpcode.PushV, 1u);
		Emit(text, DaedalusOpcode.PushV, 2u);
		Emit(text, DaedalusOpcode.Add);
		Emit(text, DaedalusOpcode.Rsr);

		Emit(text, DaedalusOpcode.PushI, 5);
		Emit(text, DaedalusOpcode.Be, 4u);
		Emit(text, DaedalusOpcode.PushV, 6u);
		Emit(text, DaedalusOpcode.MovI);
		Emit(text, DaedalusOpcode.Rsr);

		Emit(text, DaedalusOpcode.PushI, 42);
		Emit(text, DaedalusOpcode.PushV, 8u);
		Emit(text, DaedalusOpcode.MovI);
		Emit(text, DaedalusOpcode.Rsr);

		writer.Write((uint)text.Count);
		writer.Write(text.ToArray());

		return stream.ToArray();
	}

	private static void Emit(List<byte> text, DaedalusOpcode opcode)
	{
		text.Add((byte)opcode);
	}

	private static void Emit(List<byte> text, DaedalusOpcode opcode, int value)
	{
		text.Add((byte)opcode);
		text.AddRange(BitConverter.GetBytes(value));
	}

	private static void Emit(List<byte> text, DaedalusOpcode opcode, uint value)
	{
		text.Add((byte)opcode);
		text.AddRange(BitConverter.GetBytes(value));
	}

	private static void WriteFunction(BinaryWriter writer, string name, uint parameterCount,
		DaedalusDataType returnType, int address, DaedalusSymbolFlags flags, int parent)
	{
		writer.Write(1u);
		WriteCString(writer, name);
		writer.Write((uint)returnType);
		writer.Write(parameterCount | ((uint)DaedalusDataType.Function << 12) | ((uint)flags << 16));
		WriteSourcePosition(writer);
		writer.Write(address);
		writer.Write(parent);
	}

	private static void WriteInt(BinaryWriter writer, string name, int value, int parent)
	{
		writer.Write(1u);
		WriteCString(writer, name);
		writer.Write(0u);
		writer.Write(1u | ((uint)DaedalusDataType.Int << 12));
		WriteSourcePosition(writer);
		writer.Write(value);
		writer.Write(parent);
	}

	private static void WriteClass(BinaryWriter writer, string name, uint classSize)
	{
		writer.Write(1u);
		WriteCString(writer, name);
		writer.Write(classSize);
		writer.Write((uint)DaedalusDataType.Class << 12);
		WriteSourcePosition(writer);
		writer.Write(0);
		writer.Write(-1);
	}

	private static void WriteMember(BinaryWriter writer, string name, DaedalusDataType type,
		uint count, uint offset, int parent)
	{
		writer.Write(1u);
		WriteCString(writer, name);
		writer.Write(offset);
		writer.Write(count | ((uint)type << 12) | ((uint)DaedalusSymbolFlags.Member << 16));
		WriteSourcePosition(writer);
		writer.Write(parent);
	}

	private static void WriteInstance(BinaryWriter writer, string name, int address, int parent)
	{
		writer.Write(1u);
		WriteCString(writer, name);
		writer.Write(0u);
		writer.Write((uint)DaedalusDataType.Instance << 12);
		WriteSourcePosition(writer);
		writer.Write(address);
		writer.Write(parent);
	}

	private static void WriteSourcePosition(BinaryWriter writer)
	{
		writer.Write(0u);
		writer.Write(1u);
		writer.Write(1u);
		writer.Write(0u);
		writer.Write(1u);
	}

	private static void WriteCString(BinaryWriter writer, string value)
	{
		writer.Write(Encoding.ASCII.GetBytes(value));
		writer.Write((byte)0);
	}
}
