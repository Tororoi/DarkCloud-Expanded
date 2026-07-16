# Dark Cloud 2 extraction (for porting assets into the DC1 mod)

Tools for reading Dark Cloud 2 (`Dark Cloud 2 (USA) (v2.00).iso`, boot `SCUS_972.13`) so we can pull
assets — starting with the **Priscleen** fish — into the DC1 fishing mod.

Extracted files live in `ROMs/dc2_extracted/` (alongside `dc_extracted/`). Large archives are read
**in place from the ISO** (not copied): `DATA.HD2`/`DATA.HD3`/`SCUS_972.13` are extracted there, but
`DATA.DAT` (1.5 GB) stays in the ISO and is read at `LBA 2828`.

## Archive format (cracked)

DC2 packs assets in `DATA.DAT`, indexed by `DATA.HD2` (`DATA.HD3` is a parallel 16-byte index, unused):

```
DATA.HD2 = N entries (32 bytes) + a C-string name table (names use '\' separators)
  entry: u32 nameOff (absolute offset into HD2 -> name table)
         u32 pad[3]
         u32 dataOffset  (byte offset into DATA.DAT)
         u32 size        (bytes)
         u32 idx, u32 type
  N = entry[0].nameOff // 32   (name table begins right after the entries; = 6693 files)
File in the ISO = 2828*2048 + dataOffset.
```

`dc2_archive.py` implements this:
```
python3 dc2/dc2_archive.py list [substr]        # list files
python3 dc2/dc2_archive.py extract <path> [out] # extract one (path uses / or \)
# or import: from dc2_archive import read_file, list_files
```

## Priscleen — located

Chain, fully resolved:
- `sg/fish/fish.cfg`: `FISH_DATA 1, "<jp>", "f19", 310` → **fish #1 uses model `f19`, name message 310**.
- `menu/cfg7/comdatmes1.cfg`: `MES_SYS 310,"Priscleen"` → **message 310 = "Priscleen"** (DC2 names are
  plain ASCII, not glyph-encoded like DC1).
- So **Priscleen = fish 1 = model `f19a`**. Files:
  - `sg/fish/f19a.chr` (131632 B) — the fishing/catch model (closest analog to DC1's caught fish).
  - `menu/aqua/fish/f19a.chr` / `sg/gyo/f19a.chr` (79072 B) — aquarium / race models.
  - extracted to `ROMs/dc2_extracted/priscleen/`.

## Model format — DC1-compatible mesh 🎉

DC2 `.chr` wraps the **same MDS/MDT mesh container as DC1** (verts = XYZW floats @ MDT+0x40, same
display-list). The DC1 decoder (`tools/extract_scene_mesh.py` `read_verts`/`read_tris`) reads
Priscleen directly:
- MDT0: 224 verts, 330 tris; MDT1: 56 verts, 34 tris → **280 v / 364 t**, a fish ~28 units long.

So the mesh needs **no cross-engine conversion** — it extracts with existing tools.

## .chr container format — DC1 == DC2 (identical!)

Both games use the same pack format (DC1's `GetPackFile__FPUiPcPi` @0x13f720 parses DC2's `.chr` as-is):
a chain of sub-file entries, each:
```
  +0x00  name (C-string; the 0x08..0x3F region is ignored by GetPackFile — DC1 bakes load pointers there)
  +0x40  u32 dataOff   (always 0x50 — data follows the header)
  +0x44  u32 size
  +0x48  u32 nextStride (= 0x50 + align16(size); advance to next entry)
  +0x50  <data>
  ...ends with a 0x50-byte zero terminator (name byte 0).
```
Sub-file ORDER is irrelevant (lookup is by name). DC1 fish `.chr` sub-files: info.cfg, fNNa.mds, .img,
.wgt, .mot, .bbp. DC2 identical (info.cfg last instead of first). **So porting Priscleen needs NO
container rebuild and NO binary conversion** — copy the DC2 `.chr` verbatim and only **rewrite its
info.cfg** to DC1 grammar:

| info.cfg | DC1 | DC2 |
|---|---|---|
| line ends | (none) | `;` |
| VERTEX_ANIME | `1` | `"skin1","skin2"` |
| KEY | `start,end,weight` | `"name",start,end,weight` |
| extra | `ALLOC_DBUFF`,`SHADOW_MOTION` | `POLY_NUM`,`SCALE` |

Converter = regenerate info.cfg in DC1's exact form (mimic `chara/f01a.chr`'s), keeping DC2's values
(model/tex/motion names, KEY frame ranges 4-14/18-28/40-80, BODY_SIZE 18,7,60), then patch that entry's
size + nextStride and leave every other sub-file byte-for-byte. Injected at runtime into read_buffer at
species-8 catch (see fishing-fish-slot-limit memory).

## Converter — BUILT (`dc2/build_priscleen_chr.py`)

Copies every DC2 sub-file (mds/mot/bbp/wgt/img) verbatim, regenerates info.cfg in DC1 grammar,
re-serializes into the shared pack format → `ROMs/dc2_extracted/priscleen/f19a.chr` (131536 B). Keeps
the `f19a` name throughout (Priscleen = DC1 **species id 8**, but `f09a` is RESERVED for a future added
fish — do NOT conflate). Verified: parses with DC1's GetPackFile, valid terminator, mesh decodes 280v/364t. Open items (in-game tests): (1) DC1 info.cfg parser accepts the
regenerated cfg; (2) `VERTEX_ANIME 1` vs DC2's named `skin1/skin2` (may need matching ALLOC_DBUFF); (3)
confirm flap = motion index 1 in f19a.mot (KEYs kept in DC2 order: 0=4-14, 1=18-28, 2=40-80). Next:
bundle f09a.chr as a mod Resource + wire the read_buffer injection for species 8.
