using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Engine;
using System;
using System.Diagnostics;

namespace OpenGothic.Tools;

/// <summary>
/// Minimal MonoGame host: the mesh loader needs a live GraphicsDevice, so the console
/// tool runs a game loop just long enough to load the requested world, prints a summary
/// and exits. Nothing is rendered.
/// </summary>
internal sealed class WorldLoader : Game
{
	private readonly GraphicsDeviceManager _graphics;
	private readonly string _folder;
	private readonly string _worldName;

	private bool _finished;
	private bool _succeeded;

	public bool Succeeded => _succeeded;

	public WorldLoader(string folder, string worldName)
	{
		_folder = folder;
		_worldName = worldName;

		_graphics = new GraphicsDeviceManager(this)
		{
			PreferredBackBufferWidth = 640,
			PreferredBackBufferHeight = 360,
			SynchronizeWithVerticalRetrace = false,
		};

		IsFixedTimeStep = false;
		IsMouseVisible = false;
		Window.Title = "OpenGothic world loader";
		Window.AllowUserResizing = false;
	}

	protected override void LoadContent()
	{
		base.LoadContent();

		try
		{
			OG.GraphicsDevice = GraphicsDevice;

			Console.WriteLine($"OpenGothicSharp {OG.Version}");
			Console.WriteLine($"Game folder: {_folder}");
			Console.WriteLine($"World:       {_worldName}");
			Console.WriteLine();

			var assets = new Assets(_folder);
			Console.WriteLine();
			Console.WriteLine($"Indexed {assets.Keys.Length} files.");

			var stopwatch = Stopwatch.StartNew();
			var world = assets.GetWorld(GraphicsDevice, _worldName);
			stopwatch.Stop();

			PrintWorld(world);
			Console.WriteLine($"World loaded in {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
			_succeeded = true;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Failed to load '{_worldName}':");
			Console.Error.WriteLine(ex);
			_succeeded = false;
		}
		finally
		{
			_finished = true;
		}
	}

	protected override void Update(GameTime gameTime)
	{
		if (_finished)
		{
			Exit();
			return;
		}

		base.Update(gameTime);
	}

	protected override void Draw(GameTime gameTime)
	{
		// Intentionally empty: the tool only parses data.
	}

	private static void PrintWorld(WorldGrid world)
	{
		var usedCells = 0;
		var meshNodes = 0;
		var instancedNodes = 0;
		var instances = 0;
		BoundingBox? bounds = null;

		foreach (var cell in world.Cells)
		{
			if (cell.Children.Count == 0)
			{
				continue;
			}

			++usedCells;
			bounds = bounds.HasValue ? BoundingBox.CreateMerged(bounds.Value, cell.BoundingBox) : cell.BoundingBox;

			foreach (var node in cell.Children)
			{
				switch (node)
				{
					case InstancedMeshNode instanced:
						++instancedNodes;
						instances += instanced.InstancesTransforms.Count;
						break;

					case MeshNode:
						++meshNodes;
						break;
				}
			}
		}

		Console.WriteLine($"Grid:            {Constants.GridSize}x{Constants.GridSize} ({usedCells} non-empty cells)");
		Console.WriteLine($"Mesh nodes:      {meshNodes}");
		Console.WriteLine($"Instanced nodes: {instancedNodes} ({instances} instances)");
		Console.WriteLine($"Scene nodes:     {meshNodes + instances}");

		if (bounds.HasValue)
		{
			var b = bounds.Value;
			Console.WriteLine($"Bounds:          min {Format(b.Min)}, max {Format(b.Max)}");
		}
	}

	private static string Format(Vector3 v) => $"({v.X:0.##}, {v.Y:0.##}, {v.Z:0.##})";
}
