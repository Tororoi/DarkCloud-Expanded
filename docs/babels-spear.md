# Babel's Spear — "Curse of Babel"

Ungaga holds the guard for 5 s and a giant copy of the spear rises point-up out of the ground under the locked-on
enemy, most of it still buried. The enemy above it is struck and thrown off it; it and every enemy on the floor —
the spear are confused for the 20 s the spear stands, tinted light blue. Once risen the spear turns slowly on the spot.
Code: `Weapons/Ungaga/BabelsSpear.cs` (thread from `WeaponThreads`, `case Items.babelsspear`: the charge, the summon, the rise)
with its helpers under `Weapons/Ungaga/BabelsSpear/`, which share its members through `using static`: `BabelCopy.cs` (the spear
or the Super Steve statue behind one set of calls, `Form`), `BabelBeam.cs` (the confusion-area beam) and `BabelSpikes.cs` (the tip's
strike, the turning spikes, their planted hits). No new ISO patch. The

## Pieces

| piece | how |
|---|---|
| charge | R1 held in Ungaga's guard poses (motions 9 / 33, the Mirage's) for `GuardChargeMs` 5000 — the timer restarts whenever the pose is left with R1 still down (a swing, a hit, the raise: it once kept the old start and completed at once) — then `FlashChargeComplete` and the summon; one per guard hold; no charging while a spear stands or fades (the cooldown) |
| stars | every confused enemy wears the spinning stars (e114ex) from the resident stars instance (`StarsLane` / `ConfusionStars`, docs/confuse-ability.md) — the 8 nearest the player; they shrink with the spear's fade (`ConfusionStars.Fade`). The spear's hits also carry the Confuse ability natively (5 % per hit, 20 s) |
| weapon tint | the wielder's weapon wears the copy's own tint (50, 50, 50 ambient add, `SolarBlade`: Ungaga's spear `c10w10`, Super Steve's whole slingshot): ramping in with the charge, held while the spear stands, faded with the copy (`_fadeK`), off when it goes or a charge is dropped |
| target | `PlayerAction.LockOnTargetSlot` if live; else the nearest live enemy within 60; else a spot 15 ahead of the wielder (no strike, no confusion). The copy rises at the target's ROOT and FOLLOWS it across the ground while it rises (`Follow`: the slot's x/y and the beam's spot set to the target's root each tick; the rise cave keeps the height), until the tip strikes it — then it stays where it is (also once fully risen without a strike, or if the target dies) |
| spear | `BladeProp.Spawn(5, pointUp: true)` — the equipped weapon's engine-drawn copy in chara slot 3, baked with `BakeUpward` (R_x −90°). The c10w10 mesh runs −9.6 … +16.0 on its axis (tip at 16, dcol0). Root height = ground − 16·5 + h, h from −4 (tip buried) to +50 (exposed: the model's tip from z 6 to 16, 10 units at 5×). The rise and the spin are the ENGINE's frames, not mod ticks (20 Hz placement read as choppy): from effect frame 10 to 13 (0 → 0.5 s at 0.1) the blade-fall cave integrates the slot's height with v0 = 2D/T up and g = v0/T of deceleration (T = 30 frames), which reach zero together at the top — fast off the mark, slow to settle; the stop test is disarmed with a stop far below and the mod writes the exact top once when T is up. Then the blade-spin cave (`ElfFrameChainPatches.PatchBladeSpin`, chained after the blade-fall cave) adds `CodeCaves.BladeSpin` = 240°/s ÷ 60 to the slot's yaw every frame, wrapped to ±π (past that the engine's angle-to-matrix diverged). At 20 s the vanish clip plays (3.67 s at 0.1); from its frame 60 (2.0 s in) the spear fades out over the last 1.67 s ( opacity — taking the slot down while drawn showed a stretched frame), then comes down; also down on maintain failure or unequip |
| strike | planted the tick the rising tip (the blade-fall cave's running Y + 16·4) reaches the underside of the target's lowest active hit sphere, with the sphere still over the spear (edge within 6 of the axis); a target that left or died is not struck. One `CollisionPool.PlayerHitEntry` at the target's body (Big Bang's `BodyCentre`), damage = the weapon's attack, kick words at the WIELDER's position (thrown away from the player), at the Baselard's strength 2.02 fading 0.12 (≈ 17 units), kick type 2 |
| confusion | the target, then every enemy on the floor — at the summon and every tick after while it stands and fades, so newcomers are confused too — until the spear has fully faded (20 s + the vanish clip). Rides the Mirage's per-slot target-pointer table (`CodeCaves.PtrTable`, read by the cold-hosted `_GET_POSITION` / `_GET_DISTANCE`): a confused slot's entry points, every tick, at the live position of the NEAREST candidate anywhere on the floor (`Confusion.Configure(null, …)`: no area) — another live enemy's `CharObjects` position (guest address), or the player global. With no candidate it points at the slot's own wander quadword (`CodeCaves.BabelWander`, 16 × 16 B in the free data band): a random spot within 40 of it, renewed every 4 s or once reached. Requires `TargetRedirectCaves.Armed` |
| table ownership | `Confusion.OwnsTable` while any slot is confused; Mirage's loop skips its per-tick `WriteTable()` then (as it does for Angel Gear's ring). `Release` puts the player pointer back |
| friendly fire | each tick, every OPEN attack entry a confused enemy planted (owner slot·5 + 200, +0x70 == +0x74) is tested against every other live enemy's body spheres (`EnemyBody.NearestHitSphereEdge`); on contact a 1-unit hit entry is planted on the victim's body: the attack's damage, reaction (+0x4C) and kick (+0x80..+0x98), owner −1, +0x60/+0x68 −1, +0x64/+0x6C 0. One per attacker-victim pair per 0.5 s; withdrawn after 3 ticks if unconsumed. The attacker's own entry is never widened (mask 1): its swing sphere overlaps its own body, and CheckDmg has no owner exclusion — +0x5C is not one (any value but −1 makes the entry non-damaging for everyone) |
| solid | once risen, a column enemies cannot walk through: `tools/stubs/spear_block.s` (`ElfWeaponPatches.PatchSpearBlock`, DebugInfomationIF +0x870) takes `Step__12CMonstorUnit`'s `jal MoveChecMonster` (main 0x1DE344), runs it, then tests the unit's next position against `CodeCaves.SpearBlock` (flag, x, h, y, r = 8) the way MoveChecMonster tests other units — inside (r + the unit's move radius) and heading toward it → its heading loses the component into the column (d − (d·v/v·v)v, renormalised; head-on: the perpendicular), speed kept, so units slide round the spear as they do along walls instead of stopping (zeroing, MoveChecMonster's own answer, left them stuck). The PLAYER too: `tools/stubs/player_spear_block.s` (DebugInfomationIF +0xAA0) takes the player's move's two `jal MoveCheck__12CMonstorUnitFPfPfi` (dun 0x1DB39AC tile-grid floors / 0x1DB3E58 placed-parts floors, `DunPatches`), runs the engine's player-versus-enemy block, then — next position (pos + velocity 0x1DC2550) inside r + 6 (the engine's player allowance against a unit) and moving toward the axis — drops the velocity's part into the column and keeps the part along it (no renormalise: a wall's slide); moving away is never stopped, so a spear rising under the player can be walked out of. ENEMY SHOTS too: `tools/stubs/shot_spear_block.s` (DebugInfomationIF +0xB90) takes `Step__12CSHOT_EFFECT`'s `jal checkCollision` (main 0x1AC3E8); after the engine's test finds nothing, a shot whose victim mask (a3, config +0x48) is not 2 — every player shot and Ungaga's own charge effect pass 2 and go through — whose next position is inside r + its radius of the axis and between the floor − 2 and the column's top (mailbox +0x14, floor + the exposed length) is a wall hit (1) at its current position. Armed when the spear is up, cleared when it comes down |
| lock-on | Ungaga's entry in the lock-on factor table ×2 while the spear is his (the Cross Hinder's mechanism, `ToanLockOn`), released on unequip |
| spikes | once risen, every 60° of the spin (240°/s → every 0.25 s) each live enemy whose nearest hit sphere is within 10 of the spear's axis (the 8 solid column + 2) takes a hit of a sixth of the weapon's attack, thrown away from the spear's axis to half the Baselard's distance (`Baselard.HalfKickStrength` = 2.02 / √2 ≈ 1.43 at 0.12, ≈ 8.5 units; Super Steve's hands likewise, from the statue) (`SpinContacts`); planted with the active character as attacker (kill credit and ABS) but marked at entry +0x9C (`CodeCaves.NoDrainMark`), which the ISO's no-drain caves read, so a spike costs no weapon HP; the mod clears the mark when it withdraws the entry |
| tint | the unit's ambient add (`CCharacter` +0xCE0) written (12, 50, 84) each tick — a dim light blue — scaled by the spear's own visibility, so it fades out with the spear, zero at release |
| area | the Dark Genie's small beam c17_beem_s (ground cones r 51, billboard discs up a 21-unit column; unlit additive no-z-write; the shockwave c17_syougeki was tried first), copied by the ISO patch onto the dead `dun\effect\zibaku_f.chr` name with its palette's red and green exchanged and every alpha scaled to 0.3 (pink → cyan, purple → blue, at 0.3 strength; the genie's own entry keeps its colours — the sub-shot's opacity word does not reach a shot effect's draw) and `zibaku_f.cfg` exposed, borrowed into the SECOND main-character effect instance (`CharaMainEffectCrash` 0x21E97BC0 — Ruby's and Osmond's, idle for Ungaga, so Ungaga's own charge effect c10a_ex stays in the first; the dungeon loop steps and draws the LIVE instance alone, so the ISO patch's second-effect caves (`ElfConfusePatches.PatchSecondEffect`, DebugInfomationIF +0x780/+0x7C0, hooked at dun 0x1DB8740 / 0x1DAEB90) step and draw the second one too while `CodeCaves.SecondEffectLive` is set) through `BorrowedShots.CustomConfig(5, "zibaku_f", …, dir: "dun/effect/", instance: CharaMainEffectCrash)` from `BabelsSpear.WantedShot` (registered in `BorrowedShots.Start`), burst at the spear with damage 0 and every phase radius zeroed. The loader asks the container for `<name>.cfg` and the pack ships `info.cfg`, so the ISO patch (`BorrowedShotBakes`) writes the copy with `zibaku_f.cfg` appended. Its sub-shot is driven each tick (`ShockDrive`): position at the spear, CObject scale (1, 1, 1), play rate 0.1 (absolute, `ObjMotSpd`), KEY 0 rise (10–28) — the spear emerges from frame 10 to 13 (0 → 0.5 s) —, KEY 1 loop (28–48) rewound whenever it runs out while the spear stands, KEY 2 vanish (48–70) after the spear's time → 3.67 s, the spear fading from its frame 60. The config declares the VANISH as its muzzle motion: `Step__12CSHOT_EFFECT` retires a phase-0 sub-shot when its cursor is within one frame below the muzzle motion's end, whichever clip plays, so a rise KEY declared there kills the loop at its own first frame; with end 70 declared, frames 10–50 never reach the window and the vanish ends the sub-shot by itself |

## To verify in game

- `BakeUpward` sign: the sword's grip carries a half-turn, the spear's may not — if it points down, flip the sign.
- Slot 3's texture pass gives the copy the weapon's own atlas for Toan's swords; Ungaga's spear should follow (the
  Mirage clone already grafts his weapon into a chara slot), but check.
- Enemy draw honouring +0xCE0 (Draw__10CCharacter folds it in for the player and clone; enemies draw through
  DrawMonstor). Fallback: the Solar Flash's `_STATUS_SET_PALLET` stub programs.
- The contact test's catch rate at a 50 ms tick (an attack window of a few frames can fall between ticks).
- Whether pointing an enemy at another enemy's live position produces swings (attack-range checks read the redirected
  `_GET_DISTANCE`). First test: Rockanoff wandered in random directions — the victim choice then included the player at
  even odds and re-rolled every 2 s; now enemies only, 6 s. If it persists, the redirected read may not be the one its
  chase uses.

## Super Steve (a Babel's Spear sphere)

Xiao holding Super Steve with a Babel's Spear SynthSphere has all of Curse of Babel (`BabelsSpear.Wielded`; the thread starts
from Xiao's Super Steve case in `WeaponThreads`): the 5 s guard charge (her guard poses are the same 9 / 33), the copy rising
under the target with its ease, the strike and kick, the confusion and friendly fire, the spin and the spikes, the blue
confusion-area beam (`zibaku_f`, second instance), the solid column (enemies, the player, enemy shots), and lock-on reach ×2 —
raised on HER entry of the lock-on factor table (`MirageLineReach.Hold` raises the active character's and hands it back on a switch).

The copy is Super Steve itself, not a spear: her slingshot's visual is software-skinned, which `BladeProp` cannot copy, so it
is `SlingshotProp.SpawnStatue` — the Matador's world-rooted copy (same chara slot 3 the blade-fall and blade-spin caves drive),
without the pouch re-centring. Upright is the projectile's own pose (`CopyTree`'s orientation bake with `ProjectilePreset`
5 — fork up — the root's rotation folded into its children); nothing more is turned. Measured in game: a world-rooted copy
KEEPS its root's 3×3 under the slot's yaw, so a −90° turn written on the root (or its children) laid it down, and the
shield's preset (7) stood it upside down, as the Matador's note already said. The slot's yaw is left free, so the spin cave turns it about
the vertical. c04w13 upright: handle's end −1.85, fork tips +2.49 about the root. `StatueForm`: 4× (17.4 tall), its confusion-area beam at 0.7 scale, out once risen but for Steve's black feet (3.94 of 4.34
model units out: 1.6 units sunk), placed along the wielder's facing (the spear too: symmetric either way), tinted as the spear is (50 grey). Everything else keys off the form (`RootHeight`, the tip for the strike,
the column's top).

Super Steve's spikes are its HANDS: every 180° of turn (two per rotation, 0.75 s at 240°/s) each live enemy whose nearest hit
sphere's SURFACE is within 6 (3D) of either hand takes HALF the weapon's attack (the spear: every 60°, a sixth, against the
column). The hands are the fork's ends in the frame of the fork's centre bone eff30, (−0.04, 0.03, ±0.96) on c04w13, taken to the world
each tick through that bone's world matrix as the engine last posed it (`SlingshotProp.MuzzlePointWorld`), so they follow the
spin. (The skinned mesh node's own world matrix is not kept current for the copy: hands taken through it collapsed onto one
point, 8 out along the slingshot's length.) The catch is 3D: the hand's distance to the surface of the nearest active hit sphere.

Super Steve's solid column is radius 2 (its feet, 0.46 from the centre at 1×, × 4) — the spear's is 8. The player is held at r + 6, an enemy at r + its own move radius, an enemy shot at r + its radius.

## Friendly fire — shots

The engine tests a monster shot (a sub-shot of the shot pack at `*NowShotEffect`) against the player alone (the config's victim
mask), so a confused enemy's shot flew straight through the enemy it was aimed at. `ConfusionFriendlyFire.ShotHits` (every confusion tick
while any enemy is confused): each live flying sub-shot (phase ≤ 1) whose firing slot (`OffA060`, stamped by `SetUserID2`) is
confused is swept along the path it moved since the last tick (positions kept per pack slot × sub-shot — a 50 ms tick spans
several frames of flight) against every other live enemy's active hit spheres, widened by the config's flying radius (+0x2C). On
contact: a hit entry on the victim's body — the shot's damage (`OffDamage`), the config's reaction (+0x44), owner −1 (damage −
defence, no weapon, no drain, no credit); its element and statuses as the Angel Gear's reflected shots carry them (+0x50 one pure
element bit, 0 when the shot has status bits; poison / freeze / gooey applied by data, `AngelGear.ApplyReflectedStatus`) — and the
shot moved to the point where its path met the enemy and taken through the engine's own CONTACT branch of `Step__12CSHOT_EFFECT`
(0x1AC180), mirrored as data (`ConfusionFriendlyFire.ShotContact`): phase 2, its KEY from the config's phase table (+0x4C + phase × 2); no impact
KEY (−1) → the shot put out, and a bomb-type config (+0x54 == 100, Gemron's) DETONATES — `SetBombEffect(size +0x58, pos, mask,
+0x5C)` (0x1D5940: a CItemBombEffect, SE 0x6C, a collision of size × 20 radius / size × 45 damage on the mask, the shock ring past
size 1) called with the config's own victim mask (+0x48), as the engine would; an impact KEY → the object restarts on it (first frame
from its frame table +0x344, motion flags 6, rate −1) and its velocity becomes the phase's speed (+0x18 + phase × 4). The burst plants
its own damage as anywhere — the confusion shelters the summoner from nothing (its latch is not touched; note the step tests the latch
as a SIGNED byte `< 1`, so a hold must be ≤ 0x7F). (Zeroing `OffWait` instead
— the Angel Gear's end of flight — leads to phase 3, the EXPIRY, which most configs lack: those shots simply vanished.)

## The shared confusion

The confusion — targets through the Mirage's pointer table, wandering, the tint, the swings' and shots' friendly fire — lives in
`Weapons/Confuse/Confusion.cs` (the friendly fire in `Weapons/Confuse/ConfusionFriendlyFire.cs`), shared with Super Steve's Terra Sword
sphere (docs/terra-sword.md) and the Confuse ability's procs (docs/confuse-ability.md). Babel's Spear configures it with
no area (the whole floor), its light-blue tint (× the spear's fade) and no
provoking, and ticks it from its loop. Planted friendly hits live 150 ms (time, not ticks: the users tick at 50 and 16 ms). The
Mirage loop stands down on `Confusion.OwnsTable`.

## Lessons

- **The rise and the spin are the engine's frames.** Placing the copy from the thread's 50 ms tick (20 Hz) read as choppy; the
  blade-fall cave integrates the rise and the blade-spin cave adds the yaw every frame, and the thread only arms them.
- **Fade the copy, then take the slot down.** Taking the slot down while it was still drawing left a frame of it stretched across
  the screen; the copy's opacity goes to zero over the vanish clip's last frames first.
- **A sub-shot's opacity word does not reach a shot effect's draw.** The beam's strength is baked into the ISO's `zibaku_f` copy
  (its palette alphas scaled in `BorrowedShotBakes`), not written at runtime.
- **The beam config's muzzle motion is the VANISH clip.** `Step__12CSHOT_EFFECT` retires a phase-0 sub-shot the frame its cursor sits
  within one frame below the muzzle motion's end, whichever clip is playing: with KEY 0 (end 40) declared there, the loop clip's own
  first frame killed the sub-shot. With the vanish (end 70) declared the rise and the loop (frames 10–50) never reach the window.
- **The thread ends the moment the active character changes, a menu open or not.** The copy's slot was cloned from this character's
  objects, which an ally switch reloads under it (Desert Bloom's finding); a sphere keeps `Wielded()` true across the switch, so the
  character check is what ends it.
- **The guard timer restarts whenever the pose is left** with R1 still down (a swing, a hit, the raise): keeping the old start
  completed the charge at once on the way back into the pose.
