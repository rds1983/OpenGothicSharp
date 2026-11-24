using Xunit;

namespace OpenGothic.Tests;

public class NpcProcessPolicyTests
{
	[Theory]
	[InlineData(0f)]
	[InlineData(1000f * 1000f)]
	[InlineData(3000f * 3000f)]
	public void WithinNormalRadius_TicksFully(float distanceSquared)
	{
		Assert.Equal(NpcProcessPolicy.AiNormal, NpcAiSystem.PolicyFor(distanceSquared));
	}

	[Theory]
	[InlineData(3000f * 3000f + 1f)]
	[InlineData(5000f * 5000f)]
	[InlineData(6000f * 6000f)]
	public void BetweenNormalAndFar_KeepsSlowLoops(float distanceSquared)
	{
		Assert.Equal(NpcProcessPolicy.AiFar, NpcAiSystem.PolicyFor(distanceSquared));
	}

	[Theory]
	[InlineData(6000f * 6000f + 4f)]
	[InlineData(10000f * 10000f)]
	public void BeyondFarRadius_IsDormant(float distanceSquared)
	{
		Assert.Equal(NpcProcessPolicy.AiFar2, NpcAiSystem.PolicyFor(distanceSquared));
	}
}
