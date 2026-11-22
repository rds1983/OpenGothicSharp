using Microsoft.Xna.Framework;

namespace OpenGothic;

public static class Constants
{
	public const int GridSize = 4;
	public const float MaxDistance = 20000.0f;
	public const float NearPlane = 10.0f;
	public const float FarPlane = MaxDistance;
	public const float MaxShadowDistance = MaxDistance / 2.0f;
	public const float FogStart = MaxShadowDistance;
	public const float FogEnd = MaxDistance;

	public static readonly Color SkyHorizonColor = new Color(150, 190, 230);
	public static readonly Color SkyZenithColor = new Color(60, 110, 190);
}
