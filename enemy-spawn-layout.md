# Dungeon Enemy Spawn Layout Tables (`BtEnemyLayout` / `BtUraEnemyLayout`)

_Generated 2026-06-07 from `SCUS_971.11` (Dark Cloud USA, v1.02) by static ELF analysis._

## How dungeon enemy spawns are wired (model + AI)

The dungeon overlay (`dun.bin`) builds enemies through this chain, traced by
disassembling `BtLoadMonstor__Fi`:

```
BtEnemyLayoutList    @ 0x002917B0  (7 dungeon pointers — normal floors)
BtUraEnemyLayoutList @ 0x002917D0  (7 dungeon pointers — Ura / back floors)
        │  index by dungeon number  ([gp-0x625C])
        ▼
BtEnemyLayout0N / BtUraEnemyLayout0N   (per-dungeon array)
        │  + floor * 0x70             (one 112-byte block per floor)
        ▼
Floor block = 9 entries × 0x0C bytes
        │  for each entry with id != -1:
        ▼
CMonstorUnit::SetupBaseModel(this, _, enemyId, 0x26, MonstorModelBuffer)
        │  enemyId → packed species record (ELF @0x0027FB00) → ModelCode "eNNa"/"cNNx"
        ▼
loads mesh into MonstorModelBuffer (0x01F066D0) and behavior/AI script into
MonstorScriptBuffer (0x01F066E0); result populates a slot in MainMonstorUnit
(0x01DF87D0), whose sub-arrays are the live enemy slots (0x21E16BA0) and the
ModelScale table (0x21E18530).
```

### Floor-block entry (`0x0C` bytes each, 9 per floor)

| Offset | Field | Notes |
|--|--|--|
| `+0x0` | spawn count / population | entry 0 often 3–4 (floor enemy count); others 1. **Not** read by `BtLoadMonstor` — consumed by placement code. |
| `+0x4` | **enemy id** | the game's internal species id (0–166). `-1` = empty slot. This is what `SetupBaseModel` loads. Maps to a species record via a non-sequential id→record table (see `EnemyDefaults.TableIndex`). |
| `+0x8` | spawn weight % | active entries' weights sum to ≈100 per floor → weighted-random species selection. |

(Each floor block ends with a 4-byte tail at `+0x6C`, observed `= 1`.)

### Caveats
- Enemy **names / model codes** below are resolved through this project's
  `EnemyData.cs` (`Id → Name, ModelCode`). Entries flagged "(no species record …)"
  are ids the packed species table skips (e.g. 53/54 Killer Snake) — spawned, if at
  all, by scripted/event paths, not the normal table lookup.
- The `eNNa` model-code convention (id 52 → `e52a`) holds for most mid-range ids
  but **not all** — bosses use `cNNx`, and several ids remap; always resolve via the
  species table / `EnemyData.cs`, not by string-formatting the id.
- Dungeon names are best-effort (from `dun\dNN*.cfg` ordering); floor counts are
  authoritative (from symbol sizes).

### File offsets (for editing the ELF directly)
`file_offset = vaddr - 0x100000 + 0x100`. e.g. `BtEnemyLayoutList` `0x002917B0` → file `0x1918B0`.

---

## Dungeon 0 — Divine Beast Cave (d01)

### Normal floors

#### `BtEnemyLayout00` @ `0x002860D0`  (15 floors, 0x690 bytes)

- **Floor 0** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 3 | 52 | 20 | e52a | Curse Dancer |
  | 1 | 1 | 50 | e01a | Master Jacket (Enhanced) |
  | 1 | 3 | 30 | e03a | Skeleton Soldier |

- **Floor 1** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 3 | 52 | 20 | e52a | Curse Dancer |
  | 1 | 1 | 20 | e01a | Master Jacket (Enhanced) |
  | 1 | 3 | 40 | e03a | Skeleton Soldier |
  | 1 | 96 | 20 | — (no direct record) | (unmapped) |

- **Floor 2** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 3 | 52 | 25 | e52a | Curse Dancer |
  | 1 | 1 | 25 | e01a | Master Jacket (Enhanced) |
  | 1 | 3 | 10 | e03a | Skeleton Soldier |
  | 1 | 2 | 20 | — (no direct record) | (unmapped) |
  | 1 | 97 | 20 | — (no direct record) | (unmapped) |

- **Floor 3** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 3 | 1 | 100 | e01a | Master Jacket (Enhanced) |

- **Floor 4** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 52 | 20 | e52a | Curse Dancer |
  | 1 | 3 | 20 | e03a | Skeleton Soldier |
  | 1 | 2 | 20 | — (no direct record) | (unmapped) |
  | 1 | 97 | 20 | — (no direct record) | (unmapped) |
  | 1 | 96 | 20 | — (no direct record) | (unmapped) |

- **Floor 5** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 52 | 25 | e52a | Curse Dancer |
  | 1 | 1 | 25 | e01a | Master Jacket (Enhanced) |
  | 1 | 2 | 25 | — (no direct record) | (unmapped) |
  | 1 | 30 | 25 | e30a | Golem |

- **Floor 6** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 52 | 20 | e52a | Curse Dancer |
  | 1 | 1 | 20 | e01a | Master Jacket (Enhanced) |
  | 1 | 36 | 20 | e36a | King Mimic (Sun & Moon Temple) |
  | 1 | 98 | 20 | — (no direct record) | (unmapped) |
  | 1 | 0 | 20 | c17_ | (DS padding tbl_166) |

- **Floor 7** — (no entries)

- **Floor 8** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 52 | 20 | e52a | Curse Dancer |
  | 1 | 36 | 20 | e36a | King Mimic (Sun & Moon Temple) |
  | 1 | 2 | 20 | — (no direct record) | (unmapped) |
  | 1 | 97 | 20 | — (no direct record) | (unmapped) |
  | 1 | 30 | 20 | e30a | Golem |

- **Floor 9** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 52 | 25 | e52a | Curse Dancer |
  | 1 | 3 | 10 | e03a | Skeleton Soldier |
  | 1 | 36 | 10 | e36a | King Mimic (Sun & Moon Temple) |
  | 1 | 0 | 30 | c17_ | (DS padding tbl_166) |
  | 1 | 69 | 25 | e69a | Billy |

- **Floor 10** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 52 | 20 | e52a | Curse Dancer |
  | 1 | 3 | 25 | e03a | Skeleton Soldier |
  | 1 | 98 | 20 | — (no direct record) | (unmapped) |
  | 1 | 0 | 20 | c17_ | (DS padding tbl_166) |
  | 1 | 51 | 15 | e51a | Lich (Enhanced) |

- **Floor 11** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 52 | 20 | e52a | Curse Dancer |
  | 1 | 2 | 20 | — (no direct record) | (unmapped) |
  | 1 | 0 | 20 | c17_ | (DS padding tbl_166) |
  | 1 | 29 | 20 | — (no direct record) | (unmapped) |
  | 1 | 69 | 20 | e69a | Billy |

- **Floor 12** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 52 | 20 | e52a | Curse Dancer |
  | 1 | 98 | 20 | — (no direct record) | (unmapped) |
  | 1 | 0 | 20 | c17_ | (DS padding tbl_166) |
  | 1 | 69 | 20 | e69a | Billy |
  | 1 | 51 | 20 | e51a | Lich (Enhanced) |

- **Floor 13** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 52 | 20 | e52a | Curse Dancer |
  | 1 | 36 | 20 | e36a | King Mimic (Sun & Moon Temple) |
  | 1 | 29 | 20 | — (no direct record) | (unmapped) |
  | 1 | 69 | 20 | e69a | Billy |
  | 1 | 51 | 20 | e51a | Lich (Enhanced) |

- **Floor 14** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 78 | 100 | e78a | King Mimic (Wise Owl Forest) |

### Ura (back) floors

#### `BtUraEnemyLayout00` @ `0x00286760`  (15 floors, 0x690 bytes)

- **Floor 0** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 3 | 1 | 60 | e01a | Master Jacket (Enhanced) |
  | 1 | 36 | 20 | e36a | King Mimic (Sun & Moon Temple) |
  | 1 | 96 | 20 | — (no direct record) | (unmapped) |

- **Floor 1** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 3 | 1 | 80 | e01a | Master Jacket (Enhanced) |
  | 1 | 36 | 20 | e36a | King Mimic (Sun & Moon Temple) |

- **Floor 2** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 3 | 1 | 80 | e01a | Master Jacket (Enhanced) |
  | 1 | 36 | 20 | e36a | King Mimic (Sun & Moon Temple) |

- **Floor 3** — (no entries)

- **Floor 4** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 30 | 50 | e30a | Golem |
  | 1 | 29 | 50 | — (no direct record) | (unmapped) |

- **Floor 5** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 2 | 50 | — (no direct record) | (unmapped) |
  | 1 | 97 | 50 | — (no direct record) | (unmapped) |

- **Floor 6** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 3 | 50 | e03a | Skeleton Soldier |
  | 1 | 69 | 50 | e69a | Billy |

- **Floor 7** — (no entries)

- **Floor 8** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 52 | 100 | e52a | Curse Dancer |

- **Floor 9** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 30 | 30 | e30a | Golem |
  | 1 | 29 | 30 | — (no direct record) | (unmapped) |
  | 1 | 69 | 40 | e69a | Billy |

- **Floor 10** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 36 | 25 | e36a | King Mimic (Sun & Moon Temple) |
  | 1 | 30 | 50 | e30a | Golem |
  | 1 | 0 | 25 | c17_ | (DS padding tbl_166) |

- **Floor 11** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 52 | 50 | e52a | Curse Dancer |
  | 1 | 51 | 50 | e51a | Lich (Enhanced) |

- **Floor 12** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 30 | 50 | e30a | Golem |
  | 1 | 29 | 50 | — (no direct record) | (unmapped) |

- **Floor 13** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 69 | 100 | e69a | Billy |

- **Floor 14** — (no entries)


---

## Dungeon 1 — Wise Owl Forest (d02)

### Normal floors

#### `BtEnemyLayout01` @ `0x00286DF0`  (17 floors, 0x770 bytes)

- **Floor 0** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 8 | 20 | e08a | FliFli |
  | 1 | 16 | 20 | e16a | Tuesday |
  | 1 | 15 | 20 | e15a | Monday |
  | 1 | 14 | 20 | e14a | Sunday |
  | 1 | 13 | 20 | — (no direct record) | (unmapped) |

- **Floor 1** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 8 | 25 | e08a | FliFli |
  | 1 | 12 | 25 | e12a | Earth Digger |
  | 1 | 11 | 25 | e11a | Cannibal Plant |
  | 1 | 5 | 25 | e05a | Statue |

- **Floor 2** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 8 | 20 | e08a | FliFli |
  | 1 | 14 | 20 | e14a | Sunday |
  | 1 | 10 | 20 | e10a | Halloween (Enhanced) |
  | 1 | 6 | 20 | e06a | Dasher |
  | 1 | 100 | 20 | c15b | (phase entity 100) |

- **Floor 3** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 14 | 20 | e14a | Sunday |
  | 1 | 10 | 20 | e10a | Halloween (Enhanced) |
  | 1 | 5 | 20 | e05a | Statue |
  | 1 | 6 | 20 | e06a | Dasher |
  | 1 | 100 | 20 | c15b | (phase entity 100) |

- **Floor 4** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 8 | 20 | e08a | FliFli |
  | 1 | 15 | 20 | e15a | Monday |
  | 1 | 13 | 20 | — (no direct record) | (unmapped) |
  | 1 | 71 | 20 | e71a | Crabby Hermit (Enhanced) |
  | 1 | 99 | 20 | — (no direct record) | (unmapped) |

- **Floor 5** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 16 | 20 | e16a | Tuesday |
  | 1 | 14 | 20 | e14a | Sunday |
  | 1 | 5 | 20 | e05a | Statue |
  | 1 | 6 | 20 | e06a | Dasher |
  | 1 | 18 | 20 | e18a | Thursday |

- **Floor 6** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 13 | 20 | — (no direct record) | (unmapped) |
  | 1 | 12 | 20 | e12a | Earth Digger |
  | 1 | 11 | 20 | e11a | Cannibal Plant |
  | 1 | 10 | 20 | e10a | Halloween (Enhanced) |
  | 1 | 18 | 20 | e18a | Thursday |

- **Floor 7** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 5 | 20 | e05a | Statue |
  | 1 | 6 | 20 | e06a | Dasher |
  | 1 | 71 | 20 | e71a | Crabby Hermit (Enhanced) |
  | 1 | 18 | 20 | e18a | Thursday |
  | 1 | 100 | 20 | c15b | (phase entity 100) |

- **Floor 8** — (no entries)

- **Floor 9** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 8 | 20 | e08a | FliFli |
  | 1 | 6 | 20 | e06a | Dasher |
  | 1 | 71 | 20 | e71a | Crabby Hermit (Enhanced) |
  | 1 | 9 | 20 | e09a | Hornet |
  | 1 | 99 | 20 | — (no direct record) | (unmapped) |

- **Floor 10** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 5 | 20 | e05a | Statue |
  | 1 | 18 | 20 | e18a | Thursday |
  | 1 | 9 | 20 | e09a | Hornet |
  | 1 | 7 | 20 | e07a | Werewolf |
  | 1 | 99 | 20 | — (no direct record) | (unmapped) |

- **Floor 11** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 6 | 25 | e06a | Dasher |
  | 1 | 18 | 25 | e18a | Thursday |
  | 1 | 9 | 25 | e09a | Hornet |
  | 1 | 70 | 25 | e70a | Vulcan (Enhanced) |

- **Floor 12** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 8 | 30 | e08a | FliFli |
  | 1 | 5 | 40 | e05a | Statue |
  | 1 | 9 | 30 | e09a | Hornet |

- **Floor 13** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 10 | 25 | e10a | Halloween (Enhanced) |
  | 1 | 18 | 25 | e18a | Thursday |
  | 1 | 7 | 25 | e07a | Werewolf |
  | 1 | 4 | 25 | — (no direct record) | (unmapped) |

- **Floor 14** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 14 | 20 | e14a | Sunday |
  | 1 | 6 | 20 | e06a | Dasher |
  | 1 | 7 | 20 | e07a | Werewolf |
  | 1 | 70 | 20 | e70a | Vulcan (Enhanced) |
  | 1 | 100 | 20 | c15b | (phase entity 100) |

- **Floor 15** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 18 | 20 | e18a | Thursday |
  | 1 | 9 | 20 | e09a | Hornet |
  | 1 | 7 | 20 | e07a | Werewolf |
  | 1 | 70 | 20 | e70a | Vulcan (Enhanced) |
  | 1 | 4 | 20 | — (no direct record) | (unmapped) |

- **Floor 16** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 1 | 79 | 100 | e79a | Mimic (Wise Owl Forest) |

### Ura (back) floors

#### `BtUraEnemyLayout01` @ `0x00287560`  (17 floors, 0x770 bytes)

- **Floor 0** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 8 | 50 | e08a | FliFli |
  | 1 | 6 | 50 | e06a | Dasher |

- **Floor 1** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 9 | 100 | e09a | Hornet |

- **Floor 2** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 5 | 50 | e05a | Statue |
  | 1 | 18 | 50 | e18a | Thursday |

- **Floor 3** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 71 | 50 | e71a | Crabby Hermit (Enhanced) |
  | 1 | 70 | 50 | e70a | Vulcan (Enhanced) |

- **Floor 4** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 5 | 30 | e05a | Statue |
  | 1 | 18 | 30 | e18a | Thursday |
  | 1 | 7 | 40 | e07a | Werewolf |

- **Floor 5** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 14 | 25 | e14a | Sunday |
  | 1 | 12 | 25 | e12a | Earth Digger |
  | 1 | 11 | 25 | e11a | Cannibal Plant |
  | 1 | 10 | 25 | e10a | Halloween (Enhanced) |

- **Floor 6** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 9 | 50 | e09a | Hornet |
  | 1 | 100 | 50 | c15b | (phase entity 100) |

- **Floor 7** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 71 | 50 | e71a | Crabby Hermit (Enhanced) |
  | 1 | 70 | 50 | e70a | Vulcan (Enhanced) |

- **Floor 8** — (no entries)

- **Floor 9** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 4 | 100 | — (no direct record) | (unmapped) |

- **Floor 10** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 7 | 100 | e07a | Werewolf |

- **Floor 11** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 8 | 50 | e08a | FliFli |
  | 1 | 5 | 50 | e05a | Statue |

- **Floor 12** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 14 | 20 | e14a | Sunday |
  | 1 | 12 | 20 | e12a | Earth Digger |
  | 1 | 11 | 20 | e11a | Cannibal Plant |
  | 1 | 10 | 20 | e10a | Halloween (Enhanced) |
  | 1 | 7 | 20 | e07a | Werewolf |

- **Floor 13** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 9 | 100 | e09a | Hornet |

- **Floor 14** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 71 | 50 | e71a | Crabby Hermit (Enhanced) |
  | 1 | 70 | 50 | e70a | Vulcan (Enhanced) |

- **Floor 15** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 0 | 16 | 25 | e16a | Tuesday |
  | 1 | 15 | 25 | e15a | Monday |
  | 1 | 14 | 25 | e14a | Sunday |
  | 1 | 13 | 25 | — (no direct record) | (unmapped) |

- **Floor 16** — (no entries)


---

## Dungeon 2 — Lake Gilna / Coastal Cave (d03)

### Normal floors

#### `BtEnemyLayout02` @ `0x00287CD0`  (18 floors, 0x7e0 bytes)

- **Floor 0** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 24 | 30 | e24a | Gyon (Enhanced) |
  | 1 | 19 | 70 | e19a | Friday |

- **Floor 1** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 24 | 30 | e24a | Gyon (Enhanced) |
  | 1 | 19 | 50 | e19a | Friday |
  | 1 | 60 | 20 | e60a | Cave Bat (Enhanced) |

- **Floor 2** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 24 | 40 | e24a | Gyon (Enhanced) |
  | 1 | 19 | 50 | e19a | Friday |
  | 1 | 60 | 10 | e60a | Cave Bat (Enhanced) |

- **Floor 3** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 24 | 20 | e24a | Gyon (Enhanced) |
  | 1 | 19 | 35 | e19a | Friday |
  | 1 | 60 | 20 | e60a | Cave Bat (Enhanced) |
  | 1 | 73 | 25 | e73a | Blue Dragon |

- **Floor 4** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 24 | 40 | e24a | Gyon (Enhanced) |
  | 1 | 19 | 40 | e19a | Friday |
  | 1 | 73 | 20 | e73a | Blue Dragon |

- **Floor 5** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 24 | 5 | e24a | Gyon (Enhanced) |
  | 1 | 60 | 5 | e60a | Cave Bat (Enhanced) |
  | 1 | 73 | 25 | e73a | Blue Dragon |
  | 1 | 20 | 65 | e20a | Saturday |

- **Floor 6** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 19 | 30 | e19a | Friday |
  | 1 | 60 | 10 | e60a | Cave Bat (Enhanced) |
  | 1 | 20 | 40 | e20a | Saturday |
  | 1 | 77 | 20 | e77a | Rockanoff (Enhanced) |

- **Floor 7** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 73 | 25 | e73a | Blue Dragon |
  | 1 | 20 | 45 | e20a | Saturday |
  | 1 | 77 | 5 | e77a | Rockanoff (Enhanced) |
  | 1 | 23 | 25 | e23a | Gunny |

- **Floor 8** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 24 | 50 | e24a | Gyon (Enhanced) |
  | 1 | 23 | 25 | e23a | Gunny |
  | 1 | 21 | 25 | e21a | Witch Hellza (Enhanced) |

- **Floor 9** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 19 | 30 | e19a | Friday |
  | 1 | 60 | 20 | e60a | Cave Bat (Enhanced) |
  | 1 | 23 | 25 | e23a | Gunny |
  | 1 | 21 | 25 | e21a | Witch Hellza (Enhanced) |

- **Floor 10** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 24 | 20 | e24a | Gyon (Enhanced) |
  | 1 | 20 | 35 | e20a | Saturday |
  | 1 | 21 | 20 | e21a | Witch Hellza (Enhanced) |
  | 1 | 72 | 25 | e72a | Space Gyon (Enhanced) |

- **Floor 11** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 60 | 25 | e60a | Cave Bat (Enhanced) |
  | 1 | 73 | 25 | e73a | Blue Dragon |
  | 1 | 23 | 25 | e23a | Gunny |
  | 1 | 72 | 25 | e72a | Space Gyon (Enhanced) |

- **Floor 12** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 73 | 25 | e73a | Blue Dragon |
  | 1 | 23 | 25 | e23a | Gunny |
  | 1 | 21 | 25 | e21a | Witch Hellza (Enhanced) |
  | 1 | 22 | 25 | e22a | Witch Illza |

- **Floor 13** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 77 | 25 | e77a | Rockanoff (Enhanced) |
  | 1 | 72 | 25 | e72a | Space Gyon (Enhanced) |
  | 1 | 22 | 25 | e22a | Witch Illza |
  | 1 | 67 | 25 | e67a | Dark Flower |

- **Floor 14** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 20 | 40 | e20a | Saturday |
  | 1 | 23 | 10 | e23a | Gunny |
  | 1 | 22 | 25 | e22a | Witch Illza |
  | 1 | 67 | 25 | e67a | Dark Flower |

- **Floor 15** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 60 | 25 | e60a | Cave Bat (Enhanced) |
  | 1 | 23 | 25 | e23a | Gunny |
  | 1 | 21 | 25 | e21a | Witch Hellza (Enhanced) |
  | 1 | 67 | 25 | e67a | Dark Flower |

- **Floor 16** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 21 | 25 | e21a | Witch Hellza (Enhanced) |
  | 1 | 72 | 25 | e72a | Space Gyon (Enhanced) |
  | 1 | 22 | 25 | e22a | Witch Illza |
  | 1 | 67 | 25 | e67a | Dark Flower |

- **Floor 17** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 1 | 80 | 40 | e80a | King Mimic (Shipwreck) |
  | 1 | 101 | 10 | — (no direct record) | (unmapped) |
  | 1 | 76 | 10 | e76a | Crescent Baron (Enhanced) |
  | 1 | 102 | 10 | — (no direct record) | (unmapped) |
  | 1 | 103 | 10 | — (no direct record) | (unmapped) |
  | 1 | 92 | 10 | — (no direct record) | (unmapped) |
  | 1 | 104 | 10 | — (no direct record) | (unmapped) |

### Ura (back) floors

#### `BtUraEnemyLayout02` @ `0x002884B0`  (18 floors, 0x7e0 bytes)

- **Floor 0** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 19 | 50 | e19a | Friday |
  | 1 | 20 | 50 | e20a | Saturday |

- **Floor 1** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 24 | 50 | e24a | Gyon (Enhanced) |
  | 1 | 23 | 50 | e23a | Gunny |

- **Floor 2** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 60 | 50 | e60a | Cave Bat (Enhanced) |
  | 1 | 23 | 50 | e23a | Gunny |

- **Floor 3** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 77 | 30 | e77a | Rockanoff (Enhanced) |
  | 1 | 67 | 70 | e67a | Dark Flower |

- **Floor 4** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 21 | 50 | e21a | Witch Hellza (Enhanced) |
  | 1 | 67 | 50 | e67a | Dark Flower |

- **Floor 5** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 73 | 50 | e73a | Blue Dragon |
  | 1 | 72 | 50 | e72a | Space Gyon (Enhanced) |

- **Floor 6** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 60 | 50 | e60a | Cave Bat (Enhanced) |
  | 1 | 22 | 50 | e22a | Witch Illza |

- **Floor 7** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 20 | 50 | e20a | Saturday |
  | 1 | 22 | 50 | e22a | Witch Illza |

- **Floor 8** — (no entries)

- **Floor 9** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 73 | 50 | e73a | Blue Dragon |
  | 1 | 72 | 50 | e72a | Space Gyon (Enhanced) |

- **Floor 10** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 77 | 30 | e77a | Rockanoff (Enhanced) |
  | 1 | 67 | 70 | e67a | Dark Flower |

- **Floor 11** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 21 | 50 | e21a | Witch Hellza (Enhanced) |
  | 1 | 67 | 50 | e67a | Dark Flower |

- **Floor 12** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 24 | 50 | e24a | Gyon (Enhanced) |
  | 1 | 60 | 50 | e60a | Cave Bat (Enhanced) |

- **Floor 13** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 67 | 100 | e67a | Dark Flower |

- **Floor 14** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 20 | 50 | e20a | Saturday |
  | 1 | 22 | 50 | e22a | Witch Illza |

- **Floor 15** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 73 | 50 | e73a | Blue Dragon |
  | 1 | 72 | 50 | e72a | Space Gyon (Enhanced) |

- **Floor 16** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 24 | 30 | e24a | Gyon (Enhanced) |
  | 1 | 23 | 30 | e23a | Gunny |
  | 1 | 21 | 40 | e21a | Witch Hellza (Enhanced) |

- **Floor 17** — (no entries)


---

## Dungeon 3 — Queens (d04)

### Normal floors

#### `BtEnemyLayout03` @ `0x00288C90`  (18 floors, 0x7e0 bytes)

- **Floor 0** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 44 | 50 | e44a | Heart (Enhanced) |
  | 1 | 50 | 50 | e50a | Mummy (Enhanced) |

- **Floor 1** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 44 | 40 | e44a | Heart (Enhanced) |
  | 1 | 50 | 30 | e50a | Mummy (Enhanced) |
  | 1 | 43 | 30 | e43a | Alexnder (Enhanced) |

- **Floor 2** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 44 | 25 | e44a | Heart (Enhanced) |
  | 1 | 50 | 25 | e50a | Mummy (Enhanced) |
  | 1 | 43 | 25 | e43a | Alexnder (Enhanced) |
  | 1 | 32 | 25 | e32a | Dune |

- **Floor 3** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 50 | 25 | e50a | Mummy (Enhanced) |
  | 1 | 43 | 25 | e43a | Alexnder (Enhanced) |
  | 1 | 32 | 25 | e32a | Dune |
  | 1 | 25 | 25 | e25a | Pirate's Chariot (Enhanced) |

- **Floor 4** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 44 | 20 | e44a | Heart (Enhanced) |
  | 1 | 32 | 35 | e32a | Dune |
  | 1 | 25 | 45 | e25a | Pirate's Chariot (Enhanced) |

- **Floor 5** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 44 | 25 | e44a | Heart (Enhanced) |
  | 1 | 50 | 25 | e50a | Mummy (Enhanced) |
  | 1 | 25 | 25 | e25a | Pirate's Chariot (Enhanced) |
  | 1 | 26 | 25 | e26a | Auntie Medu (Enhanced) |

- **Floor 6** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 43 | 25 | e43a | Alexnder (Enhanced) |
  | 1 | 32 | 25 | e32a | Dune |
  | 1 | 25 | 25 | e25a | Pirate's Chariot (Enhanced) |
  | 1 | 63 | 25 | e63a | Rash Dasher (Enhanced) |

- **Floor 7** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 43 | 50 | e43a | Alexnder (Enhanced) |
  | 1 | 26 | 50 | e26a | Auntie Medu (Enhanced) |

- **Floor 8** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 94 | 50 | — (no direct record) | (unmapped) |
  | 1 | 95 | 50 | — (no direct record) | (unmapped) |

- **Floor 9** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 50 | 25 | e50a | Mummy (Enhanced) |
  | 1 | 43 | 25 | e43a | Alexnder (Enhanced) |
  | 1 | 25 | 25 | e25a | Pirate's Chariot (Enhanced) |
  | 1 | 27 | 25 | e27a | Captain (Enhanced) |

- **Floor 10** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 32 | 25 | e32a | Dune |
  | 1 | 63 | 25 | e63a | Rash Dasher (Enhanced) |
  | 1 | 27 | 50 | e27a | Captain (Enhanced) |

- **Floor 11** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 44 | 25 | e44a | Heart (Enhanced) |
  | 1 | 25 | 25 | e25a | Pirate's Chariot (Enhanced) |
  | 1 | 27 | 25 | e27a | Captain (Enhanced) |
  | 1 | 31 | 25 | e31a | Mr. Blare |

- **Floor 12** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 50 | 25 | e50a | Mummy (Enhanced) |
  | 1 | 43 | 25 | e43a | Alexnder (Enhanced) |
  | 1 | 26 | 25 | e26a | Auntie Medu (Enhanced) |
  | 1 | 27 | 25 | e27a | Captain (Enhanced) |

- **Floor 13** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 32 | 25 | e32a | Dune |
  | 1 | 25 | 20 | e25a | Pirate's Chariot (Enhanced) |
  | 1 | 63 | 20 | e63a | Rash Dasher (Enhanced) |
  | 1 | 31 | 25 | e31a | Mr. Blare |
  | 1 | 65 | 10 | e65a | Blizzard |

- **Floor 14** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 50 | 25 | e50a | Mummy (Enhanced) |
  | 1 | 43 | 25 | e43a | Alexnder (Enhanced) |
  | 1 | 27 | 25 | e27a | Captain (Enhanced) |
  | 1 | 56 | 25 | e56a | White Fang (Enhanced) |

- **Floor 15** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 32 | 50 | e32a | Dune |
  | 1 | 31 | 50 | e31a | Mr. Blare |

- **Floor 16** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 27 | 25 | e27a | Captain (Enhanced) |
  | 1 | 31 | 25 | e31a | Mr. Blare |
  | 1 | 65 | 25 | e65a | Blizzard |
  | 1 | 56 | 25 | e56a | White Fang (Enhanced) |

- **Floor 17** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 1 | 81 | 50 | e81a | Mimic (Shipwreck) |
  | 1 | 82 | 50 | e82a | King Mimic (Gallery of Time) |

### Ura (back) floors

#### `BtUraEnemyLayout03` @ `0x00289470`  (18 floors, 0x7e0 bytes)

- **Floor 0** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 25 | 100 | e25a | Pirate's Chariot (Enhanced) |

- **Floor 1** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 50 | 40 | e50a | Mummy (Enhanced) |
  | 1 | 63 | 20 | e63a | Rash Dasher (Enhanced) |
  | 1 | 27 | 40 | e27a | Captain (Enhanced) |

- **Floor 2** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 32 | 50 | e32a | Dune |
  | 1 | 31 | 50 | e31a | Mr. Blare |

- **Floor 3** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 43 | 50 | e43a | Alexnder (Enhanced) |
  | 1 | 26 | 50 | e26a | Auntie Medu (Enhanced) |

- **Floor 4** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 25 | 30 | e25a | Pirate's Chariot (Enhanced) |
  | 1 | 27 | 30 | e27a | Captain (Enhanced) |
  | 1 | 56 | 40 | e56a | White Fang (Enhanced) |

- **Floor 5** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 63 | 30 | e63a | Rash Dasher (Enhanced) |
  | 1 | 27 | 70 | e27a | Captain (Enhanced) |

- **Floor 6** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 43 | 50 | e43a | Alexnder (Enhanced) |
  | 1 | 26 | 50 | e26a | Auntie Medu (Enhanced) |

- **Floor 7** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 32 | 50 | e32a | Dune |
  | 1 | 31 | 50 | e31a | Mr. Blare |

- **Floor 8** — (no entries)

- **Floor 9** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 43 | 50 | e43a | Alexnder (Enhanced) |
  | 1 | 56 | 50 | e56a | White Fang (Enhanced) |

- **Floor 10** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 43 | 100 | e43a | Alexnder (Enhanced) |

- **Floor 11** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 43 | 50 | e43a | Alexnder (Enhanced) |
  | 1 | 56 | 50 | e56a | White Fang (Enhanced) |

- **Floor 12** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 43 | 50 | e43a | Alexnder (Enhanced) |
  | 1 | 26 | 50 | e26a | Auntie Medu (Enhanced) |

- **Floor 13** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 44 | 100 | e44a | Heart (Enhanced) |

- **Floor 14** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 50 | 100 | e50a | Mummy (Enhanced) |

- **Floor 15** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 32 | 50 | e32a | Dune |
  | 1 | 31 | 50 | e31a | Mr. Blare |

- **Floor 16** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 65 | 100 | e65a | Blizzard |

- **Floor 17** — (no entries)


---

## Dungeon 4 — Shipwreck (d05)

### Normal floors

#### `BtEnemyLayout04` @ `0x00289C50`  (15 floors, 0x690 bytes)

- **Floor 0** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 54 | 40 | — (no direct record) | Killer Snake |
  | 1 | 49 | 30 | e49a | Bomber Head (Enhanced) |
  | 1 | 17 | 30 | e17a | Wednesday |

- **Floor 1** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 54 | 25 | — (no direct record) | Killer Snake |
  | 1 | 49 | 25 | e49a | Bomber Head (Enhanced) |
  | 1 | 17 | 25 | e17a | Wednesday |
  | 1 | 64 | 25 | e64a | Steel Giant (Enhanced) |

- **Floor 2** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 54 | 25 | — (no direct record) | Killer Snake |
  | 1 | 49 | 25 | e49a | Bomber Head (Enhanced) |
  | 1 | 64 | 25 | e64a | Steel Giant (Enhanced) |
  | 1 | 34 | 25 | e34a | King Mimic (Divine Beast Cave) |

- **Floor 3** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 49 | 25 | e49a | Bomber Head (Enhanced) |
  | 1 | 17 | 25 | e17a | Wednesday |
  | 1 | 64 | 25 | e64a | Steel Giant (Enhanced) |
  | 1 | 34 | 25 | e34a | King Mimic (Divine Beast Cave) |

- **Floor 4** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 54 | 50 | — (no direct record) | Killer Snake |
  | 1 | 58 | 50 | e58a | Phantom |

- **Floor 5** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 17 | 25 | e17a | Wednesday |
  | 1 | 64 | 25 | e64a | Steel Giant (Enhanced) |
  | 1 | 34 | 25 | e34a | King Mimic (Divine Beast Cave) |
  | 1 | 48 | 25 | e48a | Joker (Enhanced) |

- **Floor 6** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 49 | 25 | e49a | Bomber Head (Enhanced) |
  | 1 | 58 | 25 | e58a | Phantom |
  | 1 | 48 | 25 | e48a | Joker (Enhanced) |
  | 1 | 62 | 25 | e62a | Hell Pockle |

- **Floor 7** — (no entries)

- **Floor 8** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 64 | 25 | e64a | Steel Giant (Enhanced) |
  | 1 | 58 | 25 | e58a | Phantom |
  | 1 | 62 | 25 | e62a | Hell Pockle |
  | 1 | 33 | 25 | e33a | Titan (Enhanced) |

- **Floor 9** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 58 | 25 | e58a | Phantom |
  | 1 | 48 | 25 | e48a | Joker (Enhanced) |
  | 1 | 62 | 25 | e62a | Hell Pockle |
  | 1 | 28 | 25 | e28a | Corcea (Enhanced) |

- **Floor 10** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 49 | 50 | e49a | Bomber Head (Enhanced) |
  | 1 | 28 | 50 | e28a | Corcea (Enhanced) |

- **Floor 11** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 17 | 25 | e17a | Wednesday |
  | 1 | 62 | 25 | e62a | Hell Pockle |
  | 1 | 28 | 25 | e28a | Corcea (Enhanced) |
  | 1 | 68 | 25 | e68a | Cursed Rose (Enhanced) |

- **Floor 12** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 58 | 25 | e58a | Phantom |
  | 1 | 62 | 25 | e62a | Hell Pockle |
  | 1 | 68 | 25 | e68a | Cursed Rose (Enhanced) |
  | 1 | 35 | 25 | e35a | Mimic (Divine Beast Cave) |

- **Floor 13** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 33 | 25 | e33a | Titan (Enhanced) |
  | 1 | 28 | 25 | e28a | Corcea (Enhanced) |
  | 1 | 68 | 25 | e68a | Cursed Rose (Enhanced) |
  | 1 | 35 | 25 | e35a | Mimic (Divine Beast Cave) |

- **Floor 14** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 83 | 50 | e83a | Mimic (Gallery of Time) |
  | 1 | 91 | 50 | e91a | Sil (Enhanced) |

### Ura (back) floors

#### `BtUraEnemyLayout04` @ `0x0028A2E0`  (15 floors, 0x690 bytes)

- **Floor 0** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 58 | 100 | e58a | Phantom |

- **Floor 1** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 28 | 50 | e28a | Corcea (Enhanced) |
  | 1 | 35 | 50 | e35a | Mimic (Divine Beast Cave) |

- **Floor 2** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 54 | 50 | — (no direct record) | Killer Snake |
  | 1 | 68 | 50 | e68a | Cursed Rose (Enhanced) |

- **Floor 3** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 17 | 50 | e17a | Wednesday |
  | 1 | 28 | 50 | e28a | Corcea (Enhanced) |

- **Floor 4** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 49 | 100 | e49a | Bomber Head (Enhanced) |

- **Floor 5** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 54 | 33 | — (no direct record) | Killer Snake |
  | 1 | 34 | 33 | e34a | King Mimic (Divine Beast Cave) |
  | 1 | 33 | 34 | e33a | Titan (Enhanced) |

- **Floor 6** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 49 | 50 | e49a | Bomber Head (Enhanced) |
  | 1 | 68 | 50 | e68a | Cursed Rose (Enhanced) |

- **Floor 7** — (no entries)

- **Floor 8** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 54 | 50 | — (no direct record) | Killer Snake |
  | 1 | 58 | 50 | e58a | Phantom |

- **Floor 9** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 62 | 100 | e62a | Hell Pockle |

- **Floor 10** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 35 | 100 | e35a | Mimic (Divine Beast Cave) |

- **Floor 11** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 64 | 100 | e64a | Steel Giant (Enhanced) |

- **Floor 12** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 54 | 33 | — (no direct record) | Killer Snake |
  | 1 | 34 | 33 | e34a | King Mimic (Divine Beast Cave) |
  | 1 | 33 | 34 | e33a | Titan (Enhanced) |

- **Floor 13** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 17 | 34 | e17a | Wednesday |
  | 1 | 48 | 33 | e48a | Joker (Enhanced) |
  | 1 | 68 | 33 | e68a | Cursed Rose (Enhanced) |

- **Floor 14** — (no entries)


---

## Dungeon 5 — Muska Lacka / Sun & Moon Temple (d06)

### Normal floors

#### `BtEnemyLayout05` @ `0x0028A970`  (26 floors, 0xb60 bytes)

- **Floor 0** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 53 | 40 | — (no direct record) | (unmapped) |
  | 1 | 46 | 30 | e46a | Diamond (Enhanced) |
  | 1 | 59 | 30 | e59a | Dragon |

- **Floor 1** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 53 | 25 | — (no direct record) | (unmapped) |
  | 1 | 46 | 25 | e46a | Diamond (Enhanced) |
  | 1 | 59 | 25 | e59a | Dragon |
  | 1 | 61 | 25 | e61a | Evil Bat (Enhanced) |

- **Floor 2** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 53 | 25 | — (no direct record) | (unmapped) |
  | 1 | 59 | 25 | e59a | Dragon |
  | 1 | 61 | 25 | e61a | Evil Bat (Enhanced) |
  | 1 | 47 | 25 | e47a | Spade (Enhanced) |

- **Floor 3** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 53 | 25 | — (no direct record) | (unmapped) |
  | 1 | 46 | 25 | e46a | Diamond (Enhanced) |
  | 1 | 47 | 25 | e47a | Spade (Enhanced) |
  | 1 | 75 | 25 | e75a | Mask of Prajna (Enhanced) |

- **Floor 4** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 46 | 25 | e46a | Diamond (Enhanced) |
  | 1 | 59 | 25 | e59a | Dragon |
  | 1 | 47 | 25 | e47a | Spade (Enhanced) |
  | 1 | 55 | 25 | e55a | Living Armor (Enhanced) |

- **Floor 5** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 53 | 25 | — (no direct record) | (unmapped) |
  | 1 | 61 | 25 | e61a | Evil Bat (Enhanced) |
  | 1 | 75 | 25 | e75a | Mask of Prajna (Enhanced) |
  | 1 | 39 | 25 | e39a | Mimic (Moon Sea) |

- **Floor 6** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 53 | 25 | — (no direct record) | (unmapped) |
  | 1 | 75 | 25 | e75a | Mask of Prajna (Enhanced) |
  | 1 | 55 | 25 | e55a | Living Armor (Enhanced) |
  | 1 | 38 | 25 | e38a | King Mimic (Moon Sea) |

- **Floor 7** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 59 | 25 | e59a | Dragon |
  | 1 | 61 | 25 | e61a | Evil Bat (Enhanced) |
  | 1 | 47 | 25 | e47a | Spade (Enhanced) |
  | 1 | 40 | 25 | e40a | Arthur (Enhanced) |

- **Floor 8** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 53 | 25 | — (no direct record) | (unmapped) |
  | 1 | 59 | 25 | e59a | Dragon |
  | 1 | 55 | 25 | e55a | Living Armor (Enhanced) |
  | 1 | 41 | 25 | — (no direct record) | (unmapped) |

- **Floor 9** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 47 | 25 | e47a | Spade (Enhanced) |
  | 1 | 75 | 25 | e75a | Mask of Prajna (Enhanced) |
  | 1 | 55 | 25 | e55a | Living Armor (Enhanced) |
  | 1 | 42 | 25 | e42a | Ghost |

- **Floor 10** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 39 | 20 | e39a | Mimic (Moon Sea) |
  | 1 | 38 | 20 | e38a | King Mimic (Moon Sea) |
  | 1 | 40 | 20 | e40a | Arthur (Enhanced) |
  | 1 | 41 | 20 | — (no direct record) | (unmapped) |
  | 1 | 42 | 20 | e42a | Ghost |

- **Floor 11** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 55 | 25 | e55a | Living Armor (Enhanced) |
  | 1 | 38 | 25 | e38a | King Mimic (Moon Sea) |
  | 1 | 42 | 25 | e42a | Ghost |
  | 1 | 74 | 25 | e74a | Black Dragon |

- **Floor 12** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 55 | 25 | e55a | Living Armor (Enhanced) |
  | 1 | 39 | 25 | e39a | Mimic (Moon Sea) |
  | 1 | 40 | 25 | e40a | Arthur (Enhanced) |
  | 1 | 45 | 25 | e45a | Club (Enhanced) |

- **Floor 13** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 59 | 25 | e59a | Dragon |
  | 1 | 74 | 25 | e74a | Black Dragon |
  | 1 | 45 | 25 | e45a | Club (Enhanced) |
  | 1 | 57 | 25 | e57a | Moon Bug |

- **Floor 14** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 47 | 25 | e47a | Spade (Enhanced) |
  | 1 | 45 | 25 | e45a | Club (Enhanced) |
  | 1 | 57 | 25 | e57a | Moon Bug |
  | 1 | 37 | 25 | e37a | Mimic (Sun & Moon Temple) |

- **Floor 15** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 74 | 25 | e74a | Black Dragon |
  | 1 | 45 | 25 | e45a | Club (Enhanced) |
  | 1 | 37 | 25 | e37a | Mimic (Sun & Moon Temple) |
  | 1 | 66 | 25 | e66a | Moon Digger |

- **Floor 16** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 53 | 25 | — (no direct record) | (unmapped) |
  | 1 | 57 | 25 | e57a | Moon Bug |
  | 1 | 37 | 25 | e37a | Mimic (Sun & Moon Temple) |
  | 1 | 66 | 25 | e66a | Moon Digger |

- **Floor 17** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 47 | 40 | e47a | Spade (Enhanced) |
  | 1 | 75 | 30 | e75a | Mask of Prajna (Enhanced) |
  | 1 | 74 | 30 | e74a | Black Dragon |

- **Floor 18** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 53 | 25 | — (no direct record) | (unmapped) |
  | 1 | 46 | 25 | e46a | Diamond (Enhanced) |
  | 1 | 61 | 25 | e61a | Evil Bat (Enhanced) |
  | 1 | 45 | 25 | e45a | Club (Enhanced) |

- **Floor 19** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 59 | 25 | e59a | Dragon |
  | 1 | 47 | 25 | e47a | Spade (Enhanced) |
  | 1 | 55 | 25 | e55a | Living Armor (Enhanced) |
  | 1 | 66 | 25 | e66a | Moon Digger |

- **Floor 20** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 53 | 25 | — (no direct record) | (unmapped) |
  | 1 | 61 | 25 | e61a | Evil Bat (Enhanced) |
  | 1 | 38 | 25 | e38a | King Mimic (Moon Sea) |
  | 1 | 57 | 25 | e57a | Moon Bug |

- **Floor 21** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 42 | 25 | e42a | Ghost |
  | 1 | 74 | 25 | e74a | Black Dragon |
  | 1 | 37 | 25 | e37a | Mimic (Sun & Moon Temple) |
  | 1 | 66 | 25 | e66a | Moon Digger |

- **Floor 22** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 39 | 20 | e39a | Mimic (Moon Sea) |
  | 1 | 38 | 20 | e38a | King Mimic (Moon Sea) |
  | 1 | 40 | 20 | e40a | Arthur (Enhanced) |
  | 1 | 41 | 20 | — (no direct record) | (unmapped) |
  | 1 | 42 | 20 | e42a | Ghost |

- **Floor 23** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 45 | 25 | e45a | Club (Enhanced) |
  | 1 | 57 | 25 | e57a | Moon Bug |
  | 1 | 37 | 25 | e37a | Mimic (Sun & Moon Temple) |
  | 1 | 66 | 25 | e66a | Moon Digger |

- **Floor 24** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 1 | 84 | 40 | kori | Ice Arrow |
  | 1 | 85 | 10 | e86a | Sam |
  | 1 | 86 | 10 | — (no direct record) | (unmapped) |
  | 1 | 87 | 10 | — (no direct record) | (unmapped) |
  | 1 | 88 | 10 | — (no direct record) | (unmapped) |
  | 1 | 89 | 10 | — (no direct record) | (unmapped) |
  | 1 | 90 | 5 | e90a | Gol (Enhanced) |
  | 1 | 93 | 5 | — (no direct record) | (unmapped) |

- **Floor 25** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 1 | 106 | 50 | — (no direct record) | (unmapped) |
  | 0 | 105 | 10 | — (no direct record) | (unmapped) |
  | 1 | 107 | 10 | — (no direct record) | (unmapped) |
  | 1 | 108 | 10 | — (no direct record) | (unmapped) |
  | 1 | 109 | 10 | — (no direct record) | (unmapped) |
  | 1 | 110 | 10 | — (no direct record) | (unmapped) |

### Ura (back) floors

#### `BtUraEnemyLayout05` @ `0x0028B4D0`  (26 floors, 0xb60 bytes)

- **Floor 0** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 38 | 50 | e38a | King Mimic (Moon Sea) |
  | 1 | 45 | 50 | e45a | Club (Enhanced) |

- **Floor 1** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 47 | 50 | e47a | Spade (Enhanced) |
  | 1 | 37 | 50 | e37a | Mimic (Sun & Moon Temple) |

- **Floor 2** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 53 | 50 | — (no direct record) | (unmapped) |
  | 1 | 66 | 50 | e66a | Moon Digger |

- **Floor 3** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 53 | 50 | — (no direct record) | (unmapped) |
  | 1 | 59 | 50 | e59a | Dragon |

- **Floor 4** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 47 | 25 | e47a | Spade (Enhanced) |
  | 1 | 75 | 25 | e75a | Mask of Prajna (Enhanced) |
  | 1 | 74 | 25 | e74a | Black Dragon |
  | 1 | 37 | 25 | e37a | Mimic (Sun & Moon Temple) |

- **Floor 5** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 55 | 25 | e55a | Living Armor (Enhanced) |
  | 1 | 57 | 25 | e57a | Moon Bug |
  | 1 | 37 | 25 | e37a | Mimic (Sun & Moon Temple) |
  | 1 | 66 | 25 | e66a | Moon Digger |

- **Floor 6** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 47 | 50 | e47a | Spade (Enhanced) |
  | 1 | 37 | 50 | e37a | Mimic (Sun & Moon Temple) |

- **Floor 7** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 53 | 50 | — (no direct record) | (unmapped) |
  | 1 | 66 | 50 | e66a | Moon Digger |

- **Floor 8** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 47 | 50 | e47a | Spade (Enhanced) |
  | 1 | 37 | 50 | e37a | Mimic (Sun & Moon Temple) |

- **Floor 9** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 53 | 50 | — (no direct record) | (unmapped) |
  | 1 | 59 | 50 | e59a | Dragon |

- **Floor 10** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 47 | 25 | e47a | Spade (Enhanced) |
  | 1 | 75 | 25 | e75a | Mask of Prajna (Enhanced) |
  | 1 | 74 | 25 | e74a | Black Dragon |
  | 1 | 37 | 25 | e37a | Mimic (Sun & Moon Temple) |

- **Floor 11** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 55 | 25 | e55a | Living Armor (Enhanced) |
  | 1 | 57 | 25 | e57a | Moon Bug |
  | 1 | 37 | 25 | e37a | Mimic (Sun & Moon Temple) |
  | 1 | 66 | 25 | e66a | Moon Digger |

- **Floor 12** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 53 | 50 | — (no direct record) | (unmapped) |
  | 1 | 66 | 50 | e66a | Moon Digger |

- **Floor 13** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 46 | 50 | e46a | Diamond (Enhanced) |
  | 1 | 61 | 50 | e61a | Evil Bat (Enhanced) |

- **Floor 14** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 47 | 25 | e47a | Spade (Enhanced) |
  | 1 | 75 | 25 | e75a | Mask of Prajna (Enhanced) |
  | 1 | 74 | 25 | e74a | Black Dragon |
  | 1 | 37 | 25 | e37a | Mimic (Sun & Moon Temple) |

- **Floor 15** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 53 | 100 | — (no direct record) | (unmapped) |

- **Floor 16** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 53 | 50 | — (no direct record) | (unmapped) |
  | 1 | 59 | 50 | e59a | Dragon |

- **Floor 17** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 75 | 30 | e75a | Mask of Prajna (Enhanced) |
  | 1 | 40 | 40 | e40a | Arthur (Enhanced) |
  | 1 | 74 | 30 | e74a | Black Dragon |

- **Floor 18** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 75 | 30 | e75a | Mask of Prajna (Enhanced) |
  | 1 | 39 | 40 | e39a | Mimic (Moon Sea) |
  | 1 | 74 | 30 | e74a | Black Dragon |

- **Floor 19** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 75 | 30 | e75a | Mask of Prajna (Enhanced) |
  | 1 | 38 | 40 | e38a | King Mimic (Moon Sea) |
  | 1 | 74 | 30 | e74a | Black Dragon |

- **Floor 20** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 75 | 30 | e75a | Mask of Prajna (Enhanced) |
  | 1 | 41 | 40 | — (no direct record) | (unmapped) |
  | 1 | 74 | 30 | e74a | Black Dragon |

- **Floor 21** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 75 | 30 | e75a | Mask of Prajna (Enhanced) |
  | 1 | 42 | 40 | e42a | Ghost |
  | 1 | 74 | 30 | e74a | Black Dragon |

- **Floor 22** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 39 | 20 | e39a | Mimic (Moon Sea) |
  | 1 | 38 | 20 | e38a | King Mimic (Moon Sea) |
  | 1 | 40 | 20 | e40a | Arthur (Enhanced) |
  | 1 | 41 | 20 | — (no direct record) | (unmapped) |
  | 1 | 42 | 20 | e42a | Ghost |

- **Floor 23** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 1 | 53 | 100 | — (no direct record) | (unmapped) |

- **Floor 24** — (no entries)

- **Floor 25** — (no entries)


---

## Dungeon 6 — Moon Sea + Demon Shaft (d07)

### Normal floors

#### `BtEnemyLayout06` @ `0x0028C030`  (100 floors, 0x2bc0 bytes)

- **Floor 0** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 119 | 25 | c17c | Right Hand |
  | 1 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 112 | 25 | c12a | Dran |

- **Floor 1** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 119 | 25 | c17c | Right Hand |
  | 1 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 117 | 25 | c17a | Dark Genie |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |

- **Floor 2** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 120 | 25 | — (no direct record) | Left Hand |
  | 1 | 113 | 25 | c13a | Ice Queen |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 3** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 120 | 25 | — (no direct record) | Left Hand |
  | 1 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 112 | 25 | c12a | Dran |

- **Floor 4** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 119 | 25 | c17c | Right Hand |
  | 1 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 117 | 25 | c17a | Dark Genie |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |

- **Floor 5** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 117 | 25 | c17a | Dark Genie |
  | 1 | 113 | 25 | c13a | Ice Queen |
  | 1 | 112 | 25 | c12a | Dran |

- **Floor 6** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 119 | 25 | c17c | Right Hand |
  | 1 | 113 | 25 | c13a | Ice Queen |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 7** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |

- **Floor 8** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 120 | 25 | — (no direct record) | Left Hand |
  | 1 | 113 | 25 | c13a | Ice Queen |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |
  | 1 | 112 | 25 | c12a | Dran |

- **Floor 9** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 112 | 25 | c12a | Dran |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 10** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 120 | 25 | — (no direct record) | Left Hand |
  | 1 | 117 | 25 | c17a | Dark Genie |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 11** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 113 | 25 | c13a | Ice Queen |
  | 1 | 112 | 25 | c12a | Dran |

- **Floor 12** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 119 | 25 | c17c | Right Hand |
  | 1 | 117 | 25 | c17a | Dark Genie |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 13** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 119 | 25 | c17c | Right Hand |
  | 1 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 113 | 25 | c13a | Ice Queen |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |

- **Floor 14** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 120 | 25 | — (no direct record) | Left Hand |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 112 | 25 | c12a | Dran |

- **Floor 15** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 120 | 25 | — (no direct record) | Left Hand |
  | 1 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 112 | 25 | c12a | Dran |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 16** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 117 | 25 | c17a | Dark Genie |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |

- **Floor 17** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 120 | 25 | — (no direct record) | Left Hand |
  | 1 | 117 | 25 | c17a | Dark Genie |
  | 1 | 113 | 25 | c13a | Ice Queen |

- **Floor 18** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 113 | 25 | c13a | Ice Queen |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 112 | 25 | c12a | Dran |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 19** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 115 | 25 | c15a | King's Curse |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |
  | 1 | 112 | 25 | c12a | Dran |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 20** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 129 | 25 | — (no direct record) | (unmapped) |
  | 1 | 128 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |

- **Floor 21** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 129 | 25 | — (no direct record) | (unmapped) |
  | 1 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 130 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |

- **Floor 22** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 125 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 131 | 25 | — (no direct record) | (unmapped) |

- **Floor 23** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 125 | 25 | — (no direct record) | (unmapped) |
  | 1 | 128 | 25 | — (no direct record) | (unmapped) |
  | 1 | 126 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 24** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 128 | 25 | — (no direct record) | (unmapped) |
  | 1 | 130 | 25 | — (no direct record) | (unmapped) |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |

- **Floor 25** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 125 | 25 | — (no direct record) | (unmapped) |
  | 1 | 130 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |

- **Floor 26** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 129 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 126 | 25 | — (no direct record) | (unmapped) |
  | 1 | 131 | 25 | — (no direct record) | (unmapped) |

- **Floor 27** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 126 | 25 | — (no direct record) | (unmapped) |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 28** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 125 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |

- **Floor 29** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 128 | 25 | — (no direct record) | (unmapped) |
  | 1 | 126 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |
  | 1 | 131 | 25 | — (no direct record) | (unmapped) |

- **Floor 30** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 125 | 25 | — (no direct record) | (unmapped) |
  | 1 | 130 | 25 | — (no direct record) | (unmapped) |
  | 1 | 131 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 31** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 32** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 129 | 25 | — (no direct record) | (unmapped) |
  | 1 | 128 | 25 | — (no direct record) | (unmapped) |
  | 1 | 126 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |

- **Floor 33** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 129 | 25 | — (no direct record) | (unmapped) |
  | 1 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 34** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 125 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 35** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 125 | 25 | — (no direct record) | (unmapped) |
  | 1 | 128 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |
  | 1 | 131 | 25 | — (no direct record) | (unmapped) |

- **Floor 36** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 128 | 25 | — (no direct record) | (unmapped) |
  | 1 | 130 | 25 | — (no direct record) | (unmapped) |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 37** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 130 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |

- **Floor 38** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 126 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 39** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 124 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |
  | 1 | 131 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 40** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 140 | 25 | — (no direct record) | (unmapped) |
  | 1 | 138 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |

- **Floor 41** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 140 | 25 | — (no direct record) | (unmapped) |
  | 1 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 141 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |

- **Floor 42** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 137 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 142 | 25 | — (no direct record) | (unmapped) |

- **Floor 43** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 137 | 25 | — (no direct record) | (unmapped) |
  | 1 | 138 | 25 | — (no direct record) | (unmapped) |
  | 1 | 139 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 44** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 138 | 25 | — (no direct record) | (unmapped) |
  | 1 | 141 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |

- **Floor 45** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 137 | 25 | — (no direct record) | (unmapped) |
  | 1 | 141 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |

- **Floor 46** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 140 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 139 | 25 | — (no direct record) | (unmapped) |
  | 1 | 142 | 25 | — (no direct record) | (unmapped) |

- **Floor 47** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 139 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 48** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 137 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |

- **Floor 49** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 138 | 25 | — (no direct record) | (unmapped) |
  | 1 | 139 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |
  | 1 | 142 | 25 | — (no direct record) | (unmapped) |

- **Floor 50** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 137 | 25 | — (no direct record) | (unmapped) |
  | 1 | 141 | 25 | — (no direct record) | (unmapped) |
  | 1 | 142 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 51** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 52** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 140 | 25 | — (no direct record) | (unmapped) |
  | 1 | 138 | 25 | — (no direct record) | (unmapped) |
  | 1 | 139 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |

- **Floor 53** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 140 | 25 | — (no direct record) | (unmapped) |
  | 1 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 54** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 137 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 55** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 137 | 25 | — (no direct record) | (unmapped) |
  | 1 | 138 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |
  | 1 | 142 | 25 | — (no direct record) | (unmapped) |

- **Floor 56** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 138 | 25 | — (no direct record) | (unmapped) |
  | 1 | 141 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 57** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 141 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |

- **Floor 58** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 139 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 59** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 135 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |
  | 1 | 142 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 60** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 147 | 25 | — (no direct record) | (unmapped) |
  | 1 | 144 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |

- **Floor 61** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 147 | 25 | — (no direct record) | (unmapped) |
  | 1 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 152 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |

- **Floor 62** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 149 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 153 | 25 | — (no direct record) | (unmapped) |

- **Floor 63** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 149 | 25 | — (no direct record) | (unmapped) |
  | 1 | 144 | 25 | — (no direct record) | (unmapped) |
  | 1 | 146 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 64** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 144 | 25 | — (no direct record) | (unmapped) |
  | 1 | 152 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |

- **Floor 65** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 149 | 25 | — (no direct record) | (unmapped) |
  | 1 | 152 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |

- **Floor 66** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 147 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 146 | 25 | — (no direct record) | (unmapped) |
  | 1 | 153 | 25 | — (no direct record) | (unmapped) |

- **Floor 67** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 146 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 68** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 149 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |

- **Floor 69** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 144 | 25 | — (no direct record) | (unmapped) |
  | 1 | 146 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |
  | 1 | 153 | 25 | — (no direct record) | (unmapped) |

- **Floor 70** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 149 | 25 | — (no direct record) | (unmapped) |
  | 1 | 152 | 25 | — (no direct record) | (unmapped) |
  | 1 | 153 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 71** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 72** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 147 | 25 | — (no direct record) | (unmapped) |
  | 1 | 144 | 25 | — (no direct record) | (unmapped) |
  | 1 | 146 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |

- **Floor 73** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 147 | 25 | — (no direct record) | (unmapped) |
  | 1 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 74** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 149 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 75** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 149 | 25 | — (no direct record) | (unmapped) |
  | 1 | 144 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |
  | 1 | 153 | 25 | — (no direct record) | (unmapped) |

- **Floor 76** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 144 | 25 | — (no direct record) | (unmapped) |
  | 1 | 152 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 77** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 152 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |

- **Floor 78** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 146 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 79** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 150 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |
  | 1 | 153 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 80** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 162 | 25 | — (no direct record) | (unmapped) |
  | 1 | 158 | 25 | — (no direct record) | (unmapped) |
  | 1 | 154 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |

- **Floor 81** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 162 | 25 | — (no direct record) | (unmapped) |
  | 1 | 156 | 25 | — (no direct record) | (unmapped) |
  | 1 | 163 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |

- **Floor 82** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 156 | 25 | — (no direct record) | (unmapped) |
  | 1 | 160 | 25 | — (no direct record) | (unmapped) |
  | 1 | 154 | 25 | — (no direct record) | (unmapped) |
  | 1 | 164 | 25 | — (no direct record) | (unmapped) |

- **Floor 83** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 160 | 25 | — (no direct record) | (unmapped) |
  | 1 | 158 | 25 | — (no direct record) | (unmapped) |
  | 1 | 161 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 84** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 156 | 25 | — (no direct record) | (unmapped) |
  | 1 | 158 | 25 | — (no direct record) | (unmapped) |
  | 1 | 163 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |

- **Floor 85** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 160 | 25 | — (no direct record) | (unmapped) |
  | 1 | 163 | 25 | — (no direct record) | (unmapped) |
  | 1 | 154 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |

- **Floor 86** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 162 | 25 | — (no direct record) | (unmapped) |
  | 1 | 154 | 25 | — (no direct record) | (unmapped) |
  | 1 | 161 | 25 | — (no direct record) | (unmapped) |
  | 1 | 164 | 25 | — (no direct record) | (unmapped) |

- **Floor 87** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 156 | 25 | — (no direct record) | (unmapped) |
  | 1 | 161 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 88** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 160 | 25 | — (no direct record) | (unmapped) |
  | 1 | 154 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |

- **Floor 89** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 158 | 25 | — (no direct record) | (unmapped) |
  | 1 | 161 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |
  | 1 | 164 | 25 | — (no direct record) | (unmapped) |

- **Floor 90** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 160 | 25 | — (no direct record) | (unmapped) |
  | 1 | 163 | 25 | — (no direct record) | (unmapped) |
  | 1 | 164 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 91** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 156 | 25 | — (no direct record) | (unmapped) |
  | 1 | 154 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 92** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 162 | 25 | — (no direct record) | (unmapped) |
  | 1 | 158 | 25 | — (no direct record) | (unmapped) |
  | 1 | 161 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |

- **Floor 93** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 156 | 25 | — (no direct record) | (unmapped) |
  | 1 | 161 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 94** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 156 | 25 | — (no direct record) | (unmapped) |
  | 1 | 160 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 95** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 160 | 25 | — (no direct record) | (unmapped) |
  | 1 | 158 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |
  | 1 | 164 | 25 | — (no direct record) | (unmapped) |

- **Floor 96** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 158 | 25 | — (no direct record) | (unmapped) |
  | 1 | 163 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 97** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 163 | 25 | — (no direct record) | (unmapped) |
  | 1 | 154 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |

- **Floor 98** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 159 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |
  | 1 | 164 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 99** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 1 | 166 | 50 | — (no direct record) | (unmapped) |
  | 1 | 165 | 50 | — (no direct record) | (unmapped) |

### Ura (back) floors

#### `BtUraEnemyLayout06` @ `0x0028EBF0`  (100 floors, 0x2bc0 bytes)

- **Floor 0** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 120 | 25 | — (no direct record) | Left Hand |
  | 1 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 112 | 25 | c12a | Dran |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 1** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 117 | 25 | c17a | Dark Genie |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |

- **Floor 2** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 120 | 25 | — (no direct record) | Left Hand |
  | 1 | 117 | 25 | c17a | Dark Genie |
  | 1 | 113 | 25 | c13a | Ice Queen |

- **Floor 3** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 113 | 25 | c13a | Ice Queen |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 112 | 25 | c12a | Dran |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 4** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 115 | 25 | c15a | King's Curse |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |
  | 1 | 112 | 25 | c12a | Dran |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 5** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 119 | 25 | c17c | Right Hand |
  | 1 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 112 | 25 | c12a | Dran |

- **Floor 6** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 119 | 25 | c17c | Right Hand |
  | 1 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 117 | 25 | c17a | Dark Genie |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |

- **Floor 7** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 4 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 120 | 25 | — (no direct record) | Left Hand |
  | 1 | 113 | 25 | c13a | Ice Queen |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 8** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 120 | 25 | — (no direct record) | Left Hand |
  | 1 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 112 | 25 | c12a | Dran |

- **Floor 9** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 119 | 25 | c17c | Right Hand |
  | 1 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 117 | 25 | c17a | Dark Genie |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |

- **Floor 10** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 117 | 25 | c17a | Dark Genie |
  | 1 | 113 | 25 | c13a | Ice Queen |
  | 1 | 112 | 25 | c12a | Dran |

- **Floor 11** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 119 | 25 | c17c | Right Hand |
  | 1 | 113 | 25 | c13a | Ice Queen |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 12** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 6 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |

- **Floor 13** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 5 | 120 | 25 | — (no direct record) | Left Hand |
  | 1 | 113 | 25 | c13a | Ice Queen |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |
  | 1 | 112 | 25 | c12a | Dran |

- **Floor 14** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 112 | 25 | c12a | Dran |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 15** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 120 | 25 | — (no direct record) | Left Hand |
  | 1 | 117 | 25 | c17a | Dark Genie |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 16** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 111 | 25 | — (no direct record) | (unmapped) |
  | 1 | 113 | 25 | c13a | Ice Queen |
  | 1 | 112 | 25 | c12a | Dran |

- **Floor 17** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 119 | 25 | c17c | Right Hand |
  | 1 | 117 | 25 | c17a | Dark Genie |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 114 | 25 | c14a | Master Utan |

- **Floor 18** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 119 | 25 | c17c | Right Hand |
  | 1 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 113 | 25 | c13a | Ice Queen |
  | 1 | 118 | 25 | c17b | Dark Genie (form 2) |

- **Floor 19** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 7 | 116 | 25 | c16a | Minotaur Joe |
  | 1 | 120 | 25 | — (no direct record) | Left Hand |
  | 1 | 115 | 25 | c15a | King's Curse |
  | 1 | 112 | 25 | c12a | Dran |

- **Floor 20** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 128 | 25 | — (no direct record) | (unmapped) |
  | 1 | 130 | 25 | — (no direct record) | (unmapped) |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 21** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 130 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |

- **Floor 22** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 126 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 23** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 124 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |
  | 1 | 131 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 24** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 129 | 25 | — (no direct record) | (unmapped) |
  | 1 | 128 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |

- **Floor 25** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 129 | 25 | — (no direct record) | (unmapped) |
  | 1 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 130 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |

- **Floor 26** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 125 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 131 | 25 | — (no direct record) | (unmapped) |

- **Floor 27** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 125 | 25 | — (no direct record) | (unmapped) |
  | 1 | 128 | 25 | — (no direct record) | (unmapped) |
  | 1 | 126 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 28** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 128 | 25 | — (no direct record) | (unmapped) |
  | 1 | 130 | 25 | — (no direct record) | (unmapped) |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |

- **Floor 29** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 125 | 25 | — (no direct record) | (unmapped) |
  | 1 | 130 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |

- **Floor 30** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 129 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 126 | 25 | — (no direct record) | (unmapped) |
  | 1 | 131 | 25 | — (no direct record) | (unmapped) |

- **Floor 31** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 126 | 25 | — (no direct record) | (unmapped) |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 32** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 125 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |

- **Floor 33** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 128 | 25 | — (no direct record) | (unmapped) |
  | 1 | 126 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |
  | 1 | 131 | 25 | — (no direct record) | (unmapped) |

- **Floor 34** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 125 | 25 | — (no direct record) | (unmapped) |
  | 1 | 130 | 25 | — (no direct record) | (unmapped) |
  | 1 | 131 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 35** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 121 | 25 | e85a | Wine Keg |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 36** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 129 | 25 | — (no direct record) | (unmapped) |
  | 1 | 128 | 25 | — (no direct record) | (unmapped) |
  | 1 | 126 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |

- **Floor 37** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 129 | 25 | — (no direct record) | (unmapped) |
  | 1 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 124 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 38** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 122 | 25 | — (no direct record) | (unmapped) |
  | 1 | 125 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |
  | 1 | 127 | 25 | — (no direct record) | (unmapped) |

- **Floor 39** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 125 | 25 | — (no direct record) | (unmapped) |
  | 1 | 128 | 25 | — (no direct record) | (unmapped) |
  | 1 | 123 | 25 | — (no direct record) | (unmapped) |
  | 1 | 131 | 25 | — (no direct record) | (unmapped) |

- **Floor 40** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 137 | 25 | — (no direct record) | (unmapped) |
  | 1 | 138 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |
  | 1 | 142 | 25 | — (no direct record) | (unmapped) |

- **Floor 41** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 138 | 25 | — (no direct record) | (unmapped) |
  | 1 | 141 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 42** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 141 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |

- **Floor 43** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 139 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 44** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 135 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |
  | 1 | 142 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 45** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 140 | 25 | — (no direct record) | (unmapped) |
  | 1 | 138 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |

- **Floor 46** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 140 | 25 | — (no direct record) | (unmapped) |
  | 1 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 141 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |

- **Floor 47** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 137 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 142 | 25 | — (no direct record) | (unmapped) |

- **Floor 48** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 137 | 25 | — (no direct record) | (unmapped) |
  | 1 | 138 | 25 | — (no direct record) | (unmapped) |
  | 1 | 139 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 49** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 138 | 25 | — (no direct record) | (unmapped) |
  | 1 | 141 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |

- **Floor 50** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 137 | 25 | — (no direct record) | (unmapped) |
  | 1 | 141 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |

- **Floor 51** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 140 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 139 | 25 | — (no direct record) | (unmapped) |
  | 1 | 142 | 25 | — (no direct record) | (unmapped) |

- **Floor 52** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 139 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 53** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 137 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |

- **Floor 54** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 138 | 25 | — (no direct record) | (unmapped) |
  | 1 | 139 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |
  | 1 | 142 | 25 | — (no direct record) | (unmapped) |

- **Floor 55** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 137 | 25 | — (no direct record) | (unmapped) |
  | 1 | 141 | 25 | — (no direct record) | (unmapped) |
  | 1 | 142 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 56** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 132 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 57** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 140 | 25 | — (no direct record) | (unmapped) |
  | 1 | 138 | 25 | — (no direct record) | (unmapped) |
  | 1 | 139 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |

- **Floor 58** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 140 | 25 | — (no direct record) | (unmapped) |
  | 1 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 135 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 59** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 134 | 25 | — (no direct record) | (unmapped) |
  | 1 | 137 | 25 | — (no direct record) | (unmapped) |
  | 1 | 133 | 25 | — (no direct record) | (unmapped) |
  | 1 | 136 | 25 | — (no direct record) | (unmapped) |

- **Floor 60** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 149 | 25 | — (no direct record) | (unmapped) |
  | 1 | 144 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |
  | 1 | 153 | 25 | — (no direct record) | (unmapped) |

- **Floor 61** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 144 | 25 | — (no direct record) | (unmapped) |
  | 1 | 152 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 62** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 152 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |

- **Floor 63** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 146 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 64** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 150 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |
  | 1 | 153 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 65** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 147 | 25 | — (no direct record) | (unmapped) |
  | 1 | 144 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |

- **Floor 66** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 147 | 25 | — (no direct record) | (unmapped) |
  | 1 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 152 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |

- **Floor 67** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 149 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 153 | 25 | — (no direct record) | (unmapped) |

- **Floor 68** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 149 | 25 | — (no direct record) | (unmapped) |
  | 1 | 144 | 25 | — (no direct record) | (unmapped) |
  | 1 | 146 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 69** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 144 | 25 | — (no direct record) | (unmapped) |
  | 1 | 152 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |

- **Floor 70** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 149 | 25 | — (no direct record) | (unmapped) |
  | 1 | 152 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |

- **Floor 71** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 147 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 146 | 25 | — (no direct record) | (unmapped) |
  | 1 | 153 | 25 | — (no direct record) | (unmapped) |

- **Floor 72** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 146 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 73** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 149 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |

- **Floor 74** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 144 | 25 | — (no direct record) | (unmapped) |
  | 1 | 146 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |
  | 1 | 153 | 25 | — (no direct record) | (unmapped) |

- **Floor 75** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 149 | 25 | — (no direct record) | (unmapped) |
  | 1 | 152 | 25 | — (no direct record) | (unmapped) |
  | 1 | 153 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 76** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 143 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 77** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 147 | 25 | — (no direct record) | (unmapped) |
  | 1 | 144 | 25 | — (no direct record) | (unmapped) |
  | 1 | 146 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |

- **Floor 78** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 147 | 25 | — (no direct record) | (unmapped) |
  | 1 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 150 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 79** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 148 | 25 | — (no direct record) | (unmapped) |
  | 1 | 149 | 25 | — (no direct record) | (unmapped) |
  | 1 | 151 | 25 | — (no direct record) | (unmapped) |
  | 1 | 145 | 25 | — (no direct record) | (unmapped) |

- **Floor 80** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 156 | 25 | — (no direct record) | (unmapped) |
  | 1 | 160 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 81** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 160 | 25 | — (no direct record) | (unmapped) |
  | 1 | 158 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |
  | 1 | 164 | 25 | — (no direct record) | (unmapped) |

- **Floor 82** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 158 | 25 | — (no direct record) | (unmapped) |
  | 1 | 163 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 83** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 163 | 25 | — (no direct record) | (unmapped) |
  | 1 | 154 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |

- **Floor 84** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 159 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |
  | 1 | 164 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 85** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 162 | 25 | — (no direct record) | (unmapped) |
  | 1 | 158 | 25 | — (no direct record) | (unmapped) |
  | 1 | 154 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |

- **Floor 86** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 162 | 25 | — (no direct record) | (unmapped) |
  | 1 | 156 | 25 | — (no direct record) | (unmapped) |
  | 1 | 163 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |

- **Floor 87** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 156 | 25 | — (no direct record) | (unmapped) |
  | 1 | 160 | 25 | — (no direct record) | (unmapped) |
  | 1 | 154 | 25 | — (no direct record) | (unmapped) |
  | 1 | 164 | 25 | — (no direct record) | (unmapped) |

- **Floor 88** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 160 | 25 | — (no direct record) | (unmapped) |
  | 1 | 158 | 25 | — (no direct record) | (unmapped) |
  | 1 | 161 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 89** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 156 | 25 | — (no direct record) | (unmapped) |
  | 1 | 158 | 25 | — (no direct record) | (unmapped) |
  | 1 | 163 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |

- **Floor 90** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 160 | 25 | — (no direct record) | (unmapped) |
  | 1 | 163 | 25 | — (no direct record) | (unmapped) |
  | 1 | 154 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |

- **Floor 91** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 162 | 25 | — (no direct record) | (unmapped) |
  | 1 | 154 | 25 | — (no direct record) | (unmapped) |
  | 1 | 161 | 25 | — (no direct record) | (unmapped) |
  | 1 | 164 | 25 | — (no direct record) | (unmapped) |

- **Floor 92** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 156 | 25 | — (no direct record) | (unmapped) |
  | 1 | 161 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 93** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 160 | 25 | — (no direct record) | (unmapped) |
  | 1 | 154 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |

- **Floor 94** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 158 | 25 | — (no direct record) | (unmapped) |
  | 1 | 161 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |
  | 1 | 164 | 25 | — (no direct record) | (unmapped) |

- **Floor 95** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 160 | 25 | — (no direct record) | (unmapped) |
  | 1 | 163 | 25 | — (no direct record) | (unmapped) |
  | 1 | 164 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 96** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 156 | 25 | — (no direct record) | (unmapped) |
  | 1 | 154 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 97** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 162 | 25 | — (no direct record) | (unmapped) |
  | 1 | 158 | 25 | — (no direct record) | (unmapped) |
  | 1 | 161 | 25 | — (no direct record) | (unmapped) |
  | 1 | 155 | 25 | — (no direct record) | (unmapped) |

- **Floor 98** — weight sum 100

  | cnt(+0) | id(+4) | wt%(+8) | disc model (ELF) | EnemyData.cs assignment |
  |--:|--:|--:|:--|:--|
  | 8 | 156 | 25 | — (no direct record) | (unmapped) |
  | 1 | 161 | 25 | — (no direct record) | (unmapped) |
  | 1 | 159 | 25 | — (no direct record) | (unmapped) |
  | 1 | 157 | 25 | — (no direct record) | (unmapped) |

- **Floor 99** — (no entries)


---
