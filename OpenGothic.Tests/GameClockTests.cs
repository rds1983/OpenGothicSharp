using OpenGothic;
using System;
using Xunit;

namespace OpenGothic.Tests;

public class GameClockTests
{
	[Fact]
	public void DefaultsToTenOClock()
	{
		var clock = new GameClock();

		Assert.Equal(10f, clock.Hour);
		Assert.Equal(1f, clock.Scale);
	}

	[Theory]
	[InlineData(24f, 0f)]
	[InlineData(-1f, 23f)]
	[InlineData(25.5f, 1.5f)]
	public void Normalize_WrapsIntoADay(float input, float expected)
	{
		Assert.Equal(expected, GameClock.Normalize(input));
	}

	[Fact]
	public void Advance_MovesOneHourPerRealMinute()
	{
		var clock = new GameClock(10f, 1f);

		clock.Advance(TimeSpan.FromSeconds(60));

		Assert.Equal(11f, clock.Hour);
	}

	[Fact]
	public void Advance_WrapsAtMidnight()
	{
		var clock = new GameClock(23.5f, 1f);

		clock.Advance(TimeSpan.FromSeconds(60));

		Assert.Equal(0.5f, clock.Hour);
		Assert.Equal(2, clock.Day);
	}

	[Fact]
	public void SetHour_DoesNotChangeTheDay()
	{
		var clock = new GameClock(10f, 1f);

		clock.SetHour(2f);

		Assert.Equal(2f, clock.Hour);
		Assert.Equal(1, clock.Day);
	}

	[Fact]
	public void ZeroScale_FreezesTheClock()
	{
		var clock = new GameClock(10f, 0f);

		clock.Advance(TimeSpan.FromHours(1));

		Assert.Equal(10f, clock.Hour);
	}
}
