# Steel Slingshot — half the WHP cost when low

`Weapons/Xiao/SteelSlingshot.cs` (thread `SteelSlingshot.EnduranceUpEffect`).

While the weapon's HP is low — the game's own warning state, WHP ≤ 10 % of the maximum (`LowWhpWarningFraction`, the
HUD gauge blinking) — each shot costs half the WHP. Xiao drains per shot: `BattleActionPlay_Jinn` passes the mailbox
word `XiaoShotWhpFactor` (the ISO's dun.bin patch) to `SwordDmgCheck1`, and `BattleSubWeaponDmg` computes
`(1.5 − 0.01 × Endurance) × factor`, halving for Durable and doubling for Fragile on top — so those stack with the half
by themselves. `ChargedShotWhp` gained a `Base` factor: what an ordinary shot costs and what the word returns to after
a charged one (a charged factor multiplies it). The driver sets it to 0.5 while low and 1.0 otherwise, and back to 1.0
when the weapon goes. Super Steve carrying a Steel Slingshot SynthSphere has the half too (driven from
`SphereInheritanceEffect`, its own WHP being the low one); the level-up bonus below is the Steel's alone.

## Level-ups (baked)

Every level-up's BONUS is doubled: +2 endurance instead of +1, and twice the max-WHP roll. The attached items' sums
are untouched. A level-up's growth is spread over three routines: `SetLevelUpValue` (the menu's preview record: each
stat gains the attached items' sum — left alone), `SetLevelUpWeaponData` (the commit: attack / endurance / speed /
magic +1 each, max WHP +1 + rand%3), and `WeaponLevelUpValueCalc` (the item-use path's one-shot version of both,
`ItemUseFunc`). `ElfPatches.PatchSteelLevelUp` turns the endurance and max-WHP adds of the last two into calls into
`tools/stubs/steel_level_up.s` (DebugInfomationDraw's body, 0x1B42E0): the cave reads the item id off the record being
grown and, for 301, makes the +1 a +2 and adds the max-WHP roll (1 + rand%3) a second time; every other weapon gets the
vanilla add. The 99 caps stay.
