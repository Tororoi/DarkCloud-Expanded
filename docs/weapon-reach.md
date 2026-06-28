# Weapon Reach — research notes

## SOLVED — reach = dcol bone Z in the COMBAT model `.mds` (offline asset edit)

Confirmed 2026-06-28 by parsing `data.dat`. Each weapon has TWO models: `commenu\weapon\cXXwNN.chr`
(menu/display) and **`dun\item\main_wep\cXXwNN.mds` (the in-combat hitbox — the one that sets reach).**
Reach = the **max `dcol` bone Z translation** in that combat `.mds`:

| Weapon | combat .mds dcol Z values | max |
|---|---|---|
| Kitchen Knife (c01w08) | 0.83, 1.85, 4.19 | 4.2 (short) |
| Heaven's Cloud (c01w14) | 1.49, 12.51, 5.93, 10.61 | 12.5 |
| Chronicle Sword (c01w40) | 2.18, 6.19, 14.25, 17.90 | 17.9 (long) |

MDS bone = 112 B: index(4)+size(4)+name[32]+assocMDTOffset(4)+parentIndex(4)+localMatrix(64, row-major).
Translation = matrix row 3 → **x@bone+0x60, y@+0x64, z@+0x68**. The dcol bones are (0,0,Z) for Toan
weapons, so only Z matters. To change reach, scale the Z floats.

**Edit procedure (in-ISO, no repack needed — data.dat is uncompressed & contiguous):**
- data.dat is at ISO byte offset **0x51F000** (ISO9660 LBA 2622) in `Dark Cloud (USA).iso`.
- HC combat `.mds` (`dun\item\main_wep\c01w14.mds`) is at data.dat 0x190A5000; dcol bone bases (data.dat):
  dcol0=0x190A50F0, dcol2=0x190A5160, dcol3=0x190A51D0, dcol1=0x190A5240. **Z = base+0x68.**
- ISO byte offset of a Z = 0x51F000 + (base+0x68). Overwrite the float in place (size unchanged).
- Scripts: `/tmp/dcol_mds.py` (locate bones), `/tmp/iso_patch.py` (copy-safe patch). Test image:
  `~/ROMs/Dark Cloud (USA) reach-test.iso` (HC dcol ×3); original ISO untouched.

Runtime patching is NOT viable (all paths proven dead — see STATUS below). If a `data.dat` repack
step is ever added to the build, this offline edit is the shippable per-weapon reach mechanism.

---



How far a weapon hits ("reach"). Addresses are native (PS2 EE); **PCSX2 = native + 0x20000000**
(`Memory.ToMmu(native)` = `(native & 0x1FFFFFFF) | 0x20000000`). Functions in `0x01DB….`/`0x01DC….`
are in the **dun.bin overlay** (loads at vaddr 0x01DABD00, code at file +0x80). Everything else is
SCUS_971.11. `$gp` = 0x002A97F0 (main) / 0x01E00000 (dun).

> **STATUS (2026-06-28): geometry-via-runtime EXHAUSTED — pivoted to the tolerance lever.**
> Reach = the world position of the weapon model's `dcol` bone. We can find/edit it live, but the
> engine re-poses the bone every frame and we cannot hold an edit at the swing's collision-build frame
> via polling. End-to-end proof this session:
> - Writing the runtime CFrame local matrix (`+0x1d0`/`+0x200`) reverts every frame (rescale counter
>   climbed without bound).
> - A full 31 MB RAM scan for the dcol translation `(2.6789,_,4.0047)` found exactly **two** copies:
>   the runtime CFrame (`0x484xxx`) and one other at native `0x5B7170` (a flat matrix-palette-like
>   entry, rows 0/2 zero, rows 1/3 = the translation; no `dcol` name within 0x220).
> - Scaling `0x5B7170` ×3 **does propagate** to the runtime dcol local-z (4→~10.6) but **oscillates**
>   (races our 400 ms poll) and produced **no actual reach change**.
> So no stable RAM lever exists. The clean fix (bake the dcol bone matrix into `c01w14.chr`) needs a
> `data.dat`/ISO repack pipeline the project doesn't have; patching the code radius constant crashes
> PCSX2 (PINE code-patch, per EnemyModelInjector). **Now testing the one race-free, PINE-safe DATA
> lever: the per-char tolerance `0x21DC1B40` fed to `CheckHitUser` (see Fallback below).**
>
> MDS bone format (Specifications/Models/MDS.md): 112-byte bones = index(4)+size(4)+name(32)+
> assocMDTOffset(4)+parentIndex(4)+localMatrix(64, row-major; translation = row 3). So in the `.chr`
> the dcol reach offset is bone`+0x60/+0x64/+0x68` (x/y/z) — a plain editable float if a repack path
> is ever added. `dcol` is a bone; the collision volume is a sphere at its world pos (radius path),
> NOT the DC_COLLISION_TRIANGLES mesh (that's environment collision).

---

## TL;DR

- The melee hit volume is built at the weapon model's frame named **`dcol`** (Chronicle Sword / some
  weapons have `dcol0..dcol3`; Heaven's Cloud has a single `dcol`). Reach = that frame's **world
  position** relative to the player. NOT a `WeaponList` stat; the per-character value `0x21DC1B40` is a
  small hit *tolerance*, not the per-weapon reach.
- `*(0x21EA1DDC)` is the **CHARACTER** model root CFrame (Toan = `c01d_1_2_1_1`), **not** the weapon.
  The weapon's frames are attached under the hand bone **`jnt18_1`** (a child chain of the char model).
  Walk the CFrame tree (child/next) to reach them.
- Under `jnt18_1` the children are: `eff18`, `r_hand`, **`dcol`** (the hitbox), **`weapon`** (the
  visual blade mesh). `dcol` and `weapon` are **siblings** — scaling one does not affect the other.
- Editing a frame's live local matrix works for exactly one frame, then the engine resets it. **Both**
  the hitbox (`dcol`) and the visual (`weapon`) are reset every frame, so neither B (hitbox) nor C
  (visual) sticks yet. Must patch the upstream source.

---

## Runtime CFrame object layout (CONFIRMED live + from disasm)

From `SearchFrame__6CFrameFPc` (0x128700), `GetLWMatrix__6CFrameFPA4_f` (0x1281b0),
`SetPosition__6CFrameFfff` (0x127e80), `SetScale__6CFrameFfff` (0x127f50),
`SetTransMatrix__6CFrameFPA4_f` (0x128560), `SetReference__6CFrameFP6CFrame` (0x128180).
Object size ≈ **0x270** (body frames were spaced 0x270 apart in a flat scan).

| Offset | Meaning |
|--------|---------|
| `+0x110` | parent **or** reference frame pointer (native) |
| `+0x114` | flag: `1` = `+0x110` is a *reference* (copy its transform), `0` = parent. Set by `SetReference`. |
| `+0x118` | name, NUL-terminated `char[]` (`SearchFrame` compares here) |
| `+0x138` | child pointer (native) |
| `+0x13c` | next/sibling pointer (native) |
| `+0x150` | **WORLD** matrix (4×4, output cache). Translation row at `+0x180/+0x184/+0x188`. Recomputed; do NOT edit. |
| `+0x1d0` | **LOCAL** matrix (4×4). 3×3 at `+0x1d0..`, translation row at **`+0x200/+0x204/+0x208`**. `SetTransMatrix` writes here. **This is what the engine resets every frame.** |
| `+0x210/+0x214/+0x218` | TRS **scale** vec3 (written by `SetScale`) |
| `+0x220/+0x224/+0x228` | TRS **position** vec3 (written by `SetPosition`) |
| `+0x240` | "world matrix valid" flag — set `0` to force world recompute |
| `+0x24c` | "TRS dirty" flag — `SetPosition` sets `1` (rebuild local from TRS) |

Matrix layout: 4×4 row-major floats; **translation = row 3 = matrix `+0x30/+0x34/+0x38`**. So local
matrix translation = `+0x1d0 + 0x30` = `+0x200/4/8`; world = `+0x150 + 0x30` = `+0x180/4/8`.

`GetLWMatrix`: world(`+0x150`) = parentWorld × local(`+0x1d0`); copies `+0x1d0` straight out when
parent is null. It does **not** rebuild `+0x1d0` from TRS.

### Important: local matrix is NOT built from the TRS fields
`dcol`'s TRS position `+0x220` = **0**, yet its local-matrix translation `+0x200` = **(2.68, 0, 4)**.
So the loader bakes the matrix directly into `+0x1d0` and the per-frame writer copies a **stored base
matrix** into `+0x1d0` — it is *not* composing from `+0x220` TRS. (This is why writing `+0x220` +
setting `+0x24c=1` is wrong — it would rebuild `+0x1d0` from the zero TRS and collapse the frame.)

---

## The continuous-reset finding (the current blocker)

Live measurement (`WeaponSpawner` re-applying idempotently every ~400 ms, only when the value had
reverted to base): the re-scale counter climbs on **every** poll —

```
dcol RE-SCALED #57 (z 4->40.05)      weapon RE-SCALED #57 (s0 -0.92->-9.19)
dcol RE-SCALED #58 (z 4->40.05)      weapon RE-SCALED #58 (s0 -0.92->-9.19)
... climbs forever ...
```

i.e. `dcol +0x200` z always reads back **4** (base) and `weapon +0x1d0` scale `s0` always reads back
**-0.92** (base) before each re-scale. → The engine rewrites `+0x1d0` of both frames every frame. In
game you see a 1-frame flash of the giant blade on entry, then normal. (`+0x1d0` writer =
`SetTransMatrix` or a direct `sceVu0CopyMatrix` into `+0x1d0`, driven by the motion/calc pass — see
candidates below.)

The weapon subtree, dumped live (under `jnt18_1` @native 0x483E60):
```
jnt18_1 scaleDiag=(-1,-1,1)        localT=(0,0,0)        worldT=(1597,8.7,1282.8)
  eff18   scaleDiag=(1,1,1)        localT=(2.2,0,0)
  r_hand  scaleDiag=(1,-1.14,-1.29) localT=(0.1,0.12,0.23)
  dcol    scaleDiag=(-1,-1,1)      localT=(2.68,0,4)     <- HITBOX (collision frame)
  weapon  scaleDiag=(-0.92,-1,0.92) localT=(1.12,0.2,0.06) <- VISUAL blade mesh
```

---

## To extend reach — the plan / open work

The fix must change the **source** the per-frame pose copies into `+0x1d0`, not `+0x1d0` itself.
Two candidate sources, in priority order:

1. **A bind/base matrix on the CFrame object.** Likely a second 4×4 matrix slot (candidate `+0x190`,
   the 64 bytes between world `+0x150` and local `+0x1d0`) holding the rest-pose the engine restores
   into `+0x1d0` each frame. **Immediate next step (below)** dumps the fields to find a second copy of
   `(2.68, 0, 4)`.
2. **The raw MDS node in the loaded weapon `.chr` buffer.** The frame's base matrix may live in the
   `c01w14.chr` buffer (node = `name[16]` + matrix `+0x28`, translation `+0x58/+0x5C/+0x60`). NOTE:
   a flat RAM scan around `*(0x21EA1DDC)` only finds the *character* body frames, not the weapon — the
   weapon `.chr` buffer is a separate allocation that must be located (find a pointer to it, or scan
   for the `dcol`/`weapon` node names in the weapon buffer specifically).

Once the source is found: scale its translation (hitbox/`dcol`) and/or 3×3 (visual/`weapon`) and the
per-frame copy will carry the change. For `dcol`, only the translation along the blade matters; for
`weapon`, scaling one axis (the blade length) rather than the uniform 3×3 avoids a fat blade.

### Candidate #1 (on-object bind matrix) — RULED OUT (2026-06-28)
`WeaponSpawner.DumpFields` dumped the full `dcol` CFrame `+0x00..+0x250`. The base translation
`(2.6789, _, 4.0047)` = `0x402B7379 / 0x408026AE` appears **only** at `+0x200/+0x208` — no second copy
anywhere in the object. The candidate `+0x190` slot is an identity-ish matrix (`+0x190,+0x1a4,+0x1b8,
+0x1cc` = 1.0), not the offset. So the per-frame source is **not** on the CFrame object → it's the
upstream Vu1/MDS node (candidate #2). Also confirmed: no back-pointer to a source node in `+0x00..0x150`
(only `+0x110` parent, `+0x13c` next, and an undocumented node ptr at `+0x140`).

### Disasm tooling caveat
`tools/dcdis.py` is capstone MIPS32 — it **cannot decode PS2 EE vector/quadword ops** (`sq`/`lq`/COP2),
which is exactly what the matrix-copy/pose code uses (shows as `.word (undecoded)`). Static tracing of
the per-frame `+0x1d0` writer with it is unreliable. `CopyFrame__FP6CFrameP14CDataAlloc2<1>` (0x127700)
does confirm the runtime tree is a deep copy (node alloc 0x260 + `__as__6CFrameFR6CFrame` 0x128eb0 +
recurse child/next); the MDS template type is `CFrameVu1` (size 0x270, `LoadMDSFileLOD` 0x126eb0).

### Immediate next step (a scanner is already in the code)
`WeaponSpawner.ScanForSourceMatrix` (one-shot, runs when HC equipped) scans EE RAM
`0x20100000..0x22000000` for the translation pattern (x=`0x402B7379` with z=`0x408026AE` 8 bytes later)
and logs every hit, flagging the known runtime CFrame (native `0x4845B0+0x200`). Outcome decides the
path: **>1 full match** → a static source copy exists, scale the CANDIDATE (geometry path viable);
**only the runtime CFrame** → the pose recomputes the matrix, geometry-via-RAM is dead → use the
radius/tolerance fallback below.

### Find the per-frame `+0x1d0` writer (if the bind-matrix dump is inconclusive)
Disassemble the motion/calc pass that re-poses frames and see what it copies into `+0x1d0`:
- `keyCtrl__FffP11MOTION_INFO` (main 0x140570) — applies motion keys.
- `Draw__10CCharacter` (0x139310) / `CommandMOTION__FPPv` (0x13a230) — motion command handling.
- `SetTransMatrix__6CFrameFPA4_f` (0x128560) writes `+0x1d0` then `+0x240=0` — find its callers.

### Fallback (per-character, not per-weapon)
`0x21DC1B40[char]` (6 floats, Toan idx 0, base `[16,14,16,16,18,15]`) is a hit *radius/tolerance*
read by `CCollisionData::CheckHitUser` (0x1B5920) via `BtCheckDamageProc` (dun 0x1DBAFD0). It is
config-like (not reset every frame), so bumping Toan's entry should persist. NOT per-weapon, but could
be **gated on HC being equipped** (set high while HC held, restore otherwise) for a working-ish reach
boost. First verify it actually affects the *normal swing* path (it may only feed the radial/secondary
check, not the primary `dcol` collision).

---

## Full swing → hit flow (primary path)

1. **Swing** — per-character input handler: `ToanKey_Play` (main 0x241690), `UngagaKey_Play`,
   `GoroKey_Play` (`BattleActionPlay_*` for specials).
2. **Build the hit volume** — `SearchFrame(charModel, "dcol"/"dcol0".."dcol3")` (0x128700) →
   `GetWorldPosition` (0x128d60) → `CCollisionData::Set` (0x1B57A0). Active collision object =
   global **`NowColData` @0x202A35E0** (main `$gp−0x6210`). Live player collision ptr `@0x21DF9DF0`.
3. **Per-enemy hit test** — each enemy's `CMonstorUnit::CheckDmg` (0x1D9F10) builds its body sphere
   and tests it vs the weapon collision (reads center `+0x58/+0x60`). On a hit → `SwordDmgCheck1`
   (dun 0x1DB9B30): damage (`BattleSubWeaponDmg`) + spark at the weapon bone (radius 1.5 = 0x3FC00000).

---

## Ruled out
- `WeaponList` byte-diff Kitchen Knife vs Chronicle Sword: only stats differ; no reach field.
- `0x21DC1B40[0]` identical across Kitchen Knife / Chronicle Sword / Heaven's Cloud in-game → not the
  per-weapon reach (it's a per-character tolerance).
- Flat RAM scan of `*(0x21EA1DDC)` window → only character body frames (`chn*`, `jnt*`, `eff*`), no
  `dcol` → weapon frames are an attached subtree, reachable only by tree walk.
- Editing `+0x1d0` (local matrix) or `+0x220` (TRS pos): reset every frame; does not stick.

---

## Test harness (`Dark Cloud Improved Version/WeaponSpawner.cs`, TEMP — remove when done)
- Wired in `Dungeon.cs` (~line 841): `WeaponSpawner.StartReachExtender()` + `OnFloorEntered()`.
- Background thread, every 400 ms: if Toan + Heaven's Cloud (id 271) equipped, reads char-model root
  `*(0x21EA1DDC)`, walks the CFrame tree (child `+0x138` / next `+0x13c`, name `+0x118`), finds the
  `dcol` / `weapon` frames. Helpers: `DumpSubtree`/`DumpFrame` (tree dump), `DumpFields` (field dump),
  `ScaleAxis` (read×mult write). `Multiplier` currently 10.
- CFrame offset constants live in `WeaponAddresses.cs` → `WeaponCollision`.

## Data.dat reference
Weapon models: `commenu/weapon/cXXwNN.chr` (XX char, NN = `WeaponList +0x48` within-char idx). Located
via `data.hed` (80-byte filename stride) + `data.hd2` (16-byte header + 32-byte entries: +0 off, +4
size, +8 sector). Heaven's Cloud = **`c01w14`**; Kitchen Knife = `c01w08` (data.dat 0x171C0800);
Chronicle Sword = `c01w40` (0x17481000).
