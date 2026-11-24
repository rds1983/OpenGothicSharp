using BepuPhysics;
using Microsoft.Xna.Framework;
using OpenGothic.Physics;
using OpenGothic.Vobs;

namespace OpenGothic.Focus;

/// <summary>
/// The vob under the crosshair. The reference selects a cone of candidates and then checks
/// whether the actor can see each one; the port starts from the camera ray, so "under the
/// crosshair" is the primary test and line of sight is a second filter.
/// </summary>
public sealed class FocusTarget
{
	public VobNode Vob { get; private set; }

	/// <summary>Distance along the focus ray, in world units.</summary>
	public float Distance { get; private set; }

	public bool HasValue => Vob != null;

	public string Name
	{
		get
		{
			if (Vob == null)
			{
				return string.Empty;
			}

			if (Vob is ItemVob item && !string.IsNullOrEmpty(item.ItemInstance))
			{
				return item.ItemInstance;
			}

			return string.IsNullOrEmpty(Vob.Tag) ? Vob.Name : Vob.Tag;
		}
	}

	public VobKind Kind => Vob?.Kind ?? VobKind.Unknown;

	public void Clear()
	{
		Vob = null;
		Distance = 0f;
	}

	public void Set(VobNode vob, float distance)
	{
		Vob = vob;
		Distance = distance;
	}

	/// <summary>Short label for the overlay and interaction log.</summary>
	public string Describe()
	{
		if (!HasValue)
		{
			return "none";
		}

		var name = Name;
		if (Vob is NpcVob npc)
		{
			return string.IsNullOrEmpty(name)
				? $"{Kind} hp {npc.Hp}/{npc.MaxHp} ({Distance:0.#})"
				: $"{Kind} '{name}' hp {npc.Hp}/{npc.MaxHp} ({Distance:0.#})";
		}

		var state = Vob is InteractiveVob mob && mob.IsStateAnimated
			? $" state {mob.State}/{mob.StateCount}"
			: string.Empty;
		return string.IsNullOrEmpty(name)
			? $"{Kind}{state} ({Distance:0.#})"
			: $"{Kind} '{name}'{state} ({Distance:0.#})";
	}
}

/// <summary>
/// Finds the nearest interactable vob along a ray. Candidate vobs are already in the typed
/// tree; the test is a world-box hit followed by the visual's part boxes, with a physics ray
/// rejecting targets behind walls. All state is reused, so a per-frame update does not
/// allocate.
/// </summary>
public sealed class FocusSystem
{
	/// <summary>Gothic's interactive focus range is around two metres.</summary>
	public const float Reach = 200f;

	/// <summary>
	/// A target is rejected only when something solid is this much closer than it. The target's
	/// own collider lies on its box, and without the slack it would occlude itself.
	/// </summary>
	private const float OcclusionSlack = 20f;

	public FocusTarget Target { get; } = new FocusTarget();

	public void Find(WorldGrid world, PhysicsWorld physics, Ray ray, float reach = Reach)
	{
		Target.Clear();
		if (world == null)
		{
			return;
		}

		var bestDistance = reach;
		VobNode bestVob = null;

		var vobs = world.Vobs.All;
		for (var i = 0; i < vobs.Count; ++i)
		{
			var vob = vobs[i];
			if (!IsFocusable(vob.Kind) || (vob is ItemVob item && item.IsTaken))
			{
				continue;
			}

			var boxHit = vob.Bounds.Intersects(ray);
			if (!boxHit.HasValue || boxHit.Value > bestDistance)
			{
				continue;
			}

			var distance = boxHit.Value;
			var visual = vob.SceneNode;
			if (visual != null && visual.RayTest(ray, out var partHit) && partHit < distance)
			{
				distance = partHit;
			}

			if (distance >= bestDistance)
			{
				continue;
			}

			if (physics != null && IsOccluded(physics, ray, distance, (vob as NpcVob)?.PhysicsBody))
			{
				continue;
			}

			bestDistance = distance;
			bestVob = vob;
		}

		if (bestVob != null)
		{
			Target.Set(bestVob, bestDistance);
		}
	}

	private static bool IsOccluded(PhysicsWorld physics, Ray ray, float distance, BodyHandle? ignoreBody)
	{
		if (distance <= OcclusionSlack)
		{
			return false;
		}

		return physics.RayCast(ray.Position, ray.Direction, distance - OcclusionSlack, false, out _,
			ignoreBody);
	}

	private static bool IsFocusable(VobKind kind)
	{
		switch (kind)
		{
			case VobKind.Npc:
			case VobKind.Item:
			case VobKind.Interactive:
			case VobKind.Container:
			case VobKind.Door:
			case VobKind.Bed:
			case VobKind.Fire:
			case VobKind.Ladder:
			case VobKind.Switch:
			case VobKind.Wheel:
				return true;
			default:
				return false;
		}
	}
}
