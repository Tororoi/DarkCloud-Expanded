# Claymore — "Greatsword"

While the Claymore (item 288, model `c01w31`) is in Toan's hand on a dungeon floor, the blade is 1.8× its size,
reaches 1.8× as far, and every hit throws its enemy the way the Baselard's does. Code: `Weapons/Toan/Claymore.cs`,
started from `WeaponThreads` (`case Items.claymore`). No ISO patch.

## Measurements (`dun\item\main_wep\c01w31.mds`)

Root `w31` (bind = identity) → mesh `c01w31__m` (vertices z −2.63 … 8.95) → four collision bones:

| bone | local Z |
|---|---|
| dcol0 | 1.648 |
| dcol3 | 6.592 |
| dcol1 | **10.384** (the only bone the swing code reads) |
| dcol2 | 12.533 |

The menu model's dcol1 is the same 10.384. Swing spheres: combo 2.8 / 5.3 / 6.2 (ELF floats), lunge 6, whirlwind 12
(the charge radii words). Enemy body spheres are mostly 5–7.5 (docs/enemy-body-collision-table.md).

## How it works

1. **Blade ×1.8.** The root frame's local 3×3 is set to 1.8 × identity (the same lever Heaven's Cloud uses on `w14`), so
   the visible blade and the dcol bones under it grow together: the hit point rides at S·Z ≈ 18.7 from the hand instead
   of Z ≈ 10.4 (S = 1.8). The write is idempotent — identity → scaled, already scaled → untouched — so nothing can compound it
   across a restart, a floor change or a rebuilt model. Any other matrix is left alone (logged once).
2. **Close-range spheres.** A hit sphere at S·Z with radius r clears anything whose body edge is closer than about
   S·Z − r − R. So every enemy whose body edge (unit position less its largest live sphere) is within S·Z of Toan gets
   (S − 1)·Z ≈ 8.3 added to each body sphere radius, and gets stock back when farther. For a gated enemy this puts the
   near edge exactly where the stock blade's was (Z − r − R): the hit sphere moved out by (S − 1)·Z and the target grew
   by the same. Straight
   behind Toan the geometry is identical to stock; to the sides it is a little more generous, in line with the
   bigger blade. Nothing past the gate is inflated, so nothing beyond the visible blade is ever hit. The lunge and
   the whirlwind read the same bone, so they follow — the whirl sweeps dcol1 around Toan at S·Z with its 12-unit
   sphere, and the bonus closes the hole that leaves in the middle.
3. **Knockback.** `MeleeKick.Set(Baselard.KickStrength, Baselard.KickDecay)` (2.02 fading 0.12 ≈ 17 units) while
   Toan is active; restored when an ally is out or the sword goes.

Stock radii are captured on first sight per slot and part; a radius found holding neither stock nor stock + bonus was
rewritten by something else (a miniboss scale-up) and is taken as the new stock. All restored on exit. The whirl
visual is sized to the scaled blade (`Weapons.BladeFactor`).

Super Steve with a Claymore sphere inherits the throw only (her pellets kick as with the Baselard sphere, via
`Baselard.DriveSphere`), not the size. Point-blank and overhead-swing behaviour needs an in-game pass.
