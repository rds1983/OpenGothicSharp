using AssetManagementBase;
using System;

namespace OpenGothic.Viewer;

class Program
{
	static void Main(string[] args)
	{
		try
		{
			if (args.Length == 0)
			{
				Console.WriteLine("Usage: OpenGothic.Viewer <gothic_folder>");
				return;
			}

#if FNA
			Environment.SetEnvironmentVariable("FNA3D_FORCE_DRIVER", "D3D11");
#endif

			AMBConfiguration.Logger = Console.WriteLine;
			using (var game = new ViewerGame(args[0]))
			{
				game.Run();
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.ToString());
		}
	}
}
