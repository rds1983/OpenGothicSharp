# OpenGothicSharp

**A C# port of [OpenGothic](https://github.com/Try/OpenGothic)** — the open-source C++ re-implementation of the **Gothic 2: Night of the Raven** client. This project targets .NET 8 and MonoGame, reads data from a locally installed copy of the game (VDF archives, ZEN worlds, MDL models, DAT scripts) and runs it without the original executable.

## Stack

- **C# / .NET 8**
- **MonoGame** — platform, input, rendering
- **DigitalRiseModel** — models, skeletons, animations
- **Myra** — UI
- **BepuPhysics2** — physics and collision
- **ZenKit** — VDF/ZEN/MDL/DAT file formats; the native package is being replaced by the
  managed port in `OpenGothic/ZenKit/`

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

Measured on 2026-11-25 from the working tree. C# files under `OpenGothic/` and
`OpenGothic.Tests/` only; `bin/`, `obj/` and IDE folders are excluded. "Code" excludes blank
and comment-only lines; inline comments count as code.

| Area | C# files | Size | Physical lines | Code | Comments | Blank |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| All source | 109 | 630.5 KiB | 23,023 | 17,244 | 2,061 | 3,718 |
| `OpenGothic/` | 80 | 544.0 KiB | 19,490 | 14,370 | 2,036 | 3,084 |
| `OpenGothic.Tests/` | 29 | 86.5 KiB | 3,533 | 2,874 | 25 | 634 |

Main project by folder:

| Folder | Files | Physical lines | Code | Size |
| --- | ---: | ---: | ---: | ---: |
| `(root)` | 20 | 6,070 | 4,421 | 178.9 KiB |
| `Graphics` | 16 | 2,217 | 1,427 | 66.1 KiB |
| `Physics` | 4 | 1,695 | 1,245 | 49.3 KiB |
| `Scripts` | 3 | 1,475 | 1,189 | 39.0 KiB |
| `World` | 14 | 3,304 | 2,402 | 95.0 KiB |
| `Utility` | 4 | 496 | 358 | 14.9 KiB |
| `UI` | 9 | 631 | 514 | 15.7 KiB |
| `ZenKit` | 10 | 3,602 | 2,814 | 85.1 KiB |

The totals include four Myra-generated `*.Generated.cs` UI files: 293 physical lines,
245 code lines, 8.0 KiB.

## Docs

- [TASKS.md](TASKS.md) — roadmap and current backlog
- [AGENTS.md](AGENTS.md) — agentic workflow and legal/provenance rules
- [Documentation/](Documentation/README.md) — architecture, subsystem notes, format specifications

## Credits

- [OpenGothic](https://github.com/Try/OpenGothic) — the C++ project this is a port of
- [MonoGame](http://www.monogame.net/)
- [ZenKitCS](https://github.com/GothicKit/ZenKitCS)
- [DigitalRiseModel](https://github.com/rds1983/DigitalRiseModel)
- [Myra](https://github.com/rds1983/Myra)
- [BepuPhysics2](https://github.com/bepu/bepuphysics2)

## License

MIT (see [LICENSE](LICENSE)).
