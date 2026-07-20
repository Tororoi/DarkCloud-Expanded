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
container rebuild** — but five sub-files need conversion, each with a DC1-vs-DC2 incompatibility that
was found the hard way (each one alone produced a distinct in-game failure). See below.

---

# DC2 → DC1 fish port  ✅ SHIPPED (Priscleen, 2026-07-19)

The tooling lives in [`custom_fish/`](../custom_fish/README.md) (it's not DC2-specific — only
`dc2_archive.py` here is); the full source-agnostic pipeline is documented in
[`docs/custom-fish-pipeline.md`](../docs/custom-fish-pipeline.md). This README keeps the one genuinely
DC2-specific piece: what DC2 ships vs what DC1 needs.

## The five DC2→DC1 format conversions (all in `custom_fish/repack_priscleen_dc1motions.py`)

| sub-file | DC2 ships | DC1 needs | symptom if skipped |
|---|---|---|---|
| `info.cfg` | `;`-grammar, named VERTEX_ANIME/KEY | DC1 grammar (table above) + one `ALLOC_DBUFF "<skin>"` per animated mesh frame | model doesn't load / mesh static |
| `.img` | **IM3** wrapper around TIM2 | **IM2** wrapper (same TIM2 verbatim; header swap via `fix_priscleen_texture.im3_to_im2`) | garbled texture + delayed crash (EnterIMGFile walks junk offsets) |
| `.bbp` | per-bone **WORLD** rest matrices | per-bone **LOCAL** rest matrices (regenerate from the .mds locals) | spiky vertex explosion (chain double-applies ancestors) |
| `.mds` | skins parented to a bare null | skin frames parented to the **bone-chain root** (patch parent i32 @ frameRec+0x2C; locals are identity so pose unchanged) | stiff mesh + crash (see skinning contract) |
| `.wgt` | nodes for bones 0..N | same, but the list must satisfy the **reset + chain contract** below | stiff mesh + crash |

`.mot` needs no format conversion (same node/keyframe layout both games) but is REPLACED wholesale by
the authored motions (below). The mesh (`.mds` MDT nodes) and the TIM2 pixels port byte-for-byte.

The skinning contract (wgt reset node + matrix chain), `.mot`/KEY facts, the mds-reparent rationale,
the authoring workflow, and the injection budget are all in
[`docs/custom-fish-pipeline.md`](../docs/custom-fish-pipeline.md) — they are DC1 engine rules, not
DC2 specifics.
