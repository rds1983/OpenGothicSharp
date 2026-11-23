using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using OpenGothic.Scene;
using OpenGothic.Utility;
using OpenGothic.Vertices;
using System;
using System.Collections.Generic;
using System.IO;
using ZenKit;

namespace OpenGothic;

partial class Assets
{
	private DrMeshPart CreatePart(GraphicsDevice device, ISoftSkinMesh zkSkinnedMesh, IMultiResolutionMesh zkMesh, IMultiResolutionSubMesh zkSubMesh, Matrix[] bindTransforms, Dictionary<int, int> skinMap)
	{
		IMeshBuilder meshBuilder;
		if (zkSkinnedMesh == null)
		{
			var mb = new MeshBuilderPNT();
			var wedges = zkSubMesh.Wedges;
			var positions = zkMesh.Positions;
			for (var i = 0; i < wedges.Count; ++i)
			{
				var wedge = wedges[i];
				var pos = positions[wedge.Index];

				var vertex = new VertexPositionNormalTexture(pos.ToXna(), wedge.Normal.ToXna(), wedge.Texture.ToXna());

				mb.AddVertex(vertex);
			}

			meshBuilder = mb;
		}
		else
		{
			meshBuilder = CreateSkinnedBuilder(zkSkinnedMesh, zkSubMesh, bindTransforms, skinMap);
		}

		var triangles = zkSubMesh.Triangles;
		for (var i = 0; i < triangles.Count; ++i)
		{
			var triangle = triangles[i];

			meshBuilder.AddIndex(triangle.Wedge0);
			meshBuilder.AddIndex(triangle.Wedge1);
			meshBuilder.AddIndex(triangle.Wedge2);
		}

		Scene.Material material = null;
		if (zkSubMesh.Material != null)
		{
			material = new Scene.Material
			{
				DiffuseColor = Color.White
			};

			if (!string.IsNullOrEmpty(zkSubMesh.Material.Texture))
			{
				material.DiffuseTexture = GetTexture(device, zkSubMesh.Material.Texture);
			}
		}

		var result = meshBuilder.CreateMeshPart(device, true);
		result.Tag = new DrMeshPartTag(material);

		return result;
	}

	/// <summary>
	/// Rebuilds the vertices of a skinned sub-mesh.
	/// <para>
	/// A weight stores its position in its own bone's space, and every weight of one
	/// vertex resolves to the same model-space point, so the bind position is recovered by
	/// pushing a weight position through that bone's absolute bind transform. The mesh's
	/// own position array cannot be used: for skinned meshes it holds values that are not
	/// vertex positions at all.
	/// </para>
	/// <para>
	/// Weights address hierarchy node indices, while the blend indices handed to the GPU
	/// address the skin's joint array, so <paramref name="skinMap"/> translates between
	/// the two.
	/// </para>
	/// </summary>
	private static MeshBuilderS CreateSkinnedBuilder(ISoftSkinMesh zkSkinnedMesh, IMultiResolutionSubMesh zkSubMesh, Matrix[] bindTransforms, Dictionary<int, int> skinMap)
	{
		var mb = new MeshBuilderS();

		var wedges = zkSubMesh.Wedges;
		var zkWeights = zkSkinnedMesh.Weights;

		for (var i = 0; i < wedges.Count; ++i)
		{
			var wedge = wedges[i];
			var weight = zkWeights[wedge.Index];

			// The vertex format has four blend slots; real data never exceeds three.
			var count = Math.Min(weight.Count, 4);

			var position = Vector3.Zero;
			var blendIndices = Vector4.Zero;
			var blendWeights = Vector4.Zero;

			for (var j = 0; j < count; ++j)
			{
				var item = weight[j];

				// One weight is enough for the position: they all agree. Blending them would
				// only add rounding error.
				if (j == 0)
				{
					position = Vector3.Transform(item.Position.ToXna(), bindTransforms[item.NodeIndex]);
				}

				var boneIndex = skinMap[item.NodeIndex];
				switch (j)
				{
					case 0:
						blendIndices.X = boneIndex;
						blendWeights.X = item.Weight;
						break;
					case 1:
						blendIndices.Y = boneIndex;
						blendWeights.Y = item.Weight;
						break;
					case 2:
						blendIndices.Z = boneIndex;
						blendWeights.Z = item.Weight;
						break;
					default:
						blendIndices.W = boneIndex;
						blendWeights.W = item.Weight;
						break;
				}
			}

			// SkinnedEffect expects the weights to sum to one.
			var sum = blendWeights.X + blendWeights.Y + blendWeights.Z + blendWeights.W;
			if (sum > 0f && !sum.EpsilonEquals(1f))
			{
				blendWeights /= sum;
			}

			var vertex = new VertexSkinned(position, wedge.Normal.ToXna(), wedge.Texture.ToXna(), new Byte4(blendIndices), blendWeights);

			mb.AddVertex(vertex);
		}

		return mb;
	}

	private DrMesh CreateMesh(GraphicsDevice device, ISoftSkinMesh zkSkinnedMesh, IMultiResolutionMesh zkMesh, Matrix[] bindTransforms = null, Dictionary<int, int> skinMap = null)
	{
		var mesh = new DrMesh();
		foreach (var submesh in zkMesh.SubMeshes)
		{
			var meshPart = CreatePart(device, zkSkinnedMesh, zkMesh, submesh, bindTransforms, skinMap);
			mesh.MeshParts.Add(meshPart);
		}

		return mesh;
	}

	/// <summary>
	/// A skinned mesh plus the hierarchy nodes it deforms across, in blend-index order.
	/// </summary>
	private sealed class SkinnedMeshInfo
	{
		public DrMesh Mesh { get; }
		public IReadOnlyList<int> JointNodes { get; }

		public SkinnedMeshInfo(DrMesh mesh, IReadOnlyList<int> jointNodes)
		{
			Mesh = mesh;
			JointNodes = jointNodes;
		}
	}

	/// <summary>
	/// Assembles a <see cref="DrModel"/> from a hierarchy and its meshes.
	/// <para>
	/// A static mesh is attached to the hierarchy node that carries its name, so the node's
	/// transform positions it. A skinned mesh cannot be: it deforms across many bones, and
	/// one bone owns at most one mesh, so each skinned mesh gets a synthetic bone below a
	/// synthetic root and the real skeleton hangs beside them. The skin still references
	/// the real bones, which is what makes the deformation follow the skeleton.
	/// </para>
	/// </summary>
	private DrModel LoadModel(GraphicsDevice device, IModelHierarchy zkHierarchy, IModelMesh zkMesh)
	{
		// Static meshes are named after the hierarchy node they belong to.
		var staticMeshes = new Dictionary<string, DrMesh>(StringComparer.OrdinalIgnoreCase);
		foreach (var pair in zkMesh.Attachments)
		{
			staticMeshes[pair.Key] = CreateMesh(device, null, pair.Value);
		}

		// One bone per hierarchy node, in hierarchy order, so a weight's node index
		// addresses this array directly. The nodes are ordered parents-first, so both the
		// parenting and the bind transforms below can be resolved in a single pass.
		var nodes = zkHierarchy.Nodes;
		var bones = new DrModelBone[nodes.Count];
		var parents = new int[nodes.Count];

		for (var i = 0; i < nodes.Count; ++i)
		{
			var node = nodes[i];

			DrMesh mesh;
			if (!staticMeshes.TryGetValue(node.Name, out mesh))
			{
				mesh = null;
			}

			var bone = new DrModelBone(node.Name, mesh);
			bone.DefaultPose = new SrtTransform(node.Transform.ToXna());

			// The root offset lifts the model so its lowest point sits on the origin: an
			// actor's bind pose is authored around the hips, with the feet below y = 0.
			if (i == 0)
			{
				bone.DefaultPose.Translation += zkHierarchy.RootTranslation.ToXna();
			}

			bones[i] = bone;
			parents[i] = node.ParentIndex;
		}

		for (var i = 0; i < bones.Length; ++i)
		{
			List<DrModelBone> children = null;
			for (var j = 0; j < bones.Length; ++j)
			{
				if (parents[j] == i)
				{
					children ??= new List<DrModelBone>();
					children.Add(bones[j]);
				}
			}

			if (children != null)
			{
				bones[i].Children = children.ToArray();
			}
		}

		// Absolute bind transforms, in hierarchy order. Computed here rather than through
		// DrModel, whose bone numbering is a depth-first traversal order and therefore does
		// not match the hierarchy order the weights use.
		var bindTransforms = new Matrix[bones.Length];
		for (var i = 0; i < bones.Length; ++i)
		{
			var local = bones[i].CalculateDefaultLocalTransform();
			bindTransforms[i] = parents[i] < 0 ? local : local * bindTransforms[parents[i]];
		}

		var skinnedMeshes = new List<SkinnedMeshInfo>();

		var root = bones[0];
		var zkSkinnedMeshes = zkMesh.Meshes;
		if (zkSkinnedMeshes.Count > 0)
		{
			root = new DrModelBone("_ROOT");

			var children = new List<DrModelBone>();

			for (var i = 0; i < zkSkinnedMeshes.Count; ++i)
			{
				var zkSkinnedMesh = zkSkinnedMeshes[i];

				// Every node a weight mentions must be a joint, so the skin's joint array is
				// built from the mesh's node list first and any remaining weight nodes after.
				var jointNodes = new List<int>();
				var blendIndexByNode = new Dictionary<int, int>();

				foreach (var nodeIndex in zkSkinnedMesh.Nodes)
				{
					if (blendIndexByNode.TryAdd(nodeIndex, jointNodes.Count))
					{
						jointNodes.Add(nodeIndex);
					}
				}

				foreach (var vertexWeights in zkSkinnedMesh.Weights)
				{
					foreach (var weight in vertexWeights)
					{
						if (blendIndexByNode.TryAdd(weight.NodeIndex, jointNodes.Count))
						{
							jointNodes.Add(weight.NodeIndex);
						}
					}
				}

				if (jointNodes.Count > SkinnedEffect.MaxBones)
				{
					throw new NotSupportedException(
						$"Skinned mesh {i} of this model has {jointNodes.Count} joints, but SkinnedEffect supports {SkinnedEffect.MaxBones}.");
				}

				var mesh = CreateMesh(device, zkSkinnedMesh, zkSkinnedMesh.Mesh, bindTransforms, blendIndexByNode);

				skinnedMeshes.Add(new SkinnedMeshInfo(mesh, jointNodes));

				children.Add(new DrModelBone($"_MESH{i}", mesh));
			}

			children.Add(bones[0]);

			root.Children = children.ToArray();
		}

		var result = new DrModel(root);

		// Now that the parts exist, give each skinned mesh its skin. The GPU is handed
		// inverseBind * boneWorld, so this must be the inverse of the bind pose.
		for (var i = 0; i < skinnedMeshes.Count; ++i)
		{
			var info = skinnedMeshes[i];

			var joints = new DrSkinJoint[info.JointNodes.Count];
			for (var j = 0; j < joints.Length; ++j)
			{
				var nodeIndex = info.JointNodes[j];
				joints[j] = new DrSkinJoint(bones[nodeIndex], Matrix.Invert(bindTransforms[nodeIndex]));
			}

			var skin = new DrSkin(i, joints);
			foreach (var part in info.Mesh.MeshParts)
			{
				part.Skin = skin;
			}
		}

		return result;
	}

	/// <summary>
	/// Loads a model by name. A self-contained <c>.MDL</c> carries its own hierarchy and
	/// mesh and is preferred; otherwise the skeleton <c>.MDH</c> and mesh <c>.MDM</c> pair
	/// is used. Names may be given with or without an extension.
	/// </summary>
	private DrModel LoadModel(GraphicsDevice device, string name)
	{
		var stem = Path.GetFileNameWithoutExtension(name).ToUpper();

		IModelHierarchy zkHierarchy;
		IModelMesh zkMesh;

		var mdlName = stem + ".MDL";
		if (_allRecords.ContainsKey(mdlName))
		{
			var mdl = new ZenKit.Model(GetLastRecord(mdlName).Node.Buffer);
			zkHierarchy = mdl.Hierarchy;
			zkMesh = mdl.Mesh;
		}
		else
		{
			var mdhName = stem + ".MDH";
			var mdmName = stem + ".MDM";

			if (!_allRecords.ContainsKey(mdhName) || !_allRecords.ContainsKey(mdmName))
			{
				throw new InvalidOperationException($"Model '{stem}' has neither a .MDL nor a .MDH/.MDM pair.");
			}

			zkHierarchy = new ModelHierarchy(GetLastRecord(mdhName).Node.Buffer);
			zkMesh = new ZenKit.ModelMesh(GetLastRecord(mdmName).Node.Buffer);
		}

		var model = LoadModel(device, zkHierarchy, zkMesh);

		// A model carries no animation of its own: the clips come from the model script that
		// owns it, so attaching them is a separate step. Props have no script and stay still.
		// A clip problem must never cost the model itself, so a failure here is only logged.
		try
		{
			AttachAnimations(model, stem);
		}
		catch (Exception ex)
		{
			Log.Error($"Unable to attach animations to model '{stem}': {ex.Message}");
		}

		return model;
	}

	private DrMesh LoadMultiMesh(GraphicsDevice device, string name)
	{
		var record = GetLastRecord(name);
		var zkMesh = new MultiResolutionMesh(record.Node.Buffer);

		return CreateMesh(device, null, zkMesh);
	}

	public DrModel GetModel(GraphicsDevice device, string name) => Get(device, name, LoadModel);
	public DrMesh GetMultiMesh(GraphicsDevice device, string name) => Get(device, name, LoadMultiMesh);
}
