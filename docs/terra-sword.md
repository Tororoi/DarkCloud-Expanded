# Terra Sword — rockfall (`Weapons/Ungaga/TerraSword.cs`)

| | |
|---|---|
| charge | the guard pose (motions 9 / 33, R1) held 5 s primes it; the sword's mesh `c10w08__m` (frame word `c10w`) goes linearly to the green ambient add (80, 150, 20) through the Sun Sword's blade lever (`SolarBlade`), held while primed. Releasing the guard early clears it |
| trigger | the charge completing while locked on (`PlayerAction.LockHeld`, a live slot) drops it at once; with no lock it stays primed (green held) and drops the moment a lock is held. The green holds through the fall and fades out over 0.5 s from the impact; no new charge starts while a rock falls |
| rock | Master Utan's boulder `gedit\s96\chara\iwa.chr` (`IwaModel` over `CashModel`, cash label 30001): `iwa.mds` whole (5,968 B, one rigid mesh, root at the centre, ±20.4) and its one texture `d02b10` (IM2 256² 8-bit) resampled to 128², the Bomb's own size. Loaded into the cash when the sword primes; drawn by `BladeProp` in slot 3 at 1×, facing Ungaga's yaw; `IwaModel.KeepTextures` every tick it is up |
| fall | its bottom 500 above the floor under the target, falling from rest under 180 u/s² (the impact speed of the earlier 300 u drop at 0.6 × the judgement blade's 500, ~424 u/s, kept: g = v²/2h; 0.05 u/frame²; ~2.4 s) on the blade-fall cave. Every tick until it lands the slot's x/z are set to the target's root and the cave's stop height to the floor under it (`DungeonFloor.HeightAt` from root + 20, else the root's height), so it tracks across the ground and over steps. It grows from 0.01× to full size over the first 150 units of the drop (`BladeProp.SetScale` each tick), and its shadow's scale (the shadow frame's TRS scale) grows over the first 300 units. Both are written by a 2 ms loop (`GrowLoop`) that follows the cave's fall height and writes only when a frame has moved it — from the 16 ms tick the steps beat against the ~16.7 ms frame and jittered. A target that dies mid-fall leaves it falling where it is |
| impact | the cave's landed flag: one hit entry per live enemy whose hit spheres are within 26 (reference radius 21 + 5) of the spot, 4× attack, kick type 2 away from the spot at 4.95 / 0.12 (√2 × Big Bang's 3.5: twice its throw distance, which goes as force²/(2·decay)); `syougekiha` (`dun/mainchara/wep_eff`, one KEY 2–31) burst in the second main-character instance at 7× (its reference radius taken as 3 → the rock's 21), rate 0.5 (its own KEY's), played once (muzzle motion = its one clip, so the engine retires it); `GamePad.Rumble(0xE6, 22)`, the engine's own knockdown rumble; 10 base WHP billed once (`WeaponWhp.Drain`) — every hit entry of the ability carries `NoDrainMark` |
| rest | centre at floor + 20.4 − 4 (sunk 4); solid through the spear-block mailbox (r = 20, top = its top); every 1 s each live enemy whose root is inside the rock's 20.4 footprint takes 1× attack, no throw. 10 s from the impact, then `BladeProp.Alpha` 1 → 0 over 0.5 s; a new drop takes a resting rock away first |

Locks on from twice as far (the Mirage line's reach, `Mirage.HoldReach` — see docs/mirage.md). No ISO change of its own: it rides the blade-fall, spear-block, no-drain and second-effect caves. The shockwave is entered
on floor load (`BorrowedShots` provider `TerraSword.WantedShot`), so it plays from the next floor after the sword is first
equipped. Super Steve's Terra sphere is not wired yet (planned: e114kinomi).

## Camera shake — Master Utan's boulder

`gedit\s96\event.stb` (one label, 150) plays the throw as two fixed shots (`_SET_CAMERA_POS`/`_SET_CAMERA_REF`, cmds 422/424)
around ASQ motions; the impact is `_PLAY_SPECIAL_SE 51` with no camera command near it — the cutscene has no shake. The event
VM has no shake command at all (its camera set: `_SET_CAMERA*`, `_ADD_CAMERA_ANGLE/HEIGHT/DIST` 418–420, `_SET_CAMERA_ROLL` 441,
`_CAMERA_STEP`); the only engine "shake" is controller vibration (`CGamePad::SetVibration` 0x12B940, called by
`OpB_DrawProcess` when the player is hit: motor 1 at 0xE6/22 frames for a knockdown, 0xDC/12 for a light hit), which the
impact reuses. A visual shake would be new: a per-frame offset added to the dungeon camera (`NowCamera` 0x202A3498) in the
camera-pin cave at the end of the camera pass, the mod writing a decaying jitter into a mailbox word.

## The rock's shadow (ISO: the rock-shadow cave)

Shadows are per character: a CCharacter's shadow model is its frame at +0xC0 (the weapon packs' `cNNwNNs.mds`, a monster
unit's at +0x1FD90), drawn by `MGDrawShadowFast(frame, planePoint, projDir)` (0x1303B0: the point to RenderInfo +0x2B0
(0x1C757D0), the direction to +0x2C0, shadow-mode word +0x320 = 1, VU program 0x249900, then `MGDraw`) between
`MGBeginDrawShadow`/`MGEndDrawShadow` in `Draw_MainUnitShadow`, which draws only the player (0x1EA1D20, plane = position −
12.8), the enemies (`DrawShadowMonstor`, plane = unit pos − its shadow length +0x1E49C, direction (0, 1, 0, 0) at 0x291810) and
NPCs. ⚠ A lit (rigid) mesh drawn in shadow mode comes out GARBLED: the shadow VU program wants `CVisualShadow` data
(`CreateVUdataShadow`, 0x136530). A shadow model is any MDS loaded by `LoadMDSFile(mds, alloc, 8, 0, 0)` (0x1262B0; kind 8,
0xE with SHADOW_VERTEX_ANIME — `CommandSHADOW_MODEL`), so `IwaModel.ShadowRoot` loads a flat 16-segment disc of the rock's radius (iwa.mds's header and node, its MDT rebuilt by
`MdtCarve`: centre + rim at y 0, the fan wound both ways = 32 triangles) that way into the
rock's own cash allocator (`BtItemCashArea` 0x1F067E0 + i·0x10: base, used +8, cap +0xC in 16-B units — `Alloc` hangs on an
overrun, so 0x140 units free are required first; the log reports the real use). The rock itself as a shadow model (108
triangles, ~11 KB) did not fit: the 128² texture and the model leave ~445 units in the entry. Nobody else places that frame: the mod writes it as `CFrame::SetScale/SetRotation/SetPosition` do (TRS scale +0x210,
Euler +0x230, position +0x220, +0x23C/+0x244 cleared, +0x248 |= 1, DirtyTrs +0x24C = 1, WorldCacheA +0x240 = 0) each tick —
the baked local matrix (+0x1D0) is rebuilt from those, so writing it directly does nothing.

`tools/stubs/rock_shadow.s` (0x1B5460, DebugIfCave.RockShadow) takes the pass's closing `jal MGEndDrawShadow` (dun 0x1DADDD4,
DunPatches): while `CodeCaves.RockShadow` (0x01FAFB80: flag, frame, plane quadword +0x10, direction quadword +0x20) is set it
calls `MGDrawShadowFast` on the named frame, then makes the displaced call. TerraSword writes the shadow frame (on the floor under the rock, + 0.3), the point
(x, floor + 0.3 − 12.8, y, 1) and (0, 1, 0, 0) every tick while the rock falls and rests, and clears the flag as it starts to fade.
The engine always draws a shadow frame AT the character's feet with the point 12.8 (the player) or the species' shadow length
(enemies) BELOW it, and the shadow models are near-flat silhouettes there; the rock's follows that convention. (Frame at the
rock's centre with the point on the floor drew nothing.)
