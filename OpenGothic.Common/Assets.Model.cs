using DigitalRiseModel;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Utility;
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
					var material = new DrMaterial();
					material.DiffuseColor = submesh.Material.Color.ToXna();
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

		return null;
	}

	public DrModel GetModel(GraphicsDevice device, string name) => Get(device, name, LoadModel);

}
