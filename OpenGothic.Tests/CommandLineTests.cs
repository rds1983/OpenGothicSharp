using OpenGothic;
using System;
using System.IO;
using Xunit;

namespace OpenGothic.Tests;

public class CommandLineTests
{
	private static string CreateGothicFolder()
	{
		var folder = Path.Combine(Path.GetTempPath(), $"opengothic-{Guid.NewGuid():N}");
		Directory.CreateDirectory(Path.Combine(folder, "Data"));
		return folder;
	}

	[Fact]
	public void Parse_ReadsAllFlags()
	{
		var result = CommandLine.Parse(new[]
		{
			"-g", @"C:\Gothic",
			"-game:Karibik.ini",
			"-w", "newworld.zen",
			"-nomenu",
			"-devmode"
		});

		Assert.Equal(@"C:\Gothic", result.GothicPath);
		Assert.Equal("Karibik.ini", result.ModFile);
		Assert.Equal("newworld.zen", result.World);
		Assert.True(result.NoMenu);
		Assert.True(result.DevMode);
	}

	[Fact]
	public void Parse_DefaultsAreOff()
	{
		var result = CommandLine.Parse(Array.Empty<string>());

		Assert.Null(result.GothicPath);
		Assert.Null(result.ModFile);
		Assert.Null(result.World);
		Assert.False(result.NoMenu);
		Assert.False(result.DevMode);
	}

	[Fact]
	public void Validate_FailsWithoutGothicPath()
	{
		var result = CommandLine.Parse(Array.Empty<string>());

		Assert.False(result.Validate());
	}

	[Fact]
	public void Validate_FailsForMissingFolder()
	{
		var result = CommandLine.Parse(new[] { "-g", Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}") });

		Assert.False(result.Validate());
	}

	[Fact]
	public void Validate_FailsWhenDataFolderMissing()
	{
		var folder = Path.Combine(Path.GetTempPath(), $"opengothic-{Guid.NewGuid():N}");
		Directory.CreateDirectory(folder);

		try
		{
			var result = CommandLine.Parse(new[] { "-g", folder });

			Assert.False(result.Validate());
		}
		finally
		{
			Directory.Delete(folder, true);
		}
	}

	[Fact]
	public void Validate_SucceedsForGothicFolder()
	{
		var folder = CreateGothicFolder();

		try
		{
			var result = CommandLine.Parse(new[] { "-g", folder });

			Assert.True(result.Validate());
		}
		finally
		{
			Directory.Delete(folder, true);
		}
	}

	[Fact]
	public void Validate_FailsWhenValueMissing()
	{
		var result = CommandLine.Parse(new[] { "-g" });

		Assert.False(result.Validate());
	}

	[Fact]
	public void Parse_UnknownArgument_IsIgnored()
	{
		var result = CommandLine.Parse(new[] { "-g", @"C:\Gothic", "-bogus" });

		Assert.Equal(@"C:\Gothic", result.GothicPath);
	}
}
