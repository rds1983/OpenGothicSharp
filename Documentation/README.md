# Documentation

Long-form project documentation. `README.md` at the repo root stays brief; architecture,
subsystem notes, and findings live here.

OpenGothicSharp is a C# port of [OpenGothic](https://github.com/Try/OpenGothic). These notes
assume the C++ project as the primary source of truth for behavior; where this port diverges
from it, the divergence is called out with the reason.

## Layout

- [Specifications/](Specifications/README.md) — behavioral and file-format specifications.

## Subsystems

Each subsystem gets a short note describing what it is responsible for, its main types,
how it talks to other subsystems, and known limitations.

### Application host

`Program.Main` initializes logging, configures `AssetManagementBase` logging, and runs
`OgGame`.

`OgGame` (`OpenGothic/OgGame.cs`) is the `Microsoft.Xna.Framework.Game` subclass that owns
the window and `GraphicsDeviceManager`. On `Initialize` it publishes the device through
`OG.GraphicsDevice`, so subsystems can create GPU resources without threading a device
through every call. `Update`/`Draw` form the main loop; Escape or the gamepad Back button
exits. Window title carries `OG.Version`.

### Command line

`CommandLine` (`OpenGothic/CommandLine.cs`) parses startup arguments and exposes them via
`CommandLine.Instance`. Unknown arguments are logged as warnings. `Validate()` checks that
`-g` was given and that the folder contains a `Data` directory. `Main` logs the parsed
values, applies the debug switches with `CommandLine.Apply()`, and returns exit code 1 with
an actionable error if validation fails.

On/off switches accept a bare flag, which turns them on, or `-name:true|false`, which sets
the value explicitly. Values left unset stay `null` so `Apply()` does not overwrite a
default chosen elsewhere.

| Option | Effect |
| --- | --- |
| `-g <path>` | Gothic installation (required). |
| `-game:<modfile.ini>` | Mod manifest. |
| `-w <world.zen>` | Startup world. |
| `-nomenu` | Skip the main menu. |
| `-devmode` | Developer mode. |
| `-nocull` / `-cull:true\|false` | Per-part frustum culling; see "Frustum culling". |
| `-nomodels` / `-models:true\|false` | Instantiate vobs that carry a model. Off shows the level without model geometry, which separates a model bug from a world one. |
| `-noanims` / `-anims:true\|false` | Attach the animation clips a model's script lists. Off keeps the bind pose and reads no `.MAN` file, which separates an animation problem from a model or world one. |
| `-animdump:<script>` | Log a script's clip list — nodes, frames, fps, duration, next clip — for the shipped data. `-animclip:<clip>` adds the first frame of that clip's bone tracks. |
| `-play:<clip>` | Play a clip on the `-model:` model. Accepts an alias name. Without it the model holds its bind pose. |
| `-overlay:<clip>` | Play a clip on its script layer over `-play:`, so an upper-body gesture leaves the legs alone. |
| `-freeze` | Pin playback to `-animtime:` instead of advancing it, so a capture does not depend on how long the process has been running. |
| `-animtime:<seconds>` | Playback position used with `-freeze`. A looping clip wraps it; a one-shot clamps. |
| `-wireframe` / `-bounds` | Start with the F2 / F3 debug views already on. |
| `-cam:<x,y,z>` | Override the camera position. |
| `-lookat:<x,y,z>` | Point the camera at a world position (implies yaw and pitch). |
| `-model:<name>` | Spawn a model directly in front of the camera, with or without an extension. Reaches models no world vob uses. |
| `-modeldist:<n>` | How far ahead to place `-model` (default 400). |
| `-modeloffset:<x,y,z>` | Move the `-model` spawn in camera space (right, up, forward), so a capture can put it at the edge of the view. |
| `-attach:<point>:<model>` | Hang a model or `.MRM` mesh on a bone of the `-model` spawn, e.g. `-attach:BIP01 R HAND:ITMW_1H_MACHETE_01.MRM`. |
| `-screenshot:<file.png>` | Write the rendered frame to a PNG once it is ready, then exit. |
| `-screenshotframe:<n>` | Which frame to capture, counting from one (default 1). |

Captures go through `-screenshot:` rather than an OS-level screenshot: the hardware
surface never passes through the desktop compositor here, so an external capture of the
window comes back **black** even though the frame renders. On this machine the same was
true of `PrintWindow` and of the HUD; `GraphicsDevice.GetBackBufferData` is the only path
that returns the image. Frame 90–120 is a safe choice while the world is still loading.

The debugging trio — `-cam:`, `-lookat:` and `-model:` — is the quickest way to inspect a
single asset without walking there: the camera starts at the world origin, so the nearest
model vob in `newworld.zen` is over 300 units away and below eye level.

Options that are only for diagnosing a subsystem live in `DebugSettings`
(`OpenGothic/DebugSettings.cs`) as static properties rather than in `Settings`, which owns
the game's INI files. They are startup diagnostics, not user preferences. A switch that
changes what is on screen is also reflected in the `DebugOverlay`, so a screenshot says
which switches were active.

### Settings

`IniFile` (`OpenGothic/IniFile.cs`) is a small INI reader/writer: `;` comments,
`[SECTION]` headers, `name=value` pairs, case-insensitive section/key lookup, Latin-1
(Windows-1252) encoding, and `Save()` that rewrites the file.

`Settings` (`OpenGothic/Settings.cs`) layers four INI sources, most-specific first: the
user file (`Gothic.ini` in the working directory), the installation's
`system/Gothic.ini`, `system/SystemPack.ini`, then built-in defaults. It exposes typed
properties (volumes, video brightness/contrast/gamma, mouse sensitivity, camera FOV, FPS
limit, FPS counter) and generic `GetString/GetInt/GetFloat/Set` accessors. `Set` writes to
the user file; `Save()` flushes it. `Main` loads settings after command-line validation and
publishes them via `OG.Settings`.

The user `Gothic.ini` is written to the working directory at runtime and is gitignored.

### Logging

`Log` (`OpenGothic/Log.cs`) is the single logging entry point and exposes `Log.Info`,
`Log.Warning`, and `Log.Error`. `Log.Initialize()` runs before anything else and routes
`ZenKit.Logger` messages under a `zenkit:<name>` category. Each line is written to the
console (errors to stderr) and to `log.txt` in the working directory, timestamped and
level-tagged. `Log.Shutdown()` flushes and closes the file; it is called from a `finally`
in `Main` so even a fatal exception is recorded first. `log.txt` is gitignored.

`ZenKit` ships native libraries and injects a `RuntimeIdentifier` per platform, so the
project declares `RuntimeIdentifiers` for its supported platforms to keep restore and
build targets in sync.

## Testing

Unit tests live in `OpenGothic.Tests` (xunit). Run them with:

```bash
dotnet test OpenGothic.MonoGame.sln
```

Tests avoid touching the real game installation — they build temporary folders and INI
files instead. Anything requiring a GPU, a window, or game data is exercised by running
the app, not by unit tests.

## Running locally

`dotnet run --project OpenGothic/OpenGothic.MonoGame.csproj -- -g "<path-to-gothic>"` works
from any shell. For a one-click launch, create `OpenGothic/Properties/launchSettings.json`
with profiles that pass your path in `commandLineArgs`, e.g.:

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "OpenGothic.MonoGame": {
      "commandName": "Project",
      "commandLineArgs": "-g \"<path-to-gothic>\""
    },
    "OpenGothic.MonoGame.DevMode": {
      "commandName": "Project",
      "commandLineArgs": "-g \"<path-to-gothic>\" -devmode -nomenu"
    }
  }
}
```

That file is **gitignored** because it holds a machine-specific path. Then run
`dotnet run --project OpenGothic/OpenGothic.MonoGame.csproj --launch-profile OpenGothic.MonoGame`.

## Rendering

### Coordinate systems

There are two handedness regimes, and the boundary between them is **asset loading**:

| | Handedness | Used by |
| --- | --- | --- |
| Game data | **Left-handed** (LHS) | Gothic ZEN/MDL/DAT — VDF contents, on disk |
| Engine | **Right-handed** (RHS) | MonoGame, XNA math, everything after loading |

Gothic's files are left-handed; MonoGame is right-handed. Rather than poke at this on
every draw call, the conversion is done **once, at load time**, in
`Mathematics.ToXna` (`OpenGothic/Utility/Mathematics.cs`), which mirrors Z
(`v.X, v.Y, -v.Z`; quaternions and matrices get the same treatment). Everything that
leaves `Assets` is therefore already right-handed.

Consequences worth respecting:

- The camera, renderer, and physics only ever see RHS data. MonoGame's
  `CreateLookAt`/`CreatePerspectiveFieldOfView` apply with no extra flips.
- Do **not** add per-frame Z flips or handedness fixes in rendering code. If a conversion
  is missing, fix it where the asset is read, not downstream.
- Anything read from a new file format must go through the same conversion before it
  reaches engine code.

### Camera

`Camera` (`OpenGothic/Camera.cs`) holds position, yaw and pitch, vertical FOV, and near/far
planes, and exposes `Forward`/`Right`/`Up`, `View`, `Projection` and `ViewProjection`. Yaw 0
looks down **-Z**, with positive yaw turning toward +X and positive pitch looking up. The
pitch used for the view matrix is clamped internally by `CreateLookAt`'s up-vector, so
extreme pitch should be clamped by the caller (movement code, milestone D).

`OgGame` creates one camera, takes its FOV from `OG.Settings.CameraFov`, and sets its
viewport from the graphics device. Near/far come from `Constants.NearPlane`/`FarPlane`.

### Mesh renderer

Rendering is split three ways:

- **`Renderer`** (`OpenGothic/Renderer.cs`) is a low-level drawing service and owns no
  scene content. Public surface: `BeginFrame(camera)`, `Clear()`,
  `DrawMeshPart(DrMeshPart, Matrix, Matrix[]? boneTransforms, Material? customMaterial, bool cull)`,
  `DrawBoundingBox(BoundingBox, Color)`,
  `DrawMeshPartWireframe(DrMeshPart, Matrix, Color, Matrix[]? boneTransforms)`, `EndFrame()`.
  It owns the `BasicEffect`, the `SkinnedEffect`, device blend/depth/rasterizer state,
  vertex/index-buffer binding and the draw call. It also exposes per-frame stats —
  `DrawCalls`, `Triangles` — reset in `BeginFrame`.
  `BeginFrame` **saves** the device state and installs the renderer's own (opaque blend,
  default depth, back-face culling, `LinearWrap` sampler, full-viewport scissor);
  `EndFrame` **restores** what it saved. Everything in between can assume world defaults.
  `BeginFrame`/`EndFrame` must be properly nested: `_frameBegin` tracks the open frame, and
  `Clear`/`DrawMeshPart`/`EndFrame` throw without one. The frustum, both debug `Material`s
  and the box mesh are created once and reused, because these run thousands of times per
  frame.
- **`WorldRenderer`** owns the `WorldGrid` traversal and the cell-level culling early-out,
  and drives the frame: `BeginFrame` → `Clear` → sky → cells → `EndFrame`.
- **`Sky`** owns its own geometry, gradient texture and material, and submits its
  `DrMeshPart` like any other object. It passes `cull: false`, because the dome encloses
  the camera and a background must always draw.

**Materials live on the mesh part**, in `DrMeshPartTag(Scene.Material)`. `DrawMeshPart`
reads the tag and only uses `customMaterial` when explicitly passed — it is expected to be
null in normal rendering and exists for rare cases such as debug drawing. `Material`
carries `DiffuseColor`, `DiffuseTexture`, and the render state it needs
(`LightingEnabled`, `FogEnabled`, `DepthStencilState`, `RasterizerState`).

Materials without a texture fall back to a 1x1 white texture. Textures in compressed
formats (DXT1/3/5) are uploaded from the raw mip chain; palettized and uncompressed formats
are decoded to RGBA by ZenKit and uploaded as `SurfaceFormat.Color`. A texture that cannot
be found logs a warning and renders untextured rather than aborting the world load.

**Vertex data is used verbatim.** `MeshBuilder` swaps triangle indices (because mirroring Z
reverses winding) but never flips or mirrors U/V. An earlier U-mirror (`1 - u`) corrupted
every texture; OpenGothic also copies `texture.x`/`texture.y` unchanged.
The swap is skipped for non-triangle primitives: `CreateMeshPart` takes a `PrimitiveType`
and a line list has no winding to fix.

### Debug views

`DebugDraw` (`OpenGothic/DebugDraw.cs`) holds the toggles and colours for the two debug
views. It is **independent of the text overlay**: F1 shows/hides the `DebugOverlay` labels,
F2 toggles `Wireframe`, F3 toggles `Bounds`. Both default to off.

- **Wireframe** (`DrawMeshPartWireframe`) keeps the part's fog and depth state but replaces
  lighting and texture with `WireframeColor`, so edges stay readable. Sampling the level's
  textures turns the wireframe into noise. It needs `FillMode.WireFrame`, which no stock
  `RasterizerState` provides, so `Renderer` owns one. A skinned part still deforms with the
  bone transforms it is passed, so the edges show the posed geometry rather than the bind
  pose.
- **Bounds** (`DrawBoundingBox`) draws the twelve edges of a box using a shared unit-cube
  `LineList` mesh scaled and translated onto the box — one draw call per box, no rebuilds.
  Boxes are unlit, unfogged and `DepthStencilState.DepthRead`: they are occluded by
  geometry but do not write depth, so overlapping boxes do not mask each other.

Bounds draws **one box per cell and one per vob**, not one per mesh part. A vob becomes
several parts (one per material) and a level has thousands of vobs, so per-part boxes
roughly doubled the frame's draw calls and flooded the screen with lines.
`WorldGridCell.ObjectBounds` stores the vob boxes computed during world load. They are
tested against the frustum in `WorldRenderer` rather than relying on `DrawMeshPart`'s cull:
`DrawBoundingBox` deliberately bypasses culling, because it scales a shared unit cube and
the part's own box says nothing about where the result lands.

`MeshBuilder.CreateMeshPart` accepts a `PrimitiveType` and derives `primitiveCount` from
it, so line lists and triangle lists both go through the same path.

### Frustum culling

Culling happens at two levels:

1. **Cells** — `WorldRenderer` tests the 16 `WorldGridCell` boxes and skips everything
   inside a hidden cell. This is the cheap early-out that rejects most of the level.
2. **Mesh parts** — `DrawMeshPart` transforms `part.BoundingBox` by the caller's transform
   and tests it against the frustum installed by `BeginFrame`. Because the test lives in
   `Renderer`, it covers `InstancedMeshNode` instances individually: previously an instance
   was drawn whenever its *cell* was visible, even if it stood far outside the view.

Consequences worth knowing:

- `DrawMeshPart`, `Clear` and `EndFrame` **throw** `InvalidOperationException` if no frame is
  open, and `BeginFrame` throws if one already is. A draw outside a frame would use a stale
  camera and a stale frustum, so it is a caller bug rather than something to tolerate
  silently. A single `_frameBegin` flag tracks this; `BeginFrame` sets it, `EndFrame`
  clears it.
- `DrawBoundingBox` and the sky opt out with `cull: false`. Debug boxes are already
  frustum-tested by the caller, and the sky dome must always draw.
- `part.BoundingBox` is the box of the pose the mesh was built in. For a static part that is
  exact. For a skinned part it is the **bind pose**, which is also exactly the box the
  vertices form as loaded, so it is a valid cull test for a model that is not animated.
  Animation moves vertices outside it, so when C2 plays clips it must grow each skinned
  part's box to cover the clip it plays — otherwise a limb gets culled mid-swing. Culling is
  not skipped for skinned parts in the meantime; the box is correct for what is drawn today.
- `DebugSettings.CullEnabled` (default true) turns the per-part test off globally. Pass
  `-nocull` on the command line to see whether a missing object is a culling bug or a
  loading bug: it restores the unculled counts, and the overlay shows "(no cull)". Cell-level
  culling is unaffected.

`Mathematics.Transform(BoundingBox, ref Matrix)` uses the centre/extent form rather than
transforming eight corners: for an affine matrix the results are identical, but it avoids
materialising the corners. This runs once per part per frame, so it matters.

Measured effect on a full `newworld.zen` frame at the default viewpoint: **11,937 → 2,005
draw calls** and **1.39M → 386k triangles**. An A/B capture with culling forced off
restored the original counts and produced a **pixel-identical image** outside the HUD,
confirming the discards were all invisible geometry.

With model vobs drawing, the same frame is **2,136 draw calls / 393,076 triangles**; with
`-nomodels` it returns to **2,005 / 385,934**, so the model path adds roughly 6% to the
frame. Forcing culling off (`-nocull`) gives **13,471 / 1,501,566** and a **pixel-identical
image outside the HUD**, confirming every discard was invisible geometry — skinned parts
included.

### Models

A model is a `DrModel` (skeleton plus meshes) placed in the world by a `ModelNode`
(`OpenGothic/Scene/ModelNode.cs`), which holds a `DrModelInstance`, the models attached to its
bones, and a world transform in `Transform`. A model driven by a script gets an
`AnimationData` alongside its instance, which owns the `AnimationController` and the script's
playback state; a prop leaves it null. `WorldRenderer.DrawModel` walks `instance.Model.Meshes`
and dispatches per part:

- **Static part** — drawn with the owning bone's global transform times the node
  transform, exactly as a hand-authored prop expects.
- **Skinned part** — has a `Skin`, so `DrawMeshPart` deforms it with
  `instance.GetSkinTransforms(...)` before placing it with the node transform. MonoGame's
  `SkinnedEffect` takes up to `SkinnedEffect.MaxBones` (72) transforms; a mesh here tops
  out at 36 joints and a skeleton at 47 bones, so no model needs a custom effect. The draw
  path throws if a caller hands it more bones than the effect supports, and throws if a
  skinned part is drawn without its bone transforms.

Culling is per part, and a skinned part's own box describes the pose it was built in. Playing
a clip moves vertices outside that box, so the playing node widens
`DrMeshPartTag.CullBounds`: every frame of the clip is evaluated once on a throw-away
`DrModelInstance`, and each part's box is unioned with the part box transformed by every one
of its skin joints. A blended vertex lies inside the hull of its joints' rigid copies, so the
union is conservative, and `Renderer.DrawMeshPart` culls with it.

`ModelNode.BonesAttachments` holds models drawn on the node's own bones. Each is placed as
`attachment.Transform * boneGlobal * node.WorldTransform` when the model is drawn, so it
follows the skeleton without becoming a scene node and without touching the node transform
cache. A multi-resolution mesh — the form weapons ship in — is wrapped into a one-bone model
by `ModelBoneAttachment.WrapMesh`, so both forms take the same path. `-attach:<point>:<model>`
exercises this from the command line. Gothic names attach points `ZS_RIGHTHAND`, `ZS_SWORD`
and so on, but a bare armour model carries none of them — only the script skeleton does — so a
name the model lacks is reported and the attachment is skipped. `attachment.Transform` is
where an item's own grip offset belongs; the item definitions that carry it arrive with the
item work, so today a weapon is placed at the bone origin with no offset.

The union lives on the part tag because it belongs to the model rather than to a node: it only
ever widens, so every instance of a shared model sees a box valid for any clip any of them
plays, and a cross-fade needs no special case. `DrModelTag.MeasuredClips` records which clips
have already been merged, so each clip is measured once per model, not once per instance and
certainly not per frame. It cannot go on `AnimationClipTag`: a clip is shared by every model
with the same script and bone layout — the ~40 armour models share one set — while their
meshes, and therefore their part boxes, differ.

`Assets.GetModel(name, ...)` resolves a model name from the level (the authoring `.ASC` /
`.MDS` name) to a file: `STEM.MDL` if present, otherwise the `STEM.MDH` + `STEM.MDM` pair.
The loader builds absolute bind transforms in hierarchy order, reuses the hierarchy node
indexing the weights use, and gives each skinned mesh a synthetic bone under a synthetic
root so that a mesh can span many bones without one bone owning two meshes. The details —
including why the root translation goes on the root bone's default pose rather than into the
bind matrices — are in
[Specifications/gothic-model-format.md](Specifications/gothic-model-format.md).

`Assets.World.cs` creates a `ModelNode` for every `VisualModel` vob and registers the
instance's world bounds in the owning grid cell so debug bounds and the existing cell culling
still apply. A vob's transform in the file is already in world space, so the loader derives the
node's local transform against its parent and records the parent link; `SceneNode.WorldTransform`
composes the chain and caches it until the node or an ancestor moves. That keeps every static
vob exactly where the file puts it (the 1,870 parented vobs of `newnorld.zen` recompose within
0.0125 units) while letting a parent that moves carry its children. `-nomodels` skips this so a
frame can be compared against the same world without model geometry. Details in
[Specifications/zen-vob-transform.md](Specifications/zen-vob-transform.md).

Model parts are **texture-driven the same way as static meshes**: the material comes from
`DrMeshPartTag`, with the white fallback when a texture is missing. Wireframe and bounds
both extend to models: `DrawMeshPartWireframe` still applies the skeleton, so the edges show
the posed geometry rather than the bind pose.

#### Animation clips

`Assets.Animation.cs` attaches the clips a model can play. A model file carries none: the
clips are listed by a *model script* (`.MSB`, Gothic 1 spells it `.MDS`) and stored one
`<SCRIPT>-<CLIP>.MAN` file per clip. A script reaches its models two ways — the skeleton
name it declares (`SHEEP` → `SHEEP_BODY.MDL`) and the `.ASC` meshes it can drive
(`HUMANS.MSB` lists `ARMOR_PRISONER.ASC`) — and `Assets` indexes both once.

The non-obvious part is that **a clip's channel indices address the script's skeleton, not
the model's hierarchy**. The human skeleton has 34 nodes; `ARMOR_PRISONER.MDL` has 25,
because it omits the nine `ZS_*` attach nodes. So channels are resolved by node *name* and
channels the model cannot pose are dropped — 1,710 of `HUMANS`' 26,656 channels for a bare
armour model, which is why `-animdump:` reports that count.

Converted sets are cached by script plus the exact bone-name sequence, so the ~40 armour
models that share one 25-bone skeleton share one set of clip objects instead of re-reading
`HUMANS`' 784 `.MAN` files each. Clips are populated into `DrModel.Animations`, and each clip
carries its script-side playback metadata (next clip, layer, blend in/out, direction) in
`AnimationClip.Tag` as an `AnimationClipTag`. The set object itself is only a load-time cache
and is not consulted at playback.

#### Animation playback

`Scene/AnimationData.cs` plays a set's clips on a `DrModelInstance` through
DigitalRiseModel's `AnimationController`: it owns the script's state machine and hands the
controller a node to sample, so a pose reaches the GPU through the same `GetSkinTransforms`
path a bind-pose model already used. `ModelNode.AnimationData` holds it only for a model that
plays something, and `OgGame.UpdateAnimations` advances every node that has one, which keeps
playback out of the draw path.

A clip's script-side parameters travel on `AnimationClip.Tag` as an `AnimationClipTag`; an
alias is a clip of its own that shares the target's channels and carries its own tag. The
node reads its clips from `Instance.Model.Animations`, so nothing else has to keep the set
alive at playback time.

#### Layers

The script gives every clip a `Layer`, and the shipped `HUMANS` data uses it: layer 1 is
full-body locomotion, layer 2 is upper-body combat overrides (`T_MOVE_2_FISTMOVE`, 14 bones),
9 is head-only (`C_LOOK_*`), 11 is an arm gesture (`C_POINT_*`), and 20 is full-body turns.
Attacks (`S_1HATTACK`, …) are layer 1 and full body, so they do reset the legs by design.

`AnimationData.PlayLayer(name)` runs a clip on top of the base at the layer its script gives
it. All active clips are layers of one `AnimationBlendNode`, ordered by that layer value, and
each layer's `BoneFilter` keeps only the bones no higher layer drives — so a higher layer wins
on exactly the bones it animates and everything else keeps the pose below. A per-layer
`TimeOffset` turns the controller's single clock into each clip's own time, which is what lets
a walk keep looping while a gesture starts mid-stride. A layered change snaps rather than
fading — the reference ramps a new layer in over its blend time, which is the one piece not
ported yet. `-overlay:<clip>` exercises it from the command line.

The playback rules come from the script, not from guesswork:

- **Looping.** A clip cycles when its `Next` names *itself*; naming another clip makes it a
  one-shot that hands over, and naming nothing makes it stop. One override: a clip carrying
  a `ComboWindow` event is always a one-shot, because the attack animations name themselves
  yet must stay interruptible at that window. 17 shipped clips are reclassified this way.
- **Rate and duration.** `FrameCount / Fps`, both from the `.MAN`. Its rate is authoritative
  because the file is the only source that also stores the frames (161 clips disagree with
  their script). The 149 clips storing a rate of 1 are single-frame poses.
- **Direction.** `AnimationDirection.Backward` mirrors the sample walk, so a backward clip
  starts on its last stored frame. This is not cosmetic: 197 clips and 542 aliases use it,
  and a backward walk is how the data spells "the same motion, reversed".
- **Aliases.** A name in the script's alias list plays another clip's samples but keeps its
  own next/layer/blend/direction, so `S_SNEAKBL` (backward) differs from `S_SNEAKL`
  (forward). 782 of the 1,847 aliases loop, 141 of those reversed; treating an alias as a
  plain rename would play those forward and one-shot.
- **Seams.** A looping clip gets one extra key repeating frame 0 at its duration, so the
  final frame interval interpolates back into the first instead of holding and snapping.
  That closes authored seams; it does not invent motion where the data has none.
- **Blending.** A clip change ramps over the longer of the outgoing clip's `BlendOut` and the
  incoming clip's `BlendIn`; it switches immediately when both are zero (917 of the 1,253
  declared transitions). The outgoing layer keeps playing through the ramp rather than being
  frozen at the switch, and a bone the incoming clip does not drive eases back to the bind
  pose instead of holding its old value.
- **Speed is ignored**, as the reference does: all 3,949 shipped clips store `speed == 0`,
  so honouring it would stop every animation.

Nothing is allocated per frame: the controller reuses its accumulation buffer, and the only
allocation happens when a clip starts and its node is built. `-play:` and
`-freeze`/`-animtime:` exist to inspect a clip without walking to an NPC. Details in
[Specifications/gothic-animation-format.md](Specifications/gothic-animation-format.md).

`AnimationController` does the sampling and blending; the node keeps only the state machine,
because that is the part the script owns and the library cannot know: `Next` chaining, aliases
with their own direction and blend times, and the `ComboWindow` one-shot rule. Two details
bridge the two conventions:

- **Reverse.** The library's `PlayBackwards` samples `duration - time`, which puts a reverse
  clip on frame 0. The game's reverse mirrors the sample walk and starts on the last stored
  frame, so the node wraps the clip in a one-layer blend node and adds one frame period of
  `AnimationBlendLayer.TimeOffset`. The shift stays on the layer rather than on the
  controller's clock, so `AnimationController.Time` remains game time — which is what
  `HasFinished` and the cross-fade progress are measured against.
- **Zero-length fades.** 917 of the 1,253 declared transitions ask for no ramp at all, so the
  node calls `StartClip` for those instead of `CrossfadeToClip`, whose progress would be
  `0/0` at the first evaluation.

### Sky, fog and lighting

`Sky` draws a gradient dome (8 rings × 16 segments) built from a 1×64 colour ramp between
`Constants.SkyHorizonColor` and `SkyZenithColor`. It is unlit, unfogged, depth-test
disabled and cull-none, and follows the camera. The reference's LUT/cloud sky is deferred
to the shader milestone.

Fog is `BasicEffect`'s linear fog using `Constants.FogStart`/`FogEnd`, with the fog colour
set to the sky horizon so distant geometry dissolves into the backdrop.

Lighting is a fixed two-directional-light rig with specular disabled and a raised ambient
term. Gothic's textures bake their own shading, so `EnableDefaultLighting()` produces
blown-out specular smears on flat surfaces.

For reference, a full `newworld.zen` frame is roughly **2k draw calls / 386k triangles** at
the default viewpoint after per-part frustum culling (it was ~12k / 1.4M before). There is
no batching or instancing yet; milestone M4 covers that.

Uses stock MonoGame effects (`BasicEffect`, `SkinnedEffect`, …) until gameplay is polished.
A custom shader system and visual parity are deliberately deferred; see
[TASKS.md](../TASKS.md) milestone M.

## UI

Built with Myra. Usability first; parity is deferred to milestone M.

All UI code and `.xmmp` layouts live in `OpenGothic/UI/`. Screens are authored as `.xmmp`
and exported to C# rather than written by hand:

```
cd OpenGothic/UI
myra-export DebugOverlay.xmmp
```

`myra-export` (a global .NET tool) writes two files per layout:
`<Name>.Generated.cs` holds `BuildUI()` and the widget fields and is regenerated on every
export; `<Name>.cs` holds the partial class and is written **once** — re-exporting does not
touch it, so user code belongs there. Never edit a `.Generated.cs` by hand.

`OgGame.Initialize` sets `MyraEnvironment.Game`, creates a single `Desktop`, and assigns the
root widget. `Draw` renders the world, updates the overlay from the frame's counters, then
calls `Desktop.Render()`.

Myra ships its default skin and `Inter-Regular.ttf` as embedded resources, so no UI assets
need to be copied into the project.

**Device state leaks across the UI/world boundary**, so the save/restore in
`BeginFrame`/`EndFrame` is load-bearing, not defensive. Myra enables scissor testing, leaves
its small rectangle set and binds a clamping sampler; `BasicEffect` sets neither. Without
the reset, `Clear` clips to the UI's rectangle and terrain textures — whose UVs run well
outside 0..1 — smear into long streaks.

### HUD

The HUD is three layouts in `OpenGothic/UI/`:

- **`Hud`** — the `Desktop` root. A stretched `Panel`, so each block anchors to its own
  corner: `DebugOverlay` top-left, `Controls` bottom-right, both inset 8px.
- **`Controls`** — always visible, bottom-right. Lists the current key bindings.
- **`DebugOverlay`** — toggleable, top-left. FPS, camera position, draw calls, triangles,
  and the active debug views.

Anchoring relies on `Widget.Left`/`Top` being **additive offsets applied after alignment**,
so a bottom-right block uses `Alignment = Right/Bottom` with *negative* offsets.
`Hud.xmmp` cannot express this: the exporter drops `Stretch` because it equals the property
default, and it does not emit per-child layout for a bare `Panel`. The alignment and offsets
therefore live in `Hud.cs`, not the `.xmmp`.

Both blocks are styled in their own `.xmmp`: a 65% black background (`#000000A6`), 10×6
padding and a gold `#FFCC66` header. **Myra's 8-digit hex is `RRGGBBAA`, not `AARRGGBB`** —
the alpha goes last. Writing `#A6000000` yields near-transparent red.

`UI/DebugOverlay.cs` computes FPS from `GameTime` over a half-second window and pulls the
render counters from `Renderer`, formatted with thousands separators.

`OgGame` edge-detects F1 (comparing against the previous frame's `KeyboardState`, so holding
the key toggles once) and calls `Hud.ToggleDebugOverlay`. While the debug block is hidden it
is not updated at all, so no label text is refreshed. F2 and F3 use the same edge-detection
and flip `DebugDraw.Wireframe` / `DebugDraw.Bounds`.

Bindings are hand-maintained: when one is added to `OgGame.Update`, add its line to
`Controls.xmmp` and re-export.
