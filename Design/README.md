# Design

Architecture and policy notes for OpenGothicSharp. The [README](../README.md) stays short on
purpose; anything that explains *how* or *why* lives here.

| Document | Contents |
|---|---|
| [Architecture.md](Architecture.md) | Layering, project layout, conventions |
| [Assets.md](Assets.md) | VDFS, textures, models, animation, world parsing |
| [Rendering.md](Rendering.md) | Rendering policy, stock effects, culling, future effects |
| [Physics.md](Physics.md) | BepuPhysics2 plan and how it replaces the current collision |
| [UI.md](UI.md) | Myra-based UI policy |
| [Testing.md](Testing.md) | xUnit layout and what must be covered |
| [CI.md](CI.md) | GitHub Actions workflow, platforms, caching |
| [Porting.md](Porting.md) | How the C++ OpenGothic reference project is used |

## Reference project

The C++ [OpenGothic](https://github.com/Try/OpenGothic) checkout is the **behavioural reference**,
not a source of code to translate line by line. Read it to answer "how does Gothic actually do
this?", then design the C# version. See [Porting.md](Porting.md).

The local checkout used during development is `D:\Projects\chaos\OpenGothic`. Paths in TASKS.md
and the design docs (e.g. `world/objects/npc.cpp`) are relative to its `common/` folder.
