using OpenGothic.Graphics;

namespace OpenGothic;

/// <summary>
/// Process-wide switches for diagnostic behaviour, mostly for narrowing down render bugs
/// from the command line. These are startup settings, not user preferences, so they live in
/// static properties rather than <see cref="Settings"/>.
/// </summary>
public static class DebugSettings
{
	/// <summary>
	/// Whether <see cref="Renderer.DrawMeshPart"/> frustum-culls before issuing a draw.
	/// Turning it off draws every part in every visible cell, which is useful for proving a
	/// missing object is a culling problem rather than a loading one. Defaults to true.
	/// </summary>
	public static bool CullEnabled { get; set; } = true;

	/// <summary>
	/// Whether ZEN vobs that carry a model are instantiated and drawn. Turning it off
	/// leaves only the static world, which is useful for telling a model-loading problem
	/// apart from a world-rendering one. Defaults to true.
	/// </summary>
	public static bool ModelsEnabled { get; set; } = true;

	/// <summary>
	/// Whether models get the animation clips their script lists. Turning it off keeps the
	/// bind pose and skips reading any <c>.MAN</c> file, which separates an animation problem
	/// from a model or world one. Defaults to true.
	/// </summary>
	public static bool AnimationsEnabled { get; set; } = true;

	/// <summary>
	/// Whether static geometry keeps the CPU triangles the physics world collides with.
	/// Building them costs time and memory at load, so they are off unless the physics
	/// self-check is requested. Defaults to false.
	/// </summary>
	public static bool CollisionEnabled { get; set; }

	/// <summary>
	/// Log every external called from a running AI state, naming the state. Used to discover
	/// which AI actions a shipped <c>ZS_*</c> state needs. Defaults to false.
	/// </summary>
	public static bool AiTrace { get; set; }
}
