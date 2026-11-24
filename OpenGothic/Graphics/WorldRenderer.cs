using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace OpenGothic.Graphics;

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

	/// <summary>
	/// Draws the world and, when <paramref name="extra"/> is given, one visual that does not
	/// belong to a grid cell. The player avatar is always next to the camera, so culling it
	/// with a cell would only risk not drawing it at a cell border.
	/// </summary>
	public void Draw(Renderer renderer, Camera camera, WorldGrid world, DebugDraw debug = null,
		SceneNode extra = null)
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
					DrawNode(renderer, node, debug);
				}

				// Drawn after the cell's contents so the lines blend over its geometry.
				if (debug?.Bounds == true)
				{
					renderer.DrawBoundingBox(cell.BoundingBox, debug.CellBoundsColor);

					// One box per vob. Drawing one per mesh part would flood the screen:
					// a single vob is a handful of parts, and a level has thousands.
					foreach (var objectBounds in cell.ObjectBounds)
					{
						if (!_frustum.Intersects(objectBounds))
						{
							continue;
						}

						renderer.DrawBoundingBox(objectBounds, debug.ObjectBoundsColor);
					}
				}
			}
		}

		if (extra != null)
		{
			DrawNode(renderer, extra, debug);
		}

		renderer.EndFrame();
	}

	private static void DrawNode(Renderer renderer, SceneNode node, DebugDraw debug)
	{
		switch (node)
		{
			case InstancedMeshNode instanced:
				foreach (var transform in instanced.InstancesTransforms)
				{
					DrawMeshPart(renderer, instanced.Mesh, transform, debug);
				}

				break;

			case MeshNode mesh:
				DrawMeshPart(renderer, mesh.Mesh, mesh.WorldTransform, debug);
				break;

			case ModelNode model:
				DrawModel(renderer, model, debug);
				break;
		}
	}

	/// <summary>
	/// Draws a placed model and everything attached to its bones. A static part is positioned by
	/// its own bone, exactly as a hand-authored prop model expects; a skinned part is deformed by
	/// the skeleton and placed by the node transform alone, because its vertices are already in
	/// model space. <see cref="Renderer.DrawMeshPart"/> picks the path from whether the part
	/// carries a skin, so one routine covers both.
	/// </summary>
	private static void DrawModel(Renderer renderer, ModelNode node, DebugDraw debug)
	{
		var instance = node.Instance;
		var world = node.WorldTransform;

		DrawInstance(renderer, instance, world, debug);

		// An attachment is placed by the bone it follows, so it needs no scene node of its own.
		foreach (var attachment in node.BonesAttachments)
		{
			var bone = attachment.Bone;
			if (bone == null)
			{
				continue;
			}

			var attachmentWorld = attachment.Transform * instance.GetBoneGlobalTransform(bone.Index) * world;
			DrawInstance(renderer, attachment.Instance, attachmentWorld, debug);
		}
	}

	private static void DrawInstance(Renderer renderer, DrModelInstance instance, Matrix world, DebugDraw debug)
	{
		var model = instance.Model;

		foreach (var mesh in model.Meshes)
		{
			foreach (var part in mesh.MeshParts)
			{
				Matrix transform;
				Matrix[] boneTransforms = null;

				if (part.Skin != null)
				{
					transform = world;
					boneTransforms = instance.GetSkinTransforms(part.Skin.SkinIndex);
				}
				else
				{
					transform = instance.GetBoneGlobalTransform(mesh.ParentBone.Index) * world;
				}

				DrawMeshPart(renderer, part, transform, boneTransforms, part.GetCullBounds(), debug);
			}
		}
	}

	private static void DrawMeshPart(Renderer renderer, DrMeshPart part, Matrix transform, Matrix[] boneTransforms,
		BoundingBox bounds, DebugDraw debug)
	{
		if (debug?.Wireframe == true)
		{
			renderer.DrawMeshPartWireframe(part, transform, debug.WireframeColor, boneTransforms, bounds);
		}
		else
		{
			renderer.DrawMeshPart(part, transform, boneTransforms, bounds: bounds);
		}
	}

	private static void DrawMeshPart(Renderer renderer, DrMeshPart part, Matrix transform, DebugDraw debug)
	{
		DrawMeshPart(renderer, part, transform, null, part.BoundingBox, debug);
	}

	public void Dispose()
	{
		Sky.Dispose();
	}
}
