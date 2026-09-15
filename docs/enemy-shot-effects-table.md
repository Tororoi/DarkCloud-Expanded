# Enemy shot effects table

Generated 2026-09-09 from the 34-entry shot-type table (`BehaviorScriptTable` @0x27EB90, ptr array 0x27FA70), the species
table (`EnemySpeciesTable` @0x27FB00: +0x68 primary / +0x6A secondary shot index, 160 records), and a sweep of every
`dun\monstor\*.stb` for `_SET_SHOT` (cmd 133) / `_SET_SHOT2` (cmd 229).

## How the flag word is used (RE'd)

The shot type's **flags word (+0x40)** goes verbatim into the impact CollisionData entry `+0x50`. Low byte = ELEMENT bit,
high bits = STATUS bits.

- **Against the PLAYER** (`BtCheckDamageProc`, dun 0x1DBAFD0) only the status bits are read: 0x100 → Freeze (amulet 132 blocks),
  0x200 → Poison (amulet 135), 0x400 → Curse (amulet 133), 0x800 → Goo (amulet 134), 0x1000 → Stamina (65% roll, no amulet),
  0x40000 → steal gold, 0x80000 → damage = half current HP, 0x100000 → Freeze unconditionally. **The element bits 0x1–0x10 are
  never read on the player side** — no damage multiplier, no status — so vanilla play never validated them.
- **Against ENEMIES** (`CMonstorUnit::CheckDmg`, main 0x1D9F10, the path a reflected shot will take) the word is compared
  EXACTLY for an element (0x1 Fire, 0x2 Ice, 0x4 Thunder, 0x8 Wind, 0x10 Holy → resistance multiplier, "No Effect" at 0, element
  hit VFX), and tested bitwise for statuses: 0x100 → Freeze, 0x200 → Poison, 0x800 → Gooey (all gated only by the species
  susceptibility `ItemStatusRes`, 0 = immune; bosses ship 0). 0x400 (curse) and 0x1000 do nothing to enemies. A word carrying
  both an element and a status (only `f_boll_3` #27 = 0x401) reads as NO element.

**Verification anchor:** the five Gemrons use shots 5/20/23/24/25 whose element bits are exactly Fire/Ice/Thunder/Wind/Holy,
so the low-byte convention is right. Rows marked have an element bit with no supporting cue (or a cue that
disagrees) — check in-game by reflecting the shot at a same-element Gemron (expect "No Effect") or reading the hit VFX.

## Table

| # | name | element (bit) | name cue | status vs player | status vs enemies | react | base dmg | radius | speed | used by (primary) | used by (2nd) | STB dmg overrides |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | `gas_h` | none (0x0) | — | Poison (0x10) | Poison (+0x0C, 180f) | 2 | 26 | 0.1 | 0.00 | FliFli | — | FliFli 35 |
| 1 | `gas_d` | none (0x0) | — | Poison (0x10) | Poison (+0x0C, 180f) | 2 | 26 | 0.3 | 0.00 | Mask of Prajna, Mask of Prajna (Enhanced) | — | Mask of Prajna 65 |
| 2 | `nebaneba` | none (0x0) | — | Goo (0x40) | Gooey (+0x14, 180f) | 2 | 5 | 0.8 | 0.00 | Cannibal Plant, Gyon, Dark Flower, Cursed Rose, Space Gyon, Gyon (Enhanced), Cursed Rose (Enhanced), Space Gyon (Enhanced) | — | — |
| 3 | `pump_bom` | none (0x0) | — | — | — | 2 | 60 | 0.1 | 0.40 | Halloween, Halloween (Enhanced) | — | Halloween 60 |
| 4 | `ringo_ex` | none (0x0) | — | Poison (0x10) | Poison (+0x0C, 180f) | 2 | 30 | 0.2 | 0.00 | Thursday, Witch Illza | — | — |
| 5 | `f_boll_3` | Fire (0x1) | Fire | — | — | 3 | 17 | 0.1 | 0.50 | Witch Hellza, Arthur, Alexander, Dragon, Dark Genie, Gemron (Fire), Nikapous, Arthur (Enhanced), Witch Hellza (Enhanced), Alexander (Enhanced) | Mr. Blare, Bishop Q | Alexander 124 |
| 6 | `awabres` | none (0x0) | — | Poison (0x10) | Poison (+0x0C, 180f) | 2 | 45 | 0.1 | 0.00 | Gunny, Crabby Hermit, Crabby Hermit (Enhanced) | — | Gunny 26; Crabby Hermit 76 |
| 7 | `g_wave1` | none (0x0) | — | — | — | 3 | 34 | 0.1 | 1.20 | Golem, Titan, Steel Giant, Blizzard, Gol, Sil, Sil (Enhanced), Steel Giant (Enhanced), Gol (Enhanced), Titan (Enhanced) | — | Titan 90; Steel Giant 64; Blizzard 105 |
| 8 | `g_wave2` | none (0x0) | — | — | — | 3 | 66 | 2.1 | 5.00 | — | — | — |
| 9 | `magic_noroi` | none (0x0) | — | Curse (0x20) | — (curse: n/a on enemies) | 2 | 16 | 1.1 | 0.00 | Ghost | — | — |
| 10 | `magic_bin` | none (0x0) | — | Stamina (0x08), 65% roll | — | 2 | 0 | 0.8 | 0.00 | Heart, Heart (Enhanced) | — | — |
| 11 | `magic_isi` | none (0x0) | — | Freeze (0x04) | Freeze (+0x08, 300f) | 2 | 5 | 1.1 | 5.00 | Earth Digger, Auntie Medu, Lich, Moon Digger, Auntie Medu (Enhanced), Bishop Q, Lich (Enhanced) | — | Auntie Medu 60 |
| 12 | `magic_s` | none (0x0) | — | — | — | 3 | 50 | 1.8 | 0.00 | — | Heart, Heart (Enhanced) | — |
| 13 | `f_boll_2` | Fire (0x1) | Fire | — | — | 3 | 35 | 0.1 | 1.50 | Dran | — | — |
| 14 | `seedshot` | none (0x0) | — | — | — | 4 | 37 | 0.1 | 0.00 | Master Utan | — | — |
| 15 | `g_canon` | none (0x0) | — | — | — | 3 | 42 | 3.5 | 0.80 | Pirate's Chariot, Moon Bug, Pirate's Chariot (Enhanced) | — | Pirate's Chariot 69; Moon Bug 70 |
| 16 | `zibaku_f2` | Fire (0x1) | Fire | — | — | 3 | 59 | 0.0 | 1.00 | Mr. Blare, Bomber Head, Bomber Head (Enhanced) | — | Mr. Blare 90; Bomber Head 64 |
| 17 | `zibaku_r2` | Ice (0x2) | ? (r = rai/thunder?) | — | — | 3 | 60 | 0.0 | 1.00 | — | Sam | — |
| 18 | `zibaku_t2` | Thunder (0x4) | Thunder | — | — | 3 | 100 | 0.0 | 1.00 | Billy | — | Billy 110 |
| 19 | `fuki_ex` | none (0x0) | — | Poison (0x10) | Poison (+0x0C, 180f) | 2 | 31 | 4.0 | 0.00 | Tuesday | — | Tuesday 31 |
| 20 | `i_boll` | Ice (0x2) | Ice | — | — | 3 | 58 | 0.1 | 0.00 | Blue Dragon, Sam, Gemron (Ice) | — | — |
| 21 | `mikazuki_ex` | Thunder (0x4) | ? (crescent) | — | — | 3 | 70 | 0.8 | 0.00 | Crescent Baron, Crescent Baron (Enhanced) | — | — |
| 22 | `b_boll` | none (0x0) | ? (b = black/bubble) | Freeze (0x04) | Freeze (+0x08, 300f) | 3 | 70 | 1.2 | 0.00 | Black Dragon | — | — |
| 23 | `t_boll` | Thunder (0x4) | Thunder | — | — | 3 | 58 | 1.4 | 0.00 | Gemron (Thunder) | Billy | — |
| 24 | `e114a_ex` | Wind (0x8) | Wind (Gemron Wind) | — | — | 3 | 58 | 1.4 | 0.00 | Gemron (Wind) | — | — |
| 25 | `e115a_ex` | Holy (0x10) | Holy (Gemron Holy) | — | — | 3 | 58 | 1.4 | 0.00 | Gemron (Holy) | — | — |
| 26 | `last_gw2` | Fire (0x1) | ? | — | — | 3 | 130 | 1.4 | 1.50 | Dark Genie (Final Form) | — | — |
| 27 | `f_boll_3` (dup, UNUSED) | Fire (0x1) | Fire | Curse (0x20) | — (curse: n/a on enemies) | 3 | 58 | 1.4 | 1.50 | — | — | — |
| 28 | `e118a_Ex` | none (0x0) | — | — | — | 2 | 51 | 4.0 | 0.00 | Silver Gear | — | — |
| 29 | `nebaneba_b` | none (0x0) | — | Goo (0x40) | Gooey (+0x14, 180f) | 2 | 15 | 0.4 | 0.00 | Opar | — | — |
| 30 | `engetu` | none (0x0) | — | — | — | 2 | 150 | 3.4 | 0.00 | Black Knight | — | — |
| 31 | `dash` | none (0x0) | — | — | — | 3 | 170 | 2.6 | 0.00 | Black Knight Mount | — | — |
| 32 | `kamai` | none (0x0) | — | — | — | 3 | 130 | 0.0 | 0.00 | — | Black Knight Mount | — |
| 33 | `terepo` | none (0x0) | — | — | — | 3 | 90 | 0.0 | 0.00 | — | Black Knight | — |

## Rows needing in-game verification

none — verified 2026-09-09.

## Notes

- "base dmg" is the table value used when the STB `_SET_SHOT` passes no 5th argument; "STB dmg overrides" lists the per-species
  values the scripts pass (via `SetDmg` → sub-shot +0xA010). See `docs/enemy-attack-damage-table.md` for the full per-enemy view.
- react: 2 = guardable knockback, 3 = unguardable knockdown (guard-break), 4 = light.
- radius = the shot's own collision radius (+0x18) added to the target's body radius (+6 vs the player).
- Shot types with no user in the species table (8 g_wave2, 14 seedshot, 16-18 zibaku_*, 27 f_boll_3 dup) may be reached only by
  effect entities or are unused; the zibaku (self-destruct) trio carries Fire/Ice/Thunder bits by its suffix f/r/t.
- `_SET_SHOT2` (second shot slot, species +0x6A) is scripted by: Billy, Heart, Mr. Blare, Sam, __e116a, e116a, e150a.
