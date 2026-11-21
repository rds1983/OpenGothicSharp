# Assets

Everything that reads Gothic data. Implemented today; extend as new formats come in.

## Virtual file system

- Every `*.vdf` under `<Gothic>/Data` is mounted recursively with ZenKit's `Vfs`.
- Mount order is `VfsOverwriteBehavior.Older`, so later archives win over earlier ones. This
  matches the original lookup so mods and patches override base data.
- The archive tree is flattened to a name → record index; `Assets.Keys` exposes the sorted list.
- Loaded objects are cached by name.
- Names are matched case-insensitively. Missing textures fall back to the `*-C.TEX` variant.

## Textures

`ZenKit.Texture` → `Texture2D`, all mip levels uploaded. `Dxt1` and `Dxt3` supported.

## Models

`.MDL` model scripts resolve their companions:

- `.MDH` — skeleton hierarchy, including root translation,
- `.MDM` — mesh (static, skinned, soft-skin weights),
- `.MMB` — morph mesh (used for heads),
- `.MAN` — animation clips, imported on demand into `AnimationClip`s sampled at the clip's FPS.

Skinned meshes become `VertexSkinned` (position, normal, UV, `Byte4` bone indices,
`Vector4` blend weights, max 4 influences). Weights are renormalized, inverse bind matrices
applied, and bone indices remapped per mesh through the hierarchy.

Only the clips actually wanted are imported — a model script may list hundreds.

## World

`.ZEN` parsing:

1. World mesh polygons are split into a fixed 4×4 `WorldGrid` of axis-aligned cells; each polygon
   is assigned to the first cell its bounding box overlaps.
2. Polygons are grouped per cell by material (deduplicated by texture), n-gons triangulated with a
   fan, vertices deduplicated per (feature, position) pair.
3. Ghost occluders, invisible portal polygons and texture-less materials are skipped.
4. `.MRM` multi-resolution meshes referenced by virtual objects are loaded.
5. The vob tree is walked (`RootObjects` → `Children`), composing rotation and position into a
   local-to-world transform. Each object is assigned to the cell its transformed bounding box
   overlaps.
6. Repeated meshes are batched into `InstancedMeshNode`s per cell; single meshes become plain
   `MeshNode`s with a baked transform.

This "flatten everything into static meshes" approach is a **stopgap** for making the world
visible. The long-term design is a real vob tree with typed objects (see `Porting.md` and the
vob tasks in [TASKS.md](../TASKS.md)).

## Known gaps

- No lights, no animated world objects, no particle effects, no sound, no water/sky.
- `oCItem`, `oCNpc`, `oCMob*`, triggers and other vob types are currently drawn as static meshes
  or ignored; they are not yet typed objects.
- Waynet is parsed by ZenKit but not consumed.
