# Player avatar behavior

The placeholder hero is the player capsule plus an animated body. The behavior below is
ported from `Try/OpenGothic` (MIT): `common/graphics/mesh/animationsolver.cpp` for the clip
names, `common/graphics/mesh/pose.cpp` for root-motion handling, and
`common/game/playercontrol.cpp` / `common/game/movealgo.cpp` for walk mode.

## Model

Until the Daedalus VM can select `PC_HERO`, the body is composed the same way the `-compose:`
diagnostic builds a person:

```
skeleton: HUMANS
meshes:   HUM_BODY_NAKED0, ARMOR_PRISONER
head:     HUM_HEAD_PONY.MMB on BIP01 HEAD
```

The human head is not part of the body mesh: `Mdl_SetVisualBody` loads it as a separate
`.MMB` morph mesh and binds it to `BIP01 HEAD`. An `.MMB` is a morph mesh, not a model, so it
is loaded as geometry only; facial morphs are not played yet. Its vertices are authored in the
head bone's space, so it is attached like an accessory rather than baked into the body.

## Root translation and placement

A `.MDL`/`.MDH` hierarchy has a root node whose translation is zero and a separate
**root translation** offset. The bind pose is authored around the hips, so the offset lifts
it until the feet reach the origin. The reference adds that offset to the root node's bind
transform; the port puts it on the root bone's default pose. It must not be added a second
time by the caller.

Animation samples store **absolute** root positions, including the same offset
(`HUMANS-S_RUN` node 0: `y ≈ 97.71`). With the offset on the default pose, idle samples agree
with the bind and the body does not move vertically. Adding the offset again at the spawn
point — the first version did — lifts the whole body by ~97 units.

Directional clips add real root motion on top: `S_RUNL` moves root Z by ~262 units over its
20-frame cycle, `S_WALKL` by ~131 units over 24 frames. That is how Gothic tells the engine how
fast the character travels. The reference cancels the root node's X/Z before composing the
skeleton (`Pose::mkBaseTranslation`), and cancels Y too for props (`Pose::NoTranslation`).
`PlayerAvatar` applies the same X/Z cancellation each frame; the character controller owns
the actual movement. Without it the body would slide forward during a stride and snap back at
the loop.

The resulting placement is:

```
translation(-root.X, feetOffset, -root.Z) * rotationY(-cameraYaw) * translation(feet)
```

`feetOffset` is derived once from the bind box and the first idle frame, so the lowest vertex
sits on the capsule's feet. `rotationY(-cameraYaw)` is the handedness conversion: yaw 0 looks
down -Z, and the bind pose faces that way.

Props use the opposite rule. `ObjVisual::setYTranslationEnable(false)` selects
`Pose::NoTranslation`, so a vob visual cancels the root translation entirely. The loader
applies `translation(-rootTranslation)` for non-NPC vobs, which keeps the harbour bookstand on
its floor.

## Clip selection

`AnimationSolver` formats a clip name as `S_<weapon>RUN`, `S_<weapon>RUNL` and
`S_<weapon>WALKL`, where the weapon prefix is empty, `FIST`, `1H`, `2H`, `BOW`, `CBOW` or
`MAG`. The distinction that matters:

| Animation state | Clip | Frames | FPS | Duration | Flags |
| --- | --- | --- | --- | --- | --- |
| `Idle` | `S_RUN` | 50 | 25 | 2.00 s | Move, Idle |
| `Move`, running | `S_RUNL` | 20 | 25 | 0.80 s | Move |
| `Move`, walk mode | `S_WALKL` | 24 | 25 | 0.96 s | Move |

`S_RUN` is a standing loop: measured over all 34 nodes it changes rotation by under 2°, which
is why it must not be used for movement. The directional clips reach ~95° and carry the root
motion above. `S_WALK` is an alias to `S_RUN` in the shipped `HUMANS.MSB`, not a walk cycle;
the real walk data is `S_WALKL` (with `S_WALKR`, `S_WALKBL` and the rest as other directions).

`PlayerAvatar.SelectClip` implements the rule:

```text
speed < 10        -> S_RUN
10 <= speed, walk -> S_WALKL
10 <= speed, run  -> S_RUNL
```

A clip change is only requested when the name changes. This is the placeholder rule for
straight movement; turn and strafe clips are not selected yet.

## Walk mode

The reference toggles walk mode on an action (`PlayerControl::toggleWalkMode`), rather than
requiring the key to be held. The port toggles on either Shift key and caps the movement speed
at half the run speed. Movement, grounding and collision stay in `CharacterController`; only
its `Speed` value changes.

## Visibility

The avatar is drawn by `WorldRenderer` as an extra node after the grid cells. It is skipped in
first person because the camera sits inside the head, but its clip keeps advancing so an F5
switch is immediate.

## Not yet ported

- The script-selected hero model, body/head texture variants, armour and weapon state
  (`F1`, `G2`, `I3`).
- Turn-to-movement, backpedal and strafe clips (`MoveBack`, `MoveL`/`MoveR`).
- Head morph animation and the animation solver's combat, swim/sneak and transition cases.
