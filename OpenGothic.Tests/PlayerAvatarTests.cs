using Xunit;

namespace OpenGothic.Tests;

public class PlayerAvatarTests
{
	[Theory]
	[InlineData(0f, false, "S_RUN")]
	[InlineData(0f, true, "S_RUN")]
	[InlineData(5f, false, "S_RUN")]
	[InlineData(5f, true, "S_RUN")]
	[InlineData(300f, false, "S_RUNL")]
	[InlineData(300f, true, "S_WALKL")]
	public void SelectClip_FollowsTheReferenceWeaponlessClips(float speed, bool walking, string expected)
	{
		Assert.Equal(expected, PlayerAvatar.SelectClip(speed, walking));
	}
}
