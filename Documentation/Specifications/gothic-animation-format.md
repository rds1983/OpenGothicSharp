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

## Edge cases and open questions

- Whether a script may drive models with genuinely different bone names beyond the
  attach-node case above is not observed here; every measured pair maps by name.
- 161 clips disagree with their script on frame rate, and the file's value was used. Which
  one the game honoured is not established; the file is the only source that also stores
  the frames.
- Aliases (1,847) and combinations (58) in the scripts are named clip variants with their
  own blend/next settings that reuse another clip's keys. They are not needed to enumerate
  or read clips and are left to playback.
- `AnimationDirection` other than forward, `Combo`/cycle flags and event tags are recorded
  but not interpreted yet.
