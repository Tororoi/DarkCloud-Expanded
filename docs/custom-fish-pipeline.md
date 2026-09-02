# Custom Fish Pipeline — adding a new fish to Dark Cloud 1

The complete, proven path from a fish model of **any origin** (another game, a custom-authored mesh, a
kitbash) to a fully animated fish in DC1 with all seven motion slots. Shipped end-to-end for Priscleen
(DC2's f19a) on 2026-07-19; the reference implementation lives in `custom_fish/` (see the file map at the end).

Companion references:
- [fish-chr-modding.md](fish-chr-modding.md) — DC1 disc archive + early chr notes
- [priscleen-fish-files.md](priscleen-fish-files.md) — f01a vs f19a laid side by side, field by field
- `dc2/README.md` — the DC2-specific extraction + what DC2 ships vs what DC1 needs

Everything here was verified either by Ghidra decompile of DC1's engine (function addresses given) or
by byte-comparison against the shipped DC1 fish (`chara/f00s.chr`, `f01a.chr`, `f12a.chr`) — each rule
below produced a distinct in-game failure when violated, noted so you can debug from symptoms backward.

---

## 1. What a fish is: the `.chr` pack

A fish is a single `.chr` pack containing six sub-files (lookup is by NAME; order irrelevant):

| sub-file | contents |
|---|---|
| `info.cfg` | text config: model/texture/motion bindings, the 7 KEY ranges, ALLOC_DBUFF list |
| `fNNa.mds` | frame (node) hierarchy + MDT mesh nodes (verts XYZW @ +0x40, VU display list) |
| `fNNa.img` | **IM2** container wrapping a standard TIM2 texture |
| `fNNa.wgt` | vertex→bone weights — and the skinning matrix-chain program (§3) |
| `fNNa.mot` | keyframe tracks: per-bone rotation/translation/scale over one global timeline |
| `fNNa.bbp` | per-frame **LOCAL** rest matrices (0x40 bytes each, one per hierarchy frame) |

Pack container entry: `{name C-str @0; u32 dataOff@0x40 (=0x50); u32 size@0x44; u32 nextStride@0x48
(=0x50+align16(size)); data@0x50}`, terminated by a 0x50-byte zero entry. Serializer:
`build_priscleen_chr.py` (`parse_pack`/`entry`).

**Terminology:** a "frame" in the mds/engine sense is a scene-graph NODE (bone, null, or mesh) —
`CFrame`, stride 0x270 at runtime, record stride 0x70 in the mds (parent i32 @ +0x2C). Animation
timeline frames are a separate axis.

## 2. Hard requirements on the model (any source)

1. **Skeleton**: one hierarchy; parents must precede children in index order (all shipped files do
   this; the wgt chain in §3 depends on it).
2. **Skin frames** (the mesh-carrying nodes) must be parented to the **bone-chain root** with identity
   locals. *(Symptom if not: stiff mesh, then crash — the reset node in §3 can never fire.)*
3. **`.bbp` = local rest matrices**, one 4×4 float per frame, in frame order. Regenerate from the mds
   locals; never trust a foreign source's convention. *(Symptom of world-space matrices: spiky vertex
   explosion — the chain double-applies ancestors.)*
4. **Texture = IM2**. If the source wraps TIM2 differently (DC2 uses IM3), keep the TIM2 verbatim and
   swap the wrapper (`fix_priscleen_texture.im3_to_im2`; entry name must equal the mds texture token).
   *(Symptom: garbled texture + delayed crash — EnterIMGFile walks bogus offsets.)*
5. **≤ 3000 verts per skin** (engine cap, printf-checked), and the whole chr must fit the injection
   budget (§7).
6. `info.cfg` in DC1 grammar with one `ALLOC_DBUFF "<frameName>"` per mesh frame that should animate
   (max 8). A frame with wgt nodes but no ALLOC_DBUFF is a crash risk — never bind weights to a
   non-morphable frame.

## 3. The skinning contract (MotionProc2 @0x148860)

The `.wgt` node list (0x20-byte node headers `{skinFrame, bone, type=20, 0x20, count,
strideToNext(0=last), junk×2}` + count × 0x20 entries `{u32 vertIdx, 0×3, float weight 0-100, 0×3}`)
is **a matrix-chain program executed in file order every frame**, not just a weight table:

1. A node whose `bone == parent(skinFrame)` is the **RESET node** — count 0, FIRST in its skin's run.
   It initializes the working vertex buffer from the bind capture, seeds `FRAME_INF[bone]` with
   identity, and marks the mesh dirty (`frame+0xBA=1`) so the VU data re-uploads.
2. Every other node computes its bone's matrices from `FRAME_INF[parent(bone)]` (must already be set
   by an earlier node) × the bone's current matrix, and × `bbp[bone]` for the rest-pose chain; stores
   them at `FRAME_INF[bone]`; then blends its weighted verts. Ascending bone order satisfies the
   dependency whenever parents have lower indices. Count-0 nodes exist purely to extend the chain.
3. **Blended multi-bone weights are fully supported** (a vert appears under several bones, weights
   summing to 100). DC1's own fish use rigid 100s by choice, not necessity.
4. Multiple skins: each skin's run needs its own reset node; `FRAME_INF` state carries through.

Bind capture happens once in `AnimeDataInit` (@0x1493A0) from the skin frame's visual — which exists
only if the frame is ALLOC_DBUFF'd.

The repacker contains an offline validator replicating these semantics (reset present, chain order
sound) — run it against any new wgt before going in-game.

## 4. `.mot` format + KEY table (MotionProc @0x147D20, SetMotionEX @0x148D00)

- Node header `{frameIdx, 0, type, 32, count, strideToNext(0=last), junk×2}` + count × 0x20 entries
  `{i32 timelineFrame, 0×3, payload @ +0x10}`. Track types: **0 = rotation quat** (w,x,y,z, slerped),
  **1 = scale**, **2 = translation**; also 0xC vertex-pos, 0x1E-0x21 camera, 0x28/0x29 alpha,
  0x32/0x33 visibility.
- Quats are the FULL local rotation in DC handedness — negate x,y,z relative to standard
  (mathutils-style) quaternions.
- All motions share ONE global timeline; the cfg `KEY start, end, speed` lines slice it into the 7
  slots. The third number is the **playback speed** (timeline frames per game frame), not a weight.
- Out-of-range sampling is bounds-guarded (no-op), but ship tracks covering frame 0 through the last
  KEY end, like every original file.
- **Do not animate frame 0** (no shipped file does); fold whole-body transforms into its first child.
- Keys may be sparse — the engine interpolates. RDP keyframe reduction at 0.35° / 0.03-unit tolerance
  keeps ~60% of a dense bake and preserves loop seams (keep segment endpoints).

## 5. The seven motions

DC1 fish play 7 motion slots: 0 normal, 1 battle-genki (lively), 2 battle-yowaki (weak), 3 leap,
4 reeled-in, 5 caught/held, 6 idle. A single catch exercises 4 → 5 → 1. `chara/f00s.chr` is THE
reference — the only DC1 fish with 7 distinct clips; reuse its timeline verbatim:

| slot | frames | len | speed |
|---|---|---|---|
| 0 normal | 10-24 | 15 | 0.25 |
| 1 battle-genki | 30-44 | 15 | 0.50 |
| 2 battle-yowaki | 53-73 | 21 | 0.45 |
| 3 leap | 83-103 | 21 | 0.35 |
| 4 reeled | 113-133 | 21 | 0.50 |
| 5 caught | 143-163 | 21 | 0.45 |
| 6 idle | 173-187 | 15 | 0.20 |

Authoring approach that worked (see `custom_fish/build_priscleen_dc1motions.py`): a procedural generator
emits a self-contained Blender script (rig + weighted meshes + tunable per-motion parameter table);
tune each motion against `custom_fish/blender/f00s_compare.py` (the genuine f00s side-by-side); the Blender
run bakes WYSIWYG per-bone game-space local quat+trans per frame to JSON; the repacker converts that
bake to `.mot` tracks. The generator's mechanisms (carangiform spine wave, time warps, elliptical fin
rowing, jaw floor, hinge-rotated lip, direct f00s retarget) are reusable — remap its GROUPS table to
the new skeleton.

Loop motions must close (whole-cycle frequencies / matching end pose); motion 3 (leap) is the only
non-looper.

## 6. Assembly

`custom_fish/repack_priscleen_dc1motions.py` is the reference assembler: reads the bake JSON + the source chr,
applies every conversion (§2), builds the reduced `.mot`, patches the `.wgt` (and validates §3),
regenerates `.bbp` and `info.cfg`, and writes the final chr to the mod's `Resources/Fish/`.
Env toggles: `STATIC=1` (fully rigid — first-line crash bisect), `SKIN1ONLY=1` (primary skin only).

Bisect ladder when something's wrong in-game: garbled texture → IM2 wrapper; stiff mesh (+ later
crash) → reset node / skin parent; spiky explosion → bbp convention; crash at build with correct
stills → run `STATIC=1` to separate motion data from the skinning path.

## 7. Runtime injection + size budget

DC1 species 8's `chara/f09a.chr` is missing from the disc. `PriscleenFish.cs`: redirect `name_419[8]`
to the `f01a` stand-in (a real file, so the BG load succeeds), then while a species-8 fish is being
reeled, force the BG slot done and overwrite its buffer with our chr bytes (~120-frame window before
the model builds). **HARD CAP: the buffer is sized for the stand-in, f01a.chr = 168,656 bytes** — the
repacker refuses to build past it (tighten keyframe tolerances or pick a bigger stand-in file).
Priscleen ships at ~143 KB (85%).

Still TODO for a fully native new species: `fish_info[]` stat record, spawn-table entry, and the
name `.mes` entry (see the `dc2-archive-and-priscleen` memory).

## 8. New-fish checklist

1. Get the model into `.chr` shape (§1) — mesh in MDT nodes, skeleton with parents-before-children.
2. Map the skeleton: skin frame(s), chain root, anatomy groups (spine stations, fins, jaw).
3. Reparent skins to the root (§2.2); regenerate bbp from locals (§2.3); IM2 texture (§2.4).
4. Build/verify the wgt program (§3) — reset node first, ascending chain, weights sum to 100.
5. Author the 7 motions on f00s's timeline (§5); preview in Blender; bake.
6. Repack (§6); stay under 168,656 bytes (§7); test a catch — it exercises motions 4/5/1.

## 9. Reference implementation file map (`custom_fish/`)

| file | role |
|---|---|
| `../dc2/dc2_archive.py` | DC2 ISO reader (the only genuinely source-specific tool; lives in `dc2/`) |
| `build_blender_rig.py` | shared parsers: `parse_chr`, `skeleton`, `motions` |
| `build_priscleen_native.py` | + `mesh_of`, `parse_wgt`; raw-rig Blender viewer |
| `build_priscleen_dc1motions.py` | motion generator → Blender preview + bake |
| `build_f00s_compare.py` | f00s reference viewer for side-by-side tuning |
| `build_priscleen_chr.py` | pack-container library |
| `fix_priscleen_texture.py` | `im3_to_im2` wrapper swap |
| `repack_priscleen_dc1motions.py` | final assembler + offline validators |
