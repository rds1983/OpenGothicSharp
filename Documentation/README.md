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

`Program.Main` initializes logging, configures `AssetManagementBase` logging, and runs
`OgGame`.

`OgGame` (`OpenGothic/OgGame.cs`) is the `Microsoft.Xna.Framework.Game` subclass that owns
the window and `GraphicsDeviceManager`. On `Initialize` it publishes the device through
`OG.GraphicsDevice`, so subsystems can create GPU resources without threading a device
through every call. `Update`/`Draw` form the main loop; Escape or the gamepad Back button
exits. Window title carries `OG.Version`.

### Command line

`CommandLine` (`OpenGothic/CommandLine.cs`) parses startup arguments and exposes them via
`CommandLine.Instance`: `-g <path>` (Gothic installation), `-game:<modfile.ini>` (mod
manifest), `-w <world.zen>` (startup world), `-nomenu` (skip main menu), and `-devmode`.
Unknown arguments are logged as warnings. `Validate()` checks that `-g` was given and that
the folder contains a `Data` directory. `Main` logs the parsed values, and returns exit
code 1 with an actionable error if validation fails.

### Settings

`IniFile` (`OpenGothic/IniFile.cs`) is a small INI reader/writer: `;` comments,
`[SECTION]` headers, `name=value` pairs, case-insensitive section/key lookup, Latin-1
(Windows-1252) encoding, and `Save()` that rewrites the file.

`Settings` (`OpenGothic/Settings.cs`) layers four INI sources, most-specific first: the
user file (`Gothic.ini` in the working directory), the installation's
`system/Gothic.ini`, `system/SystemPack.ini`, then built-in defaults. It exposes typed
properties (volumes, video brightness/contrast/gamma, mouse sensitivity, camera FOV, FPS
limit, FPS counter) and generic `GetString/GetInt/GetFloat/Set` accessors. `Set` writes to
the user file; `Save()` flushes it. `Main` loads settings after command-line validation and
publishes them via `OG.Settings`.

The user `Gothic.ini` is written to the working directory at runtime and is gitignored.

### Logging

`Log` (`OpenGothic/Log.cs`) is the single logging entry point and exposes `Log.Info`,
`Log.Warning`, and `Log.Error`. `Log.Initialize()` runs before anything else and routes
`ZenKit.Logger` messages under a `zenkit:<name>` category. Each line is written to the
console (errors to stderr) and to `log.txt` in the working directory, timestamped and
level-tagged. `Log.Shutdown()` flushes and closes the file; it is called from a `finally`
in `Main` so even a fatal exception is recorded first. `log.txt` is gitignored.

`ZenKit` ships native libraries and injects a `RuntimeIdentifier` per platform, so the
project declares `RuntimeIdentifiers` for its supported platforms to keep restore and
build targets in sync.

## Testing

Unit tests live in `OpenGothic.Tests` (xunit). Run them with:

```bash
dotnet test OpenGothic.MonoGame.sln
```

Tests avoid touching the real game installation — they build temporary folders and INI
files instead. Anything requiring a GPU, a window, or game data is exercised by running
the app, not by unit tests.

## Rendering

Uses stock MonoGame effects (`BasicEffect`, `SkinnedEffect`, …) until gameplay is polished.
A custom shader system and visual parity are deliberately deferred; see
[TASKS.md](../TASKS.md) milestone M.

## UI

Built with Myra. Usability first; parity is deferred to milestone M.
