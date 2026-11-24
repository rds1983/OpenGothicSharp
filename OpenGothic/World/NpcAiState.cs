namespace OpenGothic;

/// <summary>
/// One running script AI state. The reference builds this from the state function's symbol:
/// the entry function itself plus optional <c>&lt;name&gt;_Loop</c> and <c>&lt;name&gt;_End</c>
/// siblings (<c>AiState</c> in <c>game/aistate.cpp</c>). The entry runs once when the state
/// starts, the loop on a fixed interval, and the end function when it is replaced.
/// </summary>
public sealed class NpcAiState
{
	/// <summary>Reference <c>LOOP_CONTINUE</c>: the state stays active.</summary>
	public const int LoopContinue = 0;

	/// <summary>Reference <c>LOOP_END</c>: the state finishes.</summary>
	public const int LoopEnd = 1;

	public int Function { get; set; }

	/// <summary>Index of the <c>_Loop</c> sibling, or -1 when the state has none.</summary>
	public int LoopFunction { get; set; } = -1;

	/// <summary>Index of the <c>_End</c> sibling, or -1 when the state has none.</summary>
	public int EndFunction { get; set; } = -1;

	public string Name { get; set; } = string.Empty;

	/// <summary>AI-system time, in seconds, at which the state started.</summary>
	public double StartedAt { get; set; }

	/// <summary>AI-system time at which the loop should run again.</summary>
	public double NextLoopAt { get; set; }

	/// <summary>
	/// Whether the loop is due at <paramref name="time"/>, scheduling the next call when it is.
	/// A state without a loop function never ticks, like the reference's ini-only states.
	/// </summary>
	public bool TryRunLoop(double time, double interval)
	{
		if (LoopFunction < 0 || time < NextLoopAt)
		{
			return false;
		}

		NextLoopAt = time + interval;
		return true;
	}

	/// <summary>The reference finishes a state when its loop returns anything but LOOP_CONTINUE.</summary>
	public static bool ShouldEnd(int loopResult)
	{
		return loopResult != LoopContinue;
	}
}
