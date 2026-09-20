# Bone Slingshot — the skeleton key

The Bone Rapier's ability, shared: while the Bone Slingshot is equipped, bone doors open without their key
(`BoneRapier.BoneRapierEffect(true)` — `Dungeon.SetBypassBoneDoor` — driven from the Xiao weapon switch in
`Dungeon.cs`), and the Bone Rapier's door-opening line ("Rattle me bones!") plays through the same `BoneDoorTrigger`
thread, which now runs for either weapon. Super Steve carrying a Bone Slingshot sphere has the key as it does with a
Bone Rapier sphere. The Xiao switch's force-clear of the bypass exempts the Bone Slingshot alongside Super Steve.

## No revival

Both bone weapons also keep the undead down, as the Cross Hinder does: `BoneRapier.BoneKeyNoRevivalEffect`
(thread `boneNoRevivalThread`, started from the Bone Rapier, Bone Slingshot and Super Steve switch cases) patches
the loaded death scripts of the reviving undead once per floor — the revive roll's threshold literal set to 0 through
the Cross Hinder's own `PatchUndeadRevivers` sweep — while the key is wielded (`BoneKeyWielded`: either weapon, or
Super Steve with either sphere), and restores the literals when the key goes or the sphere changes. The Cross Hinder's
own thread does the same for itself; the two never run for the same character at once.
