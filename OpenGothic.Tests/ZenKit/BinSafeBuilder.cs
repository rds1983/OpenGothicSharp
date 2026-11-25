using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace OpenGothic.Tests.ZenKit;

/// <summary>
/// Builds a tiny BIN_SAFE ZenGin archive in memory. Only the subset of the format the
/// managed reader needs is emitted: headers, objects, hashed fields, references and the
/// hash table.
/// </summary>
internal static class BinSafeBuilder
{
	public enum FieldType
	{
		String,
		Int,
		Float,
		Bool,
		Vec3,
		Raw,
		RawFloat,
		Enum,
		Byte,
		Word,
		Color
	}

	public sealed class Field
	{
		public string Key;
		public FieldType Type;
		public object Value;
	}

	public sealed class Obj
	{
		public string Name = string.Empty;
		public string ClassName = "zCVob";
		public uint Index;
		public bool IsReference;
		public uint ReferenceIndex;
		public List<Field> Fields = new List<Field>();
		public List<Obj> Children = new List<Obj>();

		public static Field String(string key, string value) => new Field { Key = key, Type = FieldType.String, Value = value };
		public static Field Int(string key, int value) => new Field { Key = key, Type = FieldType.Int, Value = value };
		public static Field Float(string key, float value) => new Field { Key = key, Type = FieldType.Float, Value = value };
		public static Field Bool(string key, bool value) => new Field { Key = key, Type = FieldType.Bool, Value = value };
		public static Field Vec3(string key, Vector3 value) => new Field { Key = key, Type = FieldType.Vec3, Value = value };
		public static Field Raw(string key, byte[] value) => new Field { Key = key, Type = FieldType.Raw, Value = value };
		public static Field RawFloat(string key, float[] value) => new Field { Key = key, Type = FieldType.RawFloat, Value = value };
		public static Field Enum(string key, uint value) => new Field { Key = key, Type = FieldType.Enum, Value = value };
	}

	public static byte[] Build(Obj root)
	{
		var keys = new List<string>();
		var keyIndices = new Dictionary<string, int>(StringComparer.Ordinal);
		var objects = new MemoryStream();
		var writer = new BinaryWriter(objects, Encoding.ASCII, true);

		void WriteEntryHeader(string text)
		{
			writer.Write((byte)0x01);
			writer.Write((ushort)text.Length);
			writer.Write(Encoding.ASCII.GetBytes(text));
		}

		int KeyIndex(string key)
		{
			if (!keyIndices.TryGetValue(key, out var index))
			{
				index = keys.Count;
				keys.Add(key);
				keyIndices[key] = index;
			}

			return index;
		}

		void WriteObject(Obj obj)
		{
			if (obj.IsReference)
			{
				WriteEntryHeader($"[{obj.Name} \u00A7 0 {obj.ReferenceIndex}]");
				WriteEntryHeader("[]");
				return;
			}

			WriteEntryHeader($"[{obj.Name} {obj.ClassName} 0 {obj.Index}]");
			foreach (var field in obj.Fields)
			{
				writer.Write((byte)0x12);
				writer.Write((uint)KeyIndex(field.Key));
				WriteValue(writer, field);
			}

			foreach (var child in obj.Children)
			{
				WriteObject(child);
			}

			WriteEntryHeader("[]");
		}

		WriteObject(root);
		writer.Flush();

		var header = Encoding.ASCII.GetBytes(
			"ZenGin Archive\nver 1\nzCArchiverGeneric\nBIN_SAFE\nsaveGame 0\nEND\n");
		var hashTableOffset = header.Length + 12 + (int)objects.Length;

		using var result = new MemoryStream();
		using var output = new BinaryWriter(result, Encoding.ASCII, true);
		output.Write(header);
		output.Write(2u); // bin-safe version
		output.Write(1u); // object count
		output.Write((uint)hashTableOffset);
		output.Write(objects.ToArray());

		output.Write((uint)keys.Count);
		for (var i = 0; i < keys.Count; ++i)
		{
			var bytes = Encoding.ASCII.GetBytes(keys[i]);
			output.Write((ushort)bytes.Length);
			output.Write((ushort)i);
			output.Write(0u); // hash value, unused by the reader
			output.Write(bytes);
		}

		return result.ToArray();
	}

	private static void WriteValue(BinaryWriter writer, Field field)
	{
		switch (field.Type)
		{
			case FieldType.String:
			{
				var bytes = Encoding.ASCII.GetBytes((string)field.Value);
				writer.Write((byte)0x01);
				writer.Write((ushort)bytes.Length);
				writer.Write(bytes);
				break;
			}
			case FieldType.Int:
				writer.Write((byte)0x02);
				writer.Write((int)field.Value);
				break;
			case FieldType.Float:
				writer.Write((byte)0x03);
				writer.Write((float)field.Value);
				break;
			case FieldType.Bool:
				writer.Write((byte)0x06);
				writer.Write((bool)field.Value ? 1u : 0u);
				break;
			case FieldType.Vec3:
			{
				var value = (Vector3)field.Value;
				writer.Write((byte)0x07);
				writer.Write(value.X);
				writer.Write(value.Y);
				writer.Write(value.Z);
				break;
			}
			case FieldType.Raw:
			{
				var bytes = (byte[])field.Value;
				writer.Write((byte)0x09);
				writer.Write((ushort)bytes.Length);
				writer.Write(bytes);
				break;
			}
			case FieldType.RawFloat:
			{
				var values = (float[])field.Value;
				writer.Write((byte)0x10);
				writer.Write((ushort)(values.Length * 4));
				foreach (var value in values)
				{
					writer.Write(value);
				}

				break;
			}
			case FieldType.Enum:
				writer.Write((byte)0x11);
				writer.Write((uint)field.Value);
				break;
			case FieldType.Byte:
				writer.Write((byte)0x04);
				writer.Write((byte)field.Value);
				break;
			case FieldType.Word:
				writer.Write((byte)0x05);
				writer.Write((ushort)field.Value);
				break;
			case FieldType.Color:
			{
				var value = (ColorValue)field.Value;
				writer.Write((byte)0x08);
				writer.Write(value.B);
				writer.Write(value.G);
				writer.Write(value.R);
				writer.Write(value.A);
				break;
			}
			default:
				throw new ArgumentOutOfRangeException(nameof(field));
		}
	}

	public readonly struct ColorValue
	{
		public ColorValue(byte a, byte r, byte g, byte b)
		{
			A = a;
			R = r;
			G = g;
			B = b;
		}

		public byte A { get; }
		public byte R { get; }
		public byte G { get; }
		public byte B { get; }
	}
}
