# Double Impact — two pellets a shot

`Weapons/Xiao/DoubleImpact.cs` (thread `CustomXiaoEffects.DoubleImpactEffect`; Super Steve drives it from `SuperSteveEffect`
when a Double Impact SynthSphere is attached).

Every shot is two pellets. The one the game fires is joined by a twin the tick it appears in the player's shot pool
(`PlayerShotPool`, 12 slots): a second real pellet, written the way `ShrapnelBurst` writes its fragments — position 5
units behind the pellet on its flight line, the same velocity and life, collision on, live flag last. `step__5CSHOT`
flies and collides it like any pellet and plants its own damage entry with the weapon's ability flags, so each pellet
rolls the weapon's steal / poison / critical / drain on its own and each does its own damage.

Each pellet carries 0.75 × the shot's attack: the game's damage word on the fired pellet is scaled down when the twin
is made, and the twin gets the same figure (the word is the attack the entry is planted with; the enemy's defense
applies on the hit). The Double Impact sprite already shows two pellets, so the twin's sprite scale (+0x310) is 0.001 —
not seen, and the scale word sizes the sprite only, never the hitbox.

A twin is never twinned again (a per-slot mark, cleared when the slot goes quiet); a full pool leaves that shot single.
