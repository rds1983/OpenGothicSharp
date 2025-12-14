using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.UI;
using OpenGothic.Viewer.UI;
using Nursia;

namespace OpenGothic.Viewer;

public class ViewerGame : Game
{
	private readonly GraphicsDeviceManager _graphics;
	private Desktop _desktop;
	private MainPanel _mainPanel;
	private readonly FramesPerSecondCounter _fpsCounter = new FramesPerSecondCounter();
	private string _path;

	public static GameTime LastGameTime { get; private set;  }

	public ViewerGame(string path)
	{
		_path = path;

		_graphics = new GraphicsDeviceManager(this)
		{
			PreferredBackBufferWidth = 1200,
			PreferredBackBufferHeight = 800
		};

		Window.AllowUserResizing = true;
		IsMouseVisible = true;

		Configuration.GamePath = _path;
	}

	protected override void LoadContent()
	{
		base.LoadContent();

		// UI
		MyraEnvironment.Game = this;
		_mainPanel = new MainPanel();

		_desktop = new Desktop
		{
			Root = _mainPanel
		};

		Nrs.Game = this;

		GraphicsDevice.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;

		// DebugSettings.DrawLights = true;
	}

	protected override void Update(GameTime gameTime)
	{
		base.Update(gameTime);

		LastGameTime = gameTime;
	}


	protected override void Draw(GameTime gameTime)
	{
		base.Draw(gameTime);

		GraphicsDevice.Clear(Color.Black);

		_mainPanel._labelFPS.Text = $"FPS: {_fpsCounter.FramesPerSecond}";

		_desktop.Render();

		_fpsCounter.OnFrameDrawn();
	}
}