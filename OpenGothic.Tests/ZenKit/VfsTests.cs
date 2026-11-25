using OpenGothic.ZenKit;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace OpenGothic.Tests.ZenKit;

public class VfsTests
{
	private static byte[] Text(string value) => Encoding.ASCII.GetBytes(value);

	[Fact]
	public void MountDiskBuildsTheTree()
	{
		var data = VdfBuilder.Build(
			("DATA/TEXTURES/A.TEX", Text("alpha")),
			("DATA/ROOT.TXT", Text("root")));

		using var vfs = new Vfs();
		vfs.MountDisk(data);

		var root = vfs.Resolve("DATA");
		Assert.NotNull(root);
		Assert.Equal(VfsNodeType.Directory, root.Type);
		Assert.Equal(new[] { "ROOT.TXT", "TEXTURES" },
			root.Children.Select(c => c.Name).OrderBy(n => n).ToArray());

		var texture = vfs.Resolve("DATA/TEXTURES/A.TEX");
		Assert.NotNull(texture);
		Assert.Equal(VfsNodeType.File, texture.Type);
		Assert.Equal("alpha", Encoding.ASCII.GetString(texture.ReadAllBytes()));

		var text = vfs.Resolve("DATA/ROOT.TXT");
		Assert.Equal("root", Encoding.ASCII.GetString(text.ReadAllBytes()));
	}

	[Fact]
	public void ResolveIsCaseInsensitive()
	{
		var data = VdfBuilder.Build(("DATA/Textures/A.TEX", Text("alpha")));

		using var vfs = new Vfs();
		vfs.MountDisk(data);

		Assert.NotNull(vfs.Resolve("data/textures/a.tex"));
		Assert.Equal("A.TEX", vfs.Resolve("DATA/TEXTURES/A.TEX").Name);
	}

	[Fact]
	public void FindSearchesNestedDirectories()
	{
		var data = VdfBuilder.Build(("DATA/TEXTURES/A.TEX", Text("alpha")));

		using var vfs = new Vfs();
		vfs.MountDisk(data);

		Assert.NotNull(vfs.Find("a.tex"));
		Assert.Equal("A.TEX", vfs.Find("a.tex").Name);
		Assert.Null(vfs.Find("missing.tex"));
	}

	[Fact]
	public void MissingPathsResolveToNull()
	{
		using var vfs = new Vfs();
		vfs.MountDisk(VdfBuilder.Build(("A.TXT", Text("a"))));

		Assert.Null(vfs.Resolve("A.TXT/nested"));
		Assert.Null(vfs.Resolve("B.TXT"));
	}

	[Fact]
	public void DirectoryChildrenAreMergedWhenMountsOverlap()
	{
		var first = VdfBuilder.Build(("DATA/A.TXT", Text("a")));
		var second = VdfBuilder.Build(("DATA/B.TXT", Text("b")));

		using var vfs = new Vfs();
		vfs.MountDisk(first, VfsOverwriteBehavior.All);
		vfs.MountDisk(second, VfsOverwriteBehavior.All);

		Assert.Equal("a", Encoding.ASCII.GetString(vfs.Resolve("DATA/A.TXT").ReadAllBytes()));
		Assert.Equal("b", Encoding.ASCII.GetString(vfs.Resolve("DATA/B.TXT").ReadAllBytes()));
	}

	[Theory]
	[InlineData(VfsOverwriteBehavior.None, "old")]
	[InlineData(VfsOverwriteBehavior.All, "new")]
	[InlineData(VfsOverwriteBehavior.Newer, "old")]
	[InlineData(VfsOverwriteBehavior.Older, "new")]
	public void OverwriteBehaviorFollowsTheReference(VfsOverwriteBehavior behavior, string expected)
	{
		var oldArchive = VdfBuilder.Build(new DateTime(2020, 1, 1), ("A.TXT", Text("old")));
		var newArchive = VdfBuilder.Build(new DateTime(2021, 1, 1), ("A.TXT", Text("new")));

		using var vfs = new Vfs();
		vfs.MountDisk(oldArchive, VfsOverwriteBehavior.All);
		vfs.MountDisk(newArchive, behavior);

		var node = vfs.Resolve("A.TXT");
		Assert.NotNull(node);
		Assert.Equal(expected, Encoding.ASCII.GetString(node.ReadAllBytes()));
	}

	[Fact]
	public void MkdirAndRemoveWorkOnTheTree()
	{
		using var vfs = new Vfs();
		vfs.MountDisk(VdfBuilder.Build(("A.TXT", Text("a"))));

		var directory = vfs.Mkdir("/DATA/TEXTURES");
		Assert.Equal("TEXTURES", directory.Name);
		Assert.NotNull(vfs.Resolve("DATA/TEXTURES"));

		Assert.True(vfs.Remove("DATA/TEXTURES"));
		Assert.Null(vfs.Resolve("DATA/TEXTURES"));
	}

	[Fact]
	public void InvalidSignatureThrows()
	{
		var data = VdfBuilder.Build(("A.TXT", Text("a")));
		Array.Fill(data, (byte)'X', 256, 16);

		using var vfs = new Vfs();
		Assert.Throws<VfsBrokenDiskException>(() => vfs.MountDisk(data));
	}

	[Fact]
	public void MountDiskMapsAFile()
	{
		var path = Path.Combine(Path.GetTempPath(), $"opengothic-vfs-{Guid.NewGuid():N}.vdf");
		File.WriteAllBytes(path, VdfBuilder.Build(("DATA/A.TXT", Text("mapped"))));

		try
		{
			using var vfs = new Vfs();
			vfs.MountDisk(path);

			var node = vfs.Resolve("DATA/A.TXT");
			Assert.NotNull(node);
			Assert.Equal("mapped", Encoding.ASCII.GetString(node.ReadAllBytes()));
		}
		finally
		{
			File.Delete(path);
		}
	}
}
