# World items and inventory behavior

World items are ZEN `oCMobItem` vobs. They carry an item script instance (for example
`ITFO_SAUSAGE`), an amount, and usually **no visual**: the shipped records have
`show_visual = false` and an empty visual name. The reference's `Item::setVisual` draws the
item from its item definition rather than the vob's own visual flag.

## Rendering

The port renders explicit vob visuals as before. For items without one it looks for a
multi-resolution mesh named after the item instance:

```
instance  ITFO_SAUSAGE
mesh      ITFO_SAUSAGE.MRM
```

Items ignore the vob's `show_visual` gate because the flag is false for normal world items.
The mesh joins the existing per-cell multi-resolution batch, so many identical items share
draw calls. An item whose instance has no `.MRM` is still focusable and pickupable but has no
geometry; its ZEN box remains as the focus target.

The shipped `Amount` is zero on world items, so the port treats a missing amount as one.
Script-defined stack sizes arrive with the item/inventory milestone.

## Pickup

With an `ItemVob` under the crosshair, E (or `-interact`) does the following:

1. adds `ItemInstance` to the player's inventory, merging with an existing stack;
2. marks the vob `IsTaken` so focus and further uses skip it;
3. removes its rendered geometry from the grid — a direct `SceneNode`, or one instance of a
   batched `InstancedMeshNode`;
4. leaves the typed vob in the tree for queries and future save/load.

The inventory is stack-based: adding the same instance merges counts, and `Remove` reduces a
stack or drops it at zero. The full item definitions, equipment and UI come with I3.

## Presentation

A minimal bottom-left `InventoryPanel` lists the stacks (`(empty)` before the first pickup)
and is refreshed only on a pickup, not per frame. The focus overlay names the item instance,
and the interaction log reports the pickup and total inventory count.

## Known limits

- Instance names such as `ITFO_SAUSAGE` are shown until the Daedalus item definitions supply
  localized names and icons.
- There is no drop, use, equip or trade path, and no weight/stack limits.
- Items without any instance mesh are invisible.
- Pickup does not notify scripts or play a sound; those hooks arrive with G2/J1.
