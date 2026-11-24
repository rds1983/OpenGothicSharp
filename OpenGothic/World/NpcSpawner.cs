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

	private static readonly HashSet<string> MissingClips =
		new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public static List<NpcActor> Spawn(ScriptRuntime scripts, Assets assets, GraphicsDevice device,
		WorldGrid world, PhysicsWorld physics, float hour)
	{
		var actors = new List<NpcActor>();
		if (scripts == null || assets == null || world == null)
		{
			return actors;
		}

		var placer = new WaypointPlacer(world);
		var humans = 0;
		var pending = 0;
		var skipped = 0;
		var routineLogs = 0;

		foreach (var binding in scripts.Npcs)
		{
			var vob = binding.Vob;
			var visual = vob.Visual;
			var routine = NpcRoutine.Select(vob.Routines, hour,
				name => world.Waypoints.ContainsKey(name));
			var placementName = routine?.Waypoint;
			if (string.IsNullOrEmpty(placementName) || !world.Waypoints.ContainsKey(placementName))
			{
				placementName = binding.SpawnPoint;
			}

			var waypoint = placer.Resolve(placementName, out var waypointName);
			var position = Ground(world, waypoint.Position);
			var yaw = YawFromDirection(waypoint.Direction);
			var scale = visual.Scale == Vector3.Zero ? Vector3.One : visual.Scale;
			var human = IsHumanBody(visual, Stem(visual.Body));

			var actor = new NpcActor(vob, binding.Instance, binding.SymbolIndex, visual, position,
				yaw, scale, human);
			vob.World = Matrix.CreateScale(scale) *
			            Matrix.CreateRotationY(-yaw) *
			            Matrix.CreateTranslation(position);
			vob.Local = vob.World;

			if (physics != null)
			{
				vob.PhysicsBody = physics.AddNpcCapsule(position);
			}

			if (routine != null && routineLogs++ < 5)
			{
				Log.Info(
					$"NPC routine '{vob.Name}': starts at '{waypointName}' for " +
					$"'{routine.Waypoint}' ({routine.Start:00.00}-{routine.End:00.00}).");
			}

			if (human)
			{
				if (TryBuildModel(actor, assets, device, world))
				{
					actors.Add(actor);
					++humans;

					if (humans <= 5)
					{
						Log.Info(
							$"NPC spawned '{vob.Name}' ({vob.NpcInstance}) at '{waypointName}', " +
							$"body '{CompositionKey(visual)}'.");
					}
				}
				else
				{
					++skipped;
				}
			}
			else
			{
				actors.Add(actor);
				++pending;

				if (pending <= 5)
				{
					Log.Info(
						$"NPC pending '{vob.Name}' ({vob.NpcInstance}) at '{waypointName}' " +
						$"({position.X:0.#}, {position.Y:0.#}, {position.Z:0.#}), body " +
						$"'{CompositionKey(visual)}'.");
				}
			}
		}

		Log.Info(
			$"NPC spawn: {humans} human bodies, {pending} monster bodies pending, {skipped} " +
			$"skipped, {scripts.InsertedNpcCount} script NPCs.");
		return actors;
	}

	/// <summary>
	/// Builds a pending actor's body and places it in the world. Monster models are built on
	/// demand because their animation sets are large; the caller limits this to nearby actors.
	/// </summary>
	internal static bool TryBuildModel(NpcActor actor, Assets assets, GraphicsDevice device,
		WorldGrid world)
	{
		if (actor == null || actor.HasModel || actor.ModelFailed)
		{
			return actor?.HasModel == true;
		}

		var vob = actor.Vob;
		var visual = actor.Visual;

		DrModel model;
		try
		{
			model = BuildBody(assets, device, visual);
		}
		catch (Exception ex)
		{
			Log.Warning($"Unable to build the body of '{vob.Name}': {ex.Message}");
			actor.ModelFailed = true;
			return false;
		}

		if (model == null)
		{
			actor.ModelFailed = true;
			return false;
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
		if (animation.Play(actor.IdleClip) ||
		    (!actor.IsHuman && animation.Play("S_RUN")))
		{
			node.AnimationData = animation;
		}
		else if (MissingClips.Add(CompositionKey(visual) + ":" + actor.IdleClip))
		{
			Log.Warning($"NPC body '{CompositionKey(visual)}' has no idle clip '{actor.IdleClip}'.");
		}

		var rootBone = model.FindBoneByName("BIP01") ??
		               (model.Bones.Length > 0 ? model.Bones[0] : null);
		if (rootBone == null)
		{
			Log.Warning($"NPC body '{CompositionKey(visual)}' has no root bone.");
			actor.ModelFailed = true;
			return false;
		}

		BoundingBox bounds = instance.BoundingBox ?? new BoundingBox(actor.Position, actor.Position);
		var defaultRootY = rootBone.DefaultPose.Translation.Y;
		var idleRootY = instance.GetBoneGlobalTransform(rootBone.Index).Translation.Y;
		var feetOffset = -bounds.Min.Y - (idleRootY - defaultRootY);

		actor.AttachModel(node, rootBone.Index, feetOffset, bounds);

		var worldMatrix = node.Transform;
		vob.Bounds = bounds.Transform(ref worldMatrix);

		world.Vobs.Add(vob);
		var cell = world.FindCellByBox(vob.Bounds) ?? world.FindCellByPoint(actor.Position);
		if (cell != null)
		{
			cell.Children.Add(node);
			cell.ObjectBounds.Add(vob.Bounds);
			vob.SceneNode = node;
			node.Vob = vob;
		}
		else
		{
			Log.Warning(
				$"NPC '{vob.Name}' at ({actor.Position.X:0.#}, {actor.Position.Z:0.#}) is outside the grid.");
		}

		return true;
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

		// A monster body is driven by a model script (.MSB/.MDS) that names the mesh and its
		// skeleton. The animation index already maps that mesh to the script, so derive the
		// skeleton from the script instead of a hand-written monster table.
		var script = assets.FindScriptForModel(body);
		if (!string.IsNullOrEmpty(script) &&
		    (assets.HasRecord(script + ".MDH") || assets.HasRecord(script + ".MDL")))
		{
			return assets.GetComposedModel(device, script, body);
		}

		// Last resort: a self-contained .MDL with its own hierarchy.
		if (assets.HasRecord(body + ".MDL"))
		{
			return assets.GetModel(device, body);
		}

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
	/// Stands a spawn point on the floor: waynet points are authored above the ground, which
	/// is what the reference's <c>fixNpcPosition</c> ray-cast solves. A short spiral catches
	/// points that hang over a ledge or sit inside geometry.
	/// </summary>
	private static Vector3 Ground(WorldGrid world, Vector3 position)
	{
		if (world.TryGetGround(position, WorldGrid.GroundSnap, out var ground))
		{
			return ground;
		}

		for (var radius = 40f; radius <= 400f; radius += 40f)
		{
			for (var angle = 0; angle < 360; angle += 45)
			{
				var radians = MathHelper.ToRadians(angle);
				var probe = position + new Vector3(MathF.Cos(radians) * radius, 0f,
					MathF.Sin(radians) * radius);
				if (world.TryGetGround(probe, WorldGrid.GroundSnap, out ground))
				{
					return ground;
				}
			}
		}

		return position;
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
