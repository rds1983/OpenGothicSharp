using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using OpenGothic.Graphics;
using System;
using System.Collections.Generic;
using Xunit;

namespace OpenGothic.Tests;

public class AnimationDataTests
{
	private static AnimationData CreatePlayback(bool loop, bool reverse = false)
	{
		var model = new DrModel(new DrModelBone("BIP01"));
		var info = new ModelClipInfo
		{
			Name = "TEST",
			Next = "TEST",
			FrameCount = 3,
			Fps = 10f,
			Loop = loop
		};

		var keys = new[]
		{
			Key(0f, 10f, 0f),
			Key(0.1f, 20f, 2f),
			Key(0.2f, 30f, 4f)
		};
		if (loop)
		{
			// A looping clip closes with a key that repeats frame zero at the duration.
			keys = new[] { keys[0], keys[1], keys[2], Key(0.3f, 10f, 0f) };
		}

		var clip = new AnimationClip("TEST", TimeSpan.FromSeconds(0.3),
			new[] { new AnimationChannel(0, keys) })
		{
			Tag = new AnimationClipTag(info)
		};
		model.Animations = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase)
		{
			["TEST"] = clip
		};

		var data = new AnimationData(new DrModelInstance(model));
		Assert.True(data.Play("TEST", blend: false, reverse: reverse));
		return data;
	}

	private static AnimationChannelKeyframe Key(float time, float y, float z) =>
		new AnimationChannelKeyframe(TimeSpan.FromSeconds(time),
			new SrtTransform(new Vector3(0f, y, z), Quaternion.Identity, Vector3.One));

	[Fact]
	public void BaseRootTranslation_InterpolatesTheRootChannel()
	{
		var data = CreatePlayback(loop: false);

		Assert.True(data.TryGetBaseRootTranslation(0, out var start));
		Assert.Equal(10f, start.Y, 3);
		Assert.Equal(0f, start.Z, 3);

		data.Update(TimeSpan.FromSeconds(0.15));

		Assert.True(data.TryGetBaseRootTranslation(0, out var mid));
		Assert.Equal(25f, mid.Y, 3);
		Assert.Equal(3f, mid.Z, 3);
	}

	[Fact]
	public void BaseRootTranslation_WrapsALoopingClip()
	{
		var data = CreatePlayback(loop: true);
		data.Update(TimeSpan.FromSeconds(0.45));

		Assert.True(data.TryGetBaseRootTranslation(0, out var sample));
		Assert.Equal(25f, sample.Y, 3);
		Assert.Equal(3f, sample.Z, 3);
	}

	[Fact]
	public void BaseRootTranslation_SkipsBonesWithoutAChannel()
	{
		var data = CreatePlayback(loop: false);
		Assert.False(data.TryGetBaseRootTranslation(1, out _));
	}

	[Fact]
	public void BaseClipReversed_ReportsTheRuntimeReverseFlag()
	{
		var forward = CreatePlayback(loop: false);
		Assert.False(forward.IsBaseClipReversed);

		var backwards = CreatePlayback(loop: false, reverse: true);
		Assert.True(backwards.IsBaseClipReversed);

		backwards.Update(TimeSpan.FromSeconds(0.05));
		Assert.True(backwards.TryGetBaseRootTranslation(0, out var sample));
		Assert.Equal(15f, sample.Y, 3);
	}
}
