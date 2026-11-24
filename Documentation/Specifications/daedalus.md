# Daedalus script boot (G1)

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

## Scope

- The VM and cutscene library stay alive for G2/G3.
- No script objects are bound to world entities, no globals are mutated, and no event
  pipeline exists yet.
- `-noscripts` skips loading and booting for diagnostics.

**Done when:** startup scripts run without unimplemented-call crashes. The four entry points
complete; default returns cover the unbound externals.
