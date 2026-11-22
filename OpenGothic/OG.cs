using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection;

namespace OpenGothic;

public static class OG
{
	private static GraphicsDevice _device;

	/// <summary>
	/// Gets or sets the action used for info-level logging.
	/// </summary>
	public static Action<string> LogInfo = Console.WriteLine;

	/// <summary>
	/// Gets or sets the action used for warning-level logging.
	/// </summary>
	public static Action<string> LogWarning = Console.WriteLine;

	/// <summary>
	/// Gets or sets the action used for error-level logging.
	/// </summary>
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
