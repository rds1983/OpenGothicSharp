# NPC runtime behavior (F1/F1.5)

Gothic's shipped worlds contain **no `oCNpc` vobs**. `NEWWORLD.ZEN`, `OLDWORLD.ZEN`,
`ADDONWORLD.ZEN` and `DRAGONISLAND.ZEN` all report zero NPCs; the Daedalus startup scripts
insert them with `Wld_InsertNpc`.

Because of that, F1 does **not** invent placeholder NPCs or bodies. It provides the runtime
container a script-created NPC will use:

| Member | Meaning |
| --- | --- |
| `NpcInstance` | script instance name, e.g. `NPC_DIEGO` |
| `Guild`, `Level` | character data from the ZEN/script NPC |
| `Hp`, `MaxHp`, `IsDead` | health |
| `TakeDamage(amount)` | clamps health at zero and logs damage/death |
| `Say(message)` / `LastMessage` | bark/debug message hook; scripted dialog lines use `DialogLine` |

`NpcVob` is created by `VobFactory` when an `oCNpc` source exists, exactly like the other
typed vobs. The focus system already accepts `VobKind.Npc`, and its overlay reports
`Npc '<name>' hp <hp>/<maxHp>`.

## World-bound script NPCs (F1.5)

`NpcSpawner` runs after the Daedalus boot and turns the script bindings into placed bodies.

- `WorldGrid` keeps the waynet (`Waypoint` position, facing, free flag). An NPC starts at its
  authored spawn point; when that point is already used, the nearest unused waypoint within
  800 units is chosen, matching `WayMatrix::findNextPoint`. Missing points use the dead point
  `TOT` (or `(-1000,-1000,-1000)`).
- **Waynet points sit about 50 units above the floor** and an NPC's model origin is at its
  feet, so placing the node at the authored height makes the body hover. `WorldGrid.
  TryGetGround` casts a vertical ray through the chunked terrain triangles and the NPC is
  moved down onto the highest surface within `GroundSnap` (60 units); the reference solves
  the same problem in `fixNpcPosition`. Surfaces beyond that band are ignored so a point on a
  vob floor (a house's wooden boards) keeps its authored height instead of dropping to the
  street below. A short spiral retries around points that hang over a ledge. Grounding needs
  the collision chunks, so it is active whenever collision is (the normal gameplay path).
  Waypoint ground heights are cached per point and reused by routine movement.
- A human body is the armour item's `VisualChange` mesh (`Armor_Mil_L.asc` → `ARMOR_MIL_L.MDM`)
  or the naked body `.MDM`, composed with `HUMANS`; the head `.MMB` is attached to
  `BIP01 HEAD`. The composed geometry is cached and shared.
- `MDL_SetVisualBody`'s body/head/teeth/colour variant is applied per instance by rewriting
  the `_V0`/`_C0` texture tags. `GothicModel.Materials[mesh][part]` starts as the model's own
  materials; the variant replaces individual entries with a `Material` carrying the variant
  texture, so the geometry stays shared. A missing variant falls back to the authored
  texture. `MDL_SetVisual` and `MDL_SetModelScale` are stored on `NpcVisual`.
- Each NPC gets a kinematic Bepu capsule at its feet. Focus scans the typed vob tree, ignores
  the NPC's own capsule when checking occlusion, and E opens the G3 dialog.
- The body idles on `S_RUN` for humans and `S_FISTRUN` for monsters while the NPC is within
  3000 units of the player; a distant NPC keeps its last pose. A fixed direction from the
  waypoint turns the body.
- A dialog opened with a placed NPC uses a two-shot dialog camera (midpoint, side orbit) and
  restores the exploration camera on close. `DialogLine.Voice` is played through MonoGame
  `SoundEffect`; the full sound-definition layer is J1.

The tested NotR startup has **406 script NPCs**: **356 human bodies are built immediately**,
and **50 non-human bodies are created lazily** when the player comes within 3000 units. The
172 NPCs assigned to `NW_CITY_ENTRANCE_01` share the five nearby waypoints and therefore
overlap until F2 routines move them.

## Non-human bodies (F1.6)

- `MDL_SetVisual` names a model script (`Giant_Rat.MSB`/`.MDS`). The animation index maps the
  `MDL_SetVisualBody` mesh back to that script, whose stem supplies the `.MDH`/`.MDL` skeleton;
  the body is composed with it and the script's clips are attached.
- The first idle attempt is `S_FISTRUN` (case-insensitive; the shipped clips are
  `s_FistRun`), falling back to `S_RUN`. A body with no matching clip stays in its bind pose.
- Monster models are built lazily: the vob and kinematic capsule are created at spawn, and
  `NpcSpawner.TryBuildModel` builds at most one nearby body per frame. A failed model is
  marked and not retried. This keeps startup bounded; one shipped body (`Snorting Dragon
  Snapper`) fails with `std::bad_alloc` and stays invisible.

## Daily routines (F2)

- After the C_NPC instance and its binding exist, `ScriptRuntime` calls the instance's
  `daily_routine` entry point with `SELF` set to the NPC. Its `TA_MIN` calls add
  `(start, end, action, waypoint)` entries to `NpcVob.Routines`.
- `NpcRoutine.Select` picks the range active at the current game hour, including ranges that
  cross midnight, and otherwise the routine whose end is closest before now. Routines whose
  waypoint is missing are skipped.
- The NPC is placed at the active routine's waypoint at spawn, so the city crowd starts at
  its guard/shop/tavern points instead of all stacking on the insertion point.
- `GameClock` starts at `-time:` (default 10:00) and advances at `-timescale:<game-min/sec>`
  (default 1). `-time:21 -timescale:60` walks a full game hour per real second for tests.
- When the routine changes, the actor walks to the new waypoint at 300 units/s, switching
  from the idle clip to `S_RUNL`/`S_FISTRUNL`, and updates its vob bounds, grid cell and
  kinematic capsule. Path targets use the grounded `WorldGrid.WaypointGroundY`, so a walk
  ends on the floor instead of at the authored height; the Y between two points is
  interpolated. The routine's `action` callback is stored for F3, not invoked yet.
- **Waynet edges are not available through the installed ZenKitCS native export**
  (`ZkWayNet_getEdges` is missing; the DLL exposes `getEdgeCount`/`getEdge` with a different
  ABI). Way points are parsed and `WorldGrid.FindWaypointPath` is tested, but routine
  movement currently falls back to the direct target. The managed ZenKit port (N1) removes
  this limitation.
- Verified: Matteo changes from `NW_CITY_MATTEO` to `NW_CITY_BED_MATTEO` at 22:00 and logs
  the arrival; Citizen/Rupert routines change on the same clock.

## Deferred

AI, routines' action callbacks, movement collision avoidance, combat and armour attachments
(pauldrons and similar) remain. The dialog camera is a usability-first two-shot, not the
reference's exact spin; voice playback is the minimal J1 hook.
