# Double Impact — two pellets a shot

`Weapons/Xiao/DoubleImpact.cs` (thread `DoubleImpact.DoubleImpactEffect`; Super Steve drives it from `SuperSteveEffect`
when a Double Impact SynthSphere is attached).

Every shot is two pellets. The one the game fires is joined by a twin the tick it appears in the player's shot pool
(`PlayerShotPool`, 12 slots): a second real pellet, written the way `ShrapnelBurst` writes its fragments — the same
velocity and life, collision on, live flag last — beside the first: the pair straddles the flight line 2 units apart,
side by side in the ground plane (the first pellet is moved 1 unit to one side, the twin placed 1 unit to the other).
`step__5CSHOT` flies and collides each like any pellet and plants its own damage entry with the weapon's ability flags,
so each pellet rolls the weapon's steal / poison / critical / drain on its own and each does its own damage.

Every pellet is drawn as the Steel Slingshot's single stone: while the weapon is out the driver holds
`Mailbox.PelletSpriteId` at the Steel Slingshot's id (the ISO's pellet-sprite hook, docs/super-steve-sphere-icon.md), so
the pair reads as two stones rather than the weapon's own two-stone cell twice. The word goes back to 0 when the weapon
goes.

Each pellet carries 0.75 × the shot's attack: the game's damage word on the fired pellet is scaled down when the twin
is made, and the twin gets the same figure (the word is the attack the entry is planted with; the enemy's defense
applies on the hit).

Both pellets ricochet as the Hardshooter's do (`Hardshooter.Drive`, driven from here; docs/hardshooter.md), each at a
different target — a target one pellet takes is left to it for the next 250 ms, so the second picks the next nearest,
or flies in a random direction when no other is near. A ricochet is never twinned; a twin is never twinned again (a
per-slot mark, cleared when the slot goes quiet); a full pool leaves that shot single.
