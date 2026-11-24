using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGothic.Graphics;
using OpenGothic.Physics;
using OpenGothic.Scripts;
using OpenGothic.Utility;
using OpenGothic.Vobs;
using System;
using System.Collections.Generic;
using System.IO;
using BoundingBox = Microsoft.Xna.Framework.BoundingBox;

namespace OpenGothic;

/// <summary>
/// Turns the data-only NPCs created by <see cref="ScriptRuntime"/> into placed, animated
/// bodies. The composition follows the reference: the body or armour mesh is a bare
/// <c>.MDM</c> authored against <c>HUMANS</c>, the head is a <c>.MMB</c> morph mesh, and the
/// waypoint supplies the feet position and facing. The model is shared between NPCs with the
/// same composition; the texture variant and transform are per instance.
/// </summary>
internal static class NpcSpawner
{
	private const string Skeleton = "HUMANS";

	public static List<NpcActor> Spawn(ScriptRuntime scripts, Assets assets, GraphicsDevice device,
		WorldGrid world, PhysicsWorld physics)
	{
		var actors = new List<NpcActor>();
		if (scripts == null || assets == null || world == null)
		{
			return actors;
		}

		var placer = new WaypointPlacer(world);
		var missingClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var skipped = 0;

		foreach (var binding in scripts.Npcs)
		{
			var vob = binding.Vob;
			var visual = vob.Visual;
			var waypoint = placer.Resolve(binding.SpawnPoint, out var waypointName);
			var position = waypoint.Position;
			var yaw = YawFromDirection(waypoint.Direction);
			var scale = visual.Scale == Vector3.Zero ? Vector3.One : visual.Scale;

			DrModel model;
			try
			{
				model = BuildBody(assets, device, visual);
			}
			catch (Exception ex)
			{
				Log.Warning($"Unable to build the body of '{vob.Name}': {ex.Message}");
				model = null;
			}

			if (model == null)
			{
				++skipped;
				continue;
			}

			var instance = new DrModelInstance(model);
			var node = new ModelNode
			{
				Instance = instance,
				Collision = false
			};

			assets.ApplyMaterialVariants(device, model, visual.BodyTexture, 0, visual.BodyColor,
				node.Materials);
			AttachHead(assets, device, model, node, visual);

			var animation = new AnimationData(instance);
			if (IsHumanBody(visual, Stem(visual.Body)) && animation.Play("S_RUN"))
			{
				node.AnimationData = animation;
			}
			else if (IsHumanBody(visual, Stem(visual.Body)) && missingClips.Add(CompositionKey(visual)))
			{
				Log.Warning($"NPC body '{CompositionKey(visual)}' has no idle clip 'S_RUN'.");
			}

			var rootBone = model.FindBoneByName("BIP01") ??
			               (model.Bones.Length > 0 ? model.Bones[0] : null);
			if (rootBone == null)
			{
				Log.Warning($"NPC body '{CompositionKey(visual)}' has no root bone.");
				++skipped;
				continue;
			}

			BoundingBox bounds = instance.BoundingBox ?? new BoundingBox(position, position);
			var defaultRootY = rootBone.DefaultPose.Translation.Y;
			var idleRootY = instance.GetBoneGlobalTransform(rootBone.Index).Translation.Y;
			var feetOffset = -bounds.Min.Y - (idleRootY - defaultRootY);

			var actor = new NpcActor(vob, node, rootBone.Index, feetOffset, position, yaw, scale);
			actor.UpdateTransform();

			vob.World = Matrix.CreateScale(scale) *
			            Matrix.CreateRotationY(-yaw) *
			            Matrix.CreateTranslation(position);
			vob.Local = vob.World;

			var worldMatrix = actor.Node.Transform;
			vob.Bounds = bounds.Transform(ref worldMatrix);

			world.Vobs.Add(vob);
			var cell = world.FindCellByBox(vob.Bounds) ?? world.FindCellByPoint(position);
			if (cell != null)
			{
				cell.Children.Add(node);
				cell.ObjectBounds.Add(vob.Bounds);
				vob.SceneNode = node;
				node.Vob = vob;
			}
			else
			{
				Log.Warning($"NPC '{vob.Name}' at ({position.X:0.#}, {position.Z:0.#}) is outside the grid.");
			}

			if (physics != null)
			{
				vob.PhysicsBody = physics.AddNpcCapsule(position);
			}

			actors.Add(actor);

			if (actors.Count <= 5)
			{
				Log.Info(
					$"NPC spawned '{vob.Name}' ({vob.NpcInstance}) at '{waypointName}', " +
					$"body '{CompositionKey(visual)}'.");
			}
		}

		Log.Info($"NPC spawn: {actors.Count} visible, {skipped} skipped, {scripts.InsertedNpcCount} script NPCs.");
		return actors;
	}

	private static DrModel BuildBody(Assets assets, GraphicsDevice device, NpcVisual visual)
	{
		var overrideName = Stem(visual.ModelOverride);
		if (!string.IsNullOrEmpty(overrideName) && HasActorMesh(assets, overrideName))
		{
			return assets.GetModel(device, overrideName);
		}

		var armor = Stem(visual.ArmorModel);
		if (!string.IsNullOrEmpty(armor))
		{
			if (HasActorMesh(assets, armor))
			{
				return assets.GetComposedModel(device, Skeleton, armor);
			}

			Log.Warning($"NPC armour mesh '{armor}' is missing; using the naked body.");
		}

		var body = Stem(visual.Body);
		if (string.IsNullOrEmpty(body))
		{
			return null;
		}

		if (!HasActorMesh(assets, body))
		{
			Log.Warning($"NPC body mesh '{body}' is missing.");
			return null;
		}

		if (IsHumanBody(visual, body))
		{
			return assets.GetComposedModel(device, Skeleton, body);
		}

		// Monster bodies need their model-script skeleton and animation set. Composing them
		// against HUMANS deforms them, and loading every monster model at startup is too
		// expensive for the F1.5 visibility step, so they stay script objects until F2/F3.
		return null;
	}

	private static bool IsHumanBody(NpcVisual visual, string body)
	{
		return !string.IsNullOrEmpty(Stem(visual.Head)) ||
		       body.StartsWith("HUM_BODY_", StringComparison.OrdinalIgnoreCase);
	}

	private static void AttachHead(Assets assets, GraphicsDevice device, DrModel model, ModelNode node,
		NpcVisual visual)
	{
		var head = Stem(visual.Head);
		if (string.IsNullOrEmpty(head))
		{
			return;
		}

		var bone = model.FindBoneByName("BIP01 HEAD");
		if (bone == null)
		{
			Log.Warning($"NPC skeleton has no 'BIP01 HEAD'; '{head}' is not attached.");
			return;
		}

		var name = head + ".MMB";
		if (!assets.HasRecord(name))
		{
			Log.Warning($"NPC head mesh '{name}' is missing.");
			return;
		}

		try
		{
			var mesh = assets.GetMorphMesh(device, name);
			var attachment = new ModelBoneAttachment(ModelBoneAttachment.WrapMesh(mesh))
			{
				Bone = bone
			};
			assets.ApplyMaterialVariants(device, mesh, visual.HeadTexture, visual.TeethTexture,
				visual.BodyColor, attachment.Materials);
			node.BonesAttachments.Add(attachment);
		}
		catch (Exception ex)
		{
			Log.Warning($"Unable to attach NPC head '{name}': {ex.Message}");
		}
	}

	private static bool HasActorMesh(Assets assets, string stem)
	{
		return assets.HasRecord(stem + ".MDM") || assets.HasRecord(stem + ".MDL");
	}

	private static string Stem(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return string.Empty;
		}

		return Path.GetFileNameWithoutExtension(name.Trim()).TrimEnd('.');
	}

	private static float YawFromDirection(Vector3 direction)
	{
		if (direction.LengthSquared() < 1e-6f)
		{
			return 0f;
		}

		return MathF.Atan2(direction.X, -direction.Z);
	}

	private static string CompositionKey(NpcVisual visual)
	{
		var armor = Stem(visual.ArmorModel);
		return string.IsNullOrEmpty(armor) ? Stem(visual.Body) : armor;
	}

	/// <summary>
	/// Matches the reference's spawn distribution: use the authored waypoint, then the
	/// nearest unused waypoint within 800 units, exactly like <c>WayMatrix::findNextPoint</c>.
	/// When none is free the NPC stacks on the authored point, as the reference does.
	/// </summary>
	private sealed class WaypointPlacer
	{
		private const float ThresholdSquared = 800f * 800f;

		private readonly WorldGrid _world;
		private readonly List<(string Name, Waypoint Point)> _points = new List<(string, Waypoint)>();
		private readonly HashSet<string> _used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private readonly HashSet<string> _missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public WaypointPlacer(WorldGrid world)
		{
			_world = world;
			foreach (var pair in world.Waypoints)
			{
				_points.Add((pair.Key, pair.Value));
			}
		}

		public Waypoint Resolve(string spawnPoint, out string name)
		{
			if (string.IsNullOrEmpty(spawnPoint) || !_world.Waypoints.TryGetValue(spawnPoint, out var point))
			{
				if (_missing.Add(spawnPoint ?? string.Empty))
				{
					Log.Warning($"NPC spawn point '{spawnPoint}' is missing; using the dead point.");
				}

				return DeadPoint(out name);
			}

			name = spawnPoint;
			if (_used.Contains(name) && TryNearest(point.Position, out var nextName, out var next))
			{
				name = nextName;
				point = next;
			}

			_used.Add(name);
			return point;
		}

		private bool TryNearest(Vector3 at, out string name, out Waypoint point)
		{
			var best = ThresholdSquared;
			name = null;
			point = default;

			foreach (var entry in _points)
			{
				if (_used.Contains(entry.Name))
				{
					continue;
				}

				var distance = Vector3.DistanceSquared(entry.Point.Position, at);
				if (distance >= best)
				{
					continue;
				}

				best = distance;
				name = entry.Name;
				point = entry.Point;
			}

			return name != null;
		}

		private Waypoint DeadPoint(out string name)
		{
			if (_world.Waypoints.TryGetValue("TOT", out var point))
			{
				name = "TOT";
				return point;
			}

			name = "TOT";
			return new Waypoint(new Vector3(-1000f), Vector3.UnitZ, true);
		}
	}
}
