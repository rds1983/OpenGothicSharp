using OpenGothic;
using Xunit;

namespace OpenGothic.Tests;

public class AssetsNpcTests
{
	[Theory]
	[InlineData("HUM_BODY_NAKED_V0_C0.TGA", 'V', 1, "HUM_BODY_NAKED_V1_C0.TGA")]
	[InlineData("HUM_HEAD_V0_C0.TGA", 'V', 111, "HUM_HEAD_V111_C0.TGA")]
	[InlineData("HUM_HEAD_V0_C0.TGA", 'C', 2, "HUM_HEAD_V0_C2.TGA")]
	[InlineData("HUM_TEETH_V0.TGA", 'V', 7, "HUM_TEETH_V7.TGA")]
	public void ReplaceTag_RewritesTheAuthoredZero(string source, char tag, int value, string expected)
	{
		Assert.Equal(expected, Assets.ReplaceTag(source, tag, value));
	}

	[Fact]
	public void ReplaceTag_LeavesANonZeroTagAlone()
	{
		// The reference only replaces the literal zero after `_V`/`_C`; an authored
		// non-zero variant is not a template.
		Assert.Equal("HUM_HEAD_V1_C0.TGA", Assets.ReplaceTag("HUM_HEAD_V1_C0.TGA", 'V', 111));
	}

	[Fact]
	public void ReplaceTag_LeavesNamesWithoutTheTagAlone()
	{
		Assert.Equal("HUM_HEAD.TGA", Assets.ReplaceTag("HUM_HEAD.TGA", 'V', 3));
	}

	[Fact]
	public void ReplaceTag_ReplacesOnlyTheFirstOccurrence()
	{
		Assert.Equal("X_V1_Y_V0.TGA", Assets.ReplaceTag("X_V0_Y_V0.TGA", 'V', 1));
	}
}
