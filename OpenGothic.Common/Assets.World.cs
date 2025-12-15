using Microsoft.Xna.Framework.Graphics;
using Nursia.Materials;
using Nursia.SceneGraph;
using OpenGothic.Materials;
using OpenGothic.Utility;
using System;
using System.Collections.Generic;
using ZenKit;
using IMaterial = Nursia.Materials.IMaterial;

namespace OpenGothic
{
	partial class Assets
	{
		private MeshNode[] LoadWorld(GraphicsDevice device, string name)
		{
			var record = GetLastRecord(name);

			// Group polygons by materials
			var groupedPolygons = new Dictionary<int, Tuple<IMaterial, List<IPolygon>>>();
			var zkWorld = new World(record.Node.Buffer);

			var polygons = zkWorld.Mesh.Polygons;
			var materials = zkWorld.Mesh.Materials;
			for (var i = 0; i < polygons.Count; ++i)
			{
				var polygon = polygons[i];

				Tuple<IMaterial, List<IPolygon>> p;
				if (!groupedPolygons.TryGetValue(polygon.MaterialIndex, out p))
				{
					var zkMaterial = materials[polygon.MaterialIndex];

					Texture2D texture = null;

					if (!string.IsNullOrEmpty(zkMaterial.Texture))
					{
						texture = GetTexture(device, zkMaterial.Texture);
					} else
					{
						continue;
					}

					var mat = new DefaultMaterial
					{
//						DiffuseColor = zkMaterial.Color.ToXna(),
						DiffuseTexture = texture
					};

					if (zkMaterial.Group == MaterialGroup.Water/* || zkMaterial.Group == MaterialGroup.Earth || zkMaterial.Group == MaterialGroup.Snow*/)
					{
						mat.CastsShadows = false;
					}

					switch (zkMaterial.AlphaFunction)
					{
						case AlphaFunction.None:
							break;
						case AlphaFunction.Blend:
							// mat.AlphaMask = true;
							break;

						default:
							break;
					}

					p = new Tuple<IMaterial, List<IPolygon>>(mat, new List<IPolygon>());
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
					var featureIndices = polygon.FeatureIndices;
					var positionIndices = polygon.PositionIndices;

					if (featureIndices.Count != positionIndices.Count)
					{
						throw new Exception($"featureIndices.Count != positionIndices.Count");
					}

					// Convert polygon to triangles
					for (var t = 2; t < featureIndices.Count; ++t)
					{
						for (var i = 0; i < 3; ++i)
						{
							int idx = 0;
							
							if (i == 1)
							{
								idx = t - 1;
							}
							else if (i == 2)
							{
								idx = t;
							}

							var fidx = featureIndices[idx];

							Dictionary<int, int> posMap;
							if (!vertexIndexMap.TryGetValue(fidx, out posMap))
							{
								posMap = new Dictionary<int, int>();
								vertexIndexMap[fidx] = posMap;
							}

							var pidx = positionIndices[idx];
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
				}

				var meshPart = meshBuilder.CreateMeshPart(device, true);
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
