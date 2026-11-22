using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Scene;
using OpenGothic.Utility;
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
	private readonly BoundingFrustum _frustum = new BoundingFrustum(Matrix.Identity);

	// Reused across draws: these are per-frame hot paths, so allocating a Material per call
	// would put thousands of short-lived objects on the GC every frame.
	private readonly Material _boxMaterial = new Material();
	private readonly Material _wireframeMaterial = new Material();

	private bool _frameBegin;
	private Texture2D _fallbackTexture;
	private DrMeshPart _boxLines;
	private RasterizerState _wireframeRasterizerState;

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

		_boxLines = BuildBoxLines(device);

		// Wireframe needs triangles drawn as lines, which no stock rasterizer state provides.
		_wireframeRasterizerState = new RasterizerState
		{
			FillMode = FillMode.WireFrame,
			CullMode = CullMode.None
		};
	}

	/// <summary>Draw calls issued during the current frame.</summary>
	public int DrawCalls { get; private set; }

	/// <summary>Triangles submitted during the current frame.</summary>
	public int Triangles { get; private set; }

	/// <summary>
	/// Clears the backbuffer to the sky's horizon color. Requires an open frame, because
	/// <see cref="BeginFrame"/> is what installs the full-viewport scissor rect the clear is
	/// clipped by.
	/// </summary>
	public void Clear()
	{
		EnsureFrameOpen(nameof(Clear));

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

		if (_frameBegin)
		{
			throw new InvalidOperationException(
				$"{nameof(BeginFrame)} was called while a frame is already open. Call {nameof(EndFrame)} first.");
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

		// Reuse the same instance; BeginFrame runs every frame, so allocating here would be
		// pure garbage.
		_frustum.Matrix = camera.ViewProjection;
		_frameBegin = true;

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
		EnsureFrameOpen(nameof(EndFrame));

		_frameBegin = false;

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
	/// <para>
	/// Must be called between <see cref="BeginFrame"/> and <see cref="EndFrame"/>: the
	/// effect's view/projection and the culling frustum both come from the frame's camera.
	/// The part is frustum-culled using its bounding box transformed by
	/// <paramref name="transform"/>; pass <paramref name="cull"/> = false for geometry that
	/// must draw regardless, such as the sky dome or anything whose box does not describe
	/// what is actually drawn.
	/// </para>
	/// </summary>
	public void DrawMeshPart(DrMeshPart part, Matrix transform, Material customMaterial = null, bool cull = true)
	{
		EnsureFrameOpen(nameof(DrawMeshPart));

		if (part == null || part.VertexBuffer == null || part.IndexBuffer == null)
		{
			return;
		}

		if (cull && DebugSettings.CullEnabled)
		{
			var worldBounds = part.BoundingBox.Transform(ref transform);
			if (!_frustum.Intersects(worldBounds))
			{
				return;
			}
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

	/// <summary>
	/// Draws the twelve edges of <paramref name="box"/> as unlit, unfogged lines. Used by the
	/// debug bounds view; the caller owns the transform so it can draw arbitrary boxes.
	/// </summary>
	public void DrawBoundingBox(BoundingBox box, Color color)
	{
		_boxMaterial.DiffuseColor = color;
		_boxMaterial.DiffuseTexture = _fallbackTexture;
		_boxMaterial.LightingEnabled = false;
		_boxMaterial.FogEnabled = false;

		// Depth test stays on so boxes are occluded by geometry, but they must not write
		// depth themselves or they mask each other as they overlap.
		_boxMaterial.DepthStencilState = DepthStencilState.DepthRead;

		// The transform rescales a shared unit cube, so the part's own box says nothing
		// about where the result lands; the caller has already decided this box is wanted.
		DrawMeshPart(_boxLines, BuildBoxTransform(box), _boxMaterial, false);
	}

	/// <summary>
	/// Draws <paramref name="part"/> with fill mode forced to wireframe. The material's fog
	/// still applies so depth reads correctly, but lighting and texture are replaced by
	/// <paramref name="color"/> — sampling the level's textures turns the edges into noise.
	/// Culling is left to the caller's own visibility decision, so the wireframe always
	/// matches the geometry that would have been drawn.
	/// </summary>
	public void DrawMeshPartWireframe(DrMeshPart part, Matrix transform, Color color)
	{
		EnsureFrameOpen(nameof(DrawMeshPartWireframe));

		if (part == null || part.VertexBuffer == null || part.IndexBuffer == null)
		{
			return;
		}

		var source = (part.Tag as DrMeshPartTag)?.material;

		_wireframeMaterial.DiffuseColor = color;
		_wireframeMaterial.DiffuseTexture = _fallbackTexture;
		_wireframeMaterial.LightingEnabled = false;
		_wireframeMaterial.FogEnabled = source?.FogEnabled ?? true;
		_wireframeMaterial.DepthStencilState = source?.DepthStencilState ?? DepthStencilState.Default;
		_wireframeMaterial.RasterizerState = _wireframeRasterizerState;

		DrawMeshPart(part, transform, _wireframeMaterial);
	}

	/// <summary>
	/// Builds the transform that maps the shared unit-cube edge mesh onto
	/// <paramref name="box"/>. The cube spans -0.5..0.5 around the origin, so it translates
	/// by the box centre. Scaling by zero is legal, so a degenerate box still draws.
	/// </summary>
	private static Matrix BuildBoxTransform(BoundingBox box)
	{
		var size = box.Max - box.Min;
		var center = (box.Min + box.Max) * 0.5f;

		return Matrix.CreateScale(size) * Matrix.CreateTranslation(center);
	}

	/// <summary>
	/// Builds a unit cube centred on the origin with its edges as a line list; each corner
	/// joins exactly three edges so no vertex is shared.
	/// </summary>
	private static DrMeshPart BuildBoxLines(GraphicsDevice device)
	{
		var min = -Vector3.One * 0.5f;
		var max = Vector3.One * 0.5f;

		var corners = new[]
		{
			new Vector3(min.X, min.Y, min.Z),
			new Vector3(max.X, min.Y, min.Z),
			new Vector3(max.X, max.Y, min.Z),
			new Vector3(min.X, max.Y, min.Z),
			new Vector3(min.X, min.Y, max.Z),
			new Vector3(max.X, min.Y, max.Z),
			new Vector3(max.X, max.Y, max.Z),
			new Vector3(min.X, max.Y, max.Z)
		};

		// Corner pairs, in the order a cube's twelve edges are usually enumerated.
		var edges = new[]
		{
			0, 1, 1, 2, 2, 3, 3, 0,
			4, 5, 5, 6, 6, 7, 7, 4,
			0, 4, 1, 5, 2, 6, 3, 7
		};

		var builder = new MeshBuilderPNT();
		foreach (var corner in corners)
		{
			builder.AddVertex(new VertexPositionNormalTexture(corner, Vector3.UnitY, Vector2.Zero));
		}

		foreach (var index in edges)
		{
			builder.AddIndex(index);
		}

		// Not converted for handedness: the mesh is already authored in engine space.
		return builder.CreateMeshPart(device, false, PrimitiveType.LineList);
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

	/// <summary>
	/// Throws unless a frame is open. Draw calls outside a frame would use a stale camera
	/// and a stale frustum, so they are a caller bug rather than something to tolerate.
	/// </summary>
	private void EnsureFrameOpen(string method)
	{
		if (!_frameBegin)
		{
			throw new InvalidOperationException(
				$"{method} requires an open frame. Call {nameof(BeginFrame)} first and {nameof(EndFrame)} after.");
		}
	}

	public void Dispose()
	{
		_effect.Dispose();
		_boxLines?.Dispose();
		_boxLines = null;
		_wireframeRasterizerState?.Dispose();
		_wireframeRasterizerState = null;
		_fallbackTexture?.Dispose();
		_fallbackTexture = null;
	}
}
