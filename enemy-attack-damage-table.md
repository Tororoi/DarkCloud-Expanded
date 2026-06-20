# Enemy attack damage table

Per-species attack damage, decoded from each enemy's behaviour script `dun/monstor/<code>.stb` in `data.dat`.

## How damage works

Hit damage = **`baseDamage − playerDefense`** (clamped > 0), computed in `BtCheckDamageProc` (dun overlay
`0x01DBAFD0`) and applied via `AddNowLife`. `baseDamage` comes from the **STB script**, not the species-table
`AttackPower` (which is identical across enemies that hit very differently). The script sets it two ways:

- **Melee** — funcId 132 sets the swing's base damage (`arg0`); funcId 131 (`_SET_DMG_PARA`) sets only its hitbox
  geometry. *Melee dmg* column.
- **Projectile** — `_SET_SHOT` (funcId 133) / `_SET_SHOT2` (funcId 229); handlers `0x1E4120` / `0x1E4310`.
  Signature `_SET_SHOT(modelName, x, y, z, [damage])`; the optional 5th arg is the damage (read only when
  present — vmcode `op21 argc==6`). The shot model is a named frame in the enemy's own model data; there is no
  separate projectile table. Decoded from the STB VM (`exe__10CRunScript` `0x23E080`; 12-byte vmcode `{op, A, B}`
  — op3 push-literal, op1 push-variable, op21 ext-call), the *Projectile dmg* column uses three forms:

| form | how the 5th arg is supplied | meaning |
|---|---|---|
| `N` | op3 literal at the call site | fixed per-species damage |
| `N var` | op1 `scope=1` local → the literal pushed at the subroutine's `call_func` site (Ghost/Lich forward it one extra level) | **distance-scaled**: `N` is the point-blank max; the engine scales it down with player distance |
| `N def` | no 5th arg; engine uses the shot effect's default at `BT_SHOT_EFFECT+0x3C` (a `BehaviorScriptTable` entry, `0x27EB90`) selected by the species record's primary shot index `+0x68` via pointer array `0x27FA70` | fixed per-effect default |

Enemies that fire two shots list both (e.g. Bishop Q `130, 130`, Golem `64 var, 34 def`).

**No damage** — `_SET_BODY_COL` / `_SET_MOV_COL` are collision geometry and `_SET_BODY_COL_PARA` sets
damage-*taken* multipliers, so none of them hurt the player; an enemy with no `_SET_DMG_PARA` and no `_SET_SHOT`
(`—` / `—` rows) is a non-attacker by design — the Ice Queen and her Ice Barrier / Ice Prison, King's Curse
Coffin, and the Dark Genie wind/light/shock effect entities.

Rows are in **TableIndex** order (`—` = mechanism not used). Truncated `ModelCode`s for the Ice Queen / Dark Genie
effect entities (`c17_`, `kori`, `i_me`, …) are resolved to their real `.stb` in *Notes*. The Ice Queen deals
damage only through spawned companions (Ice Arrow 69, Ice Meteor 69, Ice Tornado 74, Ice Aura 74); the Dark Genie
through its hands (swipe 125, beam `c17_beem` 175).

| TableIndex | Id | Name | Model | Melee dmg | Projectile dmg | Notes |
|---:|---:|---|---|---|---|---|
| 0 | 1 | Master Jacket | `e01a` | 35, 30 | — |  |
| 1 | 3 | Skeleton Soldier | `e03a` | 20, 21 | — |  |
| 2 | 5 | Statue | `e05a` | 26, 25, 26, 25 | — |  |
| 3 | 6 | Dasher | `e06a` | 22 | — |  |
| 4 | 7 | Werewolf | `e07a` | 68, 62 | — |  |
| 5 | 8 | FliFli | `e08a` | 37 | 35 |  |
| 6 | 9 | Hornet | `e09a` | 42, 40 | — |  |
| 7 | 10 | Halloween | `e10a` | 57, 57 | 60 |  |
| 8 | 11 | Cannibal Plant | `e11a` | 28, 28, 36 | 30 var |  |
| 9 | 12 | Earth Digger | `e12a` | 52 | 37 var |  |
| 10 | 14 | Sunday | `e14a` | 36, 26 | — |  |
| 11 | 15 | Monday | `e15a` | 32, 12 | — |  |
| 12 | 16 | Tuesday | `e16a` | 31 | 31 |  |
| 13 | 17 | Wednesday | `e17a` | 30, 28 | — |  |
| 14 | 18 | Thursday | `e18a` | 29 | 30 def | BST default `ringo_ex` (idx4) |
| 15 | 19 | Friday | `e19a` | 29, 29 | — |  |
| 16 | 20 | Saturday | `e20a` | 29, 29, 25 | — |  |
| 17 | 21 | Witch Hellza | `e21a` | 73 | 73 var |  |
| 18 | 22 | Witch Illza | `e22a` | 47 | 47 var |  |
| 19 | 23 | Gunny | `e23a` | 44, 44 | 26 |  |
| 20 | 24 | Gyon | `e24a` | 59, 59 | 59 var |  |
| 21 | 25 | Pirate's Chariot | `e25a` | — | 69 |  |
| 22 | 26 | Auntie Medu | `e26a` | 74 | 60 |  |
| 23 | 27 | Captain | `e27a` | 74, 74, 72, 72 | — |  |
| 24 | 28 | Corcea | `e28a` | 43, 43, 42, 42 | — |  |
| 25 | 30 | Golem | `e30a` | 71, 71, 75, 75, 62, 55, 45 | 64 var, 34 def | shot1 `g_wave1` var 64; shot2 BST default 34 |
| 26 | 31 | Mr. Blare | `e31a` | 81, 81 | 80, 90 |  |
| 27 | 32 | Dune | `e32a` | 85, 85, 85 | — |  |
| 28 | 33 | Titan | `e33a` | 105, 105, 105, 105, 90, 75, 60 | 90 var, 90 |  |
| 29 | 34 | King Mimic (Divine Beast Cave) | `e34a` | 35, 30, 35 | — |  |
| 30 | 35 | Mimic (Divine Beast Cave) | `e35a` | 33, 33 | — |  |
| 31 | 36 | King Mimic (Sun & Moon Temple) | `e36a` | 101, 102, 45 | — |  |
| 32 | 37 | Mimic (Sun & Moon Temple) | `e37a` | 71, 71 | — |  |
| 33 | 38 | King Mimic (Moon Sea) | `e38a` | 118, 96, 90 | — |  |
| 34 | 39 | Mimic (Moon Sea) | `e39a` | 83, 83 | — |  |
| 35 | 40 | Arthur | `e40a` | 116, 116 | — |  |
| 36 | 42 | Ghost | `e42a` | 20, 20 | 21 var |  |
| 37 | 43 | Alexander | `e43a` | 120 | 124 |  |
| 38 | 44 | Heart | `e44a` | 107 | 107 | funcId-229 shot (107); the 133 `magic_bin` is a separate 0-dmg bind |
| 39 | 45 | Club | `e45a` | 104 | — |  |
| 40 | 46 | Diamond | `e46a` | 110, 110 | — |  |
| 41 | 47 | Spade | `e47a` | 113, 113 | — |  |
| 42 | 48 | Joker | `e48a` | 115 | — |  |
| 43 | 49 | Bomber Head | `e49a` | 61, 61 | 64 |  |
| 44 | 50 | Mummy | `e50a` | 54, 54, 54 | — |  |
| 45 | 51 | Lich | `e51a` | 114, 114 | 100 var |  |
| 46 | 52 | Curse Dancer | `e52a` | 90, 90, 90, 90 | — |  |
| 47 | 55 | Living Armor | `e55a` | 95, 95, 95, 95 | — |  |
| 48 | 56 | White Fang | `e56a` | 90, 84 | — |  |
| 49 | 57 | Moon Bug | `e57a` | 70, 70 | 70 |  |
| 50 | 58 | Phantom | `e58a` | 60, 54 | — |  |
| 51 | 59 | Dragon | `e59a` | 45, 45 | 50 var |  |
| 52 | 60 | Cave Bat | `e60a` | 17, 16 | — |  |
| 53 | 61 | Evil Bat | `e61a` | 86, 85 | — |  |
| 54 | 62 | Hell Pockle | `e62a` | 64, 64 | — |  |
| 55 | 63 | Rash Dasher | `e63a` | 102 | — |  |
| 56 | 64 | Steel Giant | `e64a` | 93, 93, 98, 98, 80, 70, 60 | 64 var, 64 |  |
| 57 | 65 | Blizzard | `e65a` | 119, 119, 119, 119, 105, 90, 75 | 105 var, 105 |  |
| 58 | 66 | Moon Digger | `e66a` | 83 | 72 var |  |
| 59 | 67 | Dark Flower | `e67a` | 90, 90, 94 | 85 var |  |
| 60 | 68 | Cursed Rose | `e68a` | 49, 49, 48 | 46 var |  |
| 61 | 69 | Billy | `e69a` | 93, 93 | 93, 110 |  |
| 62 | 70 | Vulcan | `e70a` | 88, 94, 94 | — |  |
| 63 | 71 | Crabby Hermit | `e71a` | 83, 83, 80 | 76 |  |
| 64 | 72 | Space Gyon | `e72a` | 78, 78 | 75 var |  |
| 65 | 73 | Blue Dragon | `e73a` | 90, 90 | 90 var |  |
| 66 | 74 | Black Dragon | `e74a` | 130, 130 | 135 var |  |
| 67 | 75 | Mask of Prajna | `e75a` | 80, 78 | 65 |  |
| 68 | 76 | Crescent Baron | `e76a` | 98, 98, 94, 94 | 70 def | BST default `mikazuki_ex` (idx21) 70; BST patch verified live |
| 69 | 77 | Rockanoff | `e77a` | 35, 35 | — |  |
| 70 | 78 | King Mimic (Wise Owl Forest) | `e78a` | 67, 56, 45, 50 | — |  |
| 71 | 79 | Mimic (Wise Owl Forest) | `e79a` | 47, 47 | — |  |
| 72 | 80 | King Mimic (Shipwreck) | `e80a` | 84, 78, 56 | — |  |
| 73 | 81 | Mimic (Shipwreck) | `e81a` | 59, 59 | — |  |
| 74 | 82 | King Mimic (Gallery of Time) | `e82a` | 134, 120, 98 | — |  |
| 75 | 83 | Mimic (Gallery of Time) | `e83a` | 100, 100 | — |  |
| 76 | 84 | Ice Arrow | `korinoya` | 69 | — | resolved STB `c13_korinoya` |
| 77 | 85 | Sam | `e86a` | 64, 64, 80 | 58 def, 58 | BST default `i_boll` (idx20) 58 + funcId-229 shot 58; BST patch verified live |
| 78 | 112 | Dran | `c12a` | 45, 45, 45, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25 | 17 |  |
| 79 | 114 | Master Utan | `c14a` | 62, 47 | 13 |  |
| 80 | 113 | Ice Queen | `c13a` | — | — | no own attack — deals damage only via spawned companions |
| 81 | 115 | King's Curse Coffin | `c15a` | — | — | passive/damageable phase of King's Curse — no attack |
| 82 | 100 | King's Curse | `c15b` | 91, 91, 71 | — |  |
| 83 | 116 | Minotaur Joe | `c16a` | 100, 125, 100, 100, 100 | — |  |
| 84 | 117 | Dark Genie | `c17a` | 85 | — |  |
| 85 | 118 | Dark Genie (form 2) | `c17b` | 125 | — |  |
| 86 | 119 | Right Hand | `c17c` | 125 | — |  |
| 87 | 120 | Left Hand | `c17_` | 125 | — | resolved STB `c17c` |
| 88 | 0 | (DG companion c17_) | `c17_` | 175 | — | resolved STB `c17_beem` |
| 89 | 0 | (DG companion c17_) | `c17_` | — | — | resolved STB `c17_kaze` — Dark Genie wind effect entity — no attack |
| 90 | 0 | (DG companion c17_) | `c17_` | — | — | resolved STB `c17_hikari` — Dark Genie light effect entity — no attack |
| 91 | 121 | Wine Keg | `e85a` | 8 | — |  |
| 92 | 0 | Ice Aura | `b3_r` | 74 | — | resolved STB `b3_reiki` |
| 93 | 0 | (DG companion c17_) | `c17_` | — | — | resolved STB `c17_syougeki` — Dark Genie shock effect entity — no attack |
| 94 | 90 | Gol | `e90a` | 71, 71, 75, 75, 62, 55, 45 | 62 var |  |
| 95 | 91 | Sil | `e91a` | 71, 71, 75, 75, 62, 55, 45 | 62 var |  |
| 96 | 301 | Yammich | `e101` | 35 | — |  |
| 97 | 303 | Statue Dog | `e103` | 35 | — |  |
| 98 | 304 | Opar | `e104` | 35, 35 | 35, 35 |  |
| 99 | 305 | Haley Holey | `e105` | 37, 37, 37 | — |  |
| 100 | 306 | King Prickly | `e106` | 50, 50, 50 | — |  |
| 101 | 0 | Ice Barrier | `bari` | — | — | resolved STB `c13_baria` — Ice Queen barrier — no attack |
| 102 | 0 | Ice Prison | `kori` | — | — | resolved STB `c13_kori` — Ice Queen prison — no attack |
| 103 | 0 | Ice Meteor | `i_me` | 69 | — | resolved STB `c13_i_meteo` |
| 104 | 0 | Ice Tornado | `i_ta` | 74 | — | resolved STB `c13_i_tatumaki` |
| 105 | 317 | Gacious | `e124` | 130, 130, 130, 130, 130 | — |  |
| 106 | 223 | Dark Genie (Final Form) | `c23a` | 85, 85, 85, 85, 85 | 130 def | Dark Genie's final form (endgame battle); distinct from c17a/c17b. Mouth beam = `ex4`/`ex5` `_SET_SHOT` (BST default 130 via `last_gw2`); 5 hand swings ×85 |
| 107 | 0 | DG Final summon | `last_mc` | — | — | Dark Genie Final effect entity; motions 発射/召喚/待ち (fire/summon/wait); no own damage |
| 108 | 0 | DG Final ground wave | `last_gw1` | — | — | Dark Genie Final effect entity; motion グランドウェイブ (ground wave); no own damage |
| 109 | 0 | DG Final beam | `c23_beem` | 175 | — | Dark Genie Final beam (= the `c17_beem` Dark Genie beam, 175); motions 発射/ループ/消滅 (fire/loop/vanish) |
| 110 | 0 | DG Final beam (small) | `c23_beem_s` | 175 | — | Dark Genie Final beam variant; motions 発動/ループ/消滅 (activate/loop/vanish) |
| 111 | 311 | Gemron (Fire) | `e111` | — | 100 |  |
| 112 | 308 | Nikapous | `e108` | 150, 150, 150, 150 | 150 |  |
| 113 | 56 | White Fang (Enhanced) | `e125` | 122, 122 | — |  |
| 114 | 40 | Arthur (Enhanced) | `e126` | 130, 130 | — |  |
| 115 | 91 | Sil (Enhanced) | `e127` | 111, 111, 115, 115, 102, 95, 85 | 102 var |  |
| 116 | 10 | Halloween (Enhanced) | `e128` | 100, 100 | 100 |  |
| 117 | 1 | Master Jacket (Enhanced) | `e129` | 110, 110 | — |  |
| 118 | 70 | Vulcan (Enhanced) | `e130` | 114, 114, 114 | — |  |
| 119 | 50 | Mummy (Enhanced) | `e131` | 98, 98 | — |  |
| 120 | 46 | Diamond (Enhanced) | `e132` | 123, 123 | — |  |
| 121 | 312 | Gemron (Ice) | `e112` | — | 120 |  |
| 122 | 319 | Horn Head | `e119` | 130, 130, 130 | — |  |
| 123 | 26 | Auntie Medu (Enhanced) | `e133` | 122 | 122 |  |
| 124 | 77 | Rockanoff (Enhanced) | `e134` | 130, 130 | — |  |
| 125 | 301 | Yammich (Enhanced) | `e135` | 110 | — |  |
| 126 | 21 | Witch Hellza (Enhanced) | `e136` | 100 | 100 var |  |
| 127 | 64 | Steel Giant (Enhanced) | `e137` | 163, 163, 178, 178, 163, 162, 161 | 164 var, 164 |  |
| 128 | 45 | Club (Enhanced) | `e138` | 114 | — |  |
| 129 | 28 | Corcea (Enhanced) | `e139` | 110, 110, 110, 110 | — |  |
| 130 | 309 | Mimic (Demon Shaft) | `e109` | 130 | — |  |
| 131 | 310 | King Mimic (Demon Shaft) | `e110` | 170, 170, 170 | — |  |
| 132 | 313 | Gemron (Thunder) | `e113` | — | 130 |  |
| 133 | 316 | Bishop Q | `e116` | 130, 130 | 130, 130 | two shots: funcId-133 (130) + funcId-229 (130) |
| 134 | 60 | Cave Bat (Enhanced) | `e140` | 95, 95 | — |  |
| 135 | 90 | Gol (Enhanced) | `e141` | 131, 131, 135, 135, 132, 125, 125 | 132 var |  |
| 136 | 75 | Mask of Prajna (Enhanced) | `e142` | 140, 138 | 146 |  |
| 137 | 24 | Gyon (Enhanced) | `e143` | 100, 100 | 100 var |  |
| 138 | 47 | Spade (Enhanced) | `e144` | 110, 110 | — |  |
| 139 | 63 | Rash Dasher (Enhanced) | `e145` | 102 | — |  |
| 140 | 27 | Captain (Enhanced) | `e146` | 99, 99, 99, 99 | — |  |
| 141 | 309 | Mimic (Demon Shaft) (Enhanced) | `e109` | 130 | — |  |
| 142 | 310 | King Mimic (Demon Shaft) (Enhanced) | `e110` | 170, 170, 170 | — |  |
| 143 | 314 | Gemron (Wind) | `e114` | — | 140 |  |
| 144 | 318 | Silver Gear | `e118` | — | 110 |  |
| 145 | 43 | Alexnder (Enhanced) | `e149` | 122 | 122 |  |
| 146 | 44 | Heart (Enhanced) | `e150` | 130 | 130 | funcId-229 shot (130); 133 `magic_bin` is a 0-dmg bind |
| 147 | 49 | Bomber Head (Enhanced) | `e151` | 160, 160 | 160 |  |
| 148 | 71 | Crabby Hermit (Enhanced) | `e152` | 130, 130, 130 | 130 |  |
| 149 | 68 | Cursed Rose (Enhanced) | `e153` | 140, 140, 140 | 140 var |  |
| 150 | 25 | Pirate's Chariot (Enhanced) | `e154` | — | 114 |  |
| 151 | 72 | Space Gyon (Enhanced) | `e155` | 160, 160 | 160 var |  |
| 152 | 309 | Mimic (Demon Shaft) (Enhanced x2) | `e109` | 130 | — |  |
| 153 | 310 | King Mimic (Demon Shaft) (Enhanced x2) | `e110` | 170, 170, 170 | — |  |
| 154 | 315 | Gemron (Holy) | `e115` | — | 150 |  |
| 155 | 317 | Gacious (Enhanced) | `e117` | 180, 180, 180, 180, 180 | — |  |
| 156 | 61 | Evil Bat (Enhanced) | `e158` | 122, 122 | — |  |
| 157 | 76 | Crescent Baron (Enhanced) | `e159` | 150, 150, 150, 150 | 150 |  |
| 158 | 303 | Statue Dog (Enhanced) | `e160` | 110 | — |  |
| 159 | 48 | Joker (Enhanced) | `e161` | 170 | — |  |
| 160 | 51 | Lich (Enhanced) | `e162` | 110, 110 | 110 var |  |
| 161 | 33 | Titan (Enhanced) | `e163` | 155, 155, 160, 160, 150, 145, 140 | 160 var, 160 |  |
| 162 | 55 | Living Armor (Enhanced) | `e164` | 150, 150, 150, 150 | — |  |
| 163 | 309 | Mimic (Demon Shaft) (Enhanced x3) | `e109` | 130 | — |  |
| 164 | 310 | King Mimic (Demon Shaft) (Enhanced x3) | `e110` | 170, 170, 170 | — |  |
| 165 | 221 | Black Knight | `c21a` | 170, 170 | 150 | boss-AI specials (BST behaviors via species `+0x68/+0x6A`): `engetu` crescent slash 150, `terepo` teleport (movement, no dmg); no `_SET_SHOT`. Old `26,6,8` was a scanner misread |
| 166 | 221 | Black Knight Mount | `c22a` | 170 | 170 def | `g_center` `_SET_SHOT` (BST default → 170, = the `dash` charge); `kamai` stance (movement, no dmg). Old `4,15` was a misread |
