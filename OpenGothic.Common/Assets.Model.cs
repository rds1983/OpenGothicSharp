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
			var skinMap = new Dictionary<int, int>();

			var nodes = zkSkinnedMesh.Nodes;
			for (var i = 0; i < nodes.Count; ++i)
			{
				skinMap[nodes[i]] = i;
			}

			var mb = new MeshBuilderS();

			var wedges = zkSubMesh.Wedges;
			var zkWeights = zkSkinnedMesh.Weights;
			for (var i = 0; i < wedges.Count; ++i)
			{
				var wedge = wedges[i];

				var blendIndices = Vector4.Zero;
				var blendWeights = Vector4.Zero;
				var pos0 = Vector3.Zero;
				var pos1 = Vector3.Zero;
				var pos2 = Vector3.Zero;
				var pos3 = Vector3.Zero;

				var weight = zkWeights[wedge.Index];

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

				mb.AddVertex(vertex);
			}

			// Set indices/weights
			for (var i = 0; i < zkWeights.Count; ++i)
			{
				var weights = zkWeights[i];
				for (var j = 0; j < weights.Count; ++j)
				{
					var weight = weights[j];
				}
			}

			meshBuilder = mb;
		}

		var triangles = zkSubMesh.Triangles;
		for (var i = 0; i < triangles.Count; ++i)
		{
			var triangle = triangles[i];

			meshBuilder.AddIndex(triangle.Wedge0);
			meshBuilder.AddIndex(triangle.Wedge1);
			meshBuilder.AddIndex(triangle.Wedge2);
		}

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

		var result = meshBuilder.CreateMeshPart(device, true);
		result.Material = material;

		return result;
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

		var attachments = zkMesh.Attachments;
		foreach (var pair in attachments)
		{
			var mesh = CreateMesh(device, null, pair.Value);

			meshes[pair.Key] = mesh;
		}

		// Load nodes
		var nodesData = new List<Tuple<DrModelBone, int?>>();

		var nodes = zkHierarchy.Nodes;
		for (var i = 0; i < nodes.Count; ++i)
		{
			var node = nodes[i];

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
			bone.DefaultPose = new SrtTransform(transform);

			if (i == 0)
			{
				bone.DefaultPose.Translation += zkHierarchy.RootTranslation.ToXna();
			}

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

		var zkMeshes = zkMesh.Meshes;
		if (zkMeshes.Count > 0)
		{
			// Create new root
			root = new DrModelBone("_ROOT");

			var children = new List<DrModelBone>();

			// Load animated meshes
			for (var i = 0; i < zkMeshes.Count; ++i)
			{
				var zkSkinnedMesh = zkMeshes[i];
				var mesh = CreateMesh(device, zkSkinnedMesh, zkSkinnedMesh.Mesh);

				// Store joint bones
				var joints = new List<DrModelBone>();

				var zkMeshNodes = zkSkinnedMesh.Nodes;
				for (var j = 0; j < zkMeshNodes.Count; ++j)
				{
					joints.Add(originalBones[zkMeshNodes[j]]);
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
		if (zkMeshes.Count > 0)
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
		name = name.ToUpper();
		var record = GetLastRecord(name);

		var zkScript = new ModelScript(record.Node.Buffer);

		var hierarchyName = Path.ChangeExtension(name, "MDH");
		var zkHierarchy = new ModelHierarchy(GetLastRecord(hierarchyName).Node.Buffer);

		var meshName = Path.ChangeExtension(zkScript.SkeletonName, "MDM");
		var zkMesh = new ZenKit.ModelMesh(GetLastRecord(meshName).Node.Buffer);

		var modelInfo = LoadModel(device, zkHierarchy, zkMesh);

		var nameNoExt = Path.GetFileNameWithoutExtension(name);

		modelInfo.Model.Animations = new Dictionary<string, AnimationClip>();

		var zkAnimations = zkScript.Animations;
		for (var i = 0; i < zkAnimations.Count; ++i)
		{
			var animation = zkAnimations[i];

			var animationName = nameNoExt + "-" + animation.Name + ".MAN";

			var zkAnimation = new ModelAnimation(GetLastRecord(animationName).Node.Buffer);

			var timeStep = TimeSpan.FromSeconds(1.0f / animation.Fps);

			var channels = new List<AnimationChannel>();

			var zkAnimationNodeIndices = zkAnimation.NodeIndices;

			var samples = zkAnimation.Samples;
			for (var k = 0; k < zkAnimationNodeIndices.Count; ++k)
			{
				var bone = modelInfo.OriginalBones[zkAnimationNodeIndices[k]];

				var keyframes = new List<AnimationChannelKeyframe>();
				var time = TimeSpan.Zero;
				for (var j = 0; j < zkAnimation.FrameCount; ++j)
				{
					var pos = k + j * zkAnimationNodeIndices.Count;
					var sample = samples[pos];

					time += timeStep;
					var keyframe = new AnimationChannelKeyframe(time, new SrtTransform(sample.Position.ToXna(), sample.Rotation.ToXna(), bone.DefaultPose.Scale));

					keyframes.Add(keyframe);
				}

				var channel = new AnimationChannel(bone.Index, keyframes.ToArray());
				channels.Add(channel);
			}

			var clip = new AnimationClip(animation.Name, zkAnimation.FrameCount * timeStep, channels.ToArray());
			modelInfo.Model.Animations[clip.Name] = clip;
		}

		return modelInfo.Model;
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
