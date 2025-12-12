using Myra.Graphics2D.UI;
using Nursia;
using System;
using System.Collections.Generic;
using System.IO;
using ZenKit;

namespace OpenGothic.Viewer.UI;

public partial class MainPanel
{
	private readonly List<string> _allRecords = new List<string>();

	public MainPanel()
	{
		BuildUI();

		_textBoxFilter.TextChanged += (s, a) => RebuildList();
	}

	private void AddRecordsRecursively(VfsNode vfsRoot)
	{
		if (vfsRoot.Name.Contains("."))
		{
			if (_allRecords.Contains(vfsRoot.Name))
			{
				var k = 5;
			}


			_allRecords.Add(vfsRoot.Name);
		}

		foreach (var child in vfsRoot.Children)
		{
			AddRecordsRecursively(child);
		}
	}

	private void RebuildList()
	{
		_listItems.Widgets.Clear();

		foreach (var record in _allRecords)
		{
			if (!string.IsNullOrEmpty(_textBoxFilter.Text) && !record.Contains(_textBoxFilter.Text, StringComparison.InvariantCultureIgnoreCase))
			{
				// Apply filter
				continue;
			}

			_listItems.Widgets.Add(new Label
			{
				Text = record
			});
		}
	}

	public void Reload()
	{
		Nrs.LogInfo($"Gothic Path: {Configuration.GamePath}");

		var dataFolder = Path.Combine(Configuration.GamePath, "Data");

		var vdfs = Directory.GetFiles(dataFolder, "*.vdf", SearchOption.AllDirectories);
		foreach (var vdf in vdfs)
		{
			Nrs.LogInfo($"Processing file {vdf}");

			var vfs = new Vfs();

			vfs.MountDisk(vdf, VfsOverwriteBehavior.Older);

			AddRecordsRecursively(vfs.Root);
		}

		// Now update the ui
		RebuildList();
		/*        _tree.RemoveAllSubNodes();
				FillTreeRecursively(_tree, fileRoot);*/
	}
}