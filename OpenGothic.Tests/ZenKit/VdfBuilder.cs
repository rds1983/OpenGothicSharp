using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenGothic.Tests.ZenKit;

/// <summary>
/// Builds a small Union VDF in memory so the managed VFS can be tested without any game
/// data. Paths use <c>/</c> separators.
/// </summary>
internal static class VdfBuilder
{
	private const int HeaderSize = 296;
	private const int CatalogEntrySize = 80;

	public static readonly DateTime DefaultTime =
		new DateTime(2024, 5, 6, 7, 8, 10, DateTimeKind.Local);

	private sealed class Node
	{
		public string Name;
		public byte[] Data;
		public List<Node> Children;
	}

	private sealed class Entry
	{
		public Node Node;
		public int ChildStart;
		public int Offset;
		public bool Last;
	}

	public static byte[] Build(params (string Path, byte[] Data)[] files) => Build(DefaultTime, files);

	public static byte[] Build(DateTime timestamp, params (string Path, byte[] Data)[] files)
	{
		var roots = new List<Node>();
		foreach (var (path, data) in files)
		{
			var parts = path.Split('/');
			var level = roots;
			for (var i = 0; i < parts.Length; ++i)
			{
				var name = parts[i];
				var node = level.FirstOrDefault(n => n.Name == name);
				if (node == null)
				{
					node = new Node { Name = name };
					level.Add(node);
				}

				if (i == parts.Length - 1)
				{
					node.Data = data ?? Array.Empty<byte>();
				}
				else
				{
					node.Children ??= new List<Node>();
					level = node.Children;
				}
			}
		}

		var catalog = new List<Entry>();

		int Assign(List<Node> nodes)
		{
			var start = catalog.Count;
			foreach (var node in nodes)
			{
				catalog.Add(new Entry { Node = node });
			}

			for (var i = 0; i < nodes.Count; ++i)
			{
				catalog[start + i].Last = i == nodes.Count - 1;
				if (nodes[i].Children != null)
				{
					catalog[start + i].ChildStart = Assign(nodes[i].Children);
				}
			}

			return start;
		}

		Assign(roots);

		var dataOffset = HeaderSize + catalog.Count * CatalogEntrySize;
		var fileCount = 0;
		foreach (var entry in catalog)
		{
			if (entry.Node.Children == null)
			{
				entry.Offset = dataOffset;
				dataOffset += entry.Node.Data.Length;
				++fileCount;
			}
			else
			{
				entry.Offset = entry.ChildStart;
			}
		}

		var totalSize = dataOffset;

		using var stream = new MemoryStream(totalSize);
		using var writer = new BinaryWriter(stream);

		var comment = new byte[256];
		Array.Fill(comment, (byte)0x1A);
		var commentText = Encoding.ASCII.GetBytes("managed vfs test");
		commentText.CopyTo(comment, 0);
		writer.Write(comment);

		writer.Write(Encoding.ASCII.GetBytes("PSVDSC_V2.00\n\r\n\r"));
		writer.Write((uint)catalog.Count);
		writer.Write((uint)fileCount);
		writer.Write(ToDosTime(timestamp));
		writer.Write((uint)totalSize);
		writer.Write((uint)HeaderSize);
		writer.Write(0x50u);

		foreach (var entry in catalog)
		{
			var name = new byte[64];
			Array.Fill(name, (byte)' ');
			Encoding.ASCII.GetBytes(entry.Node.Name).CopyTo(name, 0);
			writer.Write(name);
			writer.Write((uint)entry.Offset);
			writer.Write((uint)(entry.Node.Children == null ? entry.Node.Data.Length : 0));

			var type = entry.Node.Children == null ? 0u : 0x80000000u;
			if (entry.Last)
			{
				type |= 0x40000000u;
			}

			writer.Write(type);
			writer.Write(0u);
		}

		foreach (var entry in catalog)
		{
			if (entry.Node.Children == null)
			{
				writer.Write(entry.Node.Data);
			}
		}

		return stream.ToArray();
	}

	private static uint ToDosTime(DateTime time)
	{
		return (uint)(time.Year - 1980) << 25 |
		       (uint)time.Month << 21 |
		       (uint)time.Day << 16 |
		       (uint)time.Hour << 11 |
		       (uint)time.Minute << 5 |
		       (uint)(time.Second / 2);
	}
}
