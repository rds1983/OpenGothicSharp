using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZenKit;

namespace OpenGothic
{
	public class Assets
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
					_allRecords[vfsRoot.Name] = existingRecords;
				}
				else
				{
					OG.LogInfo($"Record '{vfsRoot.Name}' is already in the list. Previous vfs: {Path.GetFileName(existingRecords[0].VfsName)}");
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
			OG.LogInfo($"Gothic Path: {Folder}");

			var dataFolder = Path.Combine(Folder, "Data");

			var vdfs = Directory.GetFiles(dataFolder, "*.vdf", SearchOption.AllDirectories);
			foreach (var vfsName in vdfs)
			{
				OG.LogInfo($"Processing file {vfsName}");

				var vfs = new Vfs();

				vfs.MountDisk(vfsName, VfsOverwriteBehavior.Older);

				AddRecordsRecursively(vfsName, vfs, vfs.Root);
			}
		}
	}
}
