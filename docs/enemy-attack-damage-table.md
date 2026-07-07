# Enemy attack table

Per-species attacks — **one row per attack** — decoded from each enemy's behaviour script
`dun/monstor/<code>.stb` in `data.dat`, cross-referenced with the shot-effect table in `SCUS_971.11`.
Sorted by `TableIndex`; an enemy with several attacks spans several rows (name shown once, `〃` after).

## How an attack hits you

Applied damage = **`Dmg − playerDefense`** (clamped > 0), computed in `BtCheckDamageProc`
(dun overlay `0x01DBAFD0`). Beyond damage, every attack carries a **reaction type** (guardability +
knockdown) and a **status-flag word** (ailments) — for melee both are per-attack STB arguments; for
projectiles both are baked into the shot type.

### Reaction type — guardability + knockdown (one attribute, values 2/3/4)

`BtCheckDamageProc` branches on the attack's reaction type (`CCollisionData` entry `+0x4C`); it is the
single field that governs *both* guard-break and knock-over:

| Reaction | Guardable? | If unguarded |
|---|---|---|
| **Knockback (2)** | **Yes** — fully blocked while guarding (0 dmg, small pushback) | damage + medium stagger (~80f); heavier reaction past a stagger threshold |
| **Knockdown (3)** | **No — breaks guard** | damage + hard knockdown / launch (`unitBlowActionRot`), long stun (~160f) |
| **Light (4)** | **Yes** | minimal flinch (~8f), no knockover |

- **Melee**: `_SET_DMG_PARA(dmg, statusFlags, reaction, [launch])` — reaction is the 3rd STB argument,
  per-attack (a combo's finisher can knock down while its openers don't). Only reaction-3 attacks read
  the 4th (launch) argument.
- **Projectile**: fixed per **shot type** (`BT_SHOT_EFFECT+0x44`), selected by the species record's
  `+0x68` (`_SET_SHOT`) / `+0x6A` (`_SET_SHOT2`) indices through the pointer array `0x27FA70` — the STB
  can override a shot's *damage* but not its reaction or status.

### Status flags — melee arg1 / shot `BT_SHOT_EFFECT+0x40`

Both melee and projectiles carry a status-flag word: melee via `_SET_DMG_PARA`'s **2nd argument**
(`+0x5A590` → collision entry `+0x50`), shots via their shot type's `+0x40`. `BtCheckDamageProc`
rolls each set bit on hit (ailments at ~65%, blocked by the matching amulet; Steal at 20%):

| Status | Effect | Blocked by | bit |
|---|---|---|---|
| Freeze | freeze (65% roll) | Anti-Freeze Amulet | `0x100` |
| Poison | poison DoT (65%) | Antidote Amulet | `0x200` |
| Curse | curse (65%) | Anti-Curse Amulet | `0x400` |
| Goo | slow/goo (65%) | Anti-Goo Amulet | `0x800` |
| Stamina | stamina drain (65%) | — | `0x1000` |
| Steal | steals ⅕ of your gold (20%), dropped as loot | — | `0x40000` |
| HalfHP | damage = **half your current HP** (replaces the dmg formula; the King Mimic bite) | — | `0x80000` |
| Freeze! | **guaranteed** freeze (no roll, no amulet) | — | `0x100000` |

Low bits (`0x1`–`0x80`) are non-status shot/element flags and are not shown. A separate command,
`_SET_STATUS_ERR`, applies an ailment directly for a few scripted status attacks.

### Damage column forms (projectiles)

`N` = fixed STB literal · `N var` = distance-scaled (`N` is the point-blank max, scaled down with range) ·
`N def` = no explicit STB damage, uses the shot type's `BT_SHOT_EFFECT+0x3C` default. `—` = mechanism
unused; an all-`—` enemy is a non-attacker by design (Ice Queen shell, King's Curse Coffin, Ice Barrier,
Dark Genie effect entities — those bosses damage you through spawned companions/hands, noted per row).

| TableIndex | Id | Name | Model | Attack | Kind | Dmg | Reaction | Status | Notes |
|---:|---:|---|---|---|---|---|---|---|---|
| 0 | 1 | Master Jacket | `e01a` | Melee 1 | Melee | 35 | Knockback (2) | — |  |
| 0 | 1 | 〃 | `e01a` | Melee 2 | Melee | 30 | Knockback (2) | — |  |
| 1 | 3 | Skeleton Soldier | `e03a` | Melee 1 | Melee | 20 | Knockback (2) | — |  |
| 1 | 3 | 〃 | `e03a` | Melee 2 | Melee | 21 | Knockback (2) | — |  |
| 2 | 5 | Statue | `e05a` | Melee 1 | Melee | 26 | Knockdown (3) | — |  |
| 2 | 5 | 〃 | `e05a` | Melee 2 | Melee | 25 | Knockdown (3) | — |  |
| 2 | 5 | 〃 | `e05a` | Melee 3 | Melee | 26 | Knockdown (3) | — |  |
| 2 | 5 | 〃 | `e05a` | Melee 4 | Melee | 25 | Knockdown (3) | — |  |
| 3 | 6 | Dasher | `e06a` | Melee | Melee | 22 | Knockdown (3) | — |  |
| 4 | 7 | Werewolf | `e07a` | Melee 1 | Melee | 68 | Knockdown (3) | — |  |
| 4 | 7 | 〃 | `e07a` | Melee 2 | Melee | 62 | Knockdown (3) | — |  |
| 5 | 8 | FliFli | `e08a` | Melee | Melee | 37 | Knockback (2) | — |  |
| 5 | 8 | 〃 | `e08a` | Shot | Projectile | 35 | Knockback (2) | Poison |  |
| 6 | 9 | Hornet | `e09a` | Melee 1 | Melee | 42 | Knockback (2) | Poison |  |
| 6 | 9 | 〃 | `e09a` | Melee 2 | Melee | 40 | Knockback (2) | — |  |
| 7 | 10 | Halloween | `e10a` | Melee 1 | Melee | 57 | Knockback (2) | — |  |
| 7 | 10 | 〃 | `e10a` | Melee 2 | Melee | 57 | Knockback (2) | — |  |
| 7 | 10 | 〃 | `e10a` | Shot | Projectile | 60 | Knockback (2) | — |  |
| 8 | 11 | Cannibal Plant | `e11a` | Melee 1 | Melee | 28 | Knockdown (3) | — |  |
| 8 | 11 | 〃 | `e11a` | Melee 2 | Melee | 28 | Knockdown (3) | — |  |
| 8 | 11 | 〃 | `e11a` | Melee 3 | Melee | 36 | Knockdown (3) | — |  |
| 8 | 11 | 〃 | `e11a` | Shot | Projectile | 30 var | Knockback (2) | Goo |  |
| 9 | 12 | Earth Digger | `e12a` | Melee | Melee | 52 | Knockdown (3) | — |  |
| 9 | 12 | 〃 | `e12a` | Shot | Projectile | 37 var | Knockback (2) | Freeze |  |
| 10 | 14 | Sunday | `e14a` | Melee 1 | Melee | 36 | Knockback (2) | Steal |  |
| 10 | 14 | 〃 | `e14a` | Melee 2 | Melee | 26 | Knockback (2) | Steal |  |
| 11 | 15 | Monday | `e15a` | Melee 1 | Melee | 32 | Knockback (2) | Steal |  |
| 11 | 15 | 〃 | `e15a` | Melee 2 | Melee | 12 | Knockback (2) | Steal |  |
| 12 | 16 | Tuesday | `e16a` | Melee | Melee | 31 | Knockback (2) | Steal |  |
| 12 | 16 | 〃 | `e16a` | Shot | Projectile | 31 | Knockback (2) | Poison |  |
| 13 | 17 | Wednesday | `e17a` | Melee 1 | Melee | 30 | Knockdown (3) | Steal |  |
| 13 | 17 | 〃 | `e17a` | Melee 2 | Melee | 28 | Knockback (2) | Steal |  |
| 14 | 18 | Thursday | `e18a` | Melee | Melee | 29 | Knockback (2) | Steal | BST default `ringo_ex` (idx4) |
| 14 | 18 | 〃 | `e18a` | Shot | Projectile | 30 def | Knockback (2) | Poison |  |
| 15 | 19 | Friday | `e19a` | Melee 1 | Melee | 29 | Knockdown (3) | — |  |
| 15 | 19 | 〃 | `e19a` | Melee 2 | Melee | 29 | Knockback (2) | Steal |  |
| 16 | 20 | Saturday | `e20a` | Melee 1 | Melee | 29 | Knockback (2) | Steal |  |
| 16 | 20 | 〃 | `e20a` | Melee 2 | Melee | 29 | Knockback (2) | Steal |  |
| 16 | 20 | 〃 | `e20a` | Melee 3 | Melee | 25 | Knockback (2) | Steal |  |
| 17 | 21 | Witch Hellza | `e21a` | Melee | Melee | 73 | Knockback (2) | — |  |
| 17 | 21 | 〃 | `e21a` | Shot | Projectile | 73 var | Knockdown (3) | — |  |
| 18 | 22 | Witch Illza | `e22a` | Melee | Melee | 47 | Knockback (2) | — |  |
| 18 | 22 | 〃 | `e22a` | Shot | Projectile | 47 var | Knockback (2) | Poison |  |
| 19 | 23 | Gunny | `e23a` | Melee 1 | Melee | 44 | Knockback (2) | — |  |
| 19 | 23 | 〃 | `e23a` | Melee 2 | Melee | 44 | Knockback (2) | — |  |
| 19 | 23 | 〃 | `e23a` | Shot | Projectile | 26 | Knockback (2) | Poison |  |
| 20 | 24 | Gyon | `e24a` | Melee 1 | Melee | 59 | Knockback (2) | — |  |
| 20 | 24 | 〃 | `e24a` | Melee 2 | Melee | 59 | Knockback (2) | — |  |
| 20 | 24 | 〃 | `e24a` | Shot | Projectile | 59 var | Knockback (2) | Goo |  |
| 21 | 25 | Pirate's Chariot | `e25a` | Shot | Projectile | 69 | Knockdown (3) | — |  |
| 22 | 26 | Auntie Medu | `e26a` | Melee | Melee | 74 | Knockback (2) | — |  |
| 22 | 26 | 〃 | `e26a` | Shot | Projectile | 60 | Knockback (2) | Freeze |  |
| 23 | 27 | Captain | `e27a` | Melee 1 | Melee | 74 | Knockback (2) | — |  |
| 23 | 27 | 〃 | `e27a` | Melee 2 | Melee | 74 | Knockback (2) | — |  |
| 23 | 27 | 〃 | `e27a` | Melee 3 | Melee | 72 | Knockback (2) | — |  |
| 23 | 27 | 〃 | `e27a` | Melee 4 | Melee | 72 | Knockback (2) | — |  |
| 24 | 28 | Corcea | `e28a` | Melee 1 | Melee | 43 | Knockback (2) | — |  |
| 24 | 28 | 〃 | `e28a` | Melee 2 | Melee | 43 | Knockback (2) | — |  |
| 24 | 28 | 〃 | `e28a` | Melee 3 | Melee | 42 | Knockback (2) | — |  |
| 24 | 28 | 〃 | `e28a` | Melee 4 | Melee | 42 | Knockback (2) | — |  |
| 25 | 30 | Golem | `e30a` | Melee 1 | Melee | 71 | Knockdown (3) | — | shot1 `g_wave1` var 64; shot2 BST default 34 |
| 25 | 30 | 〃 | `e30a` | Melee 2 | Melee | 71 | Knockdown (3) | — |  |
| 25 | 30 | 〃 | `e30a` | Melee 3 | Melee | 75 | Knockdown (3) | — |  |
| 25 | 30 | 〃 | `e30a` | Melee 4 | Melee | 75 | Knockdown (3) | — |  |
| 25 | 30 | 〃 | `e30a` | Melee 5 | Melee | 62 | Knockdown (3) | — |  |
| 25 | 30 | 〃 | `e30a` | Melee 6 | Melee | 55 | Knockdown (3) | — |  |
| 25 | 30 | 〃 | `e30a` | Melee 7 | Melee | 45 | Knockdown (3) | — |  |
| 25 | 30 | 〃 | `e30a` | Shot 1 | Projectile | 64 var | Knockdown (3) | — |  |
| 25 | 30 | 〃 | `e30a` | Shot 2 | Projectile | 34 def | Knockdown (3) | — |  |
| 26 | 31 | Mr. Blare | `e31a` | Melee 1 | Melee | 81 | Knockback (2) | — |  |
| 26 | 31 | 〃 | `e31a` | Melee 2 | Melee | 81 | Knockback (2) | — |  |
| 26 | 31 | 〃 | `e31a` | Shot 1 | Projectile | 80 | Knockdown (3) | — |  |
| 26 | 31 | 〃 | `e31a` | Shot 2 | Projectile | 90 | Knockdown (3) | — |  |
| 27 | 32 | Dune | `e32a` | Melee 1 | Melee | 85 | Knockback (2) | — |  |
| 27 | 32 | 〃 | `e32a` | Melee 2 | Melee | 85 | Knockback (2) | — |  |
| 27 | 32 | 〃 | `e32a` | Melee 3 | Melee | 85 | Knockback (2) | — |  |
| 28 | 33 | Titan | `e33a` | Melee 1 | Melee | 105 | Knockdown (3) | — |  |
| 28 | 33 | 〃 | `e33a` | Melee 2 | Melee | 105 | Knockdown (3) | — |  |
| 28 | 33 | 〃 | `e33a` | Melee 3 | Melee | 105 | Knockdown (3) | — |  |
| 28 | 33 | 〃 | `e33a` | Melee 4 | Melee | 105 | Knockdown (3) | — |  |
| 28 | 33 | 〃 | `e33a` | Melee 5 | Melee | 90 | Knockdown (3) | — |  |
| 28 | 33 | 〃 | `e33a` | Melee 6 | Melee | 75 | Knockdown (3) | — |  |
| 28 | 33 | 〃 | `e33a` | Melee 7 | Melee | 60 | Knockdown (3) | — |  |
| 28 | 33 | 〃 | `e33a` | Shot 1 | Projectile | 90 var | Knockdown (3) | — |  |
| 28 | 33 | 〃 | `e33a` | Shot 2 | Projectile | 90 | Knockdown (3) | — |  |
| 29 | 34 | King Mimic (Divine Beast Cave) | `e34a` | Melee 1 | Melee | 35 | Knockback (2) | — |  |
| 29 | 34 | 〃 | `e34a` | Melee 2 | Melee | 30 | Knockback (2) | — |  |
| 29 | 34 | 〃 | `e34a` | Melee 3 | Melee | 35 | Knockback (2) | HalfHP |  |
| 30 | 35 | Mimic (Divine Beast Cave) | `e35a` | Melee 1 | Melee | 33 | Knockback (2) | — |  |
| 30 | 35 | 〃 | `e35a` | Melee 2 | Melee | 33 | Knockback (2) | — |  |
| 31 | 36 | King Mimic (Sun & Moon Temple) | `e36a` | Melee 1 | Melee | 101 | Knockback (2) | — |  |
| 31 | 36 | 〃 | `e36a` | Melee 2 | Melee | 102 | Knockback (2) | — |  |
| 31 | 36 | 〃 | `e36a` | Melee 3 | Melee | 45 | Knockback (2) | HalfHP |  |
| 32 | 37 | Mimic (Sun & Moon Temple) | `e37a` | Melee 1 | Melee | 71 | Knockback (2) | — |  |
| 32 | 37 | 〃 | `e37a` | Melee 2 | Melee | 71 | Knockback (2) | — |  |
| 33 | 38 | King Mimic (Moon Sea) | `e38a` | Melee 1 | Melee | 118 | Knockback (2) | — |  |
| 33 | 38 | 〃 | `e38a` | Melee 2 | Melee | 96 | Knockback (2) | — |  |
| 33 | 38 | 〃 | `e38a` | Melee 3 | Melee | 90 | Knockback (2) | HalfHP |  |
| 34 | 39 | Mimic (Moon Sea) | `e39a` | Melee 1 | Melee | 83 | Knockback (2) | — |  |
| 34 | 39 | 〃 | `e39a` | Melee 2 | Melee | 83 | Knockback (2) | — |  |
| 35 | 40 | Arthur | `e40a` | Melee 1 | Melee | 116 | Knockdown (3) | — |  |
| 35 | 40 | 〃 | `e40a` | Melee 2 | Melee | 116 | Knockdown (3) | — |  |
| 36 | 42 | Ghost | `e42a` | Melee 1 | Melee | 20 | Knockback (2) | Poison |  |
| 36 | 42 | 〃 | `e42a` | Melee 2 | Melee | 20 | Knockback (2) | Poison |  |
| 36 | 42 | 〃 | `e42a` | Shot | Projectile | 21 var | Knockback (2) | Curse |  |
| 37 | 43 | Alexander | `e43a` | Melee | Melee | 120 | Knockback (2) | Poison |  |
| 37 | 43 | 〃 | `e43a` | Shot | Projectile | 124 | Knockdown (3) | — |  |
| 38 | 44 | Heart | `e44a` | Melee | Melee | 107 | Knockback (2) | — | funcId-229 shot (107); the 133 `magic_bin` is a separate 0-dmg bind |
| 38 | 44 | 〃 | `e44a` | Shot 1 | Projectile | 107 | Knockback (2) | Stamina |  |
| 38 | 44 | 〃 | `e44a` | Shot 2 | Projectile | 107 | Knockdown (3) | — |  |
| 39 | 45 | Club | `e45a` | Melee | Melee | 104 | Knockback (2) | — |  |
| 40 | 46 | Diamond | `e46a` | Melee 1 | Melee | 110 | Knockdown (3) | — |  |
| 40 | 46 | 〃 | `e46a` | Melee 2 | Melee | 110 | Knockdown (3) | — |  |
| 41 | 47 | Spade | `e47a` | Melee 1 | Melee | 113 | Knockback (2) | — |  |
| 41 | 47 | 〃 | `e47a` | Melee 2 | Melee | 113 | Knockdown (3) | — |  |
| 42 | 48 | Joker | `e48a` | Melee | Melee | 115 | Knockback (2) | — |  |
| 43 | 49 | Bomber Head | `e49a` | Melee 1 | Melee | 61 | Knockdown (3) | — |  |
| 43 | 49 | 〃 | `e49a` | Melee 2 | Melee | 61 | Knockdown (3) | — |  |
| 43 | 49 | 〃 | `e49a` | Shot | Projectile | 64 | Knockdown (3) | — |  |
| 44 | 50 | Mummy | `e50a` | Melee 1 | Melee | 54 | Knockback (2) | Curse |  |
| 44 | 50 | 〃 | `e50a` | Melee 2 | Melee | 54 | Knockback (2) | Curse |  |
| 44 | 50 | 〃 | `e50a` | Melee 3 | Melee | 54 | Knockback (2) | Curse |  |
| 45 | 51 | Lich | `e51a` | Melee 1 | Melee | 114 | Knockback (2) | — |  |
| 45 | 51 | 〃 | `e51a` | Melee 2 | Melee | 114 | Knockback (2) | — |  |
| 45 | 51 | 〃 | `e51a` | Shot | Projectile | 100 var | Knockback (2) | Freeze |  |
| 46 | 52 | Curse Dancer | `e52a` | Melee 1 | Melee | 90 | Knockback (2) | Curse |  |
| 46 | 52 | 〃 | `e52a` | Melee 2 | Melee | 90 | Knockback (2) | Curse |  |
| 46 | 52 | 〃 | `e52a` | Melee 3 | Melee | 90 | Knockback (2) | Curse |  |
| 46 | 52 | 〃 | `e52a` | Melee 4 | Melee | 90 | Knockback (2) | Curse |  |
| 47 | 55 | Living Armor | `e55a` | Melee 1 | Melee | 95 | Knockdown (3) | — |  |
| 47 | 55 | 〃 | `e55a` | Melee 2 | Melee | 95 | Knockdown (3) | — |  |
| 47 | 55 | 〃 | `e55a` | Melee 3 | Melee | 95 | Knockdown (3) | — |  |
| 47 | 55 | 〃 | `e55a` | Melee 4 | Melee | 95 | Knockdown (3) | — |  |
| 48 | 56 | White Fang | `e56a` | Melee 1 | Melee | 90 | Knockdown (3) | — |  |
| 48 | 56 | 〃 | `e56a` | Melee 2 | Melee | 84 | Knockback (2) | Curse |  |
| 49 | 57 | Moon Bug | `e57a` | Melee 1 | Melee | 70 | Knockback (2) | — |  |
| 49 | 57 | 〃 | `e57a` | Melee 2 | Melee | 70 | Knockdown (3) | — |  |
| 49 | 57 | 〃 | `e57a` | Shot | Projectile | 70 | Knockdown (3) | — |  |
| 50 | 58 | Phantom | `e58a` | Melee 1 | Melee | 60 | Knockback (2) | Poison |  |
| 50 | 58 | 〃 | `e58a` | Melee 2 | Melee | 54 | Knockback (2) | — |  |
| 51 | 59 | Dragon | `e59a` | Melee 1 | Melee | 45 | Knockdown (3) | — |  |
| 51 | 59 | 〃 | `e59a` | Melee 2 | Melee | 45 | Knockdown (3) | — |  |
| 51 | 59 | 〃 | `e59a` | Shot | Projectile | 50 var | Knockdown (3) | — |  |
| 52 | 60 | Cave Bat | `e60a` | Melee 1 | Melee | 17 | Knockback (2) | — |  |
| 52 | 60 | 〃 | `e60a` | Melee 2 | Melee | 16 | Knockback (2) | Poison |  |
| 53 | 61 | Evil Bat | `e61a` | Melee 1 | Melee | 86 | Knockback (2) | — |  |
| 53 | 61 | 〃 | `e61a` | Melee 2 | Melee | 85 | Knockback (2) | Poison |  |
| 54 | 62 | Hell Pockle | `e62a` | Melee 1 | Melee | 64 | Knockback (2) | Steal |  |
| 54 | 62 | 〃 | `e62a` | Melee 2 | Melee | 64 | Knockback (2) | — |  |
| 55 | 63 | Rash Dasher | `e63a` | Melee | Melee | 102 | Knockdown (3) | — |  |
| 56 | 64 | Steel Giant | `e64a` | Melee 1 | Melee | 93 | Knockdown (3) | — |  |
| 56 | 64 | 〃 | `e64a` | Melee 2 | Melee | 93 | Knockdown (3) | — |  |
| 56 | 64 | 〃 | `e64a` | Melee 3 | Melee | 98 | Knockdown (3) | — |  |
| 56 | 64 | 〃 | `e64a` | Melee 4 | Melee | 98 | Knockdown (3) | — |  |
| 56 | 64 | 〃 | `e64a` | Melee 5 | Melee | 80 | Knockdown (3) | — |  |
| 56 | 64 | 〃 | `e64a` | Melee 6 | Melee | 70 | Knockdown (3) | — |  |
| 56 | 64 | 〃 | `e64a` | Melee 7 | Melee | 60 | Knockdown (3) | — |  |
| 56 | 64 | 〃 | `e64a` | Shot 1 | Projectile | 64 var | Knockdown (3) | — |  |
| 56 | 64 | 〃 | `e64a` | Shot 2 | Projectile | 64 | Knockdown (3) | — |  |
| 57 | 65 | Blizzard | `e65a` | Melee 1 | Melee | 119 | Knockdown (3) | — |  |
| 57 | 65 | 〃 | `e65a` | Melee 2 | Melee | 119 | Knockdown (3) | — |  |
| 57 | 65 | 〃 | `e65a` | Melee 3 | Melee | 119 | Knockdown (3) | — |  |
| 57 | 65 | 〃 | `e65a` | Melee 4 | Melee | 119 | Knockdown (3) | — |  |
| 57 | 65 | 〃 | `e65a` | Melee 5 | Melee | 105 | Knockdown (3) | — |  |
| 57 | 65 | 〃 | `e65a` | Melee 6 | Melee | 90 | Knockdown (3) | — |  |
| 57 | 65 | 〃 | `e65a` | Melee 7 | Melee | 75 | Knockdown (3) | — |  |
| 57 | 65 | 〃 | `e65a` | Shot 1 | Projectile | 105 var | Knockdown (3) | — |  |
| 57 | 65 | 〃 | `e65a` | Shot 2 | Projectile | 105 | Knockdown (3) | — |  |
| 58 | 66 | Moon Digger | `e66a` | Melee | Melee | 83 | Knockdown (3) | — |  |
| 58 | 66 | 〃 | `e66a` | Shot | Projectile | 72 var | Knockback (2) | Freeze |  |
| 59 | 67 | Dark Flower | `e67a` | Melee 1 | Melee | 90 | Knockdown (3) | — |  |
| 59 | 67 | 〃 | `e67a` | Melee 2 | Melee | 90 | Knockdown (3) | — |  |
| 59 | 67 | 〃 | `e67a` | Melee 3 | Melee | 94 | Knockdown (3) | — |  |
| 59 | 67 | 〃 | `e67a` | Shot | Projectile | 85 var | Knockback (2) | Goo |  |
| 60 | 68 | Cursed Rose | `e68a` | Melee 1 | Melee | 49 | Knockdown (3) | Curse |  |
| 60 | 68 | 〃 | `e68a` | Melee 2 | Melee | 49 | Knockdown (3) | Curse |  |
| 60 | 68 | 〃 | `e68a` | Melee 3 | Melee | 48 | Knockdown (3) | — |  |
| 60 | 68 | 〃 | `e68a` | Shot | Projectile | 46 var | Knockback (2) | Goo |  |
| 61 | 69 | Billy | `e69a` | Melee 1 | Melee | 93 | Knockback (2) | — |  |
| 61 | 69 | 〃 | `e69a` | Melee 2 | Melee | 93 | Knockback (2) | — |  |
| 61 | 69 | 〃 | `e69a` | Shot 1 | Projectile | 93 | Knockdown (3) | — |  |
| 61 | 69 | 〃 | `e69a` | Shot 2 | Projectile | 110 | Knockdown (3) | — |  |
| 62 | 70 | Vulcan | `e70a` | Melee 1 | Melee | 88 | Knockdown (3) | — |  |
| 62 | 70 | 〃 | `e70a` | Melee 2 | Melee | 94 | Knockback (2) | — |  |
| 62 | 70 | 〃 | `e70a` | Melee 3 | Melee | 94 | Knockback (2) | — |  |
| 63 | 71 | Crabby Hermit | `e71a` | Melee 1 | Melee | 83 | Knockback (2) | — |  |
| 63 | 71 | 〃 | `e71a` | Melee 2 | Melee | 83 | Knockback (2) | — |  |
| 63 | 71 | 〃 | `e71a` | Melee 3 | Melee | 80 | Knockback (2) | — |  |
| 63 | 71 | 〃 | `e71a` | Shot | Projectile | 76 | Knockback (2) | Poison |  |
| 64 | 72 | Space Gyon | `e72a` | Melee 1 | Melee | 78 | Knockback (2) | — |  |
| 64 | 72 | 〃 | `e72a` | Melee 2 | Melee | 78 | Knockback (2) | — |  |
| 64 | 72 | 〃 | `e72a` | Shot | Projectile | 75 var | Knockback (2) | Goo |  |
| 65 | 73 | Blue Dragon | `e73a` | Melee 1 | Melee | 90 | Knockdown (3) | — |  |
| 65 | 73 | 〃 | `e73a` | Melee 2 | Melee | 90 | Knockdown (3) | — |  |
| 65 | 73 | 〃 | `e73a` | Shot | Projectile | 90 var | Knockdown (3) | — |  |
| 66 | 74 | Black Dragon | `e74a` | Melee 1 | Melee | 130 | Knockdown (3) | — |  |
| 66 | 74 | 〃 | `e74a` | Melee 2 | Melee | 130 | Knockdown (3) | — |  |
| 66 | 74 | 〃 | `e74a` | Shot | Projectile | 135 var | Knockdown (3) | Freeze |  |
| 67 | 75 | Mask of Prajna | `e75a` | Melee 1 | Melee | 80 | Knockback (2) | Poison |  |
| 67 | 75 | 〃 | `e75a` | Melee 2 | Melee | 78 | Knockback (2) | — |  |
| 67 | 75 | 〃 | `e75a` | Shot | Projectile | 65 | Knockback (2) | Poison |  |
| 68 | 76 | Crescent Baron | `e76a` | Melee 1 | Melee | 98 | Knockdown (3) | — | BST default `mikazuki_ex` (idx21) 70; BST patch verified live |
| 68 | 76 | 〃 | `e76a` | Melee 2 | Melee | 98 | Knockdown (3) | — |  |
| 68 | 76 | 〃 | `e76a` | Melee 3 | Melee | 94 | Knockdown (3) | — |  |
| 68 | 76 | 〃 | `e76a` | Melee 4 | Melee | 94 | Knockdown (3) | — |  |
| 68 | 76 | 〃 | `e76a` | Shot | Projectile | 70 def | Knockdown (3) | — |  |
| 69 | 77 | Rockanoff | `e77a` | Melee 1 | Melee | 35 | Knockdown (3) | — |  |
| 69 | 77 | 〃 | `e77a` | Melee 2 | Melee | 35 | Knockdown (3) | — |  |
| 70 | 78 | King Mimic (Wise Owl Forest) | `e78a` | Melee 1 | Melee | 67 | Knockback (2) | — |  |
| 70 | 78 | 〃 | `e78a` | Melee 2 | Melee | 56 | Knockback (2) | — |  |
| 70 | 78 | 〃 | `e78a` | Melee 3 | Melee | 45 | Knockback (2) | — |  |
| 70 | 78 | 〃 | `e78a` | Melee 4 | Melee | 50 | Knockdown (3) | HalfHP |  |
| 71 | 79 | Mimic (Wise Owl Forest) | `e79a` | Melee 1 | Melee | 47 | Knockback (2) | — |  |
| 71 | 79 | 〃 | `e79a` | Melee 2 | Melee | 47 | Knockback (2) | — |  |
| 72 | 80 | King Mimic (Shipwreck) | `e80a` | Melee 1 | Melee | 84 | Knockback (2) | — |  |
| 72 | 80 | 〃 | `e80a` | Melee 2 | Melee | 78 | Knockback (2) | — |  |
| 72 | 80 | 〃 | `e80a` | Melee 3 | Melee | 56 | Knockback (2) | HalfHP |  |
| 73 | 81 | Mimic (Shipwreck) | `e81a` | Melee 1 | Melee | 59 | Knockback (2) | — |  |
| 73 | 81 | 〃 | `e81a` | Melee 2 | Melee | 59 | Knockback (2) | — |  |
| 74 | 82 | King Mimic (Gallery of Time) | `e82a` | Melee 1 | Melee | 134 | Knockback (2) | — |  |
| 74 | 82 | 〃 | `e82a` | Melee 2 | Melee | 120 | Knockback (2) | — |  |
| 74 | 82 | 〃 | `e82a` | Melee 3 | Melee | 98 | Knockback (2) | HalfHP |  |
| 75 | 83 | Mimic (Gallery of Time) | `e83a` | Melee 1 | Melee | 100 | Knockback (2) | — |  |
| 75 | 83 | 〃 | `e83a` | Melee 2 | Melee | 100 | Knockback (2) | — |  |
| 76 | 84 | Ice Arrow | `korinoya` | Melee | Melee | 69 | Knockback (2) | Freeze! | resolved STB `c13_korinoya` |
| 77 | 85 | Sam | `e86a` | Melee 1 | Melee | 64 | Knockback (2) | — | BST default `i_boll` (idx20) 58 + funcId-229 shot 58; BST patch verified live |
| 77 | 85 | 〃 | `e86a` | Melee 2 | Melee | 64 | Knockback (2) | — |  |
| 77 | 85 | 〃 | `e86a` | Melee 3 | Melee | 80 | Knockback (2) | — |  |
| 77 | 85 | 〃 | `e86a` | Shot 1 | Projectile | 58 def | Knockdown (3) | — |  |
| 77 | 85 | 〃 | `e86a` | Shot 2 | Projectile | 58 | Knockdown (3) | — |  |
| 78 | 112 | Dran | `c12a` | Melee 1 | Melee | 45 | Knockdown (3) | — |  |
| 78 | 112 | 〃 | `c12a` | Melee 2 | Melee | 45 | Knockdown (3) | — |  |
| 78 | 112 | 〃 | `c12a` | Melee 3 | Melee | 45 | Knockdown (3) | — |  |
| 78 | 112 | 〃 | `c12a` | Melee 4 | Melee | 25 | Knockdown (3) | — |  |
| 78 | 112 | 〃 | `c12a` | Melee 5 | Melee | 25 | Knockdown (3) | — |  |
| 78 | 112 | 〃 | `c12a` | Melee 6 | Melee | 25 | Knockdown (3) | — |  |
| 78 | 112 | 〃 | `c12a` | Melee 7 | Melee | 25 | Knockdown (3) | — |  |
| 78 | 112 | 〃 | `c12a` | Melee 8 | Melee | 25 | Knockdown (3) | — |  |
| 78 | 112 | 〃 | `c12a` | Melee 9 | Melee | 25 | Knockdown (3) | — |  |
| 78 | 112 | 〃 | `c12a` | Melee 10 | Melee | 25 | Knockdown (3) | — |  |
| 78 | 112 | 〃 | `c12a` | Melee 11 | Melee | 25 | Knockdown (3) | — |  |
| 78 | 112 | 〃 | `c12a` | Melee 12 | Melee | 25 | Knockdown (3) | — |  |
| 78 | 112 | 〃 | `c12a` | Melee 13 | Melee | 25 | Knockdown (3) | — |  |
| 79 | 114 | Master Utan | `c14a` | Melee 1 | Melee | 62 | Knockdown (3) | — |  |
| 79 | 114 | 〃 | `c14a` | Melee 2 | Melee | 47 | Knockdown (3) | — |  |
| 80 | 113 | Ice Queen | `c13a` | — | — | — | — | — | no own attack — deals damage only via spawned companions |
| 81 | 115 | King's Curse Coffin | `c15a` | — | — | — | — | — | passive/damageable phase of King's Curse — no attack |
| 82 | 100 | King's Curse | `c15b` | Melee 1 | Melee | 91 | Knockdown (3) | Curse | 2nd-format STB bytecode, hand-decoded |
| 82 | 100 | 〃 | `c15b` | Melee 2 | Melee | 91 | Knockdown (3) | Curse |  |
| 82 | 100 | 〃 | `c15b` | Melee 3 | Melee | 71 | Knockdown (3) | Curse |  |
| 83 | 116 | Minotaur Joe | `c16a` | Melee 1 | Melee | 100 | Knockdown (3) | — |  |
| 83 | 116 | 〃 | `c16a` | Melee 2 | Melee | 125 | Knockdown (3) | — |  |
| 83 | 116 | 〃 | `c16a` | Melee 3 | Melee | 100 | Knockdown (3) | — |  |
| 83 | 116 | 〃 | `c16a` | Melee 4 | Melee | 100 | Knockdown (3) | — |  |
| 83 | 116 | 〃 | `c16a` | Melee 5 | Melee | 100 | Knockdown (3) | — |  |
| 84 | 117 | Dark Genie | `c17a` | Melee | Melee | 85 | Knockdown (3) | — |  |
| 85 | 118 | Dark Genie (form 2) | `c17b` | Melee | Melee | 125 | Knockdown (3) | — |  |
| 86 | 119 | Right Hand | `c17c` | Melee | Melee | 125 | Knockdown (3) | — |  |
| 87 | 120 | Left Hand | `c17_` | Melee | Melee | 125 | Knockdown (3) | — | resolved STB `c17c` |
| 88 | 0 | (DG companion c17_) | `c17_` | Melee | Melee | 175 | Knockdown (3) | — | non-attacker effect entity (c17_kaze/hikari/syougeki — no damage script) |
| 89 | 0 | (DG companion c17_) | `c17_` | Melee | Melee | 175 | Knockdown (3) | — | resolved STB `c17_beem` — footprint 21882/Abs 17 match confirmed `c23_beem` (21938/17); funcId 132 = 175 |
| 90 | 0 | (DG companion c17_) | `c17_` | Melee | Melee | 175 | Knockdown (3) | — | resolved STB `c17_beem_s` — footprint 7475/Abs 20 match confirmed `c23_beem_s` (7423/20); funcId 132 = 175 |
| 91 | 121 | Wine Keg | `e85a` | Melee | Melee | 8 | Knockdown (3) | — |  |
| 92 | 0 | Ice Aura | `b3_r` | Melee | Melee | 74 | Knockdown (3) | — | resolved STB `b3_reiki` |
| 93 | 0 | (DG companion c17_) | `c17_` | Melee | Melee | 175 | Knockdown (3) | — | non-attacker effect entity (c17_kaze/hikari/syougeki — no damage script) |
| 94 | 90 | Gol | `e90a` | Melee 1 | Melee | 71 | Knockdown (3) | — |  |
| 94 | 90 | 〃 | `e90a` | Melee 2 | Melee | 71 | Knockdown (3) | — |  |
| 94 | 90 | 〃 | `e90a` | Melee 3 | Melee | 75 | Knockdown (3) | — |  |
| 94 | 90 | 〃 | `e90a` | Melee 4 | Melee | 75 | Knockdown (3) | — |  |
| 94 | 90 | 〃 | `e90a` | Melee 5 | Melee | 62 | Knockdown (3) | — |  |
| 94 | 90 | 〃 | `e90a` | Melee 6 | Melee | 55 | Knockdown (3) | — |  |
| 94 | 90 | 〃 | `e90a` | Melee 7 | Melee | 45 | Knockdown (3) | — |  |
| 94 | 90 | 〃 | `e90a` | Shot | Projectile | 62 var | Knockdown (3) | — |  |
| 95 | 91 | Sil | `e91a` | Melee 1 | Melee | 71 | Knockdown (3) | — |  |
| 95 | 91 | 〃 | `e91a` | Melee 2 | Melee | 71 | Knockdown (3) | — |  |
| 95 | 91 | 〃 | `e91a` | Melee 3 | Melee | 75 | Knockdown (3) | — |  |
| 95 | 91 | 〃 | `e91a` | Melee 4 | Melee | 75 | Knockdown (3) | — |  |
| 95 | 91 | 〃 | `e91a` | Melee 5 | Melee | 62 | Knockdown (3) | — |  |
| 95 | 91 | 〃 | `e91a` | Melee 6 | Melee | 55 | Knockdown (3) | — |  |
| 95 | 91 | 〃 | `e91a` | Melee 7 | Melee | 45 | Knockdown (3) | — |  |
| 95 | 91 | 〃 | `e91a` | Shot | Projectile | 62 var | Knockdown (3) | — |  |
| 96 | 301 | Yammich | `e101` | Melee | Melee | 35 | Knockdown (3) | — |  |
| 97 | 303 | Statue Dog | `e103` | Melee | Melee | 35 | Knockback (2) | — |  |
| 98 | 304 | Opar | `e104` | Melee 1 | Melee | 35 | Knockdown (3) | — |  |
| 98 | 304 | 〃 | `e104` | Melee 2 | Melee | 35 | Knockdown (3) | — |  |
| 98 | 304 | 〃 | `e104` | Shot 1 | Projectile | 35 | Knockback (2) | Goo |  |
| 98 | 304 | 〃 | `e104` | Shot 2 | Projectile | 35 | Knockback (2) | Goo |  |
| 99 | 305 | Haley Holey | `e105` | Melee 1 | Melee | 37 | Knockback (2) | — |  |
| 99 | 305 | 〃 | `e105` | Melee 2 | Melee | 37 | Knockback (2) | — |  |
| 99 | 305 | 〃 | `e105` | Melee 3 | Melee | 37 | Knockback (2) | — |  |
| 100 | 306 | King Prickly | `e106` | Melee 1 | Melee | 50 | Knockback (2) | — |  |
| 100 | 306 | 〃 | `e106` | Melee 2 | Melee | 50 | Knockdown (3) | — |  |
| 100 | 306 | 〃 | `e106` | Melee 3 | Melee | 50 | Knockdown (3) | — |  |
| 101 | 0 | Ice Barrier | `bari` | — | — | — | — | — | resolved STB `c13_baria` — Ice Queen barrier — no attack |
| 102 | 0 | Ice Prison | `kori` | — | — | — | — | — | resolved STB `c13_kori` — Ice Queen prison — no attack |
| 103 | 0 | Ice Meteor | `i_me` | Melee | Melee | 69 | Knockback (2) | — | resolved STB `c13_i_meteo` |
| 104 | 0 | Ice Tornado | `i_ta` | Melee | Melee | 74 | Knockdown (3) | — | resolved STB `c13_i_tatumaki` |
| 105 | 317 | Gacious | `e124` | Melee 1 | Melee | 130 | Knockback (2) | — |  |
| 105 | 317 | 〃 | `e124` | Melee 2 | Melee | 130 | Knockback (2) | — |  |
| 105 | 317 | 〃 | `e124` | Melee 3 | Melee | 130 | Knockdown (3) | — |  |
| 105 | 317 | 〃 | `e124` | Melee 4 | Melee | 130 | Knockdown (3) | — |  |
| 105 | 317 | 〃 | `e124` | Melee 5 | Melee | 130 | Knockdown (3) | — |  |
| 106 | 223 | Dark Genie (Final Form) | `c23a` | Melee 1 | Melee | 85 | Knockdown (3) | — | Dark Genie's final form (endgame battle); distinct from c17a/c17b. Mouth beam = `ex4`/`ex5` `_SET_SHOT` (BST default 130 via `last_gw2`); 5 hand swings ×85 |
| 106 | 223 | 〃 | `c23a` | Melee 2 | Melee | 85 | Knockdown (3) | — |  |
| 106 | 223 | 〃 | `c23a` | Melee 3 | Melee | 85 | Knockdown (3) | — |  |
| 106 | 223 | 〃 | `c23a` | Melee 4 | Melee | 85 | Knockdown (3) | — |  |
| 106 | 223 | 〃 | `c23a` | Melee 5 | Melee | 85 | Knockdown (3) | — |  |
| 106 | 223 | 〃 | `c23a` | Shot 1 | Projectile | 130 def | Knockdown (3) | — |  |
| 106 | 223 | 〃 | `c23a` | Shot 2 | Projectile | 130 def | Knockdown (3) | — |  |
| 106 | 223 | 〃 | `c23a` | Shot 3 | Projectile | 130 def | Knockdown (3) | — |  |
| 106 | 223 | 〃 | `c23a` | Shot 4 | Projectile | 130 def | Knockdown (3) | — |  |
| 107 | 0 | DG Final summon (last_mc) | `last_mc` | — | — | — | — | — | Dark Genie Final effect entity; motions 発射/召喚/待ち (fire/summon/wait); no own damage |
| 108 | 0 | DG Final ground wave (last_gw1) | `last_gw1` | — | — | — | — | — | Dark Genie Final effect entity; motion グランドウェイブ (ground wave); no own damage |
| 109 | 0 | DG Final beam | `c23_beem` | Melee | Melee | 175 | Knockdown (3) | — | Dark Genie Final beam (= the `c17_beem` Dark Genie beam, 175); motions 発射/ループ/消滅 (fire/loop/vanish) |
| 110 | 0 | DG Final beam (small) | `c23_beem_s` | Melee | Melee | 175 | Knockdown (3) | — | Dark Genie Final beam variant; motions 発動/ループ/消滅 (activate/loop/vanish) |
| 111 | 311 | Gemron (Fire) | `e111` | Melee | Melee | 100 | Knockback (2) | — | melee swing shares the cast animation (funcId 132) |
| 111 | 311 | 〃 | `e111` | Shot | Projectile | 100 | Knockdown (3) | — |  |
| 112 | 308 | Nikapous | `e108` | Melee 1 | Melee | 150 | Knockback (2) | — |  |
| 112 | 308 | 〃 | `e108` | Melee 2 | Melee | 150 | Knockback (2) | — |  |
| 112 | 308 | 〃 | `e108` | Melee 3 | Melee | 150 | Knockdown (3) | — |  |
| 112 | 308 | 〃 | `e108` | Melee 4 | Melee | 150 | Knockdown (3) | — |  |
| 112 | 308 | 〃 | `e108` | Shot | Projectile | 150 | Knockdown (3) | — |  |
| 113 | 56 | White Fang (Enhanced) | `e125` | Melee 1 | Melee | 122 | Knockdown (3) | — |  |
| 113 | 56 | 〃 | `e125` | Melee 2 | Melee | 122 | Knockback (2) | Curse |  |
| 114 | 40 | Arthur (Enhanced) | `e126` | Melee 1 | Melee | 130 | Knockdown (3) | — |  |
| 114 | 40 | 〃 | `e126` | Melee 2 | Melee | 130 | Knockdown (3) | — |  |
| 115 | 91 | Sil (Enhanced) | `e127` | Melee 1 | Melee | 111 | Knockdown (3) | — |  |
| 115 | 91 | 〃 | `e127` | Melee 2 | Melee | 111 | Knockdown (3) | — |  |
| 115 | 91 | 〃 | `e127` | Melee 3 | Melee | 115 | Knockdown (3) | — |  |
| 115 | 91 | 〃 | `e127` | Melee 4 | Melee | 115 | Knockdown (3) | — |  |
| 115 | 91 | 〃 | `e127` | Melee 5 | Melee | 102 | Knockdown (3) | — |  |
| 115 | 91 | 〃 | `e127` | Melee 6 | Melee | 95 | Knockdown (3) | — |  |
| 115 | 91 | 〃 | `e127` | Melee 7 | Melee | 85 | Knockdown (3) | — |  |
| 115 | 91 | 〃 | `e127` | Shot | Projectile | 102 var | Knockdown (3) | — |  |
| 116 | 10 | Halloween (Enhanced) | `e128` | Melee 1 | Melee | 100 | Knockback (2) | — |  |
| 116 | 10 | 〃 | `e128` | Melee 2 | Melee | 100 | Knockback (2) | — |  |
| 116 | 10 | 〃 | `e128` | Shot | Projectile | 100 | Knockback (2) | — |  |
| 117 | 1 | Master Jacket (Enhanced) | `e129` | Melee 1 | Melee | 110 | Knockback (2) | — |  |
| 117 | 1 | 〃 | `e129` | Melee 2 | Melee | 110 | Knockback (2) | — |  |
| 118 | 70 | Vulcan (Enhanced) | `e130` | Melee 1 | Melee | 114 | Knockdown (3) | — |  |
| 118 | 70 | 〃 | `e130` | Melee 2 | Melee | 114 | Knockback (2) | — |  |
| 118 | 70 | 〃 | `e130` | Melee 3 | Melee | 114 | Knockback (2) | — |  |
| 119 | 50 | Mummy (Enhanced) | `e131` | Melee 1 | Melee | 98 | Knockback (2) | Curse |  |
| 119 | 50 | 〃 | `e131` | Melee 2 | Melee | 98 | Knockback (2) | Curse |  |
| 120 | 46 | Diamond (Enhanced) | `e132` | Melee 1 | Melee | 123 | Knockdown (3) | — |  |
| 120 | 46 | 〃 | `e132` | Melee 2 | Melee | 123 | Knockdown (3) | — |  |
| 121 | 312 | Gemron (Ice) | `e112` | Melee | Melee | 120 | Knockback (2) | — | melee swing shares the cast animation (funcId 132) |
| 121 | 312 | 〃 | `e112` | Shot | Projectile | 120 | Knockdown (3) | — |  |
| 122 | 319 | Horn Head | `e119` | Melee 1 | Melee | 130 | Knockback (2) | — |  |
| 122 | 319 | 〃 | `e119` | Melee 2 | Melee | 130 | Knockback (2) | — |  |
| 122 | 319 | 〃 | `e119` | Melee 3 | Melee | 130 | Knockback (2) | — |  |
| 123 | 26 | Auntie Medu (Enhanced) | `e133` | Melee | Melee | 122 | Knockback (2) | — |  |
| 123 | 26 | 〃 | `e133` | Shot | Projectile | 122 | Knockback (2) | Freeze |  |
| 124 | 77 | Rockanoff (Enhanced) | `e134` | Melee 1 | Melee | 130 | Knockdown (3) | — |  |
| 124 | 77 | 〃 | `e134` | Melee 2 | Melee | 130 | Knockdown (3) | — |  |
| 125 | 301 | Yammich (Enhanced) | `e135` | Melee | Melee | 110 | Knockdown (3) | — |  |
| 126 | 21 | Witch Hellza (Enhanced) | `e136` | Melee | Melee | 100 | Knockback (2) | — |  |
| 126 | 21 | 〃 | `e136` | Shot | Projectile | 100 var | Knockdown (3) | — |  |
| 127 | 64 | Steel Giant (Enhanced) | `e137` | Melee 1 | Melee | 163 | Knockdown (3) | — |  |
| 127 | 64 | 〃 | `e137` | Melee 2 | Melee | 163 | Knockdown (3) | — |  |
| 127 | 64 | 〃 | `e137` | Melee 3 | Melee | 178 | Knockdown (3) | — |  |
| 127 | 64 | 〃 | `e137` | Melee 4 | Melee | 178 | Knockdown (3) | — |  |
| 127 | 64 | 〃 | `e137` | Melee 5 | Melee | 163 | Knockdown (3) | — |  |
| 127 | 64 | 〃 | `e137` | Melee 6 | Melee | 162 | Knockdown (3) | — |  |
| 127 | 64 | 〃 | `e137` | Melee 7 | Melee | 161 | Knockdown (3) | — |  |
| 127 | 64 | 〃 | `e137` | Shot 1 | Projectile | 164 var | Knockdown (3) | — |  |
| 127 | 64 | 〃 | `e137` | Shot 2 | Projectile | 164 | Knockdown (3) | — |  |
| 128 | 45 | Club (Enhanced) | `e138` | Melee | Melee | 114 | Knockback (2) | — |  |
| 129 | 28 | Corcea (Enhanced) | `e139` | Melee 1 | Melee | 110 | Knockback (2) | — |  |
| 129 | 28 | 〃 | `e139` | Melee 2 | Melee | 110 | Knockback (2) | — |  |
| 129 | 28 | 〃 | `e139` | Melee 3 | Melee | 110 | Knockback (2) | — |  |
| 129 | 28 | 〃 | `e139` | Melee 4 | Melee | 110 | Knockback (2) | — |  |
| 130 | 309 | Mimic (Demon Shaft) | `e109` | Melee | Melee | 130 | Knockback (2) | — |  |
| 131 | 310 | King Mimic (Demon Shaft) | `e110` | Melee 1 | Melee | 170 | Knockback (2) | — |  |
| 131 | 310 | 〃 | `e110` | Melee 2 | Melee | 170 | Knockback (2) | — |  |
| 131 | 310 | 〃 | `e110` | Melee 3 | Melee | 170 | Knockback (2) | HalfHP |  |
| 132 | 313 | Gemron (Thunder) | `e113` | Melee | Melee | 130 | Knockback (2) | — | melee swing shares the cast animation (funcId 132) |
| 132 | 313 | 〃 | `e113` | Shot | Projectile | 130 | Knockdown (3) | — |  |
| 133 | 316 | Bishop Q | `e116` | Melee 1 | Melee | 130 | Knockdown (3) | — | two shots: funcId-133 (130) + funcId-229 (130) |
| 133 | 316 | 〃 | `e116` | Melee 2 | Melee | 130 | Knockdown (3) | — |  |
| 133 | 316 | 〃 | `e116` | Shot 1 | Projectile | 130 | Knockback (2) | Freeze |  |
| 133 | 316 | 〃 | `e116` | Shot 2 | Projectile | 130 | Knockdown (3) | — |  |
| 134 | 60 | Cave Bat (Enhanced) | `e140` | Melee 1 | Melee | 95 | Knockback (2) | — |  |
| 134 | 60 | 〃 | `e140` | Melee 2 | Melee | 95 | Knockback (2) | Poison |  |
| 135 | 90 | Gol (Enhanced) | `e141` | Melee 1 | Melee | 131 | Knockdown (3) | — |  |
| 135 | 90 | 〃 | `e141` | Melee 2 | Melee | 131 | Knockdown (3) | — |  |
| 135 | 90 | 〃 | `e141` | Melee 3 | Melee | 135 | Knockdown (3) | — |  |
| 135 | 90 | 〃 | `e141` | Melee 4 | Melee | 135 | Knockdown (3) | — |  |
| 135 | 90 | 〃 | `e141` | Melee 5 | Melee | 132 | Knockdown (3) | — |  |
| 135 | 90 | 〃 | `e141` | Melee 6 | Melee | 125 | Knockdown (3) | — |  |
| 135 | 90 | 〃 | `e141` | Melee 7 | Melee | 125 | Knockdown (3) | — |  |
| 135 | 90 | 〃 | `e141` | Shot | Projectile | 132 var | Knockdown (3) | — |  |
| 136 | 75 | Mask of Prajna (Enhanced) | `e142` | Melee 1 | Melee | 140 | Knockback (2) | Poison |  |
| 136 | 75 | 〃 | `e142` | Melee 2 | Melee | 138 | Knockback (2) | — |  |
| 136 | 75 | 〃 | `e142` | Shot | Projectile | 146 | Knockback (2) | Poison |  |
| 137 | 24 | Gyon (Enhanced) | `e143` | Melee 1 | Melee | 100 | Knockback (2) | — |  |
| 137 | 24 | 〃 | `e143` | Melee 2 | Melee | 100 | Knockback (2) | — |  |
| 137 | 24 | 〃 | `e143` | Shot | Projectile | 100 var | Knockback (2) | Goo |  |
| 138 | 47 | Spade (Enhanced) | `e144` | Melee 1 | Melee | 110 | Knockback (2) | — |  |
| 138 | 47 | 〃 | `e144` | Melee 2 | Melee | 110 | Knockdown (3) | — |  |
| 139 | 63 | Rash Dasher (Enhanced) | `e145` | Melee | Melee | 102 | Knockdown (3) | — |  |
| 140 | 27 | Captain (Enhanced) | `e146` | Melee 1 | Melee | 99 | Knockback (2) | — |  |
| 140 | 27 | 〃 | `e146` | Melee 2 | Melee | 99 | Knockback (2) | — |  |
| 140 | 27 | 〃 | `e146` | Melee 3 | Melee | 99 | Knockback (2) | — |  |
| 140 | 27 | 〃 | `e146` | Melee 4 | Melee | 99 | Knockback (2) | — |  |
| 141 | 309 | Mimic (Demon Shaft) (Enhanced) | `e109` | Melee | Melee | 130 | Knockback (2) | — |  |
| 142 | 310 | King Mimic (Demon Shaft) (Enhanced) | `e110` | Melee 1 | Melee | 170 | Knockback (2) | — |  |
| 142 | 310 | 〃 | `e110` | Melee 2 | Melee | 170 | Knockback (2) | — |  |
| 142 | 310 | 〃 | `e110` | Melee 3 | Melee | 170 | Knockback (2) | HalfHP |  |
| 143 | 314 | Gemron (Wind) | `e114` | Melee | Melee | 140 | Knockback (2) | — | melee swing shares the cast animation (funcId 132) |
| 143 | 314 | 〃 | `e114` | Shot | Projectile | 140 | Knockdown (3) | — |  |
| 144 | 318 | Silver Gear | `e118` | Shot | Projectile | 110 | Knockback (2) | — |  |
| 145 | 43 | Alexander (Enhanced) | `e149` | Melee | Melee | 122 | Knockback (2) | Poison |  |
| 145 | 43 | 〃 | `e149` | Shot | Projectile | 122 | Knockdown (3) | — |  |
| 146 | 44 | Heart (Enhanced) | `e150` | Melee | Melee | 130 | Knockback (2) | — | funcId-229 shot (130); 133 `magic_bin` is a 0-dmg bind |
| 146 | 44 | 〃 | `e150` | Shot 1 | Projectile | 130 | Knockback (2) | Stamina |  |
| 146 | 44 | 〃 | `e150` | Shot 2 | Projectile | 130 | Knockdown (3) | — |  |
| 147 | 49 | Bomber Head (Enhanced) | `e151` | Melee 1 | Melee | 160 | Knockdown (3) | — |  |
| 147 | 49 | 〃 | `e151` | Melee 2 | Melee | 160 | Knockdown (3) | — |  |
| 147 | 49 | 〃 | `e151` | Shot | Projectile | 160 | Knockdown (3) | — |  |
| 148 | 71 | Crabby Hermit (Enhanced) | `e152` | Melee 1 | Melee | 130 | Knockback (2) | — |  |
| 148 | 71 | 〃 | `e152` | Melee 2 | Melee | 130 | Knockback (2) | — |  |
| 148 | 71 | 〃 | `e152` | Melee 3 | Melee | 130 | Knockback (2) | — |  |
| 148 | 71 | 〃 | `e152` | Shot | Projectile | 130 | Knockback (2) | Poison |  |
| 149 | 68 | Cursed Rose (Enhanced) | `e153` | Melee 1 | Melee | 140 | Knockdown (3) | Curse |  |
| 149 | 68 | 〃 | `e153` | Melee 2 | Melee | 140 | Knockdown (3) | Curse |  |
| 149 | 68 | 〃 | `e153` | Melee 3 | Melee | 140 | Knockdown (3) | — |  |
| 149 | 68 | 〃 | `e153` | Shot | Projectile | 140 var | Knockback (2) | Goo |  |
| 150 | 25 | Pirate's Chariot (Enhanced) | `e154` | Shot | Projectile | 114 | Knockdown (3) | — |  |
| 151 | 72 | Space Gyon (Enhanced) | `e155` | Melee 1 | Melee | 160 | Knockback (2) | — |  |
| 151 | 72 | 〃 | `e155` | Melee 2 | Melee | 160 | Knockback (2) | — |  |
| 151 | 72 | 〃 | `e155` | Shot | Projectile | 160 var | Knockback (2) | Goo |  |
| 152 | 309 | Mimic (Demon Shaft) (Enhanced x2) | `e109` | Melee | Melee | 130 | Knockback (2) | — |  |
| 153 | 310 | King Mimic (Demon Shaft) (Enhanced x2) | `e110` | Melee 1 | Melee | 170 | Knockback (2) | — |  |
| 153 | 310 | 〃 | `e110` | Melee 2 | Melee | 170 | Knockback (2) | — |  |
| 153 | 310 | 〃 | `e110` | Melee 3 | Melee | 170 | Knockback (2) | HalfHP |  |
| 154 | 315 | Gemron (Holy) | `e115` | Melee | Melee | 150 | Knockback (2) | — | melee swing shares the cast animation (funcId 132) |
| 154 | 315 | 〃 | `e115` | Shot | Projectile | 150 | Knockdown (3) | — |  |
| 155 | 317 | Gacious (Enhanced) | `e117` | Melee 1 | Melee | 180 | Knockback (2) | — |  |
| 155 | 317 | 〃 | `e117` | Melee 2 | Melee | 180 | Knockback (2) | — |  |
| 155 | 317 | 〃 | `e117` | Melee 3 | Melee | 180 | Knockdown (3) | — |  |
| 155 | 317 | 〃 | `e117` | Melee 4 | Melee | 180 | Knockdown (3) | — |  |
| 155 | 317 | 〃 | `e117` | Melee 5 | Melee | 180 | Knockdown (3) | — |  |
| 156 | 61 | Evil Bat (Enhanced) | `e158` | Melee 1 | Melee | 122 | Knockback (2) | — |  |
| 156 | 61 | 〃 | `e158` | Melee 2 | Melee | 122 | Knockback (2) | Poison |  |
| 157 | 76 | Crescent Baron (Enhanced) | `e159` | Melee 1 | Melee | 150 | Knockdown (3) | — |  |
| 157 | 76 | 〃 | `e159` | Melee 2 | Melee | 150 | Knockdown (3) | — |  |
| 157 | 76 | 〃 | `e159` | Melee 3 | Melee | 150 | Knockdown (3) | — |  |
| 157 | 76 | 〃 | `e159` | Melee 4 | Melee | 150 | Knockdown (3) | — |  |
| 157 | 76 | 〃 | `e159` | Shot | Projectile | 150 | Knockdown (3) | — |  |
| 158 | 303 | Statue Dog (Enhanced) | `e160` | Melee | Melee | 110 | Knockback (2) | — |  |
| 159 | 48 | Joker (Enhanced) | `e161` | Melee | Melee | 170 | Knockback (2) | — |  |
| 160 | 51 | Lich (Enhanced) | `e162` | Melee 1 | Melee | 110 | Knockback (2) | — |  |
| 160 | 51 | 〃 | `e162` | Melee 2 | Melee | 110 | Knockback (2) | — |  |
| 160 | 51 | 〃 | `e162` | Shot | Projectile | 110 var | Knockback (2) | Freeze |  |
| 161 | 33 | Titan (Enhanced) | `e163` | Melee 1 | Melee | 155 | Knockdown (3) | — |  |
| 161 | 33 | 〃 | `e163` | Melee 2 | Melee | 155 | Knockdown (3) | — |  |
| 161 | 33 | 〃 | `e163` | Melee 3 | Melee | 160 | Knockdown (3) | — |  |
| 161 | 33 | 〃 | `e163` | Melee 4 | Melee | 160 | Knockdown (3) | — |  |
| 161 | 33 | 〃 | `e163` | Melee 5 | Melee | 150 | Knockdown (3) | — |  |
| 161 | 33 | 〃 | `e163` | Melee 6 | Melee | 145 | Knockdown (3) | — |  |
| 161 | 33 | 〃 | `e163` | Melee 7 | Melee | 140 | Knockdown (3) | — |  |
| 161 | 33 | 〃 | `e163` | Shot 1 | Projectile | 160 var | Knockdown (3) | — |  |
| 161 | 33 | 〃 | `e163` | Shot 2 | Projectile | 160 | Knockdown (3) | — |  |
| 162 | 55 | Living Armor (Enhanced) | `e164` | Melee 1 | Melee | 150 | Knockdown (3) | — |  |
| 162 | 55 | 〃 | `e164` | Melee 2 | Melee | 150 | Knockdown (3) | — |  |
| 162 | 55 | 〃 | `e164` | Melee 3 | Melee | 150 | Knockdown (3) | — |  |
| 162 | 55 | 〃 | `e164` | Melee 4 | Melee | 150 | Knockdown (3) | — |  |
| 163 | 309 | Mimic (Demon Shaft) (Enhanced x3) | `e109` | Melee | Melee | 130 | Knockback (2) | — |  |
| 164 | 310 | King Mimic (Demon Shaft) (Enhanced x3) | `e110` | Melee 1 | Melee | 170 | Knockback (2) | — |  |
| 164 | 310 | 〃 | `e110` | Melee 2 | Melee | 170 | Knockback (2) | — |  |
| 164 | 310 | 〃 | `e110` | Melee 3 | Melee | 170 | Knockback (2) | HalfHP |  |
| 165 | 221 | Black Knight | `c21a` | Melee 1 | Melee | 170 | Knockdown (3) | — | boss-AI specials (BST behaviors via species `+0x68/+0x6A`): `engetu` crescent slash 150, `terepo` teleport (movement, no dmg); no `_SET_SHOT`. Old `26,6,8` was a scanner misread |
| 165 | 221 | 〃 | `c21a` | Melee 2 | Melee | 170 | Knockdown (3) | — |  |
| 166 | 221 | Black Knight Mount | `c22a` | Melee | Melee | 170 | Knockdown (3) | — | `g_center` `_SET_SHOT` (BST default → 170, = the `dash` charge); `kamai` stance (movement, no dmg). Old `4,15` was a misread |
| 166 | 221 | 〃 | `c22a` | Shot | Projectile | 170 def | Knockdown (3) | — |  |
