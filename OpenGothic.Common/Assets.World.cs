using Microsoft.Xna.Framework.Graphics;
using Nursia.Materials;
using Nursia.SceneGraph;
using OpenGothic.Materials;
using OpenGothic.Utility;
using System;
using System.Collections.Generic;
using ZenKit;

namespace OpenGothic
{
	partial class Assets
	{
		private MeshNode[] LoadWorld(GraphicsDevice device, string name)
		{
			var record = GetLastRecord(name);

			// Group polygons by materials
			var groupedPolygons = new Dictionary<int, Tuple<DefaultMaterial, List<IPolygon>>>();
			var zkWorld = new World(record.Node.Buffer);

			var polygons = zkWorld.Mesh.Polygons;
			for (var i = 0; i < polygons.Count; ++i)
			{
				var polygon = polygons[i];

				Tuple<DefaultMaterial, List<IPolygon>> p;
				if (!groupedPolygons.TryGetValue(polygon.MaterialIndex, out p))
				{
					var zkMaterial = zkWorld.Mesh.Materials[polygon.MaterialIndex];

					Texture2D texture = null;

					if (!string.IsNullOrEmpty(zkMaterial.Texture))
					{
						texture = GetTexture(device, zkMaterial.Texture);
					}

					var mat = new DefaultMaterial
					{
						DiffuseTexture = texture
					};

					switch(zkMaterial.AlphaFunction)
					{
						case AlphaFunction.None:
							break;
						case AlphaFunction.Blend:
							mat.BlendState = BlendState.AlphaBlend;
							break;

						default:
							break;
					}

					p = new Tuple<DefaultMaterial, List<IPolygon>>(mat, new List<IPolygon>());
					groupedPolygons[polygon.MaterialIndex] = p;
				}

				p.Item2.Add(polygon);
			}

			var features = zkWorld.Mesh.Features;
			var positions = zkWorld.Mesh.Positions;

			var result = new List<MeshNode>();
			foreach (var pair in groupedPolygons)
			{
				var meshBuilder = new MeshBuilder();
				var vertexIndexMap = new Dictionary<int, Dictionary<int, int>>();
				foreach (var polygon in pair.Value.Item2)
				{
					for (var i = 0; i < 3; ++i)
					{
						var fidx = polygon.FeatureIndices[i];

						Dictionary<int, int> posMap;
						if (!vertexIndexMap.TryGetValue(fidx, out posMap))
						{
							posMap = new Dictionary<int, int>();
							vertexIndexMap[fidx] = posMap;
						}

						var pidx = polygon.PositionIndices[i];
						int index;
						if (!posMap.TryGetValue(pidx, out index))
						{
							var feature = features[fidx];
							var position = positions[pidx];

							var vertex = new VertexPositionNormalTexture(position.ToXna(), feature.Normal.ToXna(), feature.Texture.ToXna());
							meshBuilder.AddVertex(vertex);

							index = meshBuilder.Vertices.Count - 1;
							posMap[pidx] = index;
						}

						meshBuilder.AddIndex(index);
					}
				}

				var meshPart = meshBuilder.CreateMeshPart(device, false);

				var meshNode = new MeshNode
				{
					Mesh = meshPart,
					Material = pair.Value.Item1
				};

				result.Add(meshNode);
			}

			return result.ToArray();
		}

		public MeshNode[] GetWorld(GraphicsDevice device, string name) => Get(device, name, LoadWorld);
	}
}
