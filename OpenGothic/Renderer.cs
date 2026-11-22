using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Scene;
using System;

namespace OpenGothic;

/// <summary>
/// Draws the static world with stock MonoGame effects.
/// A custom shader system is deferred; see TASKS.md milestone M.
/// </summary>
public class Renderer : IDisposable
{
	private readonly GraphicsDevice _device;
	private readonly BasicEffect _effect;
	private readonly BoundingFrustum _frustum = new BoundingFrustum(Matrix.Identity);
	private Texture2D _fallbackTexture;

	public Renderer(GraphicsDevice device)
	{
		_device = device ?? throw new ArgumentNullException(nameof(device));

		_effect = new BasicEffect(device)
		{
			LightingEnabled = true,
			TextureEnabled = true,
			VertexColorEnabled = false,
			PreferPerPixelLighting = true,

			// Gothic's textures already carry their own shading. Default lighting adds a
			// strong specular term that smears highlights across flat surfaces.
			SpecularColor = Vector3.Zero,
			SpecularPower = 1f,
			AmbientLightColor = new Vector3(0.45f)
		};

		// Lights travel downward and away from the camera, so surfaces facing the viewer
		// still catch some of the key light.
		_effect.DirectionalLight0.Enabled = true;
		_effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(0.35f, -1f, -0.55f));
		_effect.DirectionalLight0.DiffuseColor = new Vector3(0.65f);

		_effect.DirectionalLight1.Enabled = true;
		_effect.DirectionalLight1.Direction = Vector3.Normalize(new Vector3(-0.6f, -0.4f, -0.45f));
		_effect.DirectionalLight1.DiffuseColor = new Vector3(0.35f);

		_effect.DirectionalLight2.Enabled = false;

		_fallbackTexture = new Texture2D(device, 1, 1);
		_fallbackTexture.SetData(new[] { Color.White });
	}

	public int DrawnParts { get; private set; }

	public void Draw(Camera camera, WorldGrid world)
	{
		if (camera == null)
		{
			throw new ArgumentNullException(nameof(camera));
		}

		if (world == null)
		{
			return;
		}

		DrawnParts = 0;

		_frustum.Matrix = camera.ViewProjection;

		_device.BlendState = BlendState.Opaque;
		_device.DepthStencilState = DepthStencilState.Default;
		_device.RasterizerState = RasterizerState.CullCounterClockwise;

		_effect.View = camera.View;
		_effect.Projection = camera.Projection;
		_effect.Texture = _fallbackTexture;

		foreach (var pass in _effect.CurrentTechnique.Passes)
		{
			pass.Apply();

			foreach (var cell in world.Cells)
			{
				if (!_frustum.Intersects(cell.BoundingBox))
				{
					continue;
				}

				foreach (var node in cell.Children)
				{
					DrawNode(node);
				}
			}
		}
	}

	private void DrawNode(SceneNode node)
	{
		switch (node)
		{
			case InstancedMeshNode instanced:
				foreach (var transform in instanced.InstancesTransforms)
				{
					DrawMeshPart(instanced.Mesh, ResolveMaterial(instanced.Mesh, null), transform);
				}

				break;

			case MeshNode mesh:
				DrawMeshPart(mesh.Mesh, ResolveMaterial(mesh.Mesh, mesh.Material), mesh.Transform);
				break;
		}
	}

	/// <summary>
	/// Mesh parts carry their material in <see cref="DrMeshPartTag"/>; explicit node
	/// materials take precedence when present.
	/// </summary>
	private static Material ResolveMaterial(DigitalRiseModel.DrMeshPart part, Material explicitMaterial)
	{
		if (explicitMaterial != null)
		{
			return explicitMaterial;
		}

		return (part?.Tag as DrMeshPartTag)?.material;
	}

	private void DrawMeshPart(DigitalRiseModel.DrMeshPart part, Material material, Matrix world)
	{
		if (part == null || part.VertexBuffer == null || part.IndexBuffer == null)
		{
			return;
		}

		_effect.World = world;
		_effect.Texture = material?.DiffuseTexture ?? _fallbackTexture;
		_effect.DiffuseColor = material != null ? material.DiffuseColor.ToVector3() : Vector3.One;
		_effect.CurrentTechnique.Passes[0].Apply();

		_device.SetVertexBuffer(part.VertexBuffer);
		_device.Indices = part.IndexBuffer;

		_device.DrawIndexedPrimitives(
			part.PrimitiveType,
			part.VertexOffset,
			part.StartIndex,
			part.PrimitiveCount);

		++DrawnParts;
	}

	public void Dispose()
	{
		_effect.Dispose();
		_fallbackTexture?.Dispose();
		_fallbackTexture = null;
	}
}
