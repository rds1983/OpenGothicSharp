# OpenGothicSharp

A from-scratch **C# port of [OpenGothic](https://github.com/Try/OpenGothic)** — an open-source
re-implementation of the *Gothic II: Night of the Raven* engine — built on
[MonoGame](https://www.monogame.net/).

The engine reads the original Gothic II data files (`Data/*.vdf`) from your own installation.
No game assets and no original Gothic code ship with this repository — you must own the game.

> **Status: early.** A world viewer loads `NEWWORLD.ZEN`, draws the world and a hero model with
> stock MonoGame effects, and lets you walk around with gravity and simple wall collision.
> Nothing is playable yet. The console app is a thin host around the engine-side `GameCore`; see
> [Design/Architecture.md](Design/Architecture.md).

## Documentation

| Document | Contents |
|---|---|
| [Design/](Design/) | Architecture, subsystem designs and project policies |
| [TASKS.md](TASKS.md) | Ordered roadmap and the current work queue |
| [AGENTS.md](AGENTS.md) | How work is picked up, tested and committed |

## How this port works

The C++ [OpenGothic](https://github.com/Try/OpenGothic) project is the **behavioural reference**,
not a source to translate. We reimplement in C#, verifying against it when something is unclear.
See [Design/Porting.md](Design/Porting.md).

Development order: make entities **visible** → make them **active** → polish **parity**.

| Area | Decision |
|---|---|
| Rendering | Stock MonoGame effects for now; own effects much later |
| Performance | Frustum culling only, early; instancing/occlusion culling later |
| Physics | **BepuPhysics2** (replaces the current hand-rolled collision) |
| UI | **Myra** — convenient first, Gothic-accurate later |
| Tests | **xUnit** |

## Requirements

| Component | Notes |
|---|---|
| .NET SDK | 8.0 |
| MonoGame | 3.8.6.1 (`MonoGame.Framework.DesktopGL`) |
| ZenKit | 0.1.13 — Gothic data formats |
| DigitalRiseModel | 0.7.7 — model/animation runtime |
| Gothic II | A legitimate installation with `Data/*.vdf` (Gold / `GOTHIC2` + `NOTR` recommended) |

All NuGet versions are pinned in `Directory.Build.props`.

## Build

```bash
git clone <this-repo>
cd OpenGothicSharp
dotnet build OpenGothic.MonoGame.sln -c Release
```

CI builds and tests the solution on Windows and Linux; see [Design/CI.md](Design/CI.md).

## Run the world viewer

```bash
dotnet run --project OpenGothic.Console -- "C:\\GOG Games\\Gothic II"

# load a different world
dotnet run --project OpenGothic.Console -- "C:\\GOG Games\\Gothic II" ADDONWORLD.ZEN
```

| Input | Action |
|---|---|
| `W` / `A` / `S` / `D` | Run relative to the camera |
| `Left Shift` | Sprint |
| Mouse | Orbit the camera |
| Mouse wheel | Camera distance |
| `F1` | Toggle the debug overlay |
| `Escape` | Quit |

Loading, rendering, the HUD and the current limitations are documented in
[Design/Assets.md](Design/Assets.md), [Design/Rendering.md](Design/Rendering.md) and
[Design/UI.md](Design/UI.md).

## Legal

- No game assets and no original Gothic code are included. You must own Gothic II to use the data.
- *Gothic* and *Gothic II* are trademarks of their respective owners. This project is not
  affiliated with or endorsed by Piranha Bytes, THQ Nordic or the OpenGothic authors.

## License

[MIT](LICENSE) © 2025 Roman Shapiro and contributors.
