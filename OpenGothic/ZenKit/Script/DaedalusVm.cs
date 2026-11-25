using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace OpenGothic.ZenKit;

public static class DaedalusExecutionFlags
{
	public const byte Default = 0;
	public const byte AllowNullInstanceAccess = 1 << 1;
	public const byte IgnoreConstSpecifier = 1 << 2;
}

/// <summary>
/// The Daedalus virtual machine: stack, call frames, opcodes, globals and the external-call
/// ABI. This is a C# port of ZenKit's <c>DaedalusVm</c>; the symbol table comes from
/// <see cref="DaedalusScript"/>.
/// </summary>
public sealed class DaedalusVm : DaedalusScript
{
	public delegate void ExternalDefaultFunction(DaedalusVm vm, DaedalusSymbol sym);

	public delegate TR ExternalFunc<out TR>();

	public delegate TR ExternalFunc<out TR, in TP0>(TP0 p0);

	public delegate TR ExternalFunc<out TR, in TP0, in TP1>(TP0 p0, TP1 p1);

	public delegate TR ExternalFunc<out TR, in TP0, in TP1, in TP2>(TP0 p0, TP1 p1, TP2 p2);

	public delegate TR ExternalFunc<out TR, in TP0, in TP1, in TP2, in TP3>(TP0 p0, TP1 p1, TP2 p2, TP3 p3);

	public delegate TR ExternalFunc<out TR, in TP0, in TP1, in TP2, in TP3, in TP4>(TP0 p0, TP1 p1, TP2 p2,
		TP3 p3, TP4 p4);

	public delegate TR ExternalFunc<out TR, in TP0, in TP1, in TP2, in TP3, in TP4, in TP5>(TP0 p0, TP1 p1,
		TP2 p2, TP3 p3, TP4 p4, TP5 p5);

	public delegate TR ExternalFunc<out TR, in TP0, in TP1, in TP2, in TP3, in TP4, in TP5, in TP6>(TP0 p0, TP1 p1,
		TP2 p2, TP3 p3, TP4 p4, TP5 p5, TP6 p6);

	public delegate TR ExternalFunc<out TR, in TP0, in TP1, in TP2, in TP3, in TP4, in TP5, in TP6, in TP7>(TP0 p0,
		TP1 p1, TP2 p2, TP3 p3, TP4 p4, TP5 p5, TP6 p6, TP7 p7);

	public delegate TR ExternalFunc<out TR, in TP0, in TP1, in TP2, in TP3, in TP4, in TP5, in TP6, in TP7, in TP8>(
		TP0 p0, TP1 p1, TP2 p2, TP3 p3, TP4 p4, TP5 p5, TP6 p6, TP7 p7, TP8 p8);

	public delegate void ExternalFuncV();

	public delegate void ExternalFuncV<in TP0>(TP0 p0);

	public delegate void ExternalFuncV<in TP0, in TP1>(TP0 p0, TP1 p1);

	public delegate void ExternalFuncV<in TP0, in TP1, in TP2>(TP0 p0, TP1 p1, TP2 p2);

	public delegate void ExternalFuncV<in TP0, in TP1, in TP2, in TP3>(TP0 p0, TP1 p1, TP2 p2, TP3 p3);

	public delegate void ExternalFuncV<in TP0, in TP1, in TP2, in TP3, in TP4>(TP0 p0, TP1 p1, TP2 p2, TP3 p3,
		TP4 p4);

	public delegate void ExternalFuncV<in TP0, in TP1, in TP2, in TP3, in TP4, in TP5>(TP0 p0, TP1 p1, TP2 p2,
		TP3 p3, TP4 p4, TP5 p5);

	public delegate void ExternalFuncV<in TP0, in TP1, in TP2, in TP3, in TP4, in TP5, in TP6>(TP0 p0, TP1 p1,
		TP2 p2, TP3 p3, TP4 p4, TP5 p5, TP6 p6);

	public delegate void ExternalFuncV<in TP0, in TP1, in TP2, in TP3, in TP4, in TP5, in TP6, in TP7>(TP0 p0,
		TP1 p1, TP2 p2, TP3 p3, TP4 p4, TP5 p5, TP6 p6, TP7 p7);

	public delegate void ExternalFuncV<in TP0, in TP1, in TP2, in TP3, in TP4, in TP5, in TP6, in TP7, in TP8>(
		TP0 p0, TP1 p1, TP2 p2, TP3 p3, TP4 p4, TP5 p5, TP6 p6, TP7 p7, TP8 p8);

	private const int StackSize = 2048;

	private enum FrameKind : byte
	{
		Int,
		Float,
		Instance,
		Reference
	}

	private struct StackFrame
	{
		public FrameKind Kind;
		public DaedalusInstance Context;
		public int Index;
		public int Int;
		public float Float;
		public DaedalusSymbol Symbol;
		public DaedalusInstance Instance;
	}

	private struct CallFrame
	{
		public DaedalusSymbol Function;
		public uint ProgramCounter;
		public int StackPtr;
		public DaedalusInstance Context;
	}

	private readonly StackFrame[] _stack = new StackFrame[StackSize];
	private readonly List<CallFrame> _callStack = new List<CallFrame>(64);
	private readonly Dictionary<DaedalusSymbol, Action<DaedalusVm>> _externals =
		new Dictionary<DaedalusSymbol, Action<DaedalusVm>>();
	private readonly Dictionary<uint, DaedalusClassLayout> _layouts = new Dictionary<uint, DaedalusClassLayout>();
	private readonly List<int> _freeStrings = new List<int>(64);

	private ExternalDefaultFunction _defaultExternal;
	private DaedalusSymbol _selfSym;
	private DaedalusSymbol _otherSym;
	private DaedalusSymbol _victimSym;
	private DaedalusSymbol _heroSym;
	private DaedalusSymbol _itemSym;
	private DaedalusSymbol _temporaryStrings;
	private DaedalusInstance _instance;
	private uint _pc;
	private int _stackPtr;
	private readonly byte _flags;

	public DaedalusVm(string path) : this(new MemoryRead(File.ReadAllBytes(path)))
	{
	}

	public DaedalusVm(Read reader, byte flags = DaedalusExecutionFlags.Default)
	{
		_flags = flags;
		Read(reader);
		Initialize();
	}

	public uint ProgramCounter => _pc;

	public bool IsTopOfStackReference => _stackPtr > 0 && _stack[_stackPtr - 1].Kind == FrameKind.Reference;

	public DaedalusInstance GlobalSelf
	{
		get => _selfSym?.GetInstance();
		set => _selfSym?.SetInstance(value);
	}

	public DaedalusInstance GlobalOther
	{
		get => _otherSym?.GetInstance();
		set => _otherSym?.SetInstance(value);
	}

	public DaedalusInstance GlobalVictim
	{
		get => _victimSym?.GetInstance();
		set => _victimSym?.SetInstance(value);
	}

	public DaedalusInstance GlobalHero
	{
		get => _heroSym?.GetInstance();
		set => _heroSym?.SetInstance(value);
	}

	public DaedalusInstance GlobalItem
	{
		get => _itemSym?.GetInstance();
		set => _itemSym?.SetInstance(value);
	}

	private void Initialize()
	{
		_temporaryStrings = new DaedalusSymbol
		{
			Name = "$PHOENIX_FAKE_STRINGS",
			Type = DaedalusDataType.String,
			Count = 1,
			Index = (uint)Symbols.Count,
			Generated = true,
			Strings = new string[1]
		};
		Symbols.Add(_temporaryStrings);
		_freeStrings.Add(0);

		BuildLayouts();

		_selfSym = FindSymbolByName("SELF");
		_otherSym = FindSymbolByName("OTHER");
		_victimSym = FindSymbolByName("VICTIM");
		_heroSym = FindSymbolByName("HERO");
		_itemSym = FindSymbolByName("ITEM");
	}

	private void BuildLayouts()
	{
		foreach (var symbol in Symbols)
		{
			if (symbol.Type == DaedalusDataType.Class)
			{
				_layouts[symbol.Index] = new DaedalusClassLayout(symbol);
			}
		}

		foreach (var symbol in Symbols)
		{
			if (symbol.IsMember && _layouts.TryGetValue((uint)symbol.Parent, out var layout))
			{
				layout.Add(symbol);
			}
		}
	}

	private DaedalusSymbol ResolveClass(DaedalusSymbol symbol)
	{
		var parent = FindSymbolByIndex((uint)symbol.Parent);
		while (parent != null && parent.Type != DaedalusDataType.Class)
		{
			parent = FindSymbolByIndex((uint)parent.Parent);
		}

		return parent;
	}

	public T InitInstance<T>(string name) where T : DaedalusInstance, new()
	{
		var symbol = FindSymbolByName(name);
		if (symbol == null)
		{
			throw new DaedalusScriptException($"Symbol '{name}' not found.");
		}

		return InitInstance<T>(symbol);
	}

	public T InitInstance<T>(DaedalusSymbol symbol) where T : DaedalusInstance, new()
	{
		if (symbol == null)
		{
			throw new DaedalusScriptException("Cannot initialize instance: symbol not found.");
		}

		if (symbol.Type != DaedalusDataType.Instance)
		{
			throw new DaedalusScriptException($"Cannot initialize '{symbol.Name}': not an instance.");
		}

		var cls = ResolveClass(symbol);
		if (cls == null)
		{
			throw new DaedalusScriptException($"Cannot initialize '{symbol.Name}': parent class not found.");
		}

		if (!_layouts.TryGetValue(cls.Index, out var layout))
		{
			throw new DaedalusScriptException($"Cannot initialize '{symbol.Name}': no layout for class '{cls.Name}'.");
		}

		var instance = new T();
		var expected = instance.ExpectedClassName;
		if (!string.IsNullOrEmpty(expected) && !string.Equals(expected, cls.Name, StringComparison.OrdinalIgnoreCase))
		{
			throw new DaedalusScriptException(
				$"Cannot initialize '{symbol.Name}': class '{cls.Name}' is not '{expected}'.");
		}

		instance.Allocate(this, symbol, cls, layout);
		symbol.SetInstance(instance);

		var oldInstance = _instance;
		var oldSelf = _selfSym?.GetInstance();
		_instance = instance;
		_selfSym?.SetInstance(instance);
		try
		{
			UnsafeCall(symbol);
		}
		finally
		{
			_instance = oldInstance;
			_selfSym?.SetInstance(oldSelf);
		}

		return instance;
	}

	public DaedalusInstance InitOpaqueInstance(DaedalusSymbol symbol)
	{
		return InitInstance<DaedalusInstance>(symbol);
	}

	public void Call(string name)
	{
		var symbol = FindSymbolByName(name);
		if (symbol == null || symbol.Type != DaedalusDataType.Function)
		{
			throw new DaedalusScriptException($"Function '{name}' not found.");
		}

		Call(symbol);
	}

	public void Call(int symbolIndex)
	{
		var symbol = FindSymbolByIndex((uint)symbolIndex);
		if (symbol == null || symbol.Type != DaedalusDataType.Function)
		{
			throw new DaedalusScriptException($"Function symbol {symbolIndex} not found.");
		}

		Call(symbol);
	}

	public void Call(uint symbolIndex) => Call((int)symbolIndex);

	public void Call(DaedalusSymbol symbol)
	{
		if (symbol == null || symbol.Type != DaedalusDataType.Function)
		{
			throw new DaedalusScriptException($"Symbol '{symbol?.Name}' is not a function.");
		}

		UnsafeCall(symbol);
		if (symbol.HasReturn && _stackPtr > 0)
		{
			--_stackPtr;
		}
	}

	public T Call<T>(string name)
	{
		var symbol = FindSymbolByName(name);
		if (symbol == null || symbol.Type != DaedalusDataType.Function)
		{
			throw new DaedalusScriptException($"Function '{name}' not found.");
		}

		return Call<T>(symbol);
	}

	public T Call<T>(int symbolIndex)
	{
		var symbol = FindSymbolByIndex((uint)symbolIndex);
		if (symbol == null || symbol.Type != DaedalusDataType.Function)
		{
			throw new DaedalusScriptException($"Function symbol {symbolIndex} not found.");
		}

		return Call<T>(symbol);
	}

	public T Call<T>(uint symbolIndex) => Call<T>((int)symbolIndex);

	public T Call<T>(DaedalusSymbol symbol)
	{
		if (symbol == null || symbol.Type != DaedalusDataType.Function)
		{
			throw new DaedalusScriptException($"Symbol '{symbol?.Name}' is not a function.");
		}

		if (!symbol.HasReturn)
		{
			throw new DaedalusScriptException($"Function '{symbol.Name}' does not return a value.");
		}

		UnsafeCall(symbol);
		return Pop<T>();
	}

	private void UnsafeCall(DaedalusSymbol symbol)
	{
		PushCall(symbol);
		Jump((uint)symbol.Address);
		try
		{
			while (Exec())
			{
			}
		}
		finally
		{
			PopCall();
		}
	}

	private void Jump(uint address)
	{
		if (address >= (uint)TextSize)
		{
			throw new DaedalusScriptException($"Cannot jump to {address}: illegal address.");
		}

		_pc = address;
	}

	private bool Exec()
	{
		var instruction = InstructionAt(_pc);

		switch (instruction.Op)
		{
			case DaedalusOpcode.Add:
				PushInt(PopInt() + PopInt());
				break;
			case DaedalusOpcode.Sub:
			{
				var a = PopInt();
				var b = PopInt();
				PushInt(a - b);
				break;
			}
			case DaedalusOpcode.Mul:
				PushInt(PopInt() * PopInt());
				break;
			case DaedalusOpcode.Div:
			{
				var a = PopInt();
				var b = PopInt();
				if (b == 0)
				{
					throw new DaedalusScriptException("vm: division by zero.");
				}

				PushInt(a / b);
				break;
			}
			case DaedalusOpcode.Mod:
			{
				var a = PopInt();
				var b = PopInt();
				if (b == 0)
				{
					throw new DaedalusScriptException("vm: division by zero.");
				}

				PushInt(a % b);
				break;
			}
			case DaedalusOpcode.Or:
				PushInt(PopInt() | PopInt());
				break;
			case DaedalusOpcode.AndB:
				PushInt(PopInt() & PopInt());
				break;
			case DaedalusOpcode.Lt:
			{
				var a = PopInt();
				var b = PopInt();
				PushInt(a < b ? 1 : 0);
				break;
			}
			case DaedalusOpcode.Gt:
			{
				var a = PopInt();
				var b = PopInt();
				PushInt(a > b ? 1 : 0);
				break;
			}
			case DaedalusOpcode.Lsl:
			{
				var a = PopInt();
				var b = PopInt();
				PushInt(a << b);
				break;
			}
			case DaedalusOpcode.Lsr:
			{
				var a = PopInt();
				var b = PopInt();
				PushInt(a >> b);
				break;
			}
			case DaedalusOpcode.Lte:
			{
				var a = PopInt();
				var b = PopInt();
				PushInt(a <= b ? 1 : 0);
				break;
			}
			case DaedalusOpcode.Eq:
				PushInt(PopInt() == PopInt() ? 1 : 0);
				break;
			case DaedalusOpcode.Neq:
				PushInt(PopInt() != PopInt() ? 1 : 0);
				break;
			case DaedalusOpcode.Gte:
			{
				var a = PopInt();
				var b = PopInt();
				PushInt(a >= b ? 1 : 0);
				break;
			}
			case DaedalusOpcode.Plus:
				PushInt(+PopInt());
				break;
			case DaedalusOpcode.Negate:
				PushInt(-PopInt());
				break;
			case DaedalusOpcode.Not:
				PushInt(PopInt() == 0 ? 1 : 0);
				break;
			case DaedalusOpcode.Cmpl:
				PushInt(~PopInt());
				break;
			case DaedalusOpcode.Orr:
			{
				var a = PopInt();
				var b = PopInt();
				PushInt(a != 0 || b != 0 ? 1 : 0);
				break;
			}
			case DaedalusOpcode.And:
			{
				var a = PopInt();
				var b = PopInt();
				PushInt(a != 0 && b != 0 ? 1 : 0);
				break;
			}
			case DaedalusOpcode.Nop:
				break;
			case DaedalusOpcode.Rsr:
				return false;
			case DaedalusOpcode.Bl:
			{
				var symbol = FindSymbolByAddress(instruction.Address);
				if (symbol == null)
				{
					throw new DaedalusScriptException($"bl: no symbol found for address {instruction.Address}.");
				}

				UnsafeCall(symbol);
				break;
			}
			case DaedalusOpcode.Be:
			{
				var symbol = FindSymbolByIndex(instruction.Symbol);
				if (symbol == null)
				{
					throw new DaedalusScriptException("be: no external found for symbol.");
				}

				if (_externals.TryGetValue(symbol, out var callback))
				{
					PushCall(symbol);
					try
					{
						callback(this);
					}
					finally
					{
						PopCall();
					}
				}
				else if (_defaultExternal != null)
				{
					PopParameters(symbol);
					PushDefault(symbol.ReturnType);
					_defaultExternal(this, symbol);
				}
				else
				{
					throw new DaedalusScriptException($"be: no external registered for '{symbol.Name}'.");
				}

				break;
			}
			case DaedalusOpcode.PushI:
				PushInt(instruction.Immediate);
				break;
			case DaedalusOpcode.PushVI:
			case DaedalusOpcode.PushV:
			{
				var symbol = FindSymbolByIndex(instruction.Symbol);
				if (symbol == null)
				{
					throw new DaedalusScriptException("pushv: no symbol found for index.");
				}

				PushReference(symbol, 0);
				break;
			}
			case DaedalusOpcode.MovI:
			case DaedalusOpcode.MovVF:
			{
				PopReference(out var symbol, out var index, out var context);
				var value = PopInt();
				SetInt(context, symbol, index, value);
				break;
			}
			case DaedalusOpcode.MovF:
			{
				PopReference(out var symbol, out var index, out var context);
				var value = PopFloat();
				SetFloat(context, symbol, index, value);
				break;
			}
			case DaedalusOpcode.MovS:
			{
				PopReference(out var symbol, out var index, out var context);
				var value = PopString();
				SetString(context, symbol, index, value);
				break;
			}
			case DaedalusOpcode.MovSS:
				throw new DaedalusScriptException("not implemented: movss.");
			case DaedalusOpcode.AddMovI:
				AddToMember(1);
				break;
			case DaedalusOpcode.SubMovI:
				AddToMember(-1);
				break;
			case DaedalusOpcode.MulMovI:
				MulMember();
				break;
			case DaedalusOpcode.DivMovI:
				DivMember();
				break;
			case DaedalusOpcode.MovVI:
			{
				PopReference(out var symbol, out var index, out var context);
				var value = PopInstance();
				if (symbol.IsMember && context != null)
				{
					context.SetInstance(symbol, index, value);
				}
				else
				{
					symbol.SetInstance(value);
				}

				break;
			}
			case DaedalusOpcode.B:
				Jump(instruction.Address);
				return true;
			case DaedalusOpcode.Bz:
				if (PopInt() == 0)
				{
					Jump(instruction.Address);
					return true;
				}

				break;
			case DaedalusOpcode.GMovI:
			{
				var symbol = FindSymbolByIndex(instruction.Symbol);
				if (symbol == null)
				{
					throw new DaedalusScriptException("gmovi: no symbol found for index.");
				}

				_instance = symbol.GetInstance();
				break;
			}
			case DaedalusOpcode.PushVV:
			{
				var symbol = FindSymbolByIndex(instruction.Symbol);
				if (symbol == null)
				{
					throw new DaedalusScriptException("pushvv: no symbol found for index.");
				}

				PushReference(symbol, instruction.Index);
				break;
			}
			default:
				throw new DaedalusScriptException($"Unsupported opcode {instruction.Op}.");
		}

		_pc += (uint)instruction.Size;
		return true;
	}

	private void AddToMember(int sign)
	{
		PopReference(out var symbol, out var index, out var context);
		var value = PopInt();

		CheckConst(symbol);
		if (!symbol.IsMember || context != null || !AllowNullInstanceAccess)
		{
			SetInt(context, symbol, index, GetInt(context, symbol, index) + sign * value);
		}
	}

	private void MulMember()
	{
		PopReference(out var symbol, out var index, out var context);
		var value = PopInt();

		CheckConst(symbol);
		if (!symbol.IsMember || context != null || !AllowNullInstanceAccess)
		{
			SetInt(context, symbol, index, GetInt(context, symbol, index) * value);
		}
	}

	private void DivMember()
	{
		PopReference(out var symbol, out var index, out var context);
		var value = PopInt();

		if (value == 0)
		{
			throw new DaedalusScriptException("vm: division by zero.");
		}

		CheckConst(symbol);
		if (!symbol.IsMember || context != null || !AllowNullInstanceAccess)
		{
			SetInt(context, symbol, index, GetInt(context, symbol, index) / value);
		}
	}

	private bool AllowNullInstanceAccess => (_flags & DaedalusExecutionFlags.AllowNullInstanceAccess) != 0;

	private void CheckConst(DaedalusSymbol symbol)
	{
		if (symbol.IsConst && (_flags & DaedalusExecutionFlags.IgnoreConstSpecifier) == 0)
		{
			throw new DaedalusScriptException($"Cannot write to constant '{symbol.Name}'.");
		}
	}

	private void PushCall(DaedalusSymbol symbol)
	{
		if (LocalVariablesEnabled(symbol))
		{
			PushLocalVariables(symbol);
		}

		var parameters = (int)symbol.Count;
		_callStack.Add(new CallFrame
		{
			Function = symbol,
			ProgramCounter = _pc,
			StackPtr = _stackPtr - parameters,
			Context = _instance
		});
	}

	private void PopCall()
	{
		var call = _callStack[^1];

		if (!call.Function.HasReturn)
		{
			_stackPtr = call.StackPtr;
		}
		else
		{
			var remaining = _stackPtr - call.StackPtr;
			if (remaining == 0)
			{
				PushDefault(call.Function.ReturnType);
			}
			else if (remaining > 1)
			{
				var result = _stack[_stackPtr - 1];
				_stackPtr = call.StackPtr;
				_stack[_stackPtr++] = result;
			}
		}

		if (LocalVariablesEnabled(call.Function))
		{
			PopLocalVariables(call.Function);
		}

		_pc = call.ProgramCounter;
		_instance = call.Context;
		_callStack.RemoveAt(_callStack.Count - 1);
	}

	private static bool LocalVariablesEnabled(DaedalusSymbol symbol) =>
		(symbol.Flags & DaedalusSymbolFlags.FuncLocals) != 0;

	private void PushLocalVariables(DaedalusSymbol symbol)
	{
		var recursive = false;
		foreach (var frame in _callStack)
		{
			if (ReferenceEquals(frame.Function, symbol))
			{
				recursive = true;
				break;
			}
		}

		if (!recursive)
		{
			return;
		}

		var parameters = (int)symbol.Count;
		var locals = FindLocalsForFunction(symbol);
		var localsSize = 0;
		foreach (var local in locals)
		{
			switch (local.Type)
			{
				case DaedalusDataType.Float:
				case DaedalusDataType.Function:
				case DaedalusDataType.Int:
				case DaedalusDataType.String:
					localsSize += (int)local.Count;
					break;
				case DaedalusDataType.Instance:
					++localsSize;
					break;
			}
		}

		if (_stackPtr + localsSize > StackSize)
		{
			throw new DaedalusScriptException("stack overflow.");
		}

		if (_stackPtr < parameters)
		{
			throw new DaedalusScriptException("stack underflow.");
		}

		_stackPtr -= parameters;
		for (var i = 0; i < parameters; ++i)
		{
			_stack[_stackPtr + localsSize + i] = _stack[_stackPtr + i];
		}

		foreach (var local in locals)
		{
			switch (local.Type)
			{
				case DaedalusDataType.Float:
					for (var i = 0; i < local.Count; ++i)
					{
						PushFloat(local.GetFloat(i));
					}

					break;
				case DaedalusDataType.Function:
				case DaedalusDataType.Int:
					for (var i = 0; i < local.Count; ++i)
					{
						PushInt(local.GetInt(i));
					}

					break;
				case DaedalusDataType.String:
					for (var i = 0; i < local.Count; ++i)
					{
						PushString(local.GetString(i));
					}

					break;
				case DaedalusDataType.Instance:
					PushInstance(local.GetInstance());
					break;
			}
		}

		_stackPtr += parameters;
	}

	private void PopLocalVariables(DaedalusSymbol symbol)
	{
		var occurrences = 0;
		foreach (var frame in _callStack)
		{
			if (ReferenceEquals(frame.Function, symbol))
			{
				++occurrences;
			}
		}

		if (occurrences <= 1)
		{
			return;
		}

		StackFrame result = default;
		if (symbol.HasReturn)
		{
			result = _stack[--_stackPtr];
		}

		var locals = FindLocalsForFunction(symbol);
		for (var i = locals.Count - 1; i >= 0; --i)
		{
			var local = locals[i];
			switch (local.Type)
			{
				case DaedalusDataType.Float:
					for (var r = (int)local.Count - 1; r >= 0; --r)
					{
						local.SetFloat(PopFloat(), r);
					}

					break;
				case DaedalusDataType.Function:
				case DaedalusDataType.Int:
					for (var r = (int)local.Count - 1; r >= 0; --r)
					{
						local.SetInt(PopInt(), r);
					}

					break;
				case DaedalusDataType.String:
					for (var r = (int)local.Count - 1; r >= 0; --r)
					{
						local.SetString(PopString(), r);
					}

					break;
				case DaedalusDataType.Instance:
					local.SetInstance(PopInstance());
					break;
			}
		}

		if (symbol.HasReturn)
		{
			_stack[_stackPtr++] = result;
		}
	}

	private void EnsureStack()
	{
		if (_stackPtr >= StackSize)
		{
			throw new DaedalusScriptException("stack overflow.");
		}
	}

	public void PushInt(int value)
	{
		EnsureStack();
		_stack[_stackPtr++] = new StackFrame { Kind = FrameKind.Int, Int = value };
	}

	public void PushFloat(float value)
	{
		EnsureStack();
		_stack[_stackPtr++] = new StackFrame { Kind = FrameKind.Float, Float = value };
	}

	public void PushInstance(DaedalusInstance value)
	{
		EnsureStack();
		_stack[_stackPtr++] = new StackFrame { Kind = FrameKind.Instance, Instance = value };
	}

	public void PushReference(DaedalusSymbol symbol, int index = 0)
	{
		EnsureStack();
		_stack[_stackPtr++] = new StackFrame
		{
			Kind = FrameKind.Reference,
			Context = _instance,
			Symbol = symbol,
			Index = index
		};
	}

	public void PushString(string value)
	{
		var index = GetFreeString();
		_temporaryStrings.SetString(value ?? string.Empty, index);
		PushReference(_temporaryStrings, index);
	}

	public int PopInt()
	{
		if (_stackPtr == 0)
		{
			return 0;
		}

		var frame = _stack[--_stackPtr];
		if (frame.Kind == FrameKind.Reference)
		{
			return GetInt(frame.Context, frame.Symbol, frame.Index);
		}

		if (frame.Kind == FrameKind.Int)
		{
			return frame.Int;
		}

		throw new DaedalusScriptException("tried to pop int but the frame does not contain one.");
	}

	public float PopFloat()
	{
		if (_stackPtr == 0)
		{
			return 0f;
		}

		var frame = _stack[--_stackPtr];
		if (frame.Kind == FrameKind.Reference)
		{
			return GetFloat(frame.Context, frame.Symbol, frame.Index);
		}

		if (frame.Kind == FrameKind.Float)
		{
			return frame.Float;
		}

		if (frame.Kind == FrameKind.Int)
		{
			return BitConverter.Int32BitsToSingle(frame.Int);
		}

		throw new DaedalusScriptException("tried to pop float but the frame does not contain one.");
	}

	public DaedalusInstance PopInstance()
	{
		if (_stackPtr == 0)
		{
			throw new DaedalusScriptException("popping instance from empty stack.");
		}

		var frame = _stack[--_stackPtr];
		if (frame.Kind == FrameKind.Reference)
		{
			return frame.Symbol.GetInstance();
		}

		if (frame.Kind == FrameKind.Instance)
		{
			return frame.Instance;
		}

		throw new DaedalusScriptException("tried to pop instance but the frame does not contain one.");
	}

	public string PopString()
	{
		PopReference(out var symbol, out var index, out var context);
		if (symbol.IsMember && context == null)
		{
			if (!AllowNullInstanceAccess)
			{
				throw new DaedalusScriptException($"Member '{symbol.Name}' is accessed without an instance.");
			}

			return string.Empty;
		}

		return symbol.GetString(index, context);
	}

	public void PopReference(out DaedalusSymbol symbol, out int index, out DaedalusInstance context)
	{
		if (_stackPtr == 0)
		{
			throw new DaedalusScriptException("popping reference from empty stack.");
		}

		var frame = _stack[--_stackPtr];
		if (frame.Kind != FrameKind.Reference)
		{
			throw new DaedalusScriptException("tried to pop reference but the frame does not contain one.");
		}

		symbol = frame.Symbol;
		index = frame.Index;
		context = frame.Context;

		if (ReferenceEquals(symbol, _temporaryStrings))
		{
			MarkFreeString(index);
		}
	}

	public T Pop<T>()
	{
		var type = typeof(T);

		if (type == typeof(int))
		{
			var value = PopInt();
			return Unsafe.As<int, T>(ref value);
		}

		if (type == typeof(float))
		{
			var value = PopFloat();
			return Unsafe.As<float, T>(ref value);
		}

		if (type == typeof(bool))
		{
			var value = PopInt() != 0;
			return Unsafe.As<bool, T>(ref value);
		}

		if (type == typeof(string))
		{
			var value = PopString();
			return Unsafe.As<string, T>(ref value);
		}

		if (type.IsEnum)
		{
			return (T)Enum.ToObject(type, PopInt());
		}

		if (typeof(DaedalusInstance).IsAssignableFrom(type))
		{
			var instance = PopInstance();
			if (instance == null)
			{
				return default;
			}

			if (instance is not T typed)
			{
				throw new DaedalusScriptException(
					$"Popping instance of wrong type: {instance.ClassName}, expected {type.Name}.");
			}

			return typed;
		}

		throw new DaedalusScriptException($"Unsupported stack pop type '{type.Name}'.");
	}

	public void Push<T>(T value)
	{
		if (value is int intValue)
		{
			PushInt(intValue);
			return;
		}

		if (value is float floatValue)
		{
			PushFloat(floatValue);
			return;
		}

		if (value is bool boolValue)
		{
			PushInt(boolValue ? 1 : 0);
			return;
		}

		if (value is string stringValue)
		{
			PushString(stringValue);
			return;
		}

		if (value is DaedalusInstance instance)
		{
			PushInstance(instance);
			return;
		}

		if (typeof(DaedalusInstance).IsAssignableFrom(typeof(T)))
		{
			PushInstance(null);
			return;
		}

		if (typeof(T).IsEnum)
		{
			PushInt(Convert.ToInt32(value));
			return;
		}

		throw new DaedalusScriptException($"Unsupported stack push type '{typeof(T).Name}'.");
	}

	private int GetInt(DaedalusInstance context, DaedalusSymbol symbol, int index)
	{
		if (symbol.IsMember && context == null && !AllowNullInstanceAccess)
		{
			throw new DaedalusScriptException($"Member '{symbol.Name}' is accessed without an instance.");
		}

		return symbol.GetInt(index, context);
	}

	private float GetFloat(DaedalusInstance context, DaedalusSymbol symbol, int index)
	{
		if (symbol.IsMember && context == null && !AllowNullInstanceAccess)
		{
			throw new DaedalusScriptException($"Member '{symbol.Name}' is accessed without an instance.");
		}

		return symbol.GetFloat(index, context);
	}

	private void SetInt(DaedalusInstance context, DaedalusSymbol symbol, int index, int value)
	{
		CheckConst(symbol);
		if (symbol.IsMember && context == null && AllowNullInstanceAccess)
		{
			return;
		}

		symbol.SetInt(value, index, context);
	}

	private void SetFloat(DaedalusInstance context, DaedalusSymbol symbol, int index, float value)
	{
		CheckConst(symbol);
		if (symbol.IsMember && context == null && AllowNullInstanceAccess)
		{
			return;
		}

		symbol.SetFloat(value, index, context);
	}

	private void SetString(DaedalusInstance context, DaedalusSymbol symbol, int index, string value)
	{
		CheckConst(symbol);
		if (symbol.IsMember && context == null && AllowNullInstanceAccess)
		{
			return;
		}

		symbol.SetString(value, index, context);
	}

	private void PushDefault(DaedalusDataType type)
	{
		switch (type)
		{
			case DaedalusDataType.Float:
				PushFloat(0f);
				break;
			case DaedalusDataType.Int:
			case DaedalusDataType.Function:
				PushInt(0);
				break;
			case DaedalusDataType.String:
				PushString(string.Empty);
				break;
			case DaedalusDataType.Instance:
				PushInstance(null);
				break;
		}
	}

	private void PopParameters(DaedalusSymbol symbol)
	{
		var first = symbol.Index + 1;
		for (var i = first + symbol.Count; i > first;)
		{
			--i;
			var parameter = Symbols[(int)i];
			switch (parameter.Type)
			{
				case DaedalusDataType.Float:
					PopFloat();
					break;
				case DaedalusDataType.Int:
				case DaedalusDataType.Function:
					PopInt();
					break;
				case DaedalusDataType.Instance:
				case DaedalusDataType.String:
					PopReference(out _, out _, out _);
					break;
			}
		}
	}

	private int GetFreeString()
	{
		if (_freeStrings.Count == 0)
		{
			var previous = _temporaryStrings.Strings?.Length ?? 0;
			_temporaryStrings.GrowStrings(previous);
			for (var i = previous; i < _temporaryStrings.Count; ++i)
			{
				_freeStrings.Add(i);
			}
		}

		var last = _freeStrings.Count - 1;
		var index = _freeStrings[last];
		_freeStrings.RemoveAt(last);
		return index;
	}

	private void MarkFreeString(int index)
	{
		_freeStrings.Add(index);
	}

	public void RegisterExternalDefault(ExternalDefaultFunction callback)
	{
		_defaultExternal = callback;
	}

	private void RegisterExternalUnsafe(string name, Action<DaedalusVm> callback)
	{
		var symbol = FindSymbolByName(name);
		if (symbol == null)
		{
			return;
		}

		_externals[symbol] = callback;
	}

	public void RegisterExternal(string name, ExternalFuncV callback)
	{
		RegisterExternalUnsafe(name, _ => callback());
	}

	public void RegisterExternal<TP0>(string name, ExternalFuncV<TP0> callback)
	{
		RegisterExternalUnsafe(name, vm => callback(vm.Pop<TP0>()));
	}

	public void RegisterExternal<TP0, TP1>(string name, ExternalFuncV<TP0, TP1> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			callback(p0, p1);
		});
	}

	public void RegisterExternal<TP0, TP1, TP2>(string name, ExternalFuncV<TP0, TP1, TP2> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p2 = vm.Pop<TP2>();
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			callback(p0, p1, p2);
		});
	}

	public void RegisterExternal<TP0, TP1, TP2, TP3>(string name, ExternalFuncV<TP0, TP1, TP2, TP3> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p3 = vm.Pop<TP3>();
			var p2 = vm.Pop<TP2>();
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			callback(p0, p1, p2, p3);
		});
	}

	public void RegisterExternal<TP0, TP1, TP2, TP3, TP4>(string name,
		ExternalFuncV<TP0, TP1, TP2, TP3, TP4> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p4 = vm.Pop<TP4>();
			var p3 = vm.Pop<TP3>();
			var p2 = vm.Pop<TP2>();
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			callback(p0, p1, p2, p3, p4);
		});
	}

	public void RegisterExternal<TP0, TP1, TP2, TP3, TP4, TP5>(string name,
		ExternalFuncV<TP0, TP1, TP2, TP3, TP4, TP5> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p5 = vm.Pop<TP5>();
			var p4 = vm.Pop<TP4>();
			var p3 = vm.Pop<TP3>();
			var p2 = vm.Pop<TP2>();
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			callback(p0, p1, p2, p3, p4, p5);
		});
	}

	public void RegisterExternal<TP0, TP1, TP2, TP3, TP4, TP5, TP6>(string name,
		ExternalFuncV<TP0, TP1, TP2, TP3, TP4, TP5, TP6> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p6 = vm.Pop<TP6>();
			var p5 = vm.Pop<TP5>();
			var p4 = vm.Pop<TP4>();
			var p3 = vm.Pop<TP3>();
			var p2 = vm.Pop<TP2>();
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			callback(p0, p1, p2, p3, p4, p5, p6);
		});
	}

	public void RegisterExternal<TP0, TP1, TP2, TP3, TP4, TP5, TP6, TP7>(string name,
		ExternalFuncV<TP0, TP1, TP2, TP3, TP4, TP5, TP6, TP7> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p7 = vm.Pop<TP7>();
			var p6 = vm.Pop<TP6>();
			var p5 = vm.Pop<TP5>();
			var p4 = vm.Pop<TP4>();
			var p3 = vm.Pop<TP3>();
			var p2 = vm.Pop<TP2>();
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			callback(p0, p1, p2, p3, p4, p5, p6, p7);
		});
	}

	public void RegisterExternal<TP0, TP1, TP2, TP3, TP4, TP5, TP6, TP7, TP8>(string name,
		ExternalFuncV<TP0, TP1, TP2, TP3, TP4, TP5, TP6, TP7, TP8> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p8 = vm.Pop<TP8>();
			var p7 = vm.Pop<TP7>();
			var p6 = vm.Pop<TP6>();
			var p5 = vm.Pop<TP5>();
			var p4 = vm.Pop<TP4>();
			var p3 = vm.Pop<TP3>();
			var p2 = vm.Pop<TP2>();
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			callback(p0, p1, p2, p3, p4, p5, p6, p7, p8);
		});
	}

	public void RegisterExternal<TR>(string name, ExternalFunc<TR> callback)
	{
		RegisterExternalUnsafe(name, vm => vm.Push(callback()));
	}

	public void RegisterExternal<TR, TP0>(string name, ExternalFunc<TR, TP0> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p0 = vm.Pop<TP0>();
			vm.Push(callback(p0));
		});
	}

	public void RegisterExternal<TR, TP0, TP1>(string name, ExternalFunc<TR, TP0, TP1> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			vm.Push(callback(p0, p1));
		});
	}

	public void RegisterExternal<TR, TP0, TP1, TP2>(string name,
		ExternalFunc<TR, TP0, TP1, TP2> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p2 = vm.Pop<TP2>();
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			vm.Push(callback(p0, p1, p2));
		});
	}

	public void RegisterExternal<TR, TP0, TP1, TP2, TP3>(string name,
		ExternalFunc<TR, TP0, TP1, TP2, TP3> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p3 = vm.Pop<TP3>();
			var p2 = vm.Pop<TP2>();
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			vm.Push(callback(p0, p1, p2, p3));
		});
	}

	public void RegisterExternal<TR, TP0, TP1, TP2, TP3, TP4>(string name,
		ExternalFunc<TR, TP0, TP1, TP2, TP3, TP4> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p4 = vm.Pop<TP4>();
			var p3 = vm.Pop<TP3>();
			var p2 = vm.Pop<TP2>();
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			vm.Push(callback(p0, p1, p2, p3, p4));
		});
	}

	public void RegisterExternal<TR, TP0, TP1, TP2, TP3, TP4, TP5>(string name,
		ExternalFunc<TR, TP0, TP1, TP2, TP3, TP4, TP5> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p5 = vm.Pop<TP5>();
			var p4 = vm.Pop<TP4>();
			var p3 = vm.Pop<TP3>();
			var p2 = vm.Pop<TP2>();
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			vm.Push(callback(p0, p1, p2, p3, p4, p5));
		});
	}

	public void RegisterExternal<TR, TP0, TP1, TP2, TP3, TP4, TP5, TP6>(string name,
		ExternalFunc<TR, TP0, TP1, TP2, TP3, TP4, TP5, TP6> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p6 = vm.Pop<TP6>();
			var p5 = vm.Pop<TP5>();
			var p4 = vm.Pop<TP4>();
			var p3 = vm.Pop<TP3>();
			var p2 = vm.Pop<TP2>();
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			vm.Push(callback(p0, p1, p2, p3, p4, p5, p6));
		});
	}

	public void RegisterExternal<TR, TP0, TP1, TP2, TP3, TP4, TP5, TP6, TP7>(string name,
		ExternalFunc<TR, TP0, TP1, TP2, TP3, TP4, TP5, TP6, TP7> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p7 = vm.Pop<TP7>();
			var p6 = vm.Pop<TP6>();
			var p5 = vm.Pop<TP5>();
			var p4 = vm.Pop<TP4>();
			var p3 = vm.Pop<TP3>();
			var p2 = vm.Pop<TP2>();
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			vm.Push(callback(p0, p1, p2, p3, p4, p5, p6, p7));
		});
	}

	public void RegisterExternal<TR, TP0, TP1, TP2, TP3, TP4, TP5, TP6, TP7, TP8>(string name,
		ExternalFunc<TR, TP0, TP1, TP2, TP3, TP4, TP5, TP6, TP7, TP8> callback)
	{
		RegisterExternalUnsafe(name, vm =>
		{
			var p8 = vm.Pop<TP8>();
			var p7 = vm.Pop<TP7>();
			var p6 = vm.Pop<TP6>();
			var p5 = vm.Pop<TP5>();
			var p4 = vm.Pop<TP4>();
			var p3 = vm.Pop<TP3>();
			var p2 = vm.Pop<TP2>();
			var p1 = vm.Pop<TP1>();
			var p0 = vm.Pop<TP0>();
			vm.Push(callback(p0, p1, p2, p3, p4, p5, p6, p7, p8));
		});
	}
}
