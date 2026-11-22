# Specifications

Clean-room specifications of game behavior and file formats.

## Rules

Read [AGENTS.md](../../AGENTS.md) first. Anything in this folder must obey the project's
legal and clean-room constraints:

- **No decompiled code, no disassembly, no pseudocode lifted from game binaries.**
- **No internal symbol names, offsets, or raw constant dumps** that reproduce an
  implementation.
- **No copied game assets.**

A specification here describes *behavior* and *data layout* at a high level, so that
engine code can be written cleanly from it. Specifications are written from observation
(runtime behavior, file bytes, published docs), not from reverse-engineered source.

## Format

Each specification is a Markdown file named after its subject, e.g.
`zen-world-format.md`, `npc-perception.md`. Prefer:

- purpose and when the mechanic/format is used,
- observable inputs and outputs,
- data layout in prose or tables,
- edge cases and open questions.

If a needed mechanic cannot be specified without violating the rules above, stop and ask
the developer.
