# ZEN vob transforms

## Purpose

A ZEN file stores virtual objects (vobs) as a tree. Each vob has a visual, a rotation, a
position, and children. This note records where those transforms live in the data, because it
decides whether a child has to be composed with its parent when the world is built.

## What the data holds

The transform on a vob is already in **world space**, for parents and children alike. A vob at
the origin can have children tens of thousands of units away, which cannot be a local offset:

```
root 'NEWWORLD_PART_XARDAS_01.3DS' @ (0.0, 0.0, 0.0)
  child 'INNOS_BELIAR_ADDON_01.ASC' @ (29655.4, 5924.1, -14564.9)
```

Children near their parent exist too (a torch's flame and particles sit a few units above it),
so the data cannot be told apart by looking at one pair; the origin-relative example settles
it.

## Consequence for a loader

Using the stored transform directly is correct for a static world, and that is why a naive
loader looks right.

The tree matters only when something moves. To support that without duplicating state, derive
a local transform once per vob:

```
local = world * inverse(parentWorld)
```

and compose it again when the node is placed:

```
world = local * parentWorld
```

The two operations round-trip: over the 1,870 parented vobs of `newworld.zen` the largest
recomposition error is 0.0125 units, invisible in a world that spans tens of thousands.

A vob with children but no visual of its own still needs a node for the children to hang from.
Multi-resolution mesh vobs are recorded as world-space instance matrices, so they do not take
part in the hierarchy; no mover uses one as a child.

## Open questions

- A moving parent invalidates the child's load-time cell assignment and registered bounds.
  Whatever moves vobs has to re-register them; this is why hierarchy support lands before
  movers (doors, levers) rather than with them.
