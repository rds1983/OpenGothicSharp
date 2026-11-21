# Rendering

## Policy

1. **Stock MonoGame effects only** (`BasicEffect`, `SkinnedEffect`, and friends). Do not invest in
   shader work now. When the gameplay is in place we write our own effects.
2. **Visual parity is the lowest priority.** Gothic-accurate lighting, fog, water, sky, decals and
   post effects come last.
3. **Performance work is deferred**, with one exception: **frustum culling** is done from day one
   because it is cheap and keeps the world viewer interactive.

## Current renderer

`OpenGothic.Console/WorldViewer.cs`:

- `BasicEffect` with default lighting and fog for world geometry.
- `SkinnedEffect` for the hero; bone transforms from DigitalRiseModel's `AnimationController`.
- Per-cell frustum test on `WorldGridCell.BoundingBox`, then per-node / per-instance
  `BoundingSphere` tests. This is the "one performance improvement" we allow early.
- Opaque rendering only. No alpha blending, no shadows, no lighting from the world.

## Planned

| Stage | What | Notes |
|---|---|---|
| Now | Frustum culling | per cell, then per node/instance |
| Later | Real instanced rendering | instances are still drawn one by one |
| Later | Own effect library | replaces stock effects |
| Later | Occlusion culling | after gameplay |
| Later | Shadow maps, lights from `zCVobLight`, sky/fog/water | parity work |

## Rules for adding draws

- New draw paths go through a `SceneNode`-like abstraction in `Common`, not ad-hoc code in the
  console app.
- Culling belongs to the renderer and must be reusable by any host.
