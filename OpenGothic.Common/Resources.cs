using System.Collections.Generic;
using Nursia;
using AssetManagementBase;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System.IO;

namespace OpenGothic;

internal static class Resources
{
	private static readonly AssetManager _assetsEffects;
	private static Texture2D _white;

	public static Texture2D White
	{
		get
		{
			if (_white == null)
			{
				_white = new Texture2D(Nrs.GraphicsDevice, 1, 1);
				_white.SetData(new[] { Color.White });
			}

			return _white;
		}
	}

	static Resources()
	{
		var assembly = typeof(Resources).Assembly;

		var names = assembly.GetManifestResourceNames();

#if FNA
		var path = "OpenGothic.Effects.FNA.bin";
#else
#endif

		_assetsEffects = AssetManager.CreateResourceAssetManager(assembly, path, false);
	}

	public static Effect GetEffect(string name, Dictionary<string, string> defines = null)
	{
		name = Path.ChangeExtension(name, "efb");

		return _assetsEffects.LoadEffect(Nrs.GraphicsDevice, name, defines);
	}
}
