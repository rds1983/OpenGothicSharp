using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Engine;
using System;

namespace OpenGothic.Rendering;

/// <summary>
/// Draws opaque 3D geometry with stock MonoGame effects and owns all low-level graphics state.
/// Hosts (the world viewer, later the game) hand it meshes and transforms; they never touch
/// effects, vertex buffers or render states themselves.
/// </summary>
/// <remarks>
/// Effect selection is per mesh part: parts without a <see cref="DrSkin"/> use
/// <see cref="BasicEffect"/>, skinned parts use <see cref="SkinnedEffect"/>. An alpha-tested
/// effect will be added here once materials can request it.
/// </remarks>
public sealed class ForwardRenderer : IDisposable
{
	private static readonly Vector3 LightDirection = Vector3.Normalize(new Vector3(-0.4f, -1.0f, -0.3f));
	private static readonly Vector3 AmbientLight = new Vector3(0.45f, 0.45f, 0.45f);

	private readonly GraphicsDevice _device;
	private readonly BasicEffect _staticEffect;
	private readonly SkinnedEffect _skinnedEffect;

	public ForwardRenderer(GraphicsDevice device, Color fogColor)
	{
		_device = device ?? throw new ArgumentNullException(nameof(device));

		_staticEffect = new BasicEffect(device)
		{
			TextureEnabled = true,
			LightingEnabled = true,
			PreferPerPixelLighting = false,
			DiffuseColor = Vector3.One,
			SpecularColor = Vector3.Zero,
		};

		_staticEffect.EnableDefaultLighting();
		_staticEffect.AmbientLightColor = AmbientLight;
		_staticEffect.DirectionalLight0.Direction = LightDirection;

		// SkinnedEffect is always textured, so it has no TextureEnabled flag.
		_skinnedEffect = new SkinnedEffect(device)
		{
			PreferPerPixelLighting = false,
			DiffuseColor = Vector3.One,
			SpecularColor = Vector3.Zero,
		};

		_skinnedEffect.EnableDefaultLighting();
		_skinnedEffect.AmbientLightColor = AmbientLight;
		_skinnedEffect.DirectionalLight0.Direction = LightDirection;

		FogEnabled = true;
		FogColor = fogColor;
	}

	/// <summary>Camera view matrix, applied to every draw this frame.</summary>
	public Matrix View { get; set; } = Matrix.Identity;

	/// <summary>Camera projection matrix, applied to every draw this frame.</summary>
	public Matrix Projection { get; set; } = Matrix.Identity;

	/// <summary>Distance at which fog starts to blend in.</summary>
	public float FogStart { get; set; } = Constants.FogStart;

	/// <summary>Distance at which fog fully replaces the material colour.</summary>
	public float FogEnd { get; set; } = Constants.FogEnd;

	public bool FogEnabled { get; set; }

	public Color FogColor { get; set; }

	/// <summary>Number of draw calls issued since the last <see cref="BeginFrame"/>.</summary>
	public int DrawCalls { get; private set; }

	/// <summary>Number of triangles drawn since the last <see cref="BeginFrame"/>.</summary>
	public int DrawnTriangles { get; private set; }

	/// <summary>
	/// Starts a frame: resets the statistics and applies the render states shared by all draws.
	/// Clearing the back buffer is left to the host because it owns the sky colour.
	/// </summary>
	public void BeginFrame()
	{
		DrawCalls = 0;
		DrawnTriangles = 0;

		_device.DepthStencilState = DepthStencilState.Default;
		_device.BlendState = BlendState.Opaque;
		_device.RasterizerState = RasterizerState.CullCounterClockwise;
		_device.SamplerStates[0] = SamplerState.LinearWrap;

		_staticEffect.View = View;
		_staticEffect.Projection = Projection;
		_staticEffect.FogEnabled = FogEnabled;
		_staticEffect.FogColor = FogColor.ToVector3();
		_staticEffect.FogStart = FogStart;
		_staticEffect.FogEnd = FogEnd;

		_skinnedEffect.View = View;
		_skinnedEffect.Projection = Projection;
		_skinnedEffect.FogEnabled = FogEnabled;
		_skinnedEffect.FogColor = FogColor.ToVector3();
		_skinnedEffect.FogStart = FogStart;
		_skinnedEffect.FogEnd = FogEnd;
	}

	/// <summary>
	/// Draws a single mesh part. The required effect is chosen from the part's skinning data.
	/// </summary>
	/// <param name="part">Mesh part to draw.</param>
	/// <param name="material">
	/// Material override. When <c>null</c> the part's own material (if any) is used.
	/// </param>
	/// <param name="world">Local-to-world transform.</param>
	/// <param name="boneTransforms">
	/// Bone palette for a skinned part. Ignored for static parts; required (non-null) for skinned
	/// ones.
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="part"/> is <c>null</c>, or <paramref name="boneTransforms"/> is <c>null</c>
	/// while the part is skinned.
	/// </exception>
	public void DrawMeshPart(DrMeshPart part, Material material, Matrix world, Matrix[] boneTransforms = null)
	{
		if (part == null)
		{
			throw new ArgumentNullException(nameof(part));
		}

		var texture = material != null ? material.DiffuseTexture : part.Material?.DiffuseTexture;
		var diffuseColor = material?.DiffuseColor.ToVector3()
			?? part.Material?.DiffuseColor.ToVector3()
			?? Vector3.One;

		if (part.Skin == null)
		{
			_staticEffect.World = world;
			_staticEffect.Texture = texture;
			_staticEffect.TextureEnabled = texture != null;
			_staticEffect.DiffuseColor = diffuseColor;
			_staticEffect.CurrentTechnique.Passes[0].Apply();
		}
		else
		{
			if (boneTransforms == null)
			{
				throw new ArgumentNullException(nameof(boneTransforms), "A skinned mesh part requires a bone transform palette.");
			}

			_skinnedEffect.World = world;
			_skinnedEffect.Texture = texture;
			_skinnedEffect.DiffuseColor = diffuseColor;
			_skinnedEffect.SetBoneTransforms(boneTransforms);
			_skinnedEffect.CurrentTechnique.Passes[0].Apply();
		}

		_device.SetVertexBuffer(part.VertexBuffer);
		_device.Indices = part.IndexBuffer;
		_device.DrawIndexedPrimitives(part.PrimitiveType, 0, 0, part.PrimitiveCount);

		++DrawCalls;
		DrawnTriangles += TriangleCount(part.PrimitiveType, part.PrimitiveCount);
	}

	private static int TriangleCount(PrimitiveType primitiveType, int primitiveCount)
	{
		return primitiveType switch
		{
			PrimitiveType.TriangleList => primitiveCount,
			PrimitiveType.TriangleStrip => Math.Max(0, primitiveCount - 2),
			_ => 0,
		};
	}

	public void Dispose()
	{
		_staticEffect?.Dispose();
		_skinnedEffect?.Dispose();
	}
}
