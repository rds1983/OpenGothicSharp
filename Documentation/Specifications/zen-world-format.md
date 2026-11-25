# ZEN world format

Gothic's worlds (`*.ZEN`) are ZenGin archives. The managed reader in
`OpenGothic/ZenKit/World/` is a C# port of ZenKit's `ReadArchive`, `World`, `Mesh`, `BspTree`,
`WayNet` and virtual-object readers (MIT); this note records the layout and the parts the
client consumes.

## Archive header

The file starts with ASCII lines:

```
ZenGin Archive
ver 1
zCArchiverGeneric
BIN_SAFE
saveGame 0
[date ...]
[user ...]
END
```

Shipped Gothic 2 worlds use `BIN_SAFE`; the embedded mesh material list uses `BINARY`.
`BINARY` and `ASCII` world archives are not read by the managed world reader yet.

After the text header, `BIN_SAFE` adds three `uint32`s: format version, object count and the
absolute offset of the hash table. The hash table is a count followed by entries of
`uint16 keyLength, uint16 insertionIndex, uint32 hash, bytes key`. The `hash` value is unused.

## Entries and objects

An object begins with a `STRING` entry containing `[name class version index]` and ends with
the string `[]`. Fields are `HASH, uint32 keyIndex, value`; the key index selects the hash
table entry. Value types:

| Byte | Type | Payload |
| --- | --- | --- |
| `0x01` | string | `uint16 length` + bytes |
| `0x02` | integer | `int32` |
| `0x03` | float | `float` |
| `0x04` | byte | `uint8` |
| `0x05` | word | `uint16` |
| `0x06` | bool | `uint32` |
| `0x07` | vec3 | three floats |
| `0x08` | color | `B, G, R, A` |
| `0x09` | raw | `uint16 length` + bytes |
| `0x10` | raw float | `uint16 byte length` + floats |
| `0x11` | enum | `int32` |
| `0x12` | hash | field key, not a value |

Class `%` marks an empty/terminal object. Class `\xA7` is a reference; its `index` points to a
previously read object, which the world reader resolves after parsing.

## World object

The root is `main oCWorld:zCWorld`. Its children include:

- `MeshAndBsp` — raw mesh and BSP block (see below).
- `VobTree` — the virtual-object tree.
- `WayNet` — holds a `zCWayNet` child.
- `EndMarker`.

## Mesh and BSP

`MeshAndBsp` is not a normal archive body. It begins with:

- `uint32` BSP version (`0x4090000` for Gothic 2).
- `uint32` section size.
- A sequence of mesh chunks. Each is `uint16 type, uint32 size, payload`. The scan stops at
  the mesh `END` chunk (`0xB060`); the BSP starts immediately after it.

The BSP is a second chunk sequence (`0xC000` header, `0xC010` polygon indices, `0xC040`
tree, `0xC050` sectors, `0xC0FF` end). Only the leaf-polygon list is consumed today; the
renderer still uses its own grid.

The mesh chunks are:

| Type | Contents |
| --- | --- |
| `0xB000` | `uint16` mesh version (`265` for G2), 16-byte date, name line |
| `0xB010` | bounding boxes (ignored) |
| `0xB020` | binary archive of materials (below), then a G2 alpha-test byte |
| `0xB030` | `uint32` count + `vec3` positions |
| `0xB040` | `uint32` count + (`vec2` texture, `uint32` light, `vec3` normal) |
| `0xB050` | polygons |
| `0xB025`, `0xB026` | lightmaps (ignored) |
| `0xB060` | mesh end |

A polygon is `uint16 material, int16 lightmap, float planeDistance, vec3 planeNormal`, then a
G2 flag byte, `int16 sector`, `uint8 vertexCount`, and `vertexCount` pairs of
`uint32 positionIndex, uint32 featureIndex`. Gothic 1 uses 16-bit position indices and two
flag bytes. Flags carry portal/occluder/sector/outdoor/ghost-occluder bits. The mesh keeps its
source polygons; the renderer fan-triangulates them, and ghost occluders are skipped.

### Embedded materials

The material chunk contains a complete `BINARY` archive header, then a `uint32` material
count. Each material is a name line followed by a binary object: `uint32 size`,
`uint16 version`, `uint32 index`, object name line, class name line (`zCMaterial`), then
fields in this fixed order:

`name`, `group` (byte), `color` (B,G,R,A), `smoothAngle`, `texture`, `texScale` (string),
`texAniFPS`, `texAniMapMode` (byte), `texAniMapDir` (string), `noCollDet`, `noLightmap`,
`losDontCollapse`, `detailObject`, then for Gothic 2: `detailObjectScale`, `forceOccluder`,
`environmentalMapping`, `environmentalMappingStrength`, `waveMode`, `waveSpeed`,
`waveMaxAmplitude`, `waveGridSize`, `ignoreSunLight`, `alphaFunc` (byte), and finally
`defaultMapping` (two floats). The object size in the header is used to skip any padding.

## Vob tree

`VobTree` is a flat stream, not nested object begins. It starts with the field `childs0`, the
root count. Each vob is followed by a `childsN` field holding its child count, then its
children in pre-order:

```
childs0 = 2
vob A; childs1 = 1; vob A0; childs2 = 0
vob B; childs3 = 0
```

This is why the generic object parser alone cannot rebuild the hierarchy; the reader follows
the `childsN` fields in archive order.

Every vob has a `pack` field. The shipped worlds use `pack = 1` with an 83-byte `dataRaw`
block (Gothic 2): bounding box (6 floats), position (3 floats), rotation (9 floats), a flag
byte, a `uint16` flag word, animation strength and far-clip scale. The first flag byte carries
show-visual, camera alignment, static/dynamic collision, static-vob and shadow bits. The flag
word carries preset/vob-name/visual-name/visual-object/ai-object presence, physics, animation
mode, bias and ambient bits. Unpacked vobs (`pack = 0`) use named fields instead:
`bbox3DWS`, `trafoOSToWSRot`, `trafoOSToWSPos`, `vobName`, `visual`, `showVisual`, `cdDyn`, etc.

Optional `presetName`, `vobName` and `visual` fields follow the packed block. The visual object
itself is a child named `visual`; its class selects the visual type (`zCModel`,
`zCProgMeshProto`, `zCMesh`, `zCDecal`, `zCParticleFX`, `zCAICamera`, `zCMorphMesh`). A
reference visual resolves to the referenced object's class.

Type-specific fields are read by name:

- `oCMOB`: `focusName`, `hitpoints`, `damage`, `moveable`, `takeable`, `focusOverride`,
  `soundMaterial`, `visualDestroyed`, `owner`, `ownerGuild`, `isDestroyed`.
- `oCMobInter`: `stateNum`, `triggerTarget`, `useWithItem`, `conditionFunc`, `onStateFunc`,
  `rewind`.
- `oCMobContainer`: `locked`, `keyInstance`, `pickLockStr`, `contains`.
- `oCMobDoor`: `locked`, `keyInstance`, `pickLockStr`.
- `oCItem`: `itemInstance` (plus save-game-only `amount`/`flags`).
- `oCNpc`: `npcInstance`, `guild`, `level`, ... (the client's script NPCs are spawned from
  Daedalus, so the full save-game NPC block is not consumed yet).

Mobsi schemes are not a field: the reference derives them from the visual file name prefix
(`BENCH_3_OC.ASC` -> `BENCH`), and the managed vob exposes the visual name for that match.

## Way net

`zCWayNet` has `waynetVersion`, `numWaypoints` and `numWays` fields. Its children are the
`numWaypoints` free points, followed by two waypoint objects per edge. Edge endpoints are
often references to an earlier waypoint; the reader resolves them and deduplicates by archive
index, then emits `Points` and `Edges` with point indices. A waypoint has `wpName`,
`waterDepth`, `underWater`, `position` and `direction`.

## Verification

On the reference install the managed reader matches ZenKitCS for both `Worlds.vdf` and
`Worlds_Addon.vdf` `NEWWORLD.ZEN`: mesh position/feature/material counts and metadata, all
427,887 (base) / 461,283 (addon) polygons and their indices, the BSP polygon and leaf lists,
the full vob tree (type, name, preset, position, bounds, rotation, visual and children), and
the waypoints. The addon world has 260,696 positions, 23 root vobs, 2,959 waypoints and
3,402 edges. The client renders the same 2,461 draw calls / 429,324 triangles as before the
port.
