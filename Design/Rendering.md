# Rendering

## Policy

1. **Stock MonoGame effects only** (`BasicEffect`, `SkinnedEffect`, and friends). Do not invest in
   shader work now. When the gameplay is in place we write our own effects.
2. **Visual parity is the lowest priority.** Gothic-accurate lighting, fog, water, sky, decals and
   post effects come last.
3. **Performance work is deferred**, with one exception: **frustum culling** is done from day one
   because it is cheap and keeps the world viewer interactive.

## Current renderer

`OpenGothic.Common/Rendering/ForwardRenderer.cs` owns all low-level 3D drawing:

- Holds the effects we draw with: `BasicEffect` for static geometry, `SkinnedEffect` for skinned
  geometry. `AlphaTestEffect` will join them when materials can request it. Hosts never touch
  effects directly.
- `DrawMeshPart(part, material, world, boneTransforms)` picks the effect from `part.Skin` (null →
  `BasicEffect`, otherwise `SkinnedEffect`), binds the texture/diffuse colour and issues the indexed
  draw. A non-null `material` overrides the part's own material; passing `null` uses
  `part.Material`. `boneTransforms` is the bone palette for the part and is required for skinned
  parts — passing `null` for one throws `ArgumentNullException` rather than silently drawing in the
  bind pose. It is ignored for static parts.
- Applies the frame-wide render states in `BeginFrame()` (depth, opaque blend, back-face culling,
  linear sampler) along with view/projection and fog on both effects.
- Collects frame statistics: `DrawCalls` and `DrawnTriangles`, reset by `BeginFrame()`.
- Clearing the back buffer stays with the host, because it owns the sky colour.


The viewer (`OpenGothic.Console/WorldViewer.cs`) only walks the scene, culls, and calls
`DrawMeshPart`:

- Per-cell frustum test on `WorldGridCell.BoundingBox`, then per-node / per-instance
  `BoundingSphere` tests. This is the "one performance improvement" we allow early.
- Opaque rendering only. No alpha blending, no shadows, no lighting from the world.

## Coordinate space

All geometry reaching this layer is already right-handed MonoGame space — the loaders convert
Gothic's left-handed data once, at load time (see
[Architecture.md](Architecture.md#coordinates-gothic-lhs--monogame-rhs)). The renderer must not
apply any further axis swaps or UV flips; if a model looks mirrored, the bug is in a loader.

## Planned

| Stage | What | Notes |
|---|---|---|
| Now | Frustum culling | per cell, then per node/instance |
| Later | Real instanced rendering | instances are still drawn one by one |
| Later | Own effect library | replaces stock effects |
| Later | Occlusion culling | after gameplay |
| Later | Shadow maps, lights from `zCVobLight`, sky/fog/water | parity work |

## Rules for adding draws

- All low-level drawing goes through `ForwardRenderer`. Hosts pass meshes and transforms; they do
  not set effects, vertex buffers or render states themselves.
- New draw paths go through a `SceneNode`-like abstraction in `Common`, not ad-hoc code in the
  console app.
- Culling belongs to the renderer and must be reusable by any host.
