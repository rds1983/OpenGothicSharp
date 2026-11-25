using OpenGothic.ZenKit;

namespace OpenGothic.Physics;

/// <summary>
/// Friction coefficients by the material group stored on every polygon. The values are the
/// reference client's table, which follows the usual coefficients for these surfaces; the
/// coefficient itself is dimensionless, so the game's centimetre units do not enter it.
/// </summary>
public static class PhysicsMaterials
{
	/// <summary>Friction used when a group has no dedicated coefficient.</summary>
	public const float DefaultFriction = 0.75f;

	public static float GetFriction(MaterialGroup group) => group switch
	{
		MaterialGroup.Undefined => 0.5f,
		MaterialGroup.Metal => 1.1f,
		MaterialGroup.Stone => 0.65f,
		MaterialGroup.Wood => 0.4f,
		MaterialGroup.Earth => 0.4f,
		MaterialGroup.Water => 0.01f,
		MaterialGroup.Snow => 0.2f,
		_ => DefaultFriction,
	};

	/// <summary>Bridge for native model materials until the managed model reader lands (N3).</summary>
	public static float GetFriction(global::ZenKit.MaterialGroup group) =>
		GetFriction((MaterialGroup)(byte)group);
}
