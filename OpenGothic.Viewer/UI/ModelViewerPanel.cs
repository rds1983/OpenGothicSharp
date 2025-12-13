using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Myra.Events;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Nursia;
using System;

namespace OpenGothic.Viewer.UI;

public partial class ModelViewerPanel
{
	private readonly ModelViewerWidget _viewerWidget;

	public DrModel Model
	{
		get => _viewerWidget.Model;

		set
		{
			_viewerWidget.Model = value;

			_comboAnimations.Widgets.Clear();

			if (value.Animations != null)
			{
				// Default pose
				_comboAnimations.Widgets.Add(new Label());
				foreach (var pair in value.Animations)
				{
					var str = pair.Key;
					if (string.IsNullOrEmpty(str))
					{
						str = "(default)";
					}

					_comboAnimations.Widgets.Add(
						new Label
						{
							Text = str,
							Tag = pair.Value
						});
				}
			}

			if (_comboAnimations.Widgets.Count > 1)
			{
				// First animation
				_comboAnimations.SelectedIndex = 1;
				_comboAnimations.Enabled = true;
				_buttonPlayStop.Enabled = true;
			}
			else
			{
				_comboAnimations.Enabled = false;
				_buttonPlayStop.Enabled = false;
			}

			ResetAnimation();
		}
	}

	public ModelViewerPanel()
	{
		BuildUI();

		_viewerWidget = new ModelViewerWidget
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch
		};
		
		_panelModelViewer.Widgets.Add(_viewerWidget);

		_comboAnimations.Widgets.Clear();
		_comboAnimations.SelectedIndexChanged += _comboAnimations_SelectedIndexChanged;

		_comboPlaybackMode.SelectedIndex = 0;
		_comboPlaybackMode.SelectedIndexChanged += (s, a) =>
		{
			_viewerWidget.Player.PlaybackMode = (PlaybackMode)_comboPlaybackMode.SelectedIndex.Value;
		};

		_sliderSpeed.ValueChanged += (s, a) =>
		{
			_labelSpeed.Text = _sliderSpeed.Value.ToString("0.00");
			_viewerWidget.Player.Speed = _sliderSpeed.Value;
		};


		_sliderTime.ValueChangedByUser += _sliderTime_ValueChanged;
		_sliderTime.ValueChanged += (s, a) =>
		{
			_labelTime.Text = _sliderTime.Value.ToString("0.00");
		};

		_buttonPlayStop.Click += _buttonPlayStop_Click;

		_checkDrawBoundingBoxes.IsCheckedChanged += (s, a) =>
		{
			Nrs.DebugSettings.DrawBoundingBoxes = _checkDrawBoundingBoxes.IsChecked;
		};

		_viewerWidget.TimeChanged += (s, a) =>
		{
			var player = _viewerWidget.Player;
			if (player.AnimationClip == null)
			{
				return;
			}

			var k = (float)(player.Time / player.AnimationClip.Duration);

			var slider = _sliderTime;
			slider.Value = slider.Minimum + k * (slider.Maximum - slider.Minimum);
		};
	}

	private void ResetAnimation()
	{
		_sliderTime.Value = _sliderTime.Minimum;
	}

	private void _buttonPlayStop_Click(object sender, EventArgs e)
	{
		_viewerWidget.IsAnimating = !_viewerWidget.IsAnimating;

		var label = (Label)_buttonPlayStop.Content;
		label.Text = _viewerWidget.IsAnimating ? "Stop" : "Play";
	}

	private void _sliderTime_ValueChanged(object sender, ValueChangedEventArgs<float> e)
	{
		if (!_viewerWidget.Player.IsPlaying)
		{
			return;
		}

		var k = (e.NewValue - _sliderTime.Minimum) / (_sliderTime.Maximum - _sliderTime.Minimum);
		var passed = _viewerWidget.Player.AnimationClip.Duration * k;
		_viewerWidget.Player.Time = passed;
	}

	private void _comboAnimations_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (_comboAnimations.SelectedItem == null || string.IsNullOrEmpty(((Label)_comboAnimations.SelectedItem).Text))
		{
			_viewerWidget.Player.StopClip();
		}
		else
		{
			var clip = (AnimationClip)((Label)_comboAnimations.SelectedItem).Tag;
			if (_checkCrossfade.IsChecked)
			{
				_viewerWidget.Player.CrossFade(clip.Name, TimeSpan.FromSeconds(0.5f));
			}
			else
			{
				_viewerWidget.Player.StartClip(clip.Name);
			}
		}

		ResetAnimation();
	}

	public override void InternalRender(RenderContext context)
	{
		var stats = _viewerWidget.RenderStatistics;
		_labelDrawCalls.Text = stats.DrawCalls.ToString();
		_labelEffectsSwitches.Text = stats.EffectsSwitches.ToString();
		_labelPrimitivesDrawn.Text = stats.PrimitivesDrawn.ToString();
		_labelVerticesDrawn.Text = stats.VerticesDrawn.ToString();
		_labelPasses.Text = stats.Passes.ToString();

		base.InternalRender(context);
	}
}