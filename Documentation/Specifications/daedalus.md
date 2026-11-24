# Daedalus scripts (G1/G2)

The compiled scripts live outside the VDF archives:

```
_work/Data/Scripts/_compiled/GOTHIC.DAT
_work/Data/Scripts/CONTENT/CUTSCENE/Ou.dat
```

`GOTHIC.DAT` is the Daedalus symbol/instruction file. `Ou.dat` is the output-unit /
cutscene library used for dialogs; it is loaded with ZenKit `CutsceneLibrary` and kept for
G3. In the tested NotR installation the game script has **74,649 symbols** and the cutscene
library **20,826 blocks**.

## Boot

`OpenGothic/Scripts/ScriptRuntime.cs` builds a `ZenKit.Daedalus.DaedalusVm` and follows the
reference's `GameSession::initScripts` order:

```
startup_global
startup_<world>      e.g. startup_newworld
init_global
init_<world>         e.g. init_newworld
```

Each entry point is called only when the symbol exists. In the tested data the four calls
complete in about 0.013 s.

## External functions

The VM has one `RegisterExternalDefault` handler. An engine routine that is not implemented
yet logs once per distinct name and receives the VM's default return value. This keeps the
startup scripts running without unimplemented-call crashes while G2 supplies the real
bindings. The newworld boot reaches ten distinct routines:

`GAME_INITGERMAN`, `WLD_INSERTITEM`, `WLD_INSERTNPC`, `HLP_GETNPC`, `NPC_ISDEAD`,
`HLP_ISVALIDNPC`, `PLAYVIDEO`, `WLD_SETGUILDATTITUDE`, `WLD_SETMOBROUTINE`,
`WLD_ASSIGNROOMTOGUILD` (5,228 calls in total).

Because these are stubs, no NPCs or items are inserted yet and video/attitude/routine data is
not applied.

## Script objects and events (G2)

`WLD_INSERTNPC` and `WLD_INSERTITEM` are bound to real script-object storage. On the tested
NotR startup they initialize **406 NPCs** and **55 items** (some inserted symbols repeat), while
`PC_HERO` is initialized as the script hero and `HLP_GETNPC` returns the instance for an
inserted symbol. Each NPC binding stores only its `NpcInstance`, detached `NpcVob` and
spawnpoint; name, guild, level and HP live on the `NpcVob`. `MDL_SETVISUAL*`, inventory and AI
routines still use the default handler.

The objects are **data-only**: there is no world node, body or collision yet. Visible NPCs
and movement depend on the AI/visual pipeline (`F2`/`G2` continuation).

`LOG_ADDENTRY` is bound to the journal list. The `-talk` diagnostic calls the real
`B_ASSESSTALK` script routine for the first inserted NPC, then `B_LOGENTRY`; the resulting
`LOG_ADDENTRY` line is the observable Daedalus side effect:

```
Script NPC interaction: talked to 'City Guard'; journal 'You speak with City Guard.'.
```

## Scope

- Script objects are data-only: no world node, body, collision or AI queue yet.
- `-noscripts` skips loading and booting for diagnostics.

**Done when:** startup scripts run without unimplemented-call crashes. The four entry points
complete; default returns cover the unbound externals.
