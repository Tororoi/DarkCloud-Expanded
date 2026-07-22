# rock_obj/ — custom collision rocks for Brownboo fishing

These are the **hand-simplified rock meshes** that compile into `../brownboo_14.bin`, the fishing
collision the mod loads for Brownboo (map 14). They are **original custom geometry** (drawn in
Blender), not game data — safe to distribute, and tracked in git.

## Files
- `iwa01_simple.obj`, `iwa02_simple.obj`, `iwa03_simple.obj` — low-poly collision meshes for the three
  Brownboo pond rocks (`iwa01/02/03`), placed at the in-game rock positions. 49 / 51 / 25 verts.
- `iwa01_simple.mtl`, `iwa02_simple.mtl`, `iwa03_simple.mtl` — Blender's auto-exported material stubs
  (empty; kept only so the `.obj` files open cleanly).

## How they're used
1. `tools/export_rocks_obj.py` extracts the **originals** from `gedit/s04/scene.scn` into the untracked
   `game_data/reference/rock_obj/` (those are direct game extractions — never committed).
2. You simplify each original in Blender and save it here as `<rock>_simple.obj`.
3. `tools/import_rocks_obj.py` (or `tools/assemble_rock_collision.py`) combines these into
   `../brownboo_14.bin` (the `DCFC` collision format the mod reads).

So only the simplified customs live here; the high-poly game extractions stay in `game_data/`.
