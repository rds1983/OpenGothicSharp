using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Nursia;
using Nursia.Env;
using Nursia.Rendering;
using Nursia.SceneGraph;
using Nursia.SceneGraph.Lights;
using Nursia.Utilities;
using System;

namespace OpenGothic.Viewer.UI
{
	public class ModelViewerWidget : Widget
	{
		private readonly CameraInputController _controller;
		private readonly ForwardRenderer _renderer;
		private readonly NursiaModelNode _modelNode = new NursiaModelNode();
		private readonly SceneNode _scene = new SceneNode();

		public AnimationController Player { get; }

		public bool IsAnimating { get; set; }
		public RenderStatistics RenderStatistics => _renderer.Statistics;

		public DrModel Model
		{
			get => _modelNode.Model;

			set
			{
				_modelNode.Model = value;

				// Reset camera
				var camera = _controller.Camera;
				if (_modelNode.Model != null)
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
			}
		}

		public event EventHandler TimeChanged
		{
			add => Player.TimeChanged += value;
			remove => Player.TimeChanged -= value;
		}

		public ModelViewerWidget()
		{
			_renderer = new ForwardRenderer();

			// Front light
			_scene.Children.Add(new DirectLight { Rotation = new Vector3(45, 45, 0), CastsShadow = false });

			// Back light
			_scene.Children.Add(new DirectLight { Rotation = new Vector3(225, 45, 0), CastsShadow = false });

			_scene.Children.Add(_modelNode);

			_controller = new CameraInputController(new Camera());

			Player = new AnimationController(_modelNode.ModelInstance);
		}

		public override void InternalRender(RenderContext context)
		{
			base.InternalRender(context);

			_controller.Update();

			if (_modelNode.Model != null && IsAnimating)
			{
				//				Player.Update(gameTime.ElapsedGameTime);
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

			// _renderer.Render(_scene, _controller.Camera);
		}
	}
}
