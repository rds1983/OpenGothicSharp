using System;
using System.Collections.Generic;
using System.IO;

namespace OpenGothic.ZenKit;

/// <summary>A single cutscene conversation message.</summary>
public sealed class ConversationMessageEvent
{
	public uint Type { get; internal set; }

	public string Text { get; internal set; } = string.Empty;

	public string Name { get; internal set; } = string.Empty;

	public bool HighPriority { get; internal set; }

	public bool Used { get; internal set; }

	public bool Deleted { get; internal set; }
}

/// <summary>One named entry of a cutscene library.</summary>
public sealed class CutsceneBlock
{
	public string Name { get; internal set; } = string.Empty;

	public ConversationMessageEvent Message { get; internal set; }
}

/// <summary>
/// Managed reader for the output-unit cutscene library (<c>Ou.dat</c>). This is a C# port of
/// ZenKit's <c>CutsceneLibrary</c>; the ZenGin BIN_SAFE archive itself is read by
/// <see cref="ZenArchive"/>, and this type maps its <c>zCCSLib</c> tree to typed messages.
/// </summary>
public sealed class CutsceneLibrary
{
	private readonly Dictionary<string, CutsceneBlock> _byName =
		new Dictionary<string, CutsceneBlock>(StringComparer.Ordinal);

	public List<CutsceneBlock> Blocks { get; } = new List<CutsceneBlock>();

	public static CutsceneLibrary Load(string path)
	{
		return Load(new MemoryRead(File.ReadAllBytes(path)));
	}

	public static CutsceneLibrary Load(Read reader)
	{
		var archive = ZenArchive.Open(reader);
		var root = archive.ReadObject();
		if (root == null || !root.ClassName.StartsWith("zCCSLib", StringComparison.Ordinal))
		{
			throw new ZenArchiveException("Cutscene library root object is missing.");
		}

		var library = new CutsceneLibrary();
		var expected = root.GetInt("NumOfItems", root.Children.Count);
		if (expected != root.Children.Count)
		{
			throw new ZenArchiveException(
				$"Cutscene library declares {expected} blocks but contains {root.Children.Count}.");
		}

		foreach (var child in root.Children)
		{
			if (!child.ClassName.StartsWith("zCCSBlock", StringComparison.Ordinal))
			{
				throw new ZenArchiveException($"Unexpected cutscene block class '{child.ClassName}'.");
			}

			library.Blocks.Add(ReadBlock(child));
		}

		library.Blocks.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
		foreach (var block in library.Blocks)
		{
			library._byName[block.Name] = block;
		}

		return library;
	}

	public CutsceneBlock GetBlock(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}

		return _byName.TryGetValue(name, out var block) ? block : null;
	}

	private static CutsceneBlock ReadBlock(ZenObject obj)
	{
		var block = new CutsceneBlock
		{
			Name = obj.GetString("blockName")
		};

		var blockCount = obj.GetInt("numOfBlocks", 1);
		if (blockCount != 1)
		{
			throw new ZenArchiveException(
				$"Expected one block for '{block.Name}' but got {blockCount}.");
		}

		var child = obj.Children.Count > 0 ? obj.Children[0] : null;
		block.Message = FindMessage(child);
		return block;
	}

	private static ConversationMessageEvent FindMessage(ZenObject obj)
	{
		while (obj != null)
		{
			if (obj.ClassName.StartsWith("zCCSAtomicBlock", StringComparison.Ordinal))
			{
				foreach (var child in obj.Children)
				{
					if (child.ClassName.StartsWith("oCMsgConversation", StringComparison.Ordinal))
					{
						return ReadMessage(child);
					}
				}

				return null;
			}

			if (obj.ClassName.StartsWith("zCCSBlock", StringComparison.Ordinal))
			{
				obj = obj.Children.Count > 0 ? obj.Children[0] : null;
				continue;
			}

			return obj.ClassName.StartsWith("oCMsgConversation", StringComparison.Ordinal)
				? ReadMessage(obj)
				: null;
		}

		return null;
	}

	private static ConversationMessageEvent ReadMessage(ZenObject obj)
	{
		return new ConversationMessageEvent
		{
			Type = obj.GetUInt("subType"),
			Text = obj.GetString("text"),
			Name = obj.GetString("name"),
			HighPriority = obj.GetBool("highPriority"),
			Used = obj.GetBool("inUse"),
			Deleted = obj.GetBool("deleted")
		};
	}
}
