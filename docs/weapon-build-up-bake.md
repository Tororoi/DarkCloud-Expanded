# Weapon build-up trees baked into the ISO

`ElfPatches.PatchXiaoBuildUp`. The weapon template table (`WeaponList`, `Weapons.buildup` = 0x2027A748 + a character
offset + 0x4C per weapon) holds each weapon's build-up word at +0x3C: bit k set = the weapon may be built up into item
299 + k (for Xiao: 300 Wooden … 313 Angel Gear; Steel's 0x40 = Hardshooter, the vanilla Hardshooter's 0x1080 = Double
Impact or Matador). `Weapons.cs` still writes some characters' words at runtime; Xiao's are baked, guarded on the vanilla
words:

| weapon | vanilla | baked |
|---|---|---|
| Hardshooter (305) | 0x1080 — Double Impact, Matador | 0x0080 — Double Impact |
| Double Impact (306) | 0x0200 — Divine Beast Title | 0x1000 — Matador |
