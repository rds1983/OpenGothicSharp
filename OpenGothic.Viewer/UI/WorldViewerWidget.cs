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

namespace OpenGothic.Viewer.UI;

public class WorldViewerWidget : Widget, IViewerWidget
{
	private readonly CameraInputController _controller;
	private readonly ForwardRenderer _renderer;
	private readonly SceneNode _root = new SceneNode(), _worldRoot = new SceneNode();
	private MeshNode[] _world;

	public RenderStatistics RenderStatistics => _renderer.Statistics;

	public MeshNode[] World
	{
		get => _world;

		set
		{
			_worldRoot.Children.Clear();

			_world = value;
			if (_world != null)
			{
				foreach (var node in _world)
				{
					_worldRoot.Children.Add(node);
				}
			}
		}
	}

	public WorldViewerWidget()
	{
		_renderer = new ForwardRenderer();

		// Light
		_root.Children.Add(new DirectLight { Rotation = new Vector3(225, 45, 0), CastsShadow = true });

		_root.Children.Add(_worldRoot);

		var camera = new Camera
		{
			NearPlane = 10.0f,
			FarPlane = 100000.0f
		};

		_controller = new CameraInputController(camera)
		{
			SpeedBoost = 500.0f
		};

		Nrs.GraphicsSettings.MaxShadowDistance = 20000.0f;
		Nrs.GraphicsSettings.ShadowBias = 0.0001f;
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
			var target = _renderer.RenderToTarget(_root, _controller.Camera, Configuration.RenderEnvironment, bounds.Width, bounds.Height);

			context.Draw(target, ActualBounds, Color.White);
		}
		catch (Exception)
		{
			Nrs.GraphicsDevice.ScissorRectangle = scissor;
		}
	}
}
