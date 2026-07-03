# Game Formulas — Weapon Damage

How Dark Cloud computes the damage of every player attack from the weapon's **Attack**,
**Magic**, **element**, and **anti-enemy (slayer)** stats. Reverse-engineered from the USA ISO
(`SCUS_971.11` + the `dun.bin` battle overlay) with the Ghidra EE toolchain
(`tools/ghidra/decompile.sh`) plus targeted disassembly. All addresses are **native ELF**
addresses; add `0x20000000` for PCSX2 EE memory (repo convention).

Verified against: `CMonstorUnit::CheckDmg` (0x1D9F10) — the single function that applies
damage to a monster — and every player-side attack routine that feeds it (see §2).

---

## 1. The hit pipeline

Every damage source works the same way:

1. The **attacker** registers a hit sphere in the global collision pool `NowColData`
   (pointer @ `0x2A35E0`, 96 entries × 0xA0 bytes) via `CCollisionData::Set` (0x1B57A0),
   passing a **pre-computed integer damage value** (stored at entry `+0x34`).
2. The attacker (or projectile step code) then stamps **weapon metadata** onto that entry:

   | Collision entry field | Meaning | Player-attack source |
   |---|---|---|
   | `+0x34` | base damage (int) | latched Attack × move multiplier (§2) |
   | `+0x50` | element attribute bits | `GetWeaponElementAttr(weapon+0x16)` (§4) |
   | `+0x58` | attacker character id (0=Toan 1=Xiao 2=Goro 3=Ruby 4=Ungaga 5=Osmond, −1=none) | `basic_damage` arg 2 / shot user id |
   | `+0x60` | attack kind (0=swing, 1=Goro smash, 2=lunge, 3=windmill, 6=Osmond gun, …) | `basic_damage` arg 1 |
   | `+0x64` | pointer to weapon anti-enemy byte array | `NowWeaponHave+0x1C` |
   | `+0x68` | category restriction (−1 = none; else damages only that monster category) | `CSHOT_EFFECT::SetEnemyAttr` |
   | `+0x6C` | weapon ability flags | `*(short*)(NowWeaponHave+0xEE)` (§6) |

   For melee this is done by `basic_damage(attackKind, charId)` (0x241370) right after
   `CCollisionData::Set`. Projectiles carry the same data (`SetDmg` / `SetAttribute` /
   `SetVsMonster` / `SetWepStatus`) and their step code writes identical fields —
   confirmed for `CSHOT_EFFECT::Step` (0x1AC180), Xiao's stones in `CSHOT::step`
   (0x1ABD10), and Osmond's bullets in `CSHOT_MACHINGUN::Step` (0x1AE750).

3. Each monster's `CMonstorUnit::CheckDmg` (0x1D9F10) tests its hurtboxes against the pool
   and runs the **damage formula** (§3). There is no other damage path: element, anti-enemy,
   defense, and abilities are all applied monster-side in this one function.

---

## 2. Base damage — what each attack latches

Every attack starts from the equipped weapon's **effective Attack** stat
(`*(short*)(NowWeaponHave+4)`; `NowWeaponHave` ptr @ `0x2A34F4`, see §7 for how effective
stats are built), then applies a per-move multiplier. If the player has **buff status 8**
(`StatusErrCheck(8)`, 0x1B1930) the latched value is **doubled** — every character checks this.

| Character | Attack | Damage latched | Notes |
|---|---|---|---|
| Toan (0) | combo hits 1–4 | Attack × 1.0 | swing radii 2.8 / 5.3 / 6.2 / 6.2 (`0x2A1C68/6C/70`) |
| | combo hit 5 | Attack × **1.8** (`0x2A1C74`) | |
| | lunge (charge ≥ 1.5) | Attack × 1.5 | attack kind 2 |
| | windmill spin (charge ≥ 2.5 + unlock `UserStatus+0x4324`) | Attack × 1.5 | attack kind 3, radius 12.0 |
| | windmill projectile | Attack × 1.5 | `CSHOT_EFFECT`, full weapon data |
| Xiao (1) | slingshot stone | Attack × 1.0 | latched at `BattleActionPlay_Jinn` entry into `NowShotData` slot `+0x2E0` (pool ptr @ `0x2A35D4`, 12 slots, 120-frame life). On impact `CSHOT::step` registers the hit (radius 3.0) reading element/anti/ability data from `NowWeaponHave` **at impact time**. Her hits never cause flinch. |
| Goro (2) | swing | Attack × 1.0 | radius 5.0 |
| | charged smash | Attack × 1.5 | attack kind 1 |
| | smash shockwave | Attack × **1.8** (1.2 (`0x2A1AF8`) × 1.5) | projectile |
| Ruby (3) | normal shot | (Attack/2 + Magic/2) × 1.0 | integer halves, then float multiply + truncate |
| | partial-charge shot | (Attack/2 + Magic/2) × 1.5 | |
| | full charge (meter 60) | **two** shots, each (Attack/2 + Magic/2) × **2.2** (`0x2A1ACC`) | |
| Ungaga (4) | all 6 combo windows | Attack × 1.0 | two hit spheres per window (dual-ended spear), radius 6.0 |
| | charge projectile | Attack × 1.5 | |
| Osmond (5) | standard gun | (Attack/2 + Magic/2) per bullet | via `CSHOT_MACHINGUN`; attack kind 6 (no flinch). **Fire interval = 1.5 − 0.01 × Speed** — the only place weapon Speed affects combat directly. |
| | alt modes (`_H`, `_F` gun types) | Attack × 1.0 | mode selected by gun type (global `0x1DC4520`) |

Ruby and Osmond are the only characters whose **Magic stat feeds base damage**
(`base = Attack/2 + Magic/2`). For everyone, Magic also feeds the element bonus (§4).

---

## 3. The damage formula (`CMonstorUnit::CheckDmg`, 0x1D9F10)

Applied in this exact order. `wep` = the attacking character's **currently equipped**
weapon record (looked up via `UserStatus + charId*0xAA8 + activeIdx*0xF8` — read at
impact, not snapshotted at fire time). Monster fields are in its live battle record (§8).

```
dmg = (float) col.damage                                 // col+0x34

// 1. Distance scaling — RANGED characters only (charId 1, 3, 5)
d = distance(player, monster)
if d <= 20:      dmg *= 1.5                              // point-blank bonus
elif d >= 50:    dmg *= max(0.5, (100 - (d - 50)) / 100) // linear falloff, floor ×0.5 at d≥100

// 2. Defense
def = monster.Defense                                    // live rec +0x1E460 (short)
if charId == 3:  def /= 2                                // Ruby ignores half defense
dmg = dmg - def
if dmg <= 0:     dmg = 1                                 // minimum 1 before bonuses

// 3. Element (only if the weapon HAS an element: col.attr != 0)
e     = index of col.attr bit                            // 0=Fire 1=Ice 2=Thunder 3=Wind 4=Holy
bonus = dmg * (0.004 * wep.elementValue[e]               // byte  wep+0x17+e   (coeff @0x2A1D84)
             + 0.005 * wep.Magic)                        // short wep+0x0A     (coeff @0x2A1D74)
dmg   = (dmg + bonus) * monster.elementPct[e] / 100      // short +0x1E3FA+2e; 100 = neutral
// if this drives dmg to <= 0 the hit shows as a blocked "no damage" flash

// 4. Anti-enemy (slayer) — always, vs the monster's category
dmg += dmg * 0.015 * wep.antiValue[monster.category]     // byte wep+0x1C+cat (coeff @0x2A1B78)
                                                         // category: short +0x1E3F8

// 5. Per-hurtbox, per-character percent (boss part gating)
dmg = dmg / 100 * partPct[part][charId]                  // int @ +0x555D0 + part*0x18 + charId*4

// 6. Restrictions & states
if col.categoryLimit != -1 and monster.category != col.categoryLimit: dmg = 0
if monster.enrageTimer > 0:  dmg /= 2                    // +0x1E3E0 (monster-vs-monster rage)
if charId == -1:             dmg *= monster.pctFromMonsters / 100    // +0x1E4AC

// 7. Final
dmg = ceil(dmg)  (min 0)
monster.HP -= dmg                                        // +0x1E3F4; kill -> AddKills
```

Key consequences:

- **Element bonus multiplies what's left after defense**, so element and magic matter more
  against low-defense enemies (percentage-wise the bonus factor is constant:
  `1 + 0.004·elem + 0.005·magic`, e.g. 99 element + 50 magic ≈ ×1.65 before resistance).
- A weapon with **no element** (element index 5 → attr bits 0) completely skips step 3,
  including the monster's element-percent multiplier.
- **Anti-enemy** is a flat `+1.5 % per point` against the matching category (99 points ≈
  ×2.485 with the element factor already applied). It applies after the element multiplier,
  so it stacks multiplicatively with it.
- Monster element percents below/at 0 (immune/absorb-style entries) zero the hit and show
  the grey "no damage" feedback instead.

### Status procs on hit (before the damage math)

From the weapon ability flags on the collision entry (col+0x6C, §6), each gated by the
monster's **status susceptibility %** (`+0x1E4AE`, `rate`; 0 = immune):

| Flag | Ability | Chance | Effect on monster |
|---|---|---|---|
| 0x80 | Steal | 10 % (no rate gate) | spawns `CStealItem` with monster's held item (`+0x1E4A8`), once |
| 0x20 | Poison | 10 % AND rand < rate | poison timer `+0x1E3DC` = 180: every 180 frames deals 10 % of max HP (`+0x1E3F0`), re-arms |
| 0x40 | Stop | 4 % AND rand < rate | stop timer `+0x1E3D8` = 300 (grey tint); hitting again toggles it off |
| 0x400 | Drain | if final dmg ≥ 100 | heals attacker for 1 % of the damage dealt |
| 0x1000 | Critical | 1 % | damage becomes the monster's **remaining HP** (instant kill); blocked when monster type `+0x1E410` == 2 (bosses) |

Monsters can also **guard**: if the hit lands during a guard window (`+0x60550` table) the
damage branch is skipped entirely (spark + guard sound, WHP still drains at ×0.1).

---

## 4. Element system

- `element_tbl` @ `0x26B1B0` = `{1, 2, 4, 8, 0x10, 0}`. `GetWeaponElementAttr` (0x1B69F0)
  maps the weapon's **active element index** (byte `wep+0x16`) to one attribute bit;
  index 5 (= no element) maps to 0.
- Element order everywhere: **0 Fire/Flame, 1 Ice/Chill, 2 Thunder/Lightning,
  3 Wind/Cyclone, 4 Holy**.
- The weapon's five element *values* are bytes at `wep+0x17..0x1B` (capped 99, §7). Only the
  **active** element's value is used in the formula — the other four do nothing until the
  active element changes.
- The monster side is a per-element percent row (`+0x1E3FA`, 5 shorts): 100 = neutral,
  >100 = weak, small/0 = resist/immune.

## 5. Anti-enemy (slayer) system

Ten byte values at `wep+0x1C..0x25` (capped 99), in template order (matches
`WeaponAddresses.WeaponList` 0x1C–0x2E): **Dragon, Undead, Sea, Rock, Plant, Beast, Sky,
Metal, Mimic, Mage**. The monster's category (`+0x1E3F8`) indexes straight into this array;
effect is `+1.5 % damage per point` (§3 step 4). `basic_damage`/projectile setters pass the
array **pointer** (col+0x64), so a null pointer (non-weapon damage) skips the step.

## 6. Ability flags (`wep+0xEE`, short)

Runtime word = template `Effect1 | Effect2 << 8` (see `WeaponAddresses.WeaponList`
Effect1/Effect2), assembled by `WeaponAllValueSet` from the weapon + attached gems and
sanitized by `CheckWeaponOptionStatus` (0x20F6E0), which clears contradictory pairs
(BigBucks+Poor, Quench+Thirst, Fragile+Durable, Drain+Heal):

```
0x0002 BigBucks   0x0004 Poor      0x0008 Quench    0x0010 Thirst
0x0020 Poison     0x0040 Stop      0x0080 Steal
0x0100 Fragile    0x0200 Durable   0x0400 Drain     0x0800 Heal
0x1000 Critical   0x2000 ABS Up
```

Combat effects: Poison/Stop/Steal/Drain/Critical per §3; Durable/Fragile per §9.

## 7. Effective weapon stats (`WeaponAllValueSet`, 0x225B60)

The battle copy the formulas read (`NowWeaponHave` → equipped record in `UserStatus`,
stride 0xF8) is rebuilt by `SetWeaponAttachStatus` → `WeaponAllValueSet`:

```
effective stat = (stored stat + attachment bonuses) × rate
```

- `rate` (`GetNowWeaponRate`, 0x20CDD0) is 1.0 — except the **Lamb's Sword** (item 272):
  ×1.5 to *everything* while its WHP ≤ 20 % of max (`0x2A188C` = 0.2).
- Attack is capped at template `MaxAttack` × rate, Magic at `MaxMagic` × rate
  (template +0x44/+0x46). Endurance and Speed have global caps.
- The 5 element values and 10 anti values are stored as **bytes capped at 99**.

Runtime record offsets used by combat:

```
+0x00 item id        +0x04 Attack        +0x06 Endurance     +0x08 Speed
+0x0A Magic          +0x0C max WHP       +0x10 WHP (float)
+0x16 active element index (0-4, 5=none)
+0x17..0x1B element values (5 bytes)     +0x1C..0x25 anti values (10 bytes)
+0xEE ability flags (short)
```

## 8. Monster-side fields (live battle record)

Per-slot record at `pool + slot*0x190 + offset` (the `CMonstorUnit` block; same address
space as the slot fields in `EnemyAddresses.cs`):

```
+0x1E3D8 stop timer          +0x1E3DC poison timer      +0x1E3E0 enrage timer
+0x1E3F0 max HP              +0x1E3F4 current HP
+0x1E3F8 category (indexes weapon anti array)
+0x1E3FA element % × 5 (shorts, Fire..Holy)
+0x1E410 type (2 = boss: Critical-immune)
+0x1E460 Defense             +0x1E462 WHP cost per hit (§9)
+0x1E4A8 steal item id       +0x1E4AC dmg % taken from non-player sources
+0x1E4AE status susceptibility %
per-hurtbox: slot*0x510 + part*0x18 + charId*4 + 0x555D0 = damage % (per part, per character)
```

## 9. Weapon HP (durability) drain

On every landed hit `CheckDmg` calls `SwordDmgCheck1` (dun 0x1DB9B30) →
`BattleSubWeaponDmg(factor, whpCost)` (0x1B5D90):

```
WHP -= (1.5 − 0.01 × Endurance) × factor  +  0.1 × monster.whpCost   // +0x1E462
```

- **Durable** (0x200) halves `factor`; **Fragile** (0x100) doubles it.
- Endurance 150 zeroes the per-swing term (only the monster's `whpCost` remains).
- `factor` per move: melee swing 1.0, guarded hit 0.1, Toan charge 3.0, Toan lunge 2.0,
  Ruby 0.8 / 1.2 / 1.8 (normal / partial / full charge), Osmond standard 0.5.
- **Serpent Sword** (item 268) takes **no WHP damage** until game flag 0x30 is set
  (its story event).
- Warnings at 10 % and 5 % of max WHP; at 0 an owned Repair Powder (item 0xB7) is
  auto-consumed, else the weapon reverts/breaks via `WepDataListToHaveCopy`.

## 10. Thrown items (for completeness)

`CMainItemModel::Step` (0x1D4E20 region) special-cases throwables on landing:
elemental gems (items 161–165) and items 152/159 deal **30 × (selectMapNo + 1)**
(dungeon-indexed, ignores weapon stats entirely); items 160/166/167/169 register fixed
2- or 8-damage collisions whose attribute bits (0x100/0x200/0x800) inflict the
corresponding status instead of real damage.

## 11. Constants reference

| Address | Value | Used for |
|---|---|---|
| 0x2A1D84 | 0.004 | element-value coefficient |
| 0x2A1D74 | 0.005 | magic coefficient in element bonus |
| 0x2A1B78 | 0.015 | anti-enemy coefficient |
| 0x2A1C04 | 0.01 | percent scaling (resist %, drain heal, endurance term) |
| 0x2A1870 | 0.1 | monster WHP-cost coefficient; guarded-hit factor |
| 0x2A1C74 | 1.8 | Toan 5th-hit multiplier; Ruby full-charge WHP factor |
| 0x2A1ACC | 2.2 | Ruby full-charge shot multiplier |
| 0x2A1AF8 | 1.2 | Goro shockwave extra multiplier; Ruby partial WHP factor |
| 0x2A188C | 0.2 | Lamb's Sword low-WHP threshold |
| 0x2A1C68/6C/70 | 2.8 / 5.3 / 6.2 | Toan swing radii (HC reach patch target) |
| 0x26B1B0 | {1,2,4,8,0x10,0} | `element_tbl` |

Key functions: `CMonstorUnit::CheckDmg` 0x1D9F10 · `basic_damage` 0x241370 ·
`CCollisionData::Set` 0x1B57A0 · `GetWeaponElementAttr` 0x1B69F0 · `WeaponAllValueSet`
0x225B60 · `GetNowWeaponRate` 0x20CDD0 · `BattleSubWeaponDmg` 0x1B5D90 ·
`SwordDmgCheck1` dun 0x1DB9B30 · `ToanKey_Play` 0x241690 · `UngagaKey_Play` 0x243260 ·
`GoroKey_Play` 0x244130 · `BattleActionPlay_Jinn/Ruby/Ozumond*` dun 0x1DBC930/0x1DBD350/0x1DBDCF0
(dun symbols: RAM = listed address + 0x80) · `CSHOT::step` 0x1ABD10 ·
`CSHOT_MACHINGUN::Step` 0x1AE750 · `CSHOT_EFFECT::Step` 0x1AC180.

### Not yet traced

- How the **active element index** (`wep+0x16`) is chosen when element values change
  (menu/attach code — presumed highest value; not verified).
- Exact damage plumbing of Osmond's `_H`/`_F` alt gun modes beyond their full-Attack latch.
