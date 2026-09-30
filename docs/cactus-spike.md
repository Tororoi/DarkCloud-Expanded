# Cactus — "Desert Bloom" (`Weapons/Ungaga/CactusSpike.cs`)

| | |
|---|---|
| trigger | the guard pose (motions 9 / 33, R1) held `Mirage.GuardChargeMs` (250 ms — the Mirage clone's summon time); one per hold; a new hold while one stands takes it down at once and raises it anew at the new spot (as a new Mirage cast replaces the decoy) |
| where | 10 units ahead along Ungaga's yaw, on the floor there (`DungeonFloor.HeightAt` from his height + 20; his height if none), facing his yaw |
| copy | `BladeProp` copy of the equipped c10w13 spawned at 0.1×, grown to 3× (see rise), baked point-up, untinted; c10w13 runs −9.7 … 11.7 along its axis (dungeon `dun\item\main_wep\c10w13.mds`, root `03` identity, mesh on child `c10w13__m`), 6.8 model units of the top exposed (z 4.9 … 11.7 — 20.4 world units; its head is broader than Babel's, which shows 10) |
| rise | in a poof of smoke: e03's cutscene effect `gedit\e03\chara\e228ex.chr` baked onto the dead `dun\effect\zibaku_t` (BorrowedShotBakes; cfg `e228ex.cfg`, one KEY 1–50 at 0.2), borrowed into the second main-character instance (`CactusSpike.WantedShot`, second-effect caves, muzzle motion = KEY 0 so the engine retires it at 49–50), played at 0.6 (absolute; its own is 0.2), scale 0.5, turned to Ungaga's facing about the vertical (its puffs rise along Y and spread in ONE X–Y sheet; turned through its root frame null7's local 3×3 — the sub-shot object's Euler angles +0x60 do not reach a shot effect's draw), at the cactus on the floor; faded by `SubShotFade` (unlit `czappba` frames' constant colour) from frame 32 to nothing at 50. The cactus comes out at 0.1× by the clip's frame 6.5 (5.5 / 0.6 = 9.2 engine frames, 0.15 s) on the blade-fall cave (Babel's ease: v0 = 2D/T, g = v0/T), then grows linearly to 3.4× by frame 10.6 (exactly game frame 16 at 0.6, 0.27 s) and settles back to 3× by frame 12 (game frame 18.3, 0.31 s) — squash and stretch — from the mod's tick (`BladeProp.SetScale`; the peak is always held for at least one tick), its root at floor − (11.7 − 6.8) × scale so the same 6.8 model units stay out; solid and hurting from frame 12; `CodeCaves.BladeSpin` held at 0 — it never turns |
| solid | once risen: the spear-block mailbox (`CodeCaves.SpearBlock`, r = 6, top = floor + 20.4), so enemies and the player slide round it and enemy shots stop at it (the three spear-block caves) |
| damage | Babel's spike rate: every 0.25 s, each live enemy whose nearest hit sphere is within 6 + 2 of the axis takes ⅙ of the weapon's attack, no throw, marked `NoDrainMark` (no weapon HP) |
| life | stands 10 s from the summon, then `BladeProp.Alpha` 1 → 0 over 0.5 s; solid and hurting until it is taken down |

Shares slot 3 / the WeaponCave with the Mirage clone and every other `BladeProp` user (never co-wielded). No ISO change of its own:
it rides the blade-fall, blade-spin and spear-block caves Babel's Spear already needs.

## Super Steve (a Cactus sphere) — the palm

Xiao holding Super Steve with a Cactus SynthSphere has Desert Bloom too (`CactusSpike.Wielded`; the thread starts from Xiao's
Super Steve case in `WeaponThreads`), with the same guard, smoke, timing, squash and stretch (as ratios of full size: 1/30 out,
3.4/3 at the peak), collision and life — but NO damage (it only blocks: Xiao fights from range) — but the copy is Muska Lacka's oasis palm, not Ungaga's weapon:

| | |
|---|---|
| model | georama part 12 "木" (`gedit\e04\mapinfo.cfg` GRD_PARTS 12): sub-file `e04t01` of `gedit\e04\scene.scn` (the scene directory: 0x30-byte entries from 0x10, offset/size at +0x10/+0x14); its first `MDS\0` block up to the second (the collision block `e04t01_a`), 6,864 B, offsets block-relative. Trunk `cyl283__s` (node at z 8, base ring centred there, radius 5.8, leaning to z 13.6, y 0 … 50) and fronds `ha__a7ft` (y 31 … 69.3), upright |
| textures | `e04b04` (trunk; its UVs use the right half) and `e04b10` (fronds) from the building bank `e04b01.img` in `gedit\e04\img.pak` (IM2, 256² 8-bit, 66.6 KB each) — un-swizzled, resampled (nearest texel) to 64², CLUT kept, in a two-entry `IMG` bank of ~10.5 KB. TIM2 total-size field = header + 4 × image, the files' own convention |
| where | the item-model cash (`PalmModel` over `CashModel`, the Bomb's loader generalised), label 30000 (SetCashModel only stores the id). A cash entry's allocator is 0x9C5 units = 40,016 B (dun GameInit): model + built frames + texture bank must fit — the full-size palm textures would not |
| size | 0.5× (69.3 tall at 1× → 34.7), the whole tree out, turned −90° from Xiao's facing (`Form.Turn`); its trunk base (model z 8, turned and scaled with the copy) set on the spot; column r = 4 (the trunk is 5.8 × 0.5 = 2.9; a little wider) |
| textures kept | `PalmModel.KeepTextures` every tick the copy is up (its entries tagged into the clone slot's block 0x1D and re-sent — the Bomb's scheme); released on take-down; `Forget` when the thread ends (a floor change empties the cash) |

No ISO change: the files are read off the ISO at runtime, once a session.
