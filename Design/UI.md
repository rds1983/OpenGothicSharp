# UI

## Decision

**Myra** is the UI toolkit. `MyraVersion` is pinned in `Directory.Build.props` and referenced by
`OpenGothic.Common.MonoGame.csproj`.

## Policy

- UI parity is not required early. Build something **convenient** first; mimic Gothic's layout
  only during the parity phase.
- All UI lives in `OpenGothic.Common/UI` so any host can use it, with thin host glue in the
  console app.
- Widgets are data-driven: menu definitions, dialog choices and inventory grids come from game
  data, never from hard-coded layout files. Static HUD chrome is the exception.

## Authoring workflow

Layouts are authored as **MyraPad projects** (`.xmmp`) and exported to C# with `myra-export`:

```
myra-export OpenGothic.Common\UI\debugOverlay.xmmp
```

`myra-export` is installed as a global dotnet tool (currently 1.6.7). Export is a **manual step**,
not part of the build: the generated C# is committed so `dotnet build` needs no tooling. Re-run it
after editing an `.xmmp` and commit both files.

Each project produces two files:

| File | Contents | Regenerated |
|---|---|---|
| `<Class>.Generated.cs` | `partial class <Class> : <RootWidget>` with `BuildUI()` and the widget fields | always |
| `<Class>.cs` | user code: constructor + behaviour | **never** — `myra-export` skips it if it exists |

`<Project.ExportOptions>` controls the output:

```xml
<Project.ExportOptions Namespace="OpenGothic.UI" Class="DebugOverlay" FieldsVisibility="Private" />
```

`FieldsVisibility="Private"` keeps widget fields private, so the layout is only reachable through
the class's own methods. `OutputPath` defaults to the `.xmmp`'s directory; leave it out so the repo
stays portable.

Because the user file is never rewritten, all class behaviour goes into hand-written partials
(`DebugOverlay.cs`, `Controls.cs`). Give widgets an `Id="..."` in the `.xmmp` to get a private field
for them.

**Colours in `.xmmp` are `#RRGGBBAA`**, not `#AARRGGBB` — the alpha byte comes last.

## HUD

`UI/Hud.cs` owns the desktop and is the only UI type hosts talk to:

- `new Hud(game)` — sets `MyraEnvironment.Game`, builds the widgets. Requires `LoadContent` to have
  run, because widget styles resolve through the graphics device.
- `DebugOverlayVisible` / `ToggleDebugOverlay()` — show or hide the debug block.
- `ToggleDebugKey` — the binding listed by the controls block (`F1`), defined once in
  `Controls.ToggleDebugKey`.
- `Update(gameTime, drawCalls, polygons)` — refreshes the statistics and re-lays the widgets out.
- `Render()` — draws the desktop over the current back buffer. Call it after the 3D frame.

Hosts pass in the frame numbers rather than the HUD reaching into the renderer, so the HUD has no
dependency on rendering internals.

Two settings are worth knowing about:

- `MyraEnvironment.SetMouseCursorFromWidget` is turned off. The world viewer hides and recentres the
  mouse every frame; letting Myra swap the cursor would fight that.
- The FPS figure is averaged over 0.5 s so it stays readable instead of jittering every frame.

## Widgets

| Class | Position | Shows |
|---|---|---|
| `DebugOverlay` | top-left | FPS, draw calls, polygons for the current frame |
| `Controls` | bottom-right | the key bindings the host reacts to |

## Planned screens (rough order)

1. ~~Debug overlay: FPS, draw calls, polygons.~~ (done)
2. Main menu / world selection.
3. Dialogue menu.
4. Inventory.
5. Quest log, character sheet, map.
6. Console / script console.
