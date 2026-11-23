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
- Models carry their script's animation clips in `DrModel.Animations`, plus the playback
  metadata a `ModelAnimationSet` keeps. Clips load per model (nothing beyond the world's
  scripts is read), are shared by models with the same skeleton, and are not played yet, so
  everything renders in the bind pose; `-noanims` skips them. `-animdump:<script>` with
  `-animclip:<clip>` prints a script's clip list and a clip's first-frame bone tracks.
  Format and model-to-script mapping are in
  `Documentation/Specifications/gothic-animation-format.md`.
- No animation playback yet. No input, physics, audio, or scripting yet.
- UI exists: Myra `Desktop` rooted at a `Hud` panel with two independently anchored blocks
  — an always-visible `Controls` list (bottom-right) and a toggleable `DebugOverlay`
  (top-left, F1) showing FPS, camera position, draw-call and triangle counts and the active
  debug views. Layouts are `.xmmp` exported with `myra-export`; all UI lives in
  `OpenGothic/UI/`.
- Debug views: F2 toggles wireframe, F3 toggles bounding boxes; independent of F1 and off by
  default. Both can be started on from the command line with `-wireframe`/`-bounds`.
- Milestone A (Foundation) complete. **Milestone B (Static world rendering) complete.**
  **C1–C5 complete** (model rendering, clips, playback, animated culling boxes, vob hierarchy).
  Current milestone: **C — Models and animation**, next task **C6**.

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

### C2. Animation clips — DONE
Load the animation data a model references and expose it as playable `AnimationClip`s,
without playing them yet.

Clips are not in any model file: a *model script* (`.MSB`; Gothic 1's `.MDS` is the same
format) lists them, and each clip is a separate `<SCRIPT>-<CLIP>.MAN`. A script owns its
models through the skeleton name it declares and through the `.ASC` meshes it can drive, and
`Assets` indexes both once. A clip's channel indices address the *script's* skeleton, not the
model's, so channels are remapped by node name and channels the model cannot pose are dropped
— the human skeleton's nine `ZS_*` attach nodes are the common case.

Converted sets are cached by script and bone-name sequence so models sharing a skeleton share
one set of clip objects, and clips load per model rather than for the whole game. Audit in
[Specifications/gothic-animation-format.md](Documentation/Specifications/gothic-animation-format.md):
117 scripts, 3,949 clips, no missing `.MAN`, 37 of 63 `newworld` model names scripted (the
rest are static props), ≈ 2.3 M keyframes total.

**Done when:** a model's clips are enumerated and one clip's frames and bone tracks are
readable, with an audit of how many shipped models have clips at all. — `-animdump:HUMANS
-animclip:S_RUN` enumerates and prints them; `ModelAnimationSet` exposes names, keyframes and
per-clip metadata.

### C3. Animation playback — DONE
Play `AnimationClip`s on a `DrModelInstance` with correct looping and blending between clips.
Each clip carries its script-side parameters in `AnimationClip.Tag` as a `ModelClipInfo`; the
rules for honouring them were resolved from the shipped data and are recorded in
`Documentation/Specifications/gothic-animation-format.md`:

- **Which fps governs.** The `.MAN` value, as C2 already used: it disagrees with the script on
  161 clips and is the only file that stores the frames. The 149 clips with `fps == 1` are all
  single-frame poses.
- **`AnimationDirection.Backward`** mirrors the sample walk (time 0 = last stored frame), and
  for a looping clip must skip the closing wrap key. 197 clips and 542 aliases are backward.
- **Looping.** A clip with `Next` naming itself loops; another clip means a one-shot hand-over;
  nothing means stop. Override: any clip carrying a `ComboWindow` event is a one-shot (17
  clips).
- **Aliases** keep their own next/layer/blend/direction and reuse the target's samples — 782
  loop, 141 reversed — so resolving one to a bare rename is wrong.
- **`FirstFrame`/`LastFrame`** index the script's shared frame timeline, not the clip's samples;
  they are only for converting event frames.
- **`speed` is ignored**: all 3,949 shipped clips store zero.

**Done when:** a model plays an idle/animation clip at the right speed and loops without a
pop. — `-play:<clip>` on a `-model:` plays it through `AnimatedModelNode` and
`AnimationController`; a looping clip wraps pixel-exactly at its duration and reverse clips
mirror the forward walk.

### C4. Animated culling boxes — DONE
A skinned part's box is its **bind pose**, which stops being true once a clip moves the
skeleton: a swinging limb leaves the box and gets frustum-culled. Playing a clip must
therefore grow each skinned part's `BoundingBox` to cover the whole clip — a per-part update,
with no change to the draw path. Compute it from the clip's bone poses (or precompute a union
per clip), not by disabling culling.
**Done when:** no limb vanishes at the edge of the view while a model animates, and an
off-screen animating model still draws almost nothing. — A playing node measures its clip once
per model and merges, per part, the part box transformed by every skin joint into
`DrMeshPartTag.CullBounds`; `DrModelTag.MeasuredClips` keys the "already measured" question by
clip id. `Renderer` culls with that box when given one. With the model on the right edge
(`-modeloffset:270`) the bind-box build loses 99 px against `-nocull`; the widened box matches
`-nocull` at every offset from 120 to 300.

### C5. Vob hierarchy — DONE
C1 places a model vob at its own ZEN rotation/translation. Gothic stores every vob transform in
**world space**, so a child already lands in the right place and the flat loader was not
misplacing it; what it dropped was the link, which only matters once a parent moves.

`Assets.World.cs` now derives each child's local transform (`world * inverse(parentWorld)`) and
attaches the node to its parent through `SceneNode.Children`; a vob with children but no visual
gets a bare node for them to hang from. `SceneNode.WorldTransform` composes the chain and caches
it, and a transform change invalidates the node and its descendants. Over the 1,870 parented
vobs of `newworld.zen` the recomposition error is at most 0.0125 units, and the static world
renders pixel-identically.
**Done when:** a child vob follows its parent instead of sitting at its own absolute position. —
`SceneNode` composes local transforms up the chain; `SceneNodeTests` covers composition, a moved
parent and a moved grandparent, and the world's placement is unchanged. A mover that changes a
vob's transform also has to re-register the affected subtrees with their grid cells and bounds,
which is why this lands before movers rather than with them.

### C6. Attach points
`IModelMesh.Attachments` names attach points on a model (93 in the shipped `newworld.zen`
data), and an actor's armour and weapon meshes are meant to follow those named nodes, which
`ModelNode` cannot express yet.

Add an `AttachNode` that resolves an attach-point name to a model bone, is added to the model
node's `Children`, and sets its local transform from that bone (once, or per frame if the pose
moves). This is also where the clips' nine `ZS_*` channels that a bare armour model cannot pose
become relevant: those nodes exist for attaching, not for deforming.
**Done when:** a model parented to another model's attach point follows that bone without a
manual offset.

### C7. Animation layers
The script gives every clip a `Layer`, and the reference blends layers so an upper-body
attack can play over a moving lower body. `AnimationClipTag` already carries the field, but
playback currently runs one clip at a time, so the value is unused.

Blend the active clips by layer, using `AnimationBlendLayer.BoneFilter` to mask each layer to
the bones it drives. The lower layer stays authoritative for unclaimed bones.
**Done when:** a walk and an attack can play simultaneously on one model without the attack
resetting the legs.

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
`Scene/AnimatedModelNode` already plays a named clip and follows the script's `Next` chains;
this task is the body-state layer that decides *which* clip (idle, walk, attack) an NPC asks
for and when to interrupt one.
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
