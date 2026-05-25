using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nursia;

namespace OpenGothic;

public class MainGame : Game
{
	private readonly GraphicsDeviceManager _graphics;
	private readonly FramesPerSecondCounter _fpsCounter = new FramesPerSecondCounter();
	private string _path;

	public static GameTime LastGameTime { get; private set; }

	public MainGame(string path)
	{
		_path = path;

		_graphics = new GraphicsDeviceManager(this)
		{
			PreferredBackBufferWidth = 1200,
			PreferredBackBufferHeight = 800
		};

		Window.AllowUserResizing = true;
		IsMouseVisible = true;

		if (Configuration.NoFixedStep)
		{
			IsFixedTimeStep = false;
			_graphics.SynchronizeWithVerticalRetrace = false;
		}

		Configuration.GamePath = _path;
	}

	protected override void LoadContent()
	{
		base.LoadContent();

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

	}
}