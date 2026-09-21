# Sun Sword — Solar Flash

Hold guard with the Sun Sword and the blade whitens over 1.5 s; at full it is PRIMED (the game's own charge-complete
pulse) and stays so, guard or not. The next attack carries the charge: as the swing comes forward the blade goes back to
its own colour and the dungeon flashes blinding white, easing back over a second. Every enemy within 300 units takes a
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
  (`MoveControl.SpeedAddr`, unit+0x1E450) is zeroed once, since nothing rewrites it now, so the enemy stops where it
  stands; its facing (`+0x60`/`+0x68`) is pointed at the flash spot each tick. The hold starts 0.1 s after the flash so
  CheckDmg's label-110 reaction has requested the damage clip; once that clip is seen past its end (or 1.5 s, never
  before 0.35 s) the guard raise is requested exactly as `_SET_MOTION` (ELF 0x1E1710) requests a motion — the render
  object's motion id, flags 0 and the KEY speed from the model's motion table (halved while Gooey), body and parts, plus
  the slot's request words — then the hold loop when the raise has played out (it loops on its own); a species with no
  guard gets its idle. A clip not seen playing is requested again after 0.25 s (counted in the log; should be rare now).
  When the timer ends the PC and running word are cleared and Step starts label 100 afresh. A death during the hold is
  the engine's: the moment the slot's label (`CRunScript.Label`, ctx+0x2C) is the death label's funcdata the hold lets
  go. (Freeze status was tried and rejected; a lost-target redirect through the aggro table stopped the walking but the
  AI kept re-requesting its own clips every quarter second.) Per-species idle / damage / guard clips:
  `GameData/EnemyGuardMotions.cs`, generated from docs/enemy-motion-table.md by
  `tools/analysis/gen_enemy_guard_motions.py` (111 of 158 species have a guard).

## Tuning knobs (constants)

`SunSword`: ChargeSeconds 1.5, FlashRadius 300, FlashDamageFraction 0.25, Kick 2.0/0.3, HitLifeTicks 3, FlashPulseSpeed
90, FlashSe 0 (no sound yet — a resident SE id goes here), the five combo forward frames. `SolarBlade.WhiteMax` 200.
`SolarLighting`: EaseSeconds 1.0, FogStart/End 0/1, FogEasePow 2. `SolarStun`: StunSeconds 5, ParkDelay 0.1, StaggerMin 0.35, StaggerCap 1.5, RequestRetry 0.25, YieldOps 18.

## Open until played

The fog range semantics at the peak, the combo forward frames for swings 2-5, whether the held script ever needs its
PC restored rather than restarted, the re-request count in the log, and a flash sound. Seen in play:
the lighting flash works (ambient 50,50,50 / fog 250..650 captured on the first floor); writing `+0xEC`/`+0xF4` alone did NOT
switch a standing enemy's clip — the render-object words `_SET_MOTION` writes are what switch it.
