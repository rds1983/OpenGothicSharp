using System;
using System.IO;
using ZenKit;

namespace OpenGothic;

public static class Log
{
	private static readonly object Sync = new object();
	private static StreamWriter _file;
	private static Logger.Callback _zenKitCallback;

	public static string FileName { get; private set; }

	public static void Initialize(string fileName = "log.txt")
	{
		FileName = Path.GetFullPath(fileName);

		try
		{
			_file?.Dispose();
			_file = new StreamWriter(FileName, false) { AutoFlush = true };
		}
		catch (Exception ex)
		{
			_file = null;
			Console.Error.WriteLine($"Unable to open log file '{FileName}': {ex.Message}");
		}

		_zenKitCallback = OnZenKitLog;
		Logger.Set(LogLevel.Info, _zenKitCallback);
	}

	public static void Shutdown()
	{
		lock (Sync)
		{
			_file?.Flush();
			_file?.Dispose();
			_file = null;
		}
	}

	public static void Info(string message) => Write("INFO", message, false);

	public static void Warning(string message) => Write("WARN", message, true);

	public static void Error(string message) => Write("ERROR", message, true);

	private static void OnZenKitLog(LogLevel level, string name, string message)
	{
		var category = string.IsNullOrEmpty(name) ? "zenkit" : $"zenkit:{name}";

		switch (level)
		{
			case LogLevel.Error:
				Error($"[{category}] {message}");
				break;

			case LogLevel.Warning:
				Warning($"[{category}] {message}");
				break;

			default:
				Info($"[{category}] {message}");
				break;
		}
	}

	private static void Write(string level, string message, bool isError)
	{
		var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";

		lock (Sync)
		{
			var console = isError ? Console.Error : Console.Out;
			console.WriteLine(line);

			if (_file != null)
			{
				_file.WriteLine(line);
			}
		}
	}
}
