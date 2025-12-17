using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using Myra.Events;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Nursia;
using Nursia.Env;
using Nursia.Rendering;
using Nursia.SceneGraph;
using Nursia.SceneGraph.Lights;
using Nursia.Utilities;
using System;

namespace OpenGothic.Viewer.UI;

public partial class ModelViewerPanel : IViewerWidget
{
	private readonly CameraInputController _controller;
	private readonly ForwardRenderer _renderer;
	private NursiaModelNode _modelNode;
	private readonly SceneNode _scene = new SceneNode();

	public AnimationController Player { get; private set; }

	public bool IsAnimating { get; set; }

	public Camera Camera => _controller.Camera;

	public RenderStatistics RenderStatistics => _renderer.Statistics;

	public NursiaModelNode ModelNode
	{
		get => _modelNode;

		set
		{
			if (_modelNode != null)
			{
				_modelNode.RemoveFromParent();
			}

			_modelNode = value;

			Player = new AnimationController(_modelNode.ModelInstance);
			Player.TimeChanged += (s, a) =>
			{
				var player = Player;
				if (player.AnimationClip == null)
				{
					return;
				}

				var k = (float)(player.Time / player.AnimationClip.Duration);

				var slider = _sliderTime;
				slider.Value = slider.Minimum + k * (slider.Maximum - slider.Minimum);
			};

			_scene.Children.Add(_modelNode);

			var model = _modelNode.Model;

			// Reset camera
			var camera = _controller.Camera;
			if (model != null)
			{
				var bb = _modelNode.BoundingBox.Value;
				var min = bb.Min;
				var max = bb.Max;
				var center = (min + max) / 2;
				var cameraPosition = new Vector3(center.X, center.Y, center.Z + (max.Z - min.Z) * 3);

				camera.View = Matrix.CreateLookAt(cameraPosition, center, Vector3.Up);

				var size = Math.Max(max.X - min.X, max.Y - min.Y);
				size = Math.Max(size, max.Z - min.Z);

				camera.NearPlane = size / 1000.0f;
				camera.FarPlane = size * 10.0f;
			}
			else
			{
				camera.View = Matrix.CreateLookAt(Vector3.One, Vector3.Zero, Vector3.Up);
			}

			_comboAnimations.Widgets.Clear();

			if (model.Animations != null)
			{
				// Default pose
				_comboAnimations.Widgets.Add(new Label());
				foreach (var pair in model.Animations)
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

		_renderer = new ForwardRenderer();

		// Front light
		_scene.Children.Add(new DirectLight { Rotation = new Vector3(45, 45, 0), CastsShadow = false });

		// Back light
		_scene.Children.Add(new DirectLight { Rotation = new Vector3(225, 45, 0), CastsShadow = false });


		_controller = new CameraInputController(new Camera());

		_comboAnimations.Widgets.Clear();
		_comboAnimations.SelectedIndexChanged += _comboAnimations_SelectedIndexChanged;

		_comboPlaybackMode.SelectedIndex = 0;
		_comboPlaybackMode.SelectedIndexChanged += (s, a) =>
		{
			Player.PlaybackMode = (PlaybackMode)_comboPlaybackMode.SelectedIndex.Value;
		};

		_sliderSpeed.ValueChanged += (s, a) =>
		{
			_labelSpeed.Text = _sliderSpeed.Value.ToString("0.00");
			Player.Speed = _sliderSpeed.Value;
		};


		_sliderTime.ValueChangedByUser += _sliderTime_ValueChanged;
		_sliderTime.ValueChanged += (s, a) =>
		{
			_labelTime.Text = _sliderTime.Value.ToString("0.00");
		};

		_buttonPlayStop.Click += _buttonPlayStop_Click;
	}

	private void ResetAnimation()
	{
		_sliderTime.Value = _sliderTime.Minimum;
	}

	private void _buttonPlayStop_Click(object sender, EventArgs e)
	{
		IsAnimating = !IsAnimating;

		var label = (Label)_buttonPlayStop.Content;
		label.Text = IsAnimating ? "Stop" : "Play";
	}

	private void _sliderTime_ValueChanged(object sender, ValueChangedEventArgs<float> e)
	{
		if (!Player.IsPlaying)
		{
			return;
		}

		var k = (e.NewValue - _sliderTime.Minimum) / (_sliderTime.Maximum - _sliderTime.Minimum);
		var passed = Player.AnimationClip.Duration * k;
		Player.Time = passed;
	}

	private void _comboAnimations_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (_comboAnimations.SelectedItem == null || string.IsNullOrEmpty(((Label)_comboAnimations.SelectedItem).Text))
		{
			Player.StopClip();
		}
		else
		{
			var clip = (AnimationClip)((Label)_comboAnimations.SelectedItem).Tag;
			if (_checkCrossfade.IsChecked)
			{
				Player.CrossFade(clip.Name, TimeSpan.FromSeconds(0.5f));
			}
			else
			{
				Player.StartClip(clip.Name);
			}
		}

		ResetAnimation();
	}

	public override void InternalRender(RenderContext context)
	{
		_controller.Update();

		if (ModelNode != null && IsAnimating)
		{
			Player.Update(ViewerGame.LastGameTime.ElapsedGameTime);
		}

		var bounds = ActualBounds;

		var p = ToGlobal(bounds.Location);
		bounds.X = p.X;
		bounds.Y = p.Y;

		// Save scissor as it would be destroyed on exception
		var device = Nrs.GraphicsDevice;
		var scissor = device.ScissorRectangle;

		try
		{
			var target = _renderer.RenderToTarget(_scene, _controller.Camera, RenderEnvironment.Default, bounds.Width, bounds.Height);

			context.Draw(target, ActualBounds, Color.White);
		}
		catch (Exception)
		{
			Nrs.GraphicsDevice.ScissorRectangle = scissor;
		}

		base.InternalRender(context);
	}
}