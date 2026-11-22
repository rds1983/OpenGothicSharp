using AssetManagementBase;
using System;

namespace OpenGothic;

internal static class Program
{
	static void Main(string[] args)
	{
		AMBConfiguration.Logger = Console.WriteLine;

		using (var game = new OgGame())
		{
			game.Run();
		}
	}
}
