using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using OpenGothic.Graphics;
using OpenGothic.Utility;
using OpenGothic.Graphics.Vertices;
using System;
using System.Collections.Generic;
using System.IO;
using ZenKit;

namespace OpenGothic;

partial class Assets
{
	private DrMeshPart CreatePart(GraphicsDevice device, ISoftSkinMesh zkSkinnedMesh, IMultiResolutionMesh zkMesh, IMultiResolutionSubMesh zkSubMesh, Matrix[] bindTransforms, Matrix[] restTransforms, int[] nodeToBone, Dictionary<int, int> skinMap)
	{
		// A submesh without a texture is a hidden/placeholder face in the source data, not
		// untextured geometry: REPAIR_PLANK's ZM_REP_HIDDEN triangle is an invisible marker.
		// The reference client skips these, so drawing the white fallback would produce a
		// solid triangle in the world.
		var zkMaterial = zkSubMesh.Material;
		if (zkMaterial == null || string.IsNullOrEmpty(zkMaterial.Texture))
		{
			return null;
		}

		var texture = GetTexture(device, zkMaterial.Texture);
		if (texture == null)
		{
			return null;
		}

		IMeshBuilder meshBuilder;
		MeshBuilderS skinnedBuilder = null;
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
			skinnedBuilder = CreateSkinnedBuilder(zkSkinnedMesh, zkSubMesh, bindTransforms, restTransforms, nodeToBone, skinMap);
			meshBuilder = skinnedBuilder;
		}

		var triangles = zkSubMesh.Triangles;
		for (var i = 0; i < triangles.Count; ++i)
		{
			var triangle = triangles[i];

			meshBuilder.AddIndex(triangle.Wedge0);
			meshBuilder.AddIndex(triangle.Wedge1);
			meshBuilder.AddIndex(triangle.Wedge2);
		}

		Graphics.Material material = new Graphics.Material
		{
			DiffuseColor = Color.White,
			DiffuseTexture = texture,
			SourceTexture = zkMaterial.Texture,
			AlphaTest = IsAlphaTested(zkMaterial)
		};

		var result = meshBuilder.CreateMeshPart(device, true);

		// A composed mesh is baked from its own bind, but the skeleton poses it at the merged
		// rest, which is where it is culled and where a debug box should appear.
		if (skinnedBuilder?.RestBounds is BoundingBox rest)
		{
			result.BoundingBox = rest;
		}

		// Static geometry is what collides. A skinned part deforms with its skeleton and has
		// no single static shape, which matches the reference: only rigid attachments are
		// added to the physics world.
		var friction = 0f;
		var sensor = false;
		if (DebugSettings.CollisionEnabled && zkSkinnedMesh == null && !zkMaterial.DisableCollision)
		{
			var group = zkMaterial.Group;
			friction = Physics.PhysicsMaterials.GetFriction(group);
			sensor = group == global::ZenKit.MaterialGroup.Water;
		}

		result.Tag = new DrMeshPartTag(material)
		{
			Friction = friction,
			Sensor = sensor,
			Collision = friction > 0f ? new CollisionMesh(meshBuilder.CreateCollisionTriangles()) : null
		};

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
	/// The transform used here is the bind the mesh was authored in, which for a composed
	/// model may not be the model's own skeleton: an armour mesh keeps its authored rest and
	/// is carried into the shared pose by its inverse bind. The box, on the other hand, is
	/// computed at the merged rest via <paramref name="restTransforms"/>, because that is
	/// where the skeleton places the mesh.
	/// </para>
	/// <para>
	/// Weights address hierarchy node indices, while the blend indices handed to the GPU
	/// address the skin's joint array, so <paramref name="nodeToBone"/> and
	/// <paramref name="skinMap"/> translate between the three.
	/// </para>
	/// </summary>
	private static MeshBuilderS CreateSkinnedBuilder(ISoftSkinMesh zkSkinnedMesh, IMultiResolutionSubMesh zkSubMesh, Matrix[] bindTransforms, Matrix[] restTransforms, int[] nodeToBone, Dictionary<int, int> skinMap)
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
				var boneIndex = nodeToBone[item.NodeIndex];

				// One weight is enough for the position: they all agree. Blending them would
				// only add rounding error.
				if (j == 0)
				{
					position = Vector3.Transform(item.Position.ToXna(), bindTransforms[item.NodeIndex]);

					var rest = Vector3.Transform(item.Position.ToXna(), restTransforms[boneIndex]);
					var vertexBounds = new BoundingBox(rest, rest);
					mb.RestBounds = mb.RestBounds.HasValue
						? BoundingBox.CreateMerged(mb.RestBounds.Value, vertexBounds)
						: vertexBounds;
				}

				switch (j)
				{
					case 0:
						blendIndices.X = skinMap[boneIndex];
						blendWeights.X = item.Weight;
						break;
					case 1:
						blendIndices.Y = skinMap[boneIndex];
						blendWeights.Y = item.Weight;
						break;
					case 2:
						blendIndices.Z = skinMap[boneIndex];
						blendWeights.Z = item.Weight;
						break;
					default:
						blendIndices.W = skinMap[boneIndex];
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

	private DrMesh CreateMesh(GraphicsDevice device, ISoftSkinMesh zkSkinnedMesh, IMultiResolutionMesh zkMesh, Matrix[] bindTransforms = null, Matrix[] restTransforms = null, int[] nodeToBone = null, Dictionary<int, int> skinMap = null)
	{
		var mesh = new DrMesh();
		foreach (var submesh in zkMesh.SubMeshes)
		{
			var meshPart = CreatePart(device, zkSkinnedMesh, zkMesh, submesh, bindTransforms, restTransforms, nodeToBone, skinMap);
			if (meshPart != null)
			{
				mesh.MeshParts.Add(meshPart);
			}
		}

		return mesh;
	}

	/// <summary>
	/// A skinned mesh plus the merged bones it deforms across, in blend-index order, and the
	/// node each joint was authored as in its own hierarchy.
	/// </summary>
	private sealed class SkinnedMeshInfo
	{
		public DrMesh Mesh { get; }
		public IReadOnlyList<int> JointBones { get; }
		public IReadOnlyList<int> JointNodes { get; }
		public Matrix[] BindTransforms { get; }

		public SkinnedMeshInfo(DrMesh mesh, IReadOnlyList<int> jointBones, IReadOnlyList<int> jointNodes, Matrix[] bindTransforms)
		{
			Mesh = mesh;
			JointBones = jointBones;
			JointNodes = jointNodes;
			BindTransforms = bindTransforms;
		}
	}

	/// <summary>
	/// One mesh in a composed model and the hierarchy its weight node indices address. A bare
	/// <c>.MDM</c> is authored against the shared skeleton, so it has no hierarchy of its own.
	/// </summary>
	private sealed class ModelMeshSource
	{
		public string Name { get; }
		public IModelMesh Mesh { get; }
		public IModelHierarchy Hierarchy { get; }

		/// <summary>
		/// The native model that owns <see cref="Mesh"/> and <see cref="Hierarchy"/>. Both are
		/// borrowed ZenKit handles, so the owner must outlive every read of them.
		/// </summary>
		public object Owner { get; }

		public ModelMeshSource(string name, IModelMesh mesh, IModelHierarchy hierarchy, object owner = null)
		{
			Name = name;
			Mesh = mesh;
			Hierarchy = hierarchy;
			Owner = owner;
		}
	}

	/// <summary>
	/// Assembles a <see cref="DrModel"/> from a shared skeleton and one or more meshes.
	/// <para>
	/// A static mesh is attached to the hierarchy node that carries its name, so the node's
	/// transform positions it. A skinned mesh cannot be: it deforms across many bones, and
	/// one bone owns at most one mesh, so each skinned mesh gets a synthetic bone below a
	/// synthetic root and the real skeleton hangs beside them. The skin still references
	/// the real bones, which is what makes the deformation follow the skeleton.
	/// </para>
	/// <para>
	/// A composed mesh may have been authored against a hierarchy of its own. Its nodes are
	/// mapped to the merged bones by name, and its skin keeps the authored bind; the
	/// skeleton's rest is the explicit hierarchy's, so the mesh is carried from one rest into
	/// the other exactly as an animation would carry it.
	/// </para>
	/// </summary>
	private DrModel LoadModel(GraphicsDevice device, IModelHierarchy zkHierarchy, IReadOnlyList<ModelMeshSource> sources)
	{
		// Static meshes are named after the hierarchy node they belong to.
		var staticMeshes = new Dictionary<string, DrMesh>(StringComparer.OrdinalIgnoreCase);
		foreach (var source in sources)
		{
			foreach (var pair in source.Mesh.Attachments)
			{
				staticMeshes[pair.Key] = CreateMesh(device, null, pair.Value);
			}
		}

		// One bone per hierarchy node, in hierarchy order, so a weight's node index
		// addresses this array directly. The nodes are ordered parents-first, so both the
		// parenting and the bind transforms below can be resolved in a single pass.
		var nodes = zkHierarchy.Nodes;
		var bones = new DrModelBone[nodes.Count];
		var parents = new int[nodes.Count];
		var boneIndexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		for (var i = 0; i < nodes.Count; ++i)
		{
			var node = nodes[i];

			DrMesh mesh;
			if (!staticMeshes.TryGetValue(node.Name, out mesh))
			{
				mesh = null;
			}

			var bone = new DrModelBone(node.Name, mesh);
			var defaultPose = new SrtTransform(node.Transform.ToXna());

			// The hierarchy's root translation is part of the bind: the reference adds it to
			// every root node when it builds the skeleton, and animation samples store the
			// same absolute root position. Leaving it out would make every clip shift the
			// mesh by rootTranslation.
			if (node.ParentIndex < 0)
			{
				defaultPose.Translation += zkHierarchy.RootTranslation.ToXna();
			}

			bone.DefaultPose = defaultPose;

			bones[i] = bone;
			parents[i] = node.ParentIndex;
			boneIndexByName.TryAdd(node.Name, i);
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

		// Absolute rest transforms of the shared skeleton, in hierarchy order. Computed here
		// rather than through DrModel, whose bone numbering is a depth-first traversal order
		// and therefore does not match the hierarchy order the weights use.
		var restTransforms = new Matrix[bones.Length];
		for (var i = 0; i < bones.Length; ++i)
		{
			var local = bones[i].CalculateDefaultLocalTransform();
			restTransforms[i] = parents[i] < 0 ? local : local * restTransforms[parents[i]];
		}

		var skinnedMeshes = new List<SkinnedMeshInfo>();
		var rootChildren = new List<DrModelBone>();
		var meshIndex = 0;

		foreach (var source in sources)
		{
			var nodeToBone = BuildNodeToBone(source, bones.Length, boneIndexByName);
			var bindTransforms = source.Hierarchy == null
				? restTransforms
				: ComputeBindTransforms(source.Hierarchy);

			for (var i = 0; i < source.Mesh.Meshes.Count; ++i)
			{
				var zkSkinnedMesh = source.Mesh.Meshes[i];

				// Every node a weight mentions must be a joint, so the skin's joint array is
				// built from the mesh's node list first and any remaining weight nodes after.
				var jointBones = new List<int>();
				var jointNodes = new List<int>();
				var blendIndexByBone = new Dictionary<int, int>();

				int MapNode(int nodeIndex)
				{
					if (nodeIndex < 0 || nodeIndex >= nodeToBone.Length)
					{
						throw new InvalidOperationException(
							$"Mesh '{source.Name}' addresses node {nodeIndex}, but the shared skeleton has {nodeToBone.Length} nodes.");
					}

					return nodeToBone[nodeIndex];
				}

				void AddJoint(int nodeIndex)
				{
					var boneIndex = MapNode(nodeIndex);
					if (blendIndexByBone.TryAdd(boneIndex, jointBones.Count))
					{
						jointBones.Add(boneIndex);
						jointNodes.Add(nodeIndex);
					}
				}

				foreach (var nodeIndex in zkSkinnedMesh.Nodes)
				{
					AddJoint(nodeIndex);
				}

				foreach (var vertexWeights in zkSkinnedMesh.Weights)
				{
					foreach (var weight in vertexWeights)
					{
						AddJoint(weight.NodeIndex);
					}
				}

				if (jointBones.Count > SkinnedEffect.MaxBones)
				{
					throw new NotSupportedException(
						$"Skinned mesh {i} of '{source.Name}' has {jointBones.Count} joints, but SkinnedEffect supports {SkinnedEffect.MaxBones}.");
				}

				var mesh = CreateMesh(device, zkSkinnedMesh, zkSkinnedMesh.Mesh, bindTransforms, restTransforms, nodeToBone, blendIndexByBone);

				skinnedMeshes.Add(new SkinnedMeshInfo(mesh, jointBones, jointNodes, bindTransforms));

				rootChildren.Add(new DrModelBone($"_MESH{meshIndex++}", mesh));
			}
		}

		var root = bones[0];
		if (rootChildren.Count > 0)
		{
			root = new DrModelBone("_ROOT");
			rootChildren.Add(bones[0]);
			root.Children = rootChildren.ToArray();
		}

		var result = new DrModel(root);
		result.Tag = new DrModelTag { RootTranslation = zkHierarchy.RootTranslation.ToXna() };

		// Now that the parts exist, give each skinned mesh its skin. The GPU is handed
		// inverseBind * boneWorld, so this must be the inverse of the pose the mesh was
		// authored in � the shared skeleton's for a bare mesh, its own otherwise.
		for (var i = 0; i < skinnedMeshes.Count; ++i)
		{
			var info = skinnedMeshes[i];

			var joints = new DrSkinJoint[info.JointBones.Count];
			for (var j = 0; j < joints.Length; ++j)
			{
				var nodeIndex = info.JointNodes[j];
				joints[j] = new DrSkinJoint(bones[info.JointBones[j]], Matrix.Invert(info.BindTransforms[nodeIndex]));
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
	/// Maps a mesh's own hierarchy node indices to merged bone indices, by name. A mesh
	/// without a hierarchy already addresses the shared skeleton, so its map is the identity.
	/// </summary>
	private static int[] BuildNodeToBone(ModelMeshSource source, int boneCount, Dictionary<string, int> boneIndexByName)
	{
		if (source.Hierarchy == null)
		{
			var identity = new int[boneCount];
			for (var i = 0; i < identity.Length; ++i)
			{
				identity[i] = i;
			}

			return identity;
		}

		var nodes = source.Hierarchy.Nodes;
		var result = new int[nodes.Count];
		for (var i = 0; i < nodes.Count; ++i)
		{
			if (!boneIndexByName.TryGetValue(nodes[i].Name, out result[i]))
			{
				throw new InvalidOperationException(
					$"Mesh '{source.Name}' uses bone '{nodes[i].Name}', which the composed skeleton does not have.");
			}
		}

		return result;
	}

	/// <summary>Absolute bind transforms of a hierarchy, in its own node order.</summary>
	private static Matrix[] ComputeBindTransforms(IModelHierarchy hierarchy)
	{
		var nodes = hierarchy.Nodes;
		var result = new Matrix[nodes.Count];
		var rootTranslation = hierarchy.RootTranslation.ToXna();

		for (var i = 0; i < nodes.Count; ++i)
		{
			var pose = new SrtTransform(nodes[i].Transform.ToXna());
			if (nodes[i].ParentIndex < 0)
			{
				pose.Translation += rootTranslation;
			}

			var local = pose.ToMatrix();
			result[i] = nodes[i].ParentIndex < 0 ? local : local * result[nodes[i].ParentIndex];
		}

		return result;
	}

	/// <summary>
	/// Loads a model by name. A self-contained <c>.MDL</c> carries its own hierarchy and
	/// mesh and is preferred; otherwise the skeleton <c>.MDH</c> and mesh <c>.MDM</c> pair
	/// is used. A bare <c>.MDM</c> has no skeleton of its own and is only loadable as part of
	/// a composed model. Names may be given with or without an extension.
	/// </summary>
	private DrModel LoadModel(GraphicsDevice device, string name)
	{
		var source = LoadModelSource(name);
		if (source.Hierarchy == null)
		{
			throw new InvalidOperationException($"Model '{source.Name}' has neither a .MDL nor a .MDH/.MDM pair.");
		}

		var model = LoadModel(device, source.Hierarchy, new[] { source });
		AttachAnimationsQuietly(model, source.Name);

		return model;
	}

	/// <summary>
	/// Builds one model from an explicit skeleton and meshes that may each have their own.
	/// This is how an actor is assembled: the body ships as a bare <c>.MDM</c>, the armour as
	/// a <c>.MDL</c>, and only the skeleton (<c>HUMANS.MDH</c>) carries the <c>ZS_*</c> attach
	/// nodes. Node indices are reconciled by name, the same mapping clip channels use.
	/// </summary>
	private DrModel LoadComposedModel(GraphicsDevice device, string hierarchyStem, string[] meshStems)
	{
		var hierarchyName = Path.GetFileNameWithoutExtension(hierarchyStem).ToUpper();

		IModelHierarchy hierarchy;
		object hierarchyOwner = null;
		var mdlName = hierarchyName + ".MDL";
		var mdhName = hierarchyName + ".MDH";

		if (_allRecords.ContainsKey(mdlName))
		{
			var mdl = LoadNativeRecord(GetLastRecord(mdlName).Node, r => new global::ZenKit.Model(r));
			hierarchy = mdl.Hierarchy;
			hierarchyOwner = mdl;
		}
		else if (_allRecords.ContainsKey(mdhName))
		{
			hierarchy = LoadNativeRecord(GetLastRecord(mdhName).Node, r => new ModelHierarchy(r));
		}
		else
		{
			throw new InvalidOperationException($"Composed model skeleton '{hierarchyName}' has neither a .MDL nor a .MDH.");
		}

		var sources = new ModelMeshSource[meshStems.Length];
		for (var i = 0; i < meshStems.Length; ++i)
		{
			// An actor mesh is the bare .MDM/.ASC body or armour, not a self-contained prop
			// .MDL that happens to share the stem.
			sources[i] = LoadModelSource(meshStems[i], preferMdl: false);
		}

		var model = LoadModel(device, hierarchy, sources);
		AttachAnimationsQuietly(model, hierarchyName);
		GC.KeepAlive(hierarchyOwner);

		return model;
	}

	/// <summary>
	/// Resolves one stem to its mesh and the hierarchy that mesh was authored against. A
	/// self-contained <c>.MDL</c> supplies both; an <c>.MDH</c>/<c>.MDM</c> pair supplies the
	/// skeleton separately; a bare <c>.MDM</c> is authored against the shared skeleton.
	/// </summary>
	private ModelMeshSource LoadModelSource(string name, bool preferMdl = true)
	{
		var stem = Path.GetFileNameWithoutExtension(name).ToUpper();

		var mdlName = stem + ".MDL";
		if (preferMdl && _allRecords.ContainsKey(mdlName))
		{
			var mdl = LoadNativeRecord(GetLastRecord(mdlName).Node, r => new global::ZenKit.Model(r));
			return new ModelMeshSource(stem, mdl.Mesh, mdl.Hierarchy, mdl);
		}

		var mdmName = stem + ".MDM";
		if (_allRecords.ContainsKey(mdmName))
		{
			var mdhName = stem + ".MDH";
			IModelHierarchy hierarchy = _allRecords.ContainsKey(mdhName)
				? LoadNativeRecord(GetLastRecord(mdhName).Node, r => new ModelHierarchy(r))
				: null;

			return new ModelMeshSource(stem, LoadNativeRecord(GetLastRecord(mdmName).Node, r => new global::ZenKit.ModelMesh(r)), hierarchy);
		}

		// An actor mesh is normally a bare .MDM, but accept a self-contained .MDL when no
		// mesh file exists.
		if (_allRecords.ContainsKey(mdlName))
		{
			var mdl = LoadNativeRecord(GetLastRecord(mdlName).Node, r => new global::ZenKit.Model(r));
			return new ModelMeshSource(stem, mdl.Mesh, mdl.Hierarchy, mdl);
		}

		throw new InvalidOperationException($"Model '{stem}' has neither a .MDL nor a .MDM.");
	}

	/// <summary>
	/// A model carries no animation of its own: the clips come from the model script that
	/// owns it, so attaching them is a separate step. Props have no script and stay still.
	/// A clip problem must never cost the model itself, so a failure here is only logged.
	/// </summary>
	private void AttachAnimationsQuietly(DrModel model, string stem)
	{
		try
		{
			AttachAnimations(model, stem);
		}
		catch (Exception ex)
		{
			Log.Error($"Unable to attach animations to model '{stem}': {ex.Message}");
		}
	}

	private DrMesh LoadMultiMesh(GraphicsDevice device, string name)
	{
		var record = GetLastRecord(name);
		var zkMesh = LoadNativeRecord(record.Node, r => new MultiResolutionMesh(r));

		return CreateMesh(device, null, zkMesh);
	}

	private DrMesh LoadMorphMesh(GraphicsDevice device, string name)
	{
		var record = GetLastRecord(name);
		var zkMorph = LoadNativeRecord(record.Node, r => new MorphMesh(r));

		return CreateMesh(device, null, zkMorph.Mesh);
	}

	public DrModel GetModel(GraphicsDevice device, string name) => Get(device, name, LoadModel);

	/// <summary>
	/// One model built from <paramref name="hierarchyStem"/> and the listed meshes, cached
	/// under the composition rather than under any one file's name.
	/// </summary>
	public DrModel GetComposedModel(GraphicsDevice device, string hierarchyStem, params string[] meshStems)
	{
		var names = new string[meshStems.Length];
		for (var i = 0; i < meshStems.Length; ++i)
		{
			names[i] = Path.GetFileNameWithoutExtension(meshStems[i]).ToUpper();
		}

		var key = "compose:" + Path.GetFileNameWithoutExtension(hierarchyStem).ToUpper() + ":" + string.Join(",", names);
		return Get(device, key, (d, _) => LoadComposedModel(d, hierarchyStem, meshStems));
	}

	public DrMesh GetMultiMesh(GraphicsDevice device, string name) => Get(device, name, LoadMultiMesh);

	/// <summary>
	/// Loads a standalone mesh that carries morph animations, such as the human head
	/// <c>.MMB</c>. Only the geometry is returned; the morph targets are not played yet, and
	/// the mesh is authored in the space of the bone it is attached to.
	/// </summary>
	public DrMesh GetMorphMesh(GraphicsDevice device, string name) => Get(device, name, LoadMorphMesh);
}
