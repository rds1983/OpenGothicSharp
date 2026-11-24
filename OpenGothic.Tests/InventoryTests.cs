using Xunit;

namespace OpenGothic.Tests;

public class InventoryTests
{
	[Fact]
	public void Add_MergesStacksByName()
	{
		var inventory = new Inventory();

		inventory.Add("ItFo_Apple", 2);
		inventory.Add("itfo_apple", 3);
		inventory.Add("ItMi_Sword", 1);

		Assert.Equal(2, inventory.Stacks.Count);
		Assert.Equal(6, inventory.TotalCount);
		Assert.Equal(5, inventory.Stacks[0].Count);
	}

	[Fact]
	public void Remove_ReducesAndDropsEmptyStacks()
	{
		var inventory = new Inventory();
		inventory.Add("ItFo_Apple", 5);

		Assert.True(inventory.Remove("ItFo_Apple", 2));
		Assert.Equal(3, inventory.TotalCount);
		Assert.Single(inventory.Stacks);

		Assert.True(inventory.Remove("ItFo_Apple", 3));
		Assert.Empty(inventory.Stacks);
		Assert.Equal(0, inventory.TotalCount);
	}

	[Fact]
	public void Remove_RejectsMissingOrInsufficientStacks()
	{
		var inventory = new Inventory();
		inventory.Add("ItFo_Apple", 2);

		Assert.False(inventory.Remove("ItMi_Sword", 1));
		Assert.False(inventory.Remove("ItFo_Apple", 3));
		Assert.Equal(2, inventory.TotalCount);
	}
}
