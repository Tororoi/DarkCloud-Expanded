# Scorpion — venom (`Weapons/Ungaga/ScorpionVenom.cs`)

| | |
|---|---|
| proc | the weapon's own Poison ability, rolled by the engine in CheckDmg — seen as an enemy slot's poison timer (`EnemySlotOffsets.PoisonPeriod` +0x0C, 0 at rest) going 0 → non-zero while the weapon is out; slots already poisoned when it came out are skipped; a slot is watched again once its timer is back to 0 |
| cure | on every proc, the wielder's Poison bit (0x10) cleared from their status word (`Player.Ungaga.status` 0x21CDD824 / `Player.Xiao.status` 0x21CDD818 — Toan's and Xiao's addresses are proven by Brave Ark's cure; the status words are 4 apart) |
| ABS | on every proc, half the enemy's worth (its slot's ABS, `EnemySlotOffsets.Abs` +0xB0, integer half) added to the equipped weapon record's ABS (+0x14), up to the weapon's maximum (`MachoSword.MachoMaxExp`, the engine's GetWeaponMaxExp); a broken starting weapon gets none; an ABS already above the maximum is left alone |
| sphere | Super Steve with a Scorpion SynthSphere has both (`ScorpionVenom.Wielded`; started from Xiao's Super Steve case in `WeaponThreads`) — whenever poison lands from her shots |
| Mikara | (planned, not in this build) a "Can you remove the poison?" option with Mikara in Muska Lacka while the Scorpion is owned → "Of course, I'll do whatever I can to help you on your journey." → Fruit of Eden. Not inherited by the sphere |
