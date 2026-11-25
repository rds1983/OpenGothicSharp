# Model format (`.MDL`, `.MDH`, `.MDM`)

How static and skinned models are stored and how they have to be assembled before a stock
MonoGame `SkinnedEffect` can draw them. Written from the parsed structure and from measuring
the shipped data, not from any binary.

## Purpose

A model is a skeleton plus one or more meshes. Two on-disk shapes exist:

| Shape | Contents | Notes |
| --- | --- | --- |
| `.MDL` | hierarchy **and** mesh in one file | Self-contained. Several objects have only this. |
| `.MDH` + `.MDM` | hierarchy in the former, mesh in the latter | Skeleton/mesh split, often shared. |

A visual object in a level names a model in the **authoring** format, which is not the file
on disk: `.ASC` and `.MDS` names both resolve to the same model. Only the stem is
meaningful, so a loader resolves `STEM.MDL` first and falls back to `STEM.MDH` +
`STEM.MDM`. In the shipped `newworld.zen`, all 63 distinct visual model names resolve this
way — 51 through `.MDL`, 12 through `.MDH`/`.MDM`, none unresolved.

Animation does not live in these files at all: a model carries geometry and a skeleton, and
the clips that drive it are listed by a separate model script and stored one `.MAN` file
per clip. See [gothic-animation-format.md](gothic-animation-format.md).

## Hierarchy

The hierarchy is a flat array of nodes; each node stores a name, its parent index (-1 for
the root) and a local transform (translation, rotation, scale).

Observed on all 63 `newworld.zen` models:

- exactly one root, always at index 0;
- every parent index is **smaller** than the child's index, i.e. the array is already in
  parents-first order — no sorting or tree walking is needed;
- node names are unique within a model;
- the root node's own translation is always zero. The hierarchy also carries a separate
  **root translation** offset, and that one is generally non-zero.

### Root translation

For actor-shaped models the bind pose is authored around the hips, so the mesh extends
below y = 0. The hierarchy's root translation lifts the model so its lowest point sits on
the model origin, and `rootTranslation.Y ≈ -bboxMinY` to within a couple of units:

| model | root translation Y | bbox min Y | sum |
| --- | --- | --- | --- |
| `ARMOR_PRISONER` | 98.149 | -98.586 | -0.437 |
| `ORC_BODY` | 100.39 | -99.383 | 1.007 |
| `GIANT_RAT_BODY` | 31.717 | -30.106 | 1.611 |
| `SHEEP_BODY` / `HAMMEL_BODY` | 67.774 | -67.188 | 0.586 |

Hand-authored prop models (benches, beds, chests) have a zero root translation and a
bounding box that already starts at y = 0.

The consequence is a placement rule, not a rendering one: apply the root translation to
the **default pose of the root bone**, and the same bone plus skin data then places both
actors and props correctly with feet/base on the object origin. Applying it to a bind
matrix used for skinning instead double-counts it.

A measured check: with the offset on the root bone, `ARMOR_PRISONER` occupies
`y ∈ [-0.44, 165.6]` and `SHEEP_BODY` `y ∈ [0.59, 95.6]`; without it, both sink by their
root-translation amount. Props are identical either way because their offset is zero.

## Absolute bind transforms

A weight position is expressed in its own bone's space, so producing a model-space vertex
requires that bone's **absolute bind transform**. Building it is a single pass in hierarchy
order, because parents precede children:

```
absolute[0] = local[0]
absolute[i] = local[i] * absolute[parent[i]]
```

(`local * parent` in row-vector convention, matching the vertex transform
`Vector3.Transform(position, absolute)`.)

This order is not a matter of taste — the opposite order diverges immediately. Measured on
`ARMOR_PRISONER`'s 183 weighted vertices, "each vertex's weights all resolve to a single
point" holds with spread **0.0** for `local * parent` and fails with average spread 23.3
(max 229.4) for `parent * local`. The reconstruction can also be checked against the
hierarchy's own bounding box: with the correct order it reproduces it exactly (modulo the
Z mirror), so the two independent descriptions of the model agree.

## Skinned meshes

A skinned mesh stores, per vertex, a small list of (bone index, weight, position) tuples.
Position is in that bone's space; the weights per vertex sum to 1, and at most **3** are
used in practice (the shader format provides 4 slots).

Two traps:

- **The bone index is a hierarchy node index**, not an index into the mesh's own node list.
  Both are usually closely related but are not the same numbering.
- **The mesh's own vertex position array is unusable** for skinned meshes — its values are
  not positions (bounding boxes come out around 1e22–1e33). The bind position must be
  rebuilt from the weights: transform one weight's position by its bone's absolute bind
  transform. All weights of a vertex agree, so the first is enough; blending them would
  only add rounding noise.
- Wedge normals and texture coordinates are still usable as stored. Their indices address
  the weight array, so a wedge index selects a weight entry.

Blend indices handed to a GPU skin must address the **skin's joint array**, so a loader
needs a translation from hierarchy node index to joint slot. Building that joint list from
the mesh's node list first and then appending any remaining weight nodes covers everything
a weight can mention.

Measured joint counts across `newworld.zen`'s models: at most **36 joints per mesh** and at
most **47 bones total**. A stock `SkinnedEffect` supports 72, so no model here needs a
custom effect.

## Static attachments

A mesh file may also carry **attachment** meshes, each named after the hierarchy node that
positions it. They are ordinary rigid geometry with their own positions, wedge normals and
UVs.

All 93 attachments in `newworld.zen`'s models match a node name, and node names are unique,
so the mapping is unambiguous. Most of those nodes carry a real transform, which is why the
attachment must be attached to the node rather than assumed to sit at the origin.

A model can have both kinds at once (e.g. `BOOK_BLUE` has one attachment plus one skinned
mesh), so a loader must support a model whose geometry is partly rigid and partly skinned.

## Composed models

An actor's visible model is not always one file. The shipped data splits it:

| File | Contents | Bone count |
| --- | --- | --- |
| `HUMANS.MDH` | the human skeleton, including the nine `ZS_*` attach nodes | 34 |
| `HUM_BODY_NAKED0.MDM` | the undressed body mesh, no hierarchy | mesh weights address nodes 0–32 |
| `ARMOR_PRISONER.MDL` | one armour, hierarchy and mesh | 25 |

The body and every armour are driven by `HUMANS.MSB`, whose skeleton name is
`HUM_BODY_NAKED0`, but neither carries the `ZS_*` nodes: only the shared hierarchy does. A
model that needs those attach points is therefore assembled from an explicit hierarchy plus
one or more meshes.

Two facts make that possible:

- A bare `.MDM`'s weights already address the shared skeleton. `HUM_BODY_NAKED0`'s weights
  top out at node 32 of `HUMANS.MDH`'s 34, so its node indices need no translation.
- An `.MDL`'s weights address its **own** hierarchy. All 21 `ARMOR_*.MDL` files have node
  names that also exist in `HUMANS.MDH`, so the indices are reconciled **by name** — the
  same mapping clip channels use. The mapping is not an identity: `ARMOR_PRISONER` lacks
  all nine `ZS_*` nodes, so its 25 nodes spread differently through the 34.

Composition must keep each mesh's own bind, not the merged skeleton's:

- The shared node translations and scales match exactly between `HUMANS.MDH` and
  `ARMOR_PRISONER`, but several orientations differ — by up to **0.29** in quaternion
  distance (~34°) at the shoulders. Rebuilding the armour's bind vertices through
  `HUMANS`'s transforms displaces its weighted vertices by up to **34 units**.
- The root translations differ too: `HUMANS.MDH` has `(0, 99.95, 7.38)`,
  `ARMOR_PRISONER` `(-0.11, 98.15, -6.47)`.

So the skin of a composed mesh keeps `inverseBind` from the hierarchy it was authored in.
With the skeleton posing the merged bones, `inverseBind * boneWorld` maps the mesh from its
authored rest into the shared rest — exactly the transform an animation applies — and body
and armour land in one pose. The baked vertex positions (which the vertex buffer passes to
the shader) are in that authored space, so a composed part's **cull box** is computed
separately in the merged rest via a second transform, because that is where the skeleton
places the mesh. A normal single-file model has one hierarchy and both spaces coincide.

The explicit skeleton must contain every bone a mesh names; a missing name is an error, not
a dropped weight. The body/armour pair satisfies this, but bare `.MDM`s of other creatures
address their own skeletons — 22 of the first 122 bare `.MDM`s exceed 34 nodes, up to 77 —
and are only composable with a skeleton that covers them. None of the 21 `ARMOR_*.MDL` files
carries a rigid attachment mesh, so the static path only ever sees single-hierarchy models;
a composed source with one would need the same authored-to-merged mapping applied to its
vertex positions, which is not implemented.

## Assembling a model to draw

The draw contract of a stock `SkinnedEffect` is one bone transform per joint, where each is
`inverseBind * boneWorld`. Fitting Gothic's layout onto that needs two accommodations:

1. **A bone owns at most one mesh.** A skinned mesh spans many bones, so it cannot hang off
   one of them. Each skinned mesh is given a synthetic bone of its own, all under a
   synthetic root, with the real skeleton beside them. The mesh is positioned by the
   identity in that arrangement, because its vertices are already in model space.
2. **Bone numbering is a depth-first traversal order**, not the hierarchy order the weights
   use. Since the skin's joint array is explicit, this only matters for looking up the right
   bone object per node index.

A static part is positioned by its own node's global transform, as a prop model expects.

Textures come from each sub-mesh's material. A submesh with no texture (or a texture file that
cannot be found) is skipped: the shipped data uses those as hidden/placeholder faces, such as
`REPAIR_PLANK`'s `ZM_REP_HIDDEN` triangle, and the reference client skips them as well.

## Culling

A part's box is computed from the vertices as loaded, so for a skinned part it is the bind
pose. That is correct for what is drawn while nothing animates the skeleton, and the box can
be used as-is. Animation is what breaks it: a swinging limb leaves the bind-pose box, so
whatever plays a clip has to grow the box to cover the whole clip. The box lives on the mesh
part, so this is a per-part update rather than a change to the draw path.

## Managed reader

`OpenGothic/ZenKit/Model/` now owns the geometry readers: `ModelHierarchy` (MDH and the
hierarchy at the start of an MDL), `MultiResolutionMesh` (MRM, including its binary material
archive), `SoftSkinMesh`, `ModelMesh` (MDM/MDL body meshes and named attachments), `Model`
(MDL), `ModelScript` (MSB), `ModelAnimation` (MAN) and `MorphMesh` (MMB). `Assets.Model` and
`Assets.Animation` build `DrModel`/`DrMesh` and their clips from these managed objects; no
native model, animation or morph reader remains.

The managed readers match ZenKitCS on the reference install for `ARMOR_BAU_L.MDM`,
`ARMOR_BDT_H.MDL`, a standalone MRM and a mixed set of skeletons: positions, normals,
submesh triangles/wedges, materials, weights, nodes, wedge normals and attachments. The
verification frame draws the same geometry as the native path, and the player avatar renders
and runs with managed body/armour meshes.

## Edge cases and open questions

- Weight counts per vertex are 1–3 in the shipped data; the format's upper bound is not
  established.
- Positions in the weight list are float and already in bone space; no unit conversion
  appears to be needed (a human actor is ~165 units tall, consistent with 1 unit ≈ 1 cm).
- Whether a model can reference a skeleton in a *different* `.MDH` (shared skeleton) is not
  recorded in any file: every model resolves to a self-contained `.MDL` or to its own
  `.MDH`/`.MDM` pair with matching stems. A composed model is therefore built from an
  explicit skeleton and meshes named by the caller, not discovered from the data.
- Animation data is out of scope for this document; see
  [gothic-animation-format.md](gothic-animation-format.md).
