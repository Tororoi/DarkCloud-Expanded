# Cactus — "Desert Bloom" (`Weapons/Ungaga/CactusSpike.cs`)

| | |
|---|---|
| trigger | the guard pose (motions 9 / 33, R1) held `Mirage.GuardChargeMs` (250 ms — the Mirage clone's summon time); one per hold; a new hold while one stands takes it down at once and raises it anew at the new spot (as a new Mirage cast replaces the decoy) |
| where | 10 units ahead along Ungaga's yaw, on the floor there (`DungeonFloor.HeightAt` from his height + 20; his height if none), facing his yaw |
| copy | `BladeProp` copy of the equipped c10w13 spawned at 0.1×, grown to 3× (see rise), baked point-up, untinted; c10w13 runs −9.7 … 11.7 along its axis (dungeon `dun\item\main_wep\c10w13.mds`, root `03` identity, mesh on child `c10w13__m`), 6.8 model units of the top exposed (z 4.9 … 11.7 — 20.4 world units; its head is broader than Babel's, which shows 10) |
| rise | in a poof of smoke: e03's cutscene effect `gedit\e03\chara\e228ex.chr` baked onto the dead `dun\effect\zibaku_t` (BorrowedShotBakes; cfg `e228ex.cfg`, one KEY 1–50 at 0.2), borrowed into the second main-character instance (`CactusSpike.WantedShot`, second-effect caves, muzzle motion = KEY 0 so the engine retires it at 49–50), played at 0.6 (absolute; its own is 0.2), scale 0.5, turned to Ungaga's facing about the vertical (its puffs rise along Y and spread in ONE X–Y sheet; turned through its root frame null7's local 3×3 — the sub-shot object's Euler angles +0x60 do not reach a shot effect's draw), at the cactus on the floor; faded by `SubShotFade` (unlit `czappba` frames' constant colour) from frame 32 to nothing at 50. The cactus comes out at 0.1× by the clip's frame 6.5 (5.5 / 0.6 = 9.2 engine frames, 0.15 s) on the blade-fall cave (Babel's ease: v0 = 2D/T, g = v0/T), then grows linearly to 3.4× by frame 10.6 (exactly game frame 16 at 0.6, 0.27 s) and settles back to 3× by frame 12 (game frame 18.3, 0.31 s) — squash and stretch — from the mod's tick (`BladeProp.SetScale`; the peak is always held for at least one tick), its root at floor − (11.7 − 6.8) × scale so the same 6.8 model units stay out; solid and hurting from frame 12; `CodeCaves.BladeSpin` held at 0 — it never turns |
| solid | once risen: the spear-block mailbox (`CodeCaves.SpearBlock`, r = 6, top = floor + 20.4), so enemies and the player slide round it and enemy shots stop at it (the three spear-block caves) |
| damage | Babel's spike rate: every 0.25 s, each live enemy whose nearest hit sphere is within 6 + 2 of the axis takes ⅙ of the weapon's attack, thrown away from the cactus to half the Baselard's distance (kick type 2 from the spot, `Baselard.HalfKickStrength` = 2.02 / √2 ≈ 1.43 at its 0.12 fade: distance ≈ force² / (2·decay) ≈ 8.5 units), marked `NoDrainMark` (no weapon HP) |
| life | stands 10 s from the summon, then `BladeProp.Alpha` 1 → 0 over 0.5 s; solid and hurting until it is taken down |
| shadow | a round shadow on the floor (`GroundShadow`, docs/terra-sword.md) from the summon until the fade starts, the form's radius at the copy's scale as it grows: the cactus 1.8 per 1× (5.4 at 3×: its body's column, the spikes at 2.25–2.75 left out) on the spot; Queens' trees cast none |

Shares slot 3 / the WeaponCave with the Mirage clone and every other `BladeProp` user (never co-wielded). No ISO change of its own:
it rides the blade-fall, blade-spin and spear-block caves Babel's Spear already needs.

## Super Steve (a Cactus sphere) — Queens' trees

Xiao holding Super Steve with a Cactus SynthSphere has Desert Bloom too (`CactusSpike.Wielded`; the thread starts from Xiao's
Super Steve case in `WeaponThreads`), with the same guard, smoke, timing, squash and stretch (as ratios of full size: 1/30 out,
3.4/3 at the peak), collision and life — but NO damage (it only blocks: Xiao fights from range) and NO shadow — and the copy is
Queens' georama trees, whole, not Ungaga's weapon:

| | |
|---|---|
| model | georama part 12 (`gedit\e03\mapinfo.cfg` GRD_PARTS 12): sub-file `e03t01` of `gedit\e03\scene.scn` (the scene directory: 0x30-byte entries from 0x10, offset/size at +0x10/+0x14); its first `MDS\0` block up to the second (the collision block), 16,304 B, offsets block-relative. 16 nodes under the root `null4` (at the origin): trunk `cyl28__s` (0, 8), 51 tall + crown `ha__a7f`; trunk `cyl283__s` (−10, −11), 72.5 tall + crown `ha2__a7f`; grass sprites `k1__a40by` … `k10__a40by`; the grass square `grid__a7f` (y 0.1, x −31 … 19, z −26 … 24, centred (−6, −1)) has its mesh DROPPED in the build (its node-table mesh offset +0x28 zeroed: an empty frame, every index kept) — textured to match Queens' ground, it looked wrong on a dungeon floor |
| copy | 16 nodes are more than BladeProp's 8-node WeaponCave: a tree over 8 nodes is copied into the TOP 24 nodes of the clone's node pool (`CodeCaves.NodePool`, 96 nodes) — free whenever a copy can be up (Spawn refuses while CharacterClone is; the Divine Beast cat fills it from the bottom and never coexists with a copy) |
| textures | `e03b04` (trees + sprites; `e03b10` was the dropped grass square's) from the building bank `e03b01.img` in `gedit\e03\img.pak` (IM2, 256² 8-bit): an 8×8 stand-in goes through SetCashModel and its entry is pointed at the full 256² picture at read buffer +0x320000 (`CashModel.FullTexture`, docs/terra-sword.md) |
| where | the item-model cash (`QueensTrees` over `CashModel`), label 30000. A cash entry's allocator is 0x9C5 units = 40,016 B; the trees take ~37 KB (estimate), so `CashModel` checks an estimate (texture bank + 0x270/node + 0x80/mesh + VU ≈ 16 B × records × streams + 64 per sub-mesh, ×1.15) before SetCashModel and refuses a model that would not fit — an overrun hangs the game in `Alloc` |
| size | 0.7× (74.3 tall at 1× → 52), all of it out, turned +90° from Xiao's facing (the copy's yaw WRAPPED to ±π: the engine's angle-to-matrix diverges past it, so a facing past π/2 plus the quarter turn came out wrong — about a quarter of the summons); the grass square's centre (model −6, −1) set on the spot — the part's root is at the origin and a copy's root translation is the chara slot's position, so the root is offset by that point, turned (RotMatrixY: local +x → (cos, −sin), +z → (sin, cos)) and scaled; column r = 9.5 (the trunks' bases ~15 apart at 0.7×, about the spot) |
| textures kept | `QueensTrees.KeepTextures` every tick the copy is up (its entries tagged into the clone slot's block 0x1D and re-sent); released on take-down; `Forget` when the thread ends |

No ISO change: the files are read off the ISO at runtime, once a session.

**An ally switch ends it at once.** The thread exits the moment the active character changes — checked every tick, a menu open
or not — and its teardown unregisters the copy: chara slot 3 was cloned from that character's objects, which the switch reloads,
and a copy still drawn behind the menu read freed memory and reset the game (a Cactus sphere on Super Steve keeps `Wielded()`
true across the switch). The thread starts again for the new character with its own form. Babel's Spear does the same.
