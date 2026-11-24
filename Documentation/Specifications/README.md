# Specifications

Behavioral and file-format specifications of the game.

This project is a port of [OpenGothic](https://github.com/Try/OpenGothic), so specs exist to
pin down behavior that the port reproduces — usually after reading how OpenGothic does it.
They are the shared, written form of that knowledge; the engine code is written from them.

## Rules

Read [AGENTS.md](../../AGENTS.md) first. Anything in this folder must obey the project's
legal and provenance rules:

- **No binary analysis, decompiled code, disassembly, or pseudocode lifted from game
  binaries.** This is absolute; there is no exception for a mechanic that OpenGothic does
  not explain.
- **No internal symbol names, offsets, or raw constant dumps** that reproduce an
  implementation.
- **No copied game assets.**

A specification here describes *behavior* and *data layout* at a high level, so that engine
code can be written cleanly from it. Specifications are written from observation (runtime
behavior, file bytes read through the documented formats, published docs) and from the
behavior described by the OpenGothic source — never from reverse engineering the game's own
code.

## Format

Each specification is a Markdown file named after its subject, e.g.
`zen-world-format.md`, `npc-perception.md`. Prefer:

- purpose and when the mechanic/format is used,
- observable inputs and outputs,
- data layout in prose or tables,
- edge cases and open questions.

If a needed mechanic cannot be specified without violating the rules above, stop and ask
the developer.
