# Enemy body-collision (damage hitbox) table

Per-species **damage hitbox** — the volume your weapon must reach to hurt the enemy. Decoded from each
enemy's behaviour script `dun/monstor/<code>.stb` in `data.dat` (`_SET_BODY_COL`, STB cmd `0x82` / funcId
`130`). Each call attaches a **collision sphere** to a model bone with a **radius**:

- At spawn `_SET_BODY_COL(bone, radius)` records the sphere; `CMonstorUnit::CheckDmg` (ELF `0x1D9F10`)
  rebuilds it each frame at the bone's live position, radius → `CCollisionData` element `+0x3C`.
- A player→enemy weapon hit registers when `dist(weaponPoint, bonePos) ≤ radius`, via the generic
  point-in-sphere helper `CCollisionData::CheckHitUser` (`0x1B5920`). ⚠ Earlier this was attributed to
  `BtCheckDamageProc` (`0x1DBAFD0`) — that's wrong: `BtCheckDamageProc` is the *reverse* direction
  (enemy→player), testing the **player's** body point (`0x21EA1D30`) against the shared enemy collision
  buffer (`gp-0x6210`). It also calls `CheckHitUser` (the helper is direction-agnostic), but the
  player→enemy weapon-hit caller is a separate, not-yet-pinned function.
- Live address of a sphere's radius: `EnemyAddresses.BodyCollision.RadiusAddr(slot, bodyPart)`
  (`= MainMonstorUnit.Base + 0x55390 + slot*0x510 + bodyPart*4`). Writing it once resizes the hitbox.

Most enemies use a single `root` body sphere; bosses use several on named bones (a few early boss
spheres reference messy/comment strings in the source data and fall back to the root body — shown as
`root`). Sorted by `TableIndex`.

| TableIndex | Id | Name | Model | Hitbox spheres (radius @ bone) |
|---|---|---|---|---|
| 0 | 1 | Master Jacket | `e01a` | 6 |
| 1 | 3 | Skeleton Soldier | `e03a` | 6 |
| 2 | 5 | Statue | `e05a` | 5, 3, 3 |
| 3 | 6 | Dasher | `e06a` | 7, 3, 3 |
| 4 | 7 | Werewolf | `e07a` | 6, 3, 3 |
| 5 | 8 | FliFli | `e08a` | 7.5 |
| 6 | 9 | Hornet | `e09a` | 6 |
| 7 | 10 | Halloween | `e10a` | 5, 3 |
| 8 | 11 | Cannibal Plant | `e11a` | 4, 6 |
| 9 | 12 | Earth Digger | `e12a` | 7 |
| 10 | 14 | Sunday | `e14a` | 5 |
| 11 | 15 | Monday | `e15a` | 5 |
| 12 | 16 | Tuesday | `e16a` | 5 |
| 13 | 17 | Wednesday | `e17a` | 5 |
| 14 | 18 | Thursday | `e18a` | 5 |
| 15 | 19 | Friday | `e19a` | 7 |
| 16 | 20 | Saturday | `e20a` | 5 |
| 17 | 21 | Witch Hellza | `e21a` | 6.5 |
| 18 | 22 | Witch Illza | `e22a` | 6.5, 4 |
| 19 | 23 | Gunny | `e23a` | 8 |
| 20 | 24 | Gyon | `e24a` | 7 |
| 21 | 25 | Pirate's Chariot | `e25a` | 10.2 |
| 22 | 26 | Auntie Medu | `e26a` | 5, 4 |
| 23 | 27 | Captain | `e27a` | 6, 3, 3 |
| 24 | 28 | Corcea | `e28a` | 7, 3, 3 |
| 25 | 30 | Golem | `e30a` | 5, 10, 6, 6 |
| 26 | 31 | Mr. Blare | `e31a` | 8.5, 6.5 |
| 27 | 32 | Dune | `e32a` | 9, 7, 7 |
| 28 | 33 | Titan | `e33a` | 5, 10.6, 6, 6 |
| 29 | 34 | King Mimic (Divine Beast Cave) | `e34a` | 10 |
| 30 | 35 | Mimic (Divine Beast Cave) | `e35a` | 5.5 |
| 31 | 36 | King Mimic (Sun & Moon Temple) | `e36a` | 10 |
| 32 | 37 | Mimic (Sun & Moon Temple) | `e37a` | 5.5 |
| 33 | 38 | King Mimic (Moon Sea) | `e38a` | 10 |
| 34 | 39 | Mimic (Moon Sea) | `e39a` | 5.5 |
| 35 | 40 | Arthur | `e40a` | 7, 3, 3 |
| 36 | 42 | Ghost | `e42a` | 7 |
| 37 | 43 | Alexander | `e43a` | 5 |
| 38 | 44 | Heart | `e44a` | 6 |
| 39 | 45 | Club | `e45a` | 6 |
| 40 | 46 | Diamond | `e46a` | 7 |
| 41 | 47 | Spade | `e47a` | 5 |
| 42 | 48 | Joker | `e48a` | 6 |
| 43 | 49 | Bomber Head | `e49a` | 6 |
| 44 | 50 | Mummy | `e50a` | 8 |
| 45 | 51 | Lich | `e51a` | 6.5 |
| 46 | 52 | Curse Dancer | `e52a` | 4.8, 5.8 |
| 47 | 55 | Living Armor | `e55a` | 7 |
| 48 | 56 | White Fang | `e56a` | 6, 3, 3 |
| 49 | 57 | Moon Bug | `e57a` | 9 |
| 50 | 58 | Phantom | `e58a` | 5 |
| 51 | 59 | Dragon | `e59a` | 12, 8, 5 |
| 52 | 60 | Cave Bat | `e60a` | 6.5 |
| 53 | 61 | Evil Bat | `e61a` | 6.5 |
| 54 | 62 | Hell Pockle | `e62a` | 5 |
| 55 | 63 | Rash Dasher | `e63a` | 7, 3, 3 |
| 56 | 64 | Steel Giant | `e64a` | 5, 10 |
| 57 | 65 | Blizzard | `e65a` | 12 |
| 58 | 66 | Moon Digger | `e66a` | 7 |
| 59 | 67 | Dark Flower | `e67a` | 4, 6 |
| 60 | 68 | Cursed Rose | `e68a` | 4.5, 6 |
| 61 | 69 | Billy | `e69a` | 7.5 |
| 62 | 70 | Vulcan | `e70a` | 9, 7, 7 |
| 63 | 71 | Crabby Hermit | `e71a` | 10 |
| 64 | 72 | Space Gyon | `e72a` | 8 |
| 65 | 73 | Blue Dragon | `e73a` | 12, 8, 5 |
| 66 | 74 | Black Dragon | `e74a` | 12, 8, 5 |
| 67 | 75 | Mask of Prajna | `e75a` | 8 |
| 68 | 76 | Crescent Baron | `e76a` | 6 |
| 69 | 77 | Rockanoff | `e77a` | 12 |
| 70 | 78 | King Mimic (Wise Owl Forest) | `e78a` | 10 |
| 71 | 79 | Mimic (Wise Owl Forest) | `e79a` | 5.5 |
| 72 | 80 | King Mimic (Shipwreck) | `e80a` | 10 |
| 73 | 81 | Mimic (Shipwreck) | `e81a` | 5.5 |
| 74 | 82 | King Mimic (Gallery of Time) | `e82a` | 10 |
| 75 | 83 | Mimic (Gallery of Time) | `e83a` | 5.5 |
| 76 | 84 | Ice Arrow | `korinoya` | *(no STB)* |
| 77 | 85 | Sam | `e86a` | 8.5, 6.5 |
| 78 | 112 | Dran | `c12a` | 20, 8, 8, 8, 8, 7, 7 @`r_arm3`, 7 @`l_arm3`, 7 @`r_arm2`, 20 @`l_arm2`, 10 |
| 79 | 114 | Master Utan | `c14a` | 7, 7, 8.5, 6, 6, 10, 6 |
| 80 | 113 | Ice Queen | `c13a` | 4.5 |
| 81 | 115 | King's Curse Coffin | `c15a` | 20 |
| 82 | 100 | King's Curse | `c15b` | 20, 30 |
| 83 | 116 | Minotaur Joe | `c16a` | 7, 10, 4, 7, 7 |
| 84 | 117 | Dark Genie | `c17a` | 4, 4 |
| 85 | 118 | Dark Genie (form 2) | `c17b` | 10 |
| 86 | 119 | Right Hand | `c17c` | 10 |
| 87 | 120 | Left Hand | `c17_` | *(none — no body collision)* |
| 88 | 0 | (DG companion c17_) | `c17_` | *(none — no body collision)* |
| 89 | 0 | (DG companion c17_) | `c17_` | *(none — no body collision)* |
| 90 | 0 | (DG companion c17_) | `c17_` | *(none — no body collision)* |
| 91 | 121 | Wine Keg | `e85a` | 10 |
| 92 | 0 | Ice Aura | `b3_r` | *(none — no body collision)* |
| 93 | 0 | (DG companion c17_) | `c17_` | *(none — no body collision)* |
| 94 | 90 | Gol | `e90a` | 5, 10, 6, 6 |
| 95 | 91 | Sil | `e91a` | 5, 10, 6, 6 |
| 96 | 301 | Yammich | `e101` | 7 |
| 97 | 303 | Statue Dog | `e103` | 10, 10 |
| 98 | 304 | Opar | `e104` | 20, 10, 10 |
| 99 | 305 | Haley Holey | `e105` | 6 |
| 100 | 306 | King Prickly | `e106` | 7 |
| 101 | 0 | Ice Barrier | `bari` | *(no STB)* |
| 102 | 0 | Ice Prison | `kori` | *(no STB)* |
| 103 | 0 | Ice Meteor | `i_me` | *(no STB)* |
| 104 | 0 | Ice Tornado | `i_ta` | *(no STB)* |
| 105 | 317 | Gacious | `e124` | 7.5 |
| 106 | 223 | Dark Genie (Final Form) | `c23a` | 30 |
| 107 | 0 | DG Final summon (last_mc) | `last_mc` | *(no STB)* |
| 108 | 0 | DG Final ground wave (last_gw1) | `last_gw1` | *(no STB)* |
| 109 | 0 | DG Final beam | `c23_beem` | *(none — no body collision)* |
| 110 | 0 | DG Final beam (small) | `c23_beem_s` | *(none — no body collision)* |
| 111 | 311 | Gemron (Fire) | `e111` | 7 |
| 112 | 308 | Nikapous | `e108` | 6.7, 3.9 |
| 113 | 56 | White Fang (Enhanced) | `e125` | 6, 3, 3 |
| 114 | 40 | Arthur (Enhanced) | `e126` | 7, 3, 3 |
| 115 | 91 | Sil (Enhanced) | `e127` | 5, 10, 6, 6 |
| 116 | 10 | Halloween (Enhanced) | `e128` | 5, 3 |
| 117 | 1 | Master Jacket (Enhanced) | `e129` | 6 |
| 118 | 70 | Vulcan (Enhanced) | `e130` | 9, 7, 7 |
| 119 | 50 | Mummy (Enhanced) | `e131` | 8 |
| 120 | 46 | Diamond (Enhanced) | `e132` | 7 |
| 121 | 312 | Gemron (Ice) | `e112` | 7 |
| 122 | 319 | Horn Head | `e119` | 5, 4 |
| 123 | 26 | Auntie Medu (Enhanced) | `e133` | 5, 4 |
| 124 | 77 | Rockanoff (Enhanced) | `e134` | 12 |
| 125 | 301 | Yammich (Enhanced) | `e135` | 7 |
| 126 | 21 | Witch Hellza (Enhanced) | `e136` | 6.5 |
| 127 | 64 | Steel Giant (Enhanced) | `e137` | 5, 10 |
| 128 | 45 | Club (Enhanced) | `e138` | 6 |
| 129 | 28 | Corcea (Enhanced) | `e139` | 7, 3, 3 |
| 130 | 309 | Mimic (Demon Shaft) | `e109` | 5.5 |
| 131 | 310 | King Mimic (Demon Shaft) | `e110` | 10 |
| 132 | 313 | Gemron (Thunder) | `e113` | 7 |
| 133 | 316 | Bishop Q | `e116` | 5.9, 5.9, 5.2, 5.9, 3.5 |
| 134 | 60 | Cave Bat (Enhanced) | `e140` | 6.5 |
| 135 | 90 | Gol (Enhanced) | `e141` | 5, 10, 6, 6 |
| 136 | 75 | Mask of Prajna (Enhanced) | `e142` | 8 |
| 137 | 24 | Gyon (Enhanced) | `e143` | 7 |
| 138 | 47 | Spade (Enhanced) | `e144` | 5 |
| 139 | 63 | Rash Dasher (Enhanced) | `e145` | 7, 3, 3 |
| 140 | 27 | Captain (Enhanced) | `e146` | 6, 3, 3 |
| 141 | 309 | Mimic (Demon Shaft) (Enhanced) | `e109` | 5.5 |
| 142 | 310 | King Mimic (Demon Shaft) (Enhanced) | `e110` | 10 |
| 143 | 314 | Gemron (Wind) | `e114` | 7 |
| 144 | 318 | Silver Gear | `e118` | 7 |
| 145 | 43 | Alexnder (Enhanced) | `e149` | 5 |
| 146 | 44 | Heart (Enhanced) | `e150` | 6 |
| 147 | 49 | Bomber Head (Enhanced) | `e151` | 6 |
| 148 | 71 | Crabby Hermit (Enhanced) | `e152` | 10 |
| 149 | 68 | Cursed Rose (Enhanced) | `e153` | 4.5, 6 |
| 150 | 25 | Pirate's Chariot (Enhanced) | `e154` | 10.2 |
| 151 | 72 | Space Gyon (Enhanced) | `e155` | 8 |
| 152 | 309 | Mimic (Demon Shaft) (Enhanced x2) | `e109` | 5.5 |
| 153 | 310 | King Mimic (Demon Shaft) (Enhanced x2) | `e110` | 10 |
| 154 | 315 | Gemron (Holy) | `e115` | 7 |
| 155 | 317 | Gacious (Enhanced) | `e117` | 7.5 |
| 156 | 61 | Evil Bat (Enhanced) | `e158` | 6.5 |
| 157 | 76 | Crescent Baron (Enhanced) | `e159` | 6 |
| 158 | 303 | Statue Dog (Enhanced) | `e160` | 10, 10 |
| 159 | 48 | Joker (Enhanced) | `e161` | 6 |
| 160 | 51 | Lich (Enhanced) | `e162` | 6.5 |
| 161 | 33 | Titan (Enhanced) | `e163` | 5, 10.6, 6, 6 |
| 162 | 55 | Living Armor (Enhanced) | `e164` | 7 |
| 163 | 309 | Mimic (Demon Shaft) (Enhanced x3) | `e109` | 5.5 |
| 164 | 310 | King Mimic (Demon Shaft) (Enhanced x3) | `e110` | 10 |
| 165 | 221 | Black Knight | `c21a` | 5, 10, 5, 5, 6, 6 |
| 166 | 221 | Black Knight Mount | `c22a` | 15, 10, 10, 15, 10, 10 |
| — | 54 | Killer Snake | — | *(no STB)* |
