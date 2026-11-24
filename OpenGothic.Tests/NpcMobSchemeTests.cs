using Xunit;

namespace OpenGothic.Tests;

public class NpcMobSchemeTests
{
	[Theory]
	[InlineData("CHAIR_NW_NORMAL_01.ASC", "CHAIR")]
	[InlineData("BEDHIGH_NW_NORMAL_01.ASC", "BEDHIGH")]
	[InlineData("BENCH_3_OC.ASC", "BENCH")]
	[InlineData("CHAIR", "CHAIR")]
	[InlineData("chair_nw_normal_01.asc", "CHAIR")]
	public void SchemeMatchesItsVisualPrefix(string visualName, string scheme)
	{
		Assert.True(NpcAiSystem.MatchesMobScheme(visualName, scheme));
	}

	[Theory]
	[InlineData("CHAIRMAN.ASC", "CHAIR")]
	[InlineData("BENCH_3_OC.ASC", "BENCHHIGH")]
	[InlineData("", "CHAIR")]
	[InlineData("CHAIR_NW_NORMAL_01.ASC", "")]
	public void UnrelatedNamesDoNotMatch(string visualName, string scheme)
	{
		Assert.False(NpcAiSystem.MatchesMobScheme(visualName, scheme));
	}
}
