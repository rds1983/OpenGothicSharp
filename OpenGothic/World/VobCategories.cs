using BepuPhysics;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using OpenGothic.Graphics;
using System;
using System.Collections.Generic;
using OpenGothic.ZenKit;

namespace OpenGothic.Vobs;

/// <summary>
/// Visual parameters gathered from <c>MDL_SetVisualBody</c>, <c>MDL_SetVisual</c> and
/// <c>MDL_SetModelScale</c>. They are data only until the spawner builds the model.
/// </summary>
public sealed class NpcVisual
{
	public string Body = string.Empty;
	public int BodyTexture;
	public int BodyColor;
	public string Head = string.Empty;
	public int HeadTexture;
	public int TeethTexture;

	/// <summary>Armour item's on-body mesh, e.g. <c>Armor_Mil_L.asc</c>; empty for none.</summary>
	public string ArmorModel = string.Empty;

	/// <summary>Complete model override from <c>MDL_SetVisual</c> (monsters); empty for human.</summary>
	public string ModelOverride = string.Empty;

	public Vector3 Scale = Vector3.One;
}

/// <summary>
/// One entry from the Daedalus <c>TA_MIN</c> daily routine. Times are hours in [0,24);
/// <see cref="End"/> may be lower than <see cref="Start"/> for a range crossing midnight.
/// </summary>
public sealed class NpcRoutine
{
	public float Start;
	public float End;
	public int Action;
	public string Waypoint = string.Empty;

	/// <summary>
	/// Picks the routine active at <paramref name="hour"/>, otherwise the routine whose end is
	/// closest before the current time, like the reference. Routines without a resolvable
	/// waypoint are ignored.
	/// </summary>
	public static NpcRoutine Select(IReadOnlyList<NpcRoutine> routines, float hour,
		Func<string, bool> hasWaypoint)
	{
		if (routines == null || routines.Count == 0)
		{
			return null;
		}

		hour = ((hour % 24f) + 24f) % 24f;

		foreach (var routine in routines)
		{
			if (!IsUsable(routine, hasWaypoint))
			{
				continue;
			}

			if (routine.End < routine.Start)
			{
				if (hour >= routine.Start || hour < routine.End)
				{
					return routine;
				}
			}
			else if (hour >= routine.Start && hour < routine.End)
			{
				return routine;
			}
		}

		NpcRoutine best = null;
		var bestDelta = float.MaxValue;
		foreach (var routine in routines)
		{
			if (!IsUsable(routine, hasWaypoint))
			{
				continue;
			}

			var delta = hour - routine.End;
			if (delta < 0f)
			{
				delta += 24f;
			}

			if (delta <= bestDelta)
			{
				bestDelta = delta;
				best = routine;
			}
		}

		return best;
	}

	private static bool IsUsable(NpcRoutine routine, Func<string, bool> hasWaypoint)
	{
		return routine != null &&
		       !string.IsNullOrEmpty(routine.Waypoint) &&
		       (hasWaypoint == null || hasWaypoint(routine.Waypoint));
	}
}

// Thin category types. The concrete VobKind in the base carries the file-level behaviour;
// these classes give later milestones a place for NPC/item/interactive/mobsi state without
// growing VobNode into a union of every type.

public sealed class NpcVob : VobNode
{
	internal NpcVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds)
	{
		var npc = source as INpc;
		NpcInstance = !string.IsNullOrEmpty(npc?.NpcInstance) ? npc.NpcInstance : Name;
		Guild = npc?.Guild ?? 0;
		Level = Math.Max(1, npc?.Level ?? 1);

		var attributes = npc?.Attributes;
		var maxHp = attributes != null && attributes.Count > 1 && attributes[1] > 0
			? attributes[1]
			: 100;
		MaxHp = maxHp;
		Hp = attributes != null && attributes.Count > 0 && attributes[0] > 0
			? Math.Min(attributes[0], maxHp)
			: maxHp;
	}

	/// <summary>The NPC's script instance, e.g. <c>NPC_DIEGO</c>.</summary>
	public string NpcInstance { get; }

	/// <summary>Body/armour/head parameters collected from the Daedalus visual externals.</summary>
	public NpcVisual Visual { get; internal set; } = new NpcVisual();

	/// <summary>Daily routine entries filled by <c>TA_MIN</c> during instance initialization.</summary>
	public List<NpcRoutine> Routines { get; } = new List<NpcRoutine>();

	/// <summary>Kinematic capsule in the physics world, or null when physics is off.</summary>
	public BodyHandle? PhysicsBody { get; internal set; }

	public int Guild { get; }

	public int Level { get; }

	public int MaxHp { get; }

	public int Hp { get; internal set; }

	public bool IsDead => Hp <= 0;

	/// <summary>Last line passed to <see cref="Say"/>; scripted dialog lines use the dialog session.</summary>
	public string LastMessage { get; private set; }

	public void TakeDamage(int amount)
	{
		if (amount <= 0 || IsDead)
		{
			return;
		}

		Hp = Math.Max(0, Hp - amount);
		Log.Info($"Npc '{Name}' damaged for {amount}: {Hp}/{MaxHp}.");
		if (Hp == 0)
		{
			Log.Info($"Npc '{Name}' died.");
		}
	}

	public void Say(string message)
	{
		LastMessage = message;
		Log.Info($"Npc '{Name}' says: {message}");
	}
}

public sealed class ItemVob : VobNode
{
	internal ItemVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds)
	{
		var item = source as IItem;
		ItemInstance = !string.IsNullOrEmpty(item?.Instance) ? item.Instance : Tag;
		Amount = Math.Max(1, item?.Amount ?? 1);
	}

	/// <summary>The item's script instance, e.g. <c>ItFo_Apple</c>.</summary>
	public string ItemInstance { get; }

	/// <summary>Stack size authored on the vob; at least one.</summary>
	public int Amount { get; }

	/// <summary>Set when the player has picked the item up; the vob stays queryable but is no longer focusable.</summary>
	public bool IsTaken { get; internal set; }
}

/// <summary>
/// A door, chest, switch, wheel or other mobsi. The ZEN authoring stores a state count, a
/// trigger target, an optional use item and an optional state-change function; the visual
/// plays <c>S_S0</c> when closed and <c>T_S0_2_S1</c>/<c>S_S1</c> when opened, as the
/// reference's <c>Interactive</c> does.
/// </summary>
public sealed class InteractiveVob : VobNode
{
	internal InteractiveVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds)
	{
		var interactive = source as IInteractiveObject;
		StateCount = interactive?.State ?? 0;
		OnStateChangeFunction = interactive?.OnStateChangeFunction ?? string.Empty;
		Target = interactive?.Target ?? string.Empty;
		UseWithItem = interactive?.Item ?? string.Empty;
		ItemCount = (source as IContainer)?.ItemCount ?? 0;
		IsLocked = (source as IContainer)?.IsLocked ?? (source as IDoor)?.IsLocked ?? false;

		var visualName = (source.Visual as VisualModel)?.Name ?? string.Empty;
		IsBed = visualName.StartsWith("BED", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>Highest state the authored animation defines; the closed state is always 0.</summary>
	public int StateCount { get; }

	/// <summary>Current authored state; 0 is the default closed state.</summary>
	public int State { get; internal set; }

	public bool IsLocked { get; }

	public bool IsBed { get; }

	/// <summary>Function the script should run when the state changes; empty until G2.</summary>
	public string OnStateChangeFunction { get; }

	/// <summary>Trigger target the authored state changes; empty until the trigger layer.</summary>
	public string Target { get; }

	/// <summary>Item required to use the mob, or empty.</summary>
	public string UseWithItem { get; }

	/// <summary>Number of items in a container, or zero for other mobs.</summary>
	public int ItemCount { get; }

	/// <summary>Whether the vob has state clips and is safe to open/close through them.</summary>
	public bool IsStateAnimated =>
		!IsBed &&
		Kind is VobKind.Door or VobKind.Container or VobKind.Switch or VobKind.Wheel;

	/// <summary>
	/// Puts the model into its closed state before the first frame, so the bind pose is not
	/// mistaken for an open state. A vob whose script has no <c>S_S0</c> is left on its bind
	/// pose, exactly like a model with no clips.
	/// </summary>
	public void PrepareVisual(ModelNode node)
	{
		if (!IsStateAnimated || node?.Instance == null)
		{
			return;
		}

		var animation = new AnimationData(node.Instance);
		if (animation.Play($"S_S{State}", blend: false))
		{
			node.AnimationData = animation;
		}
	}
}

public sealed class TriggerVob : VobNode
{
	internal TriggerVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds) { }
}

public sealed class MoverVob : VobNode
{
	internal MoverVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds) { }
}

public sealed class LightVob : VobNode
{
	internal LightVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds) { }
}

public sealed class SoundVob : VobNode
{
	internal SoundVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds) { }
}

public sealed class EffectVob : VobNode
{
	internal EffectVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds) { }
}

public sealed class StaticVob : VobNode
{
	internal StaticVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds) { }
}

public sealed class ZoneVob : VobNode
{
	internal ZoneVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds) { }
}

public sealed class CameraVob : VobNode
{
	internal CameraVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds) { }
}

public sealed class SpotVob : VobNode
{
	internal SpotVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds) { }
}

public sealed class UnknownVob : VobNode
{
	internal UnknownVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds) { }
}
