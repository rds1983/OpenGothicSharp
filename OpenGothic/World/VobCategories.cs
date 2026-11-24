using DigitalRiseModel;
using Microsoft.Xna.Framework;
using OpenGothic.Graphics;
using System;
using ZenKit.Vobs;

namespace OpenGothic.Vobs;

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

	public int Guild { get; }

	public int Level { get; }

	public int MaxHp { get; }

	public int Hp { get; internal set; }

	public bool IsDead => Hp <= 0;

	/// <summary>Last line passed to <see cref="Say"/>; the script voice/bubble hooks arrive with G3.</summary>
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
