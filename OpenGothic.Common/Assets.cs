using Microsoft.Xna.Framework.Graphics;
using Nursia;
using OpenGothic.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZenKit;

namespace OpenGothic;

public partial class Assets
{
	private class RecordInfo
	{
		public string VfsName { get; }
		public Vfs Vfs { get; }
		public VfsNode Node { get; }
		public string Name => Node.Name;

		public RecordInfo(string vfsName, Vfs vfs, VfsNode node)
		{
			VfsName = vfsName;
			Vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
			Node = node ?? throw new ArgumentNullException(nameof(node));
		}
	}

	private readonly Dictionary<string, List<RecordInfo>> _allRecords = new Dictionary<string, List<RecordInfo>>();
	private readonly Dictionary<string, object> _cache = new Dictionary<string, object>();

	public string Folder { get; }

	public string[] Keys { get; }

	public Assets(string folder)
	{
		if (string.IsNullOrEmpty(folder))
		{
			throw new ArgumentNullException(nameof(folder));
		}

		Folder = folder;

		Load();

		Keys = (from k in _allRecords.Keys orderby k select k).ToArray();
	}

	private void AddRecordsRecursively(string vfsName, Vfs vfs, VfsNode vfsRoot)
	{
		if (vfsRoot.Name.Contains("."))
		{
			List<RecordInfo> existingRecords;
			if (!_allRecords.TryGetValue(vfsRoot.Name, out existingRecords))
			{
				existingRecords = new List<RecordInfo>();
				_allRecords[vfsRoot.Name.ToUpper()] = existingRecords;
			}
			else
			{
				Nrs.LogInfo($"Record '{vfsRoot.Name}' is already in the list. Previous vfs: {Path.GetFileName(existingRecords[0].VfsName)}");
			}

			var record = new RecordInfo(vfsName, vfs, vfsRoot);
			existingRecords.Add(record);
		}

		foreach (var child in vfsRoot.Children)
		{
			AddRecordsRecursively(vfsName, vfs, child);
		}
	}

	private void Load()
	{
		Nrs.LogInfo($"Gothic Path: {Folder}");

		var dataFolder = Path.Combine(Folder, "Data");

		var vdfs = Directory.GetFiles(dataFolder, "*.vdf", SearchOption.AllDirectories);
		foreach (var vfsName in vdfs)
		{
			Nrs.LogInfo($"Processing file {vfsName}");

			var vfs = new Vfs();

			vfs.MountDisk(vfsName, VfsOverwriteBehavior.Older);

			AddRecordsRecursively(vfsName, vfs, vfs.Root);
		}
	}

	private T Get<T>(GraphicsDevice device, string name, Func<GraphicsDevice, string, T> loader) where T : class
	{
		object obj;
		if (_cache.TryGetValue(name, out obj))
		{
			return (T)obj;
		}

		Nrs.LogInfo($"Quering {typeof(T).Name} '{name}'");

		var result = loader(device, name);
		_cache[name] = result;

		return result;
	}

	private RecordInfo GetLastRecord(string name)
	{
		var records = _allRecords[name.ToUpper()];

		return records[records.Count - 1];
	}

	private Texture2D LoadTexture(GraphicsDevice device, string name)
	{
		List<RecordInfo> records;

		name = name.ToUpper();
		if (!_allRecords.TryGetValue(name, out records))
		{
			// Add '-C' and .TEX ext
			var newName = Path.GetFileNameWithoutExtension(name);
			newName += "-C";
			newName = Path.ChangeExtension(newName, "TEX");
			if (!_allRecords.TryGetValue(newName, out records))
			{
				throw new Exception($"Unable to find texture '{name}'");
			}
		}

		var record = records[records.Count - 1];

		var texture = new ZenKit.Texture(record.Node.Buffer);

		var mipmapCount = texture.MipmapCount;
		var result = new Texture2D(device, texture.Width, texture.Height, mipmapCount > 1, texture.Format.ToXNA());

		var allMipmapsRaw = texture.AllMipmapsRaw;
		for (var i = 0; i < mipmapCount; ++i)
		{
			result.SetData(i, null, allMipmapsRaw[i], 0, allMipmapsRaw[i].Length);
		}

		return result;
	}


	public Texture2D GetTexture(GraphicsDevice device, string name) => Get(device, name, LoadTexture);
}
