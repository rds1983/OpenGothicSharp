# OpenGothicSharp

**A C# port of [OpenGothic](https://github.com/Try/OpenGothic)** — the open-source C++ re-implementation of the **Gothic 2: Night of the Raven** client. This project targets .NET 8 and MonoGame, reads data from a locally installed copy of the game (VDF archives, ZEN worlds, MDL models, DAT scripts) and runs it without the original executable.

## Stack

- **C# / .NET 8**
- **MonoGame** — platform, input, rendering
- **DigitalRiseModel** — models, skeletons, animations
- **Myra** — UI
- **BepuPhysics2** — physics and collision
- **ZenKit port** - managed VDF/ZEN/MDL/DAT readers in `OpenGothic/ZenKit/`; no native package

## Requirements

Gothic 2: Night of the Raven must be installed. **No game assets are bundled or redistributed** — the client loads them from your installation at runtime.

## Build & run

```bash
dotnet build OpenGothic.MonoGame.sln
dotnet run --project OpenGothic/OpenGothic.MonoGame.csproj -- -g "<path-to-gothic>"
```

Flags: `-g <path>`, `-game:<modfile.ini>`, `-w <world.zen>`, `-nomenu`, `-devmode`,
`-nocull` / `-cull:true|false`. See [Documentation/](Documentation/README.md).

## Development approach

1. Make all gameplay entities visible — hero, NPCs, objects, levels.
2. Add activities, scripts, combat, story, audio.
3. Once the game can be finished, pursue visual and UI parity.

**Visual parity is the lowest priority.** Initially rendering uses stock MonoGame effects (`BasicEffect`, `SkinnedEffect`, …). A custom shader system and visual parity come only after gameplay is polished. The same applies to UI: Myra is used for convenience first, parity later.

## Source metrics

Measured on 2026-11-26 from the working tree. C# files under `OpenGothic/` and
`OpenGothic.Tests/` only; `bin/`, `obj/` and IDE folders are excluded. "Code" excludes blank
and comment-only lines; inline comments count as code.

| Area | C# files | Size | Physical lines | Code | Comments | Blank |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| All source | 127 | 769.1 KiB | 28,904 | 22,143 | 2,144 | 4,617 |
| `OpenGothic/` | 92 | 659.2 KiB | 24,481 | 18,497 | 2,117 | 3,867 |
| `OpenGothic.Tests/` | 35 | 109.9 KiB | 4,423 | 3,646 | 27 | 750 |

Main project by folder:

| Folder | Files | Physical lines | Code | Size |
| --- | ---: | ---: | ---: | ---: |
| `(root)` | 20 | 5,948 | 4,367 | 174.7 KiB |
| `Graphics` | 16 | 2,379 | 1,546 | 69.8 KiB |
| `Physics` | 4 | 1,698 | 1,249 | 49.3 KiB |
| `Scripts` | 3 | 1,479 | 1,193 | 39.3 KiB |
| `World` | 14 | 3,589 | 2,606 | 103.5 KiB |
| `Utility` | 4 | 496 | 358 | 14.9 KiB |
| `UI` | 9 | 647 | 528 | 16.3 KiB |
| `ZenKit` | 22 | 8,245 | 6,650 | 191.5 KiB |

The totals include four Myra-generated `*.Generated.cs` UI files: 293 physical lines,
245 code lines, 8.0 KiB.

## Docs

- [TASKS.md](TASKS.md) — roadmap and current backlog
- [AGENTS.md](AGENTS.md) — agentic workflow and legal/provenance rules
- [Documentation/](Documentation/README.md) — architecture, subsystem notes, format specifications

## Credits

- [OpenGothic](https://github.com/Try/OpenGothic) — the C++ project this is a port of
- [MonoGame](http://www.monogame.net/)
- [ZenKit](https://github.com/GothicKit/ZenKit) - the C++ format reader this managed port follows
- [DigitalRiseModel](https://github.com/rds1983/DigitalRiseModel)
- [Myra](https://github.com/rds1983/Myra)
- [BepuPhysics2](https://github.com/bepu/bepuphysics2)

## License

MIT (see [LICENSE](LICENSE)).
