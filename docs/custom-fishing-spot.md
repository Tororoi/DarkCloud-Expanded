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

`_LOAD_FISHING_DATA` — **STB command 999**, handler `0x1969A0`.

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

| id | command | handler | role |
|---|---|---|---|
| 999 | `_LOAD_FISHING_DATA` | `0x1969A0` | defines the spot |
| 998 | `_GOTO_FISHING` | `0x196B30` | starts a session: sets game mode `0xB`, hooks the line to the rod frame |
| 997 | `_INIT_FISH` | `0x196C00` | re-rolls the fish |
| 996 | `_EXIT_FISHING` | `0x196C80` | ends the session |
| 995 | `_SET_FISHING_ESA` | `0x196CB0` | sets the bait |
| 25 | `_GOTO_FISH_RANKING` | `0x18C1A0` | opens the records menu |
| 24 | `_GOTO_CHANGE_ESA` | `0x18C1C0` | opens the bait menu |

Four town scripts declare spots: `gedit\e01\event.stb` (Norune), `gedit\e02\event.stb` (Matataki +
Peanut Pond), `gedit\e04\event.stb` (Muska Lacka), `gedit\s09\event.stb` (Queens Harbor).

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
