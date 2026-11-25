# VDF archive format

Union's virtual disk format (`VDF`) packs the game's records into archives. The managed
reader `OpenGothic/ZenKit/IO/Vfs.cs` mounts them and exposes a case-insensitive tree; this
note records the layout and the mount behavior ported from ZenKit (MIT), which the
reference client uses.

## Header

The header is 296 bytes:

| Offset | Size | Field |
| --- | --- | --- |
| 0 | 256 | Comment. The display text ends at the first `0x1A`; the rest is padding. |
| 256 | 16 | Signature. |
| 272 | 4 | Catalog entry count. |
| 276 | 4 | File count. |
| 280 | 4 | Archive timestamp in DOS format. |
| 284 | 4 | Archive size in bytes. |
| 288 | 4 | Catalog offset. Zero means "the header size" (296). |
| 292 | 4 | Volume flags. |

Recognized signatures:

| Signature | Source |
| --- | --- |
| `PSVDSC_V2.00\r\n\r\n` | Gothic 1 |
| `PSVDSC_V2.00\n\r\n\r` | Gothic 2 |
| `PSVDSC_V2.00\x1A\x1A\x1A\x1A` | VDFS tool archives |

Volume flags are `0x50` for uncompressed file data and `0xA0` for Union's ZippedStream
blocks. The catalog itself is always uncompressed. The managed port reads the `0x50`
archives shipped by Gothic 1/2 and rejects `0xA0` with a clear error until the block
compressor is ported.

## Catalog

Every catalog entry is 80 bytes:

| Offset | Size | Field |
| --- | --- | --- |
| 0 | 64 | Name, padded with spaces. |
| 64 | 4 | Offset. For a file this is an absolute archive offset; for a directory it is the index of its child table. |
| 68 | 4 | Size in bytes (files only; directories store zero). |
| 72 | 4 | Type flags. |
| 76 | 4 | Attributes (unused by the reader). |

Type flags:

- `0x80000000` — the entry is a directory.
- `0x40000000` — this is the last entry of its directory.

A directory's child table starts at `catalog_offset + offset * 80`. Entries are laid out
depth-first: a level's entries are contiguous, then each directory's child block follows in
sibling order. The root level is the list that starts at the catalog offset.

Names are compared case-insensitively and trailing whitespace is trimmed before comparison.
The shipped archives pad names with spaces.

## Mount behavior

Mounting parses the catalog into the VFS root. A file entry is validated against the archive
size (`offset + size <= archive_size`), then added with the archive timestamp. A directory
entry is created or reused; if a file already occupies the name, the overwrite rule decides
whether it is replaced.

`VfsOverwriteBehavior` follows the reference:

| Value | Behavior |
| --- | --- |
| `None` | Keep the existing node. |
| `All` | Replace it. |
| `Newer` | Replace only when the existing timestamp is newer than the incoming one. |
| `Older` | Replace only when the incoming timestamp is newer than the existing one (the default for whole-disk mounts). |

Directories with the same name are merged regardless of timestamp. Missing or truncated
catalogs are reported instead of looping: the reader treats an entry that starts at the end
of the archive as the last one.

## Lifetimes

`VfsNode.OpenRead()` returns a stream over the archive. A node is only valid while its
`Vfs` is alive; the managed VFS holds either a memory-mapped view of the archive or the
caller's byte array. `Vfs.Dispose()` releases those views. `Assets` keeps one mounted `Vfs`
per archive for the process lifetime.
