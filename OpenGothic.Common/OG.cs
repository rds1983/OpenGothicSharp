using System.Reflection;

namespace OpenGothic;

public static class OG
{
	public static string Version
	{
		get
		{
			var assembly = typeof(OG).Assembly;
			var name = new AssemblyName(assembly.FullName);

			return name.Version.ToString();
		}
	}

}
