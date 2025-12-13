using DigitalRiseModel;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Utility;
using System;
using System.Collections.Generic;
using ZenKit;

namespace OpenGothic;

partial class Assets
{
	public DrMeshPart CreatePart(GraphicsDevice device, IMultiResolutionMesh zkMesh, IMultiResolutionSubMesh zkSubMesh)
	{
		var builder = new MeshBuilder();

		for (var i = 0; i < zkSubMesh.Wedges.Count; ++i)
		{
			var wedge = zkSubMesh.Wedges[i];
			var pos = zkMesh.Positions[wedge.Index];

			var vertex = new VertexPositionNormalTexture(pos.ToXna(), wedge.Normal.ToXna(), wedge.Texture.ToXna());

			builder.Vertices.Add(vertex);
		}

		for (var i = 0; i < zkSubMesh.Triangles.Count; ++i)
		{
			var triangle = zkSubMesh.Triangles[i];

			builder.AddIndex(triangle.Wedge0);
			builder.AddIndex(triangle.Wedge1);
			builder.AddIndex(triangle.Wedge2);
		}

		return builder.CreateMeshPart(device, false);
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
			var attachment = pair.Value;

			var mesh = new DrMesh();
			foreach (var submesh in attachment.SubMeshes)
			{
				var meshPart = CreatePart(device, attachment, submesh);

				if (submesh.Material != null)
				{
					var material = new DrMaterial
					{
						DiffuseColor = submesh.Material.Color.ToXna()
					};
					if (!string.IsNullOrEmpty(submesh.Material.Texture))
					{
						material.DiffuseTexture = GetTexture(device, submesh.Material.Texture);
					}

					meshPart.Material = material;
				}

				mesh.MeshParts.Add(meshPart);
			}

			meshes[pair.Key] = mesh;
		}

		// Load nodes
		var nodesData = new List<Tuple<DrModelBone, int?>>();
		for(var i = 0; i < zkModel.Hierarchy.Nodes.Count; ++i)
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

			nodesData.Add(new Tuple<DrModelBone, int?>(bone, node.ParentIndex == -1 ? null : node.ParentIndex));
		}

		// Set children
		for(var i = 0; i < nodesData.Count; ++i)
		{
			var children = new List<DrModelBone>();
			for(var j = 0; j < nodesData.Count; ++j)
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

		return new DrModel(nodesData[0].Item1);
	}

	public DrModel GetModel(GraphicsDevice device, string name) => Get(device, name, LoadModel);

}
