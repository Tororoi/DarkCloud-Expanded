# custom_fish — tooling for adding fish to Dark Cloud 1

Everything needed to turn a fish model (from DC2, or any other source) into a fully animated DC1 fish.
The **pipeline itself** — DC1's requirements on any fish chr, the skinning contract, .mot facts, the
7-motion authoring workflow, injection budget, new-fish checklist — is documented in
[`docs/custom-fish-pipeline.md`](../docs/custom-fish-pipeline.md). The DC2 side (ISO archive reader,
what DC2 ships vs what DC1 needs) lives in [`dc2/README.md`](../dc2/README.md).

Shipped end-to-end for **Priscleen** (DC2's f19a → DC1 species 8), 2026-07-19.

## The three commands (Priscleen)

1. `python3 custom_fish/build_priscleen_dc1motions.py` → emits `custom_fish/blender/priscleen_dc1motions.py`
   (self-contained Blender script: native rig + meshes + weights + procedural 7-motion generator with a
   tunable per-motion MP table).
2. Run that in Blender (fresh file) to preview/tune; `custom_fish/blender/f00s_compare.py` (from
   `build_f00s_compare.py`) puts the genuine f00s beside it for comparison. The run also writes
   `custom_fish/blender/priscleen_bake.json` — the WYSIWYG per-bone game-space local quat+trans per frame.
3. `python3 custom_fish/repack_priscleen_dc1motions.py` → converts the bake to `.mot` tracks on f00s's
   global timeline, applies every format conversion (info.cfg grammar, IM3→IM2 texture, local-space
   bbp, skin reparent, wgt reset/chain + lip patch), validates offline, and writes the final chr to
   both `ROMs/dc2_extracted/priscleen/` and the mod's `Resources/Fish/f19a.chr`.
   Env toggles: `STATIC=1` (fully rigid, crash bisect), `SKIN1ONLY=1` (body-only skinning).

## File map

| file | role |
|---|---|
| `build_blender_rig.py` | shared parsers: `parse_chr`, `skeleton`, `motions` (.mot) |
| `build_priscleen_native.py` | + `mesh_of`, `parse_wgt`; emits `blender/priscleen_native.py` (raw DC2 rig + its 3 motions) |
| `build_priscleen_dc1motions.py` | THE motion generator → `blender/priscleen_dc1motions.py` (+ bake on Blender run) |
| `build_f00s_compare.py` | reference viewer → `blender/f00s_compare.py` (genuine f00s, 7 real motions) |
| `build_priscleen_chr.py` | pack-container library (`parse_pack`/`entry`/`align`) + legacy 3-motion converter |
| `fix_priscleen_texture.py` | `im3_to_im2` library (+ standalone legacy main) |
| `repack_priscleen_dc1motions.py` | **the final assembler** — bake → chr with all conversions + validators |
| `blender/compare_all.py` | all DC1 fish skeletons + Priscleen side by side (skeleton survey) |
| `blender/priscleen_bake.json` | the exact bake behind the shipped chr (reproducible builds) |

(`dc2_archive.py`, the DC2 ISO reader these scripts import for Priscleen's source data, stays in
`dc2/` — it's the only genuinely DC2-specific tool.)
