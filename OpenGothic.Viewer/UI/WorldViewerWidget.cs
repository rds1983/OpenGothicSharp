using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Nursia;
using Nursia.Rendering;
using Nursia.SceneGraph;
using Nursia.SceneGraph.Lights;
using Nursia.Utilities;
using System;

namespace OpenGothic.Viewer.UI;

public class WorldViewerWidget : Widget, IViewerWidget
{
	private readonly CameraInputController _controller;
	private readonly ForwardRenderer _renderer;
	private readonly SceneNode _root = new SceneNode(), _worldRoot = new SceneNode();

	public Camera Camera => _controller.Camera;

	public RenderStatistics RenderStatistics => _renderer.Statistics;

	public WorldGrid World { get; set; }

	public WorldViewerWidget()
	{
		_renderer = new ForwardRenderer();

		// Light
		_root.Children.Add(new DirectLight { Rotation = new Vector3(225, 45, 0), CastsShadow = true });

		_root.Children.Add(_worldRoot);

		var camera = new Camera
		{
			NearPlane = 10.0f,
			FarPlane = Constants.MaxDistance
		};

		_controller = new CameraInputController(camera)
		{
			SpeedBoost = 500.0f
		};

		Nrs.GraphicsSettings.MaxShadowDistance = Constants.MaxShadowDistance;
		Nrs.GraphicsSettings.Cascades = 2;
	}

	public override void InternalRender(RenderContext context)
	{
		base.InternalRender(context);

		_controller.Update();

		var bounds = ActualBounds;

		var p = ToGlobal(bounds.Location);
		bounds.X = p.X;
		bounds.Y = p.Y;

		// Save scissor as it would be destroyed on exception
		var device = Nrs.GraphicsDevice;
		var scissor = device.ScissorRectangle;

		try
		{
			_root.CustomBoxes.Clear();
			_worldRoot.Children.Clear();
			for(var i = 0; i < Constants.GridSize; ++i)
			{
				for(var j = 0;  j < Constants.GridSize; ++j)
				{
					var cell = World.Cells[i, j];


					_root.CustomBoxes.Add(cell.BoundingBox);

					if (_controller.Camera.Frustum.Intersects(cell.BoundingBox) && cell.Root.Children.Count > 0)
					{
						_worldRoot.Children.Add(cell.Root);
					}
				}
			}

			var target = _renderer.RenderToTarget(_root, _controller.Camera, Configuration.RenderEnvironment, bounds.Width, bounds.Height);

			context.Draw(target, ActualBounds, Color.White);
		}
		catch (Exception)
		{
			Nrs.GraphicsDevice.ScissorRectangle = scissor;
		}
	}
}
