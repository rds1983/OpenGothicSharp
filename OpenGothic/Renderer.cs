using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Scene;
using System;

namespace OpenGothic;

/// <summary>
/// Low-level 3D drawing service: effects, device state and draw calls.
/// It owns no scene content — callers supply the meshes, transforms and camera.
/// A custom shader system is deferred; see TASKS.md milestone M.
/// </summary>
public class Renderer : IDisposable
{
	private readonly GraphicsDevice _device;
	private readonly BasicEffect _effect;
	private Texture2D _fallbackTexture;

	private BlendState _savedBlendState;
	private DepthStencilState _savedDepthStencilState;
	private RasterizerState _savedRasterizerState;
	private SamplerState _savedSamplerState;
	private Rectangle _savedScissorRectangle;

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
			AmbientLightColor = new Vector3(0.45f),

			// Fog fades distant geometry into the sky's horizon color.
			FogEnabled = true,
			FogColor = Constants.SkyHorizonColor.ToVector3(),
			FogStart = Constants.FogStart,
			FogEnd = Constants.FogEnd
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

	/// <summary>Draw calls issued during the current frame.</summary>
	public int DrawCalls { get; private set; }

	/// <summary>Triangles submitted during the current frame.</summary>
	public int Triangles { get; private set; }

	/// <summary>Clears the backbuffer to the sky's horizon color.</summary>
	public void Clear()
	{
		_device.Clear(Constants.SkyHorizonColor);
	}

	/// <summary>
	/// Begins a frame: saves the device state, installs the renderer's own state and
	/// resets the counters. Must be paired with <see cref="EndFrame"/>.
	/// </summary>
	public void BeginFrame(Camera camera)
	{
		if (camera == null)
		{
			throw new ArgumentNullException(nameof(camera));
		}

		_savedBlendState = _device.BlendState;
		_savedDepthStencilState = _device.DepthStencilState;
		_savedRasterizerState = _device.RasterizerState;
		_savedSamplerState = _device.SamplerStates[0];
		_savedScissorRectangle = _device.ScissorRectangle;

		DrawCalls = 0;
		Triangles = 0;

		_effect.View = camera.View;
		_effect.Projection = camera.Projection;
		_effect.Texture = _fallbackTexture;

		// Device state is global and UI rendering leaves its own behind: Myra enables
		// scissor testing with a small rectangle and binds a clamping sampler.
		// BasicEffect sets neither, so both would corrupt the world (a partially cleared
		// target and terrain textures smearing into streaks).
		_device.BlendState = BlendState.Opaque;
		_device.DepthStencilState = DepthStencilState.Default;
		_device.RasterizerState = RasterizerState.CullCounterClockwise;
		_device.SamplerStates[0] = SamplerState.LinearWrap;
		_device.ScissorRectangle = _device.Viewport.Bounds;
	}

	/// <summary>Restores the device state saved by <see cref="BeginFrame"/>.</summary>
	public void EndFrame()
	{
		_device.BlendState = _savedBlendState;
		_device.DepthStencilState = _savedDepthStencilState;
		_device.RasterizerState = _savedRasterizerState;
		_device.SamplerStates[0] = _savedSamplerState;
		_device.ScissorRectangle = _savedScissorRectangle;
	}

	/// <summary>
	/// Draws a single mesh part. Materials normally come from the part's own
	/// <see cref="DrMeshPartTag"/>; <paramref name="customMaterial"/> is an override for
	/// rare cases such as debug drawing and is expected to be null otherwise.
	/// </summary>
	public void DrawMeshPart(DigitalRiseModel.DrMeshPart part, Matrix transform, Material customMaterial = null)
	{
		if (part == null || part.VertexBuffer == null || part.IndexBuffer == null)
		{
			return;
		}

		var material = customMaterial ?? (part.Tag as DrMeshPartTag)?.material;

		_device.BlendState = BlendState.Opaque;
		_device.DepthStencilState = material?.DepthStencilState ?? DepthStencilState.Default;
		_device.RasterizerState = material?.RasterizerState ?? RasterizerState.CullCounterClockwise;

		_effect.LightingEnabled = material?.LightingEnabled ?? true;
		_effect.FogEnabled = material?.FogEnabled ?? true;
		_effect.World = transform;
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

		++DrawCalls;
		Triangles += PrimitiveCountToTriangles(part.PrimitiveType, part.PrimitiveCount);

		// Restore the frame defaults so the next part is unaffected.
		_effect.LightingEnabled = true;
		_effect.FogEnabled = true;
	}

	private static int PrimitiveCountToTriangles(PrimitiveType primitiveType, int primitiveCount)
	{
		switch (primitiveType)
		{
			case PrimitiveType.TriangleList:
				return primitiveCount;

			case PrimitiveType.TriangleStrip:
				return Math.Max(0, primitiveCount - 2);

			default:
				return 0;
		}
	}

	public void Dispose()
	{
		_effect.Dispose();
		_fallbackTexture?.Dispose();
		_fallbackTexture = null;
	}
}
