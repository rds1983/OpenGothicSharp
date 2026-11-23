# Collision format

How the level and its static vobs become a BepuPhysics2 triangle set, and the conventions the
engine imposes on that data. Written from the parsed structures and from running the capsule
check against the shipped `newworld.zen`.

## Units and coordinate system

Gothic stores positions in centimetres: a human actor is about 165 units tall. The render path
mirrors Z once, while assets load (`Mathematics.ToXna`), so physics uses the same right-handed
world as rendering. Gravity is therefore `9.81 m/s² = 981 cm/s²` downward.

## The level mesh

`IMesh` stores positions and polygons; a polygon has a material index and the position indices
of its corners. Collision is built from the raw mesh, not from the per-cell render chunks:

- Every polygon with **three or more** corners is fan-triangulated, the same way the renderer
  triangulates it.
- The polygon is included unless its material has `DisableCollision` set. Unlike rendering,
  a material with no texture is **not** skipped: invisible geometry, including ghost
  occluders, is often exactly what should block the player.
- Polygons whose material group is `Water` are built as a **sensor** instead of a solid: the
  collider exists for queries, but its pairs generate no contacts.
- Friction is chosen from the material group; polygons are grouped by `(friction, sensor)` so a
  single Bepu mesh can use one coefficient per group.

Measured on `newworld.zen`: **367,394** collision triangles in **5** friction/sensor groups.

## Static vobs

A vob contributes collision only where it has rigid geometry:

- `VisualMultiResolutionMesh` (`.MRM`): every submesh, transformed by the vob's world matrix.
- `VisualModel` (`.MDL`, or `.MDH` + `.MDM`): only the mesh's **static attachments** — the
  bone-named rigid parts. A skinned mesh deforms with its pose and has no single static shape,
  so it gets no collider. This mirrors the reference, which creates collision only from a
  model's attachment shapes.
- Instanced multi-meshes are added once per instance transform.

The placement is the renderer's: `MeshNode.WorldTransform` for a mesh, the instanced transform
for an instanced mesh, and `boneGlobal * ModelNode.WorldTransform` for a rigid part of a model.
Vob collision is therefore built from what the scene actually contains; `-nomodels` means those
meshes were not loaded and contributes no collision.

Measured on `newworld.zen`: **1,465,637** source triangles from static vobs.

## One-sided meshes and winding

Bepu's `Mesh` is **one-sided**: only a triangle whose winding presents a front face to the
body generates contacts, and rays hit it from one side only. Gothic's polygons do not have a
consistent winding, and the renderer never relies on culling them:

| Orientation of the first three corners | `newworld.zen` polygons |
| --- | --- |
| normal has `Y > 0.1` | 130,515 |
| normal has `Y < -0.1` | 202,939 |
| steep (neither) | 127,829 |

Using the data as-is therefore leaves roughly half of every surface permeable: a capsule falls
through floors whose triangles happen to face away. Each triangle is consequently added **twice,
with opposite windings** (`a,b,c` and `a,c,b`). That doubles the physics triangle count but makes
every surface solid from either side, which is what Bullet's concave mesh does in the reference
client.

Degenerate triangles (zero area) are dropped before the mesh is built, so they cannot poison the
BVH.

## Friction

The coefficient comes from the polygon's material group, using the reference client's table:

| Group | Friction |
| --- | --- |
| Undefined | 0.5 |
| Metal | 1.1 |
| Stone | 0.65 |
| Wood | 0.4 |
| Earth | 0.4 |
| Water | 0.01 (sensor) |
| Snow | 0.2 |
| default | 0.75 |

Bepu has no per-shape material, so each collidable is allocated an entry in a
`CollidableProperty<PhysicsSurface>`. The narrow-phase callback looks up both collidables and
uses `sqrt(frictionA * frictionB)`. Sensors are rejected in `AllowContactGeneration`.

## Cost

For `newworld.zen` the source data is 367k terrain plus 1.47M vob triangles. Duplicating each
for the two-sided mesh gives about 3.7M triangles, which takes around 15–25 seconds and a few
hundred megabytes to build with a single-threaded build. The CPU triangle data is kept only
when `DebugSettings.CollisionEnabled` is set (the `-physics` switch), and the physics world is
built on request, because no gameplay uses it yet. Streaming collision by cell will be needed
before it can be always-on.

## Self-check

`-physics` builds the world and runs `PhysicsWorld.RunCapsuleTest` from the plaza ahead of the
camera:

1. Raycast down to find the ground.
2. Drop a `Capsule(radius 30, length 110)` from 200 units above it and step four seconds. It
   must end up off the surface (center between the radius and the upright half-height above
   the support) and nearly stationary.
3. Cast 16 horizontal rays from the resting point, prefer a vob over terrain, move the capsule
   to just in front of the nearest wall and drive it into the surface for a second. The center
   must stay outside the wall by roughly the capsule radius.

A run on `newworld.zen` reports a capsule resting on terrain, a vob wall about 159 units away,
and a final center 35 units from the wall with a radius of 30 — blocked rather than passed
through. Both lines are in `log.txt`, so a regression is visible without a physics overlay.
