using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace OpenGothic.ZenKit;

/// <summary>
/// Backing storage for file data referenced by a <see cref="Vfs"/> node. The VDF format
/// stores only offsets, so the archive has to stay alive while any node is used.
/// </summary>
internal interface IArchiveData : IDisposable
{
	long Length { get; }

	Read OpenRead(long offset, long length);
}

/// <summary>An archive kept in memory; used by the tests and by small synthetic archives.</summary>
internal sealed class MemoryArchive : IArchiveData
{
	private readonly byte[] _data;

	public MemoryArchive(byte[] data)
	{
		_data = data ?? throw new ArgumentNullException(nameof(data));
	}

	public long Length => _data.Length;

	public Read OpenRead(long offset, long length)
	{
		if (offset < 0 || length < 0 || offset + length > _data.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(offset));
		}

		return new MemoryRead(_data, (int)offset, (int)length);
	}

	public void Dispose()
	{
	}
}

/// <summary>
/// A read-only memory-mapped archive. The game's VDFs total well over a gigabyte, so the
/// managed VFS maps them instead of copying them into managed arrays.
/// </summary>
internal sealed class MappedArchive : IArchiveData
{
	private readonly MemoryMappedFile _file;
	private readonly MemoryMappedViewAccessor _view;
	private readonly unsafe byte* _pointer;
	private bool _disposed;

	public unsafe MappedArchive(string path)
	{
		var length = new FileInfo(path).Length;
		if (length <= 0)
		{
			throw new VfsBrokenDiskException(path, "the archive is empty");
		}

		try
		{
			_file = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
			_view = _file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

			byte* pointer = null;
			_view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
			_pointer = pointer + _view.PointerOffset;
			Length = length;
		}
		catch
		{
			_view?.Dispose();
			_file?.Dispose();
			throw;
		}
	}

	public long Length { get; }

	public unsafe Read OpenRead(long offset, long length)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (offset < 0 || length < 0 || offset + length > Length)
		{
			throw new ArgumentOutOfRangeException(nameof(offset));
		}

		return new PointerRead(_pointer + offset, length);
	}

	public unsafe void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_view.SafeMemoryMappedViewHandle.ReleasePointer();
		_view.Dispose();
		_file.Dispose();
	}
}

/// <summary>A <see cref="Read"/> over a slice of mapped archive memory.</summary>
internal sealed unsafe class PointerRead : Read
{
	private readonly byte* _data;
	private readonly long _length;
	private long _position;

	public PointerRead(byte* data, long length)
	{
		_data = data;
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

		var count = (int)Math.Min(buffer.Length, available);
		new Span<byte>(_data + _position, count).CopyTo(buffer);
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

		_position = target;
	}
}
