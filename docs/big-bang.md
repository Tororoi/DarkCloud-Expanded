# Big Bang — Detonate

Big Bang is the Sun Sword line's end: it inherits Solar Harvest and Solar Flash (docs/solar-flash.md) and makes both of
Toan's charges a BLAST. The regular charge's whirlwind IS an explosion — the spin goes off at his feet. The guard charge
is Solar Flash struck at twice the Sun Sword's share, and while it is primed and Toan is locked on, a copy of the sword
hangs point-down over the target: the JUDGEMENT BLADE. The primed swing then drops it instead of flashing, and where it
lands it detonates — the white-out, the blast, every enemy on the floor turned to watch, the weapon-HP bill. While the
blade is held, the game's own explosions cannot hurt him. Dungeon only.

Code: `Weapons/Toan/BigBang.cs` (`BigBang.DetonateEffect`, a thread per equip from `WeaponThreads`, 30 ms ticks, started
beside `SunSword.SolarHarvestEffect` and `SunSword.SolarFlashEffect(SunSword.BigBangFlash)`), and the shared engine it
drives, which the Sword of Zeus (docs/sword-of-zeus.md), Hercules' Wrath (docs/hercules-wrath.md), Babel's Spear
(docs/babels-spear.md), the Terra Sword, the Cactus and the Big Bang shot also use:

| file | what it is | who uses it |
|---|---|---|
| `Weapons/JudgementBlade.cs` | the hanging copy: the lock-on hover, the drop, the engine fall, the point hover | Big Bang, Sword of Zeus, Big Bang shot |
| `Weapons/BladeProp.cs` | the engine-drawn copy of a rigid model in chara slot 3 | JudgementBlade, Babel's Spear, Terra Sword, Cactus, the bomb model |
| `Weapons/BlastFalloff.cs` | the falloff blast: one hit entry per enemy, stepped by distance, kicked away | Big Bang (both blasts), Sword of Zeus, Hercules' Wrath, Big Bang shot |
| `Weapons/EnemyBody.cs` | where an enemy's body is: posed hurt spheres, unit height, head height, HP, shared roots | the blast, the hover, EnemyHit, PelletWatch, Confusion, Terra Sword, Cactus, Babel's Spear |
| `Weapons/EnemyFacing.cs` | every enemy turned to a point and held | Big Bang, Big Bang shot, JudgementBlade |
| `Weapons/Toan/BigBang/BladeRedirect.cs` | the per-slot target-table redirect while a blade falls (through `AggroTable`) | JudgementBlade (begun at the drop), Big Bang and Sword of Zeus (released when due) |
| `Weapons/ExplosionImmunity.cs` | the four explosion configs and the bomb reaction made inert for the player | Big Bang, Big Bang shot (through the sphere) |

## Detonate in play

| moment | what happens |
|---|---|
| the regular charge | Toan's meter runs 1.0 → 3.0 (lunge at 1.5, whirlwind at 2.5). The blade whitens across it with the same tint a guard charge uses (`BladeTint.Set`, driven by the meter instead of held time); it stands aside while Solar Flash owns the blade (`SunSword.FlashArmed`). The lunge is an ordinary swing |
| the whirlwind | the level-2 charge is the blast. From the moment the meter reaches 2.5 the spin's own hit sphere is written out of reach (`WhirlNoHit` −1000 into `CodeCaves.ChargeHitRadius`'s whirl word; the lunge's stays at its vanilla 6), so nothing is struck twice; as the spin begins the falloff blast goes off at his feet (`BlastFalloff.PlantFalloff` at his position), every enemy is turned to him, and the blade pays `BlastWhp` 20 weapon HP. Guards are crushed for as long as the spin lasts (`GuardGate.NobodyBlocks`): a blocked spin would eat the detonation. Its model is explosion.chr (below) |
| the guard charge | Solar Flash with `SunSword.BigBangFlash`: share 0.50 of attack, light (236, 226, 255) and fog (242, 236, 255) — a cool white toward pale violet — fog 0.8, prime dim 0.35, blade-glow-only (the blue disc `ToanGlowBakes.BlueName` appears with the judgement blade and goes with it; Toan never carries it). The blinding, the guard break and the lighting pipeline are Solar Flash's |
| primed, not locked on | the swing is the ordinary flash at `SunSword.FlashWhp` 5 |
| primed and locked on | the judgement blade hangs over the target (below). The primed swing drops it; the flash waits for the landing |
| the landing (`BigBang.LandBigBang`) | on one frame: the lighting white-out (`SunSword.BigBangFlash.ArmLighting` + `SceneLighting.Flash`), the burst where it fell, the falloff blast crush-marked through any guard, every enemy turned to the point, the 20 WHP bill. Solar Flash's own tick then runs the rest of the flash — the light hit, the blinding, the blade and glow — once `JudgementBlade.TakeDropLanded` hands it the landing (it is told it is already lit, so it does not white the room twice) |
| the sphere per victim | the blast is one hit entry per enemy in reach, not one big sphere: damage = attack × 1 / 2 / 3 / 4 at 50 / 40 / 25 / 10 units from the blast to the nearest edge of the enemy's hurt spheres, elementless (no resistance blunts it), kicked away from the blast ≈ 50 units |
| the auto-guard answer | explosions cannot hurt him while the blade is held (immunity, below). The auto-guard cave makes the engine forget such a hit — which is what keeps his charge alive — and ticks `CodeCaves.AutoGuardSignal`; the mod answers each tick with the controller shove the engine itself uses on a hit (`GuardRumble` 0xC0 for 10 frames: its own are motor 1 at 0xE6/22 frames for a knockdown and 0xDC/12 for a lighter one, so this sits under both) and the guard clang `GuardSe` 0xA2 at volume 90. The count is compared, never zeroed |
| the lock-on reach | the Cross Hinder's reach, inherited (`ToanLockOn.HoldReach`) |
| the WHP drain | `WeaponWhp.Drain(Items.bigbang, 20)` per blast, whirlwind or drop, through the engine's own drain (the WHP-bill cave), which Endurance scales and which breaks the blade at 0 the engine's way. The flash alone (not locked on) costs Solar Flash's 5 |

## The whirlwind's model

Toan's whirlwind visual is the main-character effect instance — the one `BorrowedShots` borrows for Xiao (hers sits
idle holding an unused mgan01; his holds c01_fuusya). Seeding it with `dun/effect/explosion.chr` REPLACES the whirl, and
the engine fires it on the spin by itself; no ISO patch, since `CustomConfig` names any container on the disc.
`BigBang.WantedShot` is the provider handed to `BorrowedShots.Start`, so the cave re-enters it whenever the floor loader
has refilled the instance.

- The container is a full pack: explosion.cfg (VERTEX_ANIME, BODY_SIZE 17,7,60 — c01_fuusya's), explosion.mds (root
  frame `null3`), explosion.mot (one motion, KEY 5–30 at speed 0.2), explosion.img. Stock config 5 supplies the shape
  (`ExplosionTemplate`); the name and motions are replaced: only the MUZZLE phase names a motion, the shape of the whirl's
  own config ("muzzle motion 0, nothing after"). Every phase radius is zeroed — the effect is the visual; a phase with a
  radius plants a damage entry of its own every frame.
- Scale: `ExplosionScale` 1.0 (authored size), held on the root frame's local 3×3 by `MaintainExplosionScale` on every
  pool slot (a spin can activate any of them; the idle copies sit at the origin). The bind is read once from the first
  root seen at 1×. The root is matched as the word "null" + the byte '3' (`ExplosionRootWord` / `ExplosionRootTail`),
  so the stock whirl scaler in `Weapons.cs` — which validates "kiru" + "fkiri" — stands down while the instance holds it
  (`BigBang.ExplosionSeeded` → `Weapons.WhirlScaleStandDown`).
- The dropped blade's burst (`BigBang.Burst`) plays the same container at the landing through `BorrowedShots.Burst`, at
  `BurstMul` 1.5× the whirl's size while its sub-shot is live. Until the config is entered on a floor, the thrown-gem
  ICE burst from the always-resident Maseki pool stands in (`GemBurst.Show`, `BurstScale` 10, `BurstSpeed` 0.6, no
  damage). The scale is visual only: what the blast HITS is the falloff's hit entries.

## The judgement blade

`JudgementBlade` hangs a `BladeProp` copy for whichever `JudgementOwner` is ticking it — Big Bang's `BigBangOwner`
(redirect on, the room darkened along the whole fall), the Sword of Zeus's `SwordOfZeus.Judgement`, the Big Bang shot's
`Owner` (the bomb model, fading in with the charge). Two hovers share the one copy: the LOCK-ON hover (`JudgementTick` /
`BeginDrop`) and the POINT hover an owner places itself (`PointHover` … `PointEnd`).

### The lock-on hover

- Gates: the owner primed (`JudgementOwner.IsPrimed`, else `SunSword.PrimedFor`) AND locked on. "Locked on" is the lock
  button's own toggle (`PlayerAction.LockHeld`): the slot word alone is only the nearest CANDIDATE, re-picked every frame
  the toggle is down, and the cursor word stays raised after a release. Liveness is HP alone (`EnemyBody.HasHp`): the
  hover must never depend on anything it changes itself. A lock that flickers off, or wanders to another enemy, for less
  than `LockGrace` 0.35 s keeps the hover on its enemy.
- The copy fades in over `FadeSeconds` 0.25 at `HoverScale` 2× (or the owner's scale), its flat facing the way the
  fallen blade will (`EnemyFacing.PlayerFacing`); the glow crosses onto it (`DriveGlow`, hung at the blade's middle:
  −length × scale / 2 below the grip) and the target's name plate is hidden through `CodeCaves.NameHide` — the gate the
  plate's getter ANDs in, because the engine re-raises the flag itself every frame the target is on screen. The enemy is
  never touched, so a kill during the hover still counts.
- Height: the tip `HoverMargin` 6 above the species' AUTHORED height (`EnemyDefaults.HeightFromRoot`, scaled with a
  grown miniboss), `HoverFallback` 20 for a species with no record, plus the blade's length at the copy's scale (the grip
  is the root, so the tip is a length below the placement). The length is the weapon's dcol1 reach from the offline
  table, `BladeLengthFallback` 12. Measured ONCE per target as the hover begins (`HoverHeightFor`), with the enemy at
  rest.
- Placement is the engine's, never a per-tick write. If this unit is the sole live user of its model root the copy is
  PINNED to that root (`BladeProp.Pin`): the draw chains its world matrix through the enemy's every frame. ⚠ Units of one
  species share one model tree, posed for whichever unit drew last, so when another of its kind is live the blade-fall
  cave FOLLOWS the unit's own position instead (`EngineFollow`: `CharObjects.PosAddr`, per slot, height included — a
  flyer takes it up).
- Losing the lock, or the prime, fades it out where it hangs and the glow shrinks with it; at zero `AbandonHover` takes
  it down and the glow comes back up on Toan while the charge still stands.

### The drop

`BeginDrop` (called from Solar Flash's tick on the primed swing): the blast point is the target's own CCharacter
position (per slot; the floor-slot record's location fields read 0 for some enemies) and its root height
(`EnemyBody.UnitHeight` — a flyer's is up). The fall starts from where the blade hangs (its own world matrix when
pinned, the followed height otherwise) and ends with the tip at the root — the grip a blade length above it — or, for
an owner with `ToTheHilt`, the grip at the ground. The copy is unpinned into the world where it is, the lights begin to
go down, the redirect begins (owners with `Redirect`), and the fall is handed to the engine. Where it lands (`Land`):
`BlastFalloff.LastBlast`, `CodeCaves.JudgementPos` left on the blast, the owner's `Land(slot, x, h, y)`, the copy gone,
and the landing noted for `TakeDropLanded` (after `FlashDelay`, 0).

The dim: along the fall the floor's light and fog are driven down from the profile's prime dim on an exponential ramp
that peaks at the landing — k = (e^(a·u) − 1) / (e^a − 1) over the fall's fraction u in frames, sharpness
`SceneLighting.RampSharpness` 4 — over the whole fall for Big Bang (`RampWholeFall`) or only its last
`SceneLighting.RampFrames` 4 for a strike's brief plunge (Zeus). The flash then lands from the darkest frame.

### The engine fall caves

The fall is stepped by the ENGINE once a frame, not by a mod thread: the vertical-drive cave (`DebugInfoCave.VerticalDrive`,
`ElfFrameChainPatches.PatchVerticalDrive`, the tail of the camera-pin chain) reads the words at `CodeCaves.VerticalDrive`
0x21FAF8B0 and writes the copy's chara-slot position:

| word | meaning |
|---|---|
| +0x00 flag | 0 off · 1 FALLING (vy += g, y −= vy, stopped at +0x10 where the flag becomes 2) · 2 LANDED · 3 FOLLOWING (x/z from the unit at +0x14 plus the offsets, height = the unit's + the y word) · 4 fall-drive (`DebugIfCave.FallDrive`, Hercules' Wrath) |
| +0x04 y | the grip's world height (falling) or its height OVER the unit (following) |
| +0x08 vy, +0x0C g | speed, gravity per frame² |
| +0x10 stop | the height a fall stops at |
| +0x14 unit | the followed position's guest address (`CharObjects.PosAddr`, or Toan's own position words) |
| +0x18 / +0x1C | an x/z offset from the unit (0 over an enemy; the spot ahead of Toan for the Zeus charge blade) |

`StartEngineFall` sets g = 2·span/N² so the fall takes exactly N frames: the swing's remaining frame cursor at
`SunSword.SwingCursorPerFrame` 0.3 when PACED (Zeus's first swing, `paceFrom`/`paceTo`), a fixed time × 60 for a point
drop, else gravity's own time √(2·span/`Gravity`) × 60 with `Gravity` 500 u/s² — the tip lands on a 12 u enemy in
~0.27 s, a 25 u one in ~0.35 s, a 40 u miniboss in ~0.43 s. `BladeLoop` (the "BigBangBlade" thread, `FallTickMs` 2 ms)
only WATCHES the words: the dim from the fraction fallen, `JudgementPos` kept on the blade while redirecting, a flag
something else took mid-fall re-armed, `_fallDone` when the cave reports 2; the tick lands it. ⚠ The hover's tick and
the drop run on different threads (the owner's loop and Solar Flash's), so the blade words are written under
`_bladeLock` and a follow is never written once a drop is on.

### The point hover

`PointHover(owner, slot, x, h, y, height, ridesPlayer)` hangs the blade outside the lock-on gates, on the owner's cue:
over a unit (followed through the cave at the lock-on hover's own height, name plate hidden), over a spot on the ground
(placed from `BladeLoop`), or over a spot AHEAD OF TOAN that rides his own position words through the cave
(`EngineFollowPoint`, the Sword of Zeus's charge attack). `PointAlpha` drives its fade by hand (a charge's progress),
`PointFade` lets it out, `PointFreeze` stops a riding blade where it is and returns the spot under it, `PointDrop(seconds)`
drops it over a fixed time to `PointLandedAt`, `PointEnd` takes it down at once. `Land` on a point hover fires no owner
callback — the owner acts on `PointLandedAt`.

## BladeProp — the copy

A free-standing copy of a rigid model, drawn by the engine in chara slot 3 (the clone-weapon slot, texgroup 0x1D, the
slot the patched group formula gives the weapon's own atlas). `Spawn(scale, rootGuest, pointDown, pointUp)` copies the
equipped weapon (root 0) or any root handed in — the bomb model, a georama part — and refuses while `SlingshotProp` or
`CharacterClone` holds the slot and cave.

- TREE: the model's CFrame tree deep-copied (links re-based, world caches zeroed, root parent cleared: WORLD-rooted) into
  `CodeCaves.WeaponCave` (0x1400 B, 8 nodes) or, for a tree up to `LargeNodes` 24 (Queens' trees are 16), the TOP of the
  clone node pool, which is free whenever a copy can be up.
- MESH: every rigid `CVisualVu1` visual (vtable `CVisualMDT.RigidVtable` or the Solar blade's tinted copy) is copied —
  visual object and VU packet both — into `CodeCaves.PropMeshCave`, the recipe of `CopyMesh` minus the MDT block (the
  rigid object is 0x20 B: CVisual plus four words; +0x18/+0x1C are the VU pointer and its size in 16-byte units). A copy
  made while the blade is tinted inherits the tinted vtable.
- MOTION: none — the slot's channels cleared and its motion id held at −1, so the character step early-outs and the pose
  is the one baked below the root: `BakeDownward` composes R_x(+90°) on the grip rotation (the sword's authored grip
  already carries a half-turn, so +90° is what points it down), `BakeUpward` R_x(−90°) for Babel's Spear.
- The slot is cloned from the live weapon object (`CharCopy` 0xD60, the draw-relevant part of a CCharacter), re-aimed at
  the copy, cloth list → `CodeCaves.ClothStub`, tint zero, dim 1, opacity 0. `Maintain` re-asserts the registry, the
  step-skip entry and `Mailbox.MirageSceneGate` = 1 every tick (Mirage's loop writes 2 there each tick a dungeon has no
  decoy and stands down only for copies it knows about), and brings the copy down if the weapon or Toan's model changes
  under it.
- `Pin` / `Unpin` / `Face` for riding a node the engine moves (the slot's position becomes an offset in the parent's
  space, divided by the parent's scale); `Place` / `Orient` / `SetXY` / `SetHeight` / `SetScale` for the world; `Alpha`
  is `NpcOpacity` 0..128; `Tint` the ambient add; `Where()` the diagnostic.

## The falloff blast and the kick

`BlastFalloff.PlantFalloff(x, h, y, noKickSlot, damageScale, kickScale, reachScale, guardBreak)`:

- For every live enemy whose nearest hurt-sphere edge (`EnemyBody.NearestHitSphereEdge`: the `_SET_BODY_COL` set CheckDmg
  tests, posed this frame) is within a step, ONE `CollisionPool.PlayerHitEntry` centred on its largest posed body sphere
  (`EnemyBody.BodyCentre`) and sized to it, so that entry can only be consumed by that enemy. Steps `Falloff` = (50, ×1),
  (40, ×2), (25, ×3), (10, ×4) of the weapon's attack, outermost first, the last match the innermost; `reachScale`
  stretches the radii (Hercules' Wrath ×2), `damageScale` the steps (Zeus ×0.5). Elementless.
- The kick rides each entry (`CollisionPool.SetKick`, origin the blast): `KickStrength` 3.5 at `KickDecay` 0.12 —
  distance ≈ force²/(2·decay) ≈ 50 units (vanilla melee is 1.2 at 0.2, roughly 3.6). `noKickSlot` is the enemy a bolt
  struck directly: strength 0, the reaction without the shove.
- `guardBreak`: each entry carries `CodeCaves.CrushMark` ("CRIK") at `NoDrainMarkOff` 0x9C, which passes every guard
  window (the guard-crush cave, `ElfDamagePatches.PatchGuardCrush`) and — the mark sharing the no-drain mark's high half
  — bills Ungaga no weapon HP per hit.
- The pool always keeps `ShellPoolReserve` 16 free entries for the engine; planted entries are tracked in `_shells`
  (`PlantedHits`, clearMark + retireSpent) and the unconsumed ones withdrawn after `ShellLifeTicks` 3 by `ExpireShells`,
  the mark zeroed as they go. `LastBlast` is where the last one went off, for a flash fired on the landing.

## Immunity to explosions

`ExplosionImmunity`: the four shot configs that ARE the explosions — `ExplosionCfgs` 3, 16, 17, 18: pump_bom (Halloween's
thrown pumpkin) and the three self-destructs zibaku_f2 / zibaku_r2 / zibaku_t2 (自爆: Mr. Blare, Bomber Head, Sam, Billy)
— have their reaction word written `ReactionInert` 5, and `CodeCaves.BombReaction` 0x21FAF838 (chest traps, thrown bombs)
likewise. An entry takes its reaction from the config at plant time, and BtCheckDamageProc subtracts the player's HP only
inside its reaction branches (3, and 2-or-4), so 5 is consumed and nothing reaches him. `CMonstorUnit::CheckDmg` never
reads the reaction: enemies are unaffected. ⚠ The configs are static ELF data shared by every enemy of those species, so
`RestoreImmunity` runs on `Reset` and on the sphere going (`DriveImmunity(false)`), and `Weapons.SeedBombReaction` puts
the vanilla 3 back. The config table is reached through `ShotEffectPack.CfgTable`, an array of POINTERS to the 0x70-byte
records: the record is at *(CfgTable + index × 4). `ArmImmunity` reports each config by name and reads the word back.

The auto-guard half lives in the ISO (`ElfDamagePatches.PatchAutoGuardMatch`): the player's damage handler zeroes
Toan's action word before it looks at a reaction at all, so an inert hit would still cancel his charge; the cave
intercepts at the CheckHitUser match (dun 0x1DBB0E0), consumes a reaction-5 entry, returns "no hit" and ticks
`CodeCaves.AutoGuardSignal` 0x21FAF840 (+0 count, +4/+8/+C where).

## Every enemy's eyes on the blade

`EnemyFacing`:

- `TurnEnemiesToward(x, y)` writes every live enemy's facing vector (`FacingX/Z`, the unit its AI steers by) and its
  CCharacter yaw (what it is drawn with) toward the point, and `FaceTick` re-writes them for `FaceHoldTicks` 12
  (≈0.36 s, through the flash's stagger into the script hold): a single write of either is undone by the next Step. An
  enemy with knockback 0 (bosses, rooted plants) is not turned. The yaw is written in whichever of the engine's four
  sign/axis conventions the floor's own enemies reveal (`YawConvention`, read once per floor from each live enemy's
  facing against its yaw; −1 leaves the yaw to the engine when none fits within 0.35 rad).
- `PlayerFacing()` is Toan's model-root Euler Y (or the CCharacter yaw when the Euler is stale), for the things placed
  square to him.

`BladeRedirect` (`Weapons/Toan/BigBang/BladeRedirect.cs`, the judgement blade's own):

- The REDIRECT (`BeginRedirect` … `ReleaseRedirect`) borrows Mirage's decoy table: each enemy's `_GET_POSITION(-2)` reads
  through `CodeCaves.PtrTable` 0x21F19000 (`RedirectSlots` 20 entries × 4), so while the blade falls every live slot is
  pointed at `CodeCaves.JudgementPos` 0x21FAF860 (x, h, y, 1) — the blade, then the blast — and their own AI turns them
  to it; the pointers go back to the live player `RedirectRelease` 0.4 s before the blinding ends, or `RedirectOrphan`
  2 s after a drop that never flashed. Mirage's writer only runs for Ungaga and Angel Gear's for Xiao, so nothing else
  writes the table while Toan holds it; `TargetRedirectCaves.Armed` (the caves armed at the main menu) gates it.

## Measured constants and addresses

| constant | value |
|---|---|
| `BigBang.TickMs` | 30 ms |
| `BlastWhp` / `SunSword.FlashWhp` | 20 / 5 weapon HP before Endurance |
| `ChargeMeterFloor` / `WhirlThreshold` / `PlayerAction.ChargeMeterCap` | 1.0 / 2.5 / 3.0 |
| `WhirlNoHit` | −1000 (the whirl's hit radius while armed) |
| `CodeCaves.ChargeHitRadius` | 0x21FAF830: +0 lunge (vanilla 6.0), +4 whirl (vanilla 12.0); `ElfToanMeleePatches.PatchChargeHitRadius` on ToanKey_Play 0x241AC0 / 0x241B90 (`lui $2,0x01FB` = 0x3C0201FB patched, 0x3C0240C0 vanilla), checked once per floor |
| `GuardRumble` / `GuardRumbleFrames` / `GuardSe` | 0xC0 / 10 / 0xA2 at volume 90 |
| `ExplosionTemplate` / `ExplosionScale` / `BurstMul` | config 5 / 1.0 / 1.5 |
| `BurstScale` / `BurstSpeed` / `BurstElement` | 10 / 0.6 / `MasekiEffect.Ice` (the fallback burst) |
| `ExplosionRootWord` / `Tail` | 0x6C6C756E "null" / 0x00000033 '3' |
| `HoverMargin` / `HoverFallback` / `HoverScale` / `BladeLengthFallback` | 6 / 20 / 2.0 / 12 |
| `FadeSeconds` / `LockGrace` / `FlashDelay` | 0.25 / 0.35 / 0 s |
| `Gravity` / `FallTickMs` | 500 u/s² / 2 ms |
| `CodeCaves.VerticalDrive` | 0x21FAF8B0 (layout above) |
| `CodeCaves.NameHide` / `JudgementPos` / `AutoGuardSignal` / `BombReaction` | 0x21FAF850 / 0x21FAF860 / 0x21FAF840 / 0x21FAF838 |
| `KickStrength` / `KickDecay` | 3.5 / 0.12 |
| `Falloff` | (50, 1) (40, 2) (25, 3) (10, 4) |
| `ShellLifeTicks` / `ShellPoolReserve` | 3 / 16 |
| `CodeCaves.CrushMark` / `NoDrainMarkOff` | "CRIK" 0x4B495243 / 0x9C |
| `ExplosionCfgs` / `ReactionInert` | 3, 16, 17, 18 / 5 (1 and 5 are unhandled; 4 is the light flinch and DAMAGES) |
| `RedirectSlots` / `RedirectRelease` / `RedirectOrphan` / `FaceHoldTicks` | 20 / 0.4 s / 2 s / 12 |
| `EnemyBody.BodyRadiusFallback` / `UnposedSphere` | 10 / 80 |
| `BladeProp.Slot` / `CharCopy` / `MaxNodes` / `LargeNodes` | 3 / 0xD60 / 8 / 24 |
| the engine's own hit rumbles | motor 1: 0xE6 / 22 frames (knockdown), 0xDC / 12 (lighter) |

## Lessons

- **The whirl's blast is planted, not the engine's sphere widened.** The earlier design wrote the whirl's radius word
  WIDE and let the engine's own charge-attack sphere do victims, damage and knockback, with a boosted attack, the
  elementless attribute and the kick written as data while the charge was up; arming had to precede the first damage
  frame because a first hit at the plain attack made that enemy invincible to the boosted frames after it. The blast is
  now `PlantFalloff` at his feet (the falloff steps the dropped blade has) and the radius word is written OUT of reach
  instead, so the planted blast is the only hit. The words are still the dependency, which is why the patch is checked
  once per floor: without it they are never read and the whirl connects at its stock 12 on top of the blast.
- **Naming a motion on every phase of explosion.chr played the explosion three times** — at the muzzle, the impact and
  the expiry burst — which was "it fires multiple explosions". Only the MUZZLE phase names one, the shape of the whirl's
  own config.
- **Shells around the blast, one per enemy per step, had every enemy in reach of several entries at once**, and the hit
  invincibility does not outlast a landing's own work — enemies took two and three hits. One entry per enemy, centred on
  its own body and sized to it, is what makes each hit that enemy's alone.
- **The gravity is a judgement's, not the Earth's.** 9.81 m/s² at ~5.5 units a metre is 54 u/s² and made even a short
  enemy a 0.8 s wait; 500 reads as a drop.
- **The fall used to be placed from the mod thread.** A 30 ms tick was a staircase; a 2 ms thread smoothed it but its
  writes still raced the frame that sampled them. The engine stepping the fall once a frame (the blade-fall cave) ended
  the jitter; the thread now only watches. The 2 ms cadence survives for the point hover placed over a spot, where the
  phase between a placement and the sampling frame is what reads as jitter.
- **The hover is pinned or followed, never re-placed per tick.** Measuring the spheres every tick had the blade bobbing
  with every animation; the height is read once at rest. Heights come from the root's WORLD height: the slot's LocationZ
  is floor-relative, and taking it for the root hung the blade a body too low and started the fall a body too high.
- **A pin to a shared model root follows the wrong enemy.** Units of one species share one model tree, posed for
  whichever unit the engine drew last; the cave's follow mode (per-slot position) exists for that case.
- **The lock-on words blink as the target is re-acquired.** Without `LockGrace` each blink faded the copy out where it
  stood and spawned it afresh over the target — a stall, then a jump. And "locked on" has to be the toggle itself:
  the slot word or the cursor word alone hung the blade over whichever enemy happened to be nearest.
- **Writing the name-plate flag only made the name flicker**: setTargetCursor re-raises it every frame the target is on
  screen. The getter's gate (`NameHide`) is what hides it.
- **The hover tick and the drop are different threads.** The hover branch once re-wrote the follow mode over a fall that
  had just been set up, and the blade hung there for good — the fall never landed, Solar Flash waited on it, the lock no
  longer faded it. Hence `_bladeLock`, and no follow once a drop is on.
- **A glow left anchored on the copy's root after the copy is gone** is a sprite drawn every frame at a node in mod
  memory that nothing maintains; the one run that left it there ended in the game resetting six seconds later.
  `AbandonHover` hides a glow anchored to the copy; only the fade-out lets it shrink off first.
- **The flash follows the burst, never before it** — a head start for the flash read as the burst arriving late, so
  `FlashDelay` is 0 and the white-out and burst go out on the landing's own frame.
- **Sharing the live visual drew both swords through ONE packet**: whichever draw wrote it last placed both, and the copy
  flickered between the target and Toan's hand with every gate steady. The visual object and its VU packet are copied
  into the prop mesh cave; a shared packet is not a fallback.
- **`ShotEffectPack.CfgTable` is a pointer array.** Indexing it by the 0x70 record size walks straight off the end of the
  34-entry table into the species table behind it, and the immunity "took" on words nobody read; the reaction is read
  back and reported by config name for that reason.
- **An auto-guarded hit that is silently dropped feels like a bug**, so the cave ticks a counter and the mod answers with
  the rumble and the clang. A flinch was wanted too; the only one available without an action change is a tint pulse,
  and it read as noise.
- **An enemy the blast cannot move is not turned either.** With no shove to settle it, the turn fought its own AI and
  never held a direction (knockback 0: bosses, rooted plants).
- **The floor-slot record's location fields read 0 for some enemies**, which put a blast at the world origin; positions
  come from the per-slot CCharacter (`CharObjects.PosAddr`).
- **The guard-charge tint and the regular-charge tint must not fight.** Two ramps over one mesh flicker, so the meter tint
  stands aside whenever `SunSword.FlashArmed`.

## Open until played

- `LogBlade` (behind `DebugDiagnostics`): the equipped blade once failed to draw on entering a floor — nothing of the
  mod's had run yet, and it drew again on re-entry. The once-per-floor log of the weapon object, its root, visual,
  vtable, opacity, dim and slot-3 registration is what the next occurrence gets compared against.
- `ProbeDamage` (behind `DebugDiagnostics`): when Toan loses HP it lists every player-hurting sphere in the pool with its
  reaction. An explosion that still damages him while its config reads 5 is either not the entry that was changed, or
  not an entry at all — HP taken by code that never touches the collision pool.
- Without the charge-radius ISO patch the ability logs `⚠ the charge-radius ISO patch is NOT applied` and the whirl
  keeps its stock reach beside the blast.
- `[JudgementBlade] engine fall found flag N mid-fall — re-armed`: something else took the blade words during a fall.
- The name-plate and lock-on diagnostics (`hover gates`, once a second while primed) remain for a hover that never
  appears.

## Super Steve: the Big Bang sphere

Xiao holding Super Steve with a Big Bang SynthSphere gets "Detonate" — Big Bang's two charges, as close as her slingshot allows.
Code: `Weapons/Xiao/BigBangShot.cs` (the charge state machine, driven from Super Steve's sphere dispatch; Solar Harvest is
inherited alongside), `Weapons/Xiao/BigBangShot/BombCarrier.cs` (the Bomb's mesh on the apple shot), `BigBangShot/PelletHide.cs`
(her pellets kept out of sight), `BigBangShot/BombModel.cs` (the Bomb's item model in the item-model cash) and
`BigBangShot/BombFx.cs` (the bomb's blast visual by data). The helpers share `BigBangShot`'s members through `using static`.

**The guard charge** (`GuardSeconds` 3 s) is Big Bang's Solar Flash: the slingshot whitens, the cyan build-up runs on her (the
room does not darken until a blast is coming); primed, she holds the white. LOCKED ON, a four-times Bomb — the item's own
model, red while it fades in over the whole charge, pulsing once it is in (a smooth cosine between 150,0,0 and black: black every
half second, red every second), the Matador's red-orange disc growing with it — hangs over the target where Big Bang hangs its
judgement blade (`JudgementBlade`, margin 10 above the species' authored height), and her RELEASE lets it fall: the shoot motion
plays and bills the shot, its pellet is drawn from the blank cell and retired the tick it appears; the room darkens along the
fall and it lands with the bomb's own blast at twice its size (drawn 2 above the floor so its ring is not flat on it), its ring
out to the blast's reach, exactly Big Bang's blast (`BlastFalloff` — the falloff damage, the kick, every enemy turned to it) and
Big Bang's flash. NOT locked on, her release IS the flash: Big Bang's flash from where she stands — twice the Sun Sword's share,
the cool light (`FlashProfile`) — after the dim ramp's four frames, with no pellet (retired as the drop's is).

**Every pellet is a bomb.** Each shot flies as Witch Illza's thrown-apple shot (shot table config 4, `ringo_ex`, entered in the
MAIN-character effect instance — hers in the monster pack is untouched) with the Bomb's mesh grafted onto the apple's node
(`dokuring__m`) in every tree of the entered instance (the template at +0xCC and each sub-shot object's own), so it flies and
turns as the apple does with no light or flash of any kind; the sub-shot is cut the tick it leaves its flight, so the apple's own
impact never draws — the bomb's burst is the impact.
- A PLAIN shot is the bomb at 1× that hits as her pellet would: the shot's own damage entry, planted ONCE on the frame its contact
  turns it to its impact (no plants in flight; the sub-shot is cut only once the plant has happened — the reload latch — or once
  nothing can plant, never on sight), with the pellet's damage (the engine's hit, reaction and weapon HP) and the drop's kick at a
  quarter of its distance stamped on it by the guard-bypass cave (`Mailbox.PelletKickDamage`, as Dragon's Y's shot); it bursts in
  the bomb's half-size visual there, no blast of its own.
- THE SHOT CHARGE (the shot held `ShotChargeSeconds` 1 s, as her other charged shots are made) is the bomb at 2× wearing the
  drop's red-orange disc (on the bomb's own node, full size from the tick it fires), bursting the tick it touches an enemy (within
  the species' authored width × the unit's scale + 3, between its feet and its authored height) or wherever it dies — a wall, the
  end of its flight, 3 s with nothing — with the bomb's own blast at 1.1× and half the drop's damage, kick and reach. Several may be
  in the air at once.
- Kick strengths follow from distances: a throw's distance ≈ strength² / (2 · decay), so the strength for a fraction f of the
  drop's distance is √f (`KickFor`).

Her white bleeds out over 0.25 s once a shot leaves. Weapon HP is the shot's, taken as the pellet leaves (`ChargedShotWhp`): 20 for
the drop, 10 for the bomb shot, 5 for the flash shot, over the 1.5 swing base. Explosions cannot hurt her while the sphere is on
(`ExplosionImmunity`). Without the apple shot entered on a floor the pellet flies plain and bursts where it dies (the visual alone).

### Lessons

- **The bomb's textures have one home at a time** — the pass drawing it now: the clone slot's texture block while the copy hangs,
  the main-effect block while the apple shot carries it (`BombModel.KeepTextures(block)`). Keeping both homes, swapped every
  tick, flickered the hanging bomb.
- **The apple's node is found by NAME, and a tree without one waits.** A tree still being built (the instance re-entered on a new
  floor) has the impact's light node (`hikari04__bacapp`) with a visual before the apple node has one; a graft onto that node drew
  the bomb offset from the shot. `GraftBomb` records nothing for such a tree and tries again next tick.
- **A sub-shot object's scale is not an animated node's.** An animated node's matrix is rebuilt every frame, so the bomb's size
  is held on the sub-shot OBJECT's scale (re-applied by the draw), set per fired shot and re-asserted while grafted.
- **Pellets are hidden for the whole time the sphere is on**, not per shot: a pellet is replaced or retired a frame or two after
  the engine drew it, and a quick shot's pellet can be out before the shoot state is even seen, so `Mailbox.PelletSpriteId` is held
  at the blank cell throughout and the sprite id in force before is put back when the sphere goes.

The section follows (docs/big-bang-shot.section.md, from the `Weapons/Xiao/BigBangShot.cs` pass).
