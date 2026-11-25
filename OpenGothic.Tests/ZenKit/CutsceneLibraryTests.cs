using OpenGothic.ZenKit;
using System.Text;
using Xunit;

namespace OpenGothic.Tests.ZenKit;

public class CutsceneLibraryTests
{
	[Fact]
	public void ReadsBlocksMessagesAndNestedBlocks()
	{
		var data = BinSafeBuilder.Build(new BinSafeBuilder.Obj
		{
			Name = "%",
			ClassName = "zCCSLib",
			Fields =
			{
				BinSafeBuilder.Obj.Int("NumOfItems", 3)
			},
			Children =
			{
				Block("DIA_HELLO", "Hello there", "HELLO_01", 1),
				Block("DIA_BYE", "Goodbye", "BYE_01", 2),
				new BinSafeBuilder.Obj
				{
					Name = "%",
					ClassName = "zCCSBlock",
					Fields =
					{
						BinSafeBuilder.Obj.String("blockName", "DIA_NESTED"),
						BinSafeBuilder.Obj.Int("numOfBlocks", 1),
						BinSafeBuilder.Obj.Float("subBlock0", 0f)
					},
					Children =
					{
						new BinSafeBuilder.Obj
						{
							Name = "%",
							ClassName = "zCCSBlock",
							Fields =
							{
								BinSafeBuilder.Obj.String("blockName", "DIA_NESTED"),
								BinSafeBuilder.Obj.Int("numOfBlocks", 1),
								BinSafeBuilder.Obj.Float("subBlock0", 0f)
							},
							Children =
							{
								Atomic("Nested text", "NESTED_01", 3)
							}
						}
					}
				}
			}
		});

		var library = CutsceneLibrary.Load(new MemoryRead(data));

		Assert.Equal(3, library.Blocks.Count);
		Assert.Equal("DIA_BYE", library.Blocks[0].Name);
		Assert.Equal("DIA_HELLO", library.Blocks[1].Name);
		Assert.Equal("DIA_NESTED", library.Blocks[2].Name);

		var hello = library.GetBlock("DIA_HELLO");
		Assert.NotNull(hello);
		Assert.Equal("Hello there", hello.Message.Text);
		Assert.Equal("HELLO_01", hello.Message.Name);
		Assert.Equal(1u, hello.Message.Type);

		var nested = library.GetBlock("DIA_NESTED");
		Assert.NotNull(nested);
		Assert.Equal("Nested text", nested.Message.Text);

		Assert.Null(library.GetBlock("MISSING"));
	}

	private static BinSafeBuilder.Obj Block(string name, string text, string audio, uint type)
	{
		return new BinSafeBuilder.Obj
		{
			Name = "%",
			ClassName = "zCCSBlock",
			Fields =
			{
				BinSafeBuilder.Obj.String("blockName", name),
				BinSafeBuilder.Obj.Int("numOfBlocks", 1),
				BinSafeBuilder.Obj.Float("subBlock0", 0f)
			},
			Children =
			{
				Atomic(text, audio, type)
			}
		};
	}

	private static BinSafeBuilder.Obj Atomic(string text, string audio, uint type)
	{
		return new BinSafeBuilder.Obj
		{
			Name = "%",
			ClassName = "zCCSAtomicBlock",
			Children =
			{
				new BinSafeBuilder.Obj
				{
					Name = "%",
					ClassName = "oCMsgConversation:oCNpcMessage:zCEventMessage",
					Fields =
					{
						BinSafeBuilder.Obj.Enum("subType", type),
						BinSafeBuilder.Obj.String("text", text),
						BinSafeBuilder.Obj.String("name", audio)
					}
				}
			}
		};
	}
}
