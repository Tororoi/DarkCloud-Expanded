# Hercules' Wrath

## The ultimate (`Weapons/Ungaga/HerculesUltimate.cs`)

The guard charge's **second level**: level 1 is the Mirage's decoy (inherited from the Mirage, 0.25 s of the guard pose);
keep guarding 5 s more:

| part | how |
|---|---|
| dim | `SolarLighting.BeginDim` / `DimTo(0.35 × charge)` — Big Bang's prime darkness; held while primed |
| body tint | the dim's own white (`SolarLighting.TintEnemies`: enemies and the active character, up to 30 at a dim of 0.5), and the Mirage clone (`CharacterClone.BodyTint`) |
| spear | gold (150, 130, 50) on an exponential ramp (e^(4f)−1)/(e^4−1) to full at 5 s: the real blade through `SolarBlade` (the Sun Sword's vtable-copy lever, frame `w09_` of c10w09), the clone's weapon slot through `CharacterClone.WeaponTint` |
| release early | guard let go before level 2: gold cleared, dim ended (the Mirage decoy stays) |
| primed | charge-complete flash; the dim holds until the next swing reaches frame 677 of Ungaga's attack motion (the end of his first swing's 674–677 hit window) for as long as a Mirage decoy stands — a new decoy cast keeps it primed; if the last one dissolves unused, the gold and the dim fall away with its dissolve (`Mirage.DecoyOutroAlpha`) and the strike is gone. The decoy lasts 18 s for Hercules' Wrath (12 for the Mirage), latched at each cast |
| clone | drawn by the dungeon's chara-slot loop, which the scene dim does not reach: its dim scalar (+0xCF0) follows the dim instead (`CharacterClone.SceneLight` = 1 − dim × 0.85) |

The swing plays s78's cutscene sparkle `e508_ex` ON THE MIRAGE (`Mirage.DecoyPosition`); the blast is centred there. The ISO patch copies it onto the dead
`dun\effect\zibaku_r.chr` with `zibaku_r.cfg` exposed (`BorrowedShotBakes`). It is borrowed into the SECOND main-character
instance (`HerculesUltimate.WantedShot`, registered in `BorrowedShots.Start`), which the second-effect caves step and draw
while `CodeCaves.SecondEffectLive` is set.

e508_ex KEYs: 0 = 2–16 @0.3, 1 = 21–80 @0.7, 2 = 81–96, 3 = 96–141. Motion 0 plays at its own rate AT THE CLONE'S SPEAR TIP (the clone weapon's `dcol0` world position, re-read each
tick; `CharacterClone.WeaponBoneWorld`), then motion 1 — at the tip's ground position as motion 0 ended, at the mirage's height, where the blast lands too — at 0.2 (absolute), faded out from frame 60 to 68
(the frames' unlit colour scaled to 0), where the mod ends the sub-shot (motion 1 runs to 80, unused past 68); motion 2 is never played (still declared as the
config's muzzle motion, so the engine's own retire window, frames 95–96, is never reached). The
sub-shot stays on the mirage.

| frame | what |
|---|---|
| 21 (motion 1 starts) | the spear's gold fades out over 21 → 36 |
| 21 → 36 | `SolarLighting.DimRamp(0.35, u)`: the exponential plunge to black |
| 36 | `BigBang.PlantFalloff(…, reachScale: 2)` (Big Bang's multipliers on rings of 20 / 50 / 80 / 100: 4× / 3× / 2× / 1× attack), `WeaponWhp.Drain(herculeswrath, 20)` (Big Bang's blast cost), `Mirage.Dispel()` (the decoy gone at once, under the flash), `SunSword.ZeusFlash.ArmLighting()` + `SolarLighting.Flash()` — the bolt's white, easing back over 2 s |

Nothing of Ungaga's steps `SolarLighting.Tick`, so the ultimate's loop does, every 16 ms, and restores the light on exit.
If the sparkle is not entered on a floor, the swing blasts at once.

## The Halberd line's charge (`Weapons/Ungaga/HerculesWrath.cs`)

Ungaga's charge effect c10a_ex at the line's form — see the class summary and the memory note.
