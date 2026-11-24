# OpenGothicSharp

**A C# port of [OpenGothic](https://github.com/Try/OpenGothic)** — the open-source C++ re-implementation of the **Gothic 2: Night of the Raven** client. This project targets .NET 8 and MonoGame, reads data from a locally installed copy of the game (VDF archives, ZEN worlds, MDL models, DAT scripts) and runs it without the original executable.

## Stack

- **C# / .NET 8**
- **MonoGame** — platform, input, rendering
- **DigitalRiseModel** — models, skeletons, animations
- **Myra** — UI
- **BepuPhysics2** — physics and collision
- **ZenKit** — VDF/ZEN/MDL/DAT file formats

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

Measured on 2026-11-24 from the working tree. C# files under `OpenGothic/` and
`OpenGothic.Tests/` only; `bin/`, `obj/` and IDE folders are excluded. "Code" excludes blank
and comment-only lines; inline comments count as code.

| Area | C# files | Size | Physical lines | Code | Comments | Blank |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| All source | 67 | 355.3 KiB | 12,422 | 9,033 | 1,357 | 2,032 |
| `OpenGothic/` | 56 | 310.6 KiB | 10,531 | 7,525 | 1,347 | 1,659 |
| `OpenGothic.Tests/` | 11 | 44.7 KiB | 1,891 | 1,508 | 10 | 373 |

Main project by folder:

| Folder | Files | Physical lines | Code | Size |
| --- | ---: | ---: | ---: | ---: |
| `(root)` | 17 | 4,906 | 3,546 | 147.1 KiB |
| `Graphics` | 15 | 2,030 | 1,307 | 60.6 KiB |
| `Physics` | 4 | 1,581 | 1,161 | 45.7 KiB |
| `Scripts` | 1 | 129 | 102 | 3.3 KiB |
| `World` | 8 | 993 | 726 | 28.8 KiB |
| `Utility` | 3 | 410 | 287 | 13.1 KiB |
| `UI` | 8 | 482 | 396 | 12.0 KiB |

The totals include four Myra-generated `*.Generated.cs` UI files: 286 physical lines,
239 code lines, 7.7 KiB.

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
