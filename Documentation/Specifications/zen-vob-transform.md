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

The **vob tree is the only transform hierarchy**, as in the reference. Each `VobNode` keeps the
world transform from the file and derives a local one against its parent:

```
local = world * inverse(parentWorld)
```

The renderer does not compose a second hierarchy: a `SceneNode` is a flat visual attached to its
vob (or to the level mesh) and stores the **world** matrix. Batched multi-resolution meshes are
world-space instance matrices in the cell's visual list, not nodes in the vob tree. A moving
parent therefore has to update its children's `VobNode.World` and the attached visual matrices;
static worlds need no such update.

A vob with children but no visual of its own is still a `VobNode`, so the children hang from it
in the gameplay tree. The old empty `SceneNode` placeholder is no longer needed.

## Rotation and the right-handed world

The vob transform is `rotation * translation`, but the rotation needs the same one-time
left-to-right-handed conversion as positions. ZenKit exposes the rotation as a column-major
`Mat3`; the reference passes its columns straight to a column-vector transform. XNA transforms
row vectors, so `Mathematics.ToXna(Matrix3x3)` transposes it and mirrors Z:

```
R_xna = L * transpose(R_zenkit) * L        L = diag(1, 1, -1)
```

Position becomes `(x, y, -z)`. This is exactly the same conversion as `Matrix4x4.ToXna`, and it
is a no-op for a pure yaw (the common case), so yaw-only props were already correct; objects
with pitch or roll, such as leaning stones, need the transpose and were previously oriented
wrong.

## Visibility and collision flags

The reference's `ObjVisual::setVisual` uses two vob flags:

- `show_visual` — when false, the visual is not added to the scene at all. The `VobNode` still
  exists so children and gameplay queries keep the full tree.
- `cd_dynamic` — the vob collides with the player only when this is set (together with
  `show_visual`). The reference does not create a physics mesh for a hidden or non-dynamic
  vob, so the player can walk through decorative geometry and invisible markers.

`Assets.World.cs` follows both rules: it gates the model/mesh branches on `ShowVisual`, sets
`ModelNode.Collision` and each `InstancedMeshNode` instance's collision from `CdDynamic`, and
`PhysicsWorld.CollectSources` honours those flags. This dropped the harbour's collectable vob
sources from 13.6k to 8.8k while keeping the walkable set, matching the reference.

`VisualMesh` vobs in `newworld.zen` are the 16 `NEWWORLD_PART_*.3DS` level-composition roots at
the origin, already represented by the merged level mesh. `VisualMorphMesh` (14 fish/skate),
`VisualParticleEffect` (666) and `VisualDecal` (1405) are not rendered yet; they are effects or
animated morphs, not static placement.

## Model root translation

A `.MDL`/`.MDH` hierarchy carries a `root_translation` (and the root node carries a local
transform). For a human actor it lifts the bind pose so its feet reach the vob origin: the mesh
is authored around the hips. `Skeleton` adds that translation to the root node in the
reference.

For a **vob prop** the reference deliberately removes it again:
`ObjVisual::setVisual` calls `MdlVisual::setYTranslationEnable(false)`, which sets
`Pose::NoTranslation`. `Pose::mkBaseTranslation` then subtracts the root node's X/Y/Z, so the
prop's raw model bounds sit exactly on the vob position. An actor keeps the translation because
it is driven through `MdlVisual` directly.

Our model loader stores the hierarchy root translation on the model's `DrModelTag`; the raw mesh
stays raw. The reference cancels X and Z for every model, and props cancel Y too.
`Assets.World.cs` uses the vob transform directly for props, which matches the reference's
`NoTranslation` result:

```
prop geometry = vobWorld * rawModelBounds
```

Actor-like callers (the diagnostic `-model`/`-compose` spawns, and later NPCs) add the stored
Y in model space:

```
actor world = Translate(0, rootTranslationY, 0) * vobWorld
```

X/Z stay on the tag for later placement work that has to reproduce the reference's
cancellation exactly; they are not applied today.

That is why the harbour bookstand's vob at `y = -53.4` renders with its base at the floor
(`y ≈ -187`) instead of `133.6` units above it. Its raw collision triangles use the same vob
transform, so the visual and collision stay together. NPC models will add the translation at
their spawn; their collision is the dynamic capsule from milestone E rather than the static
bind-pose mesh.

## Open questions

- A moving parent invalidates the child's load-time cell assignment, registered bounds and
  attached visual matrices. A mover has to update `VobNode.World` for the subtree and the
  world-space `SceneNode.Transform` of each visual; this is why hierarchy support lands before
  movers (doors, levers) rather than with them.
