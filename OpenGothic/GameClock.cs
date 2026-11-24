using System;

namespace OpenGothic;

/// <summary>
/// Minimal game clock for daily routines. <see cref="Hour"/> is hours in [0,24);
/// <see cref="Scale"/> is game minutes per real second.
/// </summary>
public sealed class GameClock
{
	public const float DefaultHour = 10f;
	public const float DefaultScale = 1f;

	public GameClock(float hour = DefaultHour, float scale = DefaultScale)
	{
		Hour = Normalize(hour);
		Scale = Math.Max(0f, scale);
	}

	public float Hour { get; private set; }

	/// <summary>Day counter, starting at 1; incremented when the clock wraps past midnight.</summary>
	public int Day { get; private set; } = 1;

	/// <summary>Game minutes elapsed per real second.</summary>
	public float Scale { get; }

	public void SetHour(float hour)
	{
		Hour = Normalize(hour);
	}

	public void Advance(TimeSpan elapsed)
	{
		if (Scale <= 0f)
		{
			return;
		}

		var next = Normalize(Hour + (float)elapsed.TotalSeconds * Scale / 60f);
		if (next < Hour)
		{
			++Day;
		}

		Hour = next;
	}

	public static float Normalize(float hour)
	{
		return ((hour % 24f) + 24f) % 24f;
	}
}
