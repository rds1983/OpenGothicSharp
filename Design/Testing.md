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
- Tests must **not** need a Gothic installation or a GPU. Loaders are tested against synthetic or
  checked-in minimal fixtures; anything that inherently needs game data is guarded by a
  `[Fact(Skip = "...")]` or an environment-variable check.
- Prefer testing **pure** logic: coordinate conversion, matrix math, animation sampling, vob-tree
  walking, script VM calls, inventory rules.
- Graphics-dependent code is kept thin so logic underneath it is testable.

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
