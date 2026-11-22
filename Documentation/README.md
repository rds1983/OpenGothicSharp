# Documentation

Long-form project documentation. `README.md` at the repo root stays brief; architecture,
subsystem notes, and findings live here.

## Layout

- [Specifications/](Specifications/README.md) — clean-room behavioral and file-format
  specifications.

## Subsystems

Each subsystem gets a short note describing what it is responsible for, its main types,
how it talks to other subsystems, and known limitations.

### Application host

`Program.Main` configures `AssetManagementBase` logging and runs `OgGame`.

`OgGame` (`OpenGothic/OgGame.cs`) is the `Microsoft.Xna.Framework.Game` subclass that owns
the window and `GraphicsDeviceManager`. On `Initialize` it publishes the device through
`OG.GraphicsDevice`, so subsystems can create GPU resources without threading a device
through every call. `Update`/`Draw` form the main loop; Escape or the gamepad Back button
exits. Window title carries `OG.Version`.

## Rendering

Uses stock MonoGame effects (`BasicEffect`, `SkinnedEffect`, …) until gameplay is polished.
A custom shader system and visual parity are deliberately deferred; see
[TASKS.md](../TASKS.md) milestone M.

## UI

Built with Myra. Usability first; parity is deferred to milestone M.
