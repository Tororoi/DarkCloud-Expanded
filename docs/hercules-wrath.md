# Hercules' Wrath

## The ultimate (`Weapons/Ungaga/HerculesWrath.cs`)

The guard charge's **second level**: level 1 is the Mirage's decoy (inherited from the Mirage, 0.25 s of the guard pose);
keep guarding 5 s more:

| part | how |
|---|---|
| dim | `SolarLighting.BeginDim` / `DimTo(0.35 × charge)` — Big Bang's prime darkness; held while primed |
| body tint | the dim's own white (`SolarLighting.TintEnemies`: enemies and the active character, up to 30 at a dim of 0.5), and the Mirage clone (`CharacterClone.BodyTint`) |
| spear | gold (150, 130, 50) on an exponential ramp (e^(4f)−1)/(e^4−1) to full at 5 s: through `SolarBlade` (the Sun Sword's vtable-copy lever, frame `w09_` of c10w09) — the clone's spear too, since its rigid weapon visuals are SHARED with the real spear's (Lessons) |
| release early | guard let go before level 2: gold cleared, dim ended (the Mirage decoy stays) |
| primed | charge-complete flash; the dim holds until the next swing reaches frame 677 of Ungaga's attack motion (the end of his first swing's 674–677 hit window) for as long as a Mirage decoy stands — a new decoy cast keeps it primed; if the last one dissolves unused, the gold and the dim fall away with its dissolve (`Mirage.DecoyOutroAlpha`) and the strike is gone. The decoy lasts 18 s for Hercules' Wrath (12 for the Mirage), latched at each cast |
| clone | drawn by the chara-slot draw (Draw__12CNPCharacter), which adds a slot's tint to the ambient and then calls Draw__10CCharacter, which adds it again: the chara-slot pass also misses the room's darkening, so the clone takes the scene's light fraction d as its own dim (`CharacterClone.SceneLight`), and every tint written to its slots is t / (1 + d) — (A + x)·d + x = A·d + t, Ungaga's |

The swing plays s78's cutscene sparkle `e508_ex` ON THE MIRAGE (`Mirage.DecoyPosition`); the blast is centred there. The ISO patch copies it onto the dead
`dun\effect\zibaku_r.chr` with `zibaku_r.cfg` exposed (`BorrowedShotBakes`). It is borrowed into the SECOND main-character
instance (`HerculesWrath.WantedShot`, registered in `BorrowedShots.Start`), which the second-effect caves step and draw
while `CodeCaves.SecondEffectLive` is set.

e508_ex KEYs: 0 = 2–16 @0.3, 1 = 21–80 @0.7, 2 = 81–96, 3 = 96–141. Motion 0 plays at its own rate AT THE CLONE'S SPEAR TIP (the clone weapon's `dcol0` world position — 15.66 along c10w09's
blade — re-read each tick; `CharacterClone.WeaponBoneWorld`), then motion 1 — at the tip's ground position as motion 0 ended, at the mirage's height, where the blast lands too — at 0.2 (absolute), faded out from frame 60 to 68
(`SubShotFade`: every mesh of the sparkle is an unlit additive frame, `__czapp…`, drawn in its frame's constant colour — CFrame +0xD0..+0xDC, 128 — which scaled to 0 fades it), where the mod ends the sub-shot (motion 1 runs to 80, unused past 68); motion 2 is never played (still declared as the
config's muzzle motion, so the engine's own retire window, frames 95–96, is never reached). The
sub-shot stays on the mirage.

| frame | what |
|---|---|
| 21 → 36 | `SolarLighting.DimRamp(0.35, u)`: the exponential plunge to black |
| 36 | `BlastFalloff.PlantFalloff(…, reachScale: 2, guardBreak: true)` (each entry crush-marked — `CodeCaves.CrushMark` at +0x9C: the ISO's guard-crush cave passes it through every guard window, and Ungaga's crushing hits bill no weapon HP per hit, the strike billing its 20 once; the thread withdraws the unspent entries, `BlastFalloff.ExpireShells`, which clears the mark with them) (Big Bang's multipliers on rings of 20 / 50 / 80 / 100: 4× / 3× / 2× / 1× attack), `WeaponWhp.Drain(herculeswrath, 20)` (Big Bang's blast cost), `Mirage.Dispel()` (the decoy gone at once, under the flash), `SunSword.ZeusFlash.ArmLighting()` + `SolarLighting.Flash()` — the bolt's white, easing back over 2 s |
| 60 → 68 | the sparkle (`SetFade`) and the spear's gold (held full until here) fade out together; the sub-shot ends at 68 |

Nothing of Ungaga's steps `SolarLighting.Tick`, so the ultimate's loop does, every 16 ms, and restores the light on exit.
If the sparkle is not entered on a floor, the swing blasts at once.

## Super Steve's sphere

Xiao holding Super Steve with a Hercules' Wrath SynthSphere has the ultimate too (`HerculesWrath.Wielded`; the thread
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

## "Tornado Charge Buff" — the Halberd line's charge (`Weapons/Ungaga/Shared/HalberdLineCharge.cs`)

Ungaga's charge effect c10a_ex (the shot `UngagaKey_Play` fires every 30 frames of a held charge from the main-character effect
instance) travels faster and is drawn larger, hit spheres to match, at a form that grows down the build path
(`HalberdLineCharge.TierOf`; each branch at its parent's form). Ungaga's own charge animation is untouched (`AnimFactor` 1). The
thread is `HalberdLineCharge.TornadoChargeBuffEffect`.

| form | weapons | travel | size / hit radius |
|---|---|---|---|
| Halberd | Halberd | 1.4× | 1.1× |
| Scorpion | Scorpion | 1.7× | 1.3× |
| Mirage | Mirage, Cactus | 2.0× | 1.5× |
| Hercules' Wrath | Hercules' Wrath, Terra Sword, Babel's Spear | 2.5× | 1.7× |

Everything is data the ENGINE reads every frame, set once per effect load (`Apply`, when the live instance's config pointer is
not yet ours) and put back when the spear goes (`Restore`, only where the words still hold ours) — nothing is re-asserted per
frame, so nothing races the engine:

- **speed** — the KEY step of each of the effect's motions in its Mot_List (sub-shot CCharacter +0x344 → entries of 0x10: start,
  end, step; c10a_ex's KEYs: rise 5–25, active 30–50, vanish 55–70), which `Step__CCharacter` reads as the play rate whenever the
  rate override is −1 — and `Step__12CSHOT_EFFECT` sets it to −1 on every phase change. Several sub-shots share one list.
- **hit area and travel** — the instance's config pointer (+0) aimed at a copy of the config (`CodeCaves.HerculesCfg`) with every
  phase's radius (+0x28..+0x34) × the form's scale and speed (+0x18..+0x24) × its travel factor. `Step` reads the radius from it
  each frame it plants; `Set__12CSHOT_EFFECT` / `Step` normalise the direction and scale it by the phase's speed when a shot starts
  or changes phase (c10a_ex: speeds 0.2, 0.9, 0.2, 0.2; radii 8, 8, 6, 6). Its flight is `SetWait`'s 45 frames, so a faster shot
  also goes that many times as far. The pointer is written last, so the radii change only once the copy is complete.
- **size** — each sub-shot's CObject scale (+0x90), which the draw folds into the model each frame and `Set__12CSHOT_EFFECT` never
  rewrites.

A floor load or a menu rebuilds the instance with the engine's own config pointer: the next 250 ms tick applies the form again.
Switching to another weapon of the line restores first and applies the new form from scratch.

**Super Steve with a sphere of the line** carries the form on a CHARGED shot (`HalberdLineCharge.DriveSphere`, every dispatch tick
while Super Steve is out): holding the draw 0.5 s charges it (`ChargeTint` ramps, the game's charge-complete flash; the shot's
weapon HP billed as a charged one, `ChargedShotWhp`), and the pellet a charged release fires — the first new slot in the player
shot pool — is drawn the form's size larger (its sprite only: the pellet's hit sphere is the engine's fixed one), has its velocity
multiplied by the travel factor and its damage by 1.5×. An ordinary shot is untouched; pellets already flying at the draw are
never the charged one.

## Floor height (`Dungeon/DungeonFloor.cs`)

Mirrors `setCollisionData` (0x1C0FC0): the 3×3 tiles around the point (gather mode +0xBDEC = 1), or the placed-parts list
(+0x640 active, +0x5A0 position, +0x600 rotation), each part's collision frame placed at tile × 160 and turned r × −90°
(r = tile rotation + part rotation base, wrapped `>3 → −3`, `3 → −1`) with the engine's RotMatrixY (row 0 = (cos, 0, −sin),
row 2 = (sin, 0, cos), row vectors). Each collision node's ready CCPolys (collision object +0x34 array, +0x38 count, stride
0x70) go to the world by local × parent (GetLWMatrix 0x1281B0). Not yet confirmed in game: the rotation sign on a rotated,
non-flat part — the strike logs the floor it found beside the pellet's and the mirage's heights.

## Lessons

- **Tint the spear once.** The Mirage clone's rigid weapon visuals are SHARED with the real spear's (private vtable and all), so the
  blade lever (`SolarBlade`) gilds both. A slot tint written to the clone on top of that doubled the gold on its spear; the clone
  takes only the dim's body white (`CharacterClone.BodyTint`).
