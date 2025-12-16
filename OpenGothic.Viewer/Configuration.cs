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
		RenderEnvironment.FogStart = Constants.FogStart;
		RenderEnvironment.FogEnd = Constants.FogEnd;
	}
}
