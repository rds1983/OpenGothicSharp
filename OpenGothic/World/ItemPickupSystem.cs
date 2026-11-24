using OpenGothic.Vobs;
using System;

namespace OpenGothic.Items;

/// <summary>
/// World item pickup. The item is added to the minimal inventory, marked taken so focus and
/// further uses ignore it, and its rendered geometry is removed from the grid. The typed vob
/// stays in the tree for queries and future save/load.
/// </summary>
public sealed class ItemPickupSystem
{
	public string PickUp(ItemVob item, WorldGrid world, Inventory inventory)
	{
		if (item == null)
		{
			throw new ArgumentNullException(nameof(item));
		}

		if (inventory == null)
		{
			throw new ArgumentNullException(nameof(inventory));
		}

		if (item.IsTaken)
		{
			return "already taken";
		}

		var name = string.IsNullOrEmpty(item.ItemInstance) ? "item" : item.ItemInstance;
		var count = Math.Max(1, item.Amount);
		inventory.Add(name, count);

		item.IsTaken = true;
		var removed = world?.RemoveVobVisual(item) ?? false;

		var text = $"picked up '{name}' x{count} (inventory {inventory.TotalCount})";
		if (!removed)
		{
			text += "; visual not found";
		}

		return text;
	}
}
