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
- **Waynet edges** are read by the managed VFS (N1/N2); `WorldGrid.FindWaypointPath` follows
  them, and routines fall back to the direct target only when the start and goal are in
  different components.
- Verified: Matteo changes from `NW_CITY_MATTEO` to `NW_CITY_BED_MATTEO` at 22:00 and logs
  the arrival; Citizen/Rupert routines change on the same clock.

## AI states (F3.1)

- A state is a Daedalus `ZS_*` function. `ScriptRuntime.TryGetStateFunctions` resolves the
  entry plus its `<name>_Loop` and `<name>_End` siblings by name, exactly like the reference's
  `AiState` (`game/aistate.cpp`). The shipped data spells them upper case and the VM lookup is
  case-insensitive, so `ZS_STAND_ARMSCROSSED` gets `ZS_STAND_ARMSCROSSED_LOOP`.
- `ScriptRuntime.InvokeState` sets `SELF`, `OTHER` and `VICTIM`, calls the function and returns
  its int result - the loop's `LOOP_CONTINUE` (0) or `LOOP_END` (1). A script error is logged
  and treated as 0 so one broken state cannot stop the frame.
- `NpcAiSystem` runs them: the entry once, `_Loop` every 0.5 s of real time, `_End` when the
  state is replaced or the loop returns anything but `LOOP_CONTINUE`. A state with no `_Loop`
  never ticks, like the reference's ini-only states.
- `AI_StartState` and `NPC_IsInState` are implemented. `AI_StartState` is queued and drained at
  the next AI update: the call happens inside another state's function and re-entering the VM
  there would nest calls. Draining also lets an entry function start its successor, so
  `ZS_GOTOBED` (ini only) ends and `ZS_SLEEP` starts in the same tick. `AI_EndState` is not a
  symbol in the shipped data, so it is not registered.
- `self.wp` (the instance's `Wp` field) is set to the routine waypoint before the entry call,
  as `Npc::startState` does; `ZS_*` states read it instead of taking an argument.
- The routine's `action` starts after the NPC arrives at its routine point. The reference
  starts it immediately and lets the state walk with `AI_GotoWP` (F3.2); until then the F2
  walker moves the body. A routine change ends the state before the walk starts.
- Only NPCs within 3000 units of the player run loops: 356 states polling every half second
  would spend the frame in the VM. F3.4 replaces this with the reference's process policies.
- Verified at `-time:21 -timescale:60`: Matteo runs `ZS_STAND_ARMSCROSSED`, chains
  `ZS_GOTOBED` -> `ZS_SLEEP` after reaching `NW_CITY_BED_MATTEO`, and returns to
  `ZS_STAND_ARMSCROSSED` when the 08:00 routine starts.

## Script AI queries and actions (F3.2, in progress)

`-aitrace` logs every external a running `ZS_*` state calls. The set below was read out of the
shipped scripts with it instead of guessed.

- **Queries implemented:** `NPC_GetNearestWP`, `NPC_GetDistToWP`, `NPC_GetDistToNpc`,
  `NPC_GetBodyState` (`BS_STAND` = interruptable + free hands), `NPC_GetStateTime`,
  `NPC_IsOnFP`, `WLD_IsMobAvailable`, `WLD_IsFPAvailable`, `NPC_SetPercTime`,
  `NPC_PercEnable`, `HLP_IsValidNpc`, `HLP_Strcmp`, `NPC_GetLookAtTarget`.
  `NPC_HasItems` returns 0 until NPC inventories exist (I3).
- **Actions implemented:** `AI_GotoWP` (walks via `NpcWalker`, the same transform/bounds/grid/
  capsule update the routine walker uses), `AI_AlignToWP`, `AI_Standup`/`AI_StandupQuick`,
  `AI_PlayAni`, `AI_SetWalkMode` (weapon bit stripped), `AI_RemoveWeapon` (no-op until combat).
  Script movement pauses while the routine walker owns the body and is cancelled when the
  state ends.
- **Mobs:** the reference derives a mobsi's scheme from its visual file name
  (`ProtoMesh::setupScheme`): `CHAIR_NW_NORMAL_01.ASC` is a `CHAIR`, `BEDHIGH_PSI.ASC` a
  `BEDHIGH`. The managed vob exposes that visual name, so `WLD_IsMobAvailable` and
  `AI_UseMob` match the scheme against it.
- **Sitting works:** the human set's `S_CHAIR_S0` is a one-frame placeholder that looks
  standing; the seated pose is `S_CHAIR_S1`. State 1 therefore plays the `T_CHAIR_S0_2_S1`
  transition (the same shape for `BENCH`), which ends in the seated idle. Verified: Rupert
  sits on his chair at `NW_CITY_SHOP02_SIT`.
- **Sleeping works:** some authored sleep spots use an `oCMobDoor` visual, so the mob search
  accepts beds and doors as well as `Interactive` vobs (the scheme prefix keeps real doors
  out). With that, `WLD_IsMobAvailable(self, "BEDHIGH")` is true at the bed and the scripts call
  `AI_UseMob(self, "BEDHIGH", 1)`; the body plays `T_BEDHIGH_FRONT_S0_2_S1` (or `_BACK_`, or
  `T_STAND_2_SLEEPGROUND`) and lies down. Verified: Matteo lies at `NW_CITY_BED_MATTEO`.
- **Release:** the human set has no `..._2_STAND` clip, so the entry transition is played
  backwards (`T_CHAIR_S0_2_S1` reversed) to stand up; verified for chairs, beds and stoves.
  When no clip was stored the body falls back to the idle clip.
- **Placement:** every mob with `ZS_POS*` nodes uses the nearest free attach point, as
  `Interactive::findNearest` does. A `_DIST` node is synthesized at its authored distance on
  the side the NPC approaches from; other nodes use the animated bone transform. The actor
  origin is ground-snapped with a physics ray (dynamic NPC bodies ignored) or the terrain,
  with the mob's floor as fallback, and faces along the node's local +Z axis. Mob clips keep
  their root motion, so the seated/lying pose carries the body from the use point onto the
  furniture. Attach points are claimed per NPC, so two NPCs cannot share one seat or bed side.
  Releasing frees the point and restores the waypoint position before the stand-up clip plays.
- **Idempotence:** `AI_UseMob` does nothing while the NPC already uses that mob at the same
  state, like the reference's `currentInteract->stateId()==act.i0` early-out. Work states call
  it every loop; without this the mob was released and re-entered every tick, teleporting the
  body between the waypoint and the bench (the reported up/down jitter). Work mobs also set
  `BS_MOBINTERACT`, which the state loops check.
- **Item animations:** `AI_UseItemToState` (brooms) still logs only; the animation needs NPC
  inventories and item visuals, tracked in I3.
- The routine callback restarts when a state ends on its own (the reference's
  `Npc::tickRoutine`), guarded to once a second and only when the actor is already at its
  routine point, so a sleep state returning `LOOP_END` at dawn re-enters `ZS_GOTOBED` instead
  of leaving the NPC stateless until the next routine change.

## Perception (F3.3, engine scope)

- Each `C_NPC` carries `senses` (SENSE_SEE/HEAR) and `senses_range` (2000 for the tested NPCs).
  `NpcAiSystem` samples the player on the NPC's perception interval (`NPC_SetPercTime`,
  default 0.4 s) instead of every frame.
- The view test copies `Npc::canRayHitPoint`: range check, then a ±80° cone around the body
  facing, then a line-of-sight ray from the NPC's head. The ray stops short of the player's
  capsule and ignores the NPC's own capsule, which it starts inside.
- `NPC_PercEnable(type, fn)` stores a handler per `PercType` (33 slots) and
  `NPC_PercDisable` clears it; `PERC_SetRange` overrides a type's range. When
  `PERC_ASSESSPLAYER` triggers, its handler runs with the player as `OTHER`
  (`B_AssessPlayer` for the tested guards).
- The engine-side support the handlers need is in: `Hlp_GetInstanceId` (symbol index),
  `WLD_IsTime` (inclusive start/exclusive end, midnight wrap), `WLD_GetDay`,
  `NPC_GetAttitude`/`NPC_GetPermAttitude`, the `GIL_ATTITUDES` matrix (66 guilds, loaded at
  boot like `GameScript::initialize`), `WLD_GetGuildAttitude`/`SetGuildAttitude`,
  `NPC_SetTarget`/`GetTarget`, `NPC_GetHeightToNpc`, `NPC_IsInFightMode` (only FMODE_NONE is
  true until weapons exist), `NPC_ClearAiQueue`.
- Verified: a guard 254 units in front of the camera fires `PERC_ASSESSPLAYER` with the player
  as `OTHER`; the same guard with the camera behind it never fires. The guard's
  `B_AssessPlayer` then runs (`Hlp_GetInstanceId`, `Npc_HasEquippedArmor`, `Wld_GetDay`,
  `Npc_GetEquippedArmor`, ...) and decides not to change state for a guildless, unarmoured
  player - the reference's remaining inputs (room/portal guild, weapons, armour) are later
  milestones.

## Process policies (F3.4)

- `NpcProcessPolicy` is chosen from the squared distance to the player with the reference's
  radii (`WorldObjects::updateNpc`): **AiNormal** within 3000, **AiFar** up to 6000,
  **AiFar2** beyond.
- **AiNormal:** script movement, perception and the 0.5 s state loop.
- **AiFar:** no movement and no perception; the state loop runs every 3 s so schedules still
  progress. `Npc::perceptionProcess` also early-outs for non-normal policies in the reference.
- **AiFar2:** dormant - no AI work at all.
- The routine walker and the animation pass keep their 3000-unit gates, which line up with
  AiNormal.
- Thresholds are covered by hermetic tests; the routine chain and sleeping still run with the
  policies active.

## Movement parity (F3.5, partial)

- NPC movement now sweeps the shared capsule through the physics world before applying a step
  (`PhysicsWorld.SweepNpcCapsule`), ignoring the actor's own capsule. A blocked step stops the
  body instead of walking through a wall or another NPC, matching the reference's
  `tryTranslate` behaviour (no surface sliding at the movement layer).
- Every step then snaps the feet to the floor: a physics ray from `feet + 60` downward sees
  vob floors, stairs and beds near the streamed player; `WorldGrid.TryGetGround` is the
  fallback outside the streamed area. The Y is no longer interpolated between waypoints, so
  slopes and steps are followed as the body moves.
- Spawn grounding uses the same physics ray when the spawn is inside the streamed area, then
  the terrain ray and the spiral.
- Both the routine walker and `AI_GotoWP` use this shared `NpcWalker`.
- Still open: the reference's stuck-capsule spiral and `distBias` at insertion, and re-snapping
  after climbs/teleports (neither exists yet).

## Deferred

AI, routines' action callbacks, movement collision avoidance, combat and armour attachments
(pauldrons and similar) remain. The dialog camera is a usability-first two-shot, not the
reference's exact spin; voice playback is the minimal J1 hook.
