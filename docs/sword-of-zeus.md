# Sword of Zeus — lightning

The Sword of Zeus is the Sun Sword line with lightning. It inherits Solar Harvest and Solar Flash (docs/solar-flash.md)
and adds the BOLT: Toan's whirlwind IS a lightning strike, and the primed flash brings that strike down on enemies — on the
locked target with every swing of the combo while locked on, or on each of the nearest six within 300 units when not. A
bolt touches nothing itself; its damage is Big Bang's falloff blast at its foot at half Big Bang's steps. A full charge
attack is a bolt as well: the room darkens as the meter fills, the level-2 release plays the lunge, and the bolt comes
down ahead of Toan as the lunge ends. Super Steve carrying a Sword of Zeus sphere fires the same bolt from Xiao's
slingshot (`ZeusShot`, below).

Code: `Weapons/Toan/SwordOfZeus.cs` (`SwordOfZeus.LightningEffect`, a thread per equip from `WeaponThreads`, 30 ms
ticks, started beside `SunSword.SolarHarvestEffect` and `SunSword.SolarFlashEffect(SunSword.ZeusFlash)`) and
`Weapons/Xiao/ZeusShot.cs` (`ZeusShot.Drive`, ticked from `SuperSteve` while the sphere is on). `BorrowedShots.Start`
registers both `WantedShot`s.

## The flash it inherits

`SunSword.ZeusFlash` is the Sun Sword's `SolarProfile` on `Items.swordofzeus` (blade `c01w39`): fog 0.8, light
(228, 240, 255) and fog (238, 246, 255) — an electric white toward blue — prime dim 0.5 (darker than Big Bang's 0.35),
ease 2 s back to the floor's own light, and no primed tint on Toan: the flash itself costs nothing and carries no hit of
its own; each bolt does (`StrikeWhp`). The blinding, the guard break and the lighting pipeline are Solar Flash's
(docs/solar-flash.md). Judgement, the lock-on reach and the falloff blast are Big Bang's (`Weapons/Toan/BigBang.cs`).

## The bolt

| piece | how |
|---|---|
| model | `gedit/s99/chara/lightning.chr`, the scene actor the Divine Beast Cave cutscene strikes the cat with (`dun/script/d01/event.stb` label 90). Loaded by path from its own directory through `BorrowedShots.CustomConfig(5, "lightning", muzzleMotion: 1, fly/impact/expire −1, dir: "gedit/s99/chara/")` — stock config 5 supplies the shape, the name and motions are replaced. The loader takes any container whose cfg record carries its name |
| why it fires on the spin | Toan's whirlwind visual is the main-character effect instance's sub-shot, so seeding that instance with lightning.chr REPLACES the whirl with the bolt and the engine fires it on the spin by itself (Big Bang borrows explosion.chr the same way). While the instance holds it (`SwordOfZeus.LightningSeeded`) the stock whirl scaler in `Weapons.cs` stands down: the model it validates ("kiru" + "fkiri") is not the one loaded |
| motions | two KEYs over the same frames 10–30: KEY 0 at speed 0 (a held pose, 止め) and KEY 1 at 0.2 — the strike. Only the MUZZLE phase names a motion, as the whirl's own shape does (c01_fuusya is "muzzle motion 0, nothing after"). Every phase radius is zeroed: the bolt itself hits nothing |
| where it stands | the actor's cards rise from its origin (nodes at +4.5 and +10 up), so it stands ON the ground under the target rather than centred on the body. A strike is played at the enemy's own position (its ground point) |
| scale | `LightningScale` 10 on the root frame's local 3×3 (`MaintainScale`, every tick), translation left anchored, the frame's world cache (`CFrameVu1.WorldCacheA`) zeroed after a write. ONE scale path for every bolt, strike or whirl: a VERTEX_ANIME mesh is only transformed by its root's local matrix, the way the stock whirl and Big Bang's explosion.chr are held. The authored bind (the identity) is read once from the first root seen at 1×; a root already scaled is a previous hold's, not the bind |
| root test | lightning.mds's root is `null2`, matched as the word "null" + the byte '2' (`IsBoltRoot`) |
| sound | SE 390, the cutscene's thunderclap, at volume 90 (`SeSeq.Play`) |
| damage | `BlastFalloff.PlantFalloff(x, h, y, noKickSlot: the enemy under it, damageScale: 0.5, guardBreak: true)`: Big Bang's steps are 50 / 40 / 25 / 10 units → 1 / 2 / 3 / 4 × attack, so a bolt does ½ … 2 × attack by distance, elementless. The enemy under the bolt takes the blast where it stands with no shove; nobody is turned to face it; the blast crushes any guard (the ISO's guard gate) |
| cost | `StrikeWhp` 10 weapon HP per bolt through `WeaponWhp.Drain` — the engine's own drain takes it, as a landed hit's is, before the weapon's Endurance scales it down. A volley (`StrikeNearest`) bills ONCE however many bolts |
| reach | not locked on: the nearest `MaxStrikes` 6 live enemies within `StrikeReach` 300 of the active character (the flash's radius, about the draw distance), nearest first, as far as the instance has sub-shots free |
| judgement | `SwordOfZeus.Judgement`, a `JudgementBlade.JudgementOwner`: the blade hung over the locked target while primed (`JudgementBlade.JudgementTick`), its own glow disc (`ToanGlowBakes.ZeusName`, `toanglowz`), the fall darkening from the Zeus prime dim, no enemy turned to watch it (`Redirect = false`), let go as the primed combo's FIRST swing begins and paced so it is in the ground to the HILT (`ToTheHilt`) at the swing's hit frame; there it is gone at once and `Land` brings the bolt down on the target |
| lock-on | `ToanLockOn.HoldReach`: Big Bang's lock-on reach, released on unequip. `CodeCaves.NameHide` is opened (0) when the thread starts |

## The charge attack

`SwordOfZeus.ChargeTick`, every tick while the sword is out and the game is not paused.

- **No level 2 for the game.** `PlayerAction.WhirlwindUnlock` (`DngStatusData` + 0x4324 = 0x21CDD870, save data) is
  zeroed for the length of the wind-up and put back the tick the release has been read, so the game never reaches its own
  level 2 and every release is the LUNGE.
- **The sword's own level 2.** The room darkens from the moment he starts charging, as the guard charge darkens it: half the
  Zeus prime dim as the meter climbs from `ChargeDimFrom` 1.0 (ToanKey_On resets it to 1.0) to `ChargeLevel1` 1.5 (the
  game's lunge threshold), the rest over `ChargeLevel2Seconds` 3.0 held past it. The blade whitens across the wind-up the
  way the guard charge whitens it, so the copy made at level 2 carries the charge's tint. A blinding's dim already on when
  the charge began is the floor under the charge's dim (never lifted while charging). At the end of the hold the stock
  charge-complete flash fires on him, the release is flagged as a bolt and `CodeCaves.LungeGravityExtra` (0x21FAF8A0)
  is set to `LungeHigher` 0.5: the ISO's lunge-gravity caves (`ElfToanMeleePatches.PatchLungeGravity`, DebugIfCave
  `LungeGravitySeed` / `LungeGravityStep`) scale the parabola's gravity by (1 + extra), so the lunge jumps 1.5× as high in
  the same frames. 0 is vanilla, and it is written back the moment the bolt fires or the charge stands down.
- **The blade.** From level 1 the judgement blade hangs `ChargeHoverHeight` 45 over the spot the bolt will land — the
  locked target, followed, or `ChargeBoltAhead` 40 units ahead of Toan, moving with him while he holds the charge
  (`ChargeAim`, `JudgementBlade.PointHover` with `ridesPlayer`) — fading in over the hold to solid at level 2 (`JudgementBlade.PointAlpha`).
  As he lunges the blade ahead of him stops riding him (`JudgementBlade.PointFreeze`): the bolt lands under where it hangs.
- **The drop.** The lunge is the same length every time: `ChargeLungeFrames` 38 from its dash state to its END state
  (`PlayerAction.ActionLungeEnd` 0x11: clip 17 from frame 196; the five lunge states are `PlayerAction.InLunge`). The
  blade is let go `ChargeDropAfterDash` = (38 − `ChargeFallFrames` 19) / 60 s after the dash begins (or the tick the END
  state comes up, if first) and falls for `ChargeFallSeconds` 19 / 60 s, so the hilt reaches the ground as the END state
  comes up. That tick the bolt comes down where the blade fell (the locked target's live position, else the frozen spot)
  with a strike's blast, and `SunSword.StrikeFlash(ZeusFlash)` goes off — the same white, Toan's pulse, the two-second ease
  and the 5 s blinding as every strike. The camera is pinned at its height through the lunge (`CameraHold`) and
  released with the bolt.
- **The plunge.** From the drop to the bolt the room goes to black: the falling blade drives it (BigBang's fall thread,
  the same ramp) or, with no blade in the air, the clock does over `ChargeFallSeconds` (`SolarLighting.DimRamp`).
- **Short of level 2.** A level-1 lunge is nothing more than the lunge: the dim lifts, the blade (not yet let go) fades
  out. Charging stands aside entirely while Solar Flash owns the blade (`SunSword.FlashArmed`).

The log prints the gap between the hilt reaching the ground and the bolt (`charge bolt N ms after the blade reached the
ground`) or `charge bolt with the blade still falling — ChargeFallSeconds is too long`.

### Timing measurements

- The lunge, dash state to END state, measures ~0.64 s (38 frames) and is the same every charge.
- A 0.45 s fall started at the dash reached the ground 163–212 ms before the bolt at 30 ms ticks; the drop was then
  moved later to land with the END state.
- `ChargeFallFrames` 21 was logged still in the air when the bolt fired; 19 leaves two frames' margin.
- The fall state just before the END state is only a few frames long — too late to start a drop on.

## Super Steve — a Sword of Zeus sphere (`ZeusShot`)

Xiao holding Super Steve with a Sword of Zeus SynthSphere fires the sword's lightning from the slingshot, with the sword's
lighting throughout: `ZeusShot.FlashProfile` is the Zeus profile on `Items.supersteve` and `SuperSteveRig.WeaponModel`
(0.5 dim, the electric white, 2 s ease, no white on her, no disc, no hit of its own). Solar Harvest and Big Bang's lock-on
reach are inherited alongside. Bolts bill the weapon as the sword's do (`SwordOfZeus.Strike` / `StrikeAt` /
`StrikeNearest` with `weapon: Items.supersteve`).

| piece | how |
|---|---|
| guard charge | `GuardWatch.IsGuarding()` for `GuardSeconds` 3: the slingshot whitens (`SolarBlade`) as the room darkens to the prime dim, the stock cyan `ChargeTint` ramp runs on her, and at full the stock charge-complete flash fires (Zeus puts no white on its wielder, so this is the readiness cue). Primed, the charge holds `PrimedSeconds` 10 unused, then dissipates. No charging while a blinding runs |
| release, not locked on | the VOLLEY: a plunge of `SolarLighting.RampFrames` (4) frames from the dim it had, then `SwordOfZeus.StrikeNearest` from where she stands and the strike's flash. The pellet is retired as it appears (its pool flag word zeroed) — no shot flies |
| release, locked on | the CHAIN window: for `ChainSeconds` 5 from that first release, every pellet that reaches an enemy brings a bolt down on it with the flash. The window does not reset (the sword's chain lasts its combo; hers is the clock, so shots can follow fast). A pellet released inside the window keeps its bolt however late it lands; only shots released after it stop calling one. The prime dim holds until the first bolt, then each flash eases back at its own cadence and the next overrides it |
| the shot charge | the shot held `ShotChargeSeconds` 1 (`ShotCharge`, as her other charged shots are made; the room darkens as it builds, as under the sword's charge attack) marks the NEXT pellet: wherever it ends — on an enemy or not — the charge bolt comes down there with the strike's flash, locked on or not. A release short of the charge lifts the dim; the charge's dim is held while its pellet flies, to the bolt |
| the pellet hurts nothing | a pellet that calls a bolt has its damage word (`PlayerShotPool.DamageAddr`, pool + 0x2E0) written `NoDamage` −1 as it leaves. The `PelletPlant` cave (an ISO patch on step__5CSHOT's plant call at 0x1ABE04, DebugIfCave + 0x6D0 = 0x1B4E90, `ElfShotPackPatches.PatchPelletPlant`) then has its contact plant nothing and simply end it, on the engine's own frame; the mod sees it end and brings the bolt down on the enemy it ended on — the bolt is the hit. `ZeusShot.Native` checks the `jal` at 0x1ABE04 points at the cave; without it the mod warns once and bolt pellets land a hit of their own |
| which enemy | the engine's own contact record first (`PelletContacts`, the pellet-contact cave at 0x1B4EC0 → `CodeCaves.PelletContact` 0x21FAF990: slot, what it met, the hit-sphere centre; `EnemyAtSphere`), else `PelletWatch.EnemyAt` at the pellet's last position. Contacts are synced when the first flight starts so only contacts from then on count |
| flights | each tracked pellet's position is followed while its pool flag is live; a pellet still out after `MissSeconds` 3 is let go. A non-charged flight that ended on nothing brings no bolt; a charged one strikes the point (`StrikeAt` with no enemy under it) |
| floor change, sphere off | flights dropped and the charge dissipated on a floor change; `ZeusShot.Stop` on the sphere or weapon going: blade and tint cleared, lighting restored, a blinding of hers ended |

## Lessons

- ⚠ **Never write the CCharacter scale (+0x90) on an engine-fired sub-shot of the main-character effect instance.** The
  engine re-fires the whirl into whichever sub-shot is free, scale and all; a +0x90 write on the whirl's object reset the
  game mid-spin, and telling the strike's object from the whirl's by that field failed for the same reason. The root
  frame's local 3×3 is the one scale path, held every tick for every sub-shot carrying the model.
- ⚠ **Only the first FIVE bytes of the root's name are reliable at runtime.** lightning.mds's `null2` read `null2 ??` on
  the live copy — whatever followed the NUL in the frame's name field — where explosion.chr's `null3` happened to be
  NUL-padded. An 8-byte compare never matched and the whirl stayed 1×; `IsBoltRoot` compares the word "null" and the
  byte '2'.
- The cutscene's own strike (`dun/script/d01/event.stb` label 90, the cat zapped into an atla): the actor is loaded hidden
  at `_SET_NPC_SCALE(5, 5)` holding motion 0, then at the moment `_PLAY_SE(390)`, `_NPC_DRAW(1, 5)`, `_SET_NPC_MOTION(5, 1)`
  played through, `_SET_NPC_POS(5, x, 0, y)` at the cat's GROUND point, `_NPC_DRAW(0, 5)`. Its ×5 on the actor scale reads
  about half the mod's ×10 on the root hold.
- The bind is read from the first root seen at identity because a root a previous hold left scaled — this session's or an
  earlier mod run's, PCSX2 running on — is not the bind.
- The charge's level 2 has to be the sword's own: the game's level 2 is the whirlwind, which is the bolt already, so the
  unlock word is taken away for the wind-up rather than letting the game branch.

## Open until played

- `[Zeus] the blade landed but no bolt was free` — every sub-shot of the instance busy when the judgement blade lands;
  the strike is lost. Whether this happens in a full combo while locked on.
- `[Zeus] charge bolt with the blade still falling` — the END state coming up before the fall ends; `ChargeFallFrames`
  is the knob.
- `MaintainScale` diagnostics (`DebugDiagnostics.Enabled`): `no lightning root among the sub-shots` means the instance
  holds models but none is the bolt — the whirl's object is not being reached.
- `ZeusShot`: a bolt pellet on an ISO without the pellet-plant cave lands its own hit as well as the bolt (logged once;
  re-patch the ISO).
