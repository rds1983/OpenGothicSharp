# Focus behavior

The reference (`Try/OpenGothic`, MIT) has three focusable object lists — NPCs, items and
interactives — and `World::findFocus` asks each list for candidates. `WorldObjects::testObj`
filters by distance, facing angle and a target-type mask, then `canSee` does a ray test against
the object. The best candidate is chosen by the script-controlled priority and range policy
(`focusNorm`, `focusMelee`, …).

The port starts from the camera ray instead of the reference's angle cone, because the debug
HUD and the eventual interaction key need a crosshair target:

- **Ray** — from the player's eye (`FeetPosition + 150`, the capsule centre) along
  `Camera.Forward`, so first and third person focus on the same place.
- **Reach** — 200 world units (`FocusSystem.Reach`), the order of Gothic's interactive range.
- **Candidates** — `Npc`, `Item`, and the interactive family (`Interactive`, `Container`,
  `Door`, `Bed`, `Fire`, `Ladder`, `Switch`, `Wheel`). Static geometry, movers, lights,
  triggers, decals and particle effects are not focusable.
- **Broad phase** — `VobNode.Bounds`, the world box the loader already keeps for each vob.
- **Narrow phase** — if the vob has a single `SceneNode`, its part boxes are tested too
  (`SceneNode.RayTest`). Models test each mesh part with the part's bone or model transform;
  batched multi-resolution meshes use their vob bounds. This is a box proxy, not a triangle
  test: it avoids keeping a second CPU copy of every mesh, and the visual difference is small
  at a two-metre reach.
- **Occlusion** — a physics ray is cast to `distance - 20`. The target's own collider lies on
  its bound, so the slack keeps it from occluding itself; a wall closer than 20 units to the
  target does not block it.

The nearest surviving candidate wins. `FocusSystem.Target` is a reused object, so the
per-frame query allocates nothing.

## Presentation

- The focused vob's bounds are drawn as a cyan wireframe box by `WorldRenderer` after the
  world geometry.
- `DebugOverlay` shows `Focus: <Kind> '<tag>' (<distance>)`, or `Focus: (none)`.
- **E** resolves the current target and logs it. Until E3 adds actions, this is the observable
  result of the interaction key. `-interact` does the same once on the first walking frame for
  a capture.

The displayed name is the vob's tag or instance name, since script display names are not
available until the Daedalus VM exists.

## Not yet ported

- The reference's angle cone and per-script focus priority/ranges.
- NPC and item display names, and highlighting the model itself rather than its bounds.
- Interaction actions, which are E3.
