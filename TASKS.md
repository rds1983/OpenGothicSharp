# TASKS

Roadmap and work queue. **Take the first unchecked task unless the developer says otherwise.**
See [AGENTS.md](AGENTS.md) for the workflow, and [Design/](Design/) for subsystem notes.

Priorities, in order:

1. Make every entity needed for gameplay **visible** — hero, NPCs, creatures, items, objects.
2. Make them **active** — scripts, schedules, movement, interactions, inventory, combat.
3. Only then chase **visual and UI parity** and serious performance work.

Non-negotiables while doing all of it:

- Stock MonoGame effects (`BasicEffect`, `SkinnedEffect`, …) — no custom shaders yet.
- **BepuPhysics2** for collision and character movement.
- **Myra** for UI.
- **xUnit** tests for new logic.
- Frustum culling stays; other performance work waits.
- No copying C++ source; the reference project is read-only inspiration.

---

## Phase 0 — Foundation

- [ ] **T-002 Add CI build + test workflow.** GitHub Actions: restore, build, test on Windows and
      Linux. Cache NuGet. Linux should build and test only; the DesktopGL host may need native
      dependencies a plain image lacks.
- [ ] **T-003 Introduce an engine-level `Game`/`GameHost` split.** Move host-independent code out
      of `OpenGothic.Console` into `Common` so a future game executable shares it. Keep the viewer
      working.
- [ ] **T-004 Restructure `Common` into the planned folders** (`World/`, `World/Vobs/`, `Game/`,
      `Physics/`, `UI/`). Pure moves + namespace updates, no behaviour change.

## Phase 1 — Physics and movement (prerequisite for everything active)

- [ ] **T-010 Add BepuPhysics2 and a `PhysicsWorld` facade.** Static mesh collidable from world
      polygons, API for shape casts and ray casts. Tests on the facade. See
      [Design/Physics.md](Design/Physics.md).
- [ ] **T-011 Build the world's static collision shape from the loaded world.** Reuse the
      triangles currently fed to `WorldCollision`, including `CdStatic` vob meshes.
- [ ] **T-012 Replace `Player` with a Bepu kinematic character.** Ground snap, step height,
      slopes, gravity, jumping; camera follows the capsule.
- [ ] **T-013 Delete `WorldCollision`** and its call sites once nothing references it.
- [ ] **T-014 Add dynamic bodies for movers and dropped items;** expose them to gameplay.
- [ ] **T-015 Movement states: swimming, diving, falling, climbing, sliding.** Reference:
      `game/movealgo.cpp`.

## Phase 2 — Visible entities

### Shared visual pipeline

- [ ] **T-020 Generalize the model wrapper.** A `Visual`/`ProtoMesh` equivalent in `Common` that
      owns meshes, materials, skeleton, poses and animation, independent of the viewer.
      Reference: `graphics/mesh/protomesh.*`, `mdlvisual.*`.
- [ ] **T-021 Animation stack: pose, skeleton, blend/crossfade, root motion.** Move the current
      ad-hoc `AnimationController` usage behind an engine API. Reference:
      `graphics/mesh/{pose,skeleton,animation,animationsolver}.cpp`.
- [ ] **T-022 Animation aliases and the `Anim` enum.** Gothic maps semantic animations
      (`Idle`, `Move`, `Fall`, `Attack*`, …) onto clip names per skeleton type. Reference:
      `graphics/mesh/animationsolver.cpp`, `S_*` naming in model scripts.
- [ ] **T-023 Morph meshes (heads) as a proper feature.** Currently drawn statically; wire morph
      animation for facial expressions. Reference: `graphics/mesh/submesh/animmesh.cpp`.

### Hero

- [ ] **T-030 Hero as an entity, not a viewer special case.** `Npc`-like object in `Common` with
      position, orientation, visual, animation state; the viewer only drives input.
- [ ] **T-031 Hero equipment visuals.** Armor mesh, head/teeth textures, body texture variants,
      weapon in hand. Reference: `World::addView(visual, headTex, teethTex, bodyColor)`.
- [ ] **T-032 Full locomotion animation set** — idle, walk, run, strafe, turn, transitions,
      jump/fall/land, swim. Replace the two-clip demo.

### NPCs and creatures

- [ ] **T-040 Load `oCNpc` vobs as typed objects.** Instance index, waypoint, guild, flags.
      Reference: `world/objects/npc.cpp` (read only, it is 4k lines).
- [ ] **T-041 Spawn and render all NPCs in a world** with correct armor/head/weapon visuals.
- [ ] **T-042 Creature models.** Non-human `.MDM` models (wolf, scavenger, molerat, …) render and
      animate with their own skeletons and animation sets.
- [ ] **T-043 NPC LOD/culling for the world population.** Only cared about when whole-world NPC
      counts make the viewer slow.

### Items and objects

- [ ] **T-050 `oCItem` as typed objects.** Spawn from the world, render with the item's visual,
      support two-hand/one-hand attachments. Reference: `world/objects/item.cpp`.
- [ ] **T-051 Interactive `oCMob*` objects.** Bed, door, inter, container, switch, ladder, wheel,
      fireplace: correct visual, state, focus name. Reference: `world/objects/interactive.cpp`.
- [ ] **T-052 Movers (`zCMover`) with animated transforms.** Reference:
      `world/triggers/movetrigger.cpp`.
- [ ] **T-053 Particle emitters (`zCPFXEmitter`, `oCMobFire` flames) — visible only.** No full
      particle sim required yet. Reference: `graphics/pfx/*`.
- [ ] **T-054 Lights (`zCVobLight`) as visible effects.** Simple point/spot lighting through stock
      effects; full light management later.

### World completeness

- [ ] **T-060 Real vob tree instead of flattened static meshes.** Typed `Vob` hierarchy with
      parent/child transforms and dynamic updates. Keep the flattened render path as an
      optimization where it is valid.
- [ ] **T-061 Sky, fog zones, far-plane zones.** Reference: `graphics/sky/sky.cpp`.
- [ ] **T-062 Water as a distinct surface** (blend + animation), currently drawn opaque.
- [ ] **T-063 Visual decals** (`zCVobDecal`). Low priority.

## Phase 3 — Active world (scripts and behaviour)

- [ ] **T-070 Daedalus VM bootstrap.** Load `DAT`/`OU`/`DAEDALUS.DAT` through ZenKit's
      `DaedalusScript`/`DaedalusVm`, register externals, run global init. Reference:
      `game/gamescript.cpp`.
- [ ] **T-071 Daedalus instance initialization.** `Npc`, `Item`, `Focus`, `Info`, `Menu`, `Spell`,
      `Svm`, `FightAi`, `Music*`, `ParticleEffect*` instance types. Reference:
      `game/gamescript.cpp` + `game/definitions/*`.
- [ ] **T-072 Quest log and global variables** (`mission`, `LOG_*`). Reference: `game/questlog.cpp`.
- [ ] **T-073 Waynet as gameplay data.** Load points/edges/freepoints, spatial queries, nearest
      waypoint. Reference: `world/waypoint.cpp`, `world/waymatrix.cpp`.
- [ ] **T-074 NPC AI: routine state machine.** `aiState`, daily schedules (`TA_*`), routine
      variables, waypoint walking. Reference: `game/aistate.cpp`, `world/aiqueue.cpp`.
- [ ] **T-075 Perception and AI output pipe** — seeing/hearing, messages, `AI_*` callbacks.
      Reference: `game/perceptionmsg.h`, `game/aiouputpipe.cpp`.
- [ ] **T-076 Interactions and focus.** Focus ray, on-screen name, `Use`/`Take`/`Open` behaviour,
      `oCMobInter` state machines. Reference: `world/focus.cpp`, `world/objects/interactive.cpp`.
- [ ] **T-077 Inventory.** Containers, equip/unequip, weight, category, item stacking. Reference:
      `game/inventory.cpp`.
- [ ] **T-078 Dialogue system.** `Info` instances, choices, conditions, voice output, camera
      framing. Reference: `game/gamescript.cpp` dialog section, `ui/dialogmenu.cpp`.
- [ ] **T-079 Combat: melee.** `FightAi` behaviour tree, hit detection through physics, damage
      calculation, hit/death animations. Reference: `game/fightalgo.cpp`,
      `game/damagecalculator.cpp`.
- [ ] **T-080 Magic and ranged combat.** Spells, mana, projectiles.
- [ ] **T-081 Triggers.** `zCTrigger`, `zCTriggerList`, `oCTriggerScript`, `zCCodeMaster`,
      `zCTriggerWorldStart`, `oCTriggerChangeLevel`, `zCMessageFilter`, `oCTouchDamage`,
      `zCEarthquake`. Reference: `world/triggers/*`.
- [ ] **T-082 Game session and world transitions.** `changeWorld`, hero carry-over, time of day,
      visited-world state. Reference: `game/gamesession.cpp`.
- [ ] **T-083 Save/load.** Own binary format is fine; must round-trip world, NPCs, inventory,
      quests, time. Reference: `game/serialize.cpp`, `game/worldstatestorage.cpp`.
- [ ] **T-084 Notifications, chapter screens, cutscenes.** Reference: `ui/chapterscreen.cpp`,
      `game/definitions/*` cutscene handling.

## Phase 4 — Audio

- [ ] **T-090 Sound effects.** Load `.WAV` from VDFS, 3D positional playback, `SVM` messages,
      material-based footsteps. Reference: `sound/soundfx.cpp`, `game/definitions/sounddefinitions.cpp`.
- [ ] **T-091 Music system** — themes, transitions, jingles. `.SGT`/DirectMusic parsing is a large
      subproject; decide scope before starting. Reference: `dmusic/*`, `gamemusic.cpp`.

## Phase 5 — UI (Myra)

- [ ] **T-101 Extend the debug overlay** with hero position, current grid cell and physics stats
      (body count, active contacts). FPS, draw calls and polygons are already shown. Physics stats
      wait for T-010.
- [ ] **T-102 Gothic font loading.** `.FNT` → Myra bitmap font. Reference: `utils/gthfont.cpp`.
- [ ] **T-103 Main menu and world/load selection.**
- [ ] **T-104 HUD:** health/mana/swim bars, focus name, item pickups.
- [ ] **T-105 Dialogue menu**, **T-106 inventory screen**, **T-107 character sheet**,
      **T-108 quest log**, **T-109 map**, **T-110 console**.

## Phase 6 — Parity and performance

- [ ] **T-120 Custom effect library** replacing stock MonoGame effects.
- [ ] **T-121 Shadow maps** and proper lighting from the world's light vobs.
- [ ] **T-122 Instanced rendering** finished and benchmarked (instances today are drawn one by one).
- [ ] **T-123 Occlusion culling** (BSP sectors from the world data, or a GPU approach).
- [ ] **T-124 Visual parity pass** — materials, fog, water, sky, decals, particle effects.
- [ ] **T-125 UI parity pass** — match Gothic's layouts, fonts and behaviour.
- [ ] **T-126 Mod support** — `modfile.ini`, extra VDFs, Ikarus/LeGo compatibility (research).
- [ ] **T-127 Platform packaging** — Windows/Linux/macOS builds.

---

## Backlog / ideas (not scheduled)

- Gothic 1 data support.
- Marvin/debug mode parity.
- Async world streaming between cells.
- Editor tooling.

## Completed

- **T-001 Set up `OpenGothic.Tests` (xUnit).** `OpenGothic.Tests` added to the solution, test
  packages pinned in `Directory.Build.props`, one smoke test; `dotnet test` green in Debug and
  Release. [Design/Testing.md](Design/Testing.md) updated.
