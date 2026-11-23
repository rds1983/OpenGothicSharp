using Microsoft.Xna.Framework;
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

	[Fact]
	public void Parse_CullIsUnsetByDefault()
	{
		var result = CommandLine.Parse(Array.Empty<string>());

		Assert.Null(result.Cull);
	}

	[Fact]
	public void Parse_NoCull_DisablesCulling()
	{
		var result = CommandLine.Parse(new[] { "-nocull" });

		Assert.False(result.Cull);
	}

	[Theory]
	[InlineData("true", true)]
	[InlineData("false", false)]
	[InlineData("1", true)]
	[InlineData("0", false)]
	public void Parse_CullValue_IsRead(string value, bool expected)
	{
		var result = CommandLine.Parse(new[] { $"-cull:{value}" });

		Assert.Equal(expected, result.Cull);
	}

	[Fact]
	public void Parse_InvalidCullValue_IsRejected()
	{
		var result = CommandLine.Parse(new[] { "-g", @"C:\Gothic", "-cull:maybe" });

		Assert.Null(result.Cull);
		Assert.False(result.Validate());
	}

	[Fact]
	public void Apply_LeavesCullAloneWhenUnset()
	{
		DebugSettings.CullEnabled = false;

		try
		{
			CommandLine.Parse(Array.Empty<string>()).Apply();

			Assert.False(DebugSettings.CullEnabled);
		}
		finally
		{
			DebugSettings.CullEnabled = true;
		}
	}

	[Fact]
	public void Apply_PushesCullToDebugSettings()
	{
		DebugSettings.CullEnabled = true;

		try
		{
			CommandLine.Parse(new[] { "-nocull" }).Apply();

			Assert.False(DebugSettings.CullEnabled);
		}
		finally
		{
			DebugSettings.CullEnabled = true;
		}
	}

	[Fact]
	public void Parse_ModelsIsUnsetByDefault()
	{
		var result = CommandLine.Parse(Array.Empty<string>());

		Assert.Null(result.Models);
	}

	[Fact]
	public void Parse_NoModels_DisablesModels()
	{
		var result = CommandLine.Parse(new[] { "-nomodels" });

		Assert.False(result.Models);
	}

	[Theory]
	[InlineData("true", true)]
	[InlineData("false", false)]
	[InlineData("1", true)]
	[InlineData("0", false)]
	public void Parse_ModelsValue_IsRead(string value, bool expected)
	{
		var result = CommandLine.Parse(new[] { $"-models:{value}" });

		Assert.Equal(expected, result.Models);
	}

	[Fact]
	public void Apply_LeavesModelsAloneWhenUnset()
	{
		DebugSettings.ModelsEnabled = false;

		try
		{
			CommandLine.Parse(Array.Empty<string>()).Apply();

			Assert.False(DebugSettings.ModelsEnabled);
		}
		finally
		{
			DebugSettings.ModelsEnabled = true;
		}
	}

	[Fact]
	public void Apply_PushesModelsToDebugSettings()
	{
		DebugSettings.ModelsEnabled = true;

		try
		{
			CommandLine.Parse(new[] { "-nomodels" }).Apply();

			Assert.False(DebugSettings.ModelsEnabled);
		}
		finally
		{
			DebugSettings.ModelsEnabled = true;
		}
	}

	[Fact]
	public void Parse_ScreenshotIsUnsetByDefault()
	{
		var result = CommandLine.Parse(Array.Empty<string>());

		Assert.Null(result.ScreenshotPath);
		Assert.Null(result.ScreenshotFrame);
	}

	[Fact]
	public void Parse_Screenshot_ReadsPathAndFrame()
	{
		var result = CommandLine.Parse(new[] { "-screenshot:out/shot.png", "-screenshotframe:12" });

		Assert.Equal("out/shot.png", result.ScreenshotPath);
		Assert.Equal(12, result.ScreenshotFrame);
	}

	[Fact]
	public void Parse_ScreenshotWithoutPath_IsRejected()
	{
		var result = CommandLine.Parse(new[] { "-g", @"C:\Gothic", "-screenshot:" });

		Assert.False(result.Validate());
	}

	[Theory]
	[InlineData("0")]
	[InlineData("-3")]
	[InlineData("soon")]
	public void Parse_InvalidScreenshotFrame_IsRejected(string value)
	{
		var result = CommandLine.Parse(new[] { "-g", @"C:\Gothic", $"-screenshotframe:{value}" });

		Assert.Null(result.ScreenshotFrame);
		Assert.False(result.Validate());
	}

	[Fact]
	public void Parse_CameraIsUnsetByDefault()
	{
		var result = CommandLine.Parse(Array.Empty<string>());

		Assert.Null(result.CameraPosition);
		Assert.Null(result.LookAtTarget);
	}

	[Fact]
	public void Parse_CameraAndLookAt_AreRead()
	{
		var result = CommandLine.Parse(new[] { "-cam:1.5,-2,300", "-lookat:10,20,-30" });

		Assert.Equal(new Vector3(1.5f, -2f, 300f), result.CameraPosition);
		Assert.Equal(new Vector3(10f, 20f, -30f), result.LookAtTarget);
	}

	[Fact]
	public void Parse_CameraAcceptsNegativeAndExponentValues()
	{
		var result = CommandLine.Parse(new[] { "-cam:-1e2,+3.25,0" });

		Assert.Equal(-100f, result.CameraPosition.Value.X);
		Assert.Equal(3.25f, result.CameraPosition.Value.Y);
		Assert.Equal(0f, result.CameraPosition.Value.Z);
	}

	[Theory]
	[InlineData("1,2")]
	[InlineData("1,2,3,4")]
	[InlineData("1,two,3")]
	[InlineData("")]
	public void Parse_InvalidVector_IsRejected(string value)
	{
		var result = CommandLine.Parse(new[] { "-g", @"C:\Gothic", $"-cam:{value}" });

		Assert.Null(result.CameraPosition);
		Assert.False(result.Validate());
	}

	[Fact]
	public void Parse_DebugViewsAreOffByDefault()
	{
		var result = CommandLine.Parse(Array.Empty<string>());

		Assert.Null(result.Wireframe);
		Assert.Null(result.Bounds);
	}

	[Fact]
	public void Parse_BareDebugViewFlags_EnableThem()
	{
		var result = CommandLine.Parse(new[] { "-wireframe", "-bounds" });

		Assert.True(result.Wireframe);
		Assert.True(result.Bounds);
	}

	[Theory]
	[InlineData("true", true)]
	[InlineData("false", false)]
	public void Parse_DebugViewValues_AreRead(string value, bool expected)
	{
		var result = CommandLine.Parse(new[] { $"-wireframe:{value}", $"-bounds:{value}" });

		Assert.Equal(expected, result.Wireframe);
		Assert.Equal(expected, result.Bounds);
	}

	[Fact]
	public void Parse_SpawnModelIsUnsetByDefault()
	{
		var result = CommandLine.Parse(Array.Empty<string>());

		Assert.Null(result.SpawnModel);
		Assert.Null(result.SpawnDistance);
	}

	[Fact]
	public void Parse_SpawnModel_ReadsNameAndDistance()
	{
		var result = CommandLine.Parse(new[] { "-model:ARMOR_PRISONER", "-modeldist:250.5" });

		Assert.Equal("ARMOR_PRISONER", result.SpawnModel);
		Assert.Equal(250.5f, result.SpawnDistance);
		Assert.Null(result.SpawnOffset);
	}

	[Fact]
	public void Parse_SpawnOffset_ReadsCameraSpaceOffset()
	{
		var result = CommandLine.Parse(new[] { "-model:ARMOR_PRISONER", "-modeloffset:120,-15,8.5" });

		Assert.Equal(new Vector3(120f, -15f, 8.5f), result.SpawnOffset);
	}

	[Theory]
	[InlineData("1,2")]
	[InlineData("1,2,3,4")]
	[InlineData("left,2,3")]
	public void Parse_InvalidSpawnOffset_IsRejected(string value)
	{
		var result = CommandLine.Parse(new[] { "-g", @"C:\Gothic", $"-modeloffset:{value}" });

		Assert.Null(result.SpawnOffset);
		Assert.False(result.Validate());
	}

	[Fact]
	public void Parse_Attach_ReadsPointAndVisual()
	{
		var result = CommandLine.Parse(new[] { "-model:ARMOR_PRISONER", "-attach:ZS_RIGHTHAND:ITMW_1H_MACHETE_01.MRM" });

		Assert.Equal("ZS_RIGHTHAND", result.AttachPoint);
		Assert.Equal("ITMW_1H_MACHETE_01.MRM", result.AttachVisual);
	}

	[Theory]
	[InlineData("-attach:ZS_RIGHTHAND")]
	[InlineData("-attach::ITMW_1H_MACHETE_01.MRM")]
	[InlineData("-attach:ZS_RIGHTHAND:")]
	public void Parse_InvalidAttach_IsRejected(string value)
	{
		var result = CommandLine.Parse(new[] { "-g", @"C:\Gothic", value });

		Assert.Null(result.AttachPoint);
		Assert.Null(result.AttachVisual);
		Assert.False(result.Validate());
	}

	[Fact]
	public void Parse_SpawnModelWithoutName_IsRejected()
	{
		var result = CommandLine.Parse(new[] { "-g", @"C:\Gothic", "-model:" });

		Assert.False(result.Validate());
	}

	[Theory]
	[InlineData("0")]
	[InlineData("-10")]
	[InlineData("close")]
	public void Parse_InvalidSpawnDistance_IsRejected(string value)
	{
		var result = CommandLine.Parse(new[] { "-g", @"C:\Gothic", $"-modeldist:{value}" });

		Assert.Null(result.SpawnDistance);
		Assert.False(result.Validate());
	}

	[Fact]
	public void Parse_AnimationsIsUnsetByDefault()
	{
		var result = CommandLine.Parse(Array.Empty<string>());

		Assert.Null(result.Animations);
		Assert.Null(result.AnimationDump);
		Assert.Null(result.AnimationClip);
	}

	[Fact]
	public void Parse_NoAnims_DisablesAnimations()
	{
		var result = CommandLine.Parse(new[] { "-noanims" });

		Assert.False(result.Animations);
	}

	[Theory]
	[InlineData("true", true)]
	[InlineData("false", false)]
	[InlineData("1", true)]
	[InlineData("0", false)]
	public void Parse_AnimationsValue_IsRead(string value, bool expected)
	{
		var result = CommandLine.Parse(new[] { $"-anims:{value}" });

		Assert.Equal(expected, result.Animations);
	}

	[Fact]
	public void Parse_InvalidAnimationsValue_IsRejected()
	{
		var result = CommandLine.Parse(new[] { "-g", @"C:\Gothic", "-anims:maybe" });

		Assert.Null(result.Animations);
		Assert.False(result.Validate());
	}

	[Fact]
	public void Apply_LeavesAnimationsAloneWhenUnset()
	{
		DebugSettings.AnimationsEnabled = false;

		try
		{
			CommandLine.Parse(Array.Empty<string>()).Apply();

			Assert.False(DebugSettings.AnimationsEnabled);
		}
		finally
		{
			DebugSettings.AnimationsEnabled = true;
		}
	}

	[Fact]
	public void Apply_PushesAnimationsToDebugSettings()
	{
		DebugSettings.AnimationsEnabled = true;

		try
		{
			CommandLine.Parse(new[] { "-noanims" }).Apply();

			Assert.False(DebugSettings.AnimationsEnabled);
		}
		finally
		{
			DebugSettings.AnimationsEnabled = true;
		}
	}

	[Fact]
	public void Parse_AnimationDump_ReadsScriptAndClip()
	{
		var result = CommandLine.Parse(new[] { "-animdump:HUMANS", "-animclip:S_RUN" });

		Assert.Equal("HUMANS", result.AnimationDump);
		Assert.Equal("S_RUN", result.AnimationClip);
	}

	[Fact]
	public void Parse_AnimationDumpWithoutScript_IsRejected()
	{
		var result = CommandLine.Parse(new[] { "-g", @"C:\Gothic", "-animdump:" });

		Assert.False(result.Validate());
	}

	[Fact]
	public void Parse_AnimationClipWithoutName_IsRejected()
	{
		var result = CommandLine.Parse(new[] { "-g", @"C:\Gothic", "-animclip:" });

		Assert.False(result.Validate());
	}

	[Fact]
	public void Parse_PlaybackIsUnsetByDefault()
	{
		var result = CommandLine.Parse(Array.Empty<string>());

		Assert.Null(result.PlayAnimation);
		Assert.Null(result.FrozenTime);
		Assert.Null(result.PlayTime);
	}

	[Fact]
	public void Parse_Play_ReadsClipName()
	{
		var result = CommandLine.Parse(new[] { "-play:S_RUN" });

		Assert.Equal("S_RUN", result.PlayAnimation);
	}

	[Fact]
	public void Parse_PlayWithoutName_IsRejected()
	{
		var result = CommandLine.Parse(new[] { "-g", @"C:\Gothic", "-play:" });

		Assert.Null(result.PlayAnimation);
		Assert.False(result.Validate());
	}

	[Fact]
	public void Parse_Freeze_IsABareFlag()
	{
		var result = CommandLine.Parse(new[] { "-freeze" });

		Assert.True(result.FrozenTime);
	}

	[Fact]
	public void Parse_AnimTime_ReadsSeconds()
	{
		var result = CommandLine.Parse(new[] { "-animtime:0.75" });

		Assert.Equal(0.75f, result.PlayTime);
	}

	[Fact]
	public void Parse_AnimTimeAcceptsZero()
	{
		var folder = CreateGothicFolder();

		try
		{
			var result = CommandLine.Parse(new[] { "-g", folder, "-animtime:0" });

			Assert.Equal(0f, result.PlayTime);
			Assert.True(result.Validate());
		}
		finally
		{
			Directory.Delete(folder, true);
		}
	}

	[Theory]
	[InlineData("soon")]
	[InlineData("")]
	public void Parse_InvalidAnimTime_IsRejected(string value)
	{
		var result = CommandLine.Parse(new[] { "-g", @"C:\Gothic", $"-animtime:{value}" });

		Assert.Null(result.PlayTime);
		Assert.False(result.Validate());
	}
}
