# The species "monster type" field (record +0x78) and the enemy id (+0x7C)

`EnemySpeciesTable.MonsterType` (+0x78, a halfword in an int-sized slot; formerly misnamed `SpawnCap`) is copied to the
spawned unit at +0x1E410 by `SetupViewMonstor` (main 0x1E0F80 region), along with the enemy id (+0x7C → unit +0x1E412).
Every reader of the unit copy (the only readers — nothing reads the table field directly at runtime except the
placement loop):

| reader | address | what the value does |
|---|---|---|
| `ArrangementPos` (floor placement) | main 0x1D82B0 | `type != 0 && type != 3` → the species may occupy ONE slot per floor (the loop retries other species for the rest); 0 and 3 fill freely |
| `CheckViewLevel` | main 0x1D9D08 | a unit in view state 2 with HP is activated (state → 1) only when `type != 2` — bosses are started by their script |
| `SoundCheck` | main 0x1D8A30 | `type == 2` → sound distances 350 / 1000 instead of 50 / 500 |
| `CheckDmg` | main 0x1DC2E8 | `type == 2` → immune to the Critical ability (docs/game-formulas.md) |
| `Step` (death block) | main 0x1DF4A4 | `type != 2` required for the 10 % rare-drop roll (`+0x1E470` ← `+0x1E4B0`) |
| `setTargetCursor` (dun) | 0x1DC090C, 0x1DC0B2C | `type != 2` → the lock-on cursor box is built from the unit's own extents; the HP gauge turns on only when `id > 0 && type != 2` |
| `DrawTargetLife` (dun) | 0x1DBFD10 | draws the gauge only while the flag set above holds |
| `setTargetCursor`, `OpC_DrawProcess` (dun) | 0x1DC0B18…, 0x1DC0748 | `id` (+0x1E412) names the target (message lookup) and, with weapon 303 or 312 (Steve) equipped, a 3 % chance per lock shows monster chatter message `4000 + id × 10` |

## Shipped values

| type | species |
|---|---|
| 0 | every regular enemy (id > 0) |
| 2 | bosses (c12a Dran … c23a), boss companions (c17_* Dark Genie parts, b3_reiki, e85a, e124a), all with id 0 or their own |
| 3 | the dungeon mimics (e35a, e37a, e39a, e79a, e81a, e83a, e109a) |
| 4 | the king mimics (e34a, e36a, e38a, e78a, e80a, e82a, e110a) |
| 1 | not shipped — only the table's terminator row |

So **1 is a clean "once per floor"**: the placement loop treats it as once, and no other reader tests for it. The
randomizer's one-of-each floors already write 1; the test injector wrote 2 for its spawn-once species, which turned
them into bosses to every other reader (the Ice Gemron with no HP gauge, 2026-09-19) — it writes 1 now, and keeps 2
only for boss-class species, whose vanilla value it is.

+0x7A and +0x7E are zero on every row.
