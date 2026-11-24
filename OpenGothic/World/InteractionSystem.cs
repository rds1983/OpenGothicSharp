using OpenGothic.Graphics;
using OpenGothic.Vobs;
using System;

namespace OpenGothic.Interactions;

/// <summary>
/// Opens, closes and toggles mobsi vobs. The reference drives both the player and the mob
/// animation and waits for the attach animation before committing a state; the port keeps the
/// mob's own state machine now, so a door or chest animates and round-trips state. The player
/// interaction animation, inventory use items, sounds and script calls arrive with later
/// milestones; their authored names are logged as pending hooks.
/// </summary>
public sealed class InteractionSystem
{
	/// <summary>
	/// Raised after a mob's state changes, with the new state. Audio and the Daedalus event
	/// pipeline subscribe here in J1/G2; the current build only logs the authored names.
	/// </summary>
	public event Action<InteractiveVob, int> StateChanged;

	/// <summary>
	/// Next state in the authored cycle. State 0 is closed, the ZEN state count is the highest
	/// state, and a closed state follows an open one.
	/// </summary>
	public static int NextState(int state, int stateCount)
	{
		var count = Math.Max(1, stateCount);
		return state >= count ? 0 : state + 1;
	}

	/// <summary>
	/// Resolves one use action on <paramref name="vob"/>. The returned text is the outcome the
	/// caller logs; the state changes only when an action actually happens.
	/// </summary>
	public string Interact(VobNode vob)
	{
		if (vob is not InteractiveVob mob)
		{
			return "not interactive";
		}

		if (mob.IsBed)
		{
			return "beds are not implemented";
		}

		if (mob.Kind is not (VobKind.Door or VobKind.Container or VobKind.Switch or VobKind.Wheel))
		{
			return "not implemented";
		}

		if (mob.IsLocked)
		{
			return "locked";
		}

		if (!string.IsNullOrEmpty(mob.UseWithItem))
		{
			return $"requires '{mob.UseWithItem}'";
		}

		var model = mob.SceneNode as ModelNode;
		var animation = model?.AnimationData;
		if (animation?.CurrentClip != null &&
		    animation.CurrentClip.StartsWith("T_", StringComparison.OrdinalIgnoreCase))
		{
			return "busy";
		}

		var from = mob.State;
		var to = NextState(from, mob.StateCount);
		var played = false;

		if (animation != null)
		{
			// The transition's Next chain lands on the stable S_S<n> loop. The state clip is
			// the fallback for a vob that has no transition authored.
			played = animation.Play($"T_S{from}_2_S{to}", blend: false) ||
			         animation.Play($"S_S{to}", blend: false);
		}

		mob.State = to;
		StateChanged?.Invoke(mob, to);

		var verb = to > from ? "opened" : "closed";
		var text = $"{verb} (state {from}->{to})";
		if (!played)
		{
			text += "; no animation";
		}

		if (mob.Kind == VobKind.Container && to > from)
		{
			text += $"; {mob.ItemCount} items";
		}

		if (!string.IsNullOrEmpty(mob.OnStateChangeFunction))
		{
			text += $"; script '{mob.OnStateChangeFunction}' pending";
		}

		if (!string.IsNullOrEmpty(mob.Target))
		{
			text += $"; target '{mob.Target}' pending";
		}

		return text;
	}
}
