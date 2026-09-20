# Hardshooter — ricochet

`Weapons/Xiao/Ricochet.cs` (thread `CustomXiaoEffects.HardshooterEffect`; Super Steve drives it from `SuperSteveEffect`
when a Hardshooter SynthSphere is attached; Double Impact drives it for its pair).

A pellet that lands on an enemy spawns a second pellet at the exact impact point that flies at the next nearest enemy
— the living one nearest the enemy just hit, within 120 units, aimed at its hurt-sphere centre nearest the impact — or
in a random direction when none is near. A ricochet never ricochets again (a per-slot mark). The second pellet is a
real one in the player's shot pool (written as `ShrapnelBurst` writes its fragments), so it flies, collides and plants
its own damage entry with the weapon's flags; it carries the first pellet's damage word and ground speed, flat.

## The hit (RE)

`step__5CSHOT` tests a live pellet BEFORE moving it: `checkCollision(2.0, out, pos, vel, 2)` walks every living
enemy's active hurt spheres (`BodyCollision`: centres rebuilt each frame at 0x55250, radii at 0x55390, active flags at
0x55450, 16 per slot) and returns 3 when the pellet's position lies within 2 + radius of a centre — the walls are tested
after — and the pellet then plants its damage entry and dies where it stands. A dead slot keeps its position, velocity
and damage word. So the driver reads the death point exactly and applies the game's own rule to the live spheres: the
enemy whose sphere holds the point is the one hit; no sphere means a wall or the end of its flight (a guarded hit dies
the same way, so it ricochets too).

## The departure

The ricochet is written at the death point with its collision OFF (+0x280), which in this engine also stops the game
moving it (movement and collision share one block — `ShrapnelBurst`'s lesson). The driver steps it along its new line
each tick until it stands clear of every sphere of the enemy it came from by 2 + radius + one frame of travel — so the
game's next test, made before the move, passes — then turns collision on and the game flies it. It cannot strike the
enemy it came from. Should it reach another enemy's sphere while still on the driver's steps, collision is turned on at
once and the game resolves that hit; a departure never stays on the driver's steps longer than 30 ticks.
