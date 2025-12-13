using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using OpenGothic.Utility;
using OpenGothic.Vertices;
using System;
using System.Collections.Generic;
using ZenKit;

namespace OpenGothic;

partial class Assets
{
	private DrMeshPart CreatePart(GraphicsDevice device, ISoftSkinMesh zkSkinnedMesh, IMultiResolutionMesh zkMesh, IMultiResolutionSubMesh zkSubMesh)
	{
		VertexBuffer vertexBuffer;
		BoundingBox boundingBox;

		if (zkSkinnedMesh == null)
		{
			var vertices = new List<VertexPositionNormalTexture>();
			for (var i = 0; i < zkSubMesh.Wedges.Count; ++i)
			{
				var wedge = zkSubMesh.Wedges[i];
				var pos = zkMesh.Positions[wedge.Index];

				var vertex = new VertexPositionNormalTexture(pos.ToXna(), wedge.Normal.ToXna(), wedge.Texture.ToXna());

				vertices.Add(vertex);
			}

			vertexBuffer = vertices.CreateVertexBuffer(device);
			boundingBox = vertices.BuildBoundingBox();
		}
		else
		{
			var vertices = new List<VertexSkinned>();

			for (var i = 0; i < zkSubMesh.Wedges.Count; ++i)
			{
				var wedge = zkSubMesh.Wedges[i];
				var pos = zkMesh.Positions[wedge.Index];

				var vertex = new VertexSkinned(pos.ToXna(), wedge.Normal.ToXna(), wedge.Texture.ToXna(), new Byte4(), Vector4.Zero);

				vertices.Add(vertex);
			}

			vertexBuffer = vertices.CreateVertexBuffer(device);
			boundingBox = vertices.BuildBoundingBox();
		}

		var indices = new List<ushort>();
		for (var i = 0; i < zkSubMesh.Triangles.Count; ++i)
		{
			var triangle = zkSubMesh.Triangles[i];

			indices.Add(triangle.Wedge0);
			indices.Add(triangle.Wedge1);
			indices.Add(triangle.Wedge2);
		}

		var indexBuffer = indices.CreateIndexBuffer(device);

		DrMaterial material = null;
		if (zkSubMesh.Material != null)
		{
			material = new DrMaterial
			{
				DiffuseColor = Color.White
			};

			if (!string.IsNullOrEmpty(zkSubMesh.Material.Texture))
			{
				material.DiffuseTexture = GetTexture(device, zkSubMesh.Material.Texture);
			}
		}

		return new DrMeshPart(vertexBuffer, indexBuffer, boundingBox)
		{
			Material = material
		};
	}

	private DrMesh CreateMesh(GraphicsDevice device, ISoftSkinMesh zkSkinnedMesh, IMultiResolutionMesh zkMesh)
	{
		var mesh = new DrMesh();
		foreach (var submesh in zkMesh.SubMeshes)
		{
			var meshPart = CreatePart(device, zkSkinnedMesh, zkMesh, submesh);
			mesh.MeshParts.Add(meshPart);
		}

		return mesh;
	}

	private DrModel LoadModel(GraphicsDevice device, string name)
	{
		var records = _allRecords[name];
		var record = records[records.Count - 1];

		var zkModel = new ZenKit.Model(record.Node.Buffer);

		// Load meshes
		var meshes = new Dictionary<string, DrMesh>();
		foreach (var pair in zkModel.Mesh.Attachments)
		{
			var mesh = CreateMesh(device, null, pair.Value);

			meshes[pair.Key] = mesh;
		}

		// Load nodes
		var nodesData = new List<Tuple<DrModelBone, int?>>();
		for (var i = 0; i < zkModel.Hierarchy.Nodes.Count; ++i)
		{
			var node = zkModel.Hierarchy.Nodes[i];

			DrMesh mesh = null;
			DrModelBone bone;
			if (meshes.TryGetValue(node.Name, out mesh))
			{
				bone = new DrModelBone(node.Name, mesh);
			}
			else
			{
				bone = new DrModelBone(node.Name);
			}

			bone.DefaultPose = new SrtTransform(node.Transform.ToXna());

			nodesData.Add(new Tuple<DrModelBone, int?>(bone, node.ParentIndex == -1 ? null : node.ParentIndex));
		}

		// Set children
		for (var i = 0; i < nodesData.Count; ++i)
		{
			var children = new List<DrModelBone>();
			for (var j = 0; j < nodesData.Count; ++j)
			{
				if (i == j)
				{
					continue;
				}

				if (nodesData[j].Item2 == i)
				{
					children.Add(nodesData[j].Item1);
				}
			}

			if (children.Count > 0)
			{
				nodesData[i].Item1.Children = children.ToArray();
			}
		}

		var root = nodesData[0].Item1;

		if (zkModel.Mesh.Meshes.Count > 0)
		{
			// Create new root
			root = new DrModelBone("_ROOT");

			var children = new List<DrModelBone>();

			// Load animated meshes
			for (var i = 0; i < zkModel.Mesh.Meshes.Count; ++i)
			{
				var zkSkinnedMesh = zkModel.Mesh.Meshes[i];
				var mesh = CreateMesh(device, zkSkinnedMesh, zkSkinnedMesh.Mesh);

				var meshNode = new DrModelBone($"_MESH{i}", mesh);
				children.Add(meshNode);
			}

			children.Add(nodesData[0].Item1);

			root.Children = children.ToArray();
		}

		return new DrModel(root);
	}

	public DrModel GetModel(GraphicsDevice device, string name) => Get(device, name, LoadModel);

}
