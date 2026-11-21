# Physics

## Decision

**BepuPhysics2** is the physics engine. Its collision detection replaces the hand-written
`WorldCollision` grid.

## Why

The current `WorldCollision` only answers two questions:

- "highest surface under this point",
- "push this vertical cylinder out of steep triangles".

That was enough to walk around a static world. It cannot do:

- dynamic bodies (doors, movable objects, dropped items),
- proper capsule sweeps and slopes,
- character controllers with correct stepping,
- raycasts and shape casts for gameplay (line of sight, interaction targeting, AI).

BepuPhysics2 provides all of it, is mature, fast and pure C#.

## Migration plan

1. Add `BepuPhysics2` and build a static `StaticMesh` collidable from the world's visual
   collision geometry (same triangles that feed `WorldCollision` today).
2. Add static collidables for `CdStatic` vobs.
3. Replace `Player` movement with a Bepu character/kinematic capsule:
   - sphere/capsule shape for the player,
   - ground snap and step handling,
   - gravity applied through the physics solver.
4. Delete `WorldCollision` once nothing references it.
5. Add dynamic bodies for movers and dropped items.

## Rules

- Gameplay code queries physics through an engine facade (`PhysicsWorld`), never through
  Bepu types directly, so the solver can be swapped or mocked in tests.
- Every collidable registration is data-driven: the world loader produces the collision
  representation, physics only consumes it.
