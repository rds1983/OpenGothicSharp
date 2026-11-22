# Documentation

Long-form project documentation. `README.md` at the repo root stays brief; architecture,
subsystem notes, and findings live here.

## Layout

- [Specifications/](Specifications/README.md) — clean-room behavioral and file-format
  specifications.

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
`CommandLine.Instance`: `-g <path>` (Gothic installation), `-game:<modfile.ini>` (mod
manifest), `-w <world.zen>` (startup world), `-nomenu` (skip main menu), and `-devmode`.
Unknown arguments are logged as warnings. `Validate()` checks that `-g` was given and that
the folder contains a `Data` directory. `Main` logs the parsed values, and returns exit
code 1 with an actionable error if validation fails.

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
  `DrawMeshPart(DrMeshPart, Matrix, Material? customMaterial)`, `EndFrame()`. It owns the
  `BasicEffect`, device blend/depth/rasterizer state, vertex/index-buffer binding and the
  draw call. It also exposes per-frame stats — `DrawCalls`, `Triangles` — reset in
  `BeginFrame`.
  `BeginFrame` **saves** the device state and installs the renderer's own (opaque blend,
  default depth, back-face culling, `LinearWrap` sampler, full-viewport scissor);
  `EndFrame` **restores** what it saved. Everything in between can assume world defaults.
- **`WorldRenderer`** owns the `WorldGrid` traversal and frustum culling, and drives the
  frame: `BeginFrame` → `Clear` → sky → cells → `EndFrame`.
- **`Sky`** owns its own geometry, gradient texture and material, and submits its
  `DrMeshPart` like any other object.

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
every texture; the reference implementation also copies `texture.x`/`texture.y` unchanged.

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

For reference, a full `newworld.zen` frame is roughly **12k draw calls / 1.4M triangles** —
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
- **`DebugOverlay`** — toggleable, top-left. FPS, camera position, draw calls, triangles.

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
is not updated at all, so no label text is refreshed.

Bindings are hand-maintained: when one is added to `OgGame.Update`, add its line to
`Controls.xmmp` and re-export.
