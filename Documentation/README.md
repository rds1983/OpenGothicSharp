# Documentation

Long-form project documentation. `README.md` at the repo root stays brief; architecture,
subsystem notes, and findings live here.

## Layout

- [Specifications/](Specifications/README.md) — clean-room behavioral and file-format
  specifications.

## Subsystems

To be filled in as subsystems land. Each should get a short note describing:

- what it is responsible for,
- the main types and where they live,
- how it talks to other subsystems,
- known limitations or TODOs.

## Rendering

Uses stock MonoGame effects (`BasicEffect`, `SkinnedEffect`, …) until gameplay is polished.
A custom shader system and visual parity are deliberately deferred; see
[TASKS.md](../TASKS.md) milestone M.

## UI

Built with Myra. Usability first; parity is deferred to milestone M.
