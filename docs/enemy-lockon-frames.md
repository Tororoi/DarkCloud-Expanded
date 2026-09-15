# Enemy lock-on frames (where Xiao aims)

Generated 2026-09-09 by sweeping every `dun\monstor\*.stb` on the disc for `_STATUS_SET_LOCKON_TRG(frame, w, h)`
(monster cmd 118, handler 0x1E3710) and resolving the named frame in the species `.chr` `.mds` bind pose.

**Runtime path (RE'd):** the handler stores the frame ptr at slot `+0x1E4CC` (FloorSlots `+0xFC`, `EnemySlotOffsets.LockOnFrame`)
and w/h at `+0x1E4E0/+0x1E4E4` (reticle). `DrawMonstor` writes the frame's world position to slot `+0x1E4D0` (FloorSlots
`+0x100`, `LockOnPoint`) every draw; `setTargetCursor` (dun 0x1DC07A0) copies it to the aim global `0x1DC4500`, which
`BattleActionPlay_Jinn` flies pellets at when locked on. **No frame → aim = enemy origin + 8.** Xiao's pellets leave her
weapon's `eff30` frame (zero offset).

"Bind pos" = the frame's position in the model's bind pose (x, height, y; model units, chained local bind matrices, no
animation). Height is the number to compare against an enemy's stance — it is where the reticle sits at rest.

**154 scripts set a frame** (138 use a node named `lockon`); **18 set none** (fallback origin+8).

| Script | Species | Frame | Reticle w×h | Bind pos (x, h, y) | In |
|---|---|---|---|---|---|
| __e116a |  | `lockon` | 1.2×1.3 | (0.0, **8.5**, 0.0) | e116a.mds |
| _c13a | Ice Queen | `spine` | 1.0×1.0 | (0.0, **10.6**, 0.1) | c13a.mds |
| c12a | Dran | `face` | 1.0×1.0 | (-0.0, **40.6**, 37.0) | c12a.mds |
| c13_baria |  | `null1_1_3_1` | 1.0×1.0 | no chr |  |
| c13a | Ice Queen | `spine` | 1.0×1.0 | (0.0, **10.6**, 0.1) | c13a.mds |
| c14a | Master Utan | `null40_2_3_1` | 1.0×1.0 | missing |  |
| c15a | King's Curse Coffin | `col` | 2.0×2.0 | (0.0, **26.1**, 0.0) | c15a.mds |
| c15b | King's Curse | `neck` | 2.0×2.0 | (-0.0, **34.3**, -0.3) | c15b.mds |
| c16a | Minotaur Joe | `hip` | 1.0×1.0 | (0.0, **11.8**, 0.7) | c16a.mds |
| c17a | Dark Genie | `dcol00` | 1.0×1.0 | (-0.0, **52.1**, 23.4) | c17a.mds |
| c17b | Dark Genie (form 2) | `r_fin_10` | 1.0×1.0 | (-1.1, **33.5**, -0.0) | c17b.mds |
| c17c | Right Hand | `l_fin_10` | 1.0×1.0 | (1.1, **33.5**, -0.0) | c17c.mds |
| c21a | Black Knight | `kosi` | 1.0×1.0 | (0.0, **14.8**, 0.0) | c21a.mds |
| c22a | Black Knight Mount | `null79` | 3.0×3.0 | (0.0, **17.5**, 0.0) | c22a.mds |
| c23a | Dark Genie (Final Form) | `coa__czapp` | 1.0×1.0 | (0.0, **-1.7**, 104.5) | c23a.mds |
| e01a | Master Jacket | `lockon` | 1.2×1.3 | (0.0, **10.7**, 1.7) | e01a.mds |
| e03a | Skeleton Soldier | `lockon` | 1.2×1.3 | (0.0, **10.7**, 1.7) | e03a.mds |
| e05a | Statue | `lockon` | 1.2×1.7 | (0.0, **11.0**, 0.0) | e05a.mds |
| e06a | Dasher | `lockon` | 1.7×1.7 | (0.0, **9.5**, 0.0) | e06a.mds |
| e07a | Werewolf | `lockon` | 1.5×1.5 | (0.0, **9.5**, 3.0) | e07a.mds |
| e08a | FliFli | `lockon` | 1.3×1.3 | (0.0, **8.0**, 0.0) | e08a.mds |
| e09a | Hornet | `lockon` | 1.2×1.2 | (0.0, **-1.0**, -2.0) | e09a.mds |
| e101a | Yammich | `lockon` | 1.0×1.7 | (0.0, **8.0**, 0.0) | e101a.mds |
| e103a | Statue Dog | `lockon` | 1.5×1.5 | (0.0, **9.0**, 0.0) | e103a.mds |
| e104a | Opar | `lockon` | 3.8×4.4 | (0.0, **23.0**, 0.0) | e104a.mds |
| e105a | Haley Holey | `lockon` | 1.0×1.1 | (0.0, **5.5**, 0.0) | e105a.mds |
| e106a | King Prickly | `lockon` | 1.4×1.5 | (0.0, **8.0**, 0.0) | e106a.mds |
| e108a | Nikapous | `lockon` | 1.2×1.2 | (0.0, **11.2**, 1.1) | e108a.mds |
| e109a | Mimic (Demon Shaft) | `lockon` | 1.1×1.0 | (0.0, **4.6**, 1.0) | e109a.mds |
| e10a | Halloween | `lockon` | 1.3×1.3 | (0.0, **9.0**, 0.0) | e10a.mds |
| e110a | King Mimic (Demon Shaft) | `lockon` | 1.9×1.65 | (0.0, **9.6**, 1.0) | e110a.mds |
| e111a | Gemron (Fire) | `lockon` | 1.2×1.3 | (0.0, **12.7**, 0.0) | e111a.mds |
| e112a | Gemron (Ice) | `lockon` | 1.2×1.3 | (0.0, **12.7**, 0.0) | e112a.mds |
| e113a | Gemron (Thunder) | `lockon` | 1.2×1.3 | (0.0, **12.7**, 0.0) | e113a.mds |
| e114a | Gemron (Wind) | `lockon` | 1.2×1.3 | (0.0, **12.7**, 0.0) | e114a.mds |
| e115a | Gemron (Holy) | `lockon` | 1.2×1.3 | (0.0, **12.7**, 0.0) | e115a.mds |
| e116a | Bishop Q | `lockon` | 1.2×1.3 | (0.0, **8.5**, 0.0) | e116a.mds |
| e117a | Gacious (Enhanced) | `lockon` | 1.8×1.8 | (0.0, **12.0**, 3.0) | e117a.mds |
| e118a | Silver Gear | `lockon` | 1.2×1.3 | (0.0, **10.7**, 1.7) | e118a.mds |
| e119a | Horn Head | `lockon` | 0.8×1.7 | (0.0, **8.5**, 0.0) | e119a.mds |
| e11a | Cannibal Plant | `lockon` | 1.4×1.6 | (0.0, **11.0**, 0.0) | e11a.mds |
| e124a | Gacious | `lockon` | 1.8×1.8 | (0.0, **12.0**, 3.0) | e117a.mds |
| e125a | White Fang (Enhanced) | `lockon` | 1.6×1.7 | (0.0, **9.5**, 3.0) | e56a.mds |
| e126a | Arthur (Enhanced) | `lockon` | 2.4×2.2 | (0.0, **13.6**, 1.0) | e40a.mds |
| e127a | Sil (Enhanced) | `lockon` | 2.4×2.4 | (0.0, **13.6**, 3.0) | e91a.mds |
| e128a | Halloween (Enhanced) | `lockon` | 1.3×1.3 | (0.0, **9.0**, 0.0) | e10a.mds |
| e129a | Master Jacket (Enhanced) | `lockon` | 1.2×1.3 | (0.0, **10.7**, 1.7) | e01a.mds |
| e12a | Earth Digger | `lockon` | 1.3×1.3 | (0.0, **2.0**, 0.0) | e12a.mds |
| e130a | Vulcan (Enhanced) | `lockon` | 1.7×1.5 | (0.0, **8.6**, 3.0) | e70a.mds |
| e131a | Mummy (Enhanced) | `lockon` | 1.3×1.4 | (0.0, **8.0**, 0.0) | e50a.mds |
| e132a | Diamond (Enhanced) | `lockon` | 1.2×1.3 | (0.0, **7.0**, 0.0) | e46a.mds |
| e133a | Auntie Medu (Enhanced) | `lockon` | 1.4×1.4 | (0.0, **8.5**, 0.0) | e26a.mds |
| e134a | Rockanoff (Enhanced) | `lockon` | 1.7×1.7 | (0.0, **8.5**, 0.0) | e77a.mds |
| e135a | Yammich (Enhanced) | `lockon` | 1.0×1.7 | (0.0, **8.0**, 0.0) | e101a.mds |
| e136a | Witch Hellza (Enhanced) | `lockon` | 1.3×1.4 | (0.0, **8.5**, 0.0) | e21a.mds |
| e137a | Steel Giant (Enhanced) | `lockon` | 2.4×2.4 | (0.0, **13.6**, 3.0) | e64a.mds |
| e138a | Club (Enhanced) | `lockon` | 1.2×1.3 | (0.0, **7.0**, 0.0) | e45a.mds |
| e139a | Corcea (Enhanced) | `lockon` | 1.4×1.4 | (0.0, **8.6**, 0.0) | e28a.mds |
| e140a | Cave Bat (Enhanced) | `lockon` | 0.8×0.8 | (-0.0, **-0.0**, -0.0) | e60a.mds |
| e141a | Gol (Enhanced) | `lockon` | 2.4×2.4 | (0.0, **13.6**, 3.0) | e90a.mds |
| e142a | Mask of Prajna (Enhanced) | `lockon` | 1.0×1.0 | (0.3, **11.4**, -0.0) | e75a.mds |
| e143a | Gyon (Enhanced) | `lockon` | 1.4×1.6 | (0.0, **10.5**, -1.0) | e24a.mds |
| e144a | Spade (Enhanced) | `lockon` | 1.2×1.3 | (0.0, **7.0**, 0.0) | e47a.mds |
| e145a | Rash Dasher (Enhanced) | `lockon` | 1.7×1.7 | (0.0, **9.5**, 0.0) | e63a.mds |
| e147a |  | `lockon` | 1.1×1.0 | (0.0, **4.6**, 1.0) | e109a.mds |
| e148a |  | `lockon` | 1.9×1.65 | (0.0, **9.6**, 1.0) | e110a.mds |
| e149a | Alexander (Enhanced) | `lockon` | 1.6×1.6 | (0.5, **13.6**, 0.0) | e43a.mds |
| e14a | Sunday | `lockon` | 1.0×1.0 | (0.0, **6.5**, 0.0) | e14a.mds |
| e150a | Heart (Enhanced) | `lockon` | 1.2×1.3 | (0.0, **7.0**, 0.0) | e44a.mds |
| e151a | Bomber Head (Enhanced) | `lockon` | 1.3×1.4 | (0.0, **9.0**, 0.0) | e49a.mds |
| e152a | Crabby Hermit (Enhanced) | `lockon` | 1.9×1.9 | (-0.0, **10.2**, -3.0) | e71a.mds |
| e153a | Cursed Rose (Enhanced) | `lockon` | 1.4×1.6 | (0.0, **11.0**, 0.0) | e68a.mds |
| e154a | Pirate's Chariot (Enhanced) | `lockon` | 1.9×1.8 | (0.0, **9.5**, 3.0) | e25a.mds |
| e155a | Space Gyon (Enhanced) | `lockon` | 1.6×1.7 | (0.0, **10.5**, -1.0) | e72a.mds |
| e156a |  | `lockon` | 1.1×1.0 | (0.0, **4.6**, 1.0) | e109a.mds |
| e157a |  | `lockon` | 1.9×1.65 | (0.0, **9.6**, 1.0) | e110a.mds |
| e158a | Evil Bat (Enhanced) | `lockon` | 0.8×0.8 | (0.0, **0.5**, 0.0) | e61a.mds |
| e159a | Crescent Baron (Enhanced) | `lockon` | 1.6×1.9 | (0.0, **15.5**, 0.0) | e76a.mds |
| e15a | Monday | `lockon` | 1.0×1.0 | (0.0, **6.5**, 0.0) | e15a.mds |
| e160a | Statue Dog (Enhanced) | `lockon` | 1.5×1.5 | (0.0, **9.0**, 0.0) | e103a.mds |
| e161a | Joker (Enhanced) | `lockon` | 1.2×1.3 | (0.0, **7.0**, 0.0) | e48a.mds |
| e162a | Lich (Enhanced) | `lockon` | 1.1×1.1 | (0.0, **6.6**, 0.0) | e51a.mds |
| e163a | Titan (Enhanced) | `lockon` | 2.4×2.4 | (0.0, **13.6**, 3.0) | e33a.mds |
| e164a | Living Armor (Enhanced) | `lockon` | 1.3×1.8 | (0.0, **11.0**, 0.0) | e55a.mds |
| e165a |  | `lockon` | 1.1×1.0 | (0.0, **4.6**, 1.0) | e109a.mds |
| e166a |  | `lockon` | 1.9×1.65 | (0.0, **9.6**, 1.0) | e110a.mds |
| e16a | Tuesday | `lockon` | 1.0×1.0 | (0.0, **6.5**, 0.0) | e16a.mds |
| e17a | Wednesday | `lockon` | 1.0×1.0 | (0.0, **6.5**, 0.0) | e17a.mds |
| e18a | Thursday | `lockon` | 1.0×1.0 | (0.0, **6.5**, 0.0) | e18a.mds |
| e19a | Friday | `lockon` | 1.0×1.0 | (0.0, **6.5**, 0.0) | e19a.mds |
| e20a | Saturday | `lockon` | 1.0×1.0 | (0.0, **6.5**, 0.0) | e20a.mds |
| e21a | Witch Hellza | `lockon` | 1.3×1.4 | (0.0, **8.5**, 0.0) | e21a.mds |
| e22a | Witch Illza | `lockon` | 1.3×1.4 | (0.0, **8.5**, 0.0) | e22a.mds |
| e23a | Gunny | `lockon` | 1.5×1.5 | (0.0, **8.5**, 0.0) | e23a.mds |
| e24a | Gyon | `lockon` | 1.4×1.6 | (0.0, **10.5**, -1.0) | e24a.mds |
| e25a | Pirate's Chariot | `lockon` | 1.9×1.8 | (0.0, **9.5**, 3.0) | e25a.mds |
| e26a | Auntie Medu | `lockon` | 1.4×1.4 | (0.0, **8.5**, 0.0) | e26a.mds |
| e28a | Corcea | `lockon` | 1.4×1.4 | (0.0, **8.6**, 0.0) | e28a.mds |
| e30a | Golem | `lockon` | 2.4×2.4 | (0.0, **13.6**, 3.0) | e30a.mds |
| e31a | Mr. Blare | `lockon` | 1.2×1.6 | (0.0, **9.0**, 0.0) | e31a.mds |
| e32a | Dune | `lockon` | 1.7×1.5 | (0.0, **8.6**, 3.0) | e32a.mds |
| e33a | Titan | `lockon` | 2.4×2.4 | (0.0, **13.6**, 3.0) | e33a.mds |
| e34a | King Mimic (Divine Beast Cave) | `lockon` | 1.9×1.65 | (0.0, **9.6**, 1.0) | e34a.mds |
| e35a | Mimic (Divine Beast Cave) | `lockon` | 1.1×1.0 | (0.0, **4.6**, 1.0) | e35a.mds |
| e36a | King Mimic (Sun & Moon Temple) | `lockon` | 1.9×1.65 | (0.0, **9.6**, 1.0) | e36a.mds |
| e37a | Mimic (Sun & Moon Temple) | `lockon` | 1.1×1.0 | (0.0, **4.6**, 1.0) | e37a.mds |
| e38a | King Mimic (Moon Sea) | `lockon` | 1.9×1.65 | (0.0, **9.6**, 1.0) | e38a.mds |
| e39a | Mimic (Moon Sea) | `lockon` | 1.1×1.0 | (0.0, **4.6**, 1.0) | e39a.mds |
| e40a | Arthur | `lockon` | 2.4×2.2 | (0.0, **13.6**, 1.0) | e40a.mds |
| e42a | Ghost | `lockon` | 1.1×1.1 | (0.0, **6.6**, 0.0) | e42a.mds |
| e43a | Alexander | `lockon` | 1.6×1.6 | (0.5, **13.6**, 0.0) | e43a.mds |
| e44a | Heart | `lockon` | 1.2×1.3 | (0.0, **7.0**, 0.0) | e44a.mds |
| e45a | Club | `lockon` | 1.2×1.3 | (0.0, **7.0**, 0.0) | e45a.mds |
| e46a | Diamond | `lockon` | 1.2×1.3 | (0.0, **7.0**, 0.0) | e46a.mds |
| e47a | Spade | `lockon` | 1.2×1.3 | (0.0, **7.0**, 0.0) | e47a.mds |
| e48a | Joker | `lockon` | 1.2×1.3 | (0.0, **7.0**, 0.0) | e48a.mds |
| e49a | Bomber Head | `lockon` | 1.3×1.4 | (0.0, **9.0**, 0.0) | e49a.mds |
| e50a | Mummy | `lockon` | 1.3×1.4 | (0.0, **8.0**, 0.0) | e50a.mds |
| e51a | Lich | `lockon` | 1.1×1.1 | (0.0, **6.6**, 0.0) | e51a.mds |
| e52a | Curse Dancer | `lockon` | 1.2×1.4 | (0.0, **8.1**, 0.0) | e52a.mds |
| e55a | Living Armor | `lockon` | 1.3×1.8 | (0.0, **11.0**, 0.0) | e55a.mds |
| e56a | White Fang | `lockon` | 1.6×1.7 | (0.0, **9.5**, 3.0) | e56a.mds |
| e57a | Moon Bug | `lockon` | 1.6×1.7 | (0.0, **8.6**, 0.0) | e57a.mds |
| e58a | Phantom | `lockon` | 1.2×1.2 | (0.0, **-1.0**, -2.0) | e58a.mds |
| e59a | Dragon | `lockon` | 2.9×2.7 | (0.0, **15.0**, 0.0) | e59a.mds |
| e60a | Cave Bat | `lockon` | 0.8×0.8 | (-0.0, **-0.0**, -0.0) | e60a.mds |
| e61a | Evil Bat | `lockon` | 0.8×0.8 | (-0.0, **-0.0**, -0.0) | e61s.mds |
| e62a | Hell Pockle | `lockon` | 1.0×1.0 | (0.0, **6.5**, 0.0) | e62a.mds |
| e63a | Rash Dasher | `lockon` | 1.7×1.7 | (0.0, **9.5**, 0.0) | e63a.mds |
| e64a | Steel Giant | `lockon` | 2.4×2.4 | (0.0, **13.6**, 3.0) | e64a.mds |
| e65a | Blizzard | `lockon` | 2.4×2.4 | (0.0, **13.6**, 3.0) | e65a.mds |
| e66a | Moon Digger | `lockon` | 1.3×1.3 | (0.0, **2.0**, 0.0) | e66a.mds |
| e67a | Dark Flower | `lockon` | 1.4×1.6 | (0.0, **12.5**, 0.0) | e67a.mds |
| e68a | Cursed Rose | `lockon` | 1.4×1.6 | (0.0, **11.0**, 0.0) | e68a.mds |
| e69a | Billy | `lockon` | 1.2×1.6 | (0.0, **9.0**, 0.0) | e69a.mds |
| e70a | Vulcan | `lockon` | 1.7×1.5 | (0.0, **8.6**, 3.0) | e70a.mds |
| e71a | Crabby Hermit | `lockon` | 1.9×1.9 | (-0.0, **10.2**, -3.0) | e71a.mds |
| e72a | Space Gyon | `lockon` | 1.6×1.7 | (0.0, **10.5**, -1.0) | e72a.mds |
| e73a | Blue Dragon | `lockon` | 2.9×2.7 | (0.0, **14.0**, 0.0) | e73a.mds |
| e74a | Black Dragon | `lockon` | 2.9×2.7 | (0.0, **14.0**, 0.0) | e74a.mds |
| e75a | Mask of Prajna | `lockon` | 1.0×1.0 | (0.3, **11.4**, -0.0) | e75a.mds |
| e76a | Crescent Baron | `lockon` | 1.6×1.9 | (0.0, **15.5**, 0.0) | e76a.mds |
| e77a | Rockanoff | `lockon` | 1.7×1.7 | (0.0, **8.5**, 0.0) | e77a.mds |
| e78a | King Mimic (Wise Owl Forest) | `lockon` | 1.9×1.65 | (0.0, **9.6**, 1.0) | e34a.mds |
| e79a | Mimic (Wise Owl Forest) | `lockon` | 1.1×1.0 | (0.0, **4.6**, 1.0) | e35a.mds |
| e80a | King Mimic (Shipwreck) | `lockon` | 1.9×1.65 | (0.0, **9.6**, 1.0) | e34a.mds |
| e81a | Mimic (Shipwreck) | `lockon` | 1.1×1.0 | (0.0, **4.6**, 1.0) | e35a.mds |
| e82a | King Mimic (Gallery of Time) | `lockon` | 1.9×1.65 | (0.0, **9.6**, 1.0) | e34a.mds |
| e83a | Mimic (Gallery of Time) | `lockon` | 1.1×1.0 | (0.0, **4.6**, 1.0) | e35a.mds |
| e84a |  | `e84a` | 1.0×1.0 | (0.0, **0.0**, 0.0) | e84a.mds |
| e85a | Wine Keg | `e85a` | 1.0×1.0 | (0.0, **0.0**, 0.0) | e85a.mds |
| e86a | Sam | `lockon` | 1.2×1.6 | (0.0, **9.0**, 0.0) | e86a.mds |
| e90a | Gol | `lockon` | 2.4×2.4 | (0.0, **13.6**, 3.0) | e90a.mds |
| e91a | Sil | `lockon` | 2.4×2.4 | (0.0, **13.6**, 3.0) | e91a.mds |

## No lock-on frame (aim = origin + 8)

b3_reiki, c13_i_meteo, c13_i_tatumaki, c13_kori, c13_korinoya, c13_reiki, c13_tatumaki, c17_beem, c17_beem_s, c17_hikari, c17_kaze, c17_syougeki, c23_beem (DG Final beam), c23_beem_s (DG Final beam (small)), c23_hasira, c23_syougeki, e146a (Captain (Enhanced)), e27a (Captain)
