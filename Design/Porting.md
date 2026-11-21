# Porting

OpenGothicSharp is a C# port of [OpenGothic](https://github.com/Try/OpenGothic) (C++). This
document defines how the reference is used.

The local C++ checkout lives at `D:\Projects\chaos\OpenGothic` (override if it moves). The engine
lives in its `common/` directory; paths cited in TASKS.md and the design docs
(e.g. `world/objects/npc.cpp`) are relative to that `common/` folder. `game/main.cpp` in the
checkout root is a three-line entry point.

## Rules

1. **No line-by-line translation.** The C++ code is a behavioural reference. Read it to learn how
   Gothic works, then write idiomatic C# that fits this codebase.
2. **Verify behaviour, not implementation.** When a format or rule is ambiguous, the reference is
   usually right; reproduce the *result*, not the code shape.
3. **Do not copy.** No copied files, no translated comments, no copied constants without
   understanding them. If a constant is a magic Gothic number, document its origin.
4. **The reference is not gospel.** It has known bugs and workarounds; don't inherit them blindly.
5. **ZenKit is the format layer.** The reference uses ZenKit too; prefer its types over writing
   our own binary parsers.

## Mapping guide

| Reference (`common/...`) | Ours |
|---|---|
| `resources.cpp` | `Assets*.cs` |
| `world/world.cpp`, `world/worldobjects.cpp` | `World/` + vob classes |
| `world/objects/*.cpp` | `World/Vobs/*.cs` |
| `graphics/mesh/*`, `graphics/*` | `Engine/` |
| `physics/*` | `Physics/` (BepuPhysics2) |
| `game/gamescript.cpp` | `Game/Script/` (Daedalus via ZenKit) |
| `game/gamesession.cpp` | `Game/GameSession.cs` |
| `game/movealgo.cpp` | `Game/Movement/` |
| `ui/*` | `UI/` (Myra, not Tempest) |

Subfolders may be introduced inside these as a subsystem grows; the top-level folders are
defined in [Architecture.md](Architecture.md).

## Divergences

These are deliberate, not oversights:

- **Physics:** BepuPhysics2 instead of Bullet3.
- **UI:** Myra instead of Tempest widgets.
- **Rendering:** stock MonoGame effects until gameplay is done.
- **Rendering perf:** frustum culling now; instancing and occlusion culling later.
- **Order of work:** make things *visible*, then *active*, then *pretty*.
