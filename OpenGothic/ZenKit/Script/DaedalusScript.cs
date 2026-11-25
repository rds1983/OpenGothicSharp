using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace OpenGothic.ZenKit;

public enum DaedalusDataType : uint
{
	Void = 0,
	Float = 1,
	Int = 2,
	String = 3,
	Class = 4,
	Function = 5,
	Prototype = 6,
	Instance = 7
}

[Flags]
public enum DaedalusSymbolFlags : uint
{
	None = 0,
	Const = 1,
	Return = 2,
	Member = 4,
	External = 8,
	Merged = 16,
	AccessTrap = 64,
	FuncLocals = 128
}

public enum DaedalusOpcode : byte
{
	Add = 0,
	Sub = 1,
	Mul = 2,
	Div = 3,
	Mod = 4,
	Or = 5,
	AndB = 6,
	Lt = 7,
	Gt = 8,
	MovI = 9,
	Orr = 11,
	And = 12,
	Lsl = 13,
	Lsr = 14,
	Lte = 15,
	Eq = 16,
	Neq = 17,
	Gte = 18,
	AddMovI = 19,
	SubMovI = 20,
	MulMovI = 21,
	DivMovI = 22,
	Plus = 30,
	Negate = 31,
	Not = 32,
	Cmpl = 33,
	Nop = 45,
	Rsr = 60,
	Bl = 61,
	Be = 62,
	PushI = 64,
	PushV = 65,
	PushVI = 67,
	MovS = 70,
	MovSS = 71,
	MovVF = 72,
	MovF = 73,
	MovVI = 74,
	B = 75,
	Bz = 76,
	GMovI = 80,
	PushVV = 245
}

public readonly struct DaedalusInstruction
{
	public DaedalusInstruction(DaedalusOpcode op, int size, uint address, int immediate, uint symbol, byte index)
	{
		Op = op;
		Size = size;
		Address = address;
		Immediate = immediate;
		Symbol = symbol;
		Index = index;
	}

	public DaedalusOpcode Op { get; }
	public int Size { get; }
	public uint Address { get; }
	public int Immediate { get; }
	public uint Symbol { get; }
	public byte Index { get; }
}

/// <summary>A compiled Daedalus symbol from a DAT file.</summary>
public sealed class DaedalusSymbol
{
	public string Name { get; internal set; } = string.Empty;
	public DaedalusDataType Type { get; internal set; }
	public DaedalusSymbolFlags Flags { get; internal set; }
	public uint Count { get; internal set; }
	public uint Index { get; internal set; }
	public int Parent { get; internal set; } = -1;
	public int Address { get; internal set; } = -1;
	public int ClassOffset { get; internal set; } = -1;
	public uint MemberOffset { get; internal set; } = uint.MaxValue;
	public uint ClassSize { get; internal set; } = uint.MaxValue;
	public DaedalusDataType ReturnType { get; internal set; }
	public uint FileIndex { get; internal set; }
	public uint LineStart { get; internal set; }
	public uint LineCount { get; internal set; }
	public uint CharStart { get; internal set; }
	public uint CharCount { get; internal set; }
	public bool Generated { get; internal set; }

	public bool IsConst => (Flags & DaedalusSymbolFlags.Const) != 0;
	public bool IsMember => (Flags & DaedalusSymbolFlags.Member) != 0;
	public bool IsExternal => (Flags & DaedalusSymbolFlags.External) != 0;
	public bool HasReturn => (Flags & DaedalusSymbolFlags.Return) != 0;

	internal float[] Floats;
	internal int[] Ints;
	internal string[] Strings;
	internal DaedalusInstance InstanceValue;

	internal DaedalusClassLayout Layout;
	internal int IntSlot = -1;
	internal int FloatSlot = -1;
	internal int StringSlot = -1;
	internal int InstanceSlot = -1;

	internal void Load(Read reader)
	{
		if (reader.ReadUInt() != 0)
		{
			Name = reader.ReadLine();
			if (Name.Length > 0 && Name[0] == '\xFF')
			{
				Name = "$" + Name.Substring(1);
				Generated = true;
			}
		}

		var vary = reader.ReadUInt();
		var properties = reader.ReadUInt();
		Count = properties & 0xFFF;
		Type = (DaedalusDataType)((properties >> 12) & 0xF);
		Flags = (DaedalusSymbolFlags)((properties >> 16) & 0x3F);

		if (IsMember)
		{
			MemberOffset = vary;
		}
		else if (Type == DaedalusDataType.Class)
		{
			ClassSize = vary;
		}
		else if (Type == DaedalusDataType.Function)
		{
			ReturnType = (DaedalusDataType)vary;
		}

		FileIndex = reader.ReadUInt() & 0x7FFFF;
		LineStart = reader.ReadUInt() & 0x7FFFF;
		LineCount = reader.ReadUInt() & 0x7FFFF;
		CharStart = reader.ReadUInt() & 0xFFFFFF;
		CharCount = reader.ReadUInt() & 0xFFFFFF;

		if (!IsMember)
		{
			switch (Type)
			{
				case DaedalusDataType.Float:
					Floats = new float[Count];
					for (var i = 0; i < Floats.Length; ++i)
					{
						Floats[i] = reader.ReadFloat();
					}

					break;

				case DaedalusDataType.Int:
					Ints = new int[Count];
					for (var i = 0; i < Ints.Length; ++i)
					{
						Ints[i] = reader.ReadInt();
					}

					break;

				case DaedalusDataType.String:
					Strings = new string[Count];
					for (var i = 0; i < Strings.Length; ++i)
					{
						Strings[i] = Unescape(reader.ReadLine());
					}

					break;

				case DaedalusDataType.Class:
					ClassOffset = reader.ReadInt();
					break;

				case DaedalusDataType.Instance:
					Address = reader.ReadInt();
					break;

				case DaedalusDataType.Function:
					if (!IsConst)
					{
						Ints = new int[1];
					}

					Address = reader.ReadInt();
					break;

				case DaedalusDataType.Prototype:
					Address = reader.ReadInt();
					break;
			}
		}

		Parent = reader.ReadInt();
		if (Type == DaedalusDataType.String && !IsMember && IsConst &&
		    Parent >= 0 && char.IsWhiteSpace((char)(Parent & 0xFF)))
		{
			var save = reader.Position;
			var found = false;
			for (var attempt = 4; attempt > 0; --attempt)
			{
				reader.Seek(-3, System.IO.SeekOrigin.Current);
				if (reader.ReadInt() == -1)
				{
					Parent = -1;
					found = true;
					break;
				}
			}

			if (!found)
			{
				reader.Seek(save);
				Parent = reader.ReadInt();
				Log.Warning("DaedalusSymbol: string parent heuristic found no endpoint.");
			}
		}
	}

	public string GetString(int index = 0)
	{
		if (Type != DaedalusDataType.String || Strings == null || index < 0 || index >= Strings.Length)
		{
			throw new DaedalusScriptException($"Illegal string access on '{Name}'.");
		}

		return Strings[index];
	}

	public float GetFloat(int index = 0)
	{
		if (Type != DaedalusDataType.Float || Floats == null || index < 0 || index >= Floats.Length)
		{
			throw new DaedalusScriptException($"Illegal float access on '{Name}'.");
		}

		return Floats[index];
	}

	public int GetInt(int index = 0)
	{
		if ((Type != DaedalusDataType.Int && Type != DaedalusDataType.Function) || Ints == null ||
		    index < 0 || index >= Ints.Length)
		{
			throw new DaedalusScriptException($"Illegal int access on '{Name}'.");
		}

		return Ints[index];
	}

	public string GetString(int index, DaedalusInstance context)
	{
		if (Type != DaedalusDataType.String || index < 0 || index >= Count)
		{
			throw new DaedalusScriptException($"Illegal string access on '{Name}'.");
		}

		if (IsMember)
		{
			if (context == null)
			{
				throw new DaedalusScriptException($"Member '{Name}' is accessed without an instance.");
			}

			return context.GetString(this, index);
		}

		return GetString(index);
	}

	public float GetFloat(int index, DaedalusInstance context)
	{
		if (Type != DaedalusDataType.Float || index < 0 || index >= Count)
		{
			throw new DaedalusScriptException($"Illegal float access on '{Name}'.");
		}

		if (IsMember)
		{
			if (context == null)
			{
				throw new DaedalusScriptException($"Member '{Name}' is accessed without an instance.");
			}

			return context.GetFloat(this, index);
		}

		return GetFloat(index);
	}

	public int GetInt(int index, DaedalusInstance context)
	{
		if ((Type != DaedalusDataType.Int && Type != DaedalusDataType.Function) || index < 0 || index >= Count)
		{
			throw new DaedalusScriptException($"Illegal int access on '{Name}'.");
		}

		if (IsMember)
		{
			if (context == null)
			{
				throw new DaedalusScriptException($"Member '{Name}' is accessed without an instance.");
			}

			return context.GetInt(this, index);
		}

		return GetInt(index);
	}

	public void SetString(string value, int index = 0, DaedalusInstance context = null)
	{
		if (Type != DaedalusDataType.String || index < 0 || index >= Count)
		{
			throw new DaedalusScriptException($"Illegal string access on '{Name}'.");
		}

		if (IsMember)
		{
			if (context == null)
			{
				throw new DaedalusScriptException($"Member '{Name}' is accessed without an instance.");
			}

			context.SetString(this, index, value);
			return;
		}

		Strings[index] = value ?? string.Empty;
	}

	public void SetFloat(float value, int index = 0, DaedalusInstance context = null)
	{
		if (Type != DaedalusDataType.Float || index < 0 || index >= Count)
		{
			throw new DaedalusScriptException($"Illegal float access on '{Name}'.");
		}

		if (IsMember)
		{
			if (context == null)
			{
				throw new DaedalusScriptException($"Member '{Name}' is accessed without an instance.");
			}

			context.SetFloat(this, index, value);
			return;
		}

		Floats[index] = value;
	}

	public void SetInt(int value, int index = 0, DaedalusInstance context = null)
	{
		if ((Type != DaedalusDataType.Int && Type != DaedalusDataType.Function) || index < 0 || index >= Count)
		{
			throw new DaedalusScriptException($"Illegal int access on '{Name}'.");
		}

		if (IsMember)
		{
			if (context == null)
			{
				throw new DaedalusScriptException($"Member '{Name}' is accessed without an instance.");
			}

			context.SetInt(this, index, value);
			return;
		}

		Ints[index] = value;
	}

	internal DaedalusInstance GetInstance()
	{
		if (Type != DaedalusDataType.Instance)
		{
			throw new DaedalusScriptException($"Illegal instance access on '{Name}'.");
		}

		return InstanceValue;
	}

	internal void SetInstance(DaedalusInstance value)
	{
		if (Type != DaedalusDataType.Instance)
		{
			throw new DaedalusScriptException($"Illegal instance access on '{Name}'.");
		}

		InstanceValue = value;
	}

	internal void GrowStrings(int extra)
	{
		var old = Strings ?? Array.Empty<string>();
		var grown = new string[old.Length + extra];
		Array.Copy(old, grown, old.Length);
		Strings = grown;
		Count = (uint)grown.Length;
	}

	private static string Unescape(string value)
	{
		if (value.IndexOf('\\') < 0)
		{
			return value;
		}

		var builder = new StringBuilder(value.Length);
		for (var i = 0; i < value.Length; ++i)
		{
			if (value[i] == '\\' && i + 1 < value.Length)
			{
				switch (value[i + 1])
				{
					case 'n':
						builder.Append('\n');
						++i;
						continue;
					case 't':
						builder.Append('\t');
						++i;
						continue;
				}
			}

			builder.Append(value[i]);
		}

		return builder.ToString();
	}
}

public sealed class DaedalusScriptException : Exception
{
	public DaedalusScriptException(string message) : base(message)
	{
	}
}

/// <summary>
/// Managed Daedalus DAT reader. Ported from ZenKit's <c>DaedalusScript</c>; the VM and the
/// typed instance bindings are built on this symbol table.
/// </summary>
public class DaedalusScript
{
	protected readonly Dictionary<string, uint> _byName = new Dictionary<string, uint>(StringComparer.Ordinal);
	protected readonly Dictionary<uint, uint> _byAddress = new Dictionary<uint, uint>();
	protected byte[] _text = Array.Empty<byte>();

	public List<DaedalusSymbol> Symbols { get; } = new List<DaedalusSymbol>();

	public uint Version { get; private set; }

	public int SymbolCount => Symbols.Count;

	public int TextSize => _text.Length;

	public long TextOffset { get; private set; }

	public static DaedalusScript Load(Read reader)
	{
		var script = new DaedalusScript();
		script.Read(reader);
		return script;
	}

	protected virtual void Read(Read reader)
	{
		Version = reader.ReadUByte();
		var symbolCount = reader.ReadUInt();

		reader.Skip(symbolCount * 4L); // name sort table, unused

		for (var i = 0; i < symbolCount; ++i)
		{
			var symbol = new DaedalusSymbol();
			symbol.Load(reader);
			symbol.Index = (uint)i;

			Symbols.Add(symbol);
			_byName[symbol.Name.ToUpperInvariant()] = (uint)i;

			if (symbol.Type == DaedalusDataType.Prototype || symbol.Type == DaedalusDataType.Instance ||
			    (symbol.Type == DaedalusDataType.Function && symbol.IsConst && !symbol.IsMember))
			{
				_byAddress[(uint)symbol.Address] = (uint)i;
			}
		}

		var textSize = reader.ReadUInt();
		TextOffset = reader.Position;
		_text = reader.ReadBytes((int)textSize);
	}

	public DaedalusSymbol FindSymbolByIndex(uint index)
	{
		return index < Symbols.Count ? Symbols[(int)index] : null;
	}

	public DaedalusSymbol FindSymbolByName(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}

		return _byName.TryGetValue(name.ToUpperInvariant(), out var index)
			? Symbols[(int)index]
			: null;
	}

	public DaedalusSymbol FindSymbolByAddress(uint address)
	{
		return _byAddress.TryGetValue(address, out var index) ? Symbols[(int)index] : null;
	}

	public DaedalusSymbol GetSymbolByIndex(int index) => FindSymbolByIndex((uint)index);

	public DaedalusSymbol GetSymbolByName(string name) => FindSymbolByName(name);

	public DaedalusSymbol GetSymbolByAddress(int address) => FindSymbolByAddress((uint)address);

	public List<DaedalusSymbol> GetInstanceSymbols(string className)
	{
		var result = new List<DaedalusSymbol>();
		EnumerateInstancesByClassName(className, result.Add);
		return result;
	}

	public DaedalusInstruction InstructionAt(uint address)
	{
		if (address >= _text.Length)
		{
			throw new DaedalusScriptException($"Instruction address {address} is outside the text.");
		}

		var text = _text;
		var offset = (int)address;
		var op = (DaedalusOpcode)text[offset];
		var size = 1;
		uint target = 0;
		int immediate = 0;
		uint symbol = 0;
		byte index = 0;

		switch (op)
		{
			case DaedalusOpcode.Bl:
			case DaedalusOpcode.Bz:
			case DaedalusOpcode.B:
				target = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(offset + 1, 4));
				size += 4;
				break;
			case DaedalusOpcode.PushI:
				immediate = BinaryPrimitives.ReadInt32LittleEndian(text.AsSpan(offset + 1, 4));
				size += 4;
				break;
			case DaedalusOpcode.Be:
			case DaedalusOpcode.PushV:
			case DaedalusOpcode.PushVI:
			case DaedalusOpcode.GMovI:
				symbol = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(offset + 1, 4));
				size += 4;
				break;
			case DaedalusOpcode.PushVV:
				symbol = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(offset + 1, 4));
				index = text[offset + 5];
				size += 5;
				break;
		}

		return new DaedalusInstruction(op, size, target, immediate, symbol, index);
	}

	public List<DaedalusSymbol> FindClassMembers(DaedalusSymbol cls)
	{
		var result = new List<DaedalusSymbol>();
		foreach (var symbol in Symbols)
		{
			if (symbol.IsMember && symbol.Parent == cls.Index)
			{
				result.Add(symbol);
			}
		}

		return result;
	}

	public List<DaedalusSymbol> FindParametersForFunction(DaedalusSymbol parent)
	{
		var start = parent.Index + 1;
		var count = parent.Count;
		if (start + count > Symbols.Count)
		{
			return new List<DaedalusSymbol>();
		}

		return Symbols.GetRange((int)start, (int)count);
	}

	public List<DaedalusSymbol> FindLocalsForFunction(DaedalusSymbol parent)
	{
		var first = parent.Index + 1 + parent.Count;
		if (first >= Symbols.Count)
		{
			return new List<DaedalusSymbol>();
		}

		for (var i = (int)first; i < Symbols.Count; ++i)
		{
			var name = Symbols[i].Name;
			if (name.StartsWith(parent.Name, StringComparison.Ordinal) &&
			    name.Length > parent.Name.Length && name[parent.Name.Length] == '.')
			{
				continue;
			}

			return Symbols.GetRange((int)first, i - (int)first);
		}

		return Symbols.GetRange((int)first, Symbols.Count - (int)first);
	}

	public void EnumerateInstancesByClassName(string name, Action<DaedalusSymbol> callback)
	{
		var cls = FindSymbolByName(name);
		if (cls == null)
		{
			return;
		}

		var prototypes = new HashSet<int>();
		foreach (var symbol in Symbols)
		{
			if (symbol.Type == DaedalusDataType.Prototype && symbol.Parent == cls.Index)
			{
				prototypes.Add((int)symbol.Index);
			}
			else if (symbol.Type == DaedalusDataType.Instance && symbol.IsConst &&
			         (prototypes.Contains(symbol.Parent) || symbol.Parent == cls.Index))
			{
				callback(symbol);
			}
		}
	}
}
