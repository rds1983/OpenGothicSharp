using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Scene;
using System;

namespace OpenGothic;

/// <summary>
/// Draws a <see cref="WorldGrid"/> plus the sky. Owns the frustum culling and the
/// scene traversal; all GPU work is delegated to <see cref="Renderer"/>.
/// </summary>
public class WorldRenderer : IDisposable
{
	private readonly BoundingFrustum _frustum = new BoundingFrustum(Matrix.Identity);

	public Sky Sky { get; }

	public WorldRenderer(GraphicsDevice device)
	{
		if (device == null)
		{
			throw new ArgumentNullException(nameof(device));
		}

		Sky = new Sky(device);
	}

	public void Draw(Renderer renderer, Camera camera, WorldGrid world)
	{
		if (renderer == null)
		{
			throw new ArgumentNullException(nameof(renderer));
		}

		if (camera == null)
		{
			throw new ArgumentNullException(nameof(camera));
		}

		// BeginFrame installs the renderer's device state (including the scissor rect), so
		// the clear must happen after it — otherwise it is clipped by whatever the UI left.
		renderer.BeginFrame(camera);
		renderer.Clear();

		Sky.Draw(renderer, camera);

		if (world != null)
		{
			_frustum.Matrix = camera.ViewProjection;

			foreach (var cell in world.Cells)
			{
				if (!_frustum.Intersects(cell.BoundingBox))
				{
					continue;
				}

				foreach (var node in cell.Children)
				{
					DrawNode(renderer, node);
				}
			}
		}

		renderer.EndFrame();
	}

	private static void DrawNode(Renderer renderer, SceneNode node)
	{
		switch (node)
		{
			case InstancedMeshNode instanced:
				foreach (var transform in instanced.InstancesTransforms)
				{
					renderer.DrawMeshPart(instanced.Mesh, transform);
				}

				break;

			case MeshNode mesh:
				renderer.DrawMeshPart(mesh.Mesh, mesh.Transform);
				break;
		}
	}

	public void Dispose()
	{
		Sky.Dispose();
	}
}
