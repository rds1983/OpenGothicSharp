# Mob interaction behavior

Doors, chests, switches and wheels are ZEN `oCMob*` vobs. The reference's `Interactive` class
attaches the player to an authored position, runs player and mob animations, waits for the
transition, then commits the new state. The port implements the mob-side state machine first:
a door or chest animates and its state round-trips. Player attach animations, inventory use,
sounds and script calls are later milestones; their authored names are kept and logged.

## Authored data

`IInteractiveObject` supplies:

- `State` — the highest authored state; state 0 is closed. Doors and chests use 1.
- `Target` — trigger target fired on a state change.
- `Item` — item required to use the mob.
- `OnStateChangeFunction` — Daedalus function called after a state change.
- `Rewind` — whether the mob returns to its previous state.

`IDoor`/`IContainer` add `IsLocked`, `Key` and `PickString`; containers also carry their items.

The model script defines the clips. Both `DOOR_NW_POOR_01` and
`CHESTSMALL_NW_POOR_OPEN`, for example, ship:

| Clip | Use |
| --- | --- |
| `S_S0` | closed loop |
| `T_S0_2_S1` | opening, `Next = S_S1` |
| `S_S1` | open loop |
| `T_S1_2_S0` | closing, `Next = S_S0` |
| `T_S0_TRY` / `T_S0_UNLOCK` | locked and failed-use reactions |

## Port

`InteractiveVob` caches the authored state count, lock/item/trigger/function strings and the
bed flag. Beds are `oCMobDoor` in the engine, but sleeping is not implemented.

At load, a door/chest/switch/wheel model gets an `AnimationData` set to `S_S0`, so the bind
pose is not mistaken for an open state. On interaction:

1. reject beds and generic `Interactive` mobs (benches, cauldrons, …) as not implemented;
2. reject locked mobs and mobs that require an item;
3. ignore the use while a `T_` transition is playing;
4. advance `State` with `NextState` (0 → 1 → … → 0);
5. play `T_S<from>_2_S<to>` with `S_S<to>` as the fallback.

`InteractionSystem.NextState` is the pure state cycle and is unit-tested. `StateChanged` is
the subscription point for audio and the script event pipeline in J1/G2. The interaction log
states the outcome, container item count, and the authored script/target hooks as pending.

## Known limits

- The player does not yet play the attach/use animation; only the mob animates.
- Door/chest **collision** is the static bind-pose collider. An opened door still blocks until
  the physics source is rebuilt; that is a later task.
- Inventory use items, lock-picking, trigger targets, state-change functions and sounds are
  recorded but not executed. G2 and J1 supply those systems.
- `Rewind` and the authored attach positions are not used yet.
