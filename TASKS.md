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
  scripts is read) and are shared by models with the same skeleton; `-noanims` skips them.
  `-animdump:<script>` with `-animclip:<clip>` prints a script's clip list and a clip's
  first-frame bone tracks. Format and model-to-script mapping are in
  `Documentation/Specifications/gothic-animation-format.md`.
- Animation playback exists: `-play:<clip>` runs a clip through `AnimationData` (looping,
  `Next` chaining, script blend times, reverse clips), `-overlay:<clip>` adds a clip on its
  script layer so an upper-body gesture leaves the legs running, and a playing clip widens its
  parts' cull bounds. `-freeze`/`-animtime:` pin a pose for captures.
- A model attached with `-attach:<point>:<model>` follows that bone of the `-model:` spawn.
- Composed models build one `DrModel` from a shared skeleton and meshes that may each carry
  their own: `-compose:HUMANS:HUM_BODY_NAKED0,ARMOR_PRISONER`. This is what carries the
  `ZS_*` attach nodes, which no armour file has. `-modelrot:<degrees>` turns the spawn so a
  capture can show its front.
- Static collision exists: BepuPhysics2 triangle meshes for the level and for static vobs,
  grouped by material friction with water as a contact-free sensor. It streams: the level mesh
  is split into 2000-unit chunks and the vobs are collected as sources, with a 3000-unit
  neighbourhood loaded around the player and refreshed every 256 units. `-physics` also runs
  the walk, step, wall and capsule checks.
- A kinematic character controller walks the terrain. It is the reference's chest-height ghost
  capsule: substepped movement that stops at a blocked step (no projection/slide), a downward
  ground ray with a 50-unit step snap, and a forward-only wall bounce. A plain launch starts it
  in first person: WASD, mouse look, F5 for the chase camera (`-thirdperson`), `-nowalk` for the
  old static diagnostic camera.
- Vob placement follows `ObjVisual`: `L * transpose(R) * L` rotation conversion, `show_visual`
  and `cd_dynamic` gates, raw model bounds for props (their hierarchy root translation is
  cancelled, so the bookstand sits on the floor), and the root translation kept on
  `DrModelTag` for actor-like spawns.
- The ZEN vobs form a typed `VobNode` tree (`NPC`, item, interactive/mobsi, trigger, light,
  sound, decal, particle system, …) with raw type, name/tag, bounds and query helpers; a
  `newworld.zen` load is 23,288 nodes. It is the only transform hierarchy: `SceneNode` is a flat
  visual attached to its vob, and the renderer/batched meshes work from flat per-cell lists.
  `-vobstats` and `-vobfind:x,y,z` inspect it.
- The walking player has a placeholder animated body: `HUMANS` composed with `HUM_BODY_NAKED0`,
  `ARMOR_PRISONER` and the `HUM_HEAD_PONY.MMB` head, placed at the capsule's feet. It idles on
  `S_RUN`, runs on `S_RUNL` and walks on `S_WALKL` when walk mode is on; Shift toggles walk
  mode. The body is drawn in third person only, because the first-person camera sits inside
  the head.
- Focus is the crosshair ray: the nearest NPC, item or interactive within 200 units, tested
  against its world box and visual parts, with a physics occlusion check. The target is
  outlined in cyan, `DebugOverlay` names it, and E resolves it. `-interact` reproduces the key
  for a capture.
- Doors, chests, switches and wheels have a state machine: use advances the authored state,
  plays `S_S0`/`T_S0_2_S1`/`S_S1`, rejects locks and item requirements, and raises the future
  audio/script hook. The focus overlay shows the live state and `-interact[:<count>]` replays
  the action.
- World items render from their instance-named `.MRM`; E picks one up into the stack-based
  `Inventory`, removes its geometry (including one batch instance), marks it taken, and adds it
  to the bottom-left inventory panel. `Inventory.Remove` is the removal core.
- NPC runtime data lives on `NpcVob` (instance, guild, level, HP, damage and message hooks).
  The shipped ZEN has no NPCs; scripts spawn them in G2, so no placeholder NPC or body exists.
- The Daedalus VM boots `GOTHIC.DAT` (74,649 symbols) and loads `Ou.dat` (20,826 cutscene
  blocks) through the four startup/init entry points. Unimplemented externals log once and use
  the VM default; the VM and cutscene library stay alive for the dialog and quest layers.
- G2 binds script objects/events: 406 NPC and 55 item instances from `WLD_INSERTNPC`/`ITEM`,
  `HLP_GETNPC`, `PC_HERO`, and `LOG_ADDENTRY`. Each NPC binding owns a detached `NpcVob`.
- G3 completes the dialog engine: 4,139 `C_INFO` instances, condition/choice filtering,
  `AI_Output` text from `Ou.dat`, `Info_AddChoice`/`Ai_StopProcessInfos`, and a Myra choice
  panel. `-talk` and `-talkui` run conversations; the dialog camera and voice playback are
  implemented by F1.5.
- F1.5 places 356 human script NPC bodies at their waynet spawn points, composes armour/body
  and head with texture variants, adds a kinematic capsule and the E-to-talk focus path, and
  runs the two-shot dialog camera plus `DialogLine.Voice`. Fifty non-human bodies await the
  model-script skeleton layer.
- Rendering lives in `OpenGothic/Graphics` (`Renderer`, `Sky`, `WorldRenderer`, `Camera`,
  `DebugDraw`, materials, scene/model/mesh visuals and vertex formats).
- ZenKit is pinned to `1.3.0-cs.1`, matching the parser version the reference C++ client uses.
  The native ZenKitCS dependency is scheduled for a managed C# port in **N1/N2** because its
  handle lifetimes and native allocation failures are the remaining low-level risk.
- Models skip submeshes whose material has no texture; the shipped data uses those as hidden
  placeholder faces (`REPAIR_PLANK`'s `ZM_REP_HIDDEN` was a white triangle on screen).
- No audio yet; scripting is live in G1–G3.
- UI exists: Myra `Desktop` rooted at a `Hud` panel with four independently anchored blocks
  — an always-visible `Controls` list (bottom-right), an `InventoryPanel` (bottom-left), and a
  toggleable `DebugOverlay` (top-left, F1) showing FPS, camera position, draw-call and triangle
  counts, the active debug views and the focused vob. Layouts are `.xmmp` exported with
  `myra-export`; all UI lives in `OpenGothic/UI/`.
- Debug views: F2 toggles wireframe, F3 toggles bounding boxes; independent of F1 and off by
  default. Both can be started on from the command line with `-wireframe`/`-bounds`.
- Milestone A (Foundation) complete. **Milestone B (Static world rendering) complete.**
  **Milestone C (Models and animation) complete** — C1–C8: model rendering, clips, playback,
  animated culling boxes, vob hierarchy, attach points, animation layers, composed models.
  **Milestone D (Physics and movement) complete** — D1–D3: static collision, kinematic
  character controller, player input, first/third-person cameras and streamed collision.
  **Milestone E (Interaction and world objects) complete** — E1–E4: typed vob tree, placeholder
  player avatar, focus/raycast, door/chest interactions and item pickup with inventory.
  F1's NPC runtime data, G1's Daedalus VM boot, G2's script-object/event bridge and G3's
  dialog engine/UI are complete. F1.5 places the **356 human** script NPCs, adds their
  collision/focus path, and completes the dialog camera/voice hook. F1.6 builds the 50
  non-human bodies lazily from their model scripts. F2 drives daily routines from a game
  clock, walks NPCs between grounded routine points, and is complete. Cut-out foliage
  materials render with `AlphaTestEffect`. F3.1 (script AI states, chaining and routine
  actions) is complete; F3.2 (AI queue) is next.

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
pop. — `-play:<clip>` on a `-model:` plays it through `AnimationData` and
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

### C6. Attach points — DONE
An actor's armour and weapons are models drawn on named bones of the actor's skeleton. The
scene graph is not the right vehicle for that: an attachment is not independently placed, it
is placed by the pose.

`ModelNode.BonesAttachments` holds `ModelBoneAttachment` entries — a nested `DrModelInstance`,
the `DrModelBone` it follows, and a local `Transform` — the same shape Nursia's
`NursiaModelNode` uses. `WorldRenderer.DrawModel` draws each one as
`attachment.Transform * boneGlobal * node.WorldTransform`, so it follows the pose without a
scene node of its own. A multi-resolution mesh, the form weapons ship in, is wrapped into a
one-bone `DrModel` by `ModelBoneAttachment.WrapMesh` and takes the same path.
**Done when:** a model parented to another model's attach point follows that bone without a
manual offset. — `-attach:<point>:<model>` attaches a model or `.MRM` to a bone of the
`-model:` spawn; a bench on `BIP01 HEAD` rides the head and moves 11,677 px between two frozen
poses, and `ITMW_1H_MACHETE_01.MRM` attaches and draws.

Two gaps remain, both data rather than design:
- **`ZS_*` points are not loadable yet.** No shipped model carries them: armour `.MDL`s omit
  the nine nodes, `HUMANS.MDH` has them without a matching `.MDL`/`.MDM`, and
  `HUM_BODY_NAKED0.MDM` has no `.MDH`. Attaching to `ZS_RIGHTHAND` needs the avatar
  composition — one model built from a hierarchy and meshes with different stems, sharing the
  skeleton. **Resolved in C8** by `Assets.GetComposedModel`.
- **An item's grip offset is unset.** `ModelBoneAttachment.Transform` is the slot; Gothic item
  definitions carry the scale/offset and name the attach node, and arrive with the item work.

Sharp edges: `DrModel.FindBoneByName` is case-sensitive; `WrapMesh` shares the cached `DrMesh`
and reassigns its `ParentBone` (safe only because the wrapper's single bone is identity, and
the wrapper must not be disposed); attachments are not registered in `cell.ObjectBounds`, so
the `-bounds` view omits them.

### C7. Animation layers — DONE
The script gives every clip a `Layer`, and the reference blends layers so an upper-body
attack can play over a moving lower body. `AnimationClipTag` already carries the field, but
playback used to run one clip at a time, so the value was unused.

Blend the active clips by layer, using `AnimationBlendLayer.BoneFilter` to mask each layer to
the bones it drives. The lower layer stays authoritative for unclaimed bones.

Over `HUMANS`, layer 1 is 644 full-body clips (locomotion **and** attacks), layer 2 is 67
upper-body overrides, 4 is 18 bow clips, 9 is 9 head-only `C_LOOK_*`, 11 is 9 arm `C_POINT_*`,
and 20 is 37 turns — so "walk and attack" is not shipped as a layered case; attacks reset the
legs by design and the gestures are the layered ones.

`AnimationData` puts every active clip in one `AnimationBlendNode` ordered by layer: each
layer's `BoneFilter` is its bones minus those of higher layers, and its `TimeOffset` turns the
controller's single clock into that clip's own time, so a layered change needs no second
instance. `-overlay:<clip>` exercises it.
**Done when:** an upper-body clip plays over a moving lower body without disturbing the legs. —
`C_POINT_1` over `S_RUN` changes 6,539 px confined to the upper body (y 12–142), where the same
clip as the base changes 17,103 px across the whole model (y 0–240); captures without an
overlay are pixel-identical to before the task.

Known gaps: a layered change snaps, where the reference ramps a new layer in over its blend
time; a one-shot overlay holds its last frame until `StopLayer`/`Stop`, which the game's
overlay lifecycle (F1) will drive.

### C8. Composed model — DONE
An actor is not one file: the body (`HUM_BODY_NAKED0.MDM`) and its armour (`ARMOR_*.MDL`) share
one skeleton, and the `ZS_*` attach nodes live only in that skeleton. `LoadModel` required a
matching stem for a `.MDH`/`.MDM` pair, which is why no loadable model carried those nodes and
C6 could only attach to ordinary bones.

`Assets.GetComposedModel(skeleton, meshes...)` builds one `DrModel` from an explicit hierarchy
plus one or more meshes: each source's nodes are mapped to the merged bones by name, the same
mapping clip channels use, and a bare `.MDM` maps identity because its weights already address
the shared skeleton. Each skinned mesh keeps its **own authored bind** rather than the merged
rest: the armour's translations and scales match `HUMANS.MDH` but several rest rotations differ
by up to ~34°, and using the merged bind displaces its weighted vertices by up to 34 units.
`inverseBind * boneWorld` then carries the mesh from its authored rest into the skeleton's, as
an animation would. Because the vertex data stays in authored space, each composed part's cull
box is computed separately in the merged rest. `-compose:<skeleton>:<mesh>[,<mesh>...]` exposes
it; `-modelrot:<degrees>` turns the spawn so a capture can show the front.

Data: `HUMANS.MDH` has 34 nodes including the nine `ZS_*`; `HUM_BODY_NAKED0.MDM`'s weights stop
at node 32; all 21 `ARMOR_*.MDL` files use a 25-name skeleton whose names are a subset of
`HUMANS`. Other creatures' bare `.MDM`s address their own skeletons (up to 77 nodes) and are
rejected unless the explicit skeleton covers them. None of the human armours has a rigid
attachment mesh, so the static path only sees single-hierarchy models today.
**Done when:** a body and an armour render on one skeleton, and a weapon attached to
`ZS_RIGHTHAND` lands in the hand. — `-compose:HUMANS:HUM_BODY_NAKED0,ARMOR_PRISONER` adds the
body as one mesh (+1 draw call, +568 triangles) on top of the standalone armour; the composed
`HUMANS` set maps all 784 clips with **0 channels the model cannot pose** (standalone armour
drops 1,710); `-attach:ZS_RIGHTHAND:ITMW_1H_MACHETE_01.MRM` is drawn at the hand and moves with
the arm (12,279 px between frozen `S_RUN` poses); a composed model animated at the view edge
matches `-nocull` exactly. The item's grip offset/orientation is still unset, as C6 recorded —
it arrives with the item definitions.

---

## Milestone D — Physics and movement

### D1. Static collision — DONE
Static collision is BepuPhysics2 triangle meshes (`OpenGothic/Physics/PhysicsWorld.cs`). The
render path was not usable as a source: it skips texture-less materials and keeps only what
fits a cell, so the level mesh is triangulated separately in `Assets.World.cs` from the raw
ZEN mesh — every polygon except `material.disableCollision`, including invisible walls and
ghost occluders. Static vobs contribute the geometry the reference collides with: a full
`.MRM`, and only the rigid attachment meshes of a model; a skinned mesh has no static shape
and gets no collider. Transforms are the renderer's (`MeshNode.WorldTransform`, the instance
transform, or `boneGlobal * ModelNode.WorldTransform`).

Two findings shaped the implementation. Bepu's meshes are **one-sided**, while Gothic's
winding is inconsistent (`newworld.zen`: 130,515 polygons face up, 202,939 down, 127,829
steep), so each triangle is added in both directions; with a single winding the capsule fell
through roughly half the terrain. Friction comes from the material group (the reference's
table) and is stored per collidable, because Bepu has no per-shape material; water is a
sensor that refuses contacts. The game's unit is the centimetre, so gravity is `-981`.

The CPU triangles cost ~15–25 s and a few hundred MB for `newworld.zen` (367,394 terrain +
1,465,637 vob source triangles, doubled for the two-sided mesh), so they are built only when
`DebugSettings.CollisionEnabled` is set by `-physics`.
**Done when:** a test capsule rests on the terrain and cannot pass through walls. —
`-physics` drops a capsule onto the plaza and drives it into the nearest wall: it rests on
Terrain (center 29.2 above the surface, speed 0.78) and the wall push ends with the center 35
units from the wall for a radius of 30, i.e. blocked. Both lines are in `log.txt`. Rendering
captures are pixel-identical with collision off.

Known limits: the build is opt-in and whole-level; there is no streaming, no dynamic-vob or
skinned collision, and `-nomodels` removes vob colliders. The doubled CPU soup is retained
after the meshes are built. Details in
[Specifications/gothic-collision-format.md](Documentation/Specifications/gothic-collision-format.md).

### D2. Character controller — DONE
Capsule character with gravity, slope handling, step-up, and slide-along-wall response.
`OpenGothic/Physics/CharacterController.cs` is a kinematic capsule rather than a dynamic
body: it sweeps the frame's displacement against the static meshes and resolves the contact
itself. That keeps the pose upright and makes the behaviours explicit.

- **Grounding** is a short downward ray from the capsule bottom. A normal above ~57° is
  walkable; the controller keeps a 0.5-unit clearance, snaps down up to 12 units, and only
  corrects a rise by the clearance so a real step goes through the step path.
- **Wall slide** projects the remaining displacement onto the contact plane for up to four
  iterations. A 0.5-unit skin keeps the next sweep from starting inside the surface.
- **Step-up** probes with a ray just past the capsule front for a walkable surface within 35
  units and moves there only if the capsule fits. Shape sweeps cannot do this reliably: the
  lower sphere grazes the step's top corner, which is why the first attempt failed.
- **Cost**: the physics build is limited to a 3000-unit neighbourhood — 4,601 terrain and
  32,755 vob source triangles, 9 static bodies, under a second — instead of the whole level.

**Done when:** the capsule walks the terrain without jitter or falling through. — `-physics`
walks 2,390 units in 8 s with 100% grounded, 0.5..0.5 clearance, a maximum per-step rise of
0.32 and no fall-throughs; in a flat test world the same controller climbs a 30-unit block by
exactly 30 and slides 382 units along a wall while holding 30.4 from it for a radius of 30.
Four hermetic controller tests cover the same paths, and the old D1 drop/wall checks still
pass. Rendering captures are pixel-identical.

Known limits: the neighbourhood is fixed at build time, so walking beyond 3000 units would
leave collision behind; streaming around the moving player is D3. `-physics` is still opt-in
until input needs it. Details in
[Specifications/gothic-collision-format.md](Documentation/Specifications/gothic-collision-format.md).

### D3. Player input & camera modes — DONE
WASD + mouse look, first/third-person camera, and collision-aware movement. This is where the
kinematic controller meets the game: enable the collision build for normal play, stream the
collision neighbourhood as the player moves, and drive the controller from input.

A plain launch now starts the capsule in first person on a placeholder harbour-plaza point
(the scripted start arrives with the session in K1). **WASD** moves relative to yaw, the
**mouse** looks, **F5** toggles first/third person, and `-thirdperson` starts in the chase
camera; the chase camera ray-casts backwards from the head so a wall does not cut through it.
Diagnostics (`-model`, `-compose`, `-attach`, `-cam`, `-lookat`, `-screenshot`, `-physics`)
keep the fixed camera, and `-nowalk` forces it back.

Collision streams: the level mesh is bucketed into 2000-unit chunks while it is read, the
static vobs are collected once as sources (one per mesh/instance, one per model with rigid
parts), and `PhysicsWorld.Update` reloads a 3000-unit neighbourhood after the player moves 256
units, with a 500-unit unload margin. A loaded set for the harbour is 16 chunks and 304 vob
sources: 9.7k terrain + 30.2k vob source triangles, 385 static bodies. Walking is the default,
so the CPU triangles are built for any launch that is not a diagnostic.

The HUD controls list was regenerated with the movement keys. A no-texture model submesh is
now skipped instead of drawn white, matching the reference: the shipped data uses those as
hidden faces, and `REPAIR_PLANK`'s `ZM_REP_HIDDEN` had been a visible white triangle in the
harbour.

**Done when:** the developer can walk around a level with mouse and keyboard. — The developer
walked from the harbour into the city and confirmed. The self-checks still pass (walk 2,387
units with 100% grounding, 30-unit step, capsule rest/wall), and the standard model captures
are unchanged. Details in
[Specifications/gothic-collision-format.md](Documentation/Specifications/gothic-collision-format.md).

The controller was then revised to follow the reference's movement model after two "stuck"
reports (a wall at the harbour and a plank pile): the collision shape is now the reference's
chest-height ghost capsule instead of a full-height capsule, movement is substepped and stops
at the first blocked substep instead of projecting the remainder along the surface, and ground
following is a downward ray with a 50-unit step snap. A blocked backward move therefore stops
instead of becoming a side step. `-yaw:<degrees>` was added so a diagnostic walk can be aimed
without `-lookat:` measuring from the camera. BepuPhysics2 2.4 has no public shape-overlap
query, so the Bullet `contactTest` of the reference is approximated with a sweep; the difference
is documented in the specification.

Vob placement was then checked against `Try/OpenGothic` as well. The rotation now uses the
reference's handedness conversion (`L * transpose(R) * L`); `show_visual` gates both rendering
and collision, and `cd_dynamic` gates vob collision. A prop model cancels the hierarchy root
translation, because the reference does so for props
(`ObjVisual::setYTranslationEnable(false)`); this is why the harbour bookstand had been hovering
`133.6` units above its floor and now sits on it. The actor bind keeps the translation on the
root bone instead, so a spawn needs no extra offset. ZenKit was upgraded from `0.1.13` to
`1.3.0-cs.1`, matching the reference's ZenKit 1.3 parser; only two enum casts were required.
Details in [Specifications/zen-vob-transform.md](Documentation/Specifications/zen-vob-transform.md).

---

## Milestone E — Interaction and world objects

### E1. Vob tree — DONE
Generalize ZEN `IVirtualObject` traversal into a typed vob tree (NPC, item,
interactive/mobsi, trigger, light, sound, decal, particle system).

`Assets.World.cs` builds a `VobNode` for every virtual object, including those with no visual
(spawn points, triggers, sounds, lights, zones, decals, particle systems). The category mapping
follows the reference's `Vob::load`; generic vobs are classified by visual, so decals and
particle systems are separate from static vobs. Each node keeps the raw ZenKit object, world and
local matrices, world bounds, name/tag, category and an optional `SceneNode` visual with a
`SceneNode.Vob` backlink.

The vob tree is the **only transform hierarchy**; `SceneNode` is a flat visual with a world
matrix, and `WorldGridCell.Children` is the flat per-cell render list. Multi-resolution meshes
remain batched for drawing, with per-instance vob backlinks.

`WorldGrid.Vobs` exposes `FindNear`, `FindAt`, `FindByName`, `FindByTag`, `FindByKind` and
`CountOf`. `-vobstats` logs per-kind counts and `-vobfind:x,y,z` (`-vobradius:`) lists nearby
nodes. A `newworld.zen` load reports **23,288 nodes**: 16 level, 11,418 static, 2,331 items,
575 interactive, 3,401 lights, 2,289 spots, 1,405 decals, 656 particle systems, and the full
trigger/sound/zone/camera set.

**Done when:** all vob kinds are enumerated and queryable by position/tag. — Done; build/tests
pass and the diagnostics show the tree and the bookstand query.

### E1.5. Placeholder player avatar — DONE
The walking capsule gets a visible animated body before F1/G2 can select the real hero. It
composes `HUMANS` with `HUM_BODY_NAKED0`, `ARMOR_PRISONER` and the `HUM_HEAD_PONY.MMB` head,
places it at the capsule's feet, and plays the reference's unarmed locomotion clips: `S_RUN`
for idle, `S_RUNL` for running and `S_WALKL` in walk mode. The body is drawn in third person
only.

The skeleton bind now carries the hierarchy root translation on the root bone's default pose.
That is the reference's placement: actors keep it, props cancel it, and the avatar needs no
second offset. Directional clips carry root motion, so the avatar cancels the animated root
X/Z while the character controller owns movement. Walk mode is a Shift toggle and also has a
`-walkmode` startup switch.

Head support needed a morph-mesh loader: `.MMB` heads are `MorphMesh` data bound to
`BIP01 HEAD`, not part of the body mesh. While testing this, borrowed ZenKit handles
(`Model.Hierarchy`/`Model.Mesh`) were found to dangle when their temporary owner was collected;
the owners are now retained, which also removed random corrupted world models.

**Done when:** third person shows an animated hero with a head that faces the camera direction,
stands on the ground, and switches between idle and walk/run with movement. — Done; verified at
the harbour and in the city with third-person captures, walk mode logs `S_WALKL`, and the
loader stress run is clean.

### E2. Focus & raycast — DONE
Crosshair focus: ray/mesh test against focused vobs within reach, highlight support.
`VobTree.FindNear`/`FindAt` and `SceneNode.Vob` are ready; this task adds the ray/mesh test and
the highlight itself.

`FocusSystem` casts the crosshair ray from the capsule centre along `Camera.Forward` with a
200-unit reach. NPCs, items and the interactive family are candidates; the vob's world box is
the broad phase and a single visual's mesh-part boxes are the narrow phase. A physics ray
rejects targets behind walls, with a 20-unit slack so the target's own collider cannot hide
it. The query reuses its result object and allocates nothing per frame.

The focused vob's bounds are drawn as a cyan box, `DebugOverlay` reports the kind, tag, state
and distance, and **E** resolves the target and logs it. `-interact[:<count>]` performs the
same resolution once (or several times) on the first walking frames for captures.

**Done when:** looking at a vob and pressing the interact key resolves the target. — Done;
verified on the harbour bench in first and third person, and `-interact` logs
`Interact: Interactive 'BENCH_NW_CITY_01.ASC' (145.7)`.

### E3. Interactions — DONE
Chests/doors/levers/switches: state, animation, sound hooks, and script callbacks.
**Done when:** a door opens and a chest interaction round-trips state.

`InteractiveVob` reads the authored state count, lock, required item, trigger target and
state-change function. Doors, chests, switches and wheels start on `S_S0`; `InteractionSystem`
advances the state and plays `T_S<from>_2_S<to>`, which the clip's `Next` chains into
`S_S<to>`. Locked mobs, item-gated mobs and uses while a transition is playing are rejected.
`StateChanged` is the subscription point for audio and the script event pipeline; the authored
function and target names are logged as pending hooks. The focus overlay shows the live state,
and `-interact[:<count>]` repeats a use for captures.

**Done when:** a door opens and a chest interaction round-trips state. — Done; the chest logs
`opened (state 0->1)` then `closed (state 1->0)`, the door does the same, and a locked chest
resolves to `locked` without changing state. Build/tests pass (187 tests).

Known limits: the player attach/use animation, inventory use, lock-picking, real trigger
callbacks and sounds are later milestones; door/chest collision stays at the closed pose until
the physics source can follow the animation. Details in
[Specifications/interactions.md](Documentation/Specifications/interactions.md).

### E4. Items in world — DONE
Render and pick up world items; inventory add/remove core.
**Done when:** an item can be picked up and appears in a minimal inventory list.

World item vobs normally have `show_visual = false` and no visual name; they carry an item
instance such as `ITFO_SAUSAGE`. The loader renders the instance-named multi-resolution mesh
(`ITFO_SAUSAGE.MRM`) and joins the normal per-cell batch. If there is no instance mesh, the
item remains focusable/pickable but invisible.

E adds the item to a stack-based `Inventory`, marks the vob taken so focus skips it, and
removes its geometry — a direct node or one instance of a shared batch. The typed vob stays in
the query tree for future save/load. A bottom-left `InventoryPanel` lists the stacks and is
refreshed only when the inventory changes. `Inventory` also has the remove core.

**Done when:** an item can be picked up and appears in a minimal inventory list. — Done; two
pickups (`ITFO_SAUSAGE`, `ITFO_BACON`) disappear from the table and appear in the panel, and
both focus and the batch removal leave the remaining item visible. Build/tests pass (190
tests).

Known limits: names are script instances until the Daedalus definitions supply localized
names/icons; ZEN amounts are zero so each pickup adds one; 110 of 160 item instances have an
instance `.MRM`, while weapons/armour/torches whose visual lives only in the item script stay
invisible; drop/use/equip belong to I3. Details in
[Specifications/items.md](Documentation/Specifications/items.md).

---

## Milestone F — NPCs and AI

### F1. Npc entity — DONE
Npc runtime: attributes, guild, animation state machine basics, name/messages hooks.
`Graphics/AnimationData` already plays a named clip and follows the script's `Next` chains;
this task is the body-state layer that decides *which* clip (idle, walk, attack) an NPC asks
for and when to interrupt one.

The shipped worlds contain **no `oCNpc` vobs**: Gothic inserts NPCs from startup scripts.
There is therefore no placeholder NPC or body. The runtime container is ready for a
script-created NPC: `NpcVob` exposes the instance name, guild, level, `Hp`/`MaxHp`,
`IsDead`, `TakeDamage` and the `Say`/`LastMessage` hook, and the focus overlay shows the
name and health.

**Done when:** an Npc spawns from a ZEN instance, idles, and can be damaged. — Done at the
runtime-data scope by developer decision. G2 supplies the script instances and F1.5 places
them in the world; no placeholder spawner remains.

### F1.5. World-bound script NPCs — DONE
Turn the G2 script instances into visible, interactable actors — the prerequisite for
routines, AI, and the G3 dialog camera.

- Apply `MDL_SetVisualBody` (body/armour/head/skin colour) to each inserted NPC and build its
  composed `DrModel`; skip the body only when the script explicitly hides it.
- Create one scene node per NPC at its spawn waypoint (fall back to the reference's dead
  point when the waypoint is missing), using the waypoint direction as facing.
- Give the NPC its dynamic collision capsule, register it with focus/interaction, and make E
  start the G3 dialog.
- Add the dialog camera: with both actor positions available, use the reference's two-shot
  (midpoint, actor distance, speaker-side spin) and restore the exploration camera on close.
- Play `DialogLine.Voice` (`<output-unit>.wav`) through the audio layer. If J1 has not landed
  yet, add the minimal `SoundEffect` playback here and let J1 generalize it.

**Depends on:** G2 (script instances) and G3 (dialog engine/UI).
**Done when:** a script-inserted NPC is visible at its waypoint, can be focused and talked to,
a conversation ends with the camera returning to the player, and the spoken line's voice file
plays (or is explicitly deferred to J1).

**Done at the human-body scope:** 356 human NPCs are placed from their waynet spawn points,
composed from armour/body + `HUMANS` + `.MMB` head, with per-instance `GothicModel.Materials`
texture variants, kinematic capsules and the E-to-talk focus path. A Daron conversation was
completed with the two-shot camera and `DialogLine.Voice`; City Guard `-talk` still passes.
The 50 non-human bodies are tracked in F1.6. The 172 NPCs at `NW_CITY_ENTRANCE_01` share the
five nearby waypoints; F2 now spreads them to their active routine points instead.

### F1.6. Non-human NPC bodies — DONE
Give the monster `WLD_INSERTNPC` instances their model-script skeleton and animation set.
**Depends on F1.5.**
**Done when:** a monster NPC (wolf, rat, troll, …) is visible at its spawn point and plays
its idle animation; building all monster models must stay lazy or otherwise bounded because
the full animation sets are large.

**Implemented:** `MDL_SetVisual`'s model script is resolved through the animation index, the
body composes with the script's `.MDH`/`.MDL`, and the idle clip is `S_FISTRUN`. Monster
bodies build lazily within 3000 units of the player, one per frame; startup creates 356 human
bodies and 50 pending monsters. A sheep was verified visible and idling at `NW_FARM2_OUT_02`.
One shipped model (`Snorting Dragon Snapper`) still fails with `std::bad_alloc` and is marked
failed. **Done:** monster bodies derive their skeleton from the model script, build lazily
within 3000 units, and play the `S_FISTRUN` idle. A sheep was verified at
`NW_FARM2_OUT_02`.

### F2. Way matrix & routines — DONE
Parse waypoints/waymatrix; daily routines (`TA`, `RTN`) move NPCs between points.
**Depends on F1.5/F1.6:** routines need placed human and monster NPCs.
**Done when:** an NPC follows a simple authored routine.

**Implemented:** `daily_routine` runs after the NPC binding exists; `TA_MIN` fills
`NpcVob.Routines`. `GameClock` (`-time:`, `-timescale:`) selects the active range, NPCs spawn
at their routine point, and a routine change walks the actor to the new waypoint at 300
units/s with its run clip, updating vob bounds, grid cell and kinematic capsule. Waynet points
are authored ~50 units above the floor, so `WorldGrid.TryGetGround` drops each NPC onto the
terrain surface at spawn and at every path target (ported from the reference's
`fixNpcPosition` ray-cast). Matteo was verified moving from `NW_CITY_MATTEO` to
`NW_CITY_BED_MATTEO` at 22:00. Way points and the pathfinder are implemented and tested, but
the installed ZenKitCS native DLL lacks the `ZkWayNet_getEdges` export, so movement currently
falls back to direct targets; N1's managed ZenKit port removes this.

### F3. AI states & perception
Idle/guard/follow/attack states, perception events (see/hear/damage), and AI queue.
**Depends on F2:** routines run on placed NPCs.
**Done when:** an NPC notices the player and transitions state plausibly.

Large enough for several sessions; split below. The reference is the guide: AI states are
Daedalus functions (`ZS_*`), the engine stores each state's ini/loop/end trio and calls them
with `SELF`/`OTHER`/`VICTIM`, and scripts drive movement through an AI queue. The routine's
stored `action` (`ZS_STAND_WP`, `ZS_GOTOBED`, `ZS_SIT_BENCH`, …) is the first state to run.

#### F3.1 State container, `AI_StartState`/`NPC_IsInState`, routine actions — DONE
- `ScriptRuntime.InvokeState(npc, other, victim, symbol)` sets the three globals, calls the
  function and returns its int, like the reference's `invokeState`.
- `NpcAiState` per actor: `funcIni`/`funcLoop`/`funcEnd`, start time, next loop time,
  started flag. Starting a state ends the previous one (end callback), then runs ini; the
  loop runs on the perception interval; clearing runs end.
- Externals `AI_StartState(fn, stateName)`, `AI_EndState`, `Npc_IsInState`.
- The routine's `action` starts when the NPC arrives and ends when the routine changes.
**Done when:** Matteo plays `ZS_GOTOBED` at 22:00 with ini/loop/end visible in the log, and
the state ends when his routine changes again.

**Implemented:** `ScriptRuntime.TryGetStateFunctions` resolves the entry plus `<name>_Loop`
and `<name>_End` by name (the reference's `AiState`); `InvokeState` sets
`SELF`/`OTHER`/`VICTIM` and returns the loop result. `NpcAiSystem` runs the entry once, the
loop every 0.5 s of real time and the end on replacement, and only for actors within 3000 units
of the player. `AI_StartState`/`NPC_IsInState` are wired; starts are queued because they arrive
from inside another state and the VM must not be re-entered. `AI_EndState` is not a shipped
symbol and is not registered. The routine action starts after arrival (F3.2 will let the state
walk) and ends on a routine change. Verified: Matteo runs `ZS_STAND_ARMSCROSSED`, chains
`ZS_GOTOBED` -> `ZS_SLEEP` at the bed, and returns to `ZS_STAND_ARMSCROSSED` at 08:00.

Done at the container scope: states run, chain and end on schedule. Their bodies still only
stand because the actions they call (`AI_GotoWP`, `AI_UseMob`, `Mdl_PlayAni`, ...) are F3.2.

#### F3.2 AI queue: movement and idle actions
Port the queue actions the shipped scripts actually call: `AI_GotoWP`, `AI_GotoNpc`,
`AI_GotoPos`, `AI_Wait`, `AI_Standup`, `AI_TurnToNpc`, `AI_TurnToSound`, `AI_PlayAni`,
`AI_RemoveWeapon`/`AI_EquipWeapon` where trivial, plus the queue result the state loop reads
(e.g. `AI_Wait` blocking until its timer expires).
Walking reuses the F2 walker (direct targets until N1 exposes waynet edges).
**Depends on F3.1.**
**Done when:** a `ZS_*` state walks to a waypoint, waits there and returns control without
unimplemented-external errors.

**Carry-over from F3.1:**
- A state that ends on its own (e.g. `ZS_SLEEP_LOOP` returning `LOOP_END` at 06:00) leaves
  the NPC stateless until the next routine change. The reference restarts the routine callback
  whenever no state is active (`Npc::tickRoutine`); do the same once the queue can drive the
  walk.
- Add a script-call trace switch (e.g. `-aitrace`) so the exact external calls of `ZS_SIT_*`,
  `ZS_GOTOBED`, `ZS_SLEEP` and friends can be recorded from the shipped scripts; the action
  set above is a guess until then.

#### F3.3 Perception
- `senses_range` from the instance, view cone, head/eye ray with occlusion against the
  collision world; hearing radius for noise; damage events.
- Per-type sampling on the reference's perception interval, not every frame.
- Call the NPC's perception function with the player as `OTHER`/victim; when the script has
  none, log and turn to face the player as an engine fallback.
**Depends on F3.1/F3.2.**
**Done when:** walking into a guard's view makes him react, and approaching from behind out
of the cone does not.

#### F3.4 Process policies & update budget
- `NpcProcessPolicy` (AiNormal/AiFar/AiFar2) selected by distance to the player, like the
  reference, so 406 NPCs do not all run perception and state loops each frame.
- Clamp the loop/perception intervals and skip far NPCs entirely.
**Depends on F3.1–F3.3.**
**Done when:** frame time stays flat with all NPCs spawned and only nearby ones tick.

#### F3.5 Movement parity and grounding follow-up
Carry-over from F2; the state machine should drive this, not the other way round.
- Translate the capsule (`tryTranslate`-style) instead of snapping the transform: slide along
  walls, honour step height, follow the ground every step instead of interpolating Y between
  waypoints.
- Full `fixNpcPosition` parity: stuck-capsule spiral and `distBias`, using physics rays so vob
  floors are valid ground everywhere (replaces the terrain-only 60-unit heuristic).
- Re-snap after climbs and teleports.
**Depends on F3.2.**
**Done when:** NPCs walk around obstacles and stand on house floors anywhere on the map.

Order: F3.1 → F3.2 → F3.3 → F3.4 → F3.5.

---

## Milestone G — Daedalus scripting (game logic)

### G1. Daedalus VM — DONE
Integrate ZenKit `DaedalusScript`/`DaedalusVm`; load `Gothic.dat`/`OU`, register
core external functions needed to boot.
**Done when:** startup scripts run without unimplemented-call crashes.

`ScriptRuntime` loads `_work/Data/Scripts/_compiled/GOTHIC.DAT` (74,649 symbols in the tested
install) and `_work/Data/Scripts/CONTENT/CUTSCENE/Ou.dat` (20,826 cutscene blocks). It boots
in the reference's order — `startup_global`, `startup_<world>`, `init_global`, `init_<world>` —
and keeps the VM and cutscene library alive for G2/G3. An unimplemented external is logged
once per name and receives the VM default, so startup cannot abort on an unbound call.
`-noscripts` skips the whole load for diagnostics.

The newworld boot completes in ~0.013 s; 5,228 calls reach the default handler across ten
routines (`GAME_INITGERMAN`, `WLD_INSERTITEM`, `WLD_INSERTNPC`, `HLP_GETNPC`, `NPC_ISDEAD`,
`HLP_ISVALIDNPC`, `PLAYVIDEO`, `WLD_SETGUILDATTITUDE`, `WLD_SETMOBROUTINE`,
`WLD_ASSIGNROOMTOGUILD`). Those stubs mean no NPCs/items are inserted and no video/attitude/
routine data is applied yet; G2 supplies the real bindings.

**Done when:** startup scripts run without unimplemented-call crashes. — Done; all four entry
points return, the VM remains usable, build/tests pass (191 tests). Details in
[Specifications/daedalus.md](Documentation/Specifications/daedalus.md).

### G2. Script classes & events — DONE
Expose NPC/item/world script objects and the event pipeline the scripts expect.
**Done when:** a scripted NPC interaction fires its Daedalus side effects.

`WLD_INSERTNPC` and `WLD_INSERTITEM` now initialize real script instances; the tested NotR
startup creates **406 NPCs** and **55 items**. `HLP_GETNPC` resolves an inserted symbol to its
`NpcInstance`, and `PC_HERO` is initialized and bound as `GlobalHero`. Each script binding
stores only the instance, a detached `NpcVob` and the spawnpoint; name, guild, level, HP and
damage/message state live on the `NpcVob`. `LOG_ADDENTRY` stores journal lines.

`-talk` calls the real `B_ASSESSTALK` routine for the first inserted NPC and then
`B_LOGENTRY`; the resulting `LOG_ADDENTRY` side effect is visible in the log:

```
Script NPC interaction: talked to 'City Guard'; journal 'You speak with City Guard.'.
```

The objects are data-only: no world nodes, bodies, collision or AI queue yet. Visible
script-spawned NPCs need the visual/AI layer; item/world bindings beyond instance storage are
also still open. Build/tests pass (192 tests). Details in
[Specifications/daedalus.md](Documentation/Specifications/daedalus.md).

**Done when:** a scripted NPC interaction fires its Daedalus side effects. — Done at the
script-object/event scope via `-talk`; the journal entry is the observable script side effect.

### G3. Dialogs — DONE
Dialog choice tree, NPC/player lines, voice hooks, and dialog camera.
**Done when:** a full conversation with a named NPC can be completed.

The data/UI layer is implemented: `C_INFO` grouping, choice/condition filtering,
`AI_Output` text from `Ou.dat`, `Info_AddChoice`/`Ai_StopProcessInfos`, and the Myra choice
panel driven by `-talk`/`-talkui`. A full conversation completes. The dialog camera and
voice playback are tracked in **F1.5**, which supplies the world-bound second actor and the
audio hook.

**Done when:** a full conversation with a named NPC can be completed. — Done at the
data/UI scope: City Guard, Pablo and Vatras conversations complete through the script
engine; `-talk` and `-talkui` exercise it. Camera/voice are F1.5/J1.

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
Sound-definition driven SFX: footsteps, hits, ambience, dialog voices (`DialogLine.Voice`).
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
**Known gap:** no transparent pass yet. `Blend`/`Add`/`Multiply` alpha functions and the
water group are drawn opaque (cut-out foliage already uses `AlphaTestEffect`); water,
windows and torch glows need a sorted transparent pass.
### M3. UI parity
Myra theme/layout matched to the original interface.
### M4. Performance
Draw batching, instancing, culling, and frame pacing.

---

## Milestone N — Managed ZenKit port

Replace the native **ZenKitCS** bindings with a C# port of ZenKit, using the available
ZenKit source as the format/behavior reference. The native library is MIT, so this is a
provenance-safe port, not a reverse-engineering step; never inspect the original game
executables for it.

**Why:** native handles/finalizers, `std::bad_alloc`, and recoverable Daedalus VM errors
(`pop_reference`/`SELF`) are hard to diagnose and fix from C#. A managed port removes the
whole class of memory-lifetime failures and lets the readers share the game's data model
directly. The failed `Snorting Dragon Snapper` model load is the current visible symptom.

**Scheduling:** after J in practice. It can start earlier in parallel because it is asset/VM
infrastructure rather than gameplay.

### N1. Managed file readers
Port the VDF/record index, ZEN/world, model (MDL/MDM/MDH/MDS/MRM/MMB), texture, DAT,
output-unit/cutscene and related binary readers.
**Done when:** the client loads the shipped data through managed readers with the same test
corpus and the native ZenKitCS package is gone.

### N2. Managed Daedalus VM
Port the Daedalus script/VM execution layer, including the external-call ABI and instance
storage, and remove the native handle/finalizer workarounds.
**Done when:** startup, script objects and dialogs pass from the managed VM, and no native
ZenKit symbols remain in the process.
