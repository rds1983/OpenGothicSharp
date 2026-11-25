# Daedalus scripts (G1вЂ“G3)

The compiled scripts live outside the VDF archives:

```
_work/Data/Scripts/_compiled/GOTHIC.DAT
_work/Data/Scripts/CONTENT/CUTSCENE/Ou.dat
```

`GOTHIC.DAT` is the Daedalus symbol/instruction file. `Ou.dat` is the output-unit /
cutscene library used for dialogs; it is read by the managed `CutsceneLibrary` and supplies
the text of every `AI_Output` line. In the tested NotR installation the game script has
**74,649 symbols** and the cutscene library **20,826 blocks**.

## Managed DAT reader (N4a)

`OpenGothic/ZenKit/Script/DaedalusScript.cs` reads `GOTHIC.DAT` directly: the symbol table
(name, type, flags, count, parent, address, class size, member offset, source position and the
inline int/float/string values), the address map and the instruction text. `InstructionAt`
decodes every opcode operand the VM needs. The parser mirrors the reference's string-parent
heuristic; function addresses in the text are relative to the text section (external function
addresses are not code addresses).

On the tested installation the managed symbol table matches ZenKitCS for the whole file
(74,648 symbols), including names, types, flags, parents, addresses, values and decoded
instructions for non-external functions.

## Managed VM (N4b)

`OpenGothic/ZenKit/Script/DaedalusVm.cs` is the managed VM: a 2,048-frame stack, call
frames, every opcode the game script uses, `SELF`/`OTHER`/`VICTIM`/`HERO`/`ITEM` globals,
temporary strings (`$PHOENIX_FAKE_STRINGS`), and the external-call ABI. The external
parameter order follows the reference: arguments are pushed in source order and popped in
reverse. An unbound external pops its parameters and receives the script's default return
value, so a missing binding cannot corrupt the stack.

`OpenGothic/ZenKit/Script/DaedalusInstance.cs` stores instance values in per-class slot
arrays. `NpcInstance`, `ItemInstance` and `InfoInstance` are typed views over that storage,
so scripts and the C# game code read and write the same values. Member names follow the
compiled symbol table (for example `C_NPC.GUILD`, `C_ITEM.VISUAL_CHANGE`).

On the reference installation the managed VM matches ZenKitCS for the four startup entry
points: the same **5,228** external calls in the same order, and identical name, guild,
level, attributes, daily-routine pointer, senses, item visual and dialog-info fields for
sampled instances. A hermetic fixture in `OpenGothic.Tests/ZenKit/DaedalusVmTests.cs`
covers parameters, globals, members, instance initialization, registered externals and the
default external.

## Cutscene library (N4c)

`OpenGothic/ZenKit/Assets/CutsceneLibrary.cs` maps the BIN_SAFE `zCCSLib` tree of `Ou.dat`
to typed blocks and messages through the shared `ZenArchive` reader. It matches ZenKitCS for
all **20,826** blocks of the reference file: block names, message text, audio names and
message types. Nested `zCCSBlock` chains are followed to the atomic message. The ASCII
`.lsc` export is not read because the game loads `Ou.dat`; the reader accepts BIN_SAFE only.
A hermetic fixture in `OpenGothic.Tests/ZenKit/CutsceneLibraryTests.cs` covers the root
count, message fields, nested blocks and name lookup. The `Log` bridge is now the only
native ZenKit dependency (N6).

## Boot

`OpenGothic/Scripts/ScriptRuntime.cs` builds an `OpenGothic.ZenKit.DaedalusVm` and follows
the reference's `GameSession::initScripts` order:

```
startup_global
startup_<world>      e.g. startup_newworld
init_global
init_<world>         e.g. init_newworld
```

Each entry point is called only when the symbol exists. In the tested data the four calls
complete in about 0.03 s.

## External functions

The VM has one `RegisterExternalDefault` handler. An engine routine that is not implemented
yet logs once per distinct name and receives the VM's default return value. This keeps the
startup scripts running without unimplemented-call crashes while later milestones supply the
real bindings. The newworld boot reaches ten distinct routines:

`GAME_INITGERMAN`, `WLD_INSERTITEM`, `WLD_INSERTNPC`, `HLP_GETNPC`, `NPC_ISDEAD`,
`HLP_ISVALIDNPC`, `PLAYVIDEO`, `WLD_SETGUILDATTITUDE`, `WLD_SETMOBROUTINE`,
`WLD_ASSIGNROOMTOGUILD` (5,228 calls in total).

Because unimplemented routines use the VM default, their video/attitude/routine effects are
not applied; G2 supplies the NPC/item insertion and tabulates the results below.

## Script objects and events (G2)

`WLD_INSERTNPC` and `WLD_INSERTITEM` are bound to real script-object storage. On the tested
NotR startup they initialize **406 NPCs** and **55 items** (some inserted symbols repeat), while
`PC_HERO` is initialized as the script hero and `HLP_GETNPC` returns the instance for an
inserted symbol. Each NPC binding stores only its `NpcInstance`, detached `NpcVob` and
spawnpoint; name, guild, level and HP live on the `NpcVob`. `MDL_SETVISUAL*`, inventory and AI
routines still use the default handler.

The script objects were initially data-only. F1.5 now gives the human NPCs a world body,
collision capsule and focus target; monster bodies and movement/AI remain.

`LOG_ADDENTRY` is bound to the journal list. See the dialog section below for the real
conversation path that replaced the old `B_ASSESSTALK` diagnostic.

## Dialogs (G3)

The dialog engine mirrors `GameScript::dialogChoices`, `updateDialog` and
`DialogMenu::onStart/onEntry/onDoneText`:

- On boot every `C_INFO` instance is initialized: **4,139 instances** covering **392 NPC
  symbols** in the tested data. The list is grouped by the `Npc` field, which is the NPC
  instance symbol index.
- A menu is built from the NPC's infos. Important entries come first; within a group the
  entries are ordered by `Nr`, then information function. An info is skipped when the hero
  already knows a non-permanent entry, when an important permanent entry was already
  executed in this conversation, or when its `condition` function returns zero. Conditions
  run with `SELF` = NPC and `OTHER` = hero.
- An important entry with an empty description is executed immediately instead of being
  shown, matching `DialogMenu::onStart`.
- Executing an entry sets the same `SELF`/`OTHER` scope, calls the information function as
  a void routine and marks a normal information as known for the hero. `Info_ClearChoices`
  and `Info_AddChoice` populate the next menu; sub-choices take precedence over rebuilding
  the NPC menu. `Ai_StopProcessInfos` ends the conversation.
- `AI_Output` resolves the output-unit name through `Ou.dat` and becomes a `DialogLine`.
  The speaker is derived from the first argument, so the hero's own lines are marked as such.
  `AI_OutputSvm`/`AI_OutputSvm_Overlay` are recorded with their raw `$вЂ¦` output unit; the SVM
  speech table is not loaded yet, so their text is unresolved.
- `Npc_KnowsInfo`, `Npc_GiveInfo` and `Npc_CheckInfo` are bound. `AI_ProcessInfos` records a
  dialog request for the UI when no conversation is active. `InfoManager_HasFinished`
  reports whether a session exists.

### Running a conversation

`-talk` runs the first NPC's dialog automatically, picking each distinct entry once until
the script ends it. `-npc:<instance>` selects another inserted NPC. `-talkui` opens the
interactive panel at boot; `F4` opens it at runtime. Digits pick an entry, Up/W and Down/S
move the cursor, Enter confirms and Escape closes. A full City Guard conversation in the
tested data:

```
Dialog choice: What's up?
Dialog Hero: What's up?
Dialog City Guard: Now listen. We can't let you into the city.
Dialog City Guard: But I'll give you a tip, and it's completely free of charge.
Dialog City Guard: Stay well away from this forest in front of us - it's haunted by ferocious monsters.
Script dialog: dialog with 'City Guard' completed in 1 step(s), 4 line(s).
```

`INFO_ADDCHOICE` is exercised by e.g. `MIL_319_PABLO`: an important opening is executed
automatically, its two sub-choices become the menu, and the selected answer completes the
dialog.

### Limits

- `DialogLine.Voice` is the `<output-unit>.wav` name; `VoicePlayer` plays it through
  MonoGame `SoundEffect`. J1 owns the general sound-definition system.
- A choice authored as a trade entry runs its script function but does not open a trade UI.
- The dialog camera targets the midpoint between the two placed actors and orbits to the
  side, then restores the exploration camera on close. It is active only when the NPC has a
  world body and the player is next to it.
- Conditions that depend on quest/topic state (`LOG_GetTopicStatus`, `Npc_GetDistToWP`,
  `Npc_HasItems`, ...) return the VM default, so some entries are unavailable or repeat until
  the quest/journal layer (G4) and the relevant world externals exist. A missing item
  external can also make ZenKit log a recoverable null-member message while the script still
  completes. The City Guard and Pablo conversations above complete with the current bindings.
- ZenKit 1.3 logs a recoverable `pop_reference`/`SELF` error while evaluating a few Vatras
  conditions in the tested data. The condition still returns the correct value and the
  conversation completes; the reference's C++ VM does not emit the message.

## Scope

- Human script NPCs are world-bound and can start the dialog. Non-human bodies, routines,
  movement and AI remain.
- `-noscripts` skips loading and booting for diagnostics.

**Done when:** startup scripts run without unimplemented-call crashes. The four entry points
complete; default returns cover the unbound externals.
