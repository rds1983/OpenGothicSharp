using System;
using System.Reflection;

namespace OpenGothic;

public static class OG
{
	public static Action<string> LogInfo = Console.WriteLine;
	public static Action<string> LogWarning = Console.WriteLine;
	public static Action<string> LogError = Console.WriteLine;

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
