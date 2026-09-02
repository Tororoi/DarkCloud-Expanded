# Town/Fishing Camera — ELF Port Plan

Porting the from-scratch C# camera (`TownCamera.cs` / `TownCameraCollision.cs`) into native
ELF function(s). Goal: keep our behaviors (occlusion autorotate, spring-arm extension, 3D
overhead min-distance) but run them in-engine so we get frame-perfect smoothness, correct
per-part `_c` collision, and no PINE round-trips — reusing native building blocks wherever
vanilla already does the work.

All addresses are ELF (SCUS_971.11); PINE = ELF | 0x20000000.

---

## 1. How the vanilla camera actually works (from RE)

The town camera is `MainCamera` (`CCameraFollow` @ 0x01D34540). Per frame, from `EdMoveChara`
(0x16A160):

- **`CameraAutoMove(CCameraFollow*, CCPoly*, playerPos, f, f)` @ 0x169B70** — the follow update:
  - `dist = DistVector(playerPos - ref, y=0)` → `SetDistance(dist + off)` — distance is the
    **horizontal** player→ref distance plus an offset (`DAT_002A1870`). *Vanilla distance is 2D.*
  - `SetAngle(atan2f(dx, dz))` then `AddAngle(±speed)` — the bearing tracks **toward the player**,
    eased. Turn speed ramps with `(dist − near)` (÷10 when inside near, ÷15 outside), clamped to 2.0.
  - `if (dist < factor·near) AddHeight(1.0)` — **raises the camera when it gets too close.** Vanilla's
    only "verticality" response, and it's a crude +1/frame nudge.
- **Collision** = `CheckCameraWidth(CCPoly*, count, …)` @ 0x14B830 → `CheckHit(CCPoly*, count, from,
  to, hitOut, …)` @ 0x149D50. Segment-vs-poly; reads the hit poly normal at `poly+0x30`, keeps only
  near-vertical walls (`|normal.y| < 0.5`), and **slides** the camera out by a width. CCPoly stride
  = 0x50 (verts +0x00/+0x10/+0x20, normal +0x30) — same struct GeoramaProbe dumps.
  - ⚠ **Vanilla's collision RESPONSE is weak and NOT reusable.** `CheckCameraWidth` is a lateral
    width-slide bound to the follow path — it does *not* cast player→camera and pull the distance in
    when the camera **rotates into a wall**. That rotate-into-wall pull-in is precisely what our
    camera adds (and why the from-scratch rewrite exists). We reuse only the low-level `CheckHit`
    **ray primitive** + the CCPoly data; the pull-in/occlusion logic stays ours.
- **Smoothing** = `Step__CCameraFollow` @ 0x1247D0 → `Step__CCamera` @ 0x123F30: eases current
  (+0x260/+0x270) toward next (+0x280/+0x290) by the +0x2A8 divisor. We already lean on this.
- **CCPoly source** = `PickUpPoly__CEditGround(CCPoly*, box, i)` @ 0x1A4F50 / `PickUpEditAreaPoly`
  @ 0x1A5170 — the game gathers the camera collision set itself, **already world-placed**.

Key takeaway: vanilla already does follow-angle, distance easing, height-when-close, and
wall-slide collision. Much of our C# reimplemented things the engine has natively.

---

## 2. Our camera vs vanilla — the real differences

| Behavior | Vanilla | Ours | Port disposition |
|---|---|---|---|
| Follow target / look-at | ref = player | same (`+0x2C0`) | **reuse** |
| Distance to player | horizontal (2D) | **3D** (`hypot(reach, rise)`) | **custom** (small) |
| Bearing | tracks toward player, eased | pad + our yaw accumulator | reuse setters, custom driver |
| Occlusion handling | none — only follows behind | **autorotate around walls to keep LOS** | **custom (the headline feature)** |
| Too-close response | `AddHeight(1)` nudge | 3D solve: hug wall, rise so `√(r²+v²)=MinDist` | **custom** |
| Extra distance | never extends past rest | **spring-arm extends** (wider arc) as last resort | **custom** |
| Pull-in on rotate-into-wall | **none** — width-slide only, follow-path bound | ray player→cam, pull distance in | **custom (key value-add)** |
| Wall collision ray | `CheckHit` primitive | our Möller–Trumbore | **reuse `CheckHit` primitive only** |
| Collision data | game's CCPoly (world-correct) | C# static cache of `_c` (mis-placed buildings, stale on georama move) | **reuse native CCPoly → fixes buildings for free** |
| Smoothing | Step ease | Step ease (via next-target) | **reuse** |
| Control decouple | FollowOn | FollowOff patch + C# drive | revisit (see §5) |

The genuinely novel logic is small: **occlusion autorotate**, **spring-arm extension**, and the
**3D overhead min-distance solve**. Everything else is either already native or a thin wrapper.

---

## 3. What we REUSE (native calls)

- **Collision rays (primitive only)** — `CheckHit(CCPoly*, n, from, to, hitOut, 0, mode)` @ 0x149D50
  replaces our ray-triangle loop. `CheckHitVertical` @ 0x14A080 for the vertical/overhead probe.
  Do **not** reuse `CheckCameraWidth` — its follow-path width-slide misses rotate-into-wall pull-in
  (see §1). We call `CheckHit` ourselves from the custom response.
- **CCPoly set** — the game's own camera poly buffer (via `PickUpPoly__CEditGround`), so building
  `_c` is correctly placed and stays valid when a georama part moves. **This alone retires the
  entire `TownCameraCollision` placement saga.**
- **Distance / vector** — `DistVector(float*)` @ 0x123560, `GetDistance__CObject(&)` @ 0x156BB0.
- **Trig** — `_sceVu0ecossin` @ 0x1218D0 (cos+sin), `atan2f` @ 0x11DD40.
- **Apply results to the camera** — `SetAngle` 0x124B20 / `AddAngle` 0x124B50 / `SetHeight`
  0x124BB0 / `AddHeight` 0x124BD0 / `SetDistance` 0x124B70 / `AddDistance` 0x124B90 /
  `SetRef` 0x124310. These are our "verticality + distance + bearing" levers — no field pokes.
- **Smoothing** — leave `Step__CCameraFollow` in place; write our result as the next-target (or
  via the setters that Step consumes) and get 60 Hz easing free.

## 4. What we CUSTOM-author (native)

0. **Pull-in / occlusion response** — cast `CheckHit` player→camera each frame and pull the arm
   distance in when a wall is between them. This is the core thing vanilla lacks: its width-slide
   never pulls in when the camera **rotates** behind a wall. Everything below layers on top.
1. **Occlusion autorotate** — `CheckHit` from ref→camera at yaw and yaw±probe (the fan for
   padding); if blocked and a side is clearer, swing the angle via `AddAngle`, eased by a blend.
2. **Spring-arm extension** — when blocked *and* rotation can't help, grow the arm target toward a
   max via `SetDistance`/`AddDistance`.
3. **3D min-distance overhead** — resolve horizontal reach `r` against the wall (CheckHit) and set
   height so `√(r²+v²) = MinDistance` when horizontal room is short; apply via `SetHeight`.
4. **Blends/eases** (`_rotBlend`, `_extBlend`) — a few floats of state in a scratch global.

## 5. Integration strategy

**Where to run it.** Vanilla's per-frame camera call is `CameraAutoMove` from `EdMoveChara`.
Two viable shapes:

- **(A) Augment vanilla** — undo the FollowOff decouple, let `CameraAutoMove` do follow/distance/
  height, and add a *post-pass* (our cave function) right after it that layers occlusion-autorotate
  + extension + 3D overhead using the setters. Smallest new code; risk = vanilla's angle-follow
  fighting our autorotate (need to let ours win when occluded).
- **(B) Replace `CameraAutoMove`** — redirect its call site (a `jal` in `EdMoveChara`) to a cave
  function that reimplements the ~30-line follow *plus* our behaviors. Full control, ~150–250 MIPS
  instructions, all reusing the native helpers above.

Recommend **(B)** for a clean single owner, but prototype the novel bits as **(A)** first to
de-risk the math against the real CCPoly set.

**How to run native code.** Two mechanisms, each with a hosting requirement:

- **ISO patch + ELF-slack host + baked `jal`.** ISO-patched code is in the ELF image at load, so
  the recompiler compiles it normally (unlike a runtime PINE `j cave`). Cleanest IF we have safe
  dead ELF code to host in — but ⚠ **the `CharaChange` block `0x228BB0`–`0x22A210` is OFF-LIMITS.**
  It's `CharaChangeLoop/Key/Draw__Fv` (in-dungeon party swap); statically it looks dead (no external
  `jal`/data-ptr to its entries — the `0x29F000` pointers are `CharaChangeKey`'s own internal switch
  table), BUT it's already EARMARKED: the `element-switch-menu` feature plans to reclaim it, and it
  may be wanted for the Mirage weapon. Not free real estate. A different verified-dead, unclaimed
  function would be needed before this path is viable — a real zero-xref hunt.
- **CHOSEN (available now): cold cave + vtable `Step` dispatch.** Per `docs/cave-code-execution.md`
  (proven by Mirage `_GET_DISTANCE`): cold-write our function to the clean `0x1F` cave band, and
  reach it by repointing the **`CCameraFollow::Step` vtable slot `0x202A1098`** → cave (a data-driven
  indirect call). No ELF slack needed. Trade-off vs the ELF-slack route: `s5`/`s8` (buffer+count in
  regs at 0x16B5DC) are NOT available at `Step` time, so the cave re-reads the buffer via
  `*(0x2A2388)→base` and scans for the count (both already validated by `CameraCPolyProbe`).

**Hook (chosen, cave+vtable) = wrap `Step`.** Cold-write a cave wrapper and repoint `0x202A1098`:
```
WriteUInt(0x202A1098, caveGuest)     ; CCameraFollow::Step vtable slot → cave (indirect dispatch)
cave:  save ra + a0(=camera this)
       buf = *(0x2A2388)→base;  count = scan(buf)      ; re-read (s5/s8 not live here)
       <occlusion autorotate: CheckHit ref→cam at yaw±probe vs buf/count;  AddAngle toward clearer side>
       restore a0;  jal 0x1247D0 (REAL Step__CCameraFollow — the smoothing);  restore ra;  jr ra
```
Camera = the `this`/a0 at dispatch (or global `*(0x21D19678)`). DROP `PatchDecoupleCamera` (let
vanilla + Step run normally).

*(ELF-slack alternative, if we ever find safe dead code: wrap `jal CheckHitVertical` @0x16B5DC
instead — it runs after `CameraAutoMove` with `s5`=buffer/`s8`=count LIVE in regs, no re-read/scan.)*

## 6. Phased plan

1. **✅ CONFIRMED (2026-07-30) — the CCPoly set is world-correct and has buildings.** Probe
   `CameraCPolyProbe.cs` read the live buffer: WorkBuffer struct ptr @ `gp(0x2A97F0)−0x7468 =
   0x2A2388` (PINE 0x202A2388), base = `*(struct)`. In Queens (east entrance) it held **203 polys
   (155 wall / 48 floor)**, wall Y up to 345 with **139 walls above y=150** (= the 170-level houses)
   — buildings present and correctly placed. Findings:
   - **Local per-player gather** — `PickUpCameraPoly` uses a box around the camera/player, so the
     buffer is small (~203, under the ~400 cap) and always holds the *nearby* occluders. Perfect for
     occlusion rays; no whole-town set needed.
   - **Normals stored UN-normalized** (e.g. `(0,0,-9800)`) — engine `sceVu0Normalize`s on read,
     which is why `CheckHit`/`CheckCameraWidth` normalize. Reusing `CheckHit` handles it for free.
   - Buffer base = `*(0x202A2388)` then `*(that)`; count not stored (scan valid entries).
2. **✅ RE DONE (2026-07-30).** `EdMoveChara` camera block 0x16AF50→0x16B5DC. Registers there:
   **`s5` = CCPoly buffer base, `s8` = poly count** (both callee-saved), `s7` = CEditGround. 4×
   `CameraAutoMove` @0x16B488/4BC/4FC/58C. Budget: `WorkBuffer` cap 2048 blocks ×0x10 /0x50 ≈ **409
   polys** (probe saw 203 — headroom). Hook mechanism found: **vtable Step slot 0x202A1098** (§5).
3. **Prototype — OBSERVABLE MARKER first (de-risk).** ISO-patch `OurCamPostPass` into slack as
   `jal 0x14A080 (real CheckHitVertical) → AddHeight(+2) on MainCamera → jr ra (return real v0)`,
   retarget the 0x16B5DC `jal`. If the camera visibly drifts up with no crash, the wrapper works.
   THEN replace the marker with real logic. (Turn the C# `TownCamera` driver off for this.)
4. **Occlusion autorotate in slack** — `CheckHit` ref→cam at yaw±probe against the live `s5`/`s8`
   buffer; `AddAngle` toward the clearer side.
5. **Add extension + 3D overhead** (`SetDistance`/`SetHeight`), then fold in follow if we want full
   ownership; retire the C# driver + `PatchDecoupleCamera`.
6. **Tune constants** as ISO patches (`camera_near_dist`, `DAT_002A1870`, `DAT_002A188C`, `DAT_002A19F0`).

## 7. Risks / open questions

- **CCPoly budget** — if the camera buffer is capped (~400) and doesn't include enough building
  detail, occlusion tests may be coarse. Mitigation: the vanilla set is what the vanilla camera
  already collides against, so parity is guaranteed; richer needs may require growing the buffer
  (see camera-collision-buffer-and-bake bake tools).
- **Cave execution stability** — mitigated by only calling existing functions from the cave and
  using the proven 0x1F10100 region; still needs a soak test.
- **MIPS authoring effort** — (B) is real assembly work. (A) keeps it minimal to start.
- **Angle-follow conflict** in (A) — vanilla keeps writing SetAngle each frame; our autorotate must
  run after and win, or we suppress vanilla's AddAngle.
- **Fishing camera** — same `CCameraFollow` in `EdMoveChara` (inherits near-pull-in; forces height
  40). Confirm the port covers the fishing sub-mode or gate by mode.

## 8. Net

Going native is *partly* subtraction: the engine provides follow, distance easing,
height-when-close, a `CheckHit` ray primitive, and a world-correct CCPoly set — but its collision
**response** (width-slide) is inadequate, so our pull-in/occlusion logic stays custom. We keep four
custom behaviors — **rotate-into-wall pull-in**, occlusion autorotate, spring-arm extension, 3D
overhead min-distance — expressed as `CheckHit` + `Add/SetAngle/Height/Distance` calls from a cave
function hooked at the `CameraAutoMove` call site. Biggest win: reusing the native CCPoly *data*
retires the C# building-placement problem entirely (independent of vanilla's weak collision response).
