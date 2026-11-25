# AGENTS.md

Instructions for AI agents contributing to OpenGothicSharp. Read this file fully before
touching code.

## What this project is

A C# port of [OpenGothic](https://github.com/Try/OpenGothic), the open-source C++ client for
Gothic 2: Night of the Raven. This port targets .NET 8 + MonoGame, reads the user's own game
installation and plays the game without the original executable.

It is **not a strict line-by-line port**. Same game, different implementation: a different
language, different libraries, and a different approach wherever the C++ design does not fit
the new stack. For example, OpenGothic's skinned vertex stores four positions, one per bone
weight; this project keeps a single position per vertex because that is what DigitalRiseModel
and MonoGame's stock `SkinnedEffect` consume. A divergence like that is not a bug — but it must
be a deliberate one, and the behavior on screen has to match.

Consequence for research: **the first place to look when implementing or debugging a mechanic
is how `Try/OpenGothic` does it.** Read its source for the behavior, the data handling, and the
edge cases it already solved, then implement the equivalent in C# with the libraries available
here. Only when OpenGothic has no answer does the task fall back to observation and original
design. Porting the behavior is the job; copying the source is not — see the rules below.

## Local paths

This repo deliberately contains no machine-specific paths. The game installation and the
reference checkout locations on this machine are recorded in `LOCAL_PATHS.md` at the repo
root. That file is **gitignored and never committed** — read it if it exists, and never
copy its paths into code, docs, or committed files.

## Legal and provenance rules (non-negotiable)

These rules override every task, convenience, and deadline. If a task cannot be completed
without breaking one, stop and ask the developer.

1. **Never analyze the game's binaries.** Do not disassemble, decompile, inspect, dump,
   read symbols from, or otherwise reverse engineer the original `.exe`, any `.dll`, or any
   code derived from them. Do not paste such output into this repo, into issues, or into
   chat. This is absolute; there is no "only way" exception. Reading the shipped *data*
   files (`VDF`, `ZEN`, `MDL`, …) through ZenKit is asset parsing, not binary analysis, and
   remains expected.

2. **Port the behavior, do not transliterate the source.** Reading `Try/OpenGothic` is
   expected — it is the primary reference — but what lands in this repo must be C# written
   for this stack, not a mechanical line-by-line translation of the C++ file. If an
   algorithm is deliberately carried over close to its original form, say so in the commit
   or a doc note; both projects are MIT, so that is a provenance note, not a legal problem.

3. **The C++ OpenGothic source is the only code reference.** If it does not explain a
   mechanic, use runtime observation, the documented data formats, or ask the developer.
   "Inspecting a binary is the only way to find out" is not a reason to do it; the task
   stops instead.

4. **Never copy original game assets into the repository.** No models, textures, audio,
   scripts, localization files, archives, or extracted content. The repo contains code and
   docs only; assets are loaded at runtime from the user's installation.

5. When in doubt about provenance, prefer documenting behavior observed at runtime. Never
   fall back to binary inspection; ask the developer.

## Technology and conventions

- **Stack:** C# / .NET 8, MonoGame, DigitalRiseModel (models/animation), Myra (UI),
  BepuPhysics2 (physics), and the managed ZenKit port in `OpenGothic/ZenKit/` (namespace
  `OpenGothic.ZenKit`) for VDF/ZEN/MDL/DAT reading. The C++ ZenKit source is the format
  reference; the native ZenKitCS package is no longer used.
- **Rendering:** stock MonoGame effects (`BasicEffect`, `SkinnedEffect`, …) until gameplay
  is polished. Do not build a custom shader system early.
- **UI:** Myra, usability first. Parity comes later.
- **Visual/UI parity has the lowest priority.** Do not spend time on fidelity before the
  game is playable.
- **Coordinate systems:** Gothic's game data is left-handed; MonoGame is right-handed. The
  conversion happens **once, while loading assets**, in `Mathematics.ToXna` (Z is mirrored).
  Everything past asset loading is right-handed. Do not add per-frame handedness flips in
  rendering code — if a conversion is missing, fix it where the asset is read.
- **Vertex data is copied verbatim.** Positions, normals and texture coordinates are taken
  from the file as-is; only the Z axis is mirrored during conversion. Do **not** flip or
  mirror U/V in mesh builders — a Z mirror already reverses triangle winding (handled by
  the index swap), and any additional UV flip corrupts every texture.
- Follow existing code style and patterns. No comments unless they clarify non-obvious
  logic.
- **Do not allocate per frame in hot paths.** Drawing runs 2k+ times per frame, so a `new`
  inside a draw call becomes tens of thousands of short-lived objects per second and shows
  up as GC hitching. Create small mutable state — transforms, `Material`s, frustums,
  scratch lists — once as a field and reassign its properties. Reuse buffers instead of
  `ToArray()`/`ToList()` per frame. Allocation at load time is free; allocation in
  `Draw`/`Update` is not.
- New subsystems get a short note in `Documentation/`; format/behavior findings go in
  `Documentation/Specifications/`.

## Command line options

Feel free to add command line options. They are cheap, they are not part of the public
interface, and they are the easiest way to expose a switch that helps debug a subsystem.

When you add one:

1. Parse it in `CommandLine.Parse` and expose it as a property on `CommandLine`. Follow the
   existing shapes: a bare flag (`-nomenu`) for an on/off switch, `-name:value` for a
   value. Leave the property **null** when the option is absent so "not specified" is
   distinguishable from "specified as false", and so a default chosen elsewhere is not
   silently overwritten.
2. Add it to the `Usage` string in `CommandLine.cs`.
3. Push it where it is consumed — `CommandLine.Apply()` for process-wide settings such as
   `DebugSettings`, or a property read in `OgGame` for game-specific state.
4. Log the effective value in `Program.Main` alongside the other parsed options, so a log
   from a bug report states exactly which switches were active.
5. Cover the parsing in `OpenGothic.Tests/CommandLineTests.cs`. These tests are hermetic:
   no game installation.
6. Document it in `Documentation/README.md` under "Command line".

Debug-only switches belong in `DebugSettings` (`OpenGothic/DebugSettings.cs`) as static
properties; they are startup diagnostics, not user preferences, so they do not belong in
`Settings`, which reads and writes the game's INI files. Anything surfaced through
`DebugSettings` that changes what is on screen should also be visible in the `DebugOverlay`
so a screenshot is self-describing.

## Development order

1. All gameplay entities visible: hero, NPCs, objects, levels.
2. Activities, scripts, combat, story, audio.
3. Once the game can be finished, visual and UI parity.

## Agentic workflow

Follow this loop for every unit of work.

1. **Get the next task from `TASKS.md`.**
   Open it, find the current milestone, and take the first unchecked task in it. Do not
   invent scope beyond that task.

2. **Do it.**
   Implement the task completely. Read the relevant code first; never edit a file you have
   not read. Before inventing an approach, check how `Try/OpenGothic` solved the same
   problem and port that behavior. Keep the change scoped to the task.

3. **Test thoroughly.**
   Build the solution. Run the app and exercise the feature with the real game data where
   possible. Fix every build error, warning you introduced, and runtime failure before
   moving on. Do not hand back broken code.

4. **Update docs.**
   Update `Documentation/` for anything architectural you added or learned. If behavior or a
   file format was discovered, record it in `Documentation/Specifications/` per the
   provenance rules. Keep `README.md` brief — long-form material belongs in
   `Documentation/`.

5. **Ask the developer whether the task is finished.**
   Summarize what changed, how it was tested, and any caveats. Explicitly ask for
   confirmation. **Do not mark the task done on your own judgment.**

6. **Only after explicit confirmation**, mark the task finished in `TASKS.md`. Then review
   the remaining tasks and reorganize them as you see fit — add, remove, split, merge, or
   reorder are all allowed. Finally commit and push.

## Git

- Commit only after the developer confirms the task is finished (step 6).
- Commit messages describe the behavior change, not the file churn.
- Push to `origin` (`git@github.com:rds1983/OpenGothicSharp.git`) on `master` unless told
  otherwise.
- Never commit game assets, logs, build output, or anything ignored by `.gitignore`.

## Escalate to the developer when

- A task cannot be done without violating the legal/provenance rules.
- Game behavior is ambiguous and guessing would risk wrong architecture.
- A change would affect shared state, CI, or published history.
