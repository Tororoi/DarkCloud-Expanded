# Dungeon tiles

How a dungeon floor is built from tiles, and whether any tile content went unused.

## The system

A floor is a **20×20 grid**. Each cell holds a **part index** (`CDungeonMap + 0x9C50`, stride 0x10) and a
**rotation** (`+0x9C54`). A part is defined in the floor's `.cfg`; the meshes live in the matching `.mpd`.

```
//no32                                     <- the part's index is its ORDER among DEF_PATS blocks
DEF_PATS
 PT_BASE "d01g01_0.mds" ,0 ,0,0,0, 0      <- visual mesh (a part may have several)
 PT_COLS "d01g01_a.mds" ,0                <- collision mesh
 PT_CAM  "d01g01_v.mds" ,0                <- camera mesh
 PT_FIRE -3.74, 2.73, 4.10                <- torch position
DEF_ENDS
```
Directives seen across all 35 floor configs: `PT_BASE`, `PT_COLS`, `PT_CAM`, `PT_LIGHT`, `PT_GLIGHT`,
`PT_MARKER`, `PT_HIT_MARKER`, `PT_FIRE`, `PT_NPC`, `PT_WATER`, `PT_ROT`, `PT_SCALE`, `PT_HEAL_ZONE`,
`PT_DRAW_FLAG`.

**Part index = the order of the `DEF_PATS` blocks** (counting only uncommented ones). Verified against the
engine: `mapPartsFilter` writes part `0x20` (32) for the healing spring, and block 32 of `d01main_a.cfg` is
exactly the 回復の泉 part — the one carrying `PT_NPC "d01o03_m.chr"` (the waterfall, 滝) and `PT_WATER`.

## How the builder chooses a part

`buildRandomMap` (0x1CB670) carves rooms, then `mapPartsFilter` (0x1C5550) walks every cell, builds a
**4-bit mask of which neighbours are open**, and looks the mask up in a chain table to get `{part, rotation}`.
The whole tile vocabulary of the game is therefore these five tables:

**`chainTableDivid`** @ ELF `0x27A000` — 6 entries

| neighbour mask | part | rot |
|---|---|---|
| `0xE8` | 18 | 0 |
| `0xD4` | 19 | 0 |
| `0x71` | 20 | 0 |
| `0xB2` | 21 | 0 |
| `0xF9` | 26 | 0 |
| `0xF6` | 27 | 0 |

**`chainTableDividDoor`** @ ELF `0x27A050` — 2 entries

| neighbour mask | part | rot |
|---|---|---|
| `0x9` | 22 | 0 |
| `0x6` | 23 | 0 |

**`chainTableDoor`** @ ELF `0x279EB0` — 4 entries

| neighbour mask | part | rot |
|---|---|---|
| `0xE` | 9 | 0 |
| `0xD` | 10 | 0 |
| `0x7` | 11 | 0 |
| `0xB` | 12 | 0 |

**`chainTableRoad`** @ ELF `0x279EE0` — 15 entries

| neighbour mask | part | rot |
|---|---|---|
| `0x6` | 0 | 0 |
| `0x9` | 0 | 1 |
| `0x5` | 1 | 0 |
| `0x3` | 1 | 1 |
| `0xA` | 1 | 2 |
| `0xC` | 1 | 3 |
| `0x0` | 2 | 0 |
| `0x1` | 3 | 0 |
| `0x2` | 3 | 1 |
| `0x8` | 3 | 2 |
| `0x4` | 3 | 3 |
| `0x7` | 4 | 0 |
| `0xB` | 4 | 1 |
| `0xE` | 4 | 2 |
| `0xD` | 4 | 3 |

**`chainTableRoom`** @ ELF `0x279FA0` — 8 entries

| neighbour mask | part | rot |
|---|---|---|
| `0x1` | 5 | 0 |
| `0x2` | 6 | 0 |
| `0x8` | 7 | 0 |
| `0x4` | 8 | 0 |
| `0x5` | 13 | 0 |
| `0x3` | 14 | 0 |
| `0xA` | 15 | 0 |
| `0xC` | 16 | 0 |

Plus parts written **directly** by the builder, outside the chain tables:

| part | written by |
|---|---|
| 17 (`0x11`) | buildRoom — room interior |
| 30 (`0x1E`) | mapPartsFilter |
| 31 (`0x1F`) | mapPartsFilter — 次の階へ (stairs to the next floor) |
| 32 (`0x20`) | mapPartsFilter — 回復の泉 (healing spring) |
| 36 (`0x24`) | setUnderDungeonStart / GetRoomLinkInfo |
| 37 (`0x25`) | GetRoomLinkInfo — room link |
| 38 (`0x26`) | GetRoomLinkInfo — room link |
| 39 (`0x27`) | GetRoomLinkInfo — room link |
| 40 (`0x28`) | mapPartsFilter |
| 45 (`0x2D`) | mapPartsFilter |
| 46 (`0x2E`) | mapPartsFilter |
| 59 (`0x3B`) | BuildCharaSpecialParts (dungeon 0 only) |
| 68 (`0x44`) | BuildCharaSpecialParts (dungeons 3, 4, 5) — ウンガガスイッチ |
| 69 (`0x45`) | BuildCharaSpecialParts (dungeons 4, 5) |

`setStair` (0x1C7530) does not introduce new parts — it *copies* a neighbouring cell's part id.

So the parts the map builder can place, on any floor, are exactly:

```
   0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 26, 27, 30, 31, 32, 36, 37, 38, 39, 40, 45, 46, 59, 68, 69
```

## Q1 — is any tile ART unused?

**Essentially no.** Of **631** tile meshes shipped across the seven dungeons' `.mpd` packs, only **4** are
never named by an ACTIVE directive in any floor config:

| Dungeon | tile meshes shipped | never referenced |
|---|---|---|
| d01 | 102 | — |
| d02 | 116 | — |
| d03 | 97 | — |
| d04 | 88 | — |
| d05 | 75 | — |
| d06 | 90 | `d06h13_v.mds`, `d06o17_3.mds` |
| d07 | 63 | `d06o15_1.mds`, `d06o15_2.mds` |

- **`d06o17_3.mds`** — a *third* visual variant of a tile whose `_2` variant IS used. The only piece of tile
  art in the game that is shipped, complete, and simply never placed.
- **`d06h13_v.mds`** — a camera mesh for a tile that is otherwise used.
- **`d06o15_1.mds` / `d06o15_2.mds`** — D6 meshes that ride along in D7's pack (D7's config was derived from
  D6's). They ARE named in D7's config, but only in **commented-out** directives, so they never load.

No hidden rooms, no cut corridors. The tile art that shipped is, to within four meshes, the tile art used.

## Q2 — is any defined PART never placed?  **YES.**

Every writer of a grid cell has now been traced (`FindXrefs` on `buildMapDat` @ `0x1D56DA0`, plus the
functions that write the grid directly). The complete set of parts the engine can EVER place is:

```
   0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 26, 27, 30, 31, 32, 36, 37, 38, 39, 40, 45, 46, 59, 68, 69
```

Everything else a floor defines is **loaded into memory and never used**. On a 70-part floor that is
**31 parts**: `24, 25, 28, 29, 33, 34, 35, 41, 42, 43, 44, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 60, 61, 62, 63, 64, 65, 66, 67`.

Most are redundant **orientation variants** — parts are authored in families of four (one per wall
configuration), but the engine stores rotation SEPARATELY (grid `+0x9C54`) and rotates a single part, so it
only ever asks for one or two members of each family. Those are not lost content, just spare authoring.

**But some are real content.** Below, only the unreached parts whose meshes NO reachable part uses — i.e.
genuinely unique art that ships, loads, and is never placed:

**`d01main_a`** — 60 parts, 23 never placed, 16 of them unique art

| part | unique meshes | label |
|---|---|---|
| 41 | `d01o12_1.mds` |  |
| 42 | `d01o13_1.mds` |  |
| 43 | `d01o14_1.mds` |  |
| 44 | `d01o15_0.mds` |  |
| 47 | `d01h26_2.mds` |  |
| 48 | `d01h26_2.mds` |  |
| 49 | `d01h26_2.mds` |  |
| 50 | `d01h26_2.mds` |  |
| 51 | `d01h30_2.mds` |  |
| 52 | `d01h30_2.mds` |  |
| 53 | `d01h30_2.mds` |  |
| 54 | `d01h30_2.mds` |  |
| 55 | `d01o07_4.mds`, `d01u01_1.mds` |  |
| 56 | `d01o07_4.mds`, `d01u01_1.mds` |  |
| 57 | `d01o07_4.mds`, `d01u01_1.mds` |  |
| 58 | `d01o07_4.mds`, `d01u01_1.mds` |  |

**`d02main_a`** — 64 parts, 27 never placed, 23 of them unique art

| part | unique meshes | label |
|---|---|---|
| 33 | `d02o04_1.mds` |  |
| 34 | `d02o05_1.mds` |  |
| 35 | `d02o06_1.mds` |  |
| 41 | `d02o12_1.mds` |  |
| 42 | `d02o13_1.mds` |  |
| 43 | `d02o14_1.mds` |  |
| 44 | `d02o15_1.mds`, `d02o15_2.mds` |  |
| 47 | `d02h26_1.mds`, `d02h26_2.mds` |  |
| 48 | `d02h26_2.mds`, `d02h27_1.mds` |  |
| 49 | `d02h26_2.mds`, `d02h28_1.mds` |  |
| 50 | `d02h26_2.mds`, `d02h29_1.mds` |  |
| 51 | `d02h30_1.mds`, `d02h30_2.mds` |  |
| 52 | `d02h30_2.mds`, `d02h31_1.mds` |  |
| 53 | `d02h30_2.mds`, `d02h32_1.mds` |  |
| 54 | `d02h30_2.mds`, `d02h33_1.mds` |  |
| 55 | `d02o07_4.mds`, `d02u01_0.mds` |  |
| 56 | `d02o07_4.mds`, `d02u01_0.mds` |  |
| 57 | `d02o07_4.mds`, `d02u01_0.mds` |  |
| 58 | `d02o07_4.mds`, `d02u01_0.mds` |  |
| 60 | `d02o07_4.mds`, `d02u03_0.mds` | **ゴロースイッチ** |
| 61 | `d02o07_4.mds`, `d02u03_0.mds` | **ゴロースイッチ** |
| 62 | `d02o07_4.mds`, `d02u03_0.mds` | **ゴロースイッチ** |
| 63 | `d02o07_4.mds`, `d02u03_0.mds` | **ゴロースイッチ** |

**`d03main_a`** — 68 parts, 31 never placed, 24 of them unique art

| part | unique meshes | label |
|---|---|---|
| 33 | `d03o04_2.mds` |  |
| 34 | `d03o05_2.mds` |  |
| 35 | `d03o06_2.mds` |  |
| 44 | `d03o15_1.mds`, `d03o15_2.mds` |  |
| 47 | `d03h26_1.mds` |  |
| 48 | `d03h26_1.mds` |  |
| 49 | `d03h26_1.mds` |  |
| 50 | `d03h26_1.mds` |  |
| 51 | `d03h30_1.mds` |  |
| 52 | `d03h30_1.mds` |  |
| 53 | `d03h30_1.mds` |  |
| 54 | `d03h30_1.mds` |  |
| 55 | `d03o07_4.mds`, `d03u01_0.mds` |  |
| 56 | `d03o07_4.mds`, `d03u01_0.mds` |  |
| 57 | `d03o07_4.mds`, `d03u01_0.mds` |  |
| 58 | `d03o07_4.mds`, `d03u01_0.mds` |  |
| 60 | `d03o07_4.mds`, `d03u03_0.mds` | **ゴロースイッチ** |
| 61 | `d03o07_4.mds`, `d03u03_0.mds` | **ゴロースイッチ** |
| 62 | `d03o07_4.mds`, `d03u03_0.mds` | **ゴロースイッチ** |
| 63 | `d03o07_4.mds`, `d03u03_0.mds` | **ゴロースイッチ** |
| 64 | `d03o07_4.mds`, `d03u04_0.mds` | **ルビーースイッチ** |
| 65 | `d03o07_4.mds`, `d03u04_0.mds` | **ルビーースイッチ** |
| 66 | `d03o07_4.mds`, `d03u04_0.mds` | **ルビーースイッチ** |
| 67 | `d03o07_4.mds`, `d03u04_0.mds` | **ルビーースイッチ** |

**`d04main_a`** — 69 parts, 31 never placed, 21 of them unique art

| part | unique meshes | label |
|---|---|---|
| 44 | `d04o15_0.mds` |  |
| 47 | `d04h26_1.mds` |  |
| 48 | `d04h26_1.mds` |  |
| 49 | `d04h26_1.mds` |  |
| 50 | `d04h26_1.mds` |  |
| 51 | `d04h30_1.mds` |  |
| 52 | `d04h30_1.mds` |  |
| 53 | `d04h30_1.mds` |  |
| 54 | `d04h30_1.mds` |  |
| 55 | `d04o07_4.mds`, `d04u01_0.mds` |  |
| 56 | `d04o07_4.mds`, `d04u01_0.mds` |  |
| 57 | `d04o07_4.mds`, `d04u01_0.mds` |  |
| 58 | `d04o07_4.mds`, `d04u01_0.mds` |  |
| 60 | `d04o07_4.mds`, `d04u03_0.mds` | **ゴロースイッチ** |
| 61 | `d04o07_4.mds`, `d04u03_0.mds` | **ゴロースイッチ** |
| 62 | `d04o07_4.mds`, `d04u03_0.mds` | **ゴロースイッチ** |
| 63 | `d04o07_4.mds`, `d04u03_0.mds` | **ゴロースイッチ** |
| 64 | `d04o07_4.mds`, `d04u04_0.mds`, `d04u04_1.mds` | **ルビーースイッチ** |
| 65 | `d04o07_4.mds`, `d04u04_0.mds`, `d04u04_1.mds` | **ルビーースイッチ** |
| 66 | `d04o07_4.mds`, `d04u04_0.mds`, `d04u04_1.mds` | **ルビーースイッチ** |
| 67 | `d04o07_4.mds`, `d04u04_0.mds`, `d04u04_1.mds` | **ルビーースイッチ** |

**`d05main_a`** — 70 parts, 31 never placed, 20 of them unique art

| part | unique meshes | label |
|---|---|---|
| 47 | `d05h26_1.mds` |  |
| 48 | `d05h26_1.mds` |  |
| 49 | `d05h26_1.mds` |  |
| 50 | `d05h26_1.mds` |  |
| 51 | `d05h30_1.mds` |  |
| 52 | `d05h30_1.mds` |  |
| 53 | `d05h30_1.mds` |  |
| 54 | `d05h30_1.mds` |  |
| 55 | `d05o07_2.mds`, `d05u01_0.mds` |  |
| 56 | `d05o07_2.mds`, `d05u01_0.mds` |  |
| 57 | `d05o07_2.mds`, `d05u01_0.mds` |  |
| 58 | `d05o07_2.mds`, `d05u01_0.mds` |  |
| 60 | `d05o07_2.mds`, `d05u03_0.mds` | **ゴロースイッチ** |
| 61 | `d05o07_2.mds`, `d05u03_0.mds` | **ゴロースイッチ** |
| 62 | `d05o07_2.mds`, `d05u03_0.mds` | **ゴロースイッチ** |
| 63 | `d05o07_2.mds`, `d05u03_0.mds` | **ゴロースイッチ** |
| 64 | `d05o07_2.mds`, `d05u04_0.mds` | **ルビーースイッチ** |
| 65 | `d05o07_2.mds`, `d05u04_0.mds` | **ルビーースイッチ** |
| 66 | `d05o07_2.mds`, `d05u04_0.mds` | **ルビーースイッチ** |
| 67 | `d05o07_2.mds`, `d05u04_0.mds` | **ルビーースイッチ** |

**`d06main_a`** — 70 parts, 31 never placed, 21 of them unique art

| part | unique meshes | label |
|---|---|---|
| 44 | `d06o15_1.mds`, `d06o15_2.mds` |  |
| 47 | `d06h26_0.mds` |  |
| 48 | `d06h26_0.mds` |  |
| 49 | `d06h26_0.mds` |  |
| 50 | `d06h26_0.mds` |  |
| 51 | `d06h30_0.mds` |  |
| 52 | `d06h30_0.mds` |  |
| 53 | `d06h30_0.mds` |  |
| 54 | `d06h30_0.mds` |  |
| 55 | `d06o07_1.mds`, `d06u01_0.mds` |  |
| 56 | `d06o07_1.mds`, `d06u01_0.mds` |  |
| 57 | `d06o07_1.mds`, `d06u01_0.mds` |  |
| 58 | `d06o07_1.mds`, `d06u01_0.mds` |  |
| 60 | `d06o07_1.mds`, `d06u03_0.mds` | **ゴロースイッチ** |
| 61 | `d06o07_1.mds`, `d06u03_0.mds` | **ゴロースイッチ** |
| 62 | `d06o07_1.mds`, `d06u03_0.mds` | **ゴロースイッチ** |
| 63 | `d06o07_1.mds`, `d06u03_0.mds` | **ゴロースイッチ** |
| 64 | `d06o07_1.mds`, `d06u04_0.mds` | **ルビーースイッチ** |
| 65 | `d06o07_1.mds`, `d06u04_0.mds` | **ルビーースイッチ** |
| 66 | `d06o07_1.mds`, `d06u04_0.mds` | **ルビーースイッチ** |
| 67 | `d06o07_1.mds`, `d06u04_0.mds` | **ルビーースイッチ** |

### The headline: the Goro and Ruby switches were never placed

Every dungeon defines a **character-switch family** — four parts (one per orientation) per character:

| parts | mesh | label | placed? |
|---|---|---|---|
| 55–58 | `uNN01` | (unnamed) | **no** |
| 60–63 | `uNN03` | **ゴロースイッチ** — Goro switch | **NO** |
| 64–67 | `uNN04` | **ルビーースイッチ** — Ruby switch | **NO** |
| 68–69 | `uNN05` | **ウンガガスイッチ** — Ungaga switch | **yes** — dungeons 3, 4, 5 |

`BuildCharaSpecialParts` is the only thing that places a character switch, and it branches on `selectMapNo`
(the dungeon: DBC=0, Wise Owl=1, …). Exhaustively, it writes exactly three parts:

```
selectMapNo == 0  ->  part 59          (and only while a story flag is below 8)
selectMapNo == 3  ->  part 68          Ungaga switch
selectMapNo == 4  ->  parts 68, 69
selectMapNo == 5  ->  parts 68, 69
```

It never writes 60–67. **The Goro switch and the Ruby switch are fully authored — meshes, collision, all four
orientations, in nearly every dungeon — and the engine has no code path that can ever place them.** Only
Ungaga's switch shipped. Dungeon 6 also defines part 59 as **ネコ** ("cat"), which only dungeon 0 ever places.

### The other unused content

- **D1's minecart has more track pieces.** Part 40 (`トロッコ`, `d01o11_1.mds`) IS placed; parts 41–44
  (`d01o12_1`, `d01o13_1`, `d01o14_1`, `d01o15_0`) are different track meshes and are never placed.
- **Corner-entry rooms** (parts 47–54, `hNN26`/`hNN30` — the 角入り pieces) are defined in every dungeon and
  never placed. These are the same pieces that appear COMMENTED OUT in the D6/D7 configs.
- **Object rooms** (parts 33–35 in D2/D3, `oNN04`–`oNN06`) — never placed.

All of this art is in the `.mpd`, is loaded when you enter the floor, and is then never drawn. Because the
part index is just an int in the grid (`CDungeonMap + 0x9C50`), a mod can place any of them by writing the
cell — no ISO change needed.

## Q3 — content disabled in the configs

The floor configs carry **1484 commented-out part directives**. Most are dead weight, but not all:

| Floor | commented-out directives |
|---|---|
| `d01boss` | 3 |
| `d01inter` | 1 |
| `d01main_a` | 4 |
| `d01main_b` | 4 |
| `d02boss` | 56 |
| `d02inter` | 4 |
| `d02main_a` | 15 |
| `d02main_b` | 15 |
| `d03inter` | 3 |
| `d03main_a` | 25 |
| `d03main_b` | 25 |
| `d03main_c` | 25 |
| `d04inter` | 9 |
| `d04main_a` | 27 |
| `d04main_b` | 27 |
| `d05main_a` | 27 |
| `d05main_b` | 27 |
| `d06main_a` | 62 |
| `d06main_b` | 30 |
| `d06main_c` | 62 |
| `d06main_d` | 63 |
| `d07boss` | 2 |
| `d07main_a` | 162 |
| `d07main_b` | 162 |
| `d07main_c` | 162 |
| `d07main_d` | 162 |
| `d07main_e` | 162 |
| `d07main_f` | 158 |

What they actually are:

- **384 of them (64 × 6) are in the D7 configs** and reference D6 meshes (`d06h26_0`, `d06h30_0` —
  角入り, "corner entry") that **are not even in D7's packs**. D7's config was derived from D6's and these
  came along as dead copy-paste. Not restorable, and never were.
- **Genuinely cut parts whose art was also removed**: `d02g05_1` (a D2 room piece) and `d04o07_3` — the
  meshes are not in the packs, so there is nothing to restore.
- **Extra rotations of parts that ARE in the game.** The nicest example: the Divine Beast Cave has a
  **minecart** (`トロッコ`, `d01mob.mds`), live as part 77 at rotation 0 — rotations 1/2/3 (parts 78–80)
  were commented out. Same for D6's `黒い霧` ("black fog") `PT_NPC`, which is active elsewhere.

## Incidental

The `.mpd` packer leaked its build environment into the shipped data — the string **`OS=Windows_NT`** sits
in the middle of `d03main_a.mpd`. It corrupts naive filename extraction (it reads as a name prefix), which
is worth knowing before trusting any string scan of these packs.

