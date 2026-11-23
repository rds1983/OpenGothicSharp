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

The whole of `newworld.zen` is 367k terrain plus 1.47M vob source triangles, and doubling
every one for the two-sided mesh gives about 3.7M triangles — roughly 15–25 seconds and a few
hundred megabytes with a single-threaded build. That is too much to do at every startup while
there is no gameplay streaming it, so `PhysicsWorld.Build` takes a centre and a radius and
keeps only the terrain triangles whose bounds touch that box and the vob parts whose world
bounds do. For the harbour with a 3000-unit radius, that is **4,601 terrain** and **32,755
vob** source triangles in **9** static bodies, built in well under a second.

The world mesh and the vob collision tags are produced only when
`DebugSettings.CollisionEnabled` is set (by `-physics`). Streamed updates as the player walks
out of the built neighbourhood are the next step; the controller does not yet request them.

## Character controller

`CharacterController` is a kinematic capsule (`radius 30`, cylinder length `110`, so it is
175 units tall) with its own gravity (`-981 cm/s²`). It does not use a dynamic body: each
frame it sweeps the displacement against the static meshes and resolves the contact itself.

- **Grounding** is a downward ray from the capsule's bottom. A normal with `Y >= 0.55`
  (about 57°) is walkable, and the controller keeps a 0.5-unit clearance while following it.
  A gap up to 12 units is snapped downward; a rise is corrected only by the clearance so a
  real step goes through the step path.
- **Slide** is the projection of the remaining displacement onto the contact plane. Four
  iterations cover a corner. A sweep that starts already touching is pulled back by a
  0.5-unit skin so the next sweep is not a zero-time hit.
- **Step-up** probes for a walkable surface up to 35 units high just past the capsule's front
  when a near-vertical wall blocks a grounded body. The controller moves there only if the
  capsule fits (a short upward sweep checks the ceiling). Sweeping the capsule upward does not
  work for this: its lower sphere grazes the step's top corner, which is why the probe is a
  ray.

## Self-checks

`-physics` runs four checks from the plaza ahead of the camera (the default camera sits over
the harbour basin, so the plaza is the stable starting point):

1. **Walk** — pick the most open of 16 directions, then walk 8 seconds. The capsule must stay
   grounded and near the surface, with no large per-step rise.
2. **Step** — in a separate flat test world, walk into a 30-unit block; the capsule must end
   up 30 units higher.
3. **Wall** — in a flat test world, walk diagonally into a wall; the capsule must keep its
   radius from the face and keep moving along it.
4. **Drop** — drop a dynamic capsule on the level and drive it into the nearest vob wall. It
   must rest on a surface and stop outside the wall.

A run on `newworld.zen` reports 2,390 units walked with 100% grounded, clearance 0.5..0.5 and
maximum rise 0.32; the step climbed 30; the wall slide moved 382 units and held 30.4 from the
face for a radius of 30; and the dropped capsule rested on terrain 29.2 above it and stayed 35
from a wall. All lines are in `log.txt`, so a regression is visible without a physics overlay.
