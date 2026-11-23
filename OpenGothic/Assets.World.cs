using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Physics;
using OpenGothic.Scene;
using OpenGothic.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using ZenKit;
using ZenKit.Vobs;

using Material = OpenGothic.Scene.Material;

namespace OpenGothic
{
	partial class Assets
	{
		private class CellData
		{
			public List<int> Polygons { get; } = new List<int>();
			public BoundingBox BoundingBox;
		}

		private class MaterialInfo
		{
			public ZenKit.IMaterial Material { get; }

			public MaterialInfo(ZenKit.IMaterial material)
			{
				Material = material ?? throw new ArgumentNullException(nameof(material));
			}

			public bool EqualsTo(ZenKit.IMaterial material)
			{
				return Material.Texture == material.Texture;
			}

			public override string ToString() => Material.Texture;
		}

		private WorldGrid LoadWorld(GraphicsDevice device, string name)
		{
			var record = GetLastRecord(name);

			var zkWorld = new World(record.Node.Buffer);

			var polygons = zkWorld.Mesh.Polygons;
			var materials = zkWorld.Mesh.Materials;
			var features = zkWorld.Mesh.Features;
			var positions = zkWorld.Mesh.Positions;

			// Group materials
			var materialsGroups = new List<MaterialInfo>();
			var materialsMap = new Dictionary<int, int>();
			for (var i = 0; i < materials.Count; ++i)
			{
				var mat = materials[i];
				if (string.IsNullOrEmpty(mat.Texture))
				{
					continue;
				}

				int? mgIndex = null;
				for (var j = 0; j < materialsGroups.Count; ++j)
				{
					if (materialsGroups[j].EqualsTo(mat))
					{
						mgIndex = j;
						break;
					}
				}

				if (mgIndex == null)
				{
					var mg = new MaterialInfo(mat);
					materialsGroups.Add(mg);
					mgIndex = materialsGroups.Count - 1;
				}

				materialsMap[i] = mgIndex.Value;
			}

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
					var max = new Vector3(min.X + cellSizeX, boundingBox.Max.Y, min.Z + cellSizeZ);

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
			found:;
			}

			var result = new WorldGrid();
			result.MeshBounds = boundingBox;
			for (var i = 0; i < Constants.GridSize; ++i)
			{
				for (var j = 0; j < Constants.GridSize; ++j)
				{
					var cellData = cellsData[i, j];

					// Group polygons by materials
					var groupedPolygons = new Dictionary<int, Tuple<Material, List<IPolygon>>>();
					for (var k = 0; k < cellData.Polygons.Count; ++k)
					{
						var polygon = polygons[cellData.Polygons[k]];

						var originalMaterial = materials[polygon.MaterialIndex];
						if (string.IsNullOrEmpty(originalMaterial.Texture))
						{
							continue;
						}

						var materialIndex = materialsMap[polygon.MaterialIndex];
						Tuple<Material, List<IPolygon>> p;
						if (!groupedPolygons.TryGetValue(materialIndex, out p))
						{
							var zkMaterial = materialsGroups[materialIndex].Material;

							Texture2D texture = null;

							if (!string.IsNullOrEmpty(zkMaterial.Texture))
							{
								texture = GetTexture(device, zkMaterial.Texture);
							}
							else
							{
								continue;
							}

							var mat = new Material
							{
								DiffuseColor = Color.White,
								DiffuseTexture = texture
							};

							if (zkMaterial.Group == MaterialGroup.Water/* || zkMaterial.Group == MaterialGroup.Earth || zkMaterial.Group == MaterialGroup.Snow*/)
							{
								mat.CastsShadow = false;
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

							p = new Tuple<Material, List<IPolygon>>(mat, new List<IPolygon>());
							groupedPolygons[materialIndex] = p;
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
							if (polygon.IsGhostOccluder)
							{
								continue;
							}

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

						if (meshBuilder.IndicesCount > 0)
						{

							var meshPart = meshBuilder.CreateMeshPart(device, true);
							meshPart.Tag = new DrMeshPartTag(pair.Value.Item1);

							var meshNode = new MeshNode
							{
								Mesh = meshPart
							};

							cell.Children.Add(meshNode);
						}
					}

					// Update bounding box
					cell.BoundingBox = cellData.BoundingBox;
				}
			}

			// Virtual objects

			// Enqueue root objects. Gothic stores a vob's transform in world space, so a child's
			// local transform is derived against its parent's. Rendering then composes the
			// chain, which is what lets a moving parent carry its children; a static world
			// still lands exactly where the file puts it.
			var vobs = new List<(SceneNode node, Matrix world, IVirtualObject vob)>();
			foreach (var vob in zkWorld.RootObjects)
			{
				vobs.Add((null, Matrix.Identity, vob));
			}

			while (vobs.Count > 0)
			{
				var (parentNode, parentWorld, vob) = vobs[0];
				vobs.RemoveAt(0);

				var world = vob.Rotation.ToXna() * Matrix.CreateTranslation(vob.Position.ToXna());
				var local = parentNode == null ? world : world * Matrix.Invert(parentWorld);

				SceneNode node = null;

				var asModel = vob.Visual as VisualModel;
				if (asModel != null && DebugSettings.ModelsEnabled)
				{
					// A vob's model name comes from the original authoring format, so only
					// the stem is meaningful; GetModel resolves the actual file. A model that
					// cannot be loaded is skipped rather than aborting the level, matching how
					// a missing texture degrades instead of failing the load.
					DrModel model = null;
					try
					{
						model = GetModel(device, asModel.Name);
					}
					catch (Exception ex)
					{
						Log.Warning($"Unable to load model '{asModel.Name}': {ex.Message}");
					}

					if (model != null)
					{
						var instance = new DrModelInstance(model);

						// The instance box is in model space; the vob transform places it. A
						// model with no geometry yields a degenerate box at the vob's own
						// position, which is still the right cell to register it in.
						var bb = instance.BoundingBox ?? new BoundingBox(vob.Position.ToXna(), vob.Position.ToXna());
						var worldBounds = bb.Transform(ref world);

						var cell = result.FindCellByBox(worldBounds);
						if (cell != null)
						{
							cell.ObjectBounds.Add(worldBounds);

							var modelNode = new ModelNode
							{
								Instance = instance,
								Transform = local
							};

							cell.Children.Add(modelNode);
							parentNode?.Children.Add(modelNode);
							node = modelNode;
						}
					}
				}

				var asMesh = vob.Visual as VisualMultiResolutionMesh;
				if (asMesh != null)
				{
					// Instanced transforms stay in world space, so a multi-mesh does not take
					// part in the node hierarchy; no mover uses one as a child today.
					var n = Path.ChangeExtension(asMesh.Name, "MRM");
					var mesh = GetMultiMesh(device, n);

					var bb = mesh.BoundingBox.Transform(ref world);

					var cell = result.FindCellByBox(bb);
					if (cell != null)
					{
						cell.ObjectBounds.Add(bb);

						Dictionary<string, List<InstancedMeshNode>> groupedMultiMeshes;
						if (cell.Tag == null)
						{
							groupedMultiMeshes = new Dictionary<string, List<InstancedMeshNode>>();
							cell.Tag = groupedMultiMeshes;
						}
						else
						{
							groupedMultiMeshes = (Dictionary<string, List<InstancedMeshNode>>)cell.Tag;
						}

						List<InstancedMeshNode> multiMeshes;
						if (groupedMultiMeshes.TryGetValue(n, out multiMeshes))
						{
							// Add instance
							foreach (var mm in multiMeshes)
							{
								mm.InstancesTransforms.Add(world);
							}
						}
						else
						{
							multiMeshes = new List<InstancedMeshNode>();

							foreach (var part in mesh.MeshParts)
							{
								var mm = new InstancedMeshNode
								{
									Mesh = part
								};

								multiMeshes.Add(mm);
							}

							groupedMultiMeshes[n] = multiMeshes;
						}
					}
				}

				foreach (var child in vob.Children)
				{
					// A vob with children needs something to hang them from even when it has no
					// visual of its own.
					if (node == null)
					{
						node = new SceneNode
						{
							Transform = local
						};

						parentNode?.Children.Add(node);
					}

					vobs.Add((node, world, child));
				}
			}

			// Add multimeshes
			for (var i = 0; i < Constants.GridSize; ++i)
			{
				for (var j = 0; j < Constants.GridSize; ++j)
				{
					var cell = result.Cells[i, j];
					if (cell.Tag == null)
					{
						continue;
					}

					var groupedMultiMeshes = (Dictionary<string, List<InstancedMeshNode>>)cell.Tag;
					cell.Tag = null;

					foreach (var pair in groupedMultiMeshes)
					{
						var multiMeshes = pair.Value;

						foreach (var mm in multiMeshes)
						{
							if (mm.InstancesTransforms.Count == 1)
							{
								// Add as simple mesh nodes
								for (var k = 0; k < mm.InstancesTransforms.Count; ++k)
								{
									var transform = mm.InstancesTransforms[k];

									var meshNode = new MeshNode
									{
										Mesh = mm.Mesh,
										Transform = transform
									};

									cell.Children.Add(meshNode);
								}
							}
							else
							{
								cell.Children.Add(mm);
							}
						}
					}
				}
			}

			// The level mesh collides as it is stored, not as it is drawn: invisible geometry
			// with no texture still blocks a body, and ghost occluders are the same kind of
			// geometry. Only a material that disables collision is left out.
			if (DebugSettings.CollisionEnabled)
			{
				BuildTerrainCollision(zkWorld.Mesh, result);
				Log.Info(
					$"World collision: {result.CollisionChunks.Count} chunks over " +
					$"{result.MeshBounds.Min}..{result.MeshBounds.Max}.");
			}

			return result;
		}

		/// <summary>
		/// Triangulates the level mesh for physics, grouped by the friction of each material
		/// and bucketed into collision chunks. The polygons are fan-triangulated exactly like
		/// the render path, but every material is kept regardless of whether it has a texture.
		/// </summary>
		private static void BuildTerrainCollision(IMesh mesh, WorldGrid result)
		{
			var positions = mesh.Positions;
			var materials = mesh.Materials;
			var bounds = result.MeshBounds;
			var origin = bounds.Min;
			const float size = WorldGrid.CollisionChunkSize;

			foreach (var polygon in mesh.Polygons)
			{
				var material = materials[polygon.MaterialIndex];
				if (material.DisableCollision)
				{
					continue;
				}

				var indices = polygon.PositionIndices;
				if (indices.Count < 3)
				{
					continue;
				}

				var sensor = material.Group == MaterialGroup.Water;
				var friction = PhysicsMaterials.GetFriction(material.Group);
				var first = positions[indices[0]].ToXna();

				for (var i = 2; i < indices.Count; ++i)
				{
					var second = positions[indices[i - 1]].ToXna();
					var third = positions[indices[i]].ToXna();
					AddToChunks(result, origin, size, bounds, first, second, third, friction, sensor);
				}
			}
		}

		/// <summary>
		/// Adds a triangle to the chunk that contains its centroid. One owner per triangle keeps
		/// chunk seams free of duplicate colliders; the streaming radius is wide enough that a
		/// loaded neighbourhood always contains the player's adjacent chunks.
		/// </summary>
		private static void AddToChunks(WorldGrid result, Vector3 origin, float size, BoundingBox meshBounds,
			Vector3 a, Vector3 b, Vector3 c, float friction, bool sensor)
		{
			var center = (a + b + c) / 3f;
			var x = (int)MathF.Floor((center.X - origin.X) / size);
			var z = (int)MathF.Floor((center.Z - origin.Z) / size);

			var key = (x, z);
			if (!result.CollisionChunks.TryGetValue(key, out var chunk))
			{
				var chunkMin = new Vector3(origin.X + x * size, meshBounds.Min.Y, origin.Z + z * size);
				var chunkMax = new Vector3(chunkMin.X + size, meshBounds.Max.Y, chunkMin.Z + size);
				chunk = new CollisionChunk(new BoundingBox(chunkMin, chunkMax));
				result.CollisionChunks[key] = chunk;
			}

			chunk.Surfaces.AddTriangle(friction, sensor, a, b, c);
		}

		public WorldGrid GetWorld(GraphicsDevice device, string name) => Get(device, name, LoadWorld);
	}
}
