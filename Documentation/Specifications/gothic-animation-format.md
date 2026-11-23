# Animation format (`.MSB`, `.MDS`, `.MAN`)

How a model's animation clips are stored and how they have to be attached to a model
before a clip can be played. Written from the parsed structure and from measuring the
shipped data, not from any binary.

## Purpose

A model file (`.MDL`, or `.MDH` + `.MDM`) contains geometry and a skeleton, but **no
animation**. Clips are described by a separate *model script* and stored one file per clip:

| File | Role |
| --- | --- |
| `.MSB` | Model script. Lists the clip names plus per-clip playback metadata. Gothic 2 ships this name. |
| `.MDS` | The same format under Gothic 1's extension. The shipped Gothic 2 data contains none. |
| `.MAN` | One clip's actual keys, named `<SCRIPT>-<CLIP>.MAN`. |

The script is therefore the entry point: a model name alone does not lead to clips, so a
loader has to find the script that *owns* the model first.

## Locating the script that owns a model

Two independent mappings exist in the data, and they agree everywhere they overlap:

1. **By mesh list.** `IModelScript.Meshes` lists the `.ASC` meshes the script can drive.
   The file stem of each entry is a model stem: `HUMANS.MSB` lists `ARMOR_PRISONER.ASC`,
   so `ARMOR_PRISONER` is driven by `HUMANS`. This is the only mapping that reaches the
   armour models, because they are not skeletons of their own.
2. **By skeleton name.** `IModelScript.SkeletonName` names the script's skeleton
   (`SHEEP.MSB` → `SHEEP_BODY`), and a model whose stem equals that name is driven by the
   script. This is the mapping that reaches the creature models.

Measured on `newworld.zen`: 63 distinct visual model names, of which **37 resolve to a
script** (36 by skeleton name, 1 — `HAMMEL_BODY` — by mesh list only). The remaining 26
are static props (benches, herbs, ore, thrones) with no script and therefore no clips.
No model resolves through both mappings to two different scripts.

An index over all 117 scripts can be built once: parse each `.MSB`, record
`skeletonName → script` and each `meshStem → script`.

## Locating the skeleton a clip addresses

**A clip's channel indices address the script's skeleton, not the model's own hierarchy.**
This is the single most important fact in this format. Measured consequence: `HUMANS`
clips carry 34 channels, while `ARMOR_PRISONER.MDL` has only 25 nodes — the model omits
the nine `ZS_*` attach nodes (`ZS_HELMET`, `ZS_SWORD`, …) that the skeleton has. Applying
channel index *i* to model node *i* would silently animate the wrong bone from the
seventh channel on.

The script's `SkeletonName` does not always have a hierarchy file (for `HUMANS` the
`HUM_BODY_NAKED0` skeleton ships only a `.MDM` mesh, no `.MDH`), so resolution needs a
fallback:

1. the script's `SkeletonName` as `STEM.MDL`, else `STEM.MDH`;
2. otherwise the script's **own stem** as `STEM.MDL`, else `STEM.MDH`.

A candidate is only accepted if its node count is at least the largest channel count of
the script's clips — a hierarchy shorter than the clips address cannot be the right one.
Across all 117 scripts this resolves 75 by `SkeletonName` and 37 by script stem, with
none unresolved.

The resolved hierarchy is used for **node names only**. Each channel's node index is
looked up in it to get a bone name, and the name is then matched against the target
model's bones. Channels whose name the model lacks are dropped; that is the intended
behaviour, since an attach node the model has no geometry for has nothing to move.
Verified with `SHEEP` → `HAMMEL_BODY` (31 of 31 channels map even though the model has 37
nodes) and `ORC` → `ORC_BODY` (32 of 32).

### Why names and not indices

All skeleton hierarchies in the shipped data have **zero** local translations — the bind
geometry lives entirely in the skin weights. So a clip's samples cannot be validated
against the hierarchy, and node indices carry no recoverable geometry either. The names
are the only stable link between the two hierarchies, and the node ordering already
differs between them (`ARMOR_PRISONER`'s `BIP01 L CLAVICLE` is node 7, `HUMANS`'s is node 8).

## Clip keys

`IModelAnimation` (one `.MAN` file) holds:

| Field | Meaning |
| --- | --- |
| `NodeIndices` | Skeleton node index per channel, in storage order. Usually every node once, but the order is arbitrary (`HUMANS-S_RUN` stores 0…24, then 26…33, then 25). |
| `FrameCount` | Number of frames stored. |
| `Fps` | Playback rate. This is the authoritative one: the script's copy differs for 161 clips (e.g. `DEMON/C_LOOK_*` is 25 in the script and 1 in the file). |
| `Samples` | `FrameCount × NodeIndices.Count` samples. |

Each sample is a **position and a rotation** in the node's local space; there is no scale
channel. Layout is **frame-major**:

```
sample[frame * NodeIndices.Count + slot]  →  node NodeIndices[slot]
```

Verified for all 3,949 clips: `Samples.Count == FrameCount × NodeIndices.Count` in every
case, and the frame-major reading of a known clip produces smoothly changing values
(`HUMANS-S_RUN` node 0: y = 97.71, 97.71, 97.70, 97.70 over the first frames) where the
node-major reading does not.

The sample values are bone-local transforms in Gothic's left-handed space and go through
the same one-time Z mirror as every other loaded position and rotation.

## Clip metadata

The `.MSB` entry (not the `.MAN`) carries the playback parameters, and they have to be
kept for playback:

- `Name`, `Next` — the clip to chain into;
- `Layer`, `Flags`, `Direction` — layering, loop/cycle flags, and whether the clip runs in
  reverse;
- `BlendIn`, `BlendOut`, `Speed` — transition times and rate;
- `FirstFrame`, `LastFrame` — the clip's slice of a shared frame timeline.

`FirstFrame`/`LastFrame` do **not** index into the `.MAN` samples. For 444 of the clips
`LastFrame - FirstFrame + 1` is not equal to the file's `FrameCount` (e.g.
`ALLIGATOR/S_FISTWALK` says 0…59 but stores 24 frames), so the sample array is the clip,
and the frame range only describes where it sits in the original timeline.

Clips with an empty `NodeIndices` list exist (18 of 3,949) — they are still enumerable but
have nothing to drive.

## Data volume

The full set is large enough that clips must not be loaded eagerly for the whole game:

| Scope | Clips | Keyframes |
| --- | --- | --- |
| all 117 scripts | 3,949 | ≈ 2.29 M (≈ 87 MB as transforms) |
| `HUMANS` alone | 784 | 484 K |
| every script reachable from `newworld.zen` | — | 134 K (≈ 6 MB) |

Per-script parse cost is dominated by the number of files: `HUMANS` 169 ms for 784 clips,
`ORC` 42 ms, `DRAGON` 27 ms, `SHEEP` 3 ms.

So: load a script's clips when a model that uses it is first needed, and share the result
between every model with the same script **and the same bone-name sequence**. All 40-odd
armour models share one 25-name skeleton, so they should share one set of clip objects
rather than re-reading 784 files each.

## Attaching clips to a model

`AnimationClip` addresses bones by index in the model's depth-first bone numbering, while
a channel still carries a *skeleton* node index. The two therefore need a translation,
built once per skeleton/model pair: skeleton node index → node name → model bone index.
Channels that survive that translation become `AnimationChannel(boneIndex, keyframes)`
with one keyframe per sample at `frame / Fps` seconds and a clip duration of
`FrameCount / Fps`.

## Playback rules

Playback is decided by the script, and the shipped data confirms each rule. These are the
findings a player has to reproduce; they are measured across all 117 scripts, 3,949 clips
and 1,847 aliases.

### Looping versus one-shot

The script has no explicit "loop" flag that is used. Instead:

- A clip whose `Next` names **itself** cycles. 1,309 clips do this.
- A clip whose `Next` names **another clip** runs once and hands over. 2,640 clips, 1,228 of
  which name nothing and therefore just stop on their last frame.
- **Override:** a clip carrying a `ComboWindow` event is a one-shot regardless of a
  self-reference, and its `Next` is treated as cleared. 17 clips fall in this group (the
  attack animations), which is what lets a sound attack be interrupted at its combo window
  instead of restarting forever.

### Frame rate and duration

`fps` is the `.MAN` value and duration is `FrameCount / Fps`. The `.MAN` wins over the
script for 161 clips because it is the only file that also stores the frame count. The 149
clips whose `.MAN` stores `fps = 1` are all single-frame poses (`C_LOOK_*`, `C_CBOW_*`), so
any rate yields the same picture.

`speed` is not used. **All 3,949 shipped clips store `speed == 0`**, so a player that
multiplied by it would freeze every animation. Ignoring it is a data-driven decision, not an
oversight.

### Direction

`AnimationDirection.Backward` mirrors the sample walk: time 0 shows the last stored frame and
time increases toward frame 0. 197 clips are backward. The walk has to be mirrored rather
than negated in time, because the stored frames are the forward-order samples; for a looping
clip the mirror also has to skip the closing wrap key, or the clip would appear to run
forward.

DigitalRiseModel's `AnimationFlags.PlayBackwards` samples `duration - time`, which puts a
reverse clip on frame 0 at t = 0 — the opposite of the convention above. Measured on a
3-frame looping clip, the flag alone yields frame 0 where the data means frame 2. One frame
period of `AnimationBlendLayer.TimeOffset`, with the flag set on the layer, yields frame 2
and mirrors the walk for the whole clip. The offset belongs on the layer rather than on
`AnimationController.Time`, so the controller's clock stays equal to game time.

### How the player uses `AnimationController`

The controller samples and blends; `AnimationData` keeps the state machine, which is the
part the script owns: `Next` chaining, per-alias direction and blend times, and the
`ComboWindow` one-shot rule. Two conventions need bridging:

- **Reverse** — as described above, a one-layer `AnimationBlendNode` whose layer carries
  `PlayBackwards` plus one frame period of `TimeOffset`.
- **Zero-length fades** — 917 of the 1,253 declared transitions ask for no ramp, so those go
  through `StartClip`; `CrossfadeToClip` would divide by its fade length and evaluate a `NaN`
  weight on the first frame.

A bone the incoming clip does not drive now eases toward the bind pose across the fade, and
`StartClip` applies its first pose immediately, so the two behaviours that previously had to
be implemented locally are the library's. The outgoing layer is advanced through the ramp,
not frozen at the value it held when the switch happened; the reference freezes it, so a
fade longer than a frame can show the old clip continuing to move for its duration.

### Layers

Every script entry carries a `Layer`, and it is not decorative. Over all `HUMANS` clips:
layer 1 has 644 clips (locomotion, full 34-node skeleton), layer 2 has 67 (combat overrides
covering only `BIP01 SPINE1` upward, e.g. `T_MOVE_2_FISTMOVE` with 14 nodes), layer 4 has 18
(bow stance, 17 nodes), layer 9 has 9 (head only, `C_LOOK_*`), layer 11 has 9 (an arm
gesture, `C_POINT_*` with 8 nodes), and layer 20 has 37 (full-body turns, 25–26 nodes).

Layers are sorted ascending and applied in order: a bone is taken from the highest layer that
drives it, and a layer leaves every bone it does not have a channel for untouched. That is why
a `C_POINT_1` overlay over `S_RUN` moves the arm and leaves the legs running — measured as
6,539 changed pixels confined to the upper body, versus 17,103 across the whole model when the
same clip is played as the base. The port expresses the two rules as layers of one
`AnimationBlendNode`: `BoneFilter` gives each layer only the bones no higher layer drives, and
`TimeOffset` gives each clip its own clock.

Attacks are layer 1 and full body, so "walking and attacking at once" is not something the
shipped data does through this mechanism; the game's layered clips are the transitions and
gestures above. The reference also ramps a new layer in over its blend time, which is not
ported yet: a layered change snaps.

### `FirstFrame` and `LastFrame`

These are **not** the clip's own frame range. They index a shared frame timeline across the
whole script: consecutive single-frame poses show `first == last == 0, 1, 2, …`. They are
only meaningful for converting event frames to times, never for the sample walk.

### Aliases

An alias is a second name with its **own** playback metadata: 542 are backward, 659 name a
different `Next`, and 782 loop by naming themselves. The samples come from the target clip,
but the direction, next, layer and blend times come from the alias. Treating an alias as a
plain rename is wrong in a visible way — it plays every backward alias forward and demotes
every self-naming alias to a one-shot.

Alias resolution is by name, one hop in practice (aliases never collide with a clip name and
never name a missing target across the shipped data), with a bounded loop for the few
scripts that alias an alias.

### Blend times

`BlendIn`/`BlendOut` go up to 0.8 s / 1.0 s. A clip change ramps over the longer of the
outgoing clip's `BlendOut` and the incoming clip's `BlendIn`, and switches immediately when
both are zero — which is most of the declared transitions (917 of 1,253), so a default ramp
would soften clip changes the data asks to be instant.

### Loop seams are authored, not a sampler bug

488 of 789 measurable looping clips have a last-to-first seam that exceeds 3× their own
median frame-to-frame rotation delta — `HUMANS/T_CBOWSNEAKTURNR` reaches 703×. That is a
property of the data, not of the interpolation. The fix is the closing wrap key described
above (last interval interpolates back to frame 0) plus blend-in on clip changes; a hardcoded
seam cross-fade would paper over authored poses and is deliberately not used.

## Edge cases and open questions

- Whether a script may drive models with genuinely different bone names beyond the
  attach-node case above is not observed here; every measured pair maps by name.
- Combinations (58) are named blends of two clips and are not interpreted yet; nothing in
  the current playback path needs them.
- Event tags are read (they decide the `ComboWindow` one-shot rule) but not yet dispatched as
  gameplay events: hit frames, item handling and sounds belong to the combat milestone.
- Whether the game blends `BlendOut` of the outgoing clip as well as `BlendIn` of the
  incoming one is not established; only `BlendIn` is used, matching the reference's
  single-ramp blend.
