# Sun Sword — Solar Flash

Hold guard with the Sun Sword and the blade whitens over 1.5 s; at full it is PRIMED and stays so, guard or not -
the white it holds IS the readiness cue, so the game's own cyan charge-complete pulse is deliberately not fired (the cyan
ramp during the build-up stays, since that reads as charging rather than ready). The next attack carries the charge: as
the swing comes forward the blade goes back to its own colour and the dungeon flashes blinding — the LIGHT a warm near-white
(255,240,200, so it reads as sunlight beside the gold glow) and the FOG pure white — receding over the whole five
seconds so the room brightens exactly as the enemies recover. Every enemy within 300 units takes a
light hit and is then blinded for 5 s. `Weapons/Toan/SunSword.cs` (`SunSword.SolarFlashEffect`, a thread per equip
from `WeaponThreads`) drives it with three helpers in `Weapons/Toan/SunSword/`.

## The pieces

- **Charge** — `GuardWatch.IsGuarding()` (R1 + guard pose; Toan's guard-move clip is 34, the others' 33) timed on
  `GameClock`. The last half second also runs the stock cyan `ChargeTint` ramp on Toan, so the build-up reads like the
  game's other charges. Priming keeps through guard release; a sidekick taking over, a floor change or putting the sword
  away drops it.
- **Blade tint** (`SolarBlade`) — the sword is a frame in Toan's own tree (EquipWeaponFrame), drawn inside
  Draw__10CCharacter under HIS ambient, so a character tint would whiten Toan too. The blade mesh's visual (the weapon
  model's private object, rebuilt on every swap) is a rigid `CVisualVu1` (vtable 0x2A11C0 — not the skinned
  CVisualMDTVu1 the cat's meshes are). It gets a private copy of its class vtable (`CodeCaves.SolarBladeVtable`, 32 B in
  the free runtime band) whose two DrawVu1 slots point at `ElfCave.SolarBladeTint`: six words in the ELF cave band's
  last gap (`ElfWeaponPatches.PatchSolarBladeTint`) that load CVisualVu1's DrawVu1 overloads (0x135000 / 0x134BC0) into
  t9 and jump into the BODY of the Divine Beast cat's mask-tint cave (`ElfCave.CatMaskTint` + 0x18), which adds
  `Mailbox.CatCapeTint` to the ambient, calls whatever t9 holds, and restores. Toan's sword and Xiao's cat are never live
  together, so the cave body and the tint word are the blade's here. The tint is the charge fraction × 200 per channel;
  `Clear` zeroes it and puts the class vtable back. The blade frame is found by `Weapons.ResolveBladeFrame("c01w10")` →
  `LocateModelFrame` (the template name sits at node + 0x118; the visual is the node's `GeomPtr`). A skinned visual would
  take the mask cave's own entries; any other class is left alone (logged once, no tint).
- **The peak palette step** (`SolarBlade.PaintPeak`) — the instant the charge completes, the blade's OWN 256-entry palette
  is EXPOSED in the texture manager's copy: every entry multiplied by `PeakGain`, and an entry bright enough after that
  gain washed toward white by up to `PeakWash`. Multiplying keeps the ratios between entries, so the blade's shading
  survives and only its brightest gold blows out. Selecting "the gold entries" and pushing each the same distance instead
  drove dark golds as far as bright ones, which flattened the shading into one cream with hard edges at the selection
  boundary. The original palette is kept and written back when the charge ends.
- **Toan's tint and glow** — while primed he holds a slight white on the character tint (`SunSword.PrimedTint`,
  re-asserted each tick so a status tint cannot strand it) and carries a white glow (`SolarGlow`), drawn by the game's
  own wall-torch routine through the cat's glow cave in the Matador's order: everything written while it is off, armed
  last. Both anchors are his model root (`CCharacter.CharModel`), so no named bone is needed — `Lift` raises it to his
  chest. The disc is HIS (`toanglow`): the cat's lives in Xiao's pack and is not resident for him — see the bake below.
- **Trigger** — `PlayerAction.ChargeActionState`: a combo swing (0x24-0x28) flashes when the frame cursor
  (`AnimFrameCursor`) passes that swing's forward point (clips 37-41 of docs/character-motion-table.md: 825 / 835 / 843
  / 852 / 870 — the first is the measured hit-window start, the rest are start + 5 and the finisher's midpoint, to tune);
  the charge lunge (0xF) and whirlwind (0x18) flash at state entry. A swing that ends before its point leaves the charge
  primed.
- **Lighting** (`SolarLighting`) — `DungeonAddresses.DungeonLighting`: the dungeon overlay's MainDraw (dun 0x1DAE2A0)
  hands the renderer these globals EVERY frame — `MGSetLight(dirs, colours)`, `MGSetAmbient(ambient)`, the bg colour and
  the fog (`fogRate` start/end, `fogColor` bytes) — the main set when `lightingMode` (0x2A34CC) is 0, the sub (ura) set
  otherwise. The flash captures the drawn set, writes ambient and the four light-colour rows to 255 and pulls the fog to
  white from 0 to 1 unit, then eases back with k = (1 − t)² over 1 s (the fog by k², so it clears first). A floor with no
  fog (end ≤ start) keeps none. Implausible captured values (outside 0-512) skip the lighting and log the set.
- **The hit** — one `CollisionPool.PlayerHitEntry` at Toan, radius 300: base = attack × 0.25, the sword's selected
  element as a pure bit, and the melee kick words (strength 2.0 / decay 0.3 / type 2, origin = Toan) so CheckDmg runs the
  enemy's own flinch + shove. Withdrawn after three ticks (the engine only withdraws its own swing spheres).
- **The blinding** (`SolarStun`) — every live slot within 300 u horizontally. Its AI is held per slot, not frozen:
  CMonstorUnit::Step (0x1DD540) runs a slot's script by `run(ctx, label)` when the slot's running word
  (`MainMonstorUnit.ScriptRunning`, unit+0x50+slot*4) is 0, else `resume(ctx)` — and `resume` just calls the VM from the
  saved PC (`CRunScript.Pc`, ctx+0x30), which YIELD (op 23) sets to the next op. So each tick the slot's PC is parked on
  `CodeCaves.SolarYieldBlock` (18 YIELDs then `push 0; RET`, bytecode in mod RAM) with the running word set: the script
  only waits, no `_SET_MOTION`, no `_SET_MOVE`, while the engine keeps stepping the motion. The scripted move speed
  (`MoveControl.SpeedAddr`, unit+0x1E450) is zeroed every tick — a further hit runs the label-110 reaction from the top
  and its `_SET_MOVE` would leave the enemy sliding once the next park cut it short — so the enemy stops where it stands; its facing (`+0x60`/`+0x68`) is pointed at the flash spot each tick. The hold starts 0.1 s after the flash so
  CheckDmg's label-110 reaction has requested the damage clip; once that clip is seen past its end (or 1.5 s, never
  before 0.35 s) the guard raise is requested exactly as `_SET_MOTION` (ELF 0x1E1710) requests a motion — the render
  object's motion id, flags 0 and the KEY speed from the model's motion table (halved while Gooey), body and parts, plus
  the slot's request words — then the hold loop when the raise has played out (it loops on its own); a species with no
  guard gets its idle. A clip not seen playing is requested again after 0.25 s (counted in the log; should be rare now).
  Near the end the guard comes DOWN through the model's own guard-return clip (ガード戻り, 108 of the 158 species
  have one) and the enemy settles into its idle, so the player sees the pose break and can back off instead of being
  attacked the instant the hold ends. Dropping straight to idle instead read as the enemy snapping out of the guard far
  faster than it ever does in play. When the timer ends the PC and running word are cleared and Step starts label 100 afresh. A death during the hold is
  the engine's: the moment the slot's label (`CRunScript.Label`, ctx+0x2C) is the death label's funcdata the hold lets
  go. (Freeze status was tried and rejected; a lost-target redirect through the aggro table stopped the walking but the
  AI kept re-requesting its own clips every quarter second.) Per-species idle / damage / guard clips:
  `GameData/EnemyGuardMotions.cs`, generated from docs/enemy-motion-table.md by
  `tools/analysis/gen_enemy_guard_motions.py` (111 of 158 species have a guard).

## Tuning knobs (constants)

`SunSword`: ChargeSeconds 1.5, FlashRadius 300, FlashDamageFraction 0.25, Kick 2.0/0.3, HitLifeTicks 3, FlashPulseSpeed
90, FlashSe 0 (no sound yet — a resident SE id goes here), the five combo forward frames. `SolarBlade`: WhiteMax 200 (the ambient add). The peak palette exposure was removed once the glow landed - the glow reads the charge on its own.
`SunSword.PrimedTint` 45. `SolarLighting`: EaseSeconds = SolarStun.StunSeconds (5 s), FogStart/End 0/1, FogEasePow 2 (the
fog clears ahead of the light). `SolarGlow`: Scale 1.0, Flags 2, Pull 5, Lift 8. ⚠ Flags 2 is the flickering flame sprite ALONE — 1 adds the steady
glow pair (18 × 9 at 1.0), which draws as a second much smaller glow beside the first, so 3 shows a tiny duplicate; the
cat and the Matador both use 2. ⚠ The disc is NOT carried by a chara slot - that was tried and disproved. Nothing services the chara texture groups
0x20+i: clearing an unused group's loaded flag left it zero even with the slot registered and holding a real active
character. The cat never relied on it either; its textures live in Xiao's own block, which the scene draw reloads every
frame, and the group retag only governs binding. So the disc stays tagged to Toan's block (reloaded every frame) and only
its pixels move, into a page-aligned window above every block's top where nothing else uploads - its original home at
0x2000 sat inside block 3's window, and block 3 re-uploads every frame, which shredded it. The block's loaded flag is
cleared each tick, because the uploader otherwise skips an entry sitting above its block's top.

⚠ Guard clips are TIMED, not frame-polled, and each from the model's OWN motion table in RAM (entry =
motion*0x10: start +0, end +4, KEY rate +8) - so a raise, a hold and a return each run at their own native rate on
every species. The decoded table (EnemyGuardMotions) carries only ONE speed, the guard loop's, which is right only
where a species shares that rate and otherwise cuts a clip short or overruns it; it is kept for the clip INDICES,
which RAM cannot supply since they come from the Japanese names, and as the fallback if a live read looks wrong.
Playback was always native - the request hands the engine that same table's rate. Arthur: return is clip 7, 10
frames at 0.2, so ~0.83 s, and the wind-down window is 1.3 s to leave room for it plus a moment of idle.

⚠ The blinding is driven by the enemies' OWN scripts, and takes over TWO labels. Label 100 (AI) gets a guard hold:
cancel movement, play the guard clip ONCE, then loop cancel+YIELD (re-issuing the motion each frame would rewrite the
frame cursor and freeze the clip on frame 1). Label 110 (hit reaction) gets the flash's own stagger: cancel, play the
damage clip, wait N frames, RET - at which point the AI label brings the guard up. Taking 110 over matters because
CheckDmg runs it DIRECTLY when the flash's hit lands, overriding the AI label for its whole duration and turning the
enemy back toward the player, which is what made the stun look delayed. Both originals are snapshotted and written back.
Labels are packed back to back, so a replacement may only use the span before the next label's code: the guard needs
108 B and fits everywhere checked, while the stagger's wait is SIZED to the room (20 frames where there is space, down
to a 4-frame minimum; a handful of Dark Genie scripts have only ~104 B and keep their vanilla reaction).
⚠ Every species is held, not just the ones that can guard. Only 111 of 158 have a guard clip - flyers such as bats
have none at all - and skipping those left them flying on through the flash, which is what 'bats are unaffected' was.
A species without a guard holds its IDLE instead: grounded enemies brace, flyers hover, and neither acts. The guard
LOWERING clip in the wind-down only applies to the ones that actually raised a guard.

`SolarGlow`: GrowSeconds 0.25, FadeSeconds 0.5 — and the CHARGE starts the glow a grow-time early, so it reaches full
size the instant the charge primes (a guard released before then hides it again). `SolarLighting.FlashColour` 255,240,200 drives the ambient, the four directional light rows and Toan's own pulse;
the fog is driven to pure white separately (`FogColour`), and lifts in 1 s while the light takes the full 5.

⚠ The flash plants ONE SPHERE PER ENEMY, not one big one. A CollisionData entry is CONSUMED by the first victim the
engine matches it against, so a single 300-unit sphere damaged exactly one enemy - which read as 'one per species',
since a species tends to be clustered. Each enemy in range gets its own small sphere centred on it, with the kick
origin left at Toan so everyone is shoved away from him; the pool holds 96 entries against at most 16 enemies.
⚠ One flash at a time: charging is refused while a flash is still out, including a charge already in flight when the
last one fired. A charge left unused for 10 s dissipates - the tint bleeds out and the glow shrinks away over 0.5 s -
and the glow swells from nothing over 0.25 s when it first appears.

⚠ Restoring is guarded, and that guard is the important one. A snapshot is only valid while the script is still the
one that was patched: five seconds is long enough for a script to be reloaded or freed - a mimic changes state when it
is opened, a floor can change, a species can leave - and by then the saved address belongs to something else, so writing
the snapshot back corrupts whatever moved in. That is what ended a run at the memory-card screen, on RESTORE rather than
on patch. So the bytes are compared against exactly what was written before anything is put back, and a script that no
longer matches is left alone and its enemies are not force-restarted. Only BOSSES are skipped outright, because their labels are already
rewritten by BossScriptPatcher and two writers on one script would collide; mimics are patched like anything else. A label's span
is not entirely free either - scripts keep subroutine bodies between label regions - so a margin is left unused and the
records about to be replaced must decode as real opcodes first.

⚠ The flash BREAKS GUARDS for as long as it is blinding the floor. Blinded enemies hold a real guard and it really
blocks, so without this the stun made them harder to hit rather than easier. Guarding is data - three guard-window
active flags per slot, consulted by CheckDmg - so `GuardBreak.Drive(true)` holds them at zero while the blinding lasts
and restores the captured originals when it ends or the ability tears down. Nothing native is patched. A hit then lands,
the enemy's OWN hit reaction staggers it, and returning from that reaction drops it back into the guard hold - the
stagger-then-resume-guarding sequence comes free from the script takeover.
`GuardBreak` is shared (Weapons/GuardBreak.cs): Dark Cloud and 7th Heaven drive it while drawn, Super Steve through
their spheres, Solar Flash only during its flash. It owns the capture/restore and the floor-change reset, so no weapon
reaches into another's state; Toan holds one sword at a time, so no two ever drive it at once.

⚠ The wind-down runs on a PER-SPECIES clock, not one shared window. The guard-lowering clips run from 0.21 s to
1.67 s (median 0.49 s), so a single figure was wrong at both ends: 1.3 s cut the longest clip off 0.37 s early - the
abruptness it was meant to fix - and left a median enemy standing idle for 0.81 s. Each species is now started exactly
its own clip-length plus IdleBeat (0.4 s) before the blinding ends, so every enemy gets the same brief pause between
lowering its guard and acting. Each clip is measured from the model's OWN motion table (frame range at its KEY rate),
with the decoded table as the fallback.

⚠ The guard HOLD and the guard RETURN are requested differently. A hold uses the 2-argument motion form, whose flags
default to 0 = loop, which is what a hold wants. The return uses the 3-argument form with flags 2 (play once, hold the
last frame) and the -1.0 speed sentinel (the clip's own rate) - requested as a hold it simply looped until the restore.

⚠ Every rewrite needs the restart, not just the first. The wind-down swaps the held clip for the species' guard-
lowering one, but by then the saved PC sits in the loop at the tail of the sequence and the replacement has the same
shape, so without a restart the PC never reaches the records that issue the motion and the guard simply holds until the
restore snaps it away. (The clips themselves are fine - Black Dragon's and Witch Hellza's returns are fully keyframed.)

⚠ Patching a label does not disturb a script already running - its saved PC points into the old code, so the enemy
finishes its current pass before reaching the new sequence, AND a PC inside the replaced bytes would resume mid-record.
Clear the slot's saved PC and its ScriptRunning word so Step re-enters the label from the top next frame; do the same on
restore so enemies resume their real AI cleanly.

⚠ The glow anchors on a POSED bone, never the model root. A root carries an unposed transform while the engine poses
its children, so a glow hung there sits at the world origin - that was the stray sprite, and why lowering the lift made
it vanish rather than slide down his chest. The live weapon is parented to the wielder's hand bone, so reading that
pointer gives a posed bone for free (CharacterClone reads it the same way rather than trusting a bone INDEX, since
every character's skeleton differs); walking up its parents to just below the root lands on the spine.

## Open until played

The fog range semantics at the peak, the combo forward frames for swings 2-5, whether the held script ever needs its
PC restored rather than restarted, the re-request count in the log, and a flash sound. Seen in play:
the lighting flash works (ambient 50,50,50 / fog 250..650 captured on the first floor); writing `+0xEC`/`+0xF4` alone did NOT
switch a standing enemy's clip — the render-object words `_SET_MOTION` writes are what switch it.

## Toan's glow disc (an ISO bake)

`IsoPatch/ToanGlowBakes.cs`, post-step `toan-glow`, run right after the cat pack in `IsoPatcher.BakeCharacterPacks`.

The glow cave draws a texture BY NAME, and the cat's disc (`catglowp`) is baked into XIAO's dungeon pack, so none of it is
resident when Toan is the active character. This appends a disc of his own to his pack's texture bank
(`dun\mainchara\c01d.chr` → record `c01d01_dun.img`), built by the SAME builder the cat's disc uses
(`CatPackBakes.GlowT8Tim2`) from the Gallery of Time's torch glow, with the Angel Gear cat's GOLD ramp
(`CatPackBakes.GlowGold`, row 8 - a pale gold core out to a deeper gold edge) resting in its CLUT. Re-running the bake on an
already-patched ISO RECOLOURS the disc rather than skipping it, so re-authoring the ramp takes effect on the next patch. `c01d01` supplies the TIM2 headers.

⚠ **The name must not be `catglowp`.** The palette cave (`tools/stubs/cat_glow_palette.s`) finds its target by matching
that name, so a disc under any other name is never repainted and keeps its baked white for good — no cave change and no
tenth palette table.

One 8-bit 64×64 disc is 5,184 B, so his pack grows by that much (1,620,480 → 1,625,712 B) and is redirected into the
DATA.DAT tail like any grown file. The step is idempotent (the disc's presence in the bank is the check) and, like the
cat pack, it must run AFTER the sign patch — that is what creates the tail room.
