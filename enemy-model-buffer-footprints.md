# Enemy model-buffer footprints (for randomizer budget-aware selection)

When a floor loads, `BtLoadMonstor` loads each roster entry's mesh into the **MonstorModelBuffer**
(`CDataAlloc2` @ PS2 `0x01F066D0` → PCSX2 `0x21F066D0`; struct: `+0x8` used, `+0xC` capacity). If the
combined mesh data of the floor's distinct species exceeds the buffer, the load stalls and the floor
**hangs**. The randomizer must therefore pick species whose combined footprint stays under the
(per-dungeon) capacity.

Static file sizes do **not** predict the footprint: the `.chr` file is ~10× too big and the `.mds`
model member is ~3× too big and even anti-correlates (it's mostly motion data). So footprints are
**measured at runtime** via `MeasureBufferMode` (one species per floor → buffer `used` ≈ that species'
footprint; baseline is small — ~187 bytes, the smallest single-species reads (Ice Queen, Black Knight
rider) land there — so it's negligible vs the 18–145 KB species footprints).

Composite bosses: some bosses are split into a small rider mesh + a large mount mesh that load as
separate species (e.g. Black Knight = c21a rider 187 + c22a mount 144491). Spawning only the rider
gives a visible, damaging enemy with bugged movement; a complete boss needs both halves loaded.

## Per-dungeon buffer capacity (`cap`)

The buffer is sized per dungeon (vanilla rosters fit exactly), so the budget is read **live** at
`0x21F066DC`. Observed:

| Dungeon | id | cap (bytes) |
|---|---|---|
| Divine Beast Cave | 0 | 413,608 |
| Wise Owl Forest | 1 | 278,824 |
| Shipwreck / Coastal | 2 | 342,304 |
| Sun & Moon Temple | 3 | 442,572 |
| Moon Sea | 4 | 298,592 |
| Gallery of Time | 5 | varies by area — see below |
| Demon Shaft | 6 | _TBD_ (likely ~5 band caps, like its 5 visual zones) |

(Cross-dungeon transition floors briefly report the previous dungeon's cap — ignore those.)

The cap tracks the floor's **visual area**, which can change mid-dungeon, so it's read live per floor.
Gallery of Time (dungeon 5) shifts every ~6 floors as its look changes:

| Display floors | cap (bytes) |
|---|---|
| 1–6   | 333,336 |
| 7–12  | 324,208 |
| 13–18 | 348,672 |
| 19–24 | 348,664 |

(Demon Shaft's 100 floors are expected to behave the same way across its ~5 visual zones.)

## Measured species footprints

`footprint` = single-species floor buffer `used` (bytes). ⚠ = suspected stale/residual read, re-measure.

| TableIndex | ModelCode | Name | footprint |
|---|---|---|---|
| 0 | e01a | Master Jacket | 78158 |
| 1 | e03a | Skeleton Soldier | 75950 |
| 2 | e05a | Statue | 54950 |
| 3 | e06a | Dasher | 37036 |
| 4 | e07a | Werewolf | 60549 |
| 5 | e08a | FliFli | 27582 |
| 6 | e09a | Hornet | 28825 |
| 7 | e10a | Halloween | 51996 |
| 8 | e11a | Cannibal Plant | 40494 |
| 9 | e12a | Earth Digger | 43573 |
| 10 | e14a | Sunday | 42766 |
| 11 | e15a | Monday | 42314 |
| 12 | e16a | Tuesday | 43983 |
| 13 | e17a | Wednesday | 42198 |
| 14 | e18a | Thursday | 45935 |
| 15 | e19a | Friday | 41825 |
| 16 | e20a | Saturday | 36941 |
| 17 | e21a | Witch Hellza | 60435 |
| 18 | e22a | Witch Illza | 44499 |
| 19 | e23a | Gunny | 35380 |
| 20 | e24a | Gyon | 53038 |
| 21 | e25a | Pirate's Chariot | 28414 |
| 22 | e26a | Auntie Medu | 56893 |
| 23 | e27a | Captain | 50447 |
| 24 | e28a | Corcea | 51363 |
| 25 | e30a | Golem | 55300 |
| 26 | e31a | Mr. Blare | 53519 |
| 27 | e32a | Dune | 43481 |
| 28 | e33a | Titan | 54336 |
| 29 | e34a | King Mimic (Divine Beast Cave) | 38794 |
| 30 | e35a | Mimic (Divine Beast Cave) | 24823 |
| 31 | e36a | King Mimic (Sun & Moon Temple) | 38858 |
| 32 | e37a | Mimic (Sun & Moon Temple) | 24831 |
| 33 | e38a | King Mimic (Moon Sea) | 39290 |
| 34 | e39a | Mimic (Moon Sea) | 25223 |
| 35 | e40a | Arthur | 45627 |
| 36 | e42a | Ghost | 49096 |
| 37 | e43a | Alexander | 39287 |
| 38 | e44a | Heart | 58698 |
| 39 | e45a | Club | 49516 |
| 40 | e46a | Diamond | 48133 |
| 41 | e47a | Spade | 48269 |
| 42 | e48a | Joker | 49660 |
| 43 | e49a | Bomber Head | 62404 |
| 44 | e50a | Mummy | 68723 |
| 45 | e51a | Lich | 54789 |
| 46 | e52a | Curse Dancer | 37022 |
| 47 | e55a | Living Armor | 55777 |
| 48 | e56a | White Fang | 59337 |
| 49 | e57a | Moon Bug | 38394 |
| 50 | e58a | Phantom | 28865 |
| 51 | e59a | Dragon | 73627 |
| 52 | e60a | Cave Bat | 18760 |
| 53 | e61a | Evil Bat | 18637 |
| 54 | e62a | Hell Pockle | 42954 |
| 55 | e63a | Rash Dasher | 42868 |
| 56 | e64a | Steel Giant | 54780 |
| 57 | e65a | Blizzard | 51196 |
| 58 | e66a | Moon Digger | 36157 |
| 59 | e67a | Dark Flower | 37762 |
| 60 | e68a | Cursed Rose | 33850 |
| 61 | e69a | Billy | 50139 |
| 62 | e70a | Vulcan | 45198 |
| 63 | e71a | Crabby Hermit | 32496 |
| 64 | e72a | Space Gyon | 54258 |
| 65 | e73a | Blue Dragon | 86880 |
| 66 | e74a | Black Dragon | 74987 |
| 67 | e75a | Mask of Prajna | 36718 |
| 68 | e76a | Crescent Baron | 40148 |
| 69 | e77a | Rockanoff | 16333 |
| 70 | e78a | King Mimic (Wise Owl Forest) | 38642 |
| 71 | e79a | Mimic (Wise Owl Forest) | 24811 |
| 72 | e80a | King Mimic (Shipwreck) | 38770 |
| 73 | e81a | Mimic (Shipwreck) | 24795 |
| 74 | e82a | King Mimic (Gallery of Time) | 38810 |
| 75 | e83a | Mimic (Gallery of Time) | 24831 |
| 76 | korinoya | Ice Arrow | 7681 |
| 77 | e86a | Sam | 58888 |
| 78 | c12a | Dran | 134603 |
| 79 | c14a | Master Utan | 121379 |
| 80 | c13a | Ice Queen | 187 (tiny — fights via companions) |
| 81 | c15a | King's Curse Coffin | 17117 |
| 82 | c15b | King's Curse | 24187 |
| 83 | c16a | Minotaur Joe | 91889 |
| 84 | c17a | Dark Genie | 114919 |
| 85 | c17b | Dark Genie (form 2) | 41201 |
| 86 | c17c | Right Hand | 41249 |
| 87 | c17_ | Left Hand | 8621 |
| 88 | c17_ | (DG companion c17_) | 16563 |
| 89 | c17_ | (DG companion c17_) | 21882 |
| 90 | c17_ | (DG companion c17_) | 7475 |
| 91 | e85a | Wine Keg | 3172 |
| 92 | b3_r | Ice Aura | 9393 |
| 93 | c17_ | (DG companion c17_) | 9076 |
| 94 | e90a | Gol | 55308 |
| 95 | e91a | Sil | 55308 |
| 96 | e101 | Yammich | 26595 |
| 97 | e103 | Statue Dog | 30135 |
| 98 | e104 | Opar | 41629 |
| 99 | e105 | Haley Holey | 59418 |
| 100 | e106 | King Prickly | 44518 |
| 101 | bari | Ice Barrier | 6805 |
| 102 | kori | Ice Prison | 7401 |
| 103 | i_me | Ice Meteor | 7646 |
| 104 | i_ta | Ice Tornado | 10741 |
| 105 | e124 | Gacious | 27766 |
| 106 | c23a | Dark Genie (Final Form) | 80660 |
| 107 | last_mc | DG Final summon (last_mc) | 8596 |
| 108 | last_gw1 | DG Final ground wave (last_gw1) | 7416 |
| 109 | c23_beem | DG Final beam | 21938 |
| 110 | c23_beem_s | DG Final beam (small) | 7423 |
| 111 | e111 | Gemron (Fire) | 64091 |
| 112 | e108 | Nikapous | 69755 |
| 113 | e125 | White Fang (Enhanced) | 68981 |
| 114 | e126 | Arthur (Enhanced) | 45647 |
| 115 | e127 | Sil (Enhanced) | 55324 |
| 116 | e128 | Halloween (Enhanced) | 52028 |
| 117 | e129 | Master Jacket (Enhanced) | 78162 |
| 118 | e130 | Vulcan (Enhanced) | 45214 |
| 119 | e131 | Mummy (Enhanced) | 91635 |
| 120 | e132 | Diamond (Enhanced) | 48133 |
| 121 | e112 | Gemron (Ice) | 64216 |
| 122 | e119 | Horn Head | 52419 |
| 123 | e133 | Auntie Medu (Enhanced) | 56893 |
| 124 | e134 | Rockanoff (Enhanced) | 16349 |
| 125 | e135 | Yammich (Enhanced) | 26611 |
| 126 | e136 | Witch Hellza (Enhanced) | 60483 |
| 127 | e137 | Steel Giant (Enhanced) | 54796 |
| 128 | e138 | Club (Enhanced) | 49516 |
| 129 | e139 | Corcea (Enhanced) | 51038 |
| 130 | e109 | Mimic (Demon Shaft) | 25403 |
| 131 | e110 | King Mimic (Demon Shaft) | 39746 |
| 132 | e113 | Gemron (Thunder) | 62767 |
| 133 | e116 | Bishop Q | 67591 |
| 134 | e140 | Cave Bat (Enhanced) | 18780 |
| 135 | e141 | Gol (Enhanced) | 55324 |
| 136 | e142 | Mask of Prajna (Enhanced) | 36718 |
| 137 | e143 | Gyon (Enhanced) | 53050 |
| 138 | e144 | Spade (Enhanced) | 48269 |
| 139 | e145 | Rash Dasher (Enhanced) | 42884 |
| 140 | e146 | Captain (Enhanced) | 50447 |
| 141 | e109 | Mimic (Demon Shaft) (Enhanced) | 25399 |
| 142 | e110 | King Mimic (Demon Shaft) (Enhanced) | 39746 |
| 143 | e114 | Gemron (Wind) | 66562 |
| 144 | e118 | Silver Gear | 64198 |
| 145 | e149 | Alexnder (Enhanced) | 39711 |
| 146 | e150 | Heart (Enhanced) | 58698 |
| 147 | e151 | Bomber Head (Enhanced) | 62424 |
| 148 | e152 | Crabby Hermit (Enhanced) | 32512 |
| 149 | e153 | Cursed Rose (Enhanced) | 33886 |
| 150 | e154 | Pirate's Chariot (Enhanced) | 28442 |
| 151 | e155 | Space Gyon (Enhanced) | 54606 |
| 152 | e109 | Mimic (Demon Shaft) (Enhanced x2) | 25431 |
| 153 | e110 | King Mimic (Demon Shaft) (Enhanced x2) | 39746 |
| 154 | e115 | Gemron (Holy) | 64147 |
| 155 | e117 | Gacious (Enhanced) | 45760 |
| 156 | e158 | Evil Bat (Enhanced) | 18673 |
| 157 | e159 | Crescent Baron (Enhanced) | 40164 |
| 158 | e160 | Statue Dog (Enhanced) | 30171 |
| 159 | e161 | Joker (Enhanced) | 49660 |
| 160 | e162 | Lich (Enhanced) | 54825 |
| 161 | e163 | Titan (Enhanced) | 54352 |
| 162 | e164 | Living Armor (Enhanced) | 55809 |
| 163 | e109 | Mimic (Demon Shaft) (Enhanced x3) | 25431 |
| 164 | e110 | King Mimic (Demon Shaft) (Enhanced x3) | 39746 |
| 165 | c21a | Black Knight | 187 ⚠ rider mesh only (loads + can hurt, but movement bugged without its mount — pair with TI 166) |
| 166 | c22a | Black Knight Mount | 144491 (the horse/mount half of the Black Knight; needs TI 165 rider for a complete, non-bugged boss) |


Range so far ~18–78 KB/species; a DBC floor (cap 413,608) fits ~6–9 depending on the mix — matching the
observed hang at 9 heavy species. Once complete, the selector greedily adds species while the running
total stays under ~90% of the live `cap`.

**Measured: all 167 species (TI 0–166)**, including the composite Black Knight (rider c21a + mount c22a).
Footprint set is complete. Only the **Demon Shaft cap** remains.

Note: the per-dungeon cap actually varies somewhat per floor (Gallery spans ~324K–349K), so the selector must read it **live** (`0x21F066DC`) each floor rather than using a fixed per-dungeon constant.
