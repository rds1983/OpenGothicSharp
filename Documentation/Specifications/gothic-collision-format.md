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

## Cost and streaming

The whole of `newworld.zen` is 367k terrain plus 1.47M vob source triangles, and doubling
every one for the two-sided mesh gives about 3.7M triangles — roughly 15–25 seconds and a few
hundred megabytes with a single-threaded build. That is too much to do at every startup.

The level mesh is therefore bucketed into **2000-unit chunks** while the world is read, and
`PhysicsWorld` collects the static vob geometry once as a list of sources: one per mesh part or
instance, one per model with rigid attachment meshes. `Initialize` loads the chunks and sources
whose bounds touch a 3000-unit box around the player; `Update` reloads after the player moves
256 units, keeping a further 500-unit margin before unloading. For the harbour the initial set
is **16 chunks** and **~394 vob sources**, 9.7k terrain + 33k vob source triangles in 478 static
bodies, built in well under a second. A streaming step builds only what crossed the edge.

The world mesh and the vob collision tags are produced only when
`DebugSettings.CollisionEnabled` is set (by `-walk` or `-physics`).

## Character controller

`CharacterController` is a kinematic **ghost capsule** ported from OpenGothic's
`NpcBody`/`NpcItem` movement. It is deliberately not a body and not full height:

- The capsule is `radius 30`, cylinder length `30`, so its total height is 90. Its centre sits
  **150 units above the logical feet**, which puts the collidable volume at feet + 105..195 —
  chest and head, not legs. The numbers follow the reference's `NpcBodyList::create`: for a
  180 × 60 human bbox it computes `radius = min(h/2, min(w,d)) / 2 = 30`,
  `ghostPadding = h/2 = 90`, `cHeight = h - 2r - ghostPadding = 30`, and
  `groundOffset = h/2 + ghostPadding/2 + 15 = 150`. The 15-unit term is the reference's
  "Khorinis port hack"; it keeps the capsule clear of small geometry.
- Because the capsule floats above the ground, **low props and terrain bumps cannot catch it**.
  A plank pile, a barrel or a 1-metre step is simply below the capsule; the player walks over
  it. This is the reference's behaviour, not an oversight.
- `Position` is the capsule centre; `FeetPosition` is the logical position used by scripts and
  the camera.

Each frame:

1. **Movement** is a series of substeps no larger than the capsule radius (`NpcItem::tryMove`).
   Every substep is a sweep, and a blocked substep **stops at the contact**. The remaining
   displacement is dropped; it is **not projected along the surface**. That projection was the
   source of the "backwards moves me sideways" report: a diagonal path into any surface was
   turned into a strafe.
2. **Forward-only bounce** ports `MoveAlgo::onMoveFailed`: when a *forward* move is blocked by
   an oblique surface (`|dot(right, normal)| >= 0.4`), headings rotated by ±5..35° are tried
   until one is clear. Backward and strafing moves never bounce, so S cannot become a strafe.
3. **Ground** is a downward ray from the capsule centre +1 over 250 units. If the feet are
   within 50 units of the hit (`dY <= stepHeight`, the reference's default guild value), the
   feet snap onto it — up *or* down. That is also how steps work: the chest capsule passes over
   the riser, then the ground ray lifts the feet. There is no separate step-assist.
4. If the ground is farther than 50 units below, gravity applies and the downward displacement
   is moved through the same substep test.

**Overlap recovery** remains as a last resort: short rays on the capsule's four sides push it
out along the shallowest exit, and the streaming loader defers a vob source whose bounds
intersect the capsule.

### Why the collision test is a sweep, not an overlap query

The reference calls Bullet's `contactTest` on the capsule at each substep position: it asks
"does the destination overlap anything?" BepuPhysics2 2.4 has **no public shape-overlap query**.
Its narrow phase is internal to `Simulation.Timestep`, and `Simulation` exposes only `RayCast`
and `Sweep`. The nearest equivalent is `Simulation.Sweep`:

- a positive-time hit is a swept contact, which is stricter than a destination overlap and also
  prevents tunnelling through thin geometry;
- a zero-time hit means the capsule **starts** inside a collider; the controller reports the
  move as failed and resolves that with the side-ray recovery.

This is a deliberate divergence forced by the library, not a different movement model. With the
0.5-unit skin the capsule keeps from every surface, a wall produces a positive-time hit, so the
"stop, do not slide" behaviour matches the reference on screen.

A plain launch (or `-walk`) drives the capsule from WASD and mouse look, streams collision
around its position, and places a first-person or (F5 / `-thirdperson`) chase camera. The chase
camera ray-casts backwards from the head so a wall does not cut through it. `-player:<x,y,z>`
starts at a diagnostic eye position (the player is dropped to the ground below it),
`-yaw:<degrees>` sets the facing without relying on the camera, and `-walktest:<seconds>` holds
forward (negative: backward) and logs the final feet position. Diagnostics keep the fixed
camera; `-nowalk` forces it.

## Self-checks

`-physics` runs four checks from the plaza ahead of the camera (the default camera sits over
the harbour basin, so the plaza is the stable starting point):

1. **Walk** — pick the most open of 16 directions, then walk 8 seconds. The capsule must stay
   grounded and near the surface, with no large per-step rise. The walk also calls
   `PhysicsWorld.Update`, so chunk/source streaming is exercised.
2. **Step** — in a separate flat test world, walk over a 30-unit block; the chest capsule passes
   above it and the ground ray must lift the feet by 30.
3. **Wall** — in a flat test world, walk into a wall and then reverse. The capsule must hold its
   radius from the face, back away, and show **zero sideways drift**.
4. **Drop** — drop a dynamic capsule on the level and rest it on terrain. The nearest-wall
   probe is best-effort: the capsule may settle beside a narrow piece of geometry that the
   16-ray fan misses.

A run on `newworld.zen` reports 2,387 units walked with 100% grounded, clearance -0.01..0.01
and maximum rise 0.32 while loading/unloading chunks and sources; the feet climbed 30 over the
block; the wall test held 30.5 from the face, backed 98 units with 0 sideways drift; and the
dropped capsule rested on terrain about 29 above it. All lines are in `log.txt`, so a
regression is visible without a physics overlay.
