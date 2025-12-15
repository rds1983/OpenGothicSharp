using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Nursia;
using OpenGothic.UI;
using System;
using System.IO;

namespace OpenGothic.Viewer.UI;

public partial class MainPanel
{
	private readonly Assets _assets;
	private IViewerWidget _viewerWidget;

	public MainPanel()
	{
		BuildUI();

		_topSplitPane.SetSplitterPosition(0, 0.3f);

		_textBoxFilter.TextChanged += (s, a) => RebuildList();
		_checkMsbFilter.IsCheckedChanged += (s, a) => RebuildList();
		_checkZenFilter.IsCheckedChanged += (s, a) => RebuildList();

		_listItems.SelectedIndexChanged += _listItems_SelectedIndexChanged;
		_buttonOptions.Click += (s, a) =>
		{
			var optionsWindow = new OptionsWindow();

			optionsWindow.Show(Desktop);
		};

		_assets = new Assets(Configuration.GamePath);
		RebuildList();
	}

	private void _listItems_SelectedIndexChanged(object sender, EventArgs e)
	{
		var label = (Label)_listItems.SelectedItem;

		Widget widget;
		if (label.Text.EndsWith(".MSB"))
		{
			var model = _assets.GetModel(Nrs.GraphicsDevice, label.Text);

			var modelViewerPanel = new ModelViewerPanel
			{
				Model = model
			};

			widget = modelViewerPanel;
		}
		else
		{
			var world = _assets.GetWorld(Nrs.GraphicsDevice, label.Text);

			var worldViewer = new WorldViewerWidget
			{
				World = world
			};

			widget = worldViewer;
		}

		_panelViewer.Widgets.Clear();

		widget.HorizontalAlignment = HorizontalAlignment.Stretch;
		widget.VerticalAlignment = VerticalAlignment.Stretch;
		_panelViewer.Widgets.Add(widget);

		_viewerWidget = (IViewerWidget)widget;

	}

	private void RebuildList()
	{
		_listItems.Widgets.Clear();

		foreach (var key in _assets.Keys)
		{
			var ext = Path.GetExtension(key).ToUpper();

			if ((ext == ".MSB" && _checkMsbFilter.IsChecked) ||
				(ext == ".ZEN" && _checkZenFilter.IsChecked))
			{
			}
			else
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

	public override void InternalRender(RenderContext context)
	{
		if (_viewerWidget != null)
		{
			var stats = _viewerWidget.RenderStatistics;
			_labelDrawCalls.Text = stats.DrawCalls.ToString();
			_labelEffectsSwitches.Text = stats.EffectsSwitches.ToString();
			_labelPrimitivesDrawn.Text = stats.PrimitivesDrawn.ToString();
			_labelVerticesDrawn.Text = stats.VerticesDrawn.ToString();
			_labelPasses.Text = stats.Passes.ToString();
		}

		base.InternalRender(context);
	}
}