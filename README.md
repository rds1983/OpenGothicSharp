# OpenGothicSharp

An open-source, from-scratch reimplementation of the **Gothic II** engine in C# on top of
[MonoGame](https://www.monogame.net/).

The goal is a clean, portable, moddable engine that can load and (eventually) play the original
Gothic II data files on modern hardware and operating systems, without touching the original
executable.

> **Status: early work in progress.** This repository currently contains the **asset-loading and
> world-parsing layer** only. There is no renderer, no game loop and no playable build yet.
> Gothic II itself is *not* included and never will be — you must own a copy of the game to use
> its data.

---

## Table of contents

- [Current state](#current-state)
- [Requirements](#requirements)
- [Building](#building)
- [Using the library](#using-the-library)
- [How the asset pipeline works](#how-the-asset-pipeline-works)
- [Project layout](#project-layout)
- [Roadmap](#roadmap)
- [Legal notice](#legal-notice)
- [Contributing](#contributing)
- [License](#license)
- [Credits](#credits)

---

## Current state

Implemented so far:

### Virtual file system

- Discovers every `*.vdf` archive under `<Gothic>/Data` (recursively) and mounts it with
  [ZenKit](https://github.com/GothicKit/ZenKitCS)'s `Vfs`.
- Archives are mounted with `VfsOverwriteBehavior.Older`, so files from a later archive win over
  duplicates from earlier ones (mods and patches override base data, matching the original
  engine's lookup order).
- Flattens the archive tree into a name → record index and exposes the sorted key list through
  `Assets.Keys`.
- Loaded objects are cached by name, so repeated requests for the same texture/model/world hit
  the cache instead of re-parsing.

### Textures

- Decodes ZenKit `Texture` objects and uploads all mip levels to a MonoGame `Texture2D`.
- Supports the `Dxt1` and `Dxt3` formats used by Gothic II.
- Automatically falls back to the compressed `*-C.TEX` variant when the requested name is missing.

### Models and animation

- Loads `.MDL` model scripts and resolves their companions: `.MDH` (skeleton hierarchy) and
  `.MDM` (mesh).
- Builds a `DrModel` bone hierarchy from the MDH, including root translation.
- Converts static and skinned geometry:
  - static meshes become `VertexPositionNormalTexture` parts;
  - skinned meshes become a custom `VertexSkinned` format (position, normal, UV, `Byte4` bone
    indices, `Vector4` blend weights, up to 4 influences per vertex).
- Re-normalizes blend weights, applies inverse bind matrices and creates `DrSkin`/`DrSkinJoint`
  data so meshes deform with their skeleton.
- Remaps bone indices per mesh through the hierarchy.
- Imports every `.MAN` animation referenced by the model script into named `AnimationClip`s,
  with keyframes sampled at the animation's FPS.
- Handles the "left-handed → right-handed" conversion: index winding is flipped and texture X
  coordinates are mirrored (`1 - u`) when mesh parts are created.

### World

- Parses `.ZEN` worlds with ZenKit's `World`.
- Splits the world mesh into a fixed **4 × 4 `WorldGrid`** of axis-aligned cells.
- Groups polygons per cell by material (deduplicating materials by texture), triangulates
  arbitrary n-gons with a fan, and deduplicates vertices per feature/position pair.
- Skips ghost occluders and materials without a texture.
- Loads `.MRM` multi-resolution meshes referenced by `VisualMultiResolutionMesh` virtual objects.
- Walks the virtual-object tree (`RootObjects` → `Children`), composing each object's rotation
  and position into a local-to-world transform.
- Assigns each virtual object to the cell its transformed bounding box overlaps.
- Batches repeated meshes into `InstancedMeshNode`s per cell; a mesh that appears only once is
  emitted as a plain `MeshNode` with a baked transform.
- Marks water materials as non-shadow-casting.

### Engine primitives

- `SceneNode`, `MeshNode`, `InstancedMeshNode` and `Material` form the minimal scene-graph shape
  the loader writes into (`WorldGridCell.Children`).
- Global `OG` service holding the `GraphicsDevice`, logging delegates and assembly version.
- Shared constants (`Constants.GridSize`, view/shadow distance, fog range) and a small
  math/`XNA` utility layer for bounding boxes, coordinate conversion and buffer creation.

**Not implemented yet:** rendering, camera/controls, UI, audio, physics/collision, the Daedalus
script VM, NPC/AI, inventory and combat systems, save/load, and a game executable.

---

## Requirements

| Component | Version / notes |
|---|---|
| .NET SDK | 8.0 (`net8.0`) |
| MonoGame | 3.8.6.1 (`MonoGame.Framework.DesktopGL`) |
| ZenKit | 0.1.13 — Gothic data formats |
| DigitalRiseModel | 0.7.5 — model/animation runtime |
| Gothic II | A legitimate installation with `Data/*.vdf` (Gothic II Gold / `GOTHIC2` + `NOTR` recommended) |

All NuGet versions are pinned centrally in `Directory.Build.props`.

---

## Building

```bash
git clone <this-repo>
cd OpenGothicSharp

dotnet build OpenGothic.MonoGame.sln -c Release
```

Output goes to `OpenGothic.Common/bin/MonoGame/Release`.

The project targets `net8.0` and builds on Windows, Linux and macOS. On Linux/macOS make sure the
usual desktop OpenGL and audio dependencies required by MonoGame DesktopGL are installed.

---

## Running the world viewer

`OpenGothic.Console` mounts the game archives, loads `NEWWORLD.ZEN` and renders it with a
free-fly camera. Rendering uses stock MonoGame effects only (`BasicEffect`), with per-cell and
per-instance frustum culling over the `WorldGrid`; nothing is pre-baked or copied into the
project.

```bash
dotnet run --project OpenGothic.Console -- "C:\\GOG Games\\Gothic II"

# load a different world
dotnet run --project OpenGothic.Console -- "C:\\GOG Games\\Gothic II" ADDONWORLD.ZEN
```

| Input | Action |
|---|---|
| `W` / `A` / `S` / `D` | Move forward / left / back / right |
| `Space` / `Left Ctrl` | Move up / down |
| `Left Shift` | Fast movement |
| Mouse | Look around |
| Mouse wheel | Change movement speed |
| `Escape` | Quit |

The window title reports the live draw-call count, camera position and movement speed. Exit
codes: `0` success, `1` missing arguments, `2` not a Gothic II folder, `3` unhandled error,
`4` world could not be loaded.

> The viewer needs a working desktop OpenGL context, since it creates a MonoGame window. Alpha
> blending, water and animated objects are not handled yet — world geometry is drawn opaque.

---

## Using the library

Point `Assets` at the folder that contains the game's `Data` directory (the Gothic II install
root), then load resources by their in-archive names:

```csharp
using Microsoft.Xna.Framework.Graphics;
using OpenGothic;

// GraphicsDevice must be assigned once before any GPU resource is created.
OG.GraphicsDevice = graphicsDevice;

var assets = new Assets(@"C:\GOG Games\Gothic II");

// Textures (the "-C.TEX" fallback is applied automatically)
Texture2D texture = assets.GetTexture(OG.GraphicsDevice, "STONES.TEX");

// A full model with skeleton and animation clips
var model = assets.GetModel(OG.GraphicsDevice, "ORC.MDL");

// A static multi-resolution mesh, as used by world objects
var mesh = assets.GetMultiMesh(OG.GraphicsDevice, "DOOR.MRM");

// The whole world, pre-split into a 4x4 grid of cells
WorldGrid world = assets.GetWorld(OG.GraphicsDevice, "GOTHIC.ZEN");

foreach (var cell in world.Cells)
{
    foreach (var node in cell.Children)
    {
        // MeshNode or InstancedMeshNode, each with its Material attached
    }
}
```

Notes:

- Names are resolved case-insensitively against the archive index.
- Loading is lazy and cached; calling `Get*` twice returns the same object.
- Without a Gothic II installation there is nothing to load — `Assets` will find no archives and
  later lookups will throw.

---

## How the asset pipeline works

```
Gothic II/Data/*.vdf
        │  ZenKit.Vfs.MountDisk(Older)
        ▼
  name → RecordInfo index (Assets)
        │
        ├── GetTexture ──► ZenKit.Texture ──► Texture2D (all mip levels, DXT1/DXT3)
        │
        ├── GetModel ────► .MDL ──► .MDH skeleton + .MDM mesh
        │                            └── .MAN clips ──► AnimationClip[]
        │
        ├── GetMultiMesh ─► .MRM ──► static DrMesh
        │
        └── GetWorld ────► .ZEN
                             ├── polygons grouped by material
                             ├── triangulated and packed into 4×4 WorldGrid cells
                             └── virtual objects walked, transformed,
                                 instanced per cell (InstancedMeshNode)
```

Coordinates are converted from Gothic's left-handed system to MonoGame's right-handed system at
load time (`Z` is negated, matrices are transposed and conjugated with the L↔R matrix, triangle
winding is reversed and U is mirrored), so the rest of the engine can work in a standard
right-handed MonoGame world.

---

## Project layout

```
OpenGothic.MonoGame.sln
Directory.Build.props          Central package versions, runtime IDs and metadata
OpenGothic.Console/
  Program.cs                   Command line parsing and usage
  WorldViewer.cs               MonoGame host, world rendering and culling
  FlyCamera.cs                 Keyboard/mouse free-fly camera
OpenGothic.Common/
  OpenGothic.Common.MonoGame.csproj
  OG.cs                        Global GraphicsDevice, logging, version
  Constants.cs                 Grid size and view distance constants
  Assets.cs                    VDFS mounting, record index, cache, textures
  Assets.Model.cs              .MDL/.MDH/.MDM/.MAN → DrModel + animation clips
  Assets.World.cs              .ZEN → WorldGrid, .MRM virtual objects, instancing
  WorldGrid.cs                 WorldGrid / WorldGridCell container
  MaterialFactory.cs           DrMaterial → engine Material
  Engine/
    SceneNode.cs               Base scene node with a local transform
    MeshNode.cs                Single mesh + material
    InstancedMeshNode.cs       Mesh + material + list of instance transforms
    Material.cs                Diffuse color/texture, shadow flag
  Utility/
    Mathematics.cs             Epsilon compares, coordinate conversion, bounding boxes
    MeshBuilder.cs             Vertex/index builders → DrMeshPart (16/32-bit indices)
    XNA.cs                     Buffer creation and format conversion helpers
  Vertices/
    VertexSkinned.cs           Skinned vertex declaration (4 bone influences)
```

---

## Roadmap

Rough order of work, subject to change:

1. **Rendering** — forward renderer, camera, frustum culling via `WorldGrid`, textured world and
   models, skinned animation playback.
2. **Playable world** — first-person/free camera, collision against world polygons, spawn points.
3. **Content completeness** — lights, animated world objects, VOB types beyond meshes,
   particle effects, sky/fog/water.
4. **Gameplay** — Daedalus script VM (`DAT`/`OU`/`DAEDALUS.DAT`), NPCs, dialogue, inventory,
   combat, AI routines.
5. **Platform** — audio (sound + music), UI (fonts, menus, HUD), save/load, mod support,
   cross-platform builds.

See the issue tracker for the finer-grained task list.

---

## Legal notice

- This project contains **no game assets and no original Gothic code**. It is a clean-room
  reimplementation of the file formats and engine behaviour.
- You must own a legitimate copy of Gothic II to use it; the game data is read from your own
  installation at runtime and is never redistributed.
- *Gothic* and *Gothic II* are trademarks of their respective owners. This project is not
  affiliated with or endorsed by Piranha Bytes or THQ Nordic.

---

## Contributing

Contributions are welcome — bug reports, format-coverage fixes, renderer work and documentation
all help.

- Match the existing code style (tabs, `var`, file-scoped namespaces, one partial `Assets` file
  per domain).
- Keep engine code data-driven and free of hard-coded asset names.
- Describe which Gothic II data files you tested a change against, since format edge cases vary
  between the base game, *Night of the Raven* and mods.
- Open an issue before starting a large subsystem so the approach can be discussed.

---

## License

[MIT](LICENSE) © 2025 Roman Shapiro and contributors.

---

## Credits

- [MonoGame](http://www.monogame.net/) — cross-platform XNA-compatible framework.
- [FNA](https://github.com/FNA-XNA/FNA) — reference implementation of the XNA API.
- [ZenKitCS](https://github.com/GothicKit/ZenKitCS) — Gothic file format parsing.
- [DigitalRiseModel](https://github.com/rds1983/DigitalRiseModel) — model and animation runtime.
- The original [OpenGothic](https://github.com/Try/OpenGothic) project and the Gothic modding
  community for documenting the formats.
