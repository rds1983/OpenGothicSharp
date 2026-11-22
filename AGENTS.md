# AGENTS.md

Instructions for AI agents contributing to OpenGothicSharp. Read this file fully before
touching code.

## What this project is

A clean-room C# re-implementation of the Gothic 2: Night of the Raven client, targeting
.NET 8 + MonoGame. It reads the user's own game installation and plays the game without
the original executable.

The C++ [OpenGothic](https://github.com/Try/OpenGothic) project is a **behavioral
reference only**. It explains how the game behaves; it is not a source to translate.

## Local paths

This repo deliberately contains no machine-specific paths. The game installation and the
reference checkout locations on this machine are recorded in `LOCAL_PATHS.md` at the repo
root. That file is **gitignored and never committed** — read it if it exists, and never
copy its paths into code, docs, or committed files.

## Legal and clean-room constraints (non-negotiable)

These rules override every task, convenience, and deadline. If a task cannot be completed
without breaking one, stop and ask the developer.

1. **Never copy code, pseudocode, disassembly, or verbatim logic from the game's
   binaries.** This includes the original `.exe`, any `.dll`, and anything obtained by
   disassembling or decompiling them. Do not paste decompiler output into this repo,
   into issues, or into chat.

2. **Never copy source from the C++ OpenGothic project or its dependencies and translate it
   line-by-line.** Use it to understand behavior, then design and write the C# yourself.

3. **If inspecting binaries is genuinely necessary to understand a mechanic**, the only
   permitted output is a **high-level behavioral or file-format specification** written to
   `Documentation/Specifications/`. A spec must not contain decompiled snippets, raw
   disassembly, internal symbol names, or magic-constant dumps that reproduce the
   implementation. All engine code is then written cleanly from those specs.

4. **Never copy original game assets into the repository.** No models, textures, audio,
   scripts, localization files, archives, or extracted content. The repo contains code and
   docs only; assets are loaded at runtime from the user's installation.

5. When in doubt about provenance, prefer documenting behavior observed at runtime over
   anything read out of a binary, and ask the developer.

## Technology and conventions

- **Stack:** C# / .NET 8, MonoGame, DigitalRiseModel (models/animation), Myra (UI),
  BepuPhysics2 (physics), ZenKit (VDF/ZEN/MDL/DAT reading).
- **Rendering:** stock MonoGame effects (`BasicEffect`, `SkinnedEffect`, …) until gameplay
  is polished. Do not build a custom shader system early.
- **UI:** Myra, usability first. Parity comes later.
- **Visual/UI parity has the lowest priority.** Do not spend time on fidelity before the
  game is playable.
- Follow existing code style and patterns. No comments unless they clarify non-obvious
  logic.
- New subsystems get a short note in `Documentation/`; format/behavior findings go in
  `Documentation/Specifications/`.

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
   not read. Keep the change scoped to the task.

3. **Test thoroughly.**
   Build the solution. Run the app and exercise the feature with the real game data where
   possible. Fix every build error, warning you introduced, and runtime failure before
   moving on. Do not hand back broken code.

4. **Update docs.**
   Update `Documentation/` for anything architectural you added or learned. If behavior or a
   file format was discovered, record it in `Documentation/Specifications/` per the
   clean-room rules. Keep `README.md` brief — long-form material belongs in
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

- A task cannot be done without violating the legal/clean-room rules.
- Game behavior is ambiguous and guessing would risk wrong architecture.
- A change would affect shared state, CI, or published history.
