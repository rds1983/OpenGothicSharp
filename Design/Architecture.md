# Architecture

## Layering

```
OpenGothic.Console     executable: window, camera, input, debug output
OpenGothic.Common      engine: assets, world, scene graph, physics, gameplay
OpenGothic.Tests       xUnit test project (planned, see T-001)
```

`OpenGothic.Common` is the whole engine. The console app is a *host*, not the engine; anything
that a future game executable would also need must live in `Common`.

Planned inside `OpenGothic.Common`:

| Folder | Responsibility |
|---|---|
| `Assets*` | VDFS mounting, resource index, loaders, cache |
| `Engine/` | scene nodes, materials, mesh parts (render-agnostic where possible) |
| `World/` | world container, cells, spatial index, vob tree |
| `World/Vobs/` | vob types (`Vob`, `StaticObj`, `Npc`, `Interactive`, triggers) |
| `Game/` | game session, time, scripts, AI, inventory, combat |
| `Physics/` | BepuPhysics2 integration |
| `UI/` | Myra widgets and screens |
| `Utility/` | math, buffers, misc |
| `Vertices/` | vertex declarations |

## Conventions

- .NET 8, file-scoped namespaces, tabs, `var`, one partial `Assets` file per domain.
- Engine code is data-driven: no hard-coded asset names, no hard-coded world names.
- Gothic coordinates are left-handed; the engine converts once at load time to MonoGame's
  right-handed convention. Everything downstream of a loader is right-handed.
- Prefer small, testable classes over god objects. The reference project's `World` and `Npc` are
  thousands of lines each; split by concern here.

## Coordinates

Gothic stores left-handed data. On load we negate `Z`, conjugate matrices with the L↔R matrix,
reverse triangle winding and mirror texture U (`1 - u`). This is already implemented in the
asset loaders and must stay in one place: `Utility/Mathematics.cs` and the loaders.
