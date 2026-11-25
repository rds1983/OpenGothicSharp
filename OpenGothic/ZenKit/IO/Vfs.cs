using System;
using System.Collections.Generic;
using System.IO;

namespace OpenGothic.ZenKit;

public enum VfsNodeType
{
	Directory = 1,
	File = 2
}

/// <summary>How <see cref="Vfs.MountDisk(string, VfsOverwriteBehavior)"/> resolves name clashes.</summary>
public enum VfsOverwriteBehavior
{
	/// <summary>Keep the existing node.</summary>
	None = 0,

	/// <summary>Always take the mounted node.</summary>
	All = 1,

	/// <summary>Replace only when the existing node is newer than the mounted one.</summary>
	Newer = 2,

	/// <summary>Replace only when the mounted node is newer than the existing one.</summary>
	Older = 3
}

public class VfsException : Exception
{
	public VfsException(string message) : base(message)
	{
	}
}

/// <summary>A VDF whose header or catalog cannot be read.</summary>
public sealed class VfsBrokenDiskException : VfsException
{
	public VfsBrokenDiskException(string signature)
		: base($"Not a VDF archive: signature '{Escape(signature)}'.")
	{
	}

	public VfsBrokenDiskException(string path, string reason)
		: base($"Unable to mount '{path}': {reason}.")
	{
	}

	private static string Escape(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}

		return value.Replace("\r", "\\r").Replace("\n", "\\n");
	}
}

/// <summary>A VDF format the managed reader does not support yet.</summary>
public sealed class VfsUnsupportedException : VfsException
{
	public VfsUnsupportedException(string message) : base(message)
	{
	}
}

public sealed class VfsNotFoundException : VfsException
{
	public VfsNotFoundException(string path) : base($"VFS path not found: '{path}'.")
	{
	}
}

public sealed class VfsFileExistsException : VfsException
{
	public VfsFileExistsException(string name) : base($"VFS path is a file: '{name}'.")
	{
	}
}

/// <summary>
/// A file or directory in a <see cref="Vfs"/>. Port of ZenKit's <c>VfsNode</c>.
/// </summary>
public sealed class VfsNode
{
	private readonly Dictionary<string, VfsNode> _children;
	private readonly IArchiveData _archive;
	private readonly long _offset;
	private readonly long _size;

	private VfsNode(string name, DateTime time)
	{
		Name = name;
		Time = time;
		_children = new Dictionary<string, VfsNode>(StringComparer.OrdinalIgnoreCase);
	}

	private VfsNode(string name, DateTime time, IArchiveData archive, long offset, long size)
	{
		Name = name;
		Time = time;
		_archive = archive ?? throw new ArgumentNullException(nameof(archive));
		_offset = offset;
		_size = size;
	}

	public string Name { get; }

	public DateTime Time { get; }

	public VfsNodeType Type => _archive == null ? VfsNodeType.Directory : VfsNodeType.File;

	public IReadOnlyCollection<VfsNode> Children =>
		_children == null ? Array.Empty<VfsNode>() : _children.Values;

	internal static VfsNode CreateDirectory(string name, DateTime time) => new VfsNode(name, time);

	internal static VfsNode CreateFile(string name, DateTime time, IArchiveData archive, long offset, long size) =>
		new VfsNode(name, time, archive, offset, size);

	/// <summary>Finds a direct child by name; the comparison is case-insensitive.</summary>
	public VfsNode Child(string name)
	{
		if (_children == null || name == null)
		{
			return null;
		}

		return _children.TryGetValue(TrimTrailingWhitespace(name), out var child) ? child : null;
	}

	/// <summary>Opens the file contents. The node must belong to a live <see cref="Vfs"/>.</summary>
	public Read OpenRead()
	{
		if (Type != VfsNodeType.File)
		{
			throw new InvalidOperationException($"'{Name}' is a directory.");
		}

		return _archive.OpenRead(_offset, _size);
	}

	/// <summary>Reads the whole file into memory; used for audio and other stream consumers.</summary>
	public byte[] ReadAllBytes()
	{
		if (Type != VfsNodeType.File)
		{
			throw new InvalidOperationException($"'{Name}' is a directory.");
		}

		if (_size > int.MaxValue)
		{
			throw new VfsException($"'{Name}' is too large to read into memory ({_size} bytes).");
		}

		var result = new byte[(int)_size];
		var reader = OpenRead();
		var total = 0;
		while (total < result.Length)
		{
			var read = reader.ReadBytes(result.AsSpan(total));
			if (read <= 0)
			{
				break;
			}

			total += read;
		}

		return result;
	}

	internal VfsNode Create(VfsNode node)
	{
		if (_children == null)
		{
			throw new InvalidOperationException($"'{Name}' is a file.");
		}

		_children.Remove(node.Name);
		_children[node.Name] = node;
		return node;
	}

	internal bool Remove(string name)
	{
		if (_children == null || name == null)
		{
			return false;
		}

		return _children.Remove(TrimTrailingWhitespace(name));
	}

	internal static string TrimTrailingWhitespace(string value)
	{
		var end = value.Length;
		while (end > 0 && char.IsWhiteSpace(value[end - 1]))
		{
			--end;
		}

		return end == value.Length ? value : value.Substring(0, end);
	}
}

/// <summary>
/// Managed port of ZenKit's virtual file system: mounts Union VDF archives and exposes
/// their records through a case-insensitive tree.
/// </summary>
public sealed class Vfs : IDisposable
{
	private const string DiskSignatureG1 = "PSVDSC_V2.00\r\n\r\n";
	private const string DiskSignatureG2 = "PSVDSC_V2.00\n\r\n\r";
	private const string DiskSignatureVdfsTool = "PSVDSC_V2.00\x1A\x1A\x1A\x1A";

	private const uint VolumeFlagNormal = 0x50;
	private const uint VolumeFlagZipped = 0xA0;

	private const int CatalogEntrySize = 80;

	private readonly List<IArchiveData> _archives = new List<IArchiveData>();
	private readonly VfsNode _root = VfsNode.CreateDirectory("/", DateTime.MinValue);

	public VfsNode Root => _root;

	/// <summary>Mounts a VDF from disk. Its data is memory-mapped, not copied.</summary>
	public void MountDisk(string path, VfsOverwriteBehavior overwrite = VfsOverwriteBehavior.Older)
	{
		if (string.IsNullOrEmpty(path))
		{
			throw new ArgumentNullException(nameof(path));
		}

		var archive = new MappedArchive(path);
		try
		{
			MountDiskCore(archive, path, overwrite);
			_archives.Add(archive);
		}
		catch
		{
			archive.Dispose();
			throw;
		}
	}

	/// <summary>Mounts a VDF held in memory. The array must not be modified afterwards.</summary>
	public void MountDisk(byte[] data, VfsOverwriteBehavior overwrite = VfsOverwriteBehavior.Older)
	{
		if (data == null)
		{
			throw new ArgumentNullException(nameof(data));
		}

		var archive = new MemoryArchive(data);
		try
		{
			MountDiskCore(archive, "(memory)", overwrite);
			_archives.Add(archive);
		}
		catch
		{
			archive.Dispose();
			throw;
		}
	}

	/// <summary>
	/// Mounts a node into an existing directory. The node is consumed: its children are
	/// moved into the target tree, so it must not be used afterwards.
	/// </summary>
	public void Mount(VfsNode node, string parent, VfsOverwriteBehavior overwrite = VfsOverwriteBehavior.All)
	{
		if (node == null)
		{
			throw new ArgumentNullException(nameof(node));
		}

		var target = Resolve(parent ?? string.Empty);
		if (target == null)
		{
			throw new VfsNotFoundException(parent ?? string.Empty);
		}

		if (target.Type != VfsNodeType.Directory)
		{
			throw new VfsFileExistsException(parent ?? string.Empty);
		}

		MountInto(node, target, overwrite);
	}

	/// <summary>Creates all missing directories in <paramref name="path"/>.</summary>
	public VfsNode Mkdir(string path)
	{
		if (path == null)
		{
			throw new ArgumentNullException(nameof(path));
		}

		var context = _root;
		while (path.Length > 0)
		{
			var next = path.IndexOf('/');
			if (next == 0)
			{
				path = path.Substring(1);
				continue;
			}

			var name = next < 0 ? path : path.Substring(0, next);
			var child = context.Child(name);
			if (child == null)
			{
				context = context.Create(VfsNode.CreateDirectory(name, DateTime.Now));
			}
			else if (child.Type == VfsNodeType.File)
			{
				throw new VfsFileExistsException(name);
			}
			else
			{
				context = child;
			}

			if (next < 0)
			{
				break;
			}

			path = path.Substring(next + 1);
		}

		return context;
	}

	public bool Remove(string path)
	{
		if (path == null)
		{
			throw new ArgumentNullException(nameof(path));
		}

		var lastSlash = path.LastIndexOf('/');
		var parentPath = lastSlash < 0 ? string.Empty : path.Substring(0, lastSlash);
		var childName = path.Substring(lastSlash + 1);

		var parent = Resolve(parentPath);
		return parent != null && parent.Type == VfsNodeType.Directory && parent.Remove(childName);
	}

	/// <summary>Resolves a slash-separated path, or null when it does not exist.</summary>
	public VfsNode Resolve(string path)
	{
		if (path == null)
		{
			return null;
		}

		var context = _root;
		while (context != null && path.Length > 0)
		{
			var next = path.IndexOf('/');
			if (next == 0)
			{
				path = path.Substring(1);
				continue;
			}

			if (context.Type != VfsNodeType.Directory)
			{
				return null;
			}

			var name = next < 0 ? path : path.Substring(0, next);
			context = context.Child(name);

			if (next < 0)
			{
				break;
			}

			path = path.Substring(next + 1);
		}

		return context;
	}

	/// <summary>Finds the first node with this name anywhere in the tree.</summary>
	public VfsNode Find(string name)
	{
		if (name == null)
		{
			return null;
		}

		var tree = new Stack<VfsNode>();
		tree.Push(_root);
		while (tree.Count > 0)
		{
			var node = tree.Pop();
			var child = node.Child(name);
			if (child != null)
			{
				return child;
			}

			foreach (var candidate in node.Children)
			{
				if (candidate.Type == VfsNodeType.Directory)
				{
					tree.Push(candidate);
				}
			}
		}

		return null;
	}

	public void Dispose()
	{
		foreach (var archive in _archives)
		{
			archive.Dispose();
		}

		_archives.Clear();
	}

	private void MountDiskCore(IArchiveData archive, string sourceName, VfsOverwriteBehavior overwrite)
	{
		var reader = archive.OpenRead(0, archive.Length);

		_ = reader.ReadString(256);
		var signature = reader.ReadString(16);
		_ = reader.ReadUInt();
		_ = reader.ReadUInt();
		var timestamp = DosToTime(reader.ReadUInt());
		_ = reader.ReadUInt();
		var catalogOffset = reader.ReadUInt();
		var volumeFlags = reader.ReadUInt();

		if (signature != DiskSignatureG1 && signature != DiskSignatureG2 &&
		    signature != DiskSignatureVdfsTool)
		{
			throw new VfsBrokenDiskException(signature);
		}

		if (volumeFlags == VolumeFlagZipped)
		{
			throw new VfsUnsupportedException(
				$"'{sourceName}' uses the compressed Union ZippedStream format, which the managed VFS does not read yet.");
		}

		if (volumeFlags != VolumeFlagNormal)
		{
			Log.Warning($"Vfs: unknown volume flags 0x{volumeFlags:X} in '{sourceName}', assuming uncompressed.");
		}
		if (catalogOffset == 0)
		{
			catalogOffset = (uint)reader.Position;
		}

		if (catalogOffset > archive.Length)
		{
			throw new VfsBrokenDiskException(sourceName, $"catalog offset {catalogOffset} is past the end");
		}

		bool LoadEntry(VfsNode parent)
		{
			if (reader.Position >= reader.Length)
			{
				Log.Warning($"Vfs: catalog in '{sourceName}' ended before a 'last' entry.");
				return true;
			}

			var name = reader.ReadFixedString(64);
			var offset = reader.ReadUInt();
			var size = reader.ReadUInt();
			var type = reader.ReadUInt();
			_ = reader.ReadUInt();

			var directory = (type & 0x80000000) != 0;
			var last = (type & 0x40000000) != 0;
			var existing = parent.Child(name);

			if (directory)
			{
				if (existing == null)
				{
					existing = parent.Create(VfsNode.CreateDirectory(name, timestamp));
				}
				else if (existing.Type != VfsNodeType.Directory)
				{
					if (!ShouldReplace(existing, timestamp, overwrite))
					{
						return last;
					}

					parent.Remove(name);
					existing = parent.Create(VfsNode.CreateDirectory(name, timestamp));
				}

				var childrenOffset = catalogOffset + (long)offset * CatalogEntrySize;
				var restore = reader.Position;
				if (childrenOffset >= 0 && childrenOffset < archive.Length)
				{
					reader.Seek(childrenOffset);
					while (!LoadEntry(existing))
					{
					}
				}
				else
				{
					Log.Warning($"Vfs: directory '{name}' in '{sourceName}' has an invalid child table.");
				}

				reader.Seek(restore);
			}
			else
			{
				if (offset + (long)size > archive.Length)
				{
					return last;
				}

				if (existing != null)
				{
					if (!ShouldReplace(existing, timestamp, overwrite))
					{
						return last;
					}

					parent.Remove(name);
				}

				parent.Create(VfsNode.CreateFile(name, timestamp, archive, offset, size));
			}

			return last;
		}

		reader.Seek(catalogOffset);
		while (!LoadEntry(_root))
		{
		}
	}

	private static void MountInto(VfsNode source, VfsNode target, VfsOverwriteBehavior overwrite)
	{
		var existing = target.Child(source.Name);
		if (existing == null)
		{
			target.Create(source);
			return;
		}

		if (existing.Type == VfsNodeType.File || source.Type == VfsNodeType.File)
		{
			if (!ShouldReplace(existing, source.Time, overwrite))
			{
				return;
			}

			target.Remove(existing.Name);
			target.Create(source);
			return;
		}

		foreach (var child in source.Children)
		{
			MountInto(child, existing, overwrite);
		}
	}

	private static bool ShouldReplace(VfsNode existing, DateTime time, VfsOverwriteBehavior overwrite)
	{
		switch (overwrite)
		{
			case VfsOverwriteBehavior.None:
				return false;
			case VfsOverwriteBehavior.Newer:
				return existing.Time > time;
			case VfsOverwriteBehavior.Older:
				return existing.Time < time;
			default:
				return true;
		}
	}

	private static DateTime DosToTime(uint dos)
	{
		var year = (int)((dos >> 25) & 0x7F) + 1980;
		var month = (int)((dos >> 21) & 0x0F);
		var day = (int)((dos >> 16) & 0x1F);
		var hour = (int)((dos >> 11) & 0x1F);
		var minute = (int)((dos >> 5) & 0x3F);
		var second = (int)(dos & 0x1F) * 2;

		if (month < 1 || month > 12 || day < 1 || day > 31 || hour > 23 || minute > 59 ||
		    second > 59)
		{
			return DateTime.MinValue;
		}

		return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local);
	}
}
