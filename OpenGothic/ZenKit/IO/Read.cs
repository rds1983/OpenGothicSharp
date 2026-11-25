using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace OpenGothic.ZenKit;

/// <summary>
/// Little-endian input stream over a game data record. This is a port of ZenKit's
/// <c>zenkit::Read</c>: primitives keep the on-disk layout, so parsers can follow a format
/// directly. A read past the end yields zero bytes instead of throwing, matching the
/// reference memory stream, which zero-fills short reads.
/// </summary>
public abstract class Read
{
	private static readonly Encoding DefaultStringEncoding = CreateStringEncoding();

	/// <summary>
	/// Encoding used by <see cref="ReadString"/>. ZenKitCS decodes the game's byte strings as
	/// CP1252, so the managed readers start there.
	/// </summary>
	public static Encoding StringEncoding { get; set; } = DefaultStringEncoding;

	private static Encoding CreateStringEncoding()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		return Encoding.GetEncoding(1252);
	}

	public abstract long Length { get; }

	public abstract long Position { get; }

	public bool Eof => Position >= Length;

	public abstract int ReadBytes(Span<byte> buffer);

	public abstract void Seek(long offset, SeekOrigin origin);

	public void Seek(long offset) => Seek(offset, SeekOrigin.Begin);

	public void Skip(long count) => Seek(count, SeekOrigin.Current);

	/// <summary>Reads exactly <paramref name="length"/> bytes; missing bytes are zero.</summary>
	public byte[] ReadBytes(int length)
	{
		if (length < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(length));
		}

		var result = new byte[length];
		ReadBytes(result);
		return result;
	}

	public char ReadChar() => (char)ReadUByte();

	public sbyte ReadByte()
	{
		Span<byte> buffer = stackalloc byte[1];
		ReadExact(buffer);
		return unchecked((sbyte)buffer[0]);
	}

	public byte ReadUByte()
	{
		Span<byte> buffer = stackalloc byte[1];
		ReadExact(buffer);
		return buffer[0];
	}

	public short ReadShort()
	{
		Span<byte> buffer = stackalloc byte[2];
		ReadExact(buffer);
		return BinaryPrimitives.ReadInt16LittleEndian(buffer);
	}

	public ushort ReadUShort()
	{
		Span<byte> buffer = stackalloc byte[2];
		ReadExact(buffer);
		return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
	}

	public int ReadInt()
	{
		Span<byte> buffer = stackalloc byte[4];
		ReadExact(buffer);
		return BinaryPrimitives.ReadInt32LittleEndian(buffer);
	}

	public uint ReadUInt()
	{
		Span<byte> buffer = stackalloc byte[4];
		ReadExact(buffer);
		return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
	}

	public long ReadLong()
	{
		Span<byte> buffer = stackalloc byte[8];
		ReadExact(buffer);
		return BinaryPrimitives.ReadInt64LittleEndian(buffer);
	}

	public float ReadFloat()
	{
		Span<byte> buffer = stackalloc byte[4];
		ReadExact(buffer);
		return BinaryPrimitives.ReadSingleLittleEndian(buffer);
	}

	public double ReadDouble()
	{
		Span<byte> buffer = stackalloc byte[8];
		ReadExact(buffer);
		return BinaryPrimitives.ReadDoubleLittleEndian(buffer);
	}

	/// <summary>Reads a fixed number of bytes and decodes them with <see cref="StringEncoding"/>.</summary>
	public string ReadString(int length) => StringEncoding.GetString(ReadBytes(length));

	/// <summary>
	/// Reads a fixed-width name field and trims the padding from the end. Union pads VDF
	/// catalog names with spaces; the reference trims every trailing whitespace byte.
	/// </summary>
	public string ReadFixedString(int length)
	{
		var result = ReadString(length);
		var end = result.Length;
		while (end > 0 && char.IsWhiteSpace(result[end - 1]))
		{
			--end;
		}

		return end == result.Length ? result : result.Substring(0, end);
	}

	/// <summary>Reads a zero-terminated string, stopping before the terminator.</summary>
	public string ReadCString()
	{
		var builder = new StringBuilder();
		char c;
		while ((c = ReadChar()) != '\0')
		{
			builder.Append(c);
		}

		return builder.ToString();
	}

	/// <summary>
	/// Reads up to the next CR/LF and optionally consumes the whitespace that follows it.
	/// Ported verbatim from ZenKit because the game's text parsers rely on the one-byte
	/// backtrack after the skipped run.
	/// </summary>
	public string ReadLine(bool skipWhitespace = false) =>
		ReadLineThenIgnore(skipWhitespace ? " \t\r\n\v\f" : string.Empty);

	public string ReadLineThenIgnore(string chars)
	{
		var builder = new StringBuilder();

		char c;
		while ((c = ReadChar()) != '\0' && c != '\r' && c != '\n')
		{
			builder.Append(c);
		}

		if (chars.Length == 0 || c == '\0' || Eof)
		{
			return builder.ToString();
		}

		do
		{
			c = ReadChar();
		} while (c != '\0' && chars.IndexOf(c) >= 0);

		if (Eof && c == '\0')
		{
			return builder.ToString();
		}

		Seek(-1, SeekOrigin.Current);
		return builder.ToString();
	}

	private void ReadExact(Span<byte> buffer)
	{
		buffer.Clear();
		ReadBytes(buffer);
	}
}

/// <summary>A <see cref="Read"/> over a byte array or a slice of one.</summary>
public sealed class MemoryRead : Read
{
	private readonly byte[] _data;
	private readonly int _offset;
	private readonly int _length;
	private int _position;

	public MemoryRead(byte[] data) : this(data, 0, data.Length)
	{
	}

	public MemoryRead(byte[] data, int offset, int length)
	{
		_data = data ?? throw new ArgumentNullException(nameof(data));
		if (offset < 0 || offset > data.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(offset));
		}

		if (length < 0 || offset + length > data.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(length));
		}

		_offset = offset;
		_length = length;
	}

	public override long Length => _length;

	public override long Position => _position;

	public override int ReadBytes(Span<byte> buffer)
	{
		var available = _length - _position;
		if (available <= 0)
		{
			return 0;
		}

		var count = Math.Min(buffer.Length, available);
		_data.AsSpan(_offset + _position, count).CopyTo(buffer);
		_position += count;
		return count;
	}

	public override void Seek(long offset, SeekOrigin origin)
	{
		long target;
		switch (origin)
		{
			case SeekOrigin.Begin:
				target = offset;
				break;
			case SeekOrigin.Current:
				target = _position + offset;
				break;
			case SeekOrigin.End:
				target = _length + offset;
				break;
			default:
				return;
		}

		if (target < 0 || target > _length)
		{
			return;
		}

		_position = (int)target;
	}
}
