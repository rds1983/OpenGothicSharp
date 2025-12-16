namespace OpenGothic;

public static class Constants
{
	public const int GridSize = 8;
	public const float MaxDistance = 20000.0f;
	public const float MaxShadowDistance = MaxDistance / 2.0f;
	public const float FogStart = MaxShadowDistance;
	public const float FogEnd = MaxDistance;
}
