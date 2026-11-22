using OpenGothic;
using System;
using System.IO;
using Xunit;

namespace OpenGothic.Tests;

public class SettingsTests
{
	private sealed class TempGothicFolder : IDisposable
	{
		public string Root { get; }
		public string Working { get; }

		public TempGothicFolder()
		{
			Root = Path.Combine(Path.GetTempPath(), $"opengothic-{Guid.NewGuid():N}");
			Working = Path.Combine(Root, "work");

			Directory.CreateDirectory(Path.Combine(Root, "system"));
			Directory.CreateDirectory(Working);
		}

		public void WriteSystemIni(string name, string content) =>
			File.WriteAllText(Path.Combine(Root, "system", name), content);

		public void WriteUserIni(string content) =>
			File.WriteAllText(Path.Combine(Working, Settings.UserFileName), content);

		public void Dispose()
		{
			if (Directory.Exists(Root))
			{
				Directory.Delete(Root, true);
			}
		}
	}

	[Fact]
	public void Load_UsesBuiltInDefaults_WhenNoFiles()
	{
		using var folder = new TempGothicFolder();

		var settings = new Settings();
		settings.Load(folder.Root, folder.Working);

		Assert.Equal(0.5f, settings.MusicVolume);
		Assert.Equal(0.5f, settings.SoundVolume);
		Assert.Equal(67.5f, settings.CameraFov);
		Assert.Equal(0, settings.FpsLimit);
		Assert.True(settings.MusicEnabled);
	}

	[Fact]
	public void Load_ReadsSystemGothicIni()
	{
		using var folder = new TempGothicFolder();
		folder.WriteSystemIni("Gothic.ini", "[SOUND]\nmusicVolume=0.8\nsoundVolume=1\n");

		var settings = new Settings();
		settings.Load(folder.Root, folder.Working);

		Assert.Equal(0.8f, settings.MusicVolume);
		Assert.Equal(1f, settings.SoundVolume);
	}

	[Fact]
	public void UserFile_OverridesSystemFile()
	{
		using var folder = new TempGothicFolder();
		folder.WriteSystemIni("Gothic.ini", "[SOUND]\nmusicVolume=0.8\n");
		folder.WriteUserIni("[SOUND]\nmusicVolume=0.2\n");

		var settings = new Settings();
		settings.Load(folder.Root, folder.Working);

		Assert.Equal(0.2f, settings.MusicVolume);
	}

	[Fact]
	public void SystemPack_IsRead()
	{
		using var folder = new TempGothicFolder();
		folder.WriteSystemIni("SystemPack.ini", "[PARAMETERS]\nVerticalFOV=70\nFPS_Limit=60\n");

		var settings = new Settings();
		settings.Load(folder.Root, folder.Working);

		Assert.Equal(70f, settings.CameraFov);
		Assert.Equal(60, settings.FpsLimit);
	}

	[Fact]
	public void CameraFov_FallsBackWhenInvalid()
	{
		using var folder = new TempGothicFolder();
		folder.WriteSystemIni("SystemPack.ini", "[PARAMETERS]\nVerticalFOV=0\n");

		var settings = new Settings();
		settings.Load(folder.Root, folder.Working);

		Assert.Equal(67.5f, settings.CameraFov);
	}

	[Fact]
	public void Set_ThenSave_PersistsToUserFile()
	{
		using var folder = new TempGothicFolder();

		var settings = new Settings();
		settings.Load(folder.Root, folder.Working);
		settings.MusicVolume = 0.25f;
		settings.ShowFpsCounter = true;
		settings.Save();

		var reloaded = new Settings();
		reloaded.Load(folder.Root, folder.Working);

		Assert.Equal(0.25f, reloaded.MusicVolume);
		Assert.True(reloaded.ShowFpsCounter);
	}

	[Fact]
	public void Save_WritesToUserFileNotSystemFile()
	{
		using var folder = new TempGothicFolder();
		folder.WriteSystemIni("Gothic.ini", "[SOUND]\nmusicVolume=0.8\n");

		var settings = new Settings();
		settings.Load(folder.Root, folder.Working);
		settings.MusicVolume = 0.1f;
		settings.Save();

		var systemContent = File.ReadAllText(Path.Combine(folder.Root, "system", "Gothic.ini"));
		Assert.Contains("0.8", systemContent);

		var userContent = File.ReadAllText(Path.Combine(folder.Working, Settings.UserFileName));
		Assert.Contains("0.1", userContent);
	}
}
