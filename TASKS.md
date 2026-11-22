# TASKS.md

Canonical backlog for OpenGothicSharp. This file is the source of truth for what to
work on next. See [AGENTS.md](AGENTS.md) for the workflow that consumes it.

- Work top-to-bottom within the **current milestone**.
- One task per work session unless the developer says otherwise.
- After a task is confirmed finished, this file may be reorganized freely
  (split, merge, add, remove, reorder remaining tasks).

## Status snapshot

- Data readers exist: VDF mounting/record index, texture loading, ZEN world mesh
  + static vob instancing (`Assets.World.cs`), MDL model loading (`Assets.Model.cs`).
- Application host exists: `OgGame` window + `Update`/`Draw` loop publishes
  `OG.GraphicsDevice`.
- Logging exists: `Log.Info/Warning/Error` to console and `log.txt`, with ZenKit
  messages routed in.
- Command line parsed and validated: `-g`, `-game:`, `-w`, `-nomenu`, `-devmode`,
  `-nocull`/`-cull:` exposed via `CommandLine.Instance`. Debug switches are pushed into the
  static `DebugSettings`. Inspecting a single asset is done with `-cam:`, `-lookat:` and
  `-model:`/`-modeldist:`; `-screenshot:`/`-screenshotframe:` write a PNG straight from the
  backbuffer, which is the only capture that works here (an OS-level window grab is black).
- Settings load from `SystemPack.ini`/`Gothic.ini` plus a user override file, with
  typed accessors and persistence.
- Unit test project (`OpenGothic.Tests`, xunit) covers INI, settings, camera and command
  line; hermetic, no game installation required.
- Camera exists: view/projection from FOV and near/far, RHS world space.
- Static world renders with a gradient sky dome and distance fog. `Renderer` is a
  low-level drawing service (`BeginFrame`, `Clear`, `DrawMeshPart`, `EndFrame`) that owns no
  scene content and reports per-frame draw-call/triangle counts; it also does per-part
  frustum culling, and throws if drawn outside a frame. `WorldRenderer` owns the cell-level
  culling and traversal, `Sky` owns its own mesh part. Materials live on the mesh part's
  `DrMeshPartTag`, so `customMaterial` is usually null except for debug drawing. No
  vertex/texture UV flips. Reused state, no per-frame allocations.
- Models render: `ModelNode` holds a `DrModelInstance`; `DrawMeshPart` picks the static or
  skinned path from whether the part carries a skin, and a part with a skin throws if it is
  drawn without its bone transforms. Model vobs in the ZEN become nodes; use
  `-nomodels`/`-models:` to disable them. Bind transforms, root translation and the synthetic
  `_ROOT` arrangement are specified in
  `Documentation/Specifications/gothic-model-format.md`.
- No animation playback yet. No input, physics, audio, or scripting yet.
- UI exists: Myra `Desktop` rooted at a `Hud` panel with two independently anchored blocks
  — an always-visible `Controls` list (bottom-right) and a toggleable `DebugOverlay`
  (top-left, F1) showing FPS, camera position, draw-call and triangle counts and the active
  debug views. Layouts are `.xmmp` exported with `myra-export`; all UI lives in
  `OpenGothic/UI/`.
- Debug views: F2 toggles wireframe, F3 toggles bounding boxes; independent of F1 and off by
  default. Both can be started on from the command line with `-wireframe`/`-bounds`.
- Milestone A (Foundation) complete. **Milestone B (Static world rendering) complete.**
  **C1 (Model rendering) complete.** Current milestone: **C — Models and animation**.

---

## Milestone A — Foundation

### A1. Application skeleton — DONE
Create a `Microsoft.Xna.Framework.Game` subclass (`OgGame`) that creates a window,
initializes `GraphicsDevice`, holds `OG.GraphicsDevice`, and runs a fixed
`Update`/`Draw` loop with an exit hook.
**Done when:** `dotnet run` opens a window that clears to a color and closes cleanly.

### A2. Logging & diagnostics — DONE
Provide `Log.Info/Warning/Error` writing to console and `log.txt`. Initialize the
logger and ZenKit logging before other subsystems.
**Done when:** startup and asset-load lines appear in `log.txt`.

### A3. Command line — DONE
Parse and expose `-g <path>`, `-game:<modfile.ini>`, `-w <world.zen>`, `-nomenu`,
`-devmode`. Validate the Gothic data folder exists and fail with a clear message
(logged and on console).
**Done when:** flags are readable from game code and bad paths report an actionable error.

### A4. Settings file — DONE
Read the game's INI settings (`gothic.ini`/`systempack.ini`) into a typed settings
object with get/set and flush-to-disk, without wiring UI yet.
**Done when:** known keys load with defaults and changes persist on save.

### A5. Unit test harness — DONE
Add an xunit test project (`OpenGothic.Tests`) wired into the solution. Tests must be
hermetic: temp folders and synthetic INI files only, never the real game installation,
so the suite runs on CI.
**Done when:** `dotnet test` passes on a machine with no Gothic installation.

### A6. Launch profile — DONE
Add a per-developer `launchSettings.json` that starts the game with `-g <local Gothic
path>`, plus a devmode variant. The file embeds a machine-specific path, so it is
gitignored; document the required shape instead of committing it.
**Done when:** `dotnet run --launch-profile <name>` opens the game against a local install.

---

## Milestone B — Static world rendering

### B1. Camera and matrices — DONE
Implement a camera with `View`/`Projection` (FOV, near/far from `Constants`) and a
world transform convention (right-handed, Y-up, matching `Mathematics.ToXna`).
**Done when:** the world grid can be viewed from an arbitrary position/orientation.

### B2. Mesh renderer — DONE
Draw `WorldGrid` cells using `BasicEffect` with the loaded diffuse textures.
Only draw cells in the view frustum.
**Done when:** a ZEN world renders recognizable textured terrain and static props.

### B3. Sky, fog and lighting — DONE
Sky color/background, distance fog matching `Constants.Fog*`, and finish the lighting
story (the renderer currently uses a fixed two-light rig with specular disabled; see
`Documentation/`).
**Done when:** the world looks grounded rather than floating in a void.

### B4. Debug overlay — DONE
Wire an on-screen debug overlay (FPS, camera position, and the `Renderer` frame stats
`DrawCalls`/`Triangles`) plus optional wireframe/bbox draw.
**Done when:** stats are visible on screen and toggleable.

### B5. Wireframe and bounds debug draw — DONE
Draw wireframe and cell/vob bounding boxes through `Renderer`'s `customMaterial` override,
toggled alongside the debug overlay. Wireframe replaces lighting/texture with a debug
colour so edges stay readable; bounds draw one box per cell plus one per vob. Independently
toggleable (F2/F3), off by default, with the active views reported in the overlay.
**Done when:** wireframe and bounds can be toggled while walking a level.

---

## Milestone C — Models and animation

### C1. Model rendering — DONE
Render `DrModel` hierarchies with `SkinnedEffect`/`BasicEffect`; map `DrMeshPart` +
`DrMeshPartTag` material to effects.
**Done when:** a static MDL model appears correctly placed and scaled in a world.

Measured from real data — see
[Documentation/Specifications/gothic-model-format.md](Documentation/Specifications/gothic-model-format.md)
for the full write-up:

- A level names a model in the authoring format (`.ASC`/`.MDS`); the file on disk is
  `STEM.MDL`, or the `STEM.MDH` + `STEM.MDM` pair. Only the stem is meaningful.
- `ISoftSkinWeight.NodeIndex` is a **hierarchy** node index, not an index into
  `ISoftSkinMesh.Nodes`. The weight node index must be translated to the skin's joint slot
  before being handed to the GPU.
- Weight positions are bone-local and all weights of one vertex resolve to the same
  model-space point, so the bind position must be rebuilt from the weights.
- `SoftSkinMesh.Mesh.Positions` is unreliable for skinned meshes (garbage bounding boxes),
  so it cannot supply the bind position.
- Absolute bind transforms must be composed in **hierarchy order** as
  `local * parentBind` (row-vector). The opposite order gives a ~23-unit average spread
  between a vertex's own weights.
- `RootTranslation` belongs on the **root bone's default pose**, not in the bind matrices.
  With it there, actor feet land at y ≈ 0; props are unaffected because their offset is
  zero.
- `DrModel`/`DrModelInstance` compose `inverseBind * boneWorld`, so the joint stores
  `Matrix.Invert(bindTransform)`, not the bind transform itself.
- `SkinnedEffect` supports 72 bones. The shipped `newworld.zen` models top out at 36
  joints per mesh and 47 bones total, so no custom effect is needed.

Skinned parts cull like any other part: their `DrMeshPart.BoundingBox` is built from the
bind positions the loader rebuilds, so it describes exactly what is drawn in the bind pose.

See [Documentation/README.md](Documentation/README.md#models) for the implemented render
path and the debug switches used to inspect a model.

### C2. Animation clips
Load the animation data a model references and expose it as playable `AnimationClip`s,
without playing them yet.

C1 deliberately left clip loading out of `LoadModel`: the `.MDL` script that lists clips does
not parse for the models measured, so the `.MDS`/`.MSB` path has to be established first.
Start with a data audit of which shipped models actually reference clips and where they live.

**Done when:** a model's clips are enumerated and one clip's frames and bone tracks are
readable, with an audit of how many shipped models have clips at all.

### C3. Animation playback
Play `AnimationClip`s on a `DrModelInstance` with correct looping and blending between clips.
**Done when:** a model plays an idle/animation clip at the right speed and loops without a
pop.

### C4. Animated culling boxes
A skinned part's box is its **bind pose**, which stops being true once a clip moves the
skeleton: a swinging limb leaves the box and gets frustum-culled. Playing a clip must
therefore grow each skinned part's `BoundingBox` to cover the whole clip — a per-part update,
with no change to the draw path. Compute it from the clip's bone poses (or precompute a union
per clip), not by disabling culling.
**Done when:** no limb vanishes at the edge of the view while a model animates, and an
off-screen animating model still draws almost nothing.

### C5. Vob hierarchy and attach points
C1 places a model vob at its own ZEN rotation/translation, but the vob **hierarchy** is still
flattened: the child walk in `Assets.World.cs` enqueues each child with the parent's
transform and then recomputes the child's from its own fields alone, so the parent transform
is discarded. Nested vobs therefore do not follow their parent.

Separately, `IModelMesh.Attachments` names attach points on a model (93 in the shipped
`newworld.zen` data). An actor's armour and weapon meshes are meant to follow those named
nodes, which `ModelNode` cannot express yet.

Add an `AttachNode` that resolves an attach-point name to a model bone and inherits that
bone's world transform, and compose the parent transform in the vob walk so nested vobs
inherit it.
**Done when:** a child vob follows its parent instead of sitting at its own absolute
position, and a model parented to another model's attach point follows that bone without a
manual offset.

---

## Milestone D — Physics and movement

### D1. Static collision
Build a BepuPhysics2 static collider set from the ZEN world mesh (terrain + static
vobs).
**Done when:** a test capsule rests on the terrain and cannot pass through walls.

### D2. Character controller
Capsule character with gravity, slope handling, step-up, and slide-along-wall
response.
**Done when:** the capsule walks the terrain without jitter or falling through.

### D3. Player input & camera modes
WASD + mouse look, first/third-person camera, and collision-aware movement.
**Done when:** the developer can walk around a level with mouse and keyboard.

---

## Milestone E — Interaction and world objects

### E1. Vob tree
Generalize ZEN `IVirtualObject` traversal into a typed vob tree (NPC, item,
interactive/mobsi, trigger, light, sound, decal, particle system).
**Done when:** all vob kinds are enumerated and queryable by position/tag.

### E2. Focus & raycast
Crosshair focus: ray/mesh test against focused vobs within reach, highlight support.
**Done when:** looking at a vob and pressing the interact key resolves the target.

### E3. Interactions
Chests/doors/levers/switches: state, animation, sound hooks, and script callbacks.
**Done when:** a door opens and a chest interaction round-trips state.

### E4. Items in world
Render and pick up world items; inventory add/remove core.
**Done when:** an item can be picked up and appears in a minimal inventory list.

---

## Milestone F — NPCs and AI

### F1. Npc entity
Npc runtime: attributes, guild, animation state machine basics, name/messages hooks.
**Done when:** an Npc spawns from a ZEN instance, idles, and can be damaged.

### F2. Way matrix & routines
Parse waypoints/waymatrix; daily routines (`TA`, `RTN`) move NPCs between points.
**Done when:** an NPC follows a simple authored routine.

### F3. AI states & perception
Idle/guard/follow/attack states, perception events (see/hear/damage), and AI queue.
**Done when:** an NPC notices the player and transitions state plausibly.

---

## Milestone G — Daedalus scripting (game logic)

### G1. Daedalus VM
Integrate ZenKit `DaedalusScript`/`DaedalusVm`; load `Gothic.dat`/`OU`, register
core external functions needed to boot.
**Done when:** startup scripts run without unimplemented-call crashes.

### G2. Script classes & events
Expose NPC/item/world script objects and the event pipeline the scripts expect.
**Done when:** a scripted NPC interaction fires its Daedalus side effects.

### G3. Dialogs
Dialog choice tree, NPC/player lines, voice hooks, and dialog camera.
**Done when:** a full conversation with a named NPC can be completed.

### G4. Quests & journal
Quest log, chapter state, and the document/mission log the scripts drive.
**Done when:** a quest can be started, progressed, and shown in the journal.

---

## Milestone H — Combat

### H1. Weapons & bullets
Melee/ranged weapon handling, `Bullet` entities, projectile collision.
**Done when:** a ranged attack hits a target and deals damage.

### H2. Damage & fight AI
Damage calculator, hit/block reactions, fight algorithm state selection.
**Done when:** an NPC fights back and a duel can be won or lost.

---

## Milestone I — UI (Myra)

### I1. HUD
Health/mana/breath bars and status messages.
**Done when:** bars reflect live player state.

### I2. Menus
Main menu, pause, options, save/load screens.
**Done when:** the game can be started and paused from Myra UI.

### I3. Inventory & trade
Grid inventory with equip/use, and trader UI.
**Done when:** items can be equipped and traded through UI.

---

## Milestone J — Audio

### J1. Sound effects
Sound-definition driven SFX: footsteps, hits, ambience, dialog voices.
**Done when:** footsteps and one ambient loop play positionally.

### J2. Music
Zone/day-time music via the music definitions and DMusic.
**Done when:** music changes with location and is controllable in options.

---

## Milestone K — Sessions and persistence

### K1. Game session
`GameSession` owning world, camera, script VM, time; world change with waypoint.
**Done when:** moving between two worlds works and preserves hero state.

### K2. Save / load
Serialize hero, world state, inventory, quests; slot management with screenshots.
**Done when:** save, quit, and load restores the same game state.

---

## Milestone L — Story completion

### L1. Playable main quest
Enough scripting, dialogs, combat, and triggers to complete the main quest line.
**Done when:** the original main story can be finished end-to-end.

### L2. Addon (Night of the Raven)
Addon content: new world, quests, mechanics.
**Done when:** the addon story can be finished end-to-end.

---

## Milestone M — Parity and polish

### M1. Custom shader system
Replace stock MonoGame effects with a custom shader pipeline.
### M2. Visual parity
Lighting, shadows, materials, sky, fog, particles matched to the original.
### M3. UI parity
Myra theme/layout matched to the original interface.
### M4. Performance
Draw batching, instancing, culling, and frame pacing.
