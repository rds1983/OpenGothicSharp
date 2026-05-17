using AssetManagementBase;
using System;

namespace OpenGothic;

class Program
{
	static void Main(string[] args)
	{
		try
		{
			if (args.Length == 0)
			{
				Console.WriteLine("Usage: OpenGothic.Launcher <gothic_folder>");
				return;
			}

#if FNA
			Environment.SetEnvironmentVariable("FNA3D_FORCE_DRIVER", "D3D11");
#endif

			AMBConfiguration.Logger = Console.WriteLine;

			using (var game = new MainGame(args[0]))
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
