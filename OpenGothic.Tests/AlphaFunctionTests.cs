using OpenGothic.ZenKit;
using Xunit;

namespace OpenGothic.Tests;

public class AlphaFunctionTests
{
	[Theory]
	[InlineData(AlphaFunction.Default)]
	[InlineData(AlphaFunction.None)]
	public void DefaultFunctions_AreCutOut(AlphaFunction alpha)
	{
		Assert.True(Assets.IsAlphaTested(alpha, MaterialGroup.Undefined, 255, true, false));
	}

	[Theory]
	[InlineData(AlphaFunction.Blend)]
	[InlineData(AlphaFunction.Add)]
	[InlineData(AlphaFunction.Subtract)]
	[InlineData(AlphaFunction.Multiply)]
	[InlineData(AlphaFunction.MultiplyAlt)]
	public void OtherModes_BelongToOtherPasses(AlphaFunction alpha)
	{
		Assert.False(Assets.IsAlphaTested(alpha, MaterialGroup.Undefined, 255, true, false));
	}

	[Fact]
	public void Water_IsNeverCutOut()
	{
		Assert.False(Assets.IsAlphaTested(AlphaFunction.None, MaterialGroup.Water, 255, true, false));
	}

	[Fact]
	public void Dxt1_WithOpaqueColour_IsSolid()
	{
		Assert.False(Assets.IsAlphaTested(AlphaFunction.None, MaterialGroup.Undefined, 255, true, true));
	}

	[Fact]
	public void Dxt1_WithTranslucentColour_IsCutOut()
	{
		Assert.True(Assets.IsAlphaTested(AlphaFunction.None, MaterialGroup.Undefined, 128, true, true));
	}

	[Fact]
	public void WithoutTexture_TheColourDecides()
	{
		Assert.False(Assets.IsAlphaTested(AlphaFunction.None, MaterialGroup.Undefined, 255, false, false));
		Assert.True(Assets.IsAlphaTested(AlphaFunction.None, MaterialGroup.Undefined, 128, false, false));
	}
}
