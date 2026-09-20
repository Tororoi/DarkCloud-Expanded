# Skunk — twice the flamethrower's reach

`Weapons/Osmond/Skunk.cs` (thread `CustomOsmondEffects.SkunkEffect`).

Osmond's flame gun mode (gun type 2, set per weapon by `Get_Main_EffectPtr` — the Blessing Gun 368 and the Skunk 369;
`BattleActionPlay_Ozumond_F`, dun 0x1DBE3A0) fires a `CSHOT_FIREBAR` (0x1EFB2F0): 24 flame particles laid along the
aim, each 2.0 units past the last — `Init` places them (0x1AEB20) and `Set` re-aims them every frame (0x1AED40), both
scaling the normalised aim by an immediate 2.0 (`lui v0,0x4000; mtc1 v0,f12`) — so the flame reaches 46 units. Each
particle plants a radius-4 collision sphere once per 30 frames (`Step`, owner 5, attack kind 6) and fades over its own
life.

`ElfPatches.PatchFlameSpacing` makes both loads read `Mailbox.FlameSpacing` (0x01F100F8) instead: the PNACH re-seeds
2.0 every frame while the owner word (0x01F100FC) is 0; the Skunk driver owns the word and holds 4.0 while the weapon
is equipped — 23 × 4 = 92 units — and writes 2.0 back when it goes, so the Blessing Gun keeps its vanilla reach. At
4-unit spacing the radius-4 spheres still overlap, so the longer flame has no gaps. Needs the patched ISO and the
updated PNACH. This took the mailbox's last two words: the next runtime word goes to the free band below the ELF caves.
