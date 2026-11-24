# NPC runtime behavior (F1)

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

## Deferred

Body/armour/head composition, animation, spawning, AI, movement, collision, waypoints and
combat belong with script-created NPCs. G2 supplies the script instance and the typed vob;
G3 turns its dialog data into conversations. The body/visual and world-binding work is
**F1.5 — World-bound script NPCs**.
