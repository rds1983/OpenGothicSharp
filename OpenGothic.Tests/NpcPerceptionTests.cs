using Microsoft.Xna.Framework;
using System;
using Xunit;

namespace OpenGothic.Tests;

public class NpcPerceptionTests
{
	private static Vector3 Direction(float x, float z)
	{
		return new Vector3(x, 0f, z);
	}

	[Fact]
	public void TargetInFront_IsInsideTheCone()
	{
		// Yaw 0 faces -Z (see NpcActor.UpdateTransform).
		Assert.True(NpcAiSystem.WithinViewCone(0f, Direction(0f, -1f)));
	}

	[Fact]
	public void TargetBehind_IsOutsideTheCone()
	{
		Assert.False(NpcAiSystem.WithinViewCone(0f, Direction(0f, 1f)));
	}

	[Fact]
	public void TargetAtSeventyDegrees_IsInsideTheCone()
	{
		var angle = MathHelper.ToRadians(70f);

		Assert.True(NpcAiSystem.WithinViewCone(0f,
			Direction(MathF.Sin(angle), -MathF.Cos(angle))));
	}

	[Fact]
	public void TargetAtNinetyDegrees_IsOutsideTheCone()
	{
		Assert.False(NpcAiSystem.WithinViewCone(0f, Direction(1f, 0f)));
	}

	[Fact]
	public void YawRotatesTheCone()
	{
		// Yaw of -90 degrees faces -X.
		var yaw = -MathHelper.PiOver2;

		Assert.True(NpcAiSystem.WithinViewCone(yaw, Direction(-1f, 0f)));
		Assert.False(NpcAiSystem.WithinViewCone(yaw, Direction(1f, 0f)));
	}

	[Fact]
	public void TargetDirectlyBelow_CountsAsVisible()
	{
		Assert.True(NpcAiSystem.WithinViewCone(0f, new Vector3(0f, -100f, 0f)));
	}
}
