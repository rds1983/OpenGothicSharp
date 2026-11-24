# NPC runtime behavior (F1/F1.5)

Gothic's shipped worlds contain **no `oCNpc` vobs**. `NEWWORLD.ZEN`, `OLDWORLD.ZEN`,
`ADDONWORLD.ZEN` and `DRAGONISLAND.ZEN` all report zero NPCs; the Daedalus startup scripts
insert them with `Wld_InsertNpc`.

Because of that, F1 does **not** invent placeholder NPCs or bodies. It provides the runtime
container a script-created NPC will use:

| Member | Meaning |
| --- | --- |
| `NpcInstance` | script instance name, e.g. `NPC_DIEGO` |
| `Guild`, `Level` | character data from the ZEN/script NPC |
| `Hp`, `MaxHp`, `IsDead` | health |
| `TakeDamage(amount)` | clamps health at zero and logs damage/death |
| `Say(message)` / `LastMessage` | bark/debug message hook; scripted dialog lines use `DialogLine` |

`NpcVob` is created by `VobFactory` when an `oCNpc` source exists, exactly like the other
typed vobs. The focus system already accepts `VobKind.Npc`, and its overlay reports
`Npc '<name>' hp <hp>/<maxHp>`.

## World-bound script NPCs (F1.5)

`NpcSpawner` runs after the Daedalus boot and turns the script bindings into placed bodies.

- `WorldGrid` keeps the waynet (`Waypoint` position, facing, free flag). An NPC starts at its
  authored spawn point; when that point is already used, the nearest unused waypoint within
  800 units is chosen, matching `WayMatrix::findNextPoint`. Missing points use the dead point
  `TOT` (or `(-1000,-1000,-1000)`).
- A human body is the armour item's `VisualChange` mesh (`Armor_Mil_L.asc` → `ARMOR_MIL_L.MDM`)
  or the naked body `.MDM`, composed with `HUMANS`; the head `.MMB` is attached to
  `BIP01 HEAD`. The composed geometry is cached and shared.
- `MDL_SetVisualBody`'s body/head/teeth/colour variant is applied per instance by rewriting
  the `_V0`/`_C0` texture tags. `GothicModel.Materials[mesh][part]` starts as the model's own
  materials; the variant replaces individual entries with a `Material` carrying the variant
  texture, so the geometry stays shared. A missing variant falls back to the authored
  texture. `MDL_SetVisual` and `MDL_SetModelScale` are stored on `NpcVisual`.
- Each NPC gets a kinematic Bepu capsule at its feet. Focus scans the typed vob tree, ignores
  the NPC's own capsule when checking occlusion, and E opens the G3 dialog.
- The body idles on `S_RUN` while the NPC is within 3000 units of the player; a distant NPC
  keeps its last pose. A fixed direction from the waypoint turns the body.
- A dialog opened with a placed NPC uses a two-shot dialog camera (midpoint, side orbit) and
  restores the exploration camera on close. `DialogLine.Voice` is played through MonoGame
  `SoundEffect`; the full sound-definition layer is J1.

The tested NotR startup has **406 script NPCs**: **356 human bodies are placed**, and **50
non-human bodies are deferred** (see below). The 172 NPCs assigned to
`NW_CITY_ENTRANCE_01` share the five nearby waypoints and therefore overlap until F2 routines
move them.

## Deferred

Monster bodies need their model-script skeleton (`MDL_SetVisual`/`.MDS`) and animation set;
loading them during F1.5 was both slow and, for one body, allocation-heavy. They stay as
script objects until F2/F3. AI, routines, movement, combat and armour attachments (pauldrons
and similar) also remain. The dialog camera is a usability-first two-shot, not the reference's
exact spin; voice playback is the minimal J1 hook.
