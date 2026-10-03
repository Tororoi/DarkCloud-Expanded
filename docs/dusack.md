# Dusack — "No Fool's Gold"

While the Dusack (item 293) is in Toan's hand on a dungeon floor, mimics and king mimics can be hit while they wake.
Code: `Weapons/Toan/Dusack.cs`, started from `WeaponThreads` (`case Items.dusack`). No ISO patch. The Dusack also
inherits Swift Strikes (docs/shamshir.md) and Fine Fare (docs/sax.md), adding Treasure Key → Gold Bullion to the chest upgrades.

## Mechanism

A chest-mimic's wake is a guard: the monster-script bake (`MonsterScriptBakes`, mode `guard_wake`) replaced the wake's
`_STATUS_SET_MUTEKI(100)` with `_SET_GUARD_FRAME(10, 28)` (kings: 10, 27), the "appear" clip's frames, registered the
moment the box opens. Guard windows live at `MainMonstorUnit + slot*0x20`: active flags (short[3] @+0x60550), start
frames (float[3] @+0x60558) and end frames (float[3] @+0x60564); `CMonstorUnit::CheckDmg` drops every hit that lands
inside an active window. Dark Cloud and 7th Heaven open every window of every enemy and the Divine Beast cat's hit passes
any (docs/guard-gate.md). No Fool's Gold opens only the window whose frames are the baked wake (start 10, end 28 or 27),
only on the 14 mimic and king mimic species, through the guard gate's per-enemy mask (`GuardGate.IgnoreWindows`; the
window itself is never written), while the sword is out and Toan is. A mimic's own guard motion (block frames 170–189) keeps blocking.

The wake flag is captured on first sight per slot and species and re-captured when the slot changes occupant; a flag found
armed after the capture (the wake registers them late) is remembered so the restore is complete. The wake's
invincibility frames (`_STATUS_SET_MUTEKI` 100) are the mimic's own and are left alone, as the other three leave them.

The Brave Ark inherits it (`Dusack.Grants`; its `WeaponThreads` case starts the same thread), and Super Steve with a
Dusack or Brave Ark sphere has it for her pellets (`Dusack.DriveSphere`, from the sphere dispatch). Dark Cloud and
7th Heaven do not inherit it: their Guard Crush already breaks every guard, the wake included.
