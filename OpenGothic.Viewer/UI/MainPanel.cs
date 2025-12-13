using Myra.Graphics2D.UI;
using Nursia;
using System;
using System.IO;
using System.Linq;
using ZenKit;

namespace OpenGothic.Viewer.UI;

public partial class MainPanel
{
	private readonly Assets _assets;

	public MainPanel()
	{
		BuildUI();

		_textBoxFilter.TextChanged += (s, a) => RebuildList();
		_listItems.SelectedIndexChanged += _listItems_SelectedIndexChanged;

		_assets = new Assets(Configuration.GamePath);
		RebuildList();
	}

	private void _listItems_SelectedIndexChanged(object sender, EventArgs e)
	{
		var label = (Label)_listItems.SelectedItem;

		var model = _assets.GetModel(Nrs.GraphicsDevice, label.Text);
	}

	private void RebuildList()
	{
		_listItems.Widgets.Clear();

		foreach (var key in _assets.Keys)
		{
			var ext = Path.GetExtension(key);

			if (ext != ".MDL")
			{
				continue;
			}

			if (!string.IsNullOrEmpty(_textBoxFilter.Text) && !key.Contains(_textBoxFilter.Text, StringComparison.InvariantCultureIgnoreCase))
			{
				// Apply filter
				continue;
			}

			_listItems.Widgets.Add(new Label
			{
				Text = key
			});
		}
	}
}