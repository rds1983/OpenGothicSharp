# ZTEX texture format

Gothic stores textures in Union's `ZTEX` container. The managed reader
`OpenGothic/ZenKit/Material/Texture.cs` is a port of ZenKit's texture loader (MIT); this note
records the layout and the pixel formats the renderer consumes.

## Header

| Offset | Size | Field |
| --- | --- | --- |
| 0 | 4 | Signature, `ZTEX`. |
| 4 | 4 | Version, always zero. |
| 8 | 4 | Pixel format (see below). |
| 12 | 4 | Width in pixels of mipmap level 0. |
| 16 | 4 | Height in pixels of mipmap level 0. |
| 20 | 4 | Mipmap count. Zero is treated as one. |
| 24 | 4 | Reference width used by the engine. |
| 28 | 4 | Reference height used by the engine. |
| 32 | 4 | Average color, stored as a raw 32-bit value. |

For format `P8` the header is followed by 256 palette entries of four bytes each, in
`B, G, R, A` order.

## Mipmaps

Mipmaps are stored **smallest first**: the level with the lowest resolution comes directly
after the header (and palette), and level 0 is last. The reader hides this quirk:
`GetMipmapRaw(0)` and `GetMipmapRgba(0)` return the full-size image, and `MipmapCount` counts
all stored levels.

The byte size of a level follows the level's dimensions:

| Formats | Bytes per pixel or block |
| --- | --- |
| `B8G8R8A8`, `R8G8B8A8`, `A8B8G8R8`, `A8R8G8B8` | 4 per pixel |
| `B8G8R8`, `R8G8B8` | 3 per pixel |
| `A4R4G4B4`, `A1R5G5B5`, `R5G6B5` | 2 per pixel |
| `P8` | 1 per pixel (palette index) |
| `DXT1` | 8 per 4×4 block |
| `DXT2`, `DXT3`, `DXT4`, `DXT5` | 16 per 4×4 block |

Block dimensions are clamped to at least one block, so a 2×2 `DXT1` mipmap still occupies
eight bytes. Each level halves both dimensions, stopping at one.

## Formats and decoding

| Value | Name | RGBA conversion |
| --- | --- | --- |
| `0x0` | `B8G8R8A8` | swap red and blue |
| `0x1` | `R8G8B8A8` | copy |
| `0x2` | `A8B8G8R8` | reverse the four channels |
| `0x3` | `A8R8G8B8` | rotate the four channels |
| `0x4` | `B8G8R8` | swap red and blue, alpha 255 |
| `0x5` | `R8G8B8` | copy, alpha 255 |
| `0x6` | `A4R4G4B4` | expand 4-bit channels by 17; the 16-bit word is `B` low nibble, then `G`, `R`, `A` |
| `0x7` | `A1R5G5B5` | stored but not decoded, same as ZenKit |
| `0x8` | `R5G6B5` | 5/6/5-bit channels scaled to 0–255 |
| `0x9` | `P8` | index into the 256-entry palette |
| `0xA` | `DXT1` | S3TC, 1-bit alpha when colour 0 is not greater than colour 1 |
| `0xB` | `DXT2` | stored but not decoded, same as ZenKit |
| `0xC` | `DXT3` | S3TC with explicit 4-bit alpha |
| `0xD` | `DXT4` | stored but not decoded, same as ZenKit |
| `0xE` | `DXT5` | S3TC with interpolated alpha |

`DXT2` and `DXT4` are mathematically the premultiplied-alpha variants of `DXT3`/`DXT5`, but
ZenKit does not convert them, so neither does the port. The shipped Gothic 2 textures use
`DXT1`, `DXT3`, `DXT5`, `P8`, `R5G6B5` and `A4R4G4B4`.

The renderer uploads `DXT1`, `DXT3` and `DXT5` mipmaps to the GPU unchanged and decodes
every other format to RGBA8. The material pass additionally treats a `DXT1` texture with an
opaque material color as solid, which is the reference's cut-out rule.

## Verification

On the reference install, the managed reader produces the same format, dimensions, mipmap
count and raw mipmap bytes as ZenKitCS for all 2,543 textures in `Textures.vdf`; sampled RGBA
decodes are byte-identical too.
