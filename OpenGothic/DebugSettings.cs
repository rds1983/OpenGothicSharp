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
}
