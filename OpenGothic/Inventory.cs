using System;
using System.Collections.Generic;

namespace OpenGothic;

/// <summary>One stack in the player's inventory.</summary>
public sealed class InventoryStack
{
	internal InventoryStack(string name, int count)
	{
		Name = name;
		Count = count;
	}

	public string Name { get; }

	public int Count { get; internal set; }

	public override string ToString() => Count > 1 ? $"{Name} x{Count}" : Name;
}

/// <summary>
/// Minimal item list for milestone E4. Stacks merge by name so a pile of arrows is one row;
/// the full equipment/inventory UI and item definitions arrive with I3.
/// </summary>
public sealed class Inventory
{
	private readonly List<InventoryStack> _stacks = new();

	public IReadOnlyList<InventoryStack> Stacks => _stacks;

	/// <summary>Sum of all stack counts.</summary>
	public int TotalCount { get; private set; }

	public void Add(string name, int count)
	{
		if (string.IsNullOrEmpty(name))
		{
			throw new ArgumentException("An inventory item needs a name.", nameof(name));
		}

		if (count <= 0)
		{
			return;
		}

		foreach (var stack in _stacks)
		{
			if (string.Equals(stack.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				stack.Count += count;
				TotalCount += count;
				return;
			}
		}

		_stacks.Add(new InventoryStack(name, count));
		TotalCount += count;
	}

	/// <summary>
	/// Removes up to <paramref name="count"/> of an item. Returns false and leaves the list
	/// untouched when the inventory does not hold that many.
	/// </summary>
	public bool Remove(string name, int count)
	{
		if (string.IsNullOrEmpty(name) || count <= 0)
		{
			return false;
		}

		for (var i = 0; i < _stacks.Count; ++i)
		{
			var stack = _stacks[i];
			if (!string.Equals(stack.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (stack.Count < count)
			{
				return false;
			}

			stack.Count -= count;
			TotalCount -= count;
			if (stack.Count == 0)
			{
				_stacks.RemoveAt(i);
			}

			return true;
		}

		return false;
	}
}
