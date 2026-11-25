using OpenGothic.ZenKit;
using System;
using System.Linq;
using Xunit;

namespace OpenGothic.Tests.ZenKit;

public class TextureTests
{
	[Fact]
	public void LoadsAnRgbaTexture()
	{
		var pixels = new byte[]
		{
			1, 2, 3, 4,
			5, 6, 7, 8,
			9, 10, 11, 12,
			13, 14, 15, 16
		};
		var data = TextureBuilder.Build(TextureFormat.R8G8B8A8, 2, 2, pixels);

		var texture = new Texture(new MemoryRead(data));

		Assert.Equal(TextureFormat.R8G8B8A8, texture.Format);
		Assert.Equal(2, texture.Width);
		Assert.Equal(2, texture.Height);
		Assert.Equal(1, texture.MipmapCount);
		Assert.Equal(pixels, texture.GetMipmapRaw(0));
		Assert.Equal(pixels, texture.GetMipmapRgba(0));
	}

	[Fact]
	public void MipmapsAreExposedInLevelOrder()
	{
		var level0 = Enumerable.Range(0, 4 * 4 * 4).Select(i => (byte)i).ToArray();
		var level1 = Enumerable.Range(0, 2 * 2 * 4).Select(i => (byte)(200 + i)).ToArray();
		var data = TextureBuilder.Build(TextureFormat.R8G8B8A8, 4, 4, level0, level1);

		var texture = new Texture(new MemoryRead(data));

		Assert.Equal(2, texture.MipmapCount);
		Assert.Equal(4, texture.GetWidth(0));
		Assert.Equal(2, texture.GetWidth(1));
		Assert.Equal(level0, texture.GetMipmapRaw(0));
		Assert.Equal(level1, texture.GetMipmapRaw(1));
	}

	[Fact]
	public void SwapsBgraToRgba()
	{
		var data = TextureBuilder.Build(TextureFormat.B8G8R8A8, 1, 1, new byte[] { 10, 20, 30, 40 });

		var texture = new Texture(new MemoryRead(data));

		Assert.Equal(new byte[] { 30, 20, 10, 40 }, texture.GetMipmapRgba(0));
	}

	[Fact]
	public void DecodesPalettizedTextures()
	{
		var palette = new byte[256 * 4];
		palette[5 * 4 + 0] = 80; // B
		palette[5 * 4 + 1] = 70; // G
		palette[5 * 4 + 2] = 60; // R
		palette[5 * 4 + 3] = 50; // A
		var data = TextureBuilder.BuildPalettized(2, 2, palette, new byte[] { 5, 5, 5, 5 });

		var texture = new Texture(new MemoryRead(data));

		Assert.Equal(60, texture.Palette[5].R);
		Assert.Equal(new byte[] { 60, 70, 80, 50, 60, 70, 80, 50, 60, 70, 80, 50, 60, 70, 80, 50 },
			texture.GetMipmapRgba(0));
	}

	[Fact]
	public void DecodesDxt1Blocks()
	{
		var block = new byte[]
		{
			0xFF, 0xFF, // colour 0: white
			0x00, 0x00, // colour 1: black
			0x00, 0x00, 0x00, 0x00 // every pixel uses colour 0
		};
		var data = TextureBuilder.Build(TextureFormat.Dxt1, 4, 4, block);

		var texture = new Texture(new MemoryRead(data));
		var rgba = texture.GetMipmapRgba(0);

		Assert.Equal(4 * 4 * 4, rgba.Length);
		for (var i = 0; i < 16; ++i)
		{
			Assert.Equal(new byte[] { 255, 255, 255, 255 }, rgba.Skip(i * 4).Take(4).ToArray());
		}
	}

	[Fact]
	public void DecodesDxt5AlphaBlocks()
	{
		var block = new byte[]
		{
			128, 0, 0, 0, 0, 0, 0, 0, // alpha: a0 = 128, every index 0
			0xFF, 0xFF, // colour 0: white
			0x00, 0x00, // colour 1: black
			0x00, 0x00, 0x00, 0x00 // every pixel uses colour 0
		};
		var data = TextureBuilder.Build(TextureFormat.Dxt5, 4, 4, block);

		var texture = new Texture(new MemoryRead(data));

		Assert.Equal(new byte[] { 255, 255, 255, 128 }, texture.GetMipmapRgba(0).Take(4).ToArray());
	}

	[Fact]
	public void RejectsAnInvalidSignature()
	{
		var data = TextureBuilder.Build(TextureFormat.R8G8B8A8, 1, 1, new byte[] { 1, 2, 3, 4 });
		data[0] = (byte)'X';

		Assert.Throws<TextureFormatException>(() => new Texture(new MemoryRead(data)));
	}
}
