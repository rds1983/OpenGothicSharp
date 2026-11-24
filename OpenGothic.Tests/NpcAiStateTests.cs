using Xunit;

namespace OpenGothic.Tests;

public class NpcAiStateTests
{
	[Fact]
	public void StateWithoutLoop_NeverTicks()
	{
		var state = new NpcAiState { NextLoopAt = 0 };

		Assert.False(state.TryRunLoop(10, 0.5));
	}

	[Fact]
	public void Loop_WaitsForItsIntervalAndReschedules()
	{
		var state = new NpcAiState { LoopFunction = 42, NextLoopAt = 1.0 };

		Assert.False(state.TryRunLoop(0.9, 0.5));
		Assert.True(state.TryRunLoop(1.0, 0.5));
		Assert.Equal(1.5, state.NextLoopAt);
		Assert.False(state.TryRunLoop(1.4, 0.5));
		Assert.True(state.TryRunLoop(1.5, 0.5));
	}

	[Theory]
	[InlineData(NpcAiState.LoopContinue, false)]
	[InlineData(NpcAiState.LoopEnd, true)]
	[InlineData(7, true)]
	public void OnlyLoopContinue_KeepsTheState(int result, bool ends)
	{
		Assert.Equal(ends, NpcAiState.ShouldEnd(result));
	}
}
