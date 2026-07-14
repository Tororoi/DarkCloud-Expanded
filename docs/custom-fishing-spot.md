# Creating a custom fishing spot

Research conducted 2026-07-13 via Ghidra decompilation of `SCUS_971.11` and inspection of the
`data.dat` archive.

A fishing spot is not a row in a table and not an object in the world. It is **one script command**.
Everything that makes a spot a spot — which fish live there, where you may cast, how high the water
is — is passed as arguments to a single STB call, and the engine then holds that state in a handful
of named globals. Those globals are writable at runtime, which is what makes most of this moddable
without touching the ISO.

---

## 1. The command that defines a spot

`_LOAD_FISHING_DATA` — **STB command 998**, handler `0x1969A0`.

```
_LOAD_FISHING_DATA(areaId, x1, z1, x2, z2, waterLevel, groundLevel)
```

| arg | stack slot | meaning |
|---|---|---|
| `areaId` | `GetStackInt()` | which fish table to load (**0–4 only** — see §3) |
| `x1`, `z1` | `+0x08`, `+0x10` | one corner of the fishable rectangle |
| `x2`, `z2` | `+0x18`, `+0x20` | the opposite corner |
| `waterLevel` | `+0x28` | height of the water surface |
| `groundLevel` | `+0x30` | height of the lake bed |

The rectangle is horizontal only: the handler hardcodes the box's Y extents to **±1000.0**
(`0xC47A0000` / `0x447A0000`), so the box is effectively 2D in the XZ plane.

The handler does exactly six things:

```c
areaId = GetStackInt();
FishingInit();                              // 0x1A9070 — clears all fishing state
FishingLoad(0x1d1b360, 8);                  // 0x1A87E0 — loads chara/fishing.pak
FishingLoadFish(areaId, 0x1d1b360, 0x36);   // 0x1A88F0 — spawns the fish (§3)
FishingSetRect(box);                        // 0x1A9260 — memcpy 0x20 bytes -> fishing_rect
FishingSetWaterLevel(water, ground);        // 0x1A9190 — WaterLevel / GroundLevel
PickUpPoly(CEditGround, box) -> FishingSetCPoly(polys, n);   // water-surface collision
```

`PickUpPoly` gathers the ground-collision polygons underneath the rectangle. **If it returns more
than `0x400` polys the game deliberately hangs in an infinite loop** — a dev assert that survived
into the retail build. Keep custom rectangles modest.

### The rest of the fishing command set

> ⚠ **These ids were WRONG in an earlier draft, by exactly one.** The dispatch table's 8-byte entries are
> `{handler, id}`, not `{id, handler}`. Reading them backwards shifted every command by one slot, which
> would have had a custom fishing spot calling `_LOAD_MAIN_CHARA` with seven floats. The ids below are
> confirmed twice: from the corrected table, and from the actual bytecode in Norune's script (whose
> `-13.0` / `-30.0` arguments match the `WaterLevel` / `GroundLevel` a live capture reported).

| id | command | handler | role | args |
|---|---|---|---|---|
| **998** | `_LOAD_FISHING_DATA` | `0x1969A0` | defines the spot | 7: area, x1, z1, x2, z2, water, ground |
| **997** | `_GOTO_FISHING` | `0x196B30` | starts a session: game mode `0xB`, hooks the line to the rod | 0 |
| **996** | `_INIT_FISH` | `0x196C00` | re-rolls the fish inside a box | 4: x1, z1, x2, z2 |
| **995** | `_EXIT_FISHING` | `0x196C80` | ends the session | 0 |
| **994** | `_SET_FISHING_ESA` | `0x196CB0` | sets the bait | 0 |
| 999 | `_LOAD_MAIN_CHARA` | `0x196910` | (NOT fishing — this is what 999 really is) | |

Four town scripts declare spots: `gedit\e01\event.stb` (Norune), `gedit\e02\event.stb` (Matataki +
Peanut Pond), `gedit\e04\event.stb` (Muska Lacka), `gedit\s09\event.stb` (Queens Harbor).

### How a spot is actually TRIGGERED — the type-3 event point

Confirmed live, after two wrong guesses. It is **not** an NPC (talking runs the dialogue system, never a
script) and it is **not** the town's init script (the fishing globals read zero at town load).

```
walk into a type-3 event point
  -> EdGetEvent sets matchedParam = 3, matchedPoint
  -> EdMoveChara:  if (matchedParam == 3 && point[0x1C] > 0) label = point[0x1C];
  -> ScriptLabelRequest (0x21D19708) = label
  -> the VM runs that label, which holds the _LOAD_FISHING_DATA / _GOTO_FISHING calls
```

The event-point field at `+0x1C` is **overloaded**: an item id for type 2 (searchable barrels), a
**script label** for type 3. Norune's fishing sign is a type-3 point naming label **256** — and `0x100`
is hardcoded in `EdMoveChara` as the fallback, which is why 256 turns up everywhere.

Note the *door* table dumped at town load holds only type-1 interior jumps; type-3 points are
**part-derived** and appear later (Norune's sits at part-local `(40, 0, 96)`, attached to the lake).

So a custom spot needs exactly two data writes: **a type-3 event point at the water**, and **the fishing
bytecode at whatever label it names**. No ISO, no code injection.

### The STB VM, and the exact call

12-byte instructions: `{u32 op, u32 a1, u32 a2}`.

| op | meaning |
|---|---|
| 3 | PUSH literal. `a1` = type (**1 = int, 2 = float (IEEE bits), 3 = string offset**), `a2` = value |
| 1 | LOAD var `a1` |
| 2 | STORE var `a1` |
| 11 | negate |
| 21 | **EXT** — call. `a1` = **stack entry count, INCLUDING the command id**. The id is the FIRST entry. |
| 15 | RET |
| 23 | YIELD |

Norune's real call, verbatim (label 256, `+0x0E8E0`):

```
op3  type=1  998           ; _LOAD_FISHING_DATA
op3  type=1  0             ; area 0
op1  var5 / var6 / var3 / var4    ; the four rect corners
op3  type=2  13.0 ; op11   ; -> WaterLevel  = -13
op3  type=2  30.0 ; op11   ; -> GroundLevel = -30
op21 argc=8                ; 1 cid + 7 args
```

and to start the session:

```
op3  type=1  997           ; _GOTO_FISHING
op21 argc=1
```

A custom spot can push the floats directly (negative literals need no `op11`). The live Norune capture
that confirms all of this:

```
fishing_rect  (-700.14, -600.12) -> (-500.14, -400.12)   [200 x 200]
WaterLevel -13   GroundLevel -30   cpoly 197/1024   FishNum 4 (area 0)
Fish 0x014798D0  == FishSlotOffsets.AreaBase_Norune
```

---

## 2. The live globals — the RAM levers

A spot's entire definition is mirrored into named ELF globals once the command runs. This is the
important part: **you do not have to patch the script to change a spot; you can write these.**

| global | guest | MMU | type |
|---|---|---|---|
| `fishing_rect` | `0x01D549D0` | `0x21D549D0` | `CBoxVu0`, 32 bytes — the castable rectangle |
| `fish_rect` | `0x01D549F0` | `0x21D549F0` | `CBoxVu0`, 32 bytes — the box the fish swim inside |
| `WaterLevel` | `0x002A2B28` | `0x202A2B28` | float |
| `GroundLevel` | `0x002A2B2C` | `0x202A2B2C` | float |
| `UkiGroundLevel` | `0x002A2B30` | `0x202A2B30` | float — bobber rest height |
| `Fish` | `0x002A2B58` | `0x202A2B58` | `CFish*` — array base, stride `0x2410` |
| `FishNum` | `0x002A2B64` | `0x202A2B64` | int — live fish count (4 or 5) |
| `AngleFish` | `0x002A2B5C` | `0x202A2B5C` | `CFish*` — the fish currently on the hook |
| `BattleFish` | `0x002A2B60` | `0x202A2B60` | `CFish*` — the fish being reeled |
| `cpoly` | `0x002A2B68` | `0x202A2B68` | `CCPoly*` — water-surface polys |
| `cpoly_num` | `0x002A2B6C` | `0x202A2B6C` | int — poly count |
| `draw_under_water` | `0x002A1FA0` | `0x202A1FA0` | int — 0 disables the underwater view |
| `esa_info` | `0x0026AE90` | `0x2026AE90` | 13 × `{u32 itemId, f32 noticeRadius}` — the bait table |

`esa_info` is the same table the mod already knows as `BaitDetectionRadiusTable`; the mod's base is
4 bytes lower because it points at the radius field of each pair.

### `CBoxVu0` layout (32 bytes)

```
+0x00  float  cornerA.x
+0x04  float  +1000.0        (hardcoded by the handler)
+0x08  float  cornerA.z
+0x0C  float  w
+0x10  float  cornerB.x
+0x14  float  -1000.0        (hardcoded)
+0x18  float  cornerB.z
+0x1C  float  w
```

Fish are seeded at the rectangle's centre, twelve units below the surface:
`((x1+x2)/2, WaterLevel - 12.0, (z1+z2)/2)`.

---

## 3. The five areas and their fish

`FishingLoadFish` (`0x1A88F0`) is a plain `if/else if` chain on the area id. There is **no default
branch**: `0x1A8A48` loads `s3 = 65535` (i.e. species `-1`) before the chain, and an area id outside
0–4 falls straight through with that value. So **a sixth area id spawns nothing at all.** The area
dispatch itself is the five `beq` at `0x1A8A58`–`0x1A8A84`.

Six `CFish` objects are allocated but only `FishNum` are used — 4 for areas 0 and 4, 5 otherwise.

| area | fish | rolled by |
|---|---|---|
| 0 — Norune Pond | Gobbler(1), Nonky(2), Gummy(6), Niler(7) | `rnd()` scaled to 0–3 |
| 1 — Peanut Pond | Gobbler(1) 35%, BakuBaku(4) 35%, Umadakara(9) 10%, Tarton(10) 20% | `rnd()*100` |
| 2 — Matataki Waterfall | Nonky(2), BakuBaku(4), Gummy(6) — even thirds | `rand()%3` |
| 3 — Queens Harbor | Bobo(0) 20%, Kaiji(3) 20%, Piccoly(11) 20%, Bon(12) 20%, Hamahama(13) 20% | `rand()%100` |
| 4 — Muska Lacka Oasis | Negie(14) 40%, Den(15) 30%, Heela(16) 30% | `rand()%100` |

**Areas 2 and 4 additionally roll for rares** before the normal table: `rand() % N == 0` yields
Mardan Garayan(5), and one in five of *those* is upgraded to Baron Garayan(17).

`N` is set by the time of day (`EdGetTime`) — **smaller means rarer fish appear more often**:

| time | N | rare chance |
|---|---|---|
| 1 | 20 | best |
| 3 | 25 | |
| default | 30 | |
| 2 | 35 | |
| 0 | 50 | worst |

Area 3 also sets `draw_under_water = 0`, which is why Queens Harbor has no underwater camera.

### Species ids are patchable immediates

Each fish is chosen by an `li s3, N` instruction — a 16-bit immediate you can rewrite in place:

| area | sites |
|---|---|
| 0 | `0x1A8AE8` → 6, `0x1A8AF4` → 7 (the 1 and 2 branches use a different encoding — disassemble before patching) |
| 1 | `0x1A8B2C` → 1, `0x1A8B44` → 4, `0x1A8B5C` → 9, `0x1A8B74` → 10 |
| 2 | `0x1A8BF0` → 2, `0x1A8C00` → 4, `0x1A8C10` → 6; rares `0x1A8BA4` → 5, `0x1A8BC0` → 17 |
| 3 | `0x1A8C6C` → 11, `0x1A8C84` → 12, `0x1A8C90` → 13 (Bobo/Kaiji use a different encoding) |
| 4 | `0x1A8D10` → 14, `0x1A8D28` → 15, `0x1A8D40` → 16; rares `0x1A8CC0` → 5, `0x1A8CDC` → 17 |

The percentage thresholds (`slti` against 0x14 / 0x23 / 0x28 / 0x3C / 0x46 / 0x50) are immediates in
the same range and can be retuned the same way. This is an in-place EE-code edit of existing
instructions, so it must land in the cold window before a fishing session loads — the same discipline
the Macho ABS display patches use.

---

## 4. The pond *is* a Georama part — and so is the sign

This is the crux, and it is much better news than it first appears.

The water you fish in is **not** baked into the town mesh. It belongs to a Georama part, and
`CEditGround` — the Georama ground — is what renders and collides it:

```
CEditGround::DrawWater          0x1A3620
CEditGround::DrawWaterSurface   0x1A3360
CEditGround::StepWater          0x1A3150
CEditGround::PickUpPoly         0x1A4F50   <-- the very function _LOAD_FISHING_DATA calls
```

So the polys a fishing rectangle picks up come from **the placed Georama parts**, not from static
level geometry. Move the part and the fishable water moves with it.

### The `m` parts

Each town's Georama models live in `gedit\<town>\scene.scn`, named `<town><letter><nn>_<variant>.mds`
— `h` house, `t` tree, `r` river, `g` ground, `c`/`w`/`a` scenery. The fishing parts are the
**`m` prefix**, and the count matches the fishing spots exactly:

| town | `m` parts | fishing spots |
|---|---|---|
| e01 Norune | `e01m01` | 1 — the pond |
| e02 Matataki | `e02m01`, `e02m02` | 2 — the waterfall and Peanut Pond |
| e04 Muska Lacka | `e04m01` | 1 — the oasis |
| e03 (no fishing) | **none** | 0 |
| s09 Queens Harbor | none — the harbour is fixed geometry | 1 |

Variants are the usual Georama set: `_0/_1/_2` build levels, `_a`, `_c` collision, `_r`, `_k`, `_s`.
`e01m01_r.mds` is the only one carrying real geometry (~40 KB, texture `e01b15`) — this is Norune's
pond, and **the fishing sign is modelled into it**. Matataki's two signs are its two `m` parts.

### `CEditGround` and `CMapParts`

```
CEditGround + 0x04 .. 0x10   CEditArea* [4]              the base ground areas
CEditGround + 0x30           CMapParts placed[128]       stride 0x2A0  <-- the placed parts
CEditGround + 0x15030        CEditPartsInfo*             stock/limit per part type
CEditGround + 0x15F30        CMapParts templates[24]     stride 0x2A0  <-- one per part type
CEditGround + 0x15F40        CMapParts extra[64]         stride 0x2A0  (fixed scenery objects)
```

A town therefore has **at most 24 placeable part types** and **128 placed instances**.

`CMapParts` is **0x2A0 bytes**:

| offset | meaning |
|---|---|
| `+0x10` | **position** (x, y, z) — what `SetPosition` writes |
| `+0xA0` | vtable |
| `+0xE8` | occupancy / kind — **`< 0` means the slot is free** |
| `+0xF0` | part id |
| `+0xF4` | area code |
| `+0x108` | model/data pointer (must be non-zero to place) |
| `+0x118` | **part type** — see the water table below |
| `+0x148` | state |
| `+0x1D0` | unit size |

```
CEditGround::SetMapParts(int partId, float x, float y, float z, int rot)   0x1A0470
CEditGround::PickUpPoly(CCPoly*, CBoxVu0, int)                             0x1A4F50
CEditGround::DrawWater(int)                                                0x1A3620
CMapParts::SetPosition(float, float, float)                                0x19A810
CMapParts::GetPosition(float*)                                             0x19A7B0
CMapParts::SetRotY(int)                                                    0x19A850
SetObjHandle(int, CMapParts*, char*)                                       0x18A470
```

**`SetMapParts` is essentially `memcpy(slot, template, 0x2A0)` followed by a position and a
rotation.** Placement is pure data — which is exactly the shape the mod needs. It cannot *call*
`SetMapParts` (injected calls crash the recompiler), but it can copy a template into a free slot
(`+0xE8 < 0`) and write `+0x10`. Parts also get script object handles, which is why the STB can
address them (`_GET_OBJHDL` 1024, `_SET_OBJHDL_POS` 1030, `_GET_OBJHDL_POS` 1033).

### Where a part's type comes from

Each town's parts are declared in **`gedit\<town>\mapinfo.cfg`**, one directive per part, with the
original Japanese comments intact:

```
//PARTS 12 //池                                    <- "ike" = pond
LAKE_PARTS  12, "e01m01.pts", 0, 1.0, "e01m01_r.mds"
```

The parser (`CommandLAKE_PARTS` @ `0x1765A0` and siblings) writes each directive into a **0x2D8-byte
part record** at `edit_info + partId*0x2D8 + 0xDD10`. The record begins with the part's **name**
(looked up in the town's `scene.scn` by `SearchPTS`), and **`+0x244` is the type**. `LoadObjectParts`
(`0x182390`) then copies `+0x244` straight into `CMapParts+0x118`.

**The directive keyword *is* the type**, read out of the seven `Command*_PARTS` handlers:

| directive | type | `DrawWater` renders it? |
|---|---|---|
| `BLD_PARTS` | **0** | no — buildings |
| `GRD_PARTS` | **0** | no — ground scenery (trees, plateaus) |
| `ROAD_PARTS` | **1** | no — auto-chained by `SetRoadParts` |
| `RIVER_PARTS` | **2** | **yes** — auto-chained by `SetRiverParts` |
| `BRIDGE_PARTS` | **3** | **yes** |
| `LAKE_PARTS` | **4** | **yes** — the 2×2 water patch |
| `ON_RIVER_PARTS` | **5** | **yes** — things that sit on water (water-wheel huts) |

> **Naming trap:** the `r##` models are **roads** (道), *not* rivers. River/lake water meshes are the
> `w##` models. Reading `r` as "river" gives exactly the wrong answer about which towns have water.

### You do not actually need a water part to fish

This is the most useful thing in this document. The three ingredients of a spot are **independent**:

| ingredient | where it comes from | needs a water part? |
|---|---|---|
| the castable rectangle | `fishing_rect` (script/global) | no |
| the surface height | `WaterLevel` (a float) | no |
| collision — bed, banks | `PickUpPoly` | **no** |
| the *visible* liquid | `DrawWater` **or** any mesh already in the map | no |

`PickUpPoly` is not water-aware. With the flag `_LOAD_FISHING_DATA` passes (0) it gathers the
collision frame of **every** part overlapping the box, plus the 64 extra objects, **plus the base
`CEditArea` ground** (`PickUpPoly__9CEditArea`). Static terrain counts.

So fishing works over **any liquid the map already draws** — you set the rectangle over it and set
`WaterLevel` to its surface. `DrawWater` exists only to render Georama-placed water; if the liquid
is already in the town's mesh, it is redundant.

---

## 5. The *other* water system — static `WATER` meshes

Georama parts are only half the story, and the smaller half. `mapinfo.cfg` also declares **static
water**, entirely outside the part system:

```
WATER_IMG      "e01b02.img", 0            // water textures
WATER          "e03c08"                   // a static water mesh   //水路の水  = "canal water"
WATER_SURFACE  "mizu", 16, 16             // attach an animated ripple grid to the frame named "mizu"
WATER_SHAKE    -1, -1, -0.2, 0.0          // ripple parameters
```

`WATER` registers a mesh into `CEditGround`'s **64 static-object slots** (`+0x15F40`, the second loop
in `PickUpPoly`). `WATER_SURFACE "<frame>", w, h` attaches an animated rippling grid to a **named
frame** inside a mesh; `WATER_SHAKE` tunes it. This is how most of the game's water is built — and it
is fully collidable and fully picked up by `PickUpPoly`.

| town | static `WATER` meshes | comment in the cfg |
|---|---|---|
| Norune e01 | none | pond = LAKE part; river = RIVER part |
| **Matataki e02** | `e02c10`, `e02c11`, `e02c13`, `e02c14` | モデルデータ |
| **Queens e03** | `e03c08`, `e03c01`, `e03c02` | **水路の水 = "canal water"** |
| Muska Lacka e04 | none | oasis = LAKE part |
| **Moon Factory e05** | **none — no `WATER`, no `WATER_IMG`, no `WATER_SURFACE`** | — |
| Brownboo s04 | `s04w01` (水面), `s04w02` (波 waves), + others | the `WATER` list also carries stairs — it is really "static object" |
| Queens Harbor s09 | none listed; `WATER_SURFACE` only | water is in the `s0901/2/3` meshes |

Inside those meshes the animated surfaces are named frames:

- **Queens** — `e03c08_0.mds` carries the frame **`mizu__a01`** (the canal surface);
  `e03c01_0.mds` carries **`taki1`/`taki2`** (滝 = *waterfall*); `e03c02_0.mds` carries `obj48__a01z*`.
  So Queens' canal runs right through the city, is animated, and is **not Georama at all**.
- **Matataki** — `e02c10/11/13/14_0.mds` reference the water meshes `e02w10_0` … `e02w14_0`.

## Where the fishing signs actually are

The signboard frame is called **`kanban`** (看板). Most `kanban` frames are shop signs on houses, but
the ones sitting in *water* meshes are the fishing signs:

| town | model carrying `kanban` | what it is |
|---|---|---|
| **Matataki e02** | **`e02w11_0.mds`** — `kanban`, `kanban1`, `kanban2` | the fishing signs — inside a **static** water mesh, not Georama |
| **Muska Lacka e04** | **`e04w01_0.mds`** — `kanban`, `kanban_1`, `kanban_1_1` | the oasis sign — this **is** the LAKE part's water mesh, so it moves with the part |
| **Queens Harbor s09** | `s0901_c.mds` — 5 `kanban` frames | baked into the harbour's static mesh |
| Norune e01 | *none* — the pond mesh `e01m01_r.mds` has frames `ike` (池), `item00`, `func_item00` | its sign is unnamed geometry inside the lake part |
| Queens e03 | only house shop signs + `e03h11_w.mds` (the fountain) | no fishing sign |
| Moon Factory e05 | **zero `kanban` frames** | no sign anywhere |

So the sign is **movable only in Norune and Muska Lacka** (it rides the LAKE part). Matataki's two
signs and the harbour's are inside static meshes and cannot be repositioned.

---

## 5b. `WATER_SURFACE` — a writable runtime table, and the way into the Moon Factory

`CommandWATER_SURFACE` (`0x1757E0`) parses the cfg into a **runtime table of at most 8 entries**:

```
entry i  =  *edit_info + 0x17CD0 + i*0xC0          (edit_info is a POINTER @ 0x202A27B0)
water_list @ 0x202A27D8   = how many entries are in use (max 8)
```

The cfg syntax and the entry layout line up one-for-one:

```
WATER_SURFACE "mizu", 16, 16,          // frame name, grid W, grid H
    -57.5, 0, -58.5,                   // corner A
     57.5, 0,  64.5,                   // corner B
     0,    5.6, 0,                     // offset — Y IS THE SURFACE HEIGHT
     0.08, 0.012, 0.1, 1.0,            // wave amp, freq, flow speed, scale
     80, 100, 128,                     // R, G, B   <-- the water is TINTED
     0, 0, 0                           // flags
WATER_SHAKE  -1, -1, -0.2, 0.0         // ripple (-1,-1 = whole surface)
```

| entry offset | field |
|---|---|
| `+0x00` | frame name (16 bytes, `strcpy`) — `""` means "not bound to a frame" |
| `+0x10`, `+0x14` | grid width, height |
| `+0x18` | owning part number (`now_parts_no`) |
| `+0x20`, `+0x24`, `+0x28` | corner A (x, y, z), `+0x2C` = 1.0 |
| `+0x30`, `+0x34`, `+0x38` | corner B, `+0x3C` = 1.0 |
| `+0x40`, `+0x44`, `+0x48` | offset — **`+0x44` is the surface height** — `+0x4C` = 1.0 |
| `+0x50`, `+0x54`, `+0x58` | **R, G, B** |
| `+0x60` … `+0x6C` | wave amplitude, frequency, flow speed, scale |
| `+0x70` … `+0x78` | flags |

Real values, straight from the ship configs:

- **Queens canal** — grid 48×16, corners `(-320,0,-70) → (320,0,70)`, offset `(0, 31, 0)`. **The canal
  surface sits at Y = 31.** Colour `128,128,128`.
- **Queens fountain** — frame `mizu`, 16×16, offset `(0, 5.6, 0)`, colour `80,100,128`.
- **Matataki** — 32×32 at offset `(0,-13,0)`, and a second 32×20 at world `(860, -14, 1170)`.

Two things fall out of this:

1. **The table is data, and it is writable.** Adding a water surface is a field write plus bumping
   `water_list` — no code injection. The Moon Factory uses **zero** of its 8 slots.
2. **Water surfaces carry an RGB tint.** A *yellow* water surface is a colour, not a new asset.

### The Moon Factory caveat

e05 declares **no `WATER_IMG`** at all, so the water textures the surface renderer samples
(`e01b02.img` / `e01b03.img`, shared by e01/e02/e03) are **not loaded in that town**. A surface
written into e05's table may therefore render untextured or with garbage. This is the one thing that
needs live testing before the plan is sound; a fishing spot there works regardless (§4), but the
animated surface may not.

The Moon Factory's only animated frame is **`moyou__a01z`** (模様 = "pattern") in `e05g02_c.mds`, plus
`moyou01/3__cfzappa01` in `e05g06_c.mds`. There is **no** frame named for liquid anywhere in e05 — no
`mizu`, `taki`, `ike`, `eki` or `abura`. So the yellow liquid is unnamed terrain geometry; `moyou` is
the best candidate for it but is **not confirmed**.

### Related STB commands

| id | command | handler |
|---|---|---|
| 507 | `_DRAW_EDIT_WATER` | `0x193280` |
| 508 | `_DRAW_WATER_SURFACE` | `0x1932E0` |

---

## 6. Town by town — summary

Two different "is it Georama?" questions get confused easily, so they are separate columns here: whether
the **town** is Georama-*editable* (declares `EDITAREA` + `*_PARTS`), and whether its **water** happens
to be a Georama part rather than static map geometry.

| town | code | town editable? | water | water is a Georama part? | sign | fishing today |
|---|---|---|---|---|---|---|
| Norune | e01 | **yes** | LAKE part 12 (`e01m01`) + RIVER part | **yes** | in the lake part | area 0 |
| Matataki | e02 | **yes** | static `WATER` ×4 | no | static (`e02w11_0`) | areas 1, 2 |
| Queens | e03 | **yes** | static `WATER` ×3 — **the canal** | no | none | — |
| Muska Lacka | e04 | **yes** | LAKE part 11 (`e04m01`) | **yes** | in the lake part (`e04w01_0`) | area 4 |
| Moon Factory | e05 | **yes** — 14 × `BLD_PARTS`, 1 `EDITAREA` | **no water system at all** | n/a | none | — |
| Brownboo | s04 | **NO** — no `EDITAREA`, no parts; a fixed `GROUND` list | static `WATER` (`s04w01`/`s04w02`) | no | none | — |
| Queens Harbor | s09 | **NO** — 7 static models | static, in the map mesh | no | baked | area 3 |

### Town ↔ folder identification — GET THIS RIGHT

This is easy to get wrong and it poisons everything downstream. **An earlier draft of this document
had `e05` labelled "Yellow Drops". It is not — `e05` is the MOON FACTORY.** Dialogue alone is
misleading, because NPCs in the factory talk *about* Yellow Drops. The decisive evidence is the BGM
comment in each `mapinfo.cfg`:

| folder | `BGM_NO` comment | Georama? | town |
|---|---|---|---|
| e01 | — | yes | Norune |
| e02 | マタタギの里テーマ | yes | Matataki |
| e03 | クイーンズテーマ | yes | Queens |
| e04 | — | yes | Muska Lacka |
| **e05** | **ファクトリーテーマ** ("Factory Theme") | **yes** — 14 parts, 1 EDITAREA | **Moon Factory** |
| **s13** | **イエロードロップテーマ** ("Yellow Drop Theme") | **NO** — 0 EDITAREA | **Yellow Drops** |
| s04 | — | **NO** — 0 EDITAREA | Brownboo |

The Moon Factory is the Georama area **for** the Yellow Drops chapter; Yellow Drops itself (`s13`) is a
separate, non-editable map. `s04talk_1.mes` #51 — *"Yellow Drops is the real home of the Brownboo
people"* — is Brownboo NPCs talking about a place they are not in.

> ⚠ **Consequence: every "Yellow Drops" finding in this document's water / liquid / sign research is
> actually about the MOON FACTORY (`e05`).** The real Yellow Drops (`s13`) has **not** been analysed —
> its water, its liquid, its `kanban` frames and its texture banks are all still unknown. Do not carry
> the `e05` conclusions across.

Note the mod's `TownCharacter.currentArea` uses a **different id space again** (Norune 0, Matataki 1,
Queens 2, Muska Lacka 3, Brownboo 14, Yellow Drops 23, Dark Heaven 38). It is not the gedit folder and
it is not `MapNo`.

### The real Yellow Drops (`s13`)

`BGM_NO 15 //イエロードロップテーマ`, `SOUND_SET 23`, `TIME_STOP`. **Not Georama** — no `EDITAREA`,
no `*_PARTS`. Its map is a fixed `GROUND` list (`s1301`–`s1317`), 14 `PEOPLE2` villagers, and one
`MOTION_PARTS "pat.chr"`.

**It has a real animated liquid surface** — and unlike the Moon Factory, this one is genuine:

```
WATER_SURFACE "", 48, 48,
    -320, 0, -320,          // corner A
     320, 0,  320,          // corner B
     0,   1,  0,            // offset -- SURFACE HEIGHT = Y 1.0
     0.1, 0.015, 0.0, 2.0,  // wave: amp, freq, flow, scale
     128, 128, 128,         // RGB (neutral)
     1, 0, 1
WATER_SHAKE -1, -1, -0.25, 0.0
```

The surface frame is **`suimenn__a01cfz`** — 水面, literally *"water surface"*. So the liquid is a
first-class, animated `WATER_SURFACE` spanning **640 × 640 units centred on the origin, at Y = 1.0**.

**That makes a fishing spot here straightforward:** a rectangle anywhere inside
`(-320, -320) → (320, 320)` with `WaterLevel = 1.0`. No Georama, no new asset, no water part —
exactly the §4 case (rectangle + water level + whatever collision `PickUpPoly` finds under it).

Other s13 facts that matter:

| | |
|---|---|
| `kanban` frames | **zero** — no sign geometry anywhere |
| texture banks | **one**: `s1301.img`. No `WATER_IMG` at all. |
| other animated frames | `revol1..4__a01zcfapp` (machinery), `naka1/2`, `bou`, `eda`, `waku` |
| villagers | **14 × `PEOPLE2`** — close to the 16 cap, and past the 10-villager `EdInitVillagerOnOff` loop |

That last row matters for the sign plan (§8): Yellow Drops has little room left in the villager table.

---

## 6. What still needs the ISO

- **An eighteenth fish.** Species 8 (`f09a`) has no model, stub stats and zero bait affinities.
- **A fishing sign in a town that has no `LAKE_PARTS`** (e02, e03, e05, s04). The sign is geometry
  inside the lake model, and a town can only instance part types its own `scene.scn` provides.

That is the whole list. **Water is no longer a blocker anywhere**, because a rectangle plus a
`WaterLevel` over existing liquid geometry is enough (§4).

### One ordering trap

`cpoly` is captured **once**, when `_LOAD_FISHING_DATA` runs. If you relocate a part you must also
retarget the rectangle, and the part must be in place *before* the command executes — otherwise
`PickUpPoly` gathers polys from wherever the water used to be.

---

## 7. Recipe: a custom fishing spot, ISO untouched

**In a town that has an `m` part** (Norune, Matataki, Muska Lacka) — the easy case:

1. Position the `m` part where you want the spot. Water, collision and sign travel with it.
2. Retarget the rectangle: patch the script's pushed constants, or let the vanilla
   `_LOAD_FISHING_DATA` run and overwrite `fishing_rect` / `fish_rect` / `WaterLevel` /
   `GroundLevel` (§2). The part must be placed *before* the command runs so `cpoly` is correct.
3. Choose the fish: reuse a stock area id, or patch the `li s3` immediates of the table you borrow
   (§3) in the cold window.

**In Queens (e03) — the canal.** This is the easiest of the three targets. `WATER "e03c01/c02/c08"`
is real, animated, collidable water running through the middle of the city, and it is *already
there* — nothing to place:

1. Pick a rectangle over the canal.
2. Add a `_LOAD_FISHING_DATA` call: new VM instructions in `gedit\e03\event.stb` in RAM, over dead
   space, reached by hijacking an existing event point. Matataki proves two spots per town work.
3. Set `WaterLevel` to the canal surface, `GroundLevel` to its bed. `PickUpPoly` already picks the
   canal meshes up via the 64 static-object slots — no Georama part needed (§4).
4. There is no sign, so mark it with something already loaded (see below).

**In Brownboo (s04).** Same shape: static `WATER "s04w01"` (水面) and `"s04w02"` (波, waves) are
already in the map. Rectangle + `WaterLevel` and it works.

**In Moon Factory (e05).** It has **no water system whatsoever** — no `WATER`, no `WATER_IMG`, no
`WATER_SURFACE`, zero `kanban`. Its yellow liquid is unnamed terrain geometry in the `e05g02`–`e05g08`
ground meshes.

Fishing still works there, because a spot only needs a rectangle, a `WaterLevel` and collision polys
(§4) — all of which the existing terrain provides. And you can go further: **write a `WATER_SURFACE`
entry into e05's empty table** (§5b) to give the liquid a real animated, rippling surface, tinted
yellow via its RGB field. All 8 slots are free and the whole thing is a field write.

Steps:

1. Find the liquid's surface height (see §8 — easiest empirically, by standing at its edge and
   reading the player's Y).
2. Write water-surface entry 0 at `*edit_info + 0x17CD0`: corners around the pool, `+0x44` = the
   surface height, RGB = a yellow, then set `water_list` (`0x202A27D8`) to 1.
3. Add the `_LOAD_FISHING_DATA` call with the rectangle over the pool and `WaterLevel` at the same
   height.
4. Set `draw_under_water = 0` — the stock underwater view will look absurd in a chemical pool.

**Caveat:** e05 loads no `WATER_IMG`, so the surface may render untextured. Needs live testing.

**Marking a spot.** Use something the town already loads — `kira.chr` sparkles, `fly_light.chr`, or a
repositioned town prop (`TownCharacter.cs` already places town objects at arbitrary coordinates).

---

## 8. Rendering a fishing sign in a town that has none

### The lever: per-frame visibility

`FrameObjectOnOff` (`0x157590`) resolves a frame by name with `SearchFrame__6CFrame` and writes a
**u16 visibility flag at `CFrame+0xB0`**. Together with `CFrame::SetPosition` (`0x127E80`) and
`CFrame::SetScale` (`0x127F50`), that means **any named frame in any loaded model can be shown,
hidden, moved and resized with plain field writes** — the same idiom the mod already uses on weapon
models.

### What signboard geometry each town actually has

The signboard frame is `kanban`.

| town | `kanban` frames present | other marker geometry |
|---|---|---|
| Queens e03 | **22 × `kanban`** + `kanban01/02/1`, and 3 × `mark` | `taimatu` (torch), `hasira` (pillar), `taru`, `tubo`, `ranpu` |
| Brownboo s04 | **none** | only `light00`–`light05` |
| Moon Factory e05 | **none** | `tubo` (pot ×12), `hasigo` (ladder), `akari` (lamp), `kabe` |

**Queens can therefore have a real sign with no new assets.** The other two cannot — *unless* you use
the indirection route below.

### Strategy A — promote a spare sign (Queens, cheapest)

Houses carry `kanban`, `kanban01` **and** `kanban02` — build-level variants, so one or two are hidden
at any moment. Turn a hidden one on (`+0xB0`) and translate its `CFrame` to the canal's edge. The
house keeps its visible sign.

*Caveat:* the frame stays a child of the house's tree, so its transform is house-relative and it is
culled/LOD'd with the house. Pick a canal-side house and keep the offset small.

### Strategy B — instance + frame mask (Queens, cleanest)

Place a second copy of a `kanban`-bearing house into a free static-object slot (the 64 at
`CEditGround + 0x15F40`), then hide **every** frame except `kanban`. A free-floating signboard,
positionable anywhere, with no house involved and no culling coupling.

### Where the *fishing* sign actually lives

Not every `kanban` is the fishing sign — most are shop signs. The fishing sign, frame by frame:

| model | root | size | named frames |
|---|---|---|---|
| **Muska Lacka oasis** (`e04m01`) | `oasisu` | 130,560 B | `oasisu`, `oasisu__a7f` (animated water), `tree1/2/3`, `kusa`, `ha1`, `func_item00`–`04`, **`kanban`** ← the fishing sign, **separable** |
| **Norune pond** (`e01m01`) | `ike` | 25,072 B | **only** `ike` and `func_item00` — its sign is **welded into the pond mesh, not separable** |

So the natural plan — *copy Norune's pond part and strip it down to the sign* — **cannot work**, because
Norune's sign is not a separate frame. **Muska Lacka's oasis is the one to harvest**: its `kanban` is a
real, isolable frame.

### The hard blocker: models resolve out of the town's OWN scene

`SearchPTS(scn_data, name)` resolves every part model by name, and `scn_data` (`0x202A29D4`) points at
exactly one town's `scene.scn` buffer. Queens' scene contains no `e01m01` or `e04m01`, two towns'
scenes are **never resident together**, and **neither pond model exists as a loose file** (there is no
`*m01*.mds` anywhere outside a `scene.scn`).

**Therefore no pointer indirection can bring the pond or oasis model into Queens.** The part record is
easy data; the geometry simply is not in the address space. This is the wall, and it is a hard one.

### Strategy C — pointer indirection with a loose model

Indirection *does* work — but only for models that exist as **loose files**. Two such models carry a
`kanban` (the windmill's shop sign, not the fishing sign):

```
opdat\norn\e01h07_0.mds     140,960 B    frames: hontai, kanban, taimatu, func_drr00, func_mapj00
opdat\norn2\e01h07_0.mds    140,960 B    (the same model)
```

They are the opening-cutscene builds of Norune's windmill (ドラン風車), which has a signboard.

And **every** Georama town loads three models by a *hardcoded path string* in the ELF, via
`LoadGroundData` (`0x1815E0`):

```
EdLoadFile(0x29AFD0)  ->  LoadMDSFile(..., CDataAlloc2 @ 0x1D3A050)   // "gedit/e01/mds/e01a03_0.mds"
EdLoadFile(0x29AFF0)  ->  LoadMDSFile(..., CDataAlloc2 @ 0x1D3A050)   // "gedit/e01/mds/e01a04_0.mds"
EdLoadFile(0x29B010)  ->  LoadMDSFile(..., CDataAlloc2 @ 0x1D3A050)   // "gedit/e01/mds/e01a05_0.mds"
```

Those strings sit in **32-byte slots in writable RAM** and each holds a 26-character path.
`"opdat/norn/e01h07_0.mds"` is **23 characters — it fits in place, no relocation.**

So: rewrite one of those strings (or alias the file via the NAME_TREE, which is the cleaner
mechanism and already proven in this project), let the town load the windmill, then hide every frame
except `kanban` and position it at the water. **A genuine Dark Cloud signboard, in any town, with no
ISO change.**

**Three real risks, all needing live testing:**

1. **Allocation.** 624 B → 140,960 B out of the shared `CDataAlloc2` arena at `0x1D3A050`. It may not
   fit. This is the most likely failure.
2. **Textures.** The windmill references `HBe01t10`–`HBe01t34`, which live in **Norune's** `img.pak`.
   In Queens, Brownboo and the Moon Factory those are not resident, so the sign will probably render
   untextured. This is the biggest cosmetic risk.
3. **Collateral.** Whatever `e01a03/04/05` normally do (Georama edit-mode markers) breaks in that
   town. Pick the least load-bearing of the three.

### Strategy C′ — harvest the real fishing sign (DONE: the model surgery)

#### The `.mds` format (verified against `LoadMDSFile` @ `0x1262B0`)

```
header (0x10)
  +0x00  char[4]  "MDS\0"
  +0x04  u32      version (1)
  +0x08  u32      nodeCount            <- the loader reads this at +8
  +0x0C  u32      nodeTableOffset      <- 0x10

node (0x70 each)
  +0x00  u32      unknown (0)
  +0x04  u32      unknown (0x70 = the node stride)
  +0x08  char[32] name                 <- strcpy'd to CFrame+0x118
  +0x28  u32      meshOffset           <- ABSOLUTE file offset of the "MDT" block; 0 = no mesh
  +0x2C  s32      parent               <- node index, -1 = root
  +0x30  float    matrix[4][4]         <- the loader transposes it into the CFrame

mesh ("MDT" block)
  +0x00  char[4]  "MDT\0"
  +0x04  u32      headerSize (0x40)
  +0x08  u32      totalSize            <- self-delimiting
  ...    VU1 packet, verts, UVs, colours, trailing "HB<texture>" name
```

`meshOffset` is absolute, so it is the **only** fixup a rebuild needs — nothing inside an MDT block
points outside itself. That is what makes this surgery clean.

Tool: **`tools/mds_surgery.py`** (`list` / `extract`).

#### The oasis, dissected

`gedit\e04\scene.scn`, model root `oasisu`, 130,560 B, **19 nodes**:

```
 idx parent    mesh  tex       name
   0     -1    2208            oasisu              <- the basin
   1      0   14080  e04b07      oasisu__a7f       <- the animated water
   2      0    5664              tree1__s          <- palms, fronds, grass (nodes 2-12)
  ...
  13-17   0       0             func_item00..04    <- empty attach points
  18      0    2032  e01b24    kanban              <- THE FISHING SIGN
```

`kanban` is **node 18, parent 0, no children, a self-contained 2,032-byte mesh.** Its local matrix
is a yaw of ~48.7° plus a translation of (90, 2, 64) — its position within the oasis.

#### The result

```
python3 tools/mds_surgery.py oasis.mds extract kanban --recenter --unrotate -o fishsign.mds
```

**`fishsign.mds` — 2,160 bytes, 1 node, identity matrix, at the origin.** Re-parses cleanly.
`--recenter` zeroes the translation so the model's origin sits on the sign itself (otherwise it
would render 90/2/64 units from wherever you place it); `--unrotate` drops the donor's incidental
yaw so it can be aimed at runtime with `CMapParts::SetRotY`.

#### Deployment — the wall, and the way round it

> **Correction.** An earlier draft of this document claimed you could re-point `LoadGroundData`'s
> hardcoded path (`gedit/e01/mds/e01a05_0.mds`, string at `0x2029B010`) and have the sign load in
> every Georama town for free. **That is wrong.** Those three models load only when `MapNo < 6` and
> are stored in dedicated `CEditGround` fields (`[0x81d3..0x81d5]`). They are flat 50x50 quads at
> y=0 with three textures — Georama *editor furniture*, not world props. Hijacking one would put the
> sign wherever that furniture draws. Do not use that route.

**The wall.** Every model that renders in the town world is resolved by
`SearchPTS(scn_data, name)` — which searches **only that town's own `scene.scn`**. This is true both
for Georama parts (`LoadObjectParts` @ `0x182390`) and for the static `WATER` / `GROUND` objects
(`LoadGroundData`). There is **no loose-file fallback on that path**. `EdLoadFile`'s fallback serves
only the sky, the edit-area grid frames, and those three editor models.

So a part or static object must physically live inside each town's `scene.scn`, and those cannot grow:
the sector slack is 112 bytes for `gedit\e03\scene.scn` and ~1 KB for the others. Growing them means
moving them, which means rebuilding the ISO.

**The way round it: villagers are loaded from LOOSE FILES.** `EdLoadVillager` (`0x186290`) does:

```c
GetEditDataDir(dir);                        // EditDataDir, e.g. "gedit/e03/"
sprintf(path, "%schara/%s.chr", dir, name); // format string @ 0x29B0A8
LoadFile2(path, read_buffer);               // a DIRECT disc load by path — no pak, no SearchPTS
```

That is the one loader that renders something in the world *and* takes an arbitrary file off the disc.
And a `.chr` is a self-contained archive (model + **its own textures** + motion), so routing the sign
through it **makes the texture problem disappear entirely** — no `img.pak` edit, no `GRD_IMG` line.

`CommandPEOPLE2` (`0x177330`) records each villager into a runtime table at
`edit_info + 0x18400 + i*0x90` (max 16, count in `people_list`), and NPCs are freely positionable
(`_SET_NPC_POS` 136, `_SET_NPC_ROT` 137, `_SET_NPC_SCALE` 139) — which `TownCharacter.cs` already does.

#### The plan: one asset, three aliases, size-neutral

The path is `<townDir>chara/<name>.chr`, so it is nominally per-town. But `data.hed` is just a table of
**80-byte name slots** mapped to `{offset, size}` in `data.hd2` — and **several names may alias the same
bytes**. So:

1. Build **`fishsign.chr`** — a `.chr` archive holding `fishsign.mds` + `fishsign.img` (+ a motion).
2. Write it **once** into dead space in `data.dat` (see docs/archive-dead-weight.md).
3. **Rename three dead `data.hed` entries** to `gedit\e03\chara\fishsign.chr`,
   `gedit\e05\chara\fishsign.chr`, `gedit\s04\chara\fishsign.chr` — all three pointing at the *same*
   offset and size. Renaming is in place; the 80-byte slot does not change length.
4. Add `PEOPLE2 "fishsign"` to each town's `mapinfo.cfg` by overwriting an equal-length run of comment
   text (the cfgs are 13 KB of densely-commented text).
5. Position it at runtime.

**Every file keeps its exact size, so the ISO's filesystem is never touched and no rebuild is needed.**
One asset, three aliases, three one-line cfg edits.

#### Can a `.chr` be a static prop? — YES, settled

**The container handles it.** `kira.chr` (26 KB) and `fly_light.chr` are pure props:

```
IMG 0,"kira.img"
IMG_END
MODEL "kira.mds"
BODY_SIZE 17,7,60
MOTION 0, "kira.mot", "", ""      <- empty bbp/wgt = NO SKELETON, no skinning
SHADOW_MOTION "", "", ""          <- no shadow at all
KEY_START 0
KEY 10, 20, 0.15
MOTION_END
```

No `SHADOW_MODEL`, no `FOOT`, no rig. And `i01h04_water.chr` is a *water prop* shipped as a `.chr` in a
town's chara folder. A rigid signboard is well within the format.

**The visibility gate is not a problem.** This was the real risk — villagers normally appear only once
their house is rebuilt. But `EdInitVillagerOnOff` (`0x186360`) gates on a part id at
`VILLAGER_INFO + 0x48`:

```c
partId = info[0x48];
if (partId < 0 || ...) { on = 1; }      // unconditionally ON
else { ...check whether that building is complete... }
```

and **`CommandPEOPLE2` initialises that field to `-1`**. A villager declared by a plain `PEOPLE2` line
therefore has no associated building and is **always visible**. No Georama condition to satisfy.

**What the villager path does demand:**

| requirement | why | what to do |
|---|---|---|
| motions by index | `p01a.chr`'s KEY comments: `0 = 立ち` stand, `1/2 = 歩き` walk, `3 = 会話` talk. A `.chr` with fewer keys risks an out-of-range motion index. | declare **four KEY ranges, all pointing at one static frame** — any index resolves to "stand still" |
| not wandering | `PEOPLE2 "name", p1, p2, p3` — `p2` is 0 for several shipped NPCs; almost certainly the "moves" flag | `PEOPLE2 "fishsign", 0, 0, 0.3`; the mod can pin the position each tick anyway |
| `PEOPLE_LIST` | the cfg gates which villagers are active | add our index |

**The bonus: villagers are TALKABLE** (`TALK_ROT`, `TALK_EVENT`, `TALK_DIR`). A talkable object at the
water's edge is exactly what a fishing sign should be — walk up, press a button, get the prompt. And
the fishing prompts live in the town's `.mes` (`EditMes1` @ `0x21D1B550`), which the mod can rewrite in
RAM the same way `WeaponDescriptions.cs` does. The sign stops being decoration and becomes the
interaction point.

#### Loose ends

- `EdInitVillagerOnOff` loops **10** villagers; `CommandPEOPLE2` accepts **16**; Queens already declares
  13. What happens to indices 10-15 is unconfirmed — keep our entry under the cap.
- `EditDataDir` is a writable global — an alternative lever for redirecting the per-town path, but
  repointing it would break every other per-town load. Last resort only.

#### The texture

> If the sign ships inside a `.chr` (see above), this section is **moot** — a `.chr` carries its own
> textures, so `e01b24` travels with it and no `img.pak` or `mapinfo.cfg` texture work is needed. The
> IM2 format is documented anyway because it is needed to *build* that `.chr`, and because it is the
> only route if the sign is ever placed as a Georama part instead.

The sign's texture is **`e01b24`** — and its distribution is the confirmation that we found the right
frame:

| town | `e01b24` in its `img.pak`? |
|---|---|
| Norune e01, Matataki e02, Muska Lacka e04, Queens Harbor s09 | **yes** |
| **Queens e03, Moon Factory e05, Brownboo s04** | **no** |

It is present in exactly the four fishing towns and absent from exactly the three targets. `e01b24`
is a named texture entry **inside the bank `e04b01.img`** (864,112 B) in Muska Lacka's `img.pak`;
Queens' equivalent bank is `e03b01.img`.

So the sign will render **untextured** in the three target towns until `e01b24` is made resident.

#### The `.img` texture-bank format (IM2)

An `.img` is a flat archive of TIM2 images addressed **by name**:

```
header (0x10)
  +0x00  char[4]  "IM2\0"
  +0x04  u32      entryCount
  +0x08  u32      0
  +0x0C  u32      0

entry (0x30 each, table at 0x10)
  +0x00  char[32] name      <- NUL-terminated
  +0x20  u32      offset    <- ABSOLUTE file offset of the TIM2 block

then the TIM2 blocks, in table order.
```

> **Trap:** the name field's padding is **uninitialized garbage** — stale heap from the packing tool,
> full of stray `Y:\Project\...` paths and `00..FF` runs. Read to the first NUL and ignore the rest;
> do **not** assume zero padding or you will misparse every bank past the first few entries.

The **entry name** is what a mesh's `HB<name>` reference resolves against — *not* the `.img`
filename. A bank can be called anything; only its entry names matter to the meshes. That is what
makes this tractable.

Tool: **`tools/img_surgery.py`** (`list` / `extract` / `make`).

`e04b01.img` (864,112 B, Muska Lacka's ground bank) holds 16 entries; the last is:

```
 15  e01b24   0x000CEB30   17472 B   128x128, 8bpp indexed, 256-colour CLUT
```

#### The result

```
python3 tools/img_surgery.py e04b01.img make e01b24 -o fishsign.img
```

**`fishsign.img` — 17,536 bytes, one entry (`e01b24`), TIM2 payload byte-identical.** A structurally
valid vanilla bank, with the padding properly zeroed.

#### `data.hed` / `data.hd2` mechanics

`data.hed` is a flat table of **80-byte name slots**; `data.hd2` holds the matching
`{offset, size, sector}`. Two consequences the patch leans on:

- **A name can be changed in place** (the slot is fixed-width), so a dead file's entry can be
  repurposed to any new path without moving anything.
- **Several names may alias the same bytes.** Nothing stops three `data.hed` entries pointing at one
  offset — which is what lets a single `fishsign.chr` serve three towns.

Pak sub-file header, for building the `.chr`: 0x50 bytes — name at `+0x00`, `u32 dataSize` at `+0x44`,
`u32 nextSubfileOffset` at `+0x48` (relative to the sub-file's own start).

### Strategies D–F — substitutes, when a real sign is not worth it

- **NPC marker.** Every town loads `p##a.chr` villagers, and the engine exposes `_SET_NPC_POS` (136),
  `_SET_NPC_ROT` (137), `_SET_NPC_SCALE` (139), `_SET_NPC_MOTION` (132), `_SET_NPC_ON_OFF` (1063).
  Stand a villager at the water. Diegetic, no assets, works everywhere.
- **Sparkle.** `kira.chr` / `fly_light.chr` are already loaded in most towns. Zero cost, and the game
  already uses shimmer to mean "interactable".
- **2D marker.** `_DRAW_SPRITE` (503), `_DRAW_BG_SPRITE` (504), or the game's own
  `_DRAW_EXCLAMATION_MARK` (cmd 9).

### Don't forget the text

The fishing prompts — *"Seems like you can fish here, but you don't have a fishing pole"* — live in
the town's `.mes` and exist **only** in e01, e02, e04 and s09. Queens, Brownboo and the Moon Factory have
none. The three entries must be injected into the town's loaded message buffer (`EditMes1` @
`0x21D1B550`). The mod already does in-RAM `.mes` rewriting in `WeaponDescriptions.cs`.

### Dead end, for the record

**No `.chr` file anywhere in the game contains a `kanban` frame** (all 1,099 scanned). So the sign
cannot be brought in as a town character; the loose-`.mds` route above is the only indirection that
reaches it.

---

## 8b. The script injection — WORKING (Yellow Drops), and the two things that made it work

`CustomFishingSpot.cs` installs a spot with **no ISO change and no injected code**: overwrite an unused
script label with a `_LOAD_FISHING_DATA` call, and create a type-3 event point pointing at it. The
engine then does the rest. Proven live in Yellow Drops:

```
fishing_rect  (-475, -186) -> (-675, -386)     <- ours
WaterLevel=1  GroundLevel=-15                   <- ours
cpoly=27/1024  FishNum=4  drawUnderWater=1
```

Two non-obvious rules had to be learned the hard way. Both cost a full test cycle each.

### A label does NOT begin with an instruction — it begins with a header

After the label's 8-byte gap there are **four 12-byte slots** before the first real instruction:

```
label 256 @0x83D4:  [27,0,0] [0,0,0] [0,0,0] [0,0,0]   then  PUSH 263 ...
label 11:           [ 3,0,0] [0,0,0] [0,0,0] [0,0,0]   then  PUSH 1 ...
label 128:          [ 4,0,0] [0,0,0] [0,0,0] [0,0,0]   then  YIELD ...
```

The leading value varies per label (27, 10, 4, 1, 3…) and is almost certainly a **local-variable
count**; the three slots after it are reserved. Code starts at **`codeOffset + 8 + 48`**
(`TownScript.LabelCodeSkip`). Writing over the header produces a maddening symptom: the event fires,
`ScriptLabelRequest` is set correctly, **and nothing runs** — because the VM reads your first `PUSH`
as the frame setup and everything after it as garbage.

### A label that RETURNS is re-entered every frame

Our label ends in `RET`, so the frame after it finishes the player is *still standing in the trigger
radius* — the point matches again, and `_LOAD_FISHING_DATA` runs again, **re-loading `chara/fishing.pak`
and allocating six fresh CFish, every frame**. Consecutive dumps caught it red-handed:
`Fish=0x00FD9790`, then `Fish=0x01552910`. The town crawls.

Norune is immune because **label 256 never returns** — it is a state machine that yields and drives the
whole session, and a label that has not returned is not re-entered.

Rather than relocate Norune's ~28 KB of branches, the mod gates the trigger from outside: the engine
only re-runs the label if the point matches, and it only matches while `+0x00` (Enabled) is non-zero.
So `GuardRetrigger()` clears Enabled the moment the spot loads and restores it once the player walks
beyond 1.5× the radius (hysteresis, so loitering after a session does not immediately re-fire).

**If you ever DO want a resident fishing script**, the shape to copy is: `_LOAD_FISHING_DATA` →
`_GOTO_FISHING` → yield-loop → `_EXIT_FISHING` → `RET`. That needs branches, which needs the jump-offset
convention, which is the next thing to reverse if this outgrows the gate.

### A script that never YIELDs is not an event at all

This is the one that cost the most, because every visible signal said "working". `EdEventInit` does not
merely *start* a script — it **runs** it:

```c
lVar4 = EdRunEvent(...);
if (lVar4 < 1) { simple_event = 1; return 0; }   // ran to completion -> NOT an event
else           { return 1; }                     // yielded           -> GameMode = 0xE
```

and `EditLoop` only sets `GameMode = 0xE` (event mode) when that returns non-zero.

A script with no `YIELD` (op 23) finishes inside the init call and is written off as a **`simple_event`**.
The game never enters event mode → `EventMode()` never runs → and `EventMode()` is *the only consumer of
the return code*. So `_GOTO_FISHING` sets `0xB`, and nothing on earth ever reads it.

Observed exactly that for three test cycles: the spot loaded, the return code went to `0xB`, `GameMode`
never left `1`, no fishing. **One `YIELD` at the top of the label fixes it** — the script survives the
init call, gets promoted to a real event, and its `RET` is then seen by `EdEventMode`.

Tellingly, the label we hijacked (310) *began with a YIELD* in its original bytecode. So do many others.

### The mode chain, for the record

```
script: _GOTO_FISHING  ->  EventReturnCode (0x21D3D618) = 0xB
EdEventMode:  if (0 < code) { EdEventFinish(); }  return code;
EventMode:    case 0xb:  GameMode = 0x10          <- FISHING IS 0x10, NOT 0xB
```

Two names in the mod were wrong and actively misled the debugging:

- **`FishingAddresses.Active` (0x21D19714) is not a flag anyone sets.** `MoveChara` recomputes it every
  frame as `DAT_01d19714 = (GameMode == 0x10)`. It is a *mirror* of the mode, so it cannot tell you
  whether a spot was set up — it read `0` through several perfectly good spot loads.
- **`GameMode 0xB` is not fishing.** `0xB` is the event return code; `0x10` is the mode.

### Setup is not enough — the SESSION needs more than three commands

With the YIELD in place fishing mode is entered, fish spawn and the menu draws. But the session itself
is broken until you copy the rest of what Norune's script does. The full Norune sequence:

```
998 _LOAD_FISHING_DATA(area, rect, water, ground)
507 _SET_CLIP_POINT(sign.x, sign.y, sign.z, 100.0)
    var3..var6 = sign.x±50, sign.z±50          # a 100x100 box AROUND THE SIGN
996 _INIT_FISH(that small box)                 # fish spawn by the sign, not across the whole rect
140 _NPC_DRAW
  7 _SET_WORLD_COORD(sign pos, sign rot)
137 _SET_NPC_POS(-1, 40, 0, 96)                # TELEPORTS the player  (-1 = the player)
138 _SET_NPC_ROT(-1, 0, 3.14, 0)               # and FACES them at the water
997 _GOTO_FISHING()
500 _FADE_IN(60)
RET
```

Note `(40, 0, 96)` — that is exactly the part-local position of Norune's fishing event point. **The
script snaps the player into the fishing stance.** Skip it and the rod misbehaves: Toan stands wherever
he walked in, facing wherever he was facing, so the cast goes off toward dry land, the bobber never
reaches water, and the engine rejects it.

⚠ **The command table was off by one until now.** `tools/stbdis.py` read `/tmp/stbcmds.json`, which had
been built from the dispatch table with the id/handler pair the wrong way round. It reported `500` as
`_FADE_OUT` and `997` as `_INIT_FISH`. The true names, rebuilt from the ELF by matching the known pair
`{handler = 0x1969A0, id = 998}`:

| id | command |  | id | command |
|---|---|---|---|---|
| 998 | `_LOAD_FISHING_DATA` | | 500 | **`_FADE_IN`** |
| 997 | `_GOTO_FISHING` | | 501 | `_FADE_OUT` |
| 996 | `_INIT_FISH` | | 507 | `_SET_CLIP_POINT` |
| 995 | `_EXIT_FISHING` | | 137 / 138 | `_SET_NPC_POS` / `_SET_NPC_ROT` |

### Toan has a SECOND MODEL for fishing — `c01d_turi.chr`

The rod is not an effect or an attachment. `_GOTO_FISHING` does:

```c
SearchFrame(chara->model, "sao");     // 竿 = fishing rod
... FishLineInit(...)
```

It resolves the rod **by frame name** on the player's model. Ordinary Toan (`chara/c01d.chr`) has no
`sao` frame and none of the fishing motions — so with the stock model you get: no rod, no line, no float,
no bait, and a "cast" that plays whatever animation happens to occupy that motion index in `c01d`'s table
(in practice, the atla-opening motion).

`chara/c01d_turi.chr` (釣り = fishing, 1.8 MB) is the fishing Toan. Norune swaps the whole model:

```
enter:  _LOAD_MAIN_CHARA("chara/c01d_turi.chr", "c01d_turi.cfg", 1)
exit:   _LOAD_MAIN_CHARA("chara/c01d.chr",      "info.cfg",      0)
```

Both must be done. Skipping the restore would leave the player walking around town with the fishing motion
table, which breaks every *other* animation the same way.

#### String operands are relative to `codeBase`, not the file

To emit `_LOAD_MAIN_CHARA` we had to push strings, which nothing else here needed. A type-3 (string) push
carries an offset **relative to the script's code base** (the u32 at header `+0x08`), NOT a file offset:
Norune's push reads `a2 = 0xED18` while the string sits at file `0xEE00`, and `0xEE00 - 0xED18 = 0xE8`,
which is exactly its `codeBase`. This matches `load__10CRunScript`, which caches `base + *(base + 8)`.

So `StbWriter.PushString` emits a placeholder and `EmitStrings` lays the bytes down just past the
generated code, patching each operand to `blobOffset - codeBase`. There is plenty of room — our scripts
are a few hundred bytes and the labels they replace are thousands — but `WriteScript` still refuses to
write if the code plus strings would run past the next label's `codeOffset`.

### Loading a main character RESETS its position

`_LOAD_MAIN_CHARA` puts the model back at a default position. Norune therefore re-issues the placement
*immediately after* the swap, on both paths — and its exit block also runs `_EXIT_FISHING` **after** the
model restore, not before:

```
_LOAD_MAIN_CHARA("chara/c01d.chr", "info.cfg", 0)
_SET_NPC_POS(-1, var0, var1, var2)      # put the player BACK
_SET_NPC_ROT(-1, var3, var4, var5)
_NPC_DRAW(1, -1)
_EXIT_FISHING()
_FADE_IN(60)
```

Omit the re-placement and quitting fishing drops the player somewhere else on the map, falling forever.

### The bait needs a MODEL loaded — `_SET_FISHING_ESA` does not load one

`_SET_FISHING_ESA(itemId)` only points the hook at **item frame 0**
(`FishingLoadEsa: EsaFrame = itemFrames[0]`, where `itemFrames` = `DAT_01d3d42c`). It loads nothing. If no
one built that frame, the bait is equipped logically and is simply invisible.

Norune's recipe (in label 134, after the menu picks an id):

```
_LOAD_ITEM_FILE(id)        # 49 — starts a BACKGROUND read (LoadFileBG)
call_func 400              # waits for it
_CLEAR_EVENT_BUFF()        # 39
_ACTIVE_FILE_BUFFER(0, 0)  # 44
_LOAD_ITEM(0)              # 50 — builds item frame 0
YIELD
_SET_FISHING_ESA(id)       # 994
_GOTO_FISHING()            # 997 — and back to fishing (see the return-code rule above)
```

`_LOAD_ITEM` returns 0 if `GetReadBGFile` is not ready, and **no load-complete command is exposed to
scripts** — Norune waits with a script function compiled into its own `.stb` (`call_func 400`), which is
not reachable from another town's script. So `CustomFishingSpot` spins YIELDs instead, and does the whole
item load in the ENTRY script (behind the fade, where a pause is free) rather than on the Square press.

### The STB VM, read from `exe()` rather than inferred

Three separate bugs came from guessing at opcode semantics from usage. The interpreter
(`exe__10CRunScript` @ 0x23E080) settles it:

| op | meaning | operands |
|---|---|---|
| 1 | push variable VALUE | `a1` = var index, **`a2` = addressing mode** |
| 2 | push variable POINTER | `a1` = var index, **`a2` = addressing mode** |
| 3 | push constant | `a1` = type (1 = int, 2 = float bits, 3 = string), `a2` = value |
| 4 | POP (`sp -= 8`) — **not** a jump | |
| 21 | EXT (call) — `sp -= argc * 8`, then dispatch | `a1` = stack entries INCLUDING the command id |
| 23 | YIELD | |

**The addressing mode is in `a2`, not `a1`.** Mode 1 = direct (`vars[a1]`); 2/4/8/0x10/0x20 are
indirect/array forms that pop an index first. Emitting `a2 = 0` matches *none* of them, so the
instruction pushes **nothing** — the stack runs short, `EXT` reads garbage as the command id, and the VM
derails. That is what froze the game on the first bait-menu attempt.

**A string operand is a POINTER, and type 3 means pointer, not "string".** `case 3` does
`push_str(this->codeBase + a2)`, and `this->codeBase` is `base + *(base+8)` — hence string offsets are
relative to the header's `codeBase`, not the file. The same type 3 is what `op 2` pushes for a variable
address, which is how out-parameters work.

**A label's header declares its local count.** `header[0].op` = number of locals (Norune: label 256 → 27,
134 → 10, 133 → 1). The labels we hijack declare 0, so any script of ours that touches `var0` must raise
it or it is reaching outside its frame.

### The bait menu is native: `_GOTO_CHANGE_ESA`

Command 25 opens the game's own use-item menu over a **static** bait list (the template at `_820` — we do
not supply one), and takes a POINTER to a script local, which the menu writes the chosen item id into:

```c
if (*param_1 == 3) {           // arg1's stack type must be 3 (pointer)
    p_use_item = param_1[1];   // where to write the result
    ... EdSetUseItem(...); menu_mode = 9;
}
```

So label 134 is: `PushVarRef(0); EXT 25; YIELD;` then the load pipeline on `var0`. One YIELD suffices —
while `menu_mode != 0`, `EdEventMode` runs the menu instead of stepping the script.

### Labels 133 and 134 are hardcoded in the engine

In fishing mode `EdMoveChara` runs a `chara_fishing` state machine that asks for script labels BY NUMBER:

```c
EdPadDown(0x40) -> chara_fishing = 2            // Cross  = cast (no script)
EdPadDown(0x20) -> ScriptLabelRequest = 0x85    // Circle = label 133, quit fishing
EdPadDown(0x80) -> ScriptLabelRequest = 0x86    // Square = label 134, bait menu
```

Norune has both (133 = a cursor menu, 134 = a menu over bait item ids 166–170/193/197). **A town that
never had fishing has neither**, so the button requests a label that does not exist, the event never
starts, and the player is stuck in fishing mode with no way out and no bait selection.

The ids are not negotiable. `CustomFishingSpot` therefore claims spare labels and **rewrites their ids**
to 133 and 134.

**A fishing sub-script that just RETs will END THE SESSION.** Every one of them runs as an ordinary
event, so when it returns, `EventMode` switches on its return code — and `default:` is `GameMode = 1`,
walking. That is precisely how quitting is implemented: label 133 is `_EXIT_FISHING; _FADE_IN(60); RET`
and the drop back to walking comes *from the default branch*, not from any command.

The corollary bit us: the bait label had the same shape, so pressing Square politely equipped the bait
and then quit fishing. Any sub-script that means to STAY in the session must end by asking for fishing
again — `_GOTO_FISHING` (return code `0xB`) — so `EventMode` runs `case 0xb: GameMode = 0x10` instead of
falling through.

Label 134 is currently a STOPGAP: `_SET_FISHING_ESA(166)` (Throbbing Cherry) with no menu, and it does
not check that you own the bait. The real menu route is `_GOTO_CHANGE_ESA` (command 25), which drives the
generic use-item menu — but its handler bails unless the first stack entry is type 3 (**a string**), so
it needs a string offset out of the town's own `.stb`.

### Field notes on the event point (each one silently ate an attempt)

| field | rule |
|---|---|
| `+0x00` Enabled | **must be non-zero** — `CheckEventPoint` opens with `if (*p == 0) return 0;` |
| `+0x0C` PartIndex | a **Georama part index**. Cloning a door inherits a valid one; use **-1** for world space |
| `+0x60` Radius | a **scalar radius**, not a box. At 2000 it masked every door in town and their "!" markers vanished |
| `+0x1C` | for type 3 this is the **script label** |
| label choice | never hijack an id < 200 — taking "the last label in the table" grabbed **128** in Yellow Drops and broke the town |

### Still open on this path

- **`fishingActive` stays 0** after `_LOAD_FISHING_DATA` alone — the spot is built but the session does
  not start. `_GOTO_FISHING` (997) is now appended to the injected bytecode; untested.
- **Queens has no free event-point slot** (50 of 51 used) — needs a different trigger.
- **Yellow Drops' water level is a guess** (`water: 1`). The trigger sits *outside* the town's declared
  `WATER_SURFACE` square (±320 about the origin), so the liquid there is not that surface.
- `FishingAddresses.OverworldState` (`0x21D19708`) is **the same address** as
  `EventPoints.ScriptLabelRequest`. One of those two names is wrong; worth settling before either is
  trusted.

---

## 9. Open threads

- **Where Matataki's two signs actually are.** e02 has no `LAKE_PARTS`, so they are not in a lake
  model. Most likely static geometry, but not confirmed.
- **What the `_m` mesh suffix means.** If it is the liquid/animated surface, `e05g02_m` and
  `e05g05_m` are the Moon Factory's liquid and give the surface height directly.
- **How to place a part from the mod.** Either write a free `CMapParts` slot directly (`+0xE8 < 0`
  marks free; copy the 0x2A0 template from `CEditGround+0x15F30`, then write position at `+0x10`),
  or feed the Georama loader different placement data. The former is one `memcpy`-shaped write; the
  latter is more faithful to how the game does it.
- **Whether a town can be given a `LAKE_PARTS` it never had** by writing a part record
  (`edit_info + id*0x2D8 + 0xDD10`, type 4 at `+0x244`) pointed at one of its *own* meshes. That
  would make `DrawWater` render a real water patch in, say, the Moon Factory — without new assets.
  Untested.
