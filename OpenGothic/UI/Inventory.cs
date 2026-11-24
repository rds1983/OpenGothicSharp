using System.Text;

namespace OpenGothic.UI;

public partial class InventoryPanel
{
	public InventoryPanel()
	{
		BuildUI();
	}

	/// <summary>Replaces the item list; called when an item is picked up, not per frame.</summary>
	public void Refresh(OpenGothic.Inventory inventory)
	{
		var text = new StringBuilder();
		if (inventory != null)
		{
			foreach (var stack in inventory.Stacks)
			{
				if (text.Length > 0)
				{
					text.Append('\n');
				}

				text.Append(stack);
			}
		}

		_labelItems.Text = text.Length == 0 ? "(empty)" : text.ToString();
	}
}
