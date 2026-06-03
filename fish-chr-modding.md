# Dark Cloud Fish Chr Modding Notes

Research conducted 2026-06-03 via PINE IPC memory scanning and direct ISO inspection.

## Disc Archive Format (DATA.HED / DATA.DAT)

The game stores all assets in a flat archive. Loose files on the UDF disc are only the
executable (`SCUS_971.11`) and a handful of system files. Everything else is packed inside:

- `DATA.DAT` — 1.6 GB blob of all game assets, sector-aligned (0x800 bytes/sector)
- `DATA.HED` — flat array of 0x50-byte index entries; each entry describes one file in DAT

### HED Entry Layout (0x50 bytes per entry)

```
+0x00  char[64]  filename, null-padded (path separator is backslash e.g. "chara\f01a.chr")
+0x40  uint32    byte offset of this file's data in DATA.DAT (always sector-aligned)
+0x44  uint32    file size in bytes
+0x48  uint32    (unknown — may be next-file offset or runtime use)
+0x4C  uint32    (unknown)
```

The interior bytes (+0x08 to +0x3F) appear to be runtime RAM pointers patched by the
loader; they should be treated as opaque when creating new entries.

### Extracting a File (Python snippet)

```python
import struct

hed  = open("DATA.HED", "rb").read()
dat  = open("DATA.DAT", "rb")
ENTRY = 0x50

for i in range(len(hed) // ENTRY):
    base = i * ENTRY
    name = hed[base:base+0x40].split(b'\x00')[0].decode('ascii').replace('\\','/')
    offset, size = struct.unpack_from('<II', hed, base + 0x40)
    if name == "chara/f01a.chr":
        dat.seek(offset)
        open("f01a.chr", "wb").write(dat.read(size))
        break
```

### Injecting a New File

To add `chara/f09a.chr` (the missing fish slot):

1. Append the new chr blob to the end of `DATA.DAT` (pad to the next 0x800 boundary).
2. Append a new 0x50-byte HED entry pointing at that offset with the correct size.
   Copy the interior bytes (+0x08–+0x3F) verbatim from an adjacent entry — the game
   patches them at runtime.
3. Repack the ISO (or point PCSX2 at the modified folder if running from a directory).

The archive has no file count header; the game appears to scan entries until an empty
name is hit, so simply appending is safe.

---

## Chr Container Format

Each `.chr` file is itself a mini-archive with an interleaved layout:

```
[TOC entry 0x50 bytes] [file data, padded to 0x10] [TOC entry 0x50 bytes] [file data] ...
```

### Sub-Files Inside a Fish Chr

| File          | Description                                          |
|---------------|------------------------------------------------------|
| `info.cfg`    | ASCII config: references img/mds/mot, keyframe data  |
| `f01a.mds`    | Geometry — Level-5 MDS format (see below)            |
| `f01a.img`    | Texture — likely PS2 TIM2 or similar                 |
| `f01a.mot`    | Motion animation data                                |
| `f01a.bbp`    | Bounding-box / physics data                          |
| `f01a.wgt`    | Vertex weights (for skeletal animation)              |

### info.cfg Example (Bobo / f01a)

```
IMG 0, "f01a.img"
IMG_END
MATERIAL_ANIME 0
VERTEX_ANIME 1
ALLOC_DBUFF "obj9"
MODEL "f01a.mds"
BODY_SIZE 18, 7, 60
MOTION 0, "f01a.mot", "f01a.bbp", "f01a.wgt"
SHADOW_MOTION "", "", ""
KEY_START 0
KEY    10,    24,    0.25,   // swim idle loop 0
KEY    30,    44,    0.50,   // ...
...
MOTION_END
```

---

## MDS Format (Level-5 Geometry)

Magic: `MDS\x00` at byte 0 of the mds sub-file.

```
+0x00  char[4]   "MDS\0"
+0x04  uint32    version (observed: 1)
+0x08  uint32    object/bone count (observed: 0x1D = 29 for Bobo)
+0x0C  uint32    (unknown)
+0x10  uint32    0
+0x14  uint32    offset to bone table (observed: 0x70)
+0x18  char[8]   root bone name (e.g. "hari\0...")
```

Bone entries follow at the offset given in +0x14. Each entry appears to be 0x70 bytes
and contains the bone name and a 4×4 transform matrix (confirmed: identity matrices are
visible as `0x3F800000` on the diagonal). Bone names observed: `hari`, `null1`, `chn1`.

The vertex buffer format following the bone table is not yet decoded. It is almost
certainly PS2 VU1 DMA packet data (VIF1 packets with inline GIF tags), which requires
knowing the VU microprogram to interpret. No open-source tools are known to handle
Level-5 MDS as of 2026.

---

## Fish Model Files on Disc

Sizes from DATA.HED (Dark Cloud USA):

| File         | DATA.DAT Offset | Size (bytes) | Notes                          |
|--------------|-----------------|--------------|--------------------------------|
| f00s.chr     | 0x0031D800      | 109,296      | Shared shadow model (all fish) |
| f01a.chr     | 0x00338800      | 168,656      | Bobo (ID 0)                    |
| f02a.chr     | 0x00362000      | 181,328      | Gobbler (ID 1)                 |
| f03a.chr     | 0x0038E800      | 170,144      | Nonky (ID 2)                   |
| f04a.chr     | 0x003B8800      | 172,656      | Kaiji (ID 3)                   |
| f05a.chr     | 0x003E3000      | 207,456      | Baku Baku (ID 4)               |
| f06a.chr     | 0x00416000      | 251,312      | Mardan Garayan (ID 5)          |
| f07a.chr     | 0x00489000      | 136,032      | Gummy (ID 6)                   |
| f08a.chr     | 0x004AA800      | 152,368      | Niler (ID 7)                   |
| f09a.chr     | —               | —            | **MISSING** (cut fish, ID 8)   |
| f10a.chr     | 0x004D0000      | 172,432      | Umadakara (ID 9)               |
| f11a.chr     | 0x004FA800      | 138,272      | Tarton (ID 10)                 |
| f12a.chr     | 0x0051C800      | 160,128      | Piccoly (ID 11)                |
| f13a.chr     | 0x00544000      | 445,248      | Bon (ID 12)                    |
| f14a.chr     | 0x005B1000      | 146,112      | Hamahama (ID 13)               |
| f15a.chr     | 0x005D5000      | 664,800      | Den (ID 14)                    |
| f16a.chr     | 0x00677800      | 134,320      | Heela (ID 15)                  |
| f17a.chr     | 0x00698800      | 162,160      | Baron Garayan (ID 16)          |
| f18a.chr     | 0x006C0800      | 244,672      | (ID 17 — unconfirmed name)     |

> **f09a.chr**: Confirmed absent from DATA.HED entirely. The string `"chara/f09a.chr"` exists
> in EE RAM (in the model pointer array at 0x20296570, index 8) but the file was never
> added to the archive. Writing fish ID 8 to a slot produces an invisible fish. This is the
> "Missing Fish" — a species cut before its model was created.

---

## Practical Path to Adding a Custom Fish

### Step 1 — Prove the Pipeline (easy)

Clone `f08a.chr` (Niler) verbatim as `f09a.chr`. The "Missing Fish" will look like Niler
but proves the archive injection and ID wiring work end-to-end. ~20 lines of Python.

### Step 2 — Custom Texture (medium)

Replace the `.img` sub-file inside the chr. Likely PS2 TIM2 format; tools like `tim2view`
or `PVR2 Studio` can convert PNG → TIM2. Requires understanding the chr container write path.

### Step 3 — Custom Geometry (hard)

Replace the `.mds` sub-file. Requires either:
- Reverse-engineering the MDS vertex buffer layout (VIF1 DMA packets, likely interleaved
  with a VU1 microprogram — significant effort)
- Finding/building a Level-5 MDS exporter (none known as of 2026)

The bone table structure is partially decoded (see above). The vertex data immediately
follows but its exact format is unknown.
