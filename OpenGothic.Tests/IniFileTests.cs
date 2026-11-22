using OpenGothic;
using System;
using System.IO;
using Xunit;

namespace OpenGothic.Tests;

public class IniFileTests
{
	[Fact]
	public void Parse_ReadsSectionsAndValues()
	{
		var ini = new IniFile();
		ini.Parse("[SOUND]\nmusicVolume=0.5\nsoundEnabled=1\n");

		Assert.True(ini.Has("SOUND", "musicVolume"));
		Assert.Equal("0.5", ini.GetString("SOUND", "musicVolume"));
		Assert.Equal(1, ini.GetInt("SOUND", "soundEnabled"));
	}

	[Fact]
	public void Parse_IgnoresCommentsAndBlankLines()
	{
		var ini = new IniFile();
		ini.Parse("; a comment\n\n[GAME]\n\n; another\nmouseSensitivity=0.5\n");

		Assert.Equal(0.5f, ini.GetFloat("GAME", "mouseSensitivity"));
	}

	[Fact]
	public void Parse_StripsInlineComment()
	{
		var ini = new IniFile();
		ini.Parse("[PERFORMANCE]\nmodelDetail=1 ; detail level\n");

		Assert.Equal(1, ini.GetInt("PERFORMANCE", "modelDetail"));
	}

	[Fact]
	public void Lookup_IsCaseInsensitive()
	{
		var ini = new IniFile();
		ini.Parse("[sound]\nMusicVolume=0.5\n");

		Assert.True(ini.Has("SOUND", "musicvolume"));
		Assert.Equal(0.5f, ini.GetFloat("Sound", "MUSICVOLUME"));
	}

	[Fact]
	public void MissingKey_ReturnsDefault()
	{
		var ini = new IniFile();
		ini.Parse("[SOUND]\nmusicEnabled=1\n");

		Assert.False(ini.Has("SOUND", "nope"));
		Assert.Equal("fallback", ini.GetString("SOUND", "nope", "fallback"));
		Assert.Equal(42, ini.GetInt("SOUND", "nope", 42));
		Assert.Equal(1.5f, ini.GetFloat("SOUND", "nope", 1.5f));
	}

	[Fact]
	public void NonNumericValue_FallsBackToDefault()
	{
		var ini = new IniFile();
		ini.Parse("[GAME]\nmouseSensitivity=fast\n");

		Assert.Equal(0.5f, ini.GetFloat("GAME", "mouseSensitivity", 0.5f));
	}

	[Fact]
	public void KeysOutsideSection_AreIgnored()
	{
		var ini = new IniFile();
		ini.Parse("orphan=1\n[GAME]\nmodelDetail=1\n");

		Assert.False(ini.Has("GAME", "orphan"));
	}

	[Fact]
	public void Save_ThenLoad_RoundTrips()
	{
		var path = Path.Combine(Path.GetTempPath(), $"opengothic-{Guid.NewGuid():N}.ini");

		try
		{
			var ini = new IniFile();
			ini.Parse("[SOUND]\nmusicVolume=0.5\n");
			ini.Set("SOUND", "musicVolume", 0.25f);
			ini.Set("DEBUG", "Show_FPS_Counter", 1);
			ini.Save(path);

			var loaded = new IniFile();
			loaded.Load(path);

			Assert.Equal(0.25f, loaded.GetFloat("SOUND", "musicVolume"));
			Assert.Equal(1, loaded.GetInt("DEBUG", "Show_FPS_Counter"));
		}
		finally
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
	}

	[Fact]
	public void Set_OverwritesExistingValue()
	{
		var ini = new IniFile();
		ini.Parse("[SOUND]\nmusicVolume=0.5\n");

		ini.Set("SOUND", "musicVolume", 0.75f);

		Assert.Equal(0.75f, ini.GetFloat("SOUND", "musicVolume"));
		Assert.True(ini.IsDirty);
	}

	[Fact]
	public void Load_MissingFile_KeepsEmptyAndDoesNotThrow()
	{
		var ini = new IniFile();
		ini.Load(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.ini"));

		Assert.False(ini.Has("SOUND", "musicVolume"));
	}
}
