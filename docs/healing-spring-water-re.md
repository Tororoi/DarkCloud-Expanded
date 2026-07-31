# Healing-spring water rendering RE — and the canal wading plan

*RE'd offline 2026-07-31 (Ghidra static only, nothing live-tested). Goal: make the Queens canal at
low tide render a wading Toan like the dungeon healing springs do — body visible and tinted below
the surface, ripples responding to movement. Addresses are ELF/RAM virtual (game view); mod PINE
view adds 0x20000000.*

## Headline: springs and town water are the SAME renderer

There is one `CWater` class, used by both:

- dungeon springs: `DrawWater__11CDungeonMap` (`0x1c4940`)
- town surfaces:   `DrawWaterSurface__11CEditGround` (`0x1a3360`) — the canal bodies we already
  drive with `CanalTide.PinRipple`

Both draw with **alpha 0x80** (springs: hardcoded in `DrawWater`'s `SetColor(..., 0x80)`; town:
`addiu $t0,$zero,0x80` at `0x182338` in `LoadGroundData`, RGB from the cfg `+0x50/54/58`). Both
sample a **copy of the framebuffer** made immediately before the draw (`MGGetFBuffTex` →
`MGMoveImage` of a 640×224 rect into texture `water_buff` in town / `water` in dungeon; the grid
UV texture is `work`). That fb-copy is the whole "underwater tint + wobble" effect: whatever is
already on screen beneath the surface is re-drawn through the rippling, colour-modulated grid.

## The one real difference: draw order

- **Dungeon `MainDraw`** (dun `0x1dae2a0`): `Draw_MainUnit` (player) → `DrawWaterLing` (rings) →
  splash actor → **fb capture → water surface**. The capture contains Toan's submerged legs, so
  the surface tints/distorts them. Z-write is masked during the surface draw.
- **Town `MainDraw`** (`0x17b7d0`): fb capture → `DrawWaterSurface` (≈`+0x9c4`, gated on a broad
  GameMode list) → … → `EdDrawCharacter` (≈`+0xa2c` label region). The capture never contains
  the player, and Toan is drawn after (and over) the surface.

Consequences, both confirmed against the decomp:

1. **The submerged-invisible quirk is NOT the water** — it's the opaque scene meshes
   (`mizu__a01` walk-view `e03c*`) drawn with Z-write in the early geometry passes; Toan drawn
   later fails Z below the plane.
2. Hiding the mesh alone is not enough for the spring look: Toan would then draw *on top of* the
   surface, dry-looking. The true spring look needs a surface draw **after** the character.

## The ripple system

- `Shake__6CWater` (`0x161370`): adds amplitude at one height-grid cell —
  `*(buf + x*H*4 + y*4) += amp`, cells clamped to [1, dim-2]. CWater struct: `[0]`=W, `[1]`=H,
  `[2]`=current height buffer ptr, `[3]/[4]`=the other two wave buffers.
- `Hamon__6CWater` (`0x1611d0`): classic 3-buffer wave-equation step; speed=`[0x25]²`,
  damping=`[0x26]`.
- Town `StepWater__11CEditGround` (`0x1a3150`) runs Shake (from the up-to-4 `WATER_SHAKE` cfg
  defs, stride 0x10 at body`+0x50`: cellX, cellY, randAmp, baseAmp; negative cell = random) +
  `Hamon` every frame for each active on-screen body. **No player-contact path exists in town.**
- Dungeon player rings = the `WaterLing` system, all in main ELF:
  `CheckHealingWater` (`0x1af3b0`) scans the dungeon tile grid for a water zone
  (room`+0x520` flag, surface Y at `+0x534`, rect at `+0x10/0x40/0x18/0x48`), requires player
  **inside rect AND below surface Y**; writes in-water flag `0x1d564f0`, surface-clamped player
  pos `0x1d564e0`, dun-BSS latch `0x1dc4514`, and on entry fires the splash actor (`0x1eb0020`)
  + SE `0x223`. `StepWaterLing` (`0x1afd60`) spawns up to 6 expanding rings (state @`0x1d56504`,
  stride 0x20: pos, radius, ttl=0x2d) every 20 frames while in water; `DrawWaterLing`
  (`0x1afae0`) draws them as alpha sprites (texture `d00e01`), Z-write masked.
- The healing itself (`HealingWater` `0x1af980`) also animates ambient light blue
  (`setUnitAmbientAnime(60f, 1f, R=0, G=122, B=208)`) — part of the spring "colour wash",
  separate from the surface tint.

## Canal wading plan

1. **Tide-conditional mesh hiding** (C#): at low tide hide the canal scene-mesh frames
   (`mizu__a01`, and the `e03c*` walk-view mesh) via the per-frame draw flag
   (`frame[0xB0] & 1`, the sign-visibility lever); restore when the tide rises. Must compose
   with `CanalTide` — keep updating the mesh CFrame while hidden so nothing pops on return.
2. **Surface-after-character draw** (the only piece needing an EE patch): re-run
   fb-capture + `DrawWaterSurface(pEditGround, NowCamera)` after `EdDrawCharacter` in town
   `MainDraw` — either move the existing block, or (safer, keeps terrain refraction correct
   behind the player) leave the early draw and add a **second** capture+draw via a proven-cave
   stub (Mirage/SignInjector pattern). Patch site: the region between the `EdDrawCharacter` call
   and `LAB_0017c1dc`. Overdraw cost: ≤4 grid bodies, negligible.
   - Cheap first test before building the stub: hide the meshes and eyeball how wrong
     Toan-over-water looks; validates step 1 independently.
3. **Player ripples** (pure data, no patch): C# detects wading (x/z inside the canal
   `WATER_SURFACE` rect, playerY < surface Y) and every ~20 frames writes an amplitude spike
   into the canal body's current height buffer at the player's cell
   (`cell = (pos - cornerA) / (cornerB - cornerA) * (dim-1)`, replicate Shake's clamps +
   `x*H*4 + y*4` indexing). The engine's own StepWater/Hamon propagates and renders it.
   Body = CEditGround (`*0x202A28D8`) `+0x15040 + i*0x3B0`, CWater at body`+0x90`.
4. **Optional flavour**: SetColor the canal body toward spring blue-green while wading
   (bytes body`+0x90..93`, alpha `+0x93`); splash SE `0x223` + ring sprites would need either a
   redraw hook or reusing `EffectWaterSpray` (already called in town MainDraw for map 1's
   waterfall) — defer until the core look works.

## Open items (need the machine / live testing)

- Verify hiding `mizu__a01`/`e03c*` reveals the CWater surface cleanly (no other occluder).
- Confirm `EdDrawCharacter`-site patch addresses against RAM, and that a second
  `MGGetFBuffTex`/`MGMoveImage` per frame is GS-timing safe (it runs per frame in dungeons, so
  expected fine).
- Ring sprites in town would draw before characters via `DrawRipple`'s pass — check whether the
  ring look is even needed once height-field ripples work.
- `0x1dc4514` readers are gp-relative in the dun overlay (xref-invisible); not needed for town.
