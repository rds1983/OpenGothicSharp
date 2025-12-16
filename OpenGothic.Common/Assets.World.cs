using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
		private class CellData
		{
			public List<int> Polygons { get; } = new List<int>();
			public BoundingBox BoundingBox;
		}

		private WorldGrid LoadWorld(GraphicsDevice device, string name)
		{
			var record = GetLastRecord(name);

			var zkWorld = new World(record.Node.Buffer);

			var polygons = zkWorld.Mesh.Polygons;
			var materials = zkWorld.Mesh.Materials;
			var features = zkWorld.Mesh.Features;
			var positions = zkWorld.Mesh.Positions;

			// Build total bounding box
			var allPositions = new List<Vector3>();
			for (var i = 0; i < polygons.Count; ++i)
			{
				var polygon = polygons[i];

				var positionIndices = polygon.PositionIndices;
				for (var j = 0; j < positionIndices.Count; ++j)
				{
					var pos = positions[positionIndices[j]].ToXna();
					allPositions.Add(pos);
				}
			}

			var boundingBox = BoundingBox.CreateFromPoints(allPositions);

			// Group polygons by cells
			var cellSizeX = (boundingBox.Max.X - boundingBox.Min.X) / Constants.GridSize;
			var cellSizeZ = (boundingBox.Max.Z - boundingBox.Min.Z) / Constants.GridSize;
			var cellsData = new CellData[Constants.GridSize, Constants.GridSize];
			for (var i = 0; i < Constants.GridSize; ++i)
			{
				for (var j = 0; j < Constants.GridSize; ++j)
				{
					cellsData[i, j] = new CellData();

					var min = boundingBox.Min + new Vector3(i * cellSizeX, 0, j * cellSizeZ);
					var max = new Vector3(min.X + cellSizeX, boundingBox.Max.Y, min.Z  + cellSizeZ);

					cellsData[i, j].BoundingBox = new BoundingBox(min, max);
				}
			}

			var ps = new List<Vector3>();
			for (var i = 0; i < polygons.Count; ++i)
			{
				var polygon = polygons[i];

				var positionIndices = polygon.PositionIndices;

				ps.Clear();
				for (var j = 0; j < positionIndices.Count; ++j)
				{
					ps.Add(positions[positionIndices[j]].ToXna());
				}

				var bb = BoundingBox.CreateFromPoints(ps);

				for (var cx = 0; cx < Constants.GridSize; ++cx)
				{
					for (var cz = 0; cz < Constants.GridSize; ++cz)
					{
						if (cellsData[cx, cz].BoundingBox.Intersects(bb))
						{
							cellsData[cx, cz].Polygons.Add(i);
							goto found;
						}
					}
				}

				var notfound = 5;
			found:;
			}

			var result = new WorldGrid();
			for (var i = 0; i < Constants.GridSize; ++i)
			{
				for (var j = 0; j < Constants.GridSize; ++j)
				{
					var cellData = cellsData[i, j];

					// Group polygons by materials
					var groupedPolygons = new Dictionary<int, Tuple<IMaterial, List<IPolygon>>>();
					for (var k = 0; k < cellData.Polygons.Count; ++k)
					{
						var polygon = polygons[cellData.Polygons[k]];

						Tuple<IMaterial, List<IPolygon>> p;
						if (!groupedPolygons.TryGetValue(polygon.MaterialIndex, out p))
						{
							var zkMaterial = materials[polygon.MaterialIndex];

							Texture2D texture = null;

							if (!string.IsNullOrEmpty(zkMaterial.Texture))
							{
								texture = GetTexture(device, zkMaterial.Texture);
							}
							else
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
									// mat.BlendState = BlendState.NonPremultiplied;
									break;

								default:
									break;
							}

							p = new Tuple<IMaterial, List<IPolygon>>(mat, new List<IPolygon>());
							groupedPolygons[polygon.MaterialIndex] = p;
						}

						p.Item2.Add(polygon);
					}

					// Create meshes
					var cell = result.Cells[i, j];
					foreach (var pair in groupedPolygons)
					{
						var meshBuilder = new MeshBuilderPNT();
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
								for (var r = 0; r < 3; ++r)
								{
									int idx = 0;

									if (r == 1)
									{
										idx = t - 1;
									}
									else if (r == 2)
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

						cell.Root.Children.Add(meshNode);
					}

					// Update bounding box
					cell.BoundingBox = cellData.BoundingBox;
				}
			}

			return result;
		}

		public WorldGrid GetWorld(GraphicsDevice device, string name) => Get(device, name, LoadWorld);
	}
}
