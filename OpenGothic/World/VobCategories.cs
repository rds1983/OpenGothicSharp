using Microsoft.Xna.Framework;
using ZenKit.Vobs;

namespace OpenGothic.Vobs;

// Thin category types. The concrete VobKind in the base carries the file-level behaviour;
// these classes give later milestones a place for NPC/item/interactive/mobsi state without
// growing VobNode into a union of every type.

public sealed class NpcVob : VobNode
{
	internal NpcVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds) { }
}

public sealed class ItemVob : VobNode
{
	internal ItemVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds) { }
}

public sealed class InteractiveVob : VobNode
{
	internal InteractiveVob(IVirtualObject source, VobNode parent, Matrix world, Matrix local, VobKind kind,
		BoundingBox bounds) : base(source, parent, world, local, kind, bounds) { }
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
