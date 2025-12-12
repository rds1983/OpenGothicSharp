using Myra.Graphics2D.UI;
using Nursia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZenKit;

namespace OpenGothic.Viewer.UI;

public partial class MainPanel
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
	private readonly string[] IgnoredExtensions = new[]
	{
		".WAV",
		".TEX",
		".TGA"
	};

	public MainPanel()
	{
		BuildUI();

		_textBoxFilter.TextChanged += (s, a) => RebuildList();
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

	private void RebuildList()
	{
		_listItems.Widgets.Clear();

		var keys = (from k in _allRecords.Keys orderby k select k).ToList();
		foreach (var key in keys)
		{
			var ext = Path.GetExtension(key);

			var isIgnored = (from i in IgnoredExtensions where i.Equals(ext, StringComparison.OrdinalIgnoreCase) select i).Any();
			if (isIgnored)
			{
				continue;
			}

			if (!string.IsNullOrEmpty(_textBoxFilter.Text) && !key.Contains(_textBoxFilter.Text, StringComparison.InvariantCultureIgnoreCase))
			{
				// Apply filter
				continue;
			}

			var records = _allRecords[key];
			var record = records[records.Count - 1];

			_listItems.Widgets.Add(new Label
			{
				Text = key,
				Tag = record
			});
		}
	}

	public void Reload()
	{
		Nrs.LogInfo($"Gothic Path: {Configuration.GamePath}");

		var dataFolder = Path.Combine(Configuration.GamePath, "Data");

		var vdfs = Directory.GetFiles(dataFolder, "*.vdf", SearchOption.AllDirectories);
		foreach (var vfsName in vdfs)
		{
			Nrs.LogInfo($"Processing file {vfsName}");

			var vfs = new Vfs();

			vfs.MountDisk(vfsName, VfsOverwriteBehavior.Older);

			AddRecordsRecursively(vfsName, vfs, vfs.Root);
		}

		// Now update the ui
		RebuildList();
		/*        _tree.RemoveAllSubNodes();
				FillTreeRecursively(_tree, fileRoot);*/
	}
}