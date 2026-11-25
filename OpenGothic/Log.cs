using System;
using System.IO;

namespace OpenGothic;

public static class Log
{
	private static readonly object Sync = new object();
	private static StreamWriter _file;

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
