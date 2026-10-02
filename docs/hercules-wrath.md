# Hercules' Wrath

## The ultimate (`Weapons/Ungaga/HerculesUltimate.cs`)

The guard charge's **second level**: level 1 is the Mirage's decoy (inherited from the Mirage, 0.25 s of the guard pose);
keep guarding 5 s more:

| part | how |
|---|---|
| dim | `SolarLighting.BeginDim` / `DimTo(0.35 × charge)` — Big Bang's prime darkness; held while primed |
| body tint | the dim's own white (`SolarLighting.TintEnemies`: enemies and the active character, up to 30 at a dim of 0.5), and the Mirage clone (`CharacterClone.BodyTint`) |
| spear | gold (150, 130, 50) on an exponential ramp (e^(4f)−1)/(e^4−1) to full at 5 s: through `SolarBlade` (the Sun Sword's vtable-copy lever, frame `w09_` of c10w09) — the clone's spear too, since its rigid weapon visuals are SHARED with the real spear's (a slot tint on top of that doubled the gold) |
| release early | guard let go before level 2: gold cleared, dim ended (the Mirage decoy stays) |
| primed | charge-complete flash; the dim holds until the next swing reaches frame 677 of Ungaga's attack motion (the end of his first swing's 674–677 hit window) for as long as a Mirage decoy stands — a new decoy cast keeps it primed; if the last one dissolves unused, the gold and the dim fall away with its dissolve (`Mirage.DecoyOutroAlpha`) and the strike is gone. The decoy lasts 18 s for Hercules' Wrath (12 for the Mirage), latched at each cast |
| clone | drawn by the chara-slot draw (Draw__12CNPCharacter), which adds a slot's tint to the ambient and then calls Draw__10CCharacter, which adds it again: the chara-slot pass also misses the room's darkening, so the clone takes the scene's light fraction d as its own dim (`CharacterClone.SceneLight`), and every tint written to its slots is t / (1 + d) — (A + x)·d + x = A·d + t, Ungaga's |

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
| 21 → 36 | `SolarLighting.DimRamp(0.35, u)`: the exponential plunge to black |
| 36 | `BigBang.PlantFalloff(…, reachScale: 2, guardBreak: true)` (each entry crush-marked — `CodeCaves.CrushMark` at +0x9C: the ISO's guard-crush cave passes it through every guard window, and Ungaga's crushing hits bill no weapon HP per hit, the strike billing its 20 once; the thread now withdraws the unspent entries, `BigBang.ExpireShells`, which clears the mark with them) (Big Bang's multipliers on rings of 20 / 50 / 80 / 100: 4× / 3× / 2× / 1× attack), `WeaponWhp.Drain(herculeswrath, 20)` (Big Bang's blast cost), `Mirage.Dispel()` (the decoy gone at once, under the flash), `SunSword.ZeusFlash.ArmLighting()` + `SolarLighting.Flash()` — the bolt's white, easing back over 2 s |
| 60 → 68 | the sparkle (`SetFade`) and the spear's gold (held full until here) fade out together; the sub-shot ends at 68 |

Nothing of Ungaga's steps `SolarLighting.Tick`, so the ultimate's loop does, every 16 ms, and restores the light on exit.
If the sparkle is not entered on a floor, the swing blasts at once.

## Super Steve's sphere

Xiao holding Super Steve with a Hercules' Wrath SynthSphere has the ultimate too (`HerculesUltimate.Wielded`; the thread
starts from Xiao's Super Steve case in `WeaponThreads`). Everything is Ungaga's — level 1 is the sphere's Mirage (18 s
decoy), level 2 the 5 s dim and gold, primed while a decoy stands — except:

| | Ungaga | Xiao |
|---|---|---|
| gold on | c10w09 frame `w09_` | the whole Super Steve rig c04w13 (as the Solar Shot whitens it) |
| strike | the swing reaching frame 677 | the FIRST pellet fired while primed (pellets already out at prime are ignored) |
| motion 0 at | the clone's spear tip (`dcol0`) | where that pellet died: the engine's contact point (`PelletContacts`, enemy or wall), else its last position (end of range); one still out after 3 s strikes where it is |
| motion 1 / blast at | tip x/y as motion 0 ends, mirage height | the pellet's death x/y, on the FLOOR there (`DungeonFloor.HeightAt`: the highest upward-facing collision triangle under it at or below the pellet's height + 10; the mirage's height if none is found) |
| blast WHP (20) | Hercules' Wrath | Super Steve |

Once the pellet has left, the strike goes ahead even if the decoy fades during its flight.

## The Halberd line's charge (`Weapons/Ungaga/HerculesWrath.cs`)

Ungaga's charge effect c10a_ex at the line's form — see the class summary and the memory note.

## Floor height (`Dungeon/DungeonFloor.cs`)

Mirrors `setCollisionData` (0x1C0FC0): the 3×3 tiles around the point (gather mode +0xBDEC = 1), or the placed-parts list
(+0x640 active, +0x5A0 position, +0x600 rotation), each part's collision frame placed at tile × 160 and turned r × −90°
(r = tile rotation + part rotation base, wrapped `>3 → −3`, `3 → −1`) with the engine's RotMatrixY (row 0 = (cos, 0, −sin),
row 2 = (sin, 0, cos), row vectors). Each collision node's ready CCPolys (collision object +0x34 array, +0x38 count, stride
0x70) go to the world by local × parent (GetLWMatrix 0x1281B0). Not yet confirmed in game: the rotation sign on a rotated,
non-flat part — the strike logs the floor it found beside the pellet's and the mirage's heights.
