# Architecture

## Layering

```
OpenGothic.Console     executable: window, camera, input, debug output
OpenGothic.Common      engine: assets, world, scene graph, physics, gameplay
OpenGothic.Tests       xUnit test project (see Design/Testing.md)
```

`OpenGothic.Common` is the whole engine. The console app is a *host*, not the engine; anything
that a future game executable would also need must live in `Common`.

Planned inside `OpenGothic.Common`:

| Folder | Responsibility |
|---|---|
| `Assets*` | VDFS mounting, resource index, loaders, cache |
| `Engine/` | scene nodes, materials, mesh parts (render-agnostic where possible) |
| `Rendering/` | `ForwardRenderer`: effects and low-level 3D drawing |
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
- Gothic coordinates are left-handed (LHS) and MonoGame uses right-handed (RHS); the engine converts
  once at load time. See [Coordinates](#coordinates-gothic-lhs--monogame-rhs). Everything downstream
  of a loader is right-handed.
- Prefer small, testable classes over god objects. The reference project's `World` and `Npc` are
  thousands of lines each; split by concern here.

## Coordinates: Gothic LHS → MonoGame RHS

**Gothic data is left-handed (LHS). MonoGame is right-handed (RHS).** The engine converts once,
at load time, and everything downstream of a loader is right-handed.

The conversion is a fixed three-part recipe, applied by the loaders via
`Utility/Mathematics.cs` (`ToXna`) and `Utility/MeshBuilder.cs` (`CreateMeshPart`):

| Step | Rule | Why |
|---|---|---|
| Position | negate `Z` | flips the forward axis, turning LHS into RHS |
| Matrix | conjugate with the L↔R matrix (`LTR * m * LTR`) | same flip applied to rotations/translations |
| Winding | reverse triangle order (`0,1,2` → `2,1,0`) | negating `Z` mirrors geometry, which inverts the facing direction |

**UVs are used verbatim.** Negating `Z` already mirrors the model, and the winding reversal keeps
faces front-facing, so the texture lookup needs no correction. Do **not** additionally mirror U
(`1 - u`).

> This is a real bug we shipped and fixed: `MeshBuilder` used to apply `U = 1 - U` on top of the
> `Z` negation. Two mirrors cancel, so every triangle sampled the opposite side of its texture
> sheet — the hero's body smeared the skin atlas and it looked like broken UV mapping. If a model
> appears to have "wrong UVs", check this first.

These rules must stay in one place. Never apply a conversion twice, and never add a
per-loader or per-draw correction.
