# Testing

## Layout

`OpenGothic.Tests` is an xUnit project targeting `$(MonoGameTargetFramework)` and referencing
`OpenGothic.Common`. It also references `MonoGame.Framework.DesktopGL` directly: `OpenGothic.Common`
marks that package `PrivateAssets="All"`, so without the direct reference the test project would not
compile against MonoGame types.

Test packages are pinned once in `Directory.Build.props` (`XunitVersion`,
`XunitRunnerVisualStudioVersion`, `MicrosoftNETTestSdkVersion`) like every other dependency; do not
put versions in the test `.csproj`.

`SmokeTests` only proves the plumbing (project reference resolves, MonoGame types load, tests are
discovered). Real coverage belongs in per-subsystem files, e.g. `Utility/MathematicsTests.cs`.

## Rules

- Every subsystem gets tests as it lands. A task is not finished without them unless the task
  explicitly says otherwise.
- Tests must **not** need a Gothic installation or a GPU.

### The three layers, and what CI can actually prove

CI has no Gothic install and no display, so "the tests pass" only ever means "the logic is still
correct". Be honest about which layer a test covers:

| Layer | Examples | Testable in CI? |
|---|---|---|
| **Pure logic** | L↔R conversion, bounding boxes, cell assignment, animation sampling, inventory rules | Yes, needs nothing |
| **Loader internals** | material grouping, vob transform composition, mesh part assembly | Yes, against **fake ZenKit interfaces** |
| **Binary formats** | parsing a real `.ZEN` / `.MDM` from a `.vdf` | **No** — see below |

Layer 2 works because ZenKit hands back interfaces (`IMaterial`, `IMultiResolutionMesh`,
`IModel`, `ISoftSkinMesh`), so a test can implement a fake with a handful of polygons and run the
real loader code over it. Prefer this over reaching for game data.

**Layer 3 cannot be unit-tested here.** ZenKit is a read-only wrapper — it has no writer or builder
API — so these fixtures cannot be synthesised. A checked-in `.ZEN` would be game data, which this
repository does not ship. That layer is verified by **running the viewer against a real Gothic II
installation**, per [AGENTS.md](../AGENTS.md).

Do **not** add `[Fact(Skip = "...")]` for data-dependent tests: a skip is silent in CI *and*
locally, so the test never runs for anyone and the green tick lies. If something genuinely needs a
local install, verify it by hand and say so in the task summary instead.

- Prefer testing **pure** logic: coordinate conversion, matrix math, animation sampling, vob-tree
  walking, script VM calls, inventory rules.
- Graphics-dependent code is kept thin so logic underneath it is testable. Nothing may open a
  window, mount a VDFS archive or touch the GPU during a test run.

## What to cover first

| Area | Examples |
|---|---|
| Utility | L↔R conversion, bounding boxes, mesh builders |
| Animation | clip sampling, crossfade math, root-motion stripping |
| World | cell assignment, material grouping, vob transform composition |
| Scripts | Daedalus VM call wiring, instance initialization |
| Gameplay | inventory add/remove, damage calculation, schedules |

## Running

```bash
dotnet test OpenGothic.MonoGame.sln
```

CI runs the same command on Windows and Linux; see [CI.md](CI.md).
