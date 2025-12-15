using Nursia.Env;

namespace OpenGothic.Viewer;

internal static class Configuration
{
	public static string GamePath { get; set; }
	public static bool NoFixedStep { get; set; }
	public static RenderEnvironment RenderEnvironment { get; set; } = RenderEnvironment.Default.Clone();
}
