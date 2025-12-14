using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using OpenGothic.Utility;
using OpenGothic.Vertices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZenKit;

namespace OpenGothic;

partial class Assets
{
	private class ModelResult
	{
		public DrModel Model { get; }
		public DrModelBone[] OriginalBones { get; }

		public ModelResult(DrModel model, DrModelBone[] originalBones)
		{
			Model = model ?? throw new ArgumentNullException(nameof(model));
			OriginalBones = originalBones;
		}
	}

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
			var skinMap = new Dictionary<int, int>();
			for (var i = 0; i < zkSkinnedMesh.Nodes.Count; ++i)
			{
				skinMap[zkSkinnedMesh.Nodes[i]] = i;
			}

			var vertices = new List<VertexSkinned>();
			for (var i = 0; i < zkSubMesh.Wedges.Count; ++i)
			{
				var wedge = zkSubMesh.Wedges[i];

				var blendIndices = Vector4.Zero;
				var blendWeights = Vector4.Zero;
				var pos0 = Vector3.Zero;
				var pos1 = Vector3.Zero;
				var pos2 = Vector3.Zero;
				var pos3 = Vector3.Zero;

				var weight = zkSkinnedMesh.Weights[wedge.Index];
				pos0 = weight[0].Position.ToXna();
				blendIndices.X = skinMap[weight[0].NodeIndex];
				blendWeights.X = weight[0].Weight;

				if (weight.Count > 1)
				{
					pos1 = weight[1].Position.ToXna();
					blendIndices.Y = skinMap[weight[1].NodeIndex];
					blendWeights.Y = weight[1].Weight;
				}

				if (weight.Count > 2)
				{
					pos2 = weight[2].Position.ToXna();
					blendIndices.Z = skinMap[weight[2].NodeIndex];
					blendWeights.Z = weight[2].Weight;
				}

				if (weight.Count > 3)
				{
					pos3 = weight[3].Position.ToXna();
					blendIndices.W = skinMap[weight[3].NodeIndex];
					blendWeights.W = weight[3].Weight;
				}

				var sum = blendWeights.X + blendWeights.Y + blendWeights.Z + blendWeights.W;
				blendWeights.X /= sum;
				blendWeights.Y /= sum;
				blendWeights.Z /= sum;
				blendWeights.W /= sum;

				var vertex = new VertexSkinned(pos0, pos1, pos2, pos3, wedge.Normal.ToXna(), wedge.Texture.ToXna(), new Byte4(blendIndices), blendWeights);

				vertices.Add(vertex);
			}

			// Set indices/weights
			for (var i = 0; i < zkSkinnedMesh.Weights.Count; ++i)
			{
				var weights = zkSkinnedMesh.Weights[i];
				for (var j = 0; j < weights.Count; ++j)
				{
					var weight = weights[j];
				}
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

	private ModelResult LoadModel(GraphicsDevice device, IModelHierarchy zkHierarchy, IModelMesh zkMesh)
	{
		// Load meshes
		var meshes = new Dictionary<string, DrMesh>();
		foreach (var pair in zkMesh.Attachments)
		{
			var mesh = CreateMesh(device, null, pair.Value);

			meshes[pair.Key] = mesh;
		}

		// Load nodes
		var nodesData = new List<Tuple<DrModelBone, int?>>();
		for (var i = 0; i < zkHierarchy.Nodes.Count; ++i)
		{
			var node = zkHierarchy.Nodes[i];

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

			var transform = node.Transform.ToXna();

			// Transpose is required
			transform = Matrix.Transpose(transform);
			bone.DefaultPose = new SrtTransform(transform);

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

		var originalBones = (from nd in nodesData select nd.Item1).ToArray();

		var root = originalBones[0];
		if (zkMesh.Meshes.Count > 0)
		{
			// Create new root
			root = new DrModelBone("_ROOT");

			var children = new List<DrModelBone>();

			// Load animated meshes
			for (var i = 0; i < zkMesh.Meshes.Count; ++i)
			{
				var zkSkinnedMesh = zkMesh.Meshes[i];
				var mesh = CreateMesh(device, zkSkinnedMesh, zkSkinnedMesh.Mesh);

				// Store joint bones
				var joints = new List<DrModelBone>();
				for (var j = 0; j < zkSkinnedMesh.Nodes.Count; ++j)
				{
					joints.Add(originalBones[zkSkinnedMesh.Nodes[j]]);
				}
				mesh.Tag = joints;

				var meshNode = new DrModelBone($"_MESH{i}", mesh);
				children.Add(meshNode);
			}

			children.Add(originalBones[0]);

			root.Children = children.ToArray();
		}

		var result = new DrModel(root);

		// Finally update the skins
		if (zkMesh.Meshes.Count > 0)
		{
			var boneTransforms = new Matrix[result.Bones.Length];
			result.CopyAbsoluteBoneTransformsTo(boneTransforms);
			for (var i = 0; i < result.Meshes.Length; ++i)
			{
				var mesh = result.Meshes[i];

				if (mesh.Tag != null)
				{
					var joints = (List<DrModelBone>)mesh.Tag;

					var jointsData = new List<DrSkinJoint>();
					for (var j = 0; j < joints.Count; ++j)
					{
						var joint = new DrSkinJoint(joints[j], Matrix.Identity);
						// var joint = new DrSkinJoint(joints[j], Matrix.Invert(boneTransforms[joints[j].Index]));
						jointsData.Add(joint);
					}

					var skin = new DrSkin(i, jointsData.ToArray());
					for (var j = 0; j < mesh.MeshParts.Count; ++j)
					{
						mesh.MeshParts[j].Skin = skin;
					}

					mesh.Tag = null;
				}
			}
		}

		return new ModelResult(result, originalBones);
	}

	private DrModel LoadModel(GraphicsDevice device, string name)
	{
		var records = _allRecords[name];
		var record = records[records.Count - 1];

		var zkScript = new ModelScript(record.Node.Buffer);

		var hierarchyName = Path.ChangeExtension(name, "MDH");
		var zkHierarchy = new ModelHierarchy(GetLastRecord(hierarchyName).Node.Buffer);

		var meshName = Path.ChangeExtension(zkScript.SkeletonName, "MDM");
		var zkMesh = new ZenKit.ModelMesh(GetLastRecord(meshName).Node.Buffer);

		var result = LoadModel(device, zkHierarchy, zkMesh);

		var nameNoExt = Path.GetFileNameWithoutExtension(name);

		result.Model.Animations = new Dictionary<string, AnimationClip>();
		for (var i = 0; i < zkScript.Animations.Count; ++i)
		{
			var animation = zkScript.Animations[i];

			var animationName = nameNoExt + "-" + animation.Name + ".MAN";

			var zkAnimation = new ModelAnimation(GetLastRecord(animationName).Node.Buffer);

			var channels = new List<AnimationChannel>();
			for (var k = 0; k < zkAnimation.NodeIndices.Count; ++k)
			{
				var bone = result.OriginalBones[zkAnimation.NodeIndices[k]];

				var keyframes = new List<AnimationChannelKeyframe>();
				for (var j = 0; j < zkAnimation.FrameCount; ++j)
				{
					var pos = k + j * zkAnimation.NodeIndices.Count;
					var sample = zkAnimation.Samples[pos];

					var keyframe = new AnimationChannelKeyframe(TimeSpan.FromMilliseconds(100), new SrtTransform(sample.Position.ToXna(), sample.Rotation.ToXna(), Vector3.One));

					keyframes.Add(keyframe);
				}

				var channel = new AnimationChannel(bone.Index, keyframes.ToArray());
				channels.Add(channel);
			}

			var clip = new AnimationClip(animation.Name, zkAnimation.FrameCount * TimeSpan.FromMilliseconds(100), channels.ToArray());
			result.Model.Animations[clip.Name] = clip;
		}

		return result.Model;

	}

	public DrModel GetModel(GraphicsDevice device, string name) => Get(device, name, LoadModel);
}
