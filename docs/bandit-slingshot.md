# Bandit Slingshot — a stolen projectile

`Weapons/Xiao/BanditSlingshot.cs` (thread `CustomXiaoEffects.BanditSlingshotEffect`; the shot through `BorrowedShots`).

A steal that lands on an enemy with a projectile takes the projectile: until Xiao steals another or the floor ends,
every pellet she fires is that enemy's shot — the species' primary shot config as the static table holds it, flags and
all (a stolen Black Dragon ball keeps its Freeze), the victim mask alone turned on enemies — at 2 × the shot's attack
(the pellet's latched Attack word, doubled, planted as the shot's base damage; `CheckDmg` applies the rest) and with the
shot's own element (the config's flags word reaches the entry's +0x50). No enemy shot carries both an element and an
ailment, so nothing is lost to `CheckDmg`'s exact element compare.

## The steal

The weapon's native Steal ability rolls 10 % per hit in `CheckDmg` and spawns the stolen item as a `CStealItem` (the pool
at 0x1EA8300: eight slots, position +0x10 + i × 0x10, state +0xD0 + i × 4 (−1 free, 0 rising, 1 flying, 2 arrived), item
id +0x134 + i × 4) that flies to Xiao. The driver watches the states: a slot leaving −1 is a proc; the victim is the
living enemy nearest the slot's spawn point (within 40 units); its projectile is `EnemySpeciesTable` +0x68 of its
species, or +0x6A when the primary is a self-detonation (the zibaku family, configs 16–18: the enemy bursts where it
stands, nothing flies — never taken; Mr. Blare gives his fireball, Billy his thunder ball); neither = the item alone. `BorrowedShots.TableConfig(cfg, keepFlags: true)` enters it into the main-character
effect instance (the provider `WantedShot`), and every pellet is replaced the tick it appears, as Dragon's Y's charged
one is: fired from the pellet's position with its velocity and life, the pellet flagged off.

## The notice

The item's arrival (`checkEvent`, dun `OPAnalyz` / `OpA_DrawProcess`) calls `BtGetAttach_Init` → `ItemGetMes` →
`SetSystemMes(10 / 20 / 30 by item id: ≤ 0x50 → 10, ≤ 0x90 → 20, ≤ 0x100 → 30, else 10)` → `MakeMesWin` from the
notices bank `meswin/system_ae.bin` (main BSS `mes_data`, 0x1CFCE00, 48,000 B; the file is 34,406 B). The templates read
`"[item]" acquired.` (0xFBFE = the item name, 0xFBFA = the count). The ISO bake (`MesTextBaker.RelocateMes`) moves those
three templates into 80-word blocks at the bank's end and repoints their index entries — the count is unchanged, no
other message moves. On a steal the driver rewrites the block the item will use — the template's words, 0xFF00 (a line
break), "[enemy]'s projectile is now yours", 0xFF01 — and puts the template back 3 s after the item arrives (8 s after
the proc at the latest, or at the floor's end). A chest opened inside that window would show the line too.

## Lessons

- `meswin/systeme.bin` is the NAMES bank (`SetBuff_system`: places, monsters); the notices are `system_ae.bin` (the
  `LanguageCode` 0 file), loaded into `mes_data` by `InitSystemMes` and read by `MakeMesWin`.
- `CStealItem::checkEvent` only hands back the arrived item id; the pickup presenter is the caller's.
