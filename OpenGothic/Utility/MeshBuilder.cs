using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Vertices;
using System.Collections.Generic;

namespace OpenGothic.Utility;

internal interface IMeshBuilder
{
	int IndicesCount { get; }

	void AddIndex(int index);
	void AddIndicesRange(IEnumerable<int> indices);
	int GetIndex(int index);
	int[] CreateIndicesArray();
	void ClearIndices();
	DrMeshPart CreateMeshPart(GraphicsDevice graphicsDevice, bool toLeftHanded);
}

internal abstract class MeshBuilder<T> : IMeshBuilder where T : IVertexType
{
	private bool _uses32BitIndices = false;

	public List<T> Vertices = new List<T>();
	private List<int> _indices { get; } = new List<int>();

	public int IndicesCount => _indices.Count;

	public void AddVertex(T v) => Vertices.Add(v);

	public void AddIndex(int index)
	{
		if (index >= ushort.MaxValue)
		{
			_uses32BitIndices = true;
		}

		_indices.Add(index);
	}

	public void AddIndicesRange(IEnumerable<int> indices)
	{
		foreach (var idx in indices)
		{
			AddIndex(idx);
		}
	}

	public int GetIndex(int index) => _indices[index];
	public int[] CreateIndicesArray() => _indices.ToArray();

	public void ClearIndices()
	{
		_indices.Clear();
		_uses32BitIndices = false;
	}

	public DrMeshPart CreateMeshPart(GraphicsDevice graphicsDevice, bool toLeftHanded)
	{
		if (toLeftHanded)
		{
			// Mirroring Z while loading reverses triangle winding; swap it back so the
			// front face stays front-facing in right-handed space.
			// Texture coordinates are copied verbatim, matching the reference
			// implementation, which never inverts U or V when building vertices.
			for (var i = 0; i < _indices.Count; i += 3)
			{
				var temp = _indices[i];
				_indices[i] = _indices[i + 2];
				_indices[i + 2] = temp;
			}
		}

		IndexBuffer indexBuffer;
		if (!_uses32BitIndices)
		{
			var indicesShort = new ushort[_indices.Count];
			for (var i = 0; i < indicesShort.Length; ++i)
			{
				indicesShort[i] = (ushort)_indices[i];
			}

			indexBuffer = indicesShort.CreateIndexBuffer(graphicsDevice);
		}
		else
		{
			indexBuffer = _indices.ToArray().CreateIndexBuffer(graphicsDevice);
		}

		var vertexBuffer = CreateVertexBuffer(graphicsDevice, Vertices.ToArray());

		return new DrMeshPart(vertexBuffer, indexBuffer, CreateBoundingBox(Vertices));
	}

	protected abstract VertexBuffer CreateVertexBuffer(GraphicsDevice device, T[] vertices);
	protected abstract BoundingBox CreateBoundingBox(IEnumerable<T> vertices);
}

internal class MeshBuilderPNT : MeshBuilder<VertexPositionNormalTexture>
{
	protected override VertexBuffer CreateVertexBuffer(GraphicsDevice device, VertexPositionNormalTexture[] vertices) => vertices.CreateVertexBuffer(device);

	protected override BoundingBox CreateBoundingBox(IEnumerable<VertexPositionNormalTexture> vertices) => BoundingBox.CreateFromPoints(vertices.GetPositions());
}

internal class MeshBuilderS : MeshBuilder<VertexSkinned>
{
	protected override VertexBuffer CreateVertexBuffer(GraphicsDevice device, VertexSkinned[] vertices) => vertices.CreateVertexBuffer(device);

	protected override BoundingBox CreateBoundingBox(IEnumerable<VertexSkinned> vertices) => BoundingBox.CreateFromPoints(vertices.GetPositions());
}