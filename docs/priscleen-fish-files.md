# Dark Cloud Fish Files — DC1 (f01a "Bobo") vs Priscleen (f19a, from DC2)

Reference for the Priscleen port. A caught fish in DC1 is a `.chr` pack containing a mesh+skeleton, a
skeletal motion, skinning weights, a texture, a bounding/bind file, and a text config. This documents the
format and lays the two fish side by side to see exactly what differs.

> **Naming note:** DC1's fish #0, `f01a`, is named 「ボーボー」 = **"Bobo"**. When our injection failed we
> saw "Bobo" because we redirect species 8 to `f01a` as a stand-in — so "Bobo" was literally f01a showing
> through.

---

## 1. The `.chr` container

A chain of sub-file entries, ending in a 0x50 zero terminator. Lookup is **by name**, so order doesn't
matter. DC1 and DC2 use the **identical** container.

```
entry:
  +0x00  char[0x40]  sub-file name (e.g. "f01a.mds")   (bytes 0x08..0x3F ignored)
  +0x40  u32         dataOffset  (= 0x50)
  +0x44  u32         size
  +0x48  u32         nextStride  (= 0x50 + align16(size))
  +0x50  ...         data
```

A fish `.chr` holds: `info.cfg`, `<fish>.mds`, `<fish>.img`, `<fish>.mot`, `<fish>.wgt`, `<fish>.bbp`.

---

## 2. The 7 DC1 fish motions

`info.cfg`'s `KEY start,end,weight` lines define motions 0..6 as **frame ranges into the `.mot` timeline**.
The game plays one by index via `SetMotion(fish, N)` (`Step__5CFish` @0x240480).

| # | Frames | Weight | Name (JP) | Meaning | Used when |
|---|--------|--------|-----------|---------|-----------|
| 0 | 10–24  | 0.25 | 通常 | Normal / idle | swimming |
| 1 | 30–44  | 0.50 | バトル（元気） | Battle (energetic) | **hooked / on the line — the "flap"** |
| 2 | 53–73  | 0.45 | バトル（弱気） | Battle (weak) | tiring on the line |
| 3 | 83–103 | 0.35 | 飛びはね | Leaping / jumping | — |
| 4 | 113–133| 0.50 | 釣り上げられ中 | Being reeled up | reeling |
| 5 | 143–163| 0.45 | 釣り上げられた | Reeled up (caught) | — |
| 6 | 173–187| 0.20 | アイドリング | Idling | `Step__5CFish` calls `SetMotion(fish,6)` in one state |

The **caught/held fish plays motion 1** (バトル（元気）), inherited from the battle state — that is the flap
we want. Note `Step__5CFish` also *calls* `SetMotion(fish,6)` in one branch; it's overwritten by motion 1
for the angle-display fish, but any fish with fewer than 7 motions is one code path away from an
out-of-range KEY read.

### Priscleen's own motions (from DC2, only 3)

DC2's `f19a` ships **3** motions, so indices 3–6 don't exist:

| # | Frames | Weight | Name (JP) | Meaning |
|---|--------|--------|-----------|---------|
| 0 | 4–14   | 0.35 | 通常 | Normal |
| 1 | 18–28  | 0.65 | バトル（元気） | Battle (energetic) — **the flap** |
| 2 | 40–80  | 0.45 | 釣れた時 | When caught |

---

## 3. `info.cfg` (full text)

### DC1 `f01a` (works)
```
//ボーボー
IMG 0,"f01a.img"
IMG_END
MATERIAL_ANIME 0
VERTEX_ANIME 1

ALLOC_DBUFF "obj9"
MODEL "f01a.mds"
BODY_SIZE 18,7,60

MOTION 0, "f01a.mot", "f01a.bbp", "f01a.wgt"
SHADOW_MOTION "", "", ""
KEY_START 0
KEY 10, 24, 0.25,  //通常    0
KEY 30, 44, 0.50,  //バトル （元気）   1
KEY 53, 73, 0.45,  //バトル （弱気）   2
KEY 83, 103, 0.35, //飛びはね   3
KEY 113, 133, 0.50,//釣り上げられ中   4
KEY 143, 163, 0.45,//釣り上げられた   5
KEY 173, 187, 0.20,//アイドリング   6
MOTION_END
```

### Priscleen `f19a` — DC2 original grammar (before conversion)
```
v2;
NAME "f19aハグハグ";
IMG 0, "f19a01.img";
IMG_END;
MATERIAL_ANIME 0;
VERTEX_ANIME "skin1", "skin2";      ← names TWO morphable frames
MODEL "f19a.mds";
BODY_SIZE 18.00, 7.00, 60.00;
POLY_NUM 364, -1;
SCALE 1.00, 1.00, 1.00;
MOTION 0, "f19a.mot", "f19a.bbp", "f19a.wgt";
KEY_START;
	KEY "通常", 4, 14, 0.35;
	KEY "バトル（元気）", 18, 28, 0.65;
	KEY "釣れた時", 40, 80, 0.45;
KEY_END;
MOTION_END;
```

**Config grammar differences** — DC1 (no `;`, `VERTEX_ANIME 1` + one `ALLOC_DBUFF "frame"` per morphable
frame) vs DC2 (`;`, `VERTEX_ANIME "skin1","skin2"`, `POLY_NUM`/`SCALE`). Our converter rewrites this to DC1
grammar; the binaries below are copied verbatim.

---

## 4. File-by-file comparison

| sub-file | f01a (DC1 "Bobo") | f19a (Priscleen) | notes |
|----------|-------------------|------------------|-------|
| `info.cfg` | 507 B, 7 motions | 311 B, 3 motions | grammar rewritten to DC1 |
| `.mds` (mesh+skeleton) | 24128 B, **29 frames** | 27616 B, **52 frames** | Priscleen has a bigger skeleton |
| mesh MDTs (vert counts) | 12, 12, **219** (`obj9`) | **224** (`skin1`) + **56** (`skin2`) | f01a = 1 skinned part; Priscleen = 2 |
| `.mot` (skeletal motion) | 62848 B, **13** bone-tracks | 67392 B, **26** bone-tracks | quaternion keyframes |
| `.wgt` (skin weights) | 12064 B, **25** nodes, frame **28** | 14784 B, **94** nodes, frames **49,50** | binds verts→bones |
| `.img` (texture) | 66688 B, **IM2**, 256×256 | 17536 B, **IM2** (converted from IM3) | ✅ fixed |
| `.bbp` (bind/bbox) | 1856 B, **head all-zero** | 3328 B, **head = identity matrices** | ⚠ structurally different |

### `.mds` — mesh + skeleton
- Header `+0x08` = frame (node) count: **f01a = 29, Priscleen = 52**.
- Each frame is a `CFrame` (0x270 B at runtime); bones are `chn*` frames, mesh parts are `obj9` / `skin1`
  / `skin2`. MDT header: `+0x0C` = vertex count, `+0x10` = vertex offset (XYZW floats, stride 0x10),
  `+0x28` = display list. **MDT layout is identical DC1↔DC2** (both decode with the same tool).
- The morphable mesh frame(s) are named by `ALLOC_DBUFF`: f01a = `obj9` (one), Priscleen = `skin1`+`skin2`
  (two).

### `.mot` — skeletal motion (bone rotations)
Linked list of bone-tracks. Node header (0x20): `[0]` = animated frame/bone index, `[4]` = keyframe count,
`[5]` = byte-stride to next node (0 = last). Each keyframe (0x20 B): `[0]` = frame number, `[4..7]` =
quaternion (w, x, y, z). f01a rotates bones progressively around one axis (the body bend).
- f01a: 13 tracks (bones 3–25), varying keyframe counts (up to 187 frames — matches motion 6's range).
- Priscleen: 26 tracks (bones 1–46), uniform 80 keyframes (matches its motion 2 ending at 80). One anomaly:
  frame 1 is animated by **two** tracks.

### `.wgt` — skinning weights
Same linked-list format. Node: `[0]` = skinned mesh frame, `[1]` = bone, `[2]` = type (20), `[4]` = vertex
count. Each 0x20-B entry = `{vertexIndex, weight}` with **weight = 100.0** — binding is **rigid** (one bone
per vertex at 100%), not smooth-blended. Processed parent-first: the runtime skinning (`MotionProc2`
@0x148860) requires the first node to be the mesh frame's **parent** (matrix setup) and later nodes in
hierarchy order.
- f01a: 25 nodes, all binding frame **28** (`obj9`) to bones 1–28.
- Priscleen: 94 nodes, binding frame **49** (`skin1`, 224 v across 47 bones) and **50** (`skin2`, 56 v
  across 47 bones).

### `.img` — texture
DC1 = **IM2** container, DC2 = **IM3** — different outer wrappers, but **both embed a standard TIM2** (same
PS2 pixel/CLUT). Our converter strips IM3 and re-wraps the TIM2 as IM2. ✅ Texture renders correctly.

### `.bbp` — bind/bounding data ⚠
Copied verbatim into the motion data (`CreateAnimeDataEX`). **This is a real structural difference:**
- **f01a.bbp**: 1856 B, header **all zeros** (679/1856 nonzero total).
- **f19a.bbp**: 3328 B, header = **3×4 identity matrices** `(1,0,-0,0, 0,1,0,0, 0,0,1,0, …)` — i.e. per-bone
  bind-pose matrices (2042/3328 nonzero).

Priscleen carries explicit per-bone bind matrices where f01a's are zeroed. This is a prime suspect for the
skinning crash and is the next thing to investigate.

---

## 5. The full DC1 fish family (17 fish) vs Priscleen

Every DC1 fish, measured. `frm` = mds frame count, `MDT vert counts` = mesh parts (the big one is the
skinned body; small ones are static eyes), `DBUFF` = `ALLOC_DBUFF` frames, `mot` = motion count, `trk` =
`.mot` bone-tracks, `wgt` = `.wgt` nodes.

| fish | frm | MDT vert counts | ALLOC_DBUFF | mot | trk | wgt | img | bbp |
|------|-----|-----------------|-------------|-----|-----|-----|-----|-----|
| f01a | 29 | 12, 12, **219** | `obj9` | 7 | 13 | 25 | IM2 | nz=679 |
| f02a | 35 | 9, 9, **251** | `skin` | 7 | 15 | 31 | IM2 | nz=850 |
| f03a | 35 | 8, 8, **228** | `skin` | 7 | 15 | 31 | IM2 | nz=672 |
| f04a | 35 | 20, 20, **208** | `skin` | 7 | 15 | 31 | IM2 | nz=678 |
| f05a | 43 | 9,9,9,9, **251** | `obj12` | 7 | 27 | 37 | IM2 | nz=797 |
| f06a | 65 | 15, 15, **311** | `skin` | 7 | 34 | 61 | IM2 | nz=2001 |
| f07a | 45 | **218** | `skin` | 7 | 15 | 43 | IM2 | nz=1291 |
| f08a | 47 | 6, 6, **352** | `skin` | 7 | 18 | 43 | IM2 | nz=1340 |
| f09a | — | *cut species 8 — this is the slot Priscleen fills* | | | | | | |
| f10a | 59 | **325** | `skin` | 7 | 25 | 58 | IM2 | nz=1973 |
| f11a | 41 | 12, 12, **234** | `skin` | 7 | 16 | 37 | IM2 | nz=1192 |
| f12a | 63 | **273** | `skin` | 7 | 25 | 61 | IM2 | nz=1953 |
| f13a | 29 | 13, 13, **161** | `skin` | 7 | 170 | **0** | IM2 | nz=0 |
| f14a | 39 | **280** | `skin` | 7 | 28 | 37 | IM2 | nz=1111 |
| f15a | 42 | **250** | `skin` | 7 | 276 | **0** | IM2 | nz=0 |
| f16a | 33 | **245** | `skin` | 7 | 19 | 31 | IM2 | nz=871 |
| f17a | 60 | **301** | `skin` | 7 | 23 | 58 | IM2 | nz=1836 |
| f18a | 59 | 15, 15, **422** | `skin` | 7 | 25 | 55 | IM2 | nz=1806 |
| **f19a** | **52** | **224, 56** | **`skin1`,`skin2`** | **3** | **26** | **94** | **IM3** | nz=2042 |
| (Priscleen) | | *two big parts* | *TWO morphable* | *(padded→7)* | | *skins both* | *(→IM2)* | |
| f00s | 35 | 8, 8, **150** | `skin` | 7 | 16 | 31 | IM2 | nz=672 |
| (shadow) | | *the swimming-fish shadow shown before a catch — a generic riggable DC1 fish* | | | | | | |

### Transplant donors (the working approach — §7)

Since a DC1 rig can drive Priscleen's mesh (the crash is Priscleen's *rig*, not its mesh), we transplant
Priscleen's mesh onto a DC1 fish's rig and re-weight. A good donor is **single-part** (no separate eye
meshes to leave floating), has its body MDT as the **last block** (transplant with no offset shifting), and
— most important for deformation quality — has the **finest skeleton (most spine bones)**, since more bones
= smoother bending and a stable face. Ranked:

| donor | body verts | skin bones | note |
|-------|-----------|-----------|------|
| **f12a** | 273 | **20** | best — finest skeleton, single part, > Priscleen's verts |
| f10a / f17a | 325 / 301 | 19 | also excellent |
| f07a | 218 | 14 | good (tried) |
| f14a | 280 | 12 | ok |
| f16a | 245 | 10 | coarser |
| f03a | 228 | 8 | avoid — coarse **and** has separate eye meshes |

**What this tells us:**
- **⚠ Priscleen is the ONLY fish with TWO morphable frames.** All 17 DC1 fish declare exactly **one**
  `ALLOC_DBUFF` — the single big body mesh — and their small parts (eyes) are static. Priscleen splits its
  body into `skin1` (224 v) + `skin2` (56 v) and makes **both** morphable, and its `.wgt` skins **both**
  frames (49 and 50). DC1's skinning path (`MotionProc2` + the global `def_vrtx`/`Bone_Matrix` buffers)
  appears built around **one** skinned frame per model. **This is the prime crash suspect.**
- `.bbp` with data is **normal** — 15/17 fish have it; only f13a/f15a are zero (and those use a weightless
  animation: 170/276 tracks, **0** wgt nodes). So the `.bbp` is *not* the outlier. (Earlier lead cleared.)
- All DC1 fish have **7 motions** and **IM2** textures. Priscleen had 3 motions (now padded to 7) and IM3
  (now converted to IM2).
- Priscleen's frame count (52) and per-part vertex counts (224) are well within the normal DC1 range — not
  outliers.

**Next fix to try:** make Priscleen structurally match DC1 — **one** morphable frame. Keep `skin1` (body)
morphable, make `skin2` static, and **remove `skin2`'s nodes from the `.wgt`** so DC1 never tries to skin a
non-morphable frame (the earlier `skin1`-only test still crashed because the unfiltered `.wgt` skinned the
now-static `skin2`, calling the wrong visual method on it). `skin2` (the smaller part, likely fins) would
not flex, but the body would.

## 6. Modified Priscleen — exactly what we ship

The runtime `.chr` (`Dark Cloud Improved Version/Resources/Fish/f19a.chr`) is built by two scripts:
`dc2/build_priscleen_chr.py` (DC2 → DC1 container + info.cfg) then `dc2/fix_priscleen_texture.py`
(IM3 → IM2 texture). Per sub-file:

| sub-file | source | change |
|----------|--------|--------|
| `info.cfg` | regenerated | **rewritten to DC1 grammar** (see below) |
| `f19a.mds` | DC2 verbatim | none — mesh/skeleton copied as-is |
| `f19a01.img` | DC2, converted | **IM3 → IM2** (DC2's TIM2 re-wrapped in a DC1 IM2 header; no pixel change) |
| `f19a.mot` | DC2 verbatim | none — motion copied as-is |
| `f19a.wgt` | DC2 verbatim | none — weights copied as-is |
| `f19a.bbp` | DC2 verbatim | none |

**Generated `info.cfg` (current):**
```
IMG 0,"f19a01.img"
IMG_END
MATERIAL_ANIME 0
VERTEX_ANIME 1
ALLOC_DBUFF "skin1"          ← two morphable frames (DC1 fish declare ONE — suspect)
ALLOC_DBUFF "skin2"
MODEL "f19a.mds"
BODY_SIZE 18,7,60
MOTION 0, "f19a.mot", "f19a.bbp", "f19a.wgt"
SHADOW_MOTION "", "", ""
KEY_START 0
KEY  4, 14, 0.35   //dc1-motion-0  (Priscleen 通常)
KEY 18, 28, 0.65   //dc1-motion-1  (Priscleen バトル)
KEY 18, 28, 0.65   //dc1-motion-2  (→ バトル)
KEY 40, 80, 0.45   //dc1-motion-3  (→ 釣れた時)
KEY 40, 80, 0.45   //dc1-motion-4  (→ 釣れた時)
KEY 40, 80, 0.45   //dc1-motion-5  (釣れた時 — the HELD-FISH motion the game plays)
KEY  4, 14, 0.35   //dc1-motion-6  (→ 通常)
MOTION_END
```
Priscleen's 3 DC2 motions are mapped onto DC1's 7 slots by meaning so every `SetMotion(0..6)` is in-range;
motion **5** (the held-fish pose) routes to Priscleen's 釣れた時 range (frames 40–80).

**Runtime injection** (`PriscleenFish.cs`, Brownboo only): species 8's model path (`name_419[8]`) is
redirected to the on-disc stand-in `f01a`, and when a species-8 fish is reeled the finished BG-read buffer
is overwritten with this `f19a.chr` (the slot forced "done" to pre-empt the disc read). `ForceAllSpecies8`
stamps every Brownboo fish as species 8 for testing.

## 7. Crash status (native flap)

The caught fish should play motion 1 by deforming its skinned mesh. Enabling that (`ALLOC_DBUFF` the mesh
frames) crashes PCSX2 ~a few seconds into the held-fish display. Tests so far:

| test | result |
|------|--------|
| Texture IM3→IM2 | ✅ fixed (model + texture correct, static) |
| Frame indices in `.mot`/`.wgt` vs frame count (52) | ✅ all in range — not an OOB index |
| Motion index the caught fish uses | resolves to motion **1** (valid) |
| `ALLOC_DBUFF skin1`+`skin2` (full DC2 rig) | ❌ crash |
| `ALLOC_DBUFF skin1` only (one part, like f01a) | ❌ crash |
| Hand-authored 1-bone `.mot`+`.wgt` | ❌ crash — **but the `.wgt` was invalid** (single node, skipped the
  parent-setup that `MotionProc2` requires → uninitialized matrix) |
| Original `.wgt` + **all motion zeroed to identity** | ❌ crash — but the KEY table still had only 3 entries |
| KEY table **padded to 7** motions (motion 5 → 40–80) | ❌ crash |
| `.bbp` — compared across all 17 DC1 fish | ✅ ruled out — 15/17 DC1 fish have non-zero `.bbp` too |

**Conclusion:** the crash is *structural*, and the family comparison (§5) isolates the outlier: **Priscleen
is the only fish with TWO morphable frames** (`skin1`+`skin2`), and its `.wgt` skins both. DC1's skinning
path looks built around a single skinned frame per model.

**Next test (untested):** one morphable frame only — `ALLOC_DBUFF "skin1"` **and a `.wgt` filtered to just
`skin1`'s nodes** (so DC1 never skins the now-static `skin2`). This makes Priscleen structurally match every
DC1 fish. If it still crashes, the remaining path is combining `skin1`+`skin2` into one mesh frame (mds
surgery) so all 280 verts live in a single morphable part.
