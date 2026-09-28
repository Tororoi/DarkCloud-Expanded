# Shamshir — "Swift Strikes"

While the Shamshir (item 270) is in Toan's hand on a dungeon floor, his five combo swings play a third faster (KEY step ×4/3, 0.3 → 0.4).
The charge attacks (lunge, whirlwind) are untouched. Code: `Weapons/Toan/Shamshir.cs`, started from `WeaponThreads`
(`case Items.shamshir`). No ISO patch.

## Mechanism

A motion's play rate is the KEY step of its Mot_List entry (CCharacter +0x344; 0x10 an entry: start frame, end frame,
step), which `Step__10CCharacter` reads every frame. The five combo entries of Toan's dungeon set are raised in place:

| motion id | frames | stock step | ×4/3 |
|---|---|---|---|
| 36 | 820–830 | 0.3 | 0.4 |
| 37 | 830–838 | 0.3 | 0.4 |
| 38 | 838–847 | 0.3 | 0.4 |
| 39 | 847–857 | 0.3 | 0.4 |
| 40 | 856–884 | 0.3 | 0.4 |

Every entry is checked against its frame range before a write, so a list that is not Toan's (an ally out, a reload)
is never touched; a step holding neither stock nor the raised figure is left alone. The list address is remembered
so the steps go back to 0.3 on unequip, on an ally swap, or when the list moves.

## Why the raise is safe

`ToanKey_Play` gates each swing's hit on a window of the frame cursor — 825–828, 833–835, 842–844, 851–854,
862–866 — and the chain inputs on windows exactly one frame wide (824–825, 832–833, …). The cursor advances by the
step each tick, so a step of 0.4 still lands in every window; a step above 1.0 could jump a chain window, which is
the ceiling for the factor. The motion-speed override at +0xC60 was not used: `motionDrive` resets it to −1 on every
motion change, so a poll would miss the first ticks of each swing, whereas the list entry holds.

## Super Steve sphere

A Shamshir sphere speeds Xiao's shot (`Shamshir.DriveSphere`, from the Super Steve dispatch):

| motion id | c04b | frames | stock step | raised |
|---|---|---|---|---|
| 11 | draw | 240–251 | 0.7 | 1.12 (×1.6) |
| 13 | shoot | 251–255 | 0.7 | 0.95 |

The draw hands over to the hold or the shoot inside [end − 2, end] = [249, 251], a two-frame window, so 1.12 still
lands. The hold (12, frame 250, step 0) is a parked loop and is left alone.

⚠ The shoot's step must stay under 1.0: `BattleActionPlay_Jinn` (dun 0x1DBC930) releases the pellet only while the
cursor is strictly inside (251, 252) and the shoot motion starts at exactly 251, so a step ≥ 1.0 means the shot never
fires (Quick Draw relies on the same window, dropping the cursor to 251.0). 0.95 is the ceiling. Entries are
frame-validated as Toan's are, and restored when the sphere or the dispatch goes.
