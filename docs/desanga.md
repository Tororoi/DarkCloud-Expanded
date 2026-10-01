# DeSanga — kill heal (`Weapons/Ungaga/DeSanga.cs`)

While DeSanga is Ungaga's equipped weapon, every enemy he kills heals it 5 WHP: the equipped weapon record's current WHP
(`WeaponHave.InventoryWeaponWhpOffset` +0x10, float — the live value the engine's own drain bills, `WeaponWhp`) raised by 5 per
kill up to its maximum (`InventoryWeaponMaxWhpOffset` +0x0C, short). A kill is Macho Sword's test: an enemy slot's HP crossing
from > 0 to ≤ 0 while it is still on the floor (`RenderStatus` ≥ 1), with the slot's killer id (`KillerCharId`) the active
character. Several kills in one tick heal 5 each. No Super Steve sphere inheritance.
