using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace OpenGothic.ZenKit;

/// <summary>Entry kinds used by the BIN_SAFE archive format.</summary>
public enum ZenArchiveEntryType : byte
{
	String = 0x01,
	Integer = 0x02,
	Float = 0x03,
	Byte = 0x04,
	Word = 0x05,
	Bool = 0x06,
	Vec3 = 0x07,
	Color = 0x08,
	Raw = 0x09,
	RawFloat = 0x10,
	Enum = 0x11,
	Hash = 0x12
}

public enum ZenArchiveFormat
{
	Binary,
	BinSafe,
	Ascii
}

public sealed class ZenArchiveHeader
{
	public int Version { get; internal set; }

	public string Archiver { get; internal set; }

	public ZenArchiveFormat Format { get; internal set; }

	public bool SaveGame { get; internal set; }

	public string User { get; internal set; }

	public string Date { get; internal set; }
}

/// <summary>A typed value stored on a <see cref="ZenObject"/>.</summary>
public sealed class ZenValue
{
	public ZenArchiveEntryType Type { get; internal set; }

	public string String { get; internal set; }

	public int Integer { get; internal set; }

	public uint Unsigned { get; internal set; }

	public float Float { get; internal set; }

	public bool Bool { get; internal set; }

	public Vector3 Vec3 { get; internal set; }

	public byte[] Raw { get; internal set; }
}

/// <summary>
/// A parsed object of a ZenGin archive. Objects carry their named fields and their child
/// objects; field names come from the BIN_SAFE hash table, so the reader does not need a
/// per-class schema.
/// </summary>
public sealed class ZenObject
{
	private readonly Dictionary<string, ZenValue> _fields =
		new Dictionary<string, ZenValue>(StringComparer.OrdinalIgnoreCase);

	public string Name { get; internal set; }

	public string ClassName { get; internal set; }

	public ushort Version { get; internal set; }

	public uint Index { get; internal set; }

	public bool IsReference => ClassName == "\xA7";

	public bool IsEmpty => ClassName == "%";

	public IReadOnlyDictionary<string, ZenValue> Fields => _fields;

	/// <summary>Fields and child objects in archive order; needed for the flat vob stream.</summary>
	public List<ZenEntry> Entries { get; } = new List<ZenEntry>();

	public List<ZenObject> Children { get; } = new List<ZenObject>();

	public bool Has(string name) => _fields.ContainsKey(name);

	public ZenValue Get(string name) => _fields.TryGetValue(name, out var value) ? value : null;

	public string GetString(string name, string fallback = "")
	{
		var value = Get(name);
		return value?.String ?? fallback;
	}

	public int GetInt(string name, int fallback = 0)
	{
		var value = Get(name);
		if (value == null)
		{
			return fallback;
		}

		return value.Type switch
		{
			ZenArchiveEntryType.Byte or ZenArchiveEntryType.Word or ZenArchiveEntryType.Enum or
				ZenArchiveEntryType.Integer => value.Integer,
			ZenArchiveEntryType.Bool => value.Bool ? 1 : 0,
			_ => fallback
		};
	}

	public uint GetUInt(string name, uint fallback = 0)
	{
		var value = Get(name);
		return value == null ? fallback : (uint)value.Integer;
	}

	public float GetFloat(string name, float fallback = 0f)
	{
		var value = Get(name);
		return value == null ? fallback : value.Float;
	}

	public bool GetBool(string name, bool fallback = false)
	{
		var value = Get(name);
		return value == null ? fallback : value.Bool;
	}

	public Vector3 GetVec3(string name, Vector3 fallback = default)
	{
		var value = Get(name);
		return value == null ? fallback : value.Vec3;
	}

	public byte[] GetRaw(string name)
	{
		return Get(name)?.Raw;
	}

	internal void SetField(string name, ZenValue value)
	{
		_fields[name] = value;
	}

	public override string ToString() => $"{Name} [{ClassName}]";
}

/// <summary>One entry of an object body, preserving whether it was a field or a child.</summary>
public sealed class ZenEntry
{
	public string Key { get; internal set; }

	public ZenValue Value { get; internal set; }

	public ZenObject Child { get; internal set; }

	public bool IsChild => Child != null;
}

/// <summary>
/// Reader for ZenGin archives. World files use the BIN_SAFE layout, whose entries carry a
/// hash-table key for every field, which lets the reader build a complete object tree
/// without decoding each game class separately.
/// </summary>
public sealed class ZenArchive
{
	private readonly Read _reader;
	private readonly List<(string Key, uint Hash)> _hashTable = new List<(string, uint)>();

	private ZenArchive(Read reader)
	{
		_reader = reader ?? throw new ArgumentNullException(nameof(reader));
	}

	public ZenArchiveHeader Header { get; } = new ZenArchiveHeader();

	/// <summary>
	/// Called after an object header when the object's body is not stored as archive
	/// entries. The callback must leave the reader at the object-end marker.
	/// </summary>
	public Func<ZenArchive, ZenObject, bool> ObjectBodyReader { get; set; }

	public Read Reader => _reader;

	public static ZenArchive Open(Read reader)
	{
		var archive = new ZenArchive(reader);
		archive.ReadHeader();
		return archive;
	}

	public ZenObject ReadObject()
	{
		if (!TryReadObjectBegin(out var obj))
		{
			return null;
		}

		ReadObjectBody(obj);
		return obj;
	}

	private void ReadHeader()
	{
		var signature = _reader.ReadLine(true);
		if (signature != "ZenGin Archive")
		{
			throw new ZenArchiveException($"Invalid archive header: '{signature}'.");
		}

		var version = _reader.ReadLine(true);
		if (!version.StartsWith("ver ", StringComparison.Ordinal))
		{
			throw new ZenArchiveException("Archive version field is missing.");
		}

		Header.Version = int.TryParse(version.AsSpan(4), out var parsed) ? parsed : 0;
		if (Header.Version != 1)
		{
			throw new ZenArchiveException($"Unsupported archive version {Header.Version}.");
		}

		Header.Archiver = _reader.ReadLine(true);
		var format = _reader.ReadLine(true);
		Header.Format = format switch
		{
			"BINARY" => ZenArchiveFormat.Binary,
			"BIN_SAFE" => ZenArchiveFormat.BinSafe,
			"ASCII" => ZenArchiveFormat.Ascii,
			_ => throw new ZenArchiveException($"Unknown archive format '{format}'.")
		};

		var saveGame = _reader.ReadLine(true);
		if (!saveGame.StartsWith("saveGame ", StringComparison.Ordinal))
		{
			throw new ZenArchiveException("Archive saveGame field is missing.");
		}

		Header.SaveGame = saveGame.Substring(9).Trim() != "0";

		var optional = _reader.ReadLine(true);
		if (optional.StartsWith("date ", StringComparison.Ordinal))
		{
			Header.Date = optional.Substring(5);
			optional = _reader.ReadLine(true);
		}

		if (optional.StartsWith("user ", StringComparison.Ordinal))
		{
			Header.User = optional.Substring(5);
			optional = _reader.ReadLine(true);
		}

		if (optional != "END")
		{
			throw new ZenArchiveException("Archive header is missing its END marker.");
		}

		if (Header.Format != ZenArchiveFormat.BinSafe)
		{
			throw new ZenArchiveException(
				$"Archive format {Header.Format} is not supported by the managed ZEN reader yet.");
		}

		ReadBinSafeHeader();
	}

	private void ReadBinSafeHeader()
	{
		_ = _reader.ReadUInt(); // bin-safe version
		_ = _reader.ReadUInt(); // object count
		var hashTableOffset = _reader.ReadUInt();

		var mark = _reader.Position;
		_reader.Seek(hashTableOffset);

		var count = _reader.ReadUInt();
		_hashTable.Clear();
		for (var i = 0; i < count; ++i)
		{
			_hashTable.Add((string.Empty, 0));
		}

		for (var i = 0; i < count; ++i)
		{
			var keyLength = _reader.ReadUShort();
			var insertionIndex = _reader.ReadUShort();
			var hash = _reader.ReadUInt();
			var key = _reader.ReadString(keyLength);
			if (insertionIndex >= _hashTable.Count)
			{
				throw new ZenArchiveException(
					$"Hash table entry {i} points outside the table ({insertionIndex}).");
			}

			_hashTable[insertionIndex] = (key, hash);
		}

		_reader.Seek(mark);
	}

	private bool TryReadObjectBegin(out ZenObject obj)
	{
		obj = null;

		if (_reader.Eof)
		{
			return false;
		}

		var type = (ZenArchiveEntryType)_reader.ReadUByte();
		if (type != ZenArchiveEntryType.String)
		{
			throw new ZenArchiveException($"Expected an object header, got entry type 0x{(byte)type:X2}.");
		}

		var length = _reader.ReadUShort();
		var header = _reader.ReadString(length);
		if (header == "[]")
		{
			return false;
		}

		if (!header.StartsWith("[", StringComparison.Ordinal) ||
		    !header.EndsWith("]", StringComparison.Ordinal))
		{
			throw new ZenArchiveException($"Invalid object header '{header}'.");
		}

		var parts = header.Substring(1, header.Length - 2).Split(' ');
		if (parts.Length < 4)
		{
			throw new ZenArchiveException($"Invalid object header '{header}'.");
		}

		obj = new ZenObject
		{
			Name = parts[0],
			ClassName = parts[1],
			Version = ushort.TryParse(parts[2], out var version) ? version : (ushort)0,
			Index = uint.TryParse(parts[3], out var index) ? index : 0
		};

		return true;
	}

	private void ReadObjectBody(ZenObject obj)
	{
		if (ObjectBodyReader != null && ObjectBodyReader(this, obj))
		{
			// The callback consumed the raw body; the object-end marker is next.
		}

		while (true)
		{
			var type = (ZenArchiveEntryType)_reader.ReadUByte();

			if (type == ZenArchiveEntryType.Hash)
			{
				var keyIndex = _reader.ReadUInt();
				if (keyIndex >= _hashTable.Count)
				{
					throw new ZenArchiveException($"Field key {keyIndex} is outside the hash table.");
				}

				var key = _hashTable[(int)keyIndex].Key;
				var value = ReadValue(_reader.ReadUByte());
				obj.SetField(key, value);
				obj.Entries.Add(new ZenEntry { Key = key, Value = value });
				continue;
			}

			if (type == ZenArchiveEntryType.String)
			{
				var length = _reader.ReadUShort();
				var text = _reader.ReadString(length);
				if (text == "[]")
				{
					return;
				}

				if (text.StartsWith("[", StringComparison.Ordinal))
				{
					var child = ParseObject(text, out _);
					ReadObjectBody(child);
					obj.Children.Add(child);
					obj.Entries.Add(new ZenEntry { Child = child });
					continue;
				}

				throw new ZenArchiveException($"Unexpected string entry '{text}' in {obj}.");
			}

			throw new ZenArchiveException($"Unexpected entry type 0x{(byte)type:X2} in {obj}.");
		}
	}

	private static ZenObject ParseObject(string header, out bool end)
	{
		end = header == "[]";
		if (end)
		{
			return null;
		}

		var parts = header.Substring(1, header.Length - 2).Split(' ');
		if (parts.Length < 4)
		{
			throw new ZenArchiveException($"Invalid object header '{header}'.");
		}

		return new ZenObject
		{
			Name = parts[0],
			ClassName = parts[1],
			Version = ushort.TryParse(parts[2], out var version) ? version : (ushort)0,
			Index = uint.TryParse(parts[3], out var index) ? index : 0
		};
	}

	private ZenValue ReadValue(byte typeByte)
	{
		var type = (ZenArchiveEntryType)typeByte;
		var value = new ZenValue { Type = type };

		switch (type)
		{
			case ZenArchiveEntryType.String:
				value.String = _reader.ReadString(_reader.ReadUShort());
				break;

			case ZenArchiveEntryType.Integer:
				value.Integer = _reader.ReadInt();
				break;

			case ZenArchiveEntryType.Float:
				value.Float = _reader.ReadFloat();
				break;

			case ZenArchiveEntryType.Byte:
				value.Integer = _reader.ReadUByte();
				break;

			case ZenArchiveEntryType.Word:
				value.Integer = _reader.ReadUShort();
				break;

			case ZenArchiveEntryType.Bool:
				value.Bool = _reader.ReadUInt() != 0;
				value.Integer = value.Bool ? 1 : 0;
				break;

			case ZenArchiveEntryType.Vec3:
				value.Vec3 = new Vector3(_reader.ReadFloat(), _reader.ReadFloat(), _reader.ReadFloat());
				break;

			case ZenArchiveEntryType.Color:
			{
				var b = _reader.ReadUByte();
				var g = _reader.ReadUByte();
				var r = _reader.ReadUByte();
				var a = _reader.ReadUByte();
				value.Unsigned = (uint)(a << 24 | r << 16 | g << 8 | b);
				break;
			}

			case ZenArchiveEntryType.Raw:
			case ZenArchiveEntryType.RawFloat:
				value.Raw = _reader.ReadBytes(_reader.ReadUShort());
				break;

			case ZenArchiveEntryType.Enum:
				value.Integer = _reader.ReadInt();
				break;

			default:
				throw new ZenArchiveException($"Unsupported archive value type 0x{typeByte:X2}.");
		}

		return value;
	}
}

public sealed class ZenArchiveException : Exception
{
	public ZenArchiveException(string message) : base(message)
	{
	}
}
