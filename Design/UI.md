# UI

## Decision

**Myra** is the UI toolkit. `MyraVersion` is already pinned in `Directory.Build.props`.

## Policy

- UI parity is not required early. Build something **convenient** first; mimic Gothic's layout
  only during the parity phase.
- All UI lives in `OpenGothic.Common/UI` so any host can use it, with thin host glue in the
  console app.
- Widgets are data-driven: menu definitions, dialog choices and inventory grids come from game
  data, never from hard-coded layout files.

## Planned screens (rough order)

1. Debug overlay: position, cell, draw calls, FPS.
2. Main menu / world selection.
3. Dialogue menu.
4. Inventory.
5. Quest log, character sheet, map.
6. Console / script console.
