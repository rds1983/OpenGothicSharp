using Nursia.Env;

namespace OpenGothic.Viewer;

internal static class Configuration
{
	public static string GamePath { get; set; }
	public static bool NoFixedStep { get; set; }
	public static RenderEnvironment RenderEnvironment { get; }

	static Configuration()
	{
		RenderEnvironment = RenderEnvironment.Default.Clone();
		RenderEnvironment.FogEnabled = true;
		RenderEnvironment.FogStart = 20000.0f;
		RenderEnvironment.FogEnd = 50000.0f;
	}
}
