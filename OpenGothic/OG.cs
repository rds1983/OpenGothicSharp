using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection;

namespace OpenGothic;

public static class OG
{
	private static GraphicsDevice _device;

	public static string Version
	{
		get
		{
			var assembly = typeof(OG).Assembly;
			var name = new AssemblyName(assembly.FullName);

			return name.Version.ToString();
		}
	}

	public static GraphicsDevice GraphicsDevice
	{
		get
		{
			if (_device == null)
			{
				throw new Exception("Set GraphicsDevice before using OpenGothic");
			}

			return _device;
		}

		set
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			_device = value;
		}
	}

}
