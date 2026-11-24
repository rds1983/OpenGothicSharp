using Microsoft.Xna.Framework;
using System;

namespace OpenGothic.Graphics;

/// <summary>
/// Static triangles of a mesh part in the part's own space, three consecutive corners per
/// triangle. A model part is placed by its bone, a mesh node by the node transform; the
/// physics world applies the same transform the renderer does.
/// </summary>
public sealed class CollisionMesh
{
	/// <summary>Triangle corners, three entries per triangle.</summary>
	public Vector3[] Vertices { get; }

	public CollisionMesh(Vector3[] vertices)
	{
		Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
	}

	public int TriangleCount => Vertices.Length / 3;
}
