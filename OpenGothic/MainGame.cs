using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Nursia;

namespace OpenGothic;

public class MainGame : Game
{
	private readonly GraphicsDeviceManager _graphics;
	private readonly FramesPerSecondCounter _fpsCounter = new FramesPerSecondCounter();
	private string _path;
	private SpriteBatch _spriteBatch;

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
	}

	protected override void LoadContent()
	{
		base.LoadContent();

		_spriteBatch = new SpriteBatch(GraphicsDevice);

		var assets = new Assets(_path);
		var world = assets.GetWorld(GraphicsDevice, "newworld.zen");

		// UI
		MyraEnvironment.Game = this;
		Nrs.Game = this;

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

		_spriteBatch.Begin();
		_spriteBatch.DrawString(Nrs.DebugFont, $"FPS: {_fpsCounter.FramesPerSecond}", new Vector2(10, 10), Color.White);
		_spriteBatch.End();

		_fpsCounter.OnFrameDrawn();
	}
}