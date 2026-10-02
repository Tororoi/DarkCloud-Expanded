# Terra Sword — rockfall (`Weapons/Ungaga/TerraSword.cs`)

| | |
|---|---|
| charge | the guard pose (motions 9 / 33, R1) held 5 s primes it; the sword's mesh `c10w08__m` (frame word `c10w`) goes linearly to the green ambient add (80, 150, 20) through the Sun Sword's blade lever (`SolarBlade`), held while primed. Releasing the guard early clears it |
| trigger | the charge completing while locked on (`PlayerAction.LockHeld`, a live slot) drops it at once; with no lock it stays primed (green held) and drops the moment a lock is held. The green holds through the fall and fades out over 0.5 s from the impact |
| rock | Master Utan's boulder `gedit\s96\chara\iwa.chr` (`IwaModel` over `CashModel`, cash label 30001): `iwa.mds` whole (5,968 B, one rigid mesh, root at the centre, ±20.4) and its one texture `d02b10` (IM2 256² 8-bit): a 128² stand-in goes through SetCashModel (the cash entry's 40,016-byte allocator holds a copy of the bank, and 66.6 KB never fits), then the entry is pointed at the FULL 256² picture kept at read buffer +0x300000 (`CashModel.FullTexture` / `ApplyFullTexture`: the texture manager uploads an entry from its own fields — width +2, height +4, bytes per texel +6, pixels +0x38 (mipmaps +0x3C…), CLUT +0x48, swizzled flag +0x4C, VRAM from TEX0 +0x28 — `ReloadTexture` 0x133070; TEX0 rewritten TBW 4 / TW 8 / TH 8 / CBP = TBP + 0x100, the model's packet swept to match, a 0x120-block window reserved above every block; the data checked every tick a copy is drawn and re-written if overwritten). `FullTexture` takes a list — Super Steve's palm uses it for its two textures at +0x320000, each model its own read-buffer span (`FullOffset`). Loaded into the cash when the sword primes; drawn by `BladeProp` in slot 3 at 1×, facing Ungaga's yaw; `IwaModel.KeepTextures` every tick it is up |
| fall | its bottom 500 above the floor under the target, falling from rest under 180 u/s² (the impact speed of the earlier 300 u drop at 0.6 × the judgement blade's 500, ~424 u/s, kept: g = v²/2h; 0.05 u/frame²; ~2.4 s) on the blade fall's MODE 4 (see below). Every tick until it lands the slot's x/z are set to the target's root and the cave's stop height to the floor under it (`DungeonFloor.HeightAt` from root + 20, else the root's height), so it tracks across the ground and over steps. It grows from 0.01× to full size over the first 150 units of the drop (`BladeProp.SetScale` each tick), and its shadow's scale (the shadow frame's TRS scale) grows over the first 300 units. Both are written by a 2 ms loop (`GrowLoop`) that follows the cave's fall height and writes only when a frame has moved it — from the 16 ms tick the steps beat against the ~16.7 ms frame and jittered. A target that dies mid-fall leaves it falling where it is |
| target dims | the target darkens with the fall — its own lighting, not the scene's: CCharacter `DimOn` (+0xC9C) set and `DimFloor` (+0xCFC) lowered by the growth loop from 1.0 to 0.55 at the impact (`Step__10CCharacter` eases `DimFactor` +0xCF0 toward the floor 0.08 a frame, 0x2A18C0, and back to 1.0 while `DimOn` is 0 — so on a stepped character those two are what to drive), `DimOn` cleared at the impact (the blast throws the target clear; the step eases it back to 1.0 within a few frames); released at once if the target dies or the rock is taken down |
| impact | the cave's landed flag: one hit entry per live enemy whose hit spheres are within 26 (reference radius 21 + 5) of the spot, 4× attack, kick type 2 away from the spot at 4.95 / 0.12 (√2 × Big Bang's 3.5: twice its throw distance, which goes as force²/(2·decay)); `syougekiha` (`dun/mainchara/wep_eff`, one KEY 2–31) burst in the second main-character instance at 7× (its reference radius taken as 3 → the rock's 21), rate 0.5 (its own KEY's), played once (muzzle motion = its one clip, so the engine retires it); `GamePad.Rumble(0xE6, 22)`, the engine's own knockdown rumble; 10 base WHP billed once (`WeaponWhp.Drain`) — every hit entry of the ability carries `NoDrainMark` |
| guards | no guard stops the drop: every hit (the blast, the pinned, the bonk) carries `CodeCaves.CrushMark` ("CRIK", 0x4B495243) at +0x9C, and the ISO's guard-crush cave (`tools/stubs/guard_crush.s`, 0x1B4650 in dead DebugInfomationDraw's tail) — CheckDmg's guard-window hook (main 0x1DAC78) now lands there first — reports "no window" for it, inside CheckDmg on the frame the hit is judged (a mod-side zeroing of the windows is a race a script re-arming them can win); anything else goes on through the rest of the guard gate (docs/guard-gate.md). The mark shares `NoDrainMark`'s high half (0x4B49), and the no-drain caves test only that half, so an Ungaga crushing hit bills no weapon HP |
| hit placement | every hit sits on the victim's largest active hurt sphere placed THIS frame (`BigBang.BodyCentre` skips a sphere more than 80 from its unit — not posed this frame — and falls back to the unit's own position; a stale sphere put the hit where the enemy no longer was, and one Dragon took neither the blast nor the pinned hits). Each Terra hit logs where it was planted against the unit, and the victim's HP |
| rest | centre at floor + 20.4 − 4 (sunk 4); solid through the spear-block mailbox (r = 20, top = its top); every 1 s each live enemy whose root is inside the rock's 20.4 footprint takes 1× attack, no throw. 20 s from the impact, then `BladeProp.Alpha` 1 → 0 over 0.5 s; no new charge starts while a rock falls, rests or fades |

Locks on from twice as far (the Mirage line's reach, `Mirage.HoldReach` — see docs/mirage.md). No ISO change of its own: it rides the blade-fall, spear-block, no-drain and second-effect caves. The shockwave is entered
on floor load (`BorrowedShots` provider `TerraSword.WantedShot`), so it plays from the next floor after the sword is first
equipped.

## Camera shake — Master Utan's boulder

`gedit\s96\event.stb` (one label, 150) plays the throw as two fixed shots (`_SET_CAMERA_POS`/`_SET_CAMERA_REF`, cmds 422/424)
around ASQ motions; the impact is `_PLAY_SPECIAL_SE 51` with no camera command near it — the cutscene has no shake. The event
VM has no shake command at all (its camera set: `_SET_CAMERA*`, `_ADD_CAMERA_ANGLE/HEIGHT/DIST` 418–420, `_SET_CAMERA_ROLL` 441,
`_CAMERA_STEP`); the only engine "shake" is controller vibration (`CGamePad::SetVibration` 0x12B940, called by
`OpB_DrawProcess` when the player is hit: motor 1 at 0xE6/22 frames for a knockdown, 0xDC/12 for a light hit), which the
impact reuses. A visual shake would be new: a per-frame offset added to the dungeon camera (`NowCamera` 0x202A3498) in the
camera-pin cave at the end of the camera pass, the mod writing a decaying jitter into a mailbox word.

## The rock's shadow (ISO: the rock-shadow cave) — `GroundShadow`

Shadows are per character: a CCharacter's shadow model is its frame at +0xC0 (the weapon packs' `cNNwNNs.mds`, a monster
unit's at +0x1FD90), drawn by `MGDrawShadowFast(frame, point, dir)` (0x1303B0: the point to RenderInfo +0x2B0 (0x1C757D0), the
direction to +0x2C0, shadow-mode word +0x320 = 1, VU program 0x249900, then `MGDraw`) between `MGBeginDrawShadow` /
`MGEndDrawShadow` in `Draw_MainUnitShadow` — the player (0x1EA1D20; point = position − 12.8), the enemies
(`DrawShadowMonstor`; point = unit pos − its shadow length +0x1E49C; direction (0, 1, 0, 0) at 0x291810) and NPCs only.

`tools/stubs/rock_shadow.s` (0x1B5460, DebugIfCave.RockShadow) takes the pass's closing `jal MGEndDrawShadow` (dun 0x1DADDD4,
DunPatches): while `CodeCaves.RockShadow` (0x01FAFB80: flag, frame, point quadword +0x10, direction quadword +0x20) is set it
calls `MGDrawShadowFast` on the named frame, then makes the displaced call. `Weapons/GroundShadow.cs` drives it — one shadow at
a time, used by the rock and by Desert Bloom's cactus and palm (docs/cactus-spike.md):
- ⚠ A lit (rigid) mesh drawn in shadow mode comes out GARBLED: the shadow program wants `CVisualShadow` data (`CreateVUdataShadow`
  0x136530). A shadow model is any MDS loaded by `LoadMDSFile(mds, alloc, 8, 0, 0)` (0x1262B0; kind 8, 0xE with
  SHADOW_VERTEX_ANIME — `CommandSHADOW_MODEL`; `CreateVisual` 0x126770 gives both a CVisualShadow, vtable at +8).
- The frame is a flat UNIT disc (16 segments, y 0, the fan wound both ways; iwa.mds's header, node and materials, the MDT rebuilt
  by `MdtCarve`), its TRS scale the radius. It lives in an item-cash entry of its own (label 30002, shared by every user): the disc
  loaded there by SetCashModel as a plain model with an 8×8 stand-in of `d02b10`, then again as the shadow model into the same
  entry's allocator (`CashModel.ShadowRoot`; `BtItemCashArea` 0x1F067E0 + i·0x10: base, used +8, capacity +0xC in 16-B units —
  `Alloc` hangs on an overrun, so the room is checked first).
- ⚠ Not the dungeon read buffer: MENUS load into it — the party screen streams each ally's model (`c04b.chr`, 2.6 MB from buffer
  +0x18D400) over +0x300000 … +0x350000. A disc kept there was overwritten and, drawn behind the menu, reset the game; switching the
  shadow off and reloading after every pause made it flicker on unpause. The cash is the game's own allocation for the floor, so the
  shadow stays up through the pause screen and the item menu; `GroundShadow.Guard()` (every user's loop, every tick, a menu open or
  not) only turns it off in the other non-play states (the character-change screen, events, floor changes). The full textures stay in
  the read buffer (pixel data only) and are re-written whole after any gap in their upkeep (`CashModel.KeepTextures`: > 100 ms).
- Placed as the engine places a character's shadow frame: the TRS fields `CFrame::SetScale/SetRotation/SetPosition` write (scale
  +0x210, Euler +0x230, position +0x220, +0x23C/+0x244 cleared, +0x248 |= 1, DirtyTrs +0x24C = 1, WorldCacheA +0x240 = 0 — the
  baked local matrix +0x1D0 is rebuilt from those, so writing it directly does nothing) AT the floor (+ 0.3), and the point 12.8
  BELOW it (the player's convention). A frame at the rock's centre with the point on the floor drew nothing.
- The rock: shown while it falls (over the target, radius growing with the drop — written by the 2 ms `GrowLoop` through
  `GroundShadow.SetRadius`) and rests; hidden as it starts to fade.

## Super Steve (a Terra Sword sphere) — the nut

Xiao holding Super Steve with a Terra Sword SynthSphere has the same charge (her slingshot greened through `SolarBlade` on
`SolarShot.WeaponModel`), lock-on trigger, drop height, gravity, tracking, growth (to 2×), shadow (its own radius) and darkening
(lighter: to 0.8, not the rock's 0.55),
but what falls is a nut, and it bonks:

| | |
|---|---|
| model | `gedit\s04\chara\e114kinomi.chr` (木の実): `e114kinomi.mds` (6,352 B, one mesh `sphere3`, root at its centre, ±1.9) and `kinomi.img` (one 128² 8-bit texture), whole, in the item-model cash (`KinomiModel`, label 30003) — no full-texture swap needed. 2× (±3.8) |
| bonk | the fall's stop follows the target's root height + its species' authored height (`EnemySpecies.Defaults` `HeightFromRoot`, × the unit's scale) + the nut's radius, so it lands ON the head the frame it reaches it, and the cave turns that landing into the armed hop on the same frame (no pause); the mod sees the cave's "hopped" word and plants the hit: 0.5× the attack, a plain player-hit entry (its own reaction 2) with the vanilla melee kick from the player (`MeleeKickWords` 1.2 / 0.2, type 2) — no blast, no shockwave, no extra WHP bill (the engine bills the hit as any hit) |
| bounce | armed in the cave from the drop on and re-aimed every 0.25 s while the nut falls: up 80 u/s and 45 u/s square to the player's line to the target (a side picked at the drop), down to the floor where it comes out (`DungeonFloor.HeightAt` there); the mod sets 400 u/s² as the fall's g when it sees the hop; then at rest (sunk 0.5), solid (the spear-block column at its radius), harmless (nothing after the bonk: no pinned damage, no blast), 20 s, then a 0.5 s fade. A nut that meets no head (its target gone) just lands and rests |
| stars | `gedit\s04\chara\e114ex.chr` (回る星, two star sprites, one KEY 1–50 at 1.0; its own `e114ex.cfg`, so `BorrowedShots.CustomConfig(5, "e114ex", …, dir: "gedit/s04/chara/")` enters it with no bake) in the second main-character instance, over the bonked enemy: its root + the species' authored height (`EnemySpecies.Defaults` `HeightFromRoot`, × the unit's scale) + 3, carried with the enemy every frame by the follow cave (the enemy's position + that lift into the sub-shot's; a mod tick jittered, and a parent link to the enemy's root failed — every unit of a species shares one model root, so it followed whichever was drawn last), scaled 0.01 → 1.5 over 0.25 s, the clip rewound 1.5 frames before its end (and re-armed if the engine retires it), until the nut is taken down or the enemy dies |
| confusion | the bonked enemy is confused until the nut is taken down (`Confusion`, shared with Babel's Spear — docs/babels-spear.md): no area (it goes after the nearest enemy, or the player when the player is nearest), no tint (the stars mark it), and PROVOKING: an enemy it hits goes after it until its confusion ends, the player nearer or not, and that enemy's swings and shots hurt it alone |

The thread ends on an ally switch (the copy's slot was cloned from the character's objects).

## The blade fall's mode 4 (ISO: the fall-drive cave)

`tools/stubs/fall_drive.s` (0x1B54B0, `DebugIfCave.FallDrive`, 364 B) sits between the blade-fall cave and the follow cave (both of
the fall cave's exits `j` it; it leaves for `tools/stubs/follow.s`, 0x1B4690, then the spin cave), and acts only on flag 4 — modes 1/2/3 (Big Bang, Babel, the
Cactus, Zeus) are untouched. Each dungeon frame, at the end of the camera pass:
- the stop follows a float when `CodeCaves.FallDrive` +0 (a source, guest) is set: stop = *src + the offset at +4 — the nut's
  is its target's root height + the authored head height + the nut's radius (hurt-sphere tops were tried: they stopped it short of
  or above the visible head, e.g. on Statue Dog);
- vy += g, y −= vy, landing at the stop (flag 2), as mode 1 — unless the HOP is armed (`FallDrive` +0xB0): then the same frame
  +0xB4 is set, vy/x-drift/z-drift/stop come from +0xB8..+0xC4, the unit and stop source are cleared, and it stays mode 4;
- slot 3's height is y, and across the ground the followed point's x/z (`BladeFall` +0x14: the target's root) plus the offsets — or, with no point, the slot's own x/z plus the offsets each frame (a drift: the nut's hop);
- five DRIVE ROWS (`FallDrive` +0x10, 0x20 each: dst, count, a, b, lo, hi) write clamp(a + b·y, lo, hi): the drop's scale (slot 3
  CObject scale ×3), the shadow disc's TRS scale (×3) and its two refresh words (DirtyTrs = 1.0f, WorldCacheA = 0), the target's
  `DimFloor` — all linear in the fall height, frame-exact.
The mod sets a fall up once (`StartFall`, the flag last) and watches for flag 2 (the rock's impact, a nut's rest, the hop's end)
and the hopped word (the nut's bonk). The FOLLOW cave (`CodeCaves.Follow` 0x01FAFC90: src, dst, offsets) copies a source vec3 +
offsets to a destination every frame — the stars over the bonked enemy.

Every drop hit zeroes the entry's ability word (+0x6C): no poison, stop, critical, steal or drain from the rock or the nut. The
rock's target sits exactly under the spot (zero direction across the ground → no throw), so a victim within 2 of the spot is
thrown from 1 past it on the side away from the player — toward the player. A consumed entry (inactive) has its CRIK mark cleared the next tick, not after 10 —
the engine never writes +0x9C, so a stale mark would let the engine's next hit in that entry crush a guard. Without the cave (`CaveLive`: its first word) nothing drops and a repatch is logged.

