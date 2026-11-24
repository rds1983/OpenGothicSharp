using OpenGothic.Interactions;
using Xunit;

namespace OpenGothic.Tests;

public class InteractionSystemTests
{
	[Theory]
	[InlineData(0, 1, 1)]
	[InlineData(1, 1, 0)]
	[InlineData(0, 2, 1)]
	[InlineData(1, 2, 2)]
	[InlineData(2, 2, 0)]
	public void NextState_CyclesTheAuthoredStates(int state, int stateCount, int expected)
	{
		Assert.Equal(expected, InteractionSystem.NextState(state, stateCount));
	}

	[Fact]
	public void NextState_TreatsAMissingStateCountAsTwoStates()
	{
		Assert.Equal(1, InteractionSystem.NextState(0, 0));
		Assert.Equal(0, InteractionSystem.NextState(1, 0));
	}
}
