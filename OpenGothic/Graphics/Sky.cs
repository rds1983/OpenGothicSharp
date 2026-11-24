 using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Utility;
using System;

namespace OpenGothic.Graphics;

/// <summary>
/// Gradient sky dome. Owns its geometry, texture and material, and submits its mesh part
/// to <see cref="Renderer"/>. The reference's LUT/cloud sky belongs to the shader milestone.
/// </summary>
public class Sky : IDisposable
{
	private const int Rings = 8;
	private const int Segments = 16;
	private const float Radius = 15000.0f;
	private const int GradientHeight = 64;

	private readonly GraphicsDevice _device;
	private readonly DrMeshPart _meshPart;
	private readonly Texture2D _gradient;

	public Color HorizonColor => Constants.SkyHorizonColor;

	public Color ZenithColor => Constants.SkyZenithColor;

	public Sky(GraphicsDevice device)
	{
		_device = device ?? throw new ArgumentNullException(nameof(device));

		_gradient = BuildGradient(device);
		_meshPart = BuildMeshPart(device);

		_meshPart.Tag = new DrMeshPartTag(new Material
		{
			DiffuseColor = Color.White,
			DiffuseTexture = _gradient,

			// The sky is unlit, unfogged and drawn behind everything.
			LightingEnabled = false,
			FogEnabled = false,
			DepthStencilState = DepthStencilState.None,
			RasterizerState = RasterizerState.CullNone
		});
	}

	private Texture2D BuildGradient(GraphicsDevice device)
	{
		var colors = new Color[GradientHeight];
		for (var i = 0; i < GradientHeight; ++i)
		{
			// Row 0 is the top of the texture; the dome maps the zenith to V = 0.
			colors[i] = Color.Lerp(HorizonColor, ZenithColor, 1f - i / (float)(GradientHeight - 1));
		}

		var texture = new Texture2D(device, 1, GradientHeight);
		texture.SetData(colors);
		return texture;
	}

	private DrMeshPart BuildMeshPart(GraphicsDevice device)
	{
		var builder = new MeshBuilderPNT();

		for (var ring = 0; ring <= Rings; ++ring)
		{
			// 0 = horizon, PI/2 = zenith.
			var elevation = MathHelper.PiOver2 * ring / Rings;
			var sin = MathF.Sin(elevation);
			var cos = MathF.Cos(elevation);
			var v = 1f - ring / (float)Rings;

			for (var segment = 0; segment <= Segments; ++segment)
			{
				var azimuth = MathHelper.TwoPi * segment / Segments;

				var position = new Vector3(
					MathF.Cos(azimuth) * cos * Radius,
					sin * Radius,
					MathF.Sin(azimuth) * cos * Radius);

				// Normals point outward; lighting is disabled, so they only satisfy the format.
				var normal = Vector3.Normalize(position);

				builder.AddVertex(new VertexPositionNormalTexture(position, normal, new Vector2(0.5f, v)));
			}
		}

		var stride = Segments + 1;
		for (var ring = 0; ring < Rings; ++ring)
		{
			for (var segment = 0; segment < Segments; ++segment)
			{
				var a = ring * stride + segment;
				var b = a + 1;
				var c = a + stride;
				var d = c + 1;

				builder.AddIndex(a);
				builder.AddIndex(c);
				builder.AddIndex(b);

				builder.AddIndex(b);
				builder.AddIndex(c);
				builder.AddIndex(d);
			}
		}

		return builder.CreateMeshPart(device, false);
	}

	public void Draw(Renderer renderer, Camera camera)
	{
		if (renderer == null)
		{
			throw new ArgumentNullException(nameof(renderer));
		}

		if (camera == null)
		{
			throw new ArgumentNullException(nameof(camera));
		}

		// The dome encloses the camera; its box can never fail a frustum test, and culling
		// the background would leave a cleared void. Skip the test explicitly.
		renderer.DrawMeshPart(_meshPart, Matrix.CreateTranslation(camera.Position), cull: false);
	}

	public void Dispose()
	{
		_meshPart?.Dispose();
		_gradient?.Dispose();
	}
}
