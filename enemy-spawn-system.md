# Dark Cloud — Enemy Spawn System & Injector Notes

Reverse-engineering notes for how dungeon enemies are selected, loaded, placed, and rendered,
plus what the `EnemyModelInjector` mod hooks can and can't do. Derived from disassembly of
`SCUS_971.11` + `dun.bin` and live PCSX2 savestate/log analysis (June 2026).

Addresses are **PS2-native**; the PCSX2 runtime address is `native | 0x20000000` (the PINE layer
masks the top nibble, so reads/writes work with either form). ELF file offset = `native − 0x100000 + 0x100`.

> **dun-overlay +0x80 shift:** `dun.bin` is copied into RAM *with* its 0x80 file header, so every
> **dun-overlay** symbol is at `symbol + 0x80` at runtime (e.g. `OpA_MotionProcess` sym `0x01DB6BF0`
> → runtime `0x01DB6C70`). **Main-segment** functions (`SetupBaseModel`, `SetupViewMonstor`,
> `ArrangementPos`, `DrawMonstor`, …) and **BSS** globals (`MainMonstorUnit`) are **not** shifted.

---

## 1. The pipeline (floor load → enemies on screen)

```
floor-select menu            sets per-floor population count globals (0x01D564xx) + dungeon/floor state
        │
        ▼
BtLoadMonstor(uraFlag)        dun-overlay; reads the roster for [dungeon][floor]:
        │                       BtEnemyLayoutList[dungeon] (or Ura) → floor block → up to 9 entries
        │                     for each entry id != -1: CMonstorUnit::SetupBaseModel(this, slot, TableIndex, 0x26, MonstorModelBuffer)
        │                       → loads that species' mesh + 0x9C species record; increments [this+0x48] (= # loaded species)
        │                     ⚠ hard cap ~8 distinct species — 9 distinct models hangs the load.
        ▼
ArrangementPos(this, map, count, …)   main-seg; places `count` enemies (see §3)
        │                     each placement: find a walkable tile, pick a species, SetupViewMonstor(...)
        ▼
SetupViewMonstor(this, idx, &pos, flag)   builds the live monster into the first free slot
        │                     (RenderStatus == -1), from that slot's 0x3510 render object
        ▼
DrawMonstor() / Step()        per frame: draw slots whose RenderStatus == 2 from their 0x3510 block; run AI
```

### Two parallel per-slot arrays inside `MainMonstorUnit` (`0x01DF87D0`, size `0x60750`)
- **Live enemy slots** — `0x21E16BA0` (= `this + 0x1E3D0`), 16 × `0x190`. HP, AI state, position, identity. See `EnemySlotOffsets`.
- **Render objects** — `this + slot*0x3510 + 0x1FCD0` (the `0x3510`-stride region, base `0x21E18530`, doc'd as `ModelScaleOffsets`). The CCharacter/mesh `DrawMonstor` actually draws, baked once at spawn. The slot array ends exactly where this begins (`16 × 0x190 = 0x1900`).

---

## 2. Data structures & key addresses

| Thing | Native addr | Notes |
|---|---|---|
| `MainMonstorUnit` (`CMonstorUnit* this`) | `0x01DF87D0` | the global all spawn code operates on |
| Live enemy slots | `0x01E16BA0` | 16 × `0x190` (`this + 0x1E3D0`) |
| Render objects (`ModelScaleOffsets`) | `0x01E18530` | 16 × `0x3510` (`this + slot*0x3510 + 0x1FCD0`) |
| `MonstorModelBuffer` | `0x01F066D0` | model load allocator (`CDataAlloc2*`) |
| `MonstorScriptBuffer` | `0x01F066E0` | 16 × `0x10` AI-script slots |
| `EnemySpeciesTable` | `0x0027FB00` | spawn-template records, stride `0x9C` (ELF file `0x17FC00`) |
| `BtEnemyLayoutList` | `0x002917B0` | 7 dungeon ptrs → `BtEnemyLayout00..06` (normal floors) |
| `BtUraEnemyLayoutList` | `0x002917D0` | 7 dungeon ptrs → Ura (back floors) |
| population count globals | `0x01D56494`, `0x01D5649C`, `0x01D564A0` | read by `ArrangementPos` as the `count` arg on some paths |
| `checkDungeon` | `0x002A3594` | current dungeon (0=DBC…6) |

### Functions
| Function | Native addr | Role |
|---|---|---|
| `CMonstorUnit::SetupBaseModel(this,slot,tableIndex,0x26,alloc)` | `0x001DFE90` | load one species' mesh+record (disc I/O, blocking). **Non-std ABI:** a0,a1,a2,a3, **t0**=alloc. `record = 0x0027FB00 + tableIndex*0x9C`. |
| `CMonstorUnit::ArrangementPos(this,map,count,a3)` | `0x001D7FC0` | place `count` enemies on walkable tiles; picks species; calls SetupViewMonstor. `count = a2`. |
| `CMonstorUnit::SetupViewMonstor(this,idx,&pos,flag)` | `0x001E02B0` | instantiate into first free slot (RenderStatus==-1) from its `0x3510` block |
| `CMonstorUnit::CleanViewMonstor(this,i)` | `0x001DF9F0` | resets **all 16** slots' RenderStatus to -1 (full reset, not per-slot) |
| `CMonstorUnit::DrawMonstor()` | `0x001D8CD0` | per frame: draw slots with RenderStatus==2 from `this+slot*0x3510+0x1FCD0` |
| `CMonstorUnit::Step(i)` | `0x001DD540` | per-frame AI/physics on a slot |
| `BtLoadMonstor(i)` (dun sym) | `0x01DB9330` | load the floor's roster models (runtime `+0x80`) |

### `EnemySpeciesTable` record fields used here (offset within the `0x9C` record)
- `0x000` `ModelCode` (`"e24a"`), `0x040` `ModelCodeCopy` — the AI-dispatch code, **bound at spawn**.
- `0x078` **`SpawnCap`** (was "Unk024") — **`0` or `3` = repeatable; any other value = at most one per floor.** Ships ~19/90 species flagged once.
- `0x07C` `EnemySpeciesId` (the EID stored in live slots; ≠ TableIndex).
- `0x088` `AttackPower` — `65535` is the **boss-class sentinel**.

### `BtEnemyLayout` floor block (`0x70` per floor, 9 entries × `0x0C`)
- `+0x0` **Count** — UNCONFIRMED; **NOT** the floor population (writing it changes nothing).
- `+0x4` **Id** — the species **`TableIndex`** (physical record index `SetupBaseModel` uses), `-1` = empty.
- `+0x8` **Weight** — **UNUSED** by the spawn path (selection is uniform, see §3).

---

## 3. The species-selection algorithm (inside `ArrangementPos`)

For each of `count` placements:

1. **Find a spot:** `SearchiDoPutArea` picks a walkable tile; rejected if on a treasure box / atra / trap circle, or if ≥3 enemies are already within range (anti-clustering). Retries up to ~65000 times, so the actual number placed is **capped by available walkable tiles** (small floors top out well under 15).
2. **Pick a species (uniform random over the loaded pool):**
   ```
   idx = (int)( rand()/RAND_MAX * count )      // count = [this+0x48] = # distinct species loaded
   ```
   The roster **`Weight` field is not read here** — every loaded species is equally likely. (Confirmed live: weights `5:95` produced the same ~50/50 split as even weights.)
3. **Apply `SpawnCap`:** read the picked species' record `SpawnCap` (`+0x78`).
   - `0` or `3` → place it (repeatable, no "used" mark).
   - otherwise → if it was already placed this floor, **retry** (re-roll position+species); else mark used and place it **once**.
   Because a re-roll retries rather than wasting the slot, spawn-once species don't reduce the floor total — the remaining slots fill with repeatable species.
4. `SetupViewMonstor` instantiates the picked species into the next free slot.

**Net behaviour:** floor total is fixed by the floor (≈15 on DBC, bounded by walkable tiles); species are chosen uniformly at random from the loaded pool; `SpawnCap` caps individual species to one-per-floor without shrinking the total.

---

## 4. What you can and can't control

| Goal | Lever | Works? |
|---|---|---|
| Which species spawn on a floor | edit `BtEnemyLayout` entries' `Id` (= TableIndex) | ✅ data write; takes effect next floor load |
| Multiple different species per floor | fill entries 0..n-1 with different TableIndexes | ✅ up to **8 distinct** (9 hangs the model load) |
| Spawn **exactly 1** of species X (total preserved) | set X's `SpawnCap (+0x78)` to non-`0/3` (e.g. `2`), keep others repeatable | ✅ the clean solution |
| Bias a species to be rarer via weight | roster `Weight (+0x8)` | ❌ unused on this path |
| Increase/decrease floor population | population count globals `0x01D564xx` | ⚠ only the global-fed `ArrangementPos` callers; clamped to a floor minimum and the walkable-tile cap; some callers use constants 8/15 baked in code |
| Reduce a species' count (lossy) | post-spawn set extra slots' `RenderStatus (0x000) = -1` | ✅ removes them (DrawMonstor draws only `==2`); **shrinks the total** |
| Re-skin a live slot's **appearance** | copy ~16 species words from a donor slot (§5b) | ⚠ cosmetic only — model+name change, but un-hittable + wrong AI |
| Re-skin a live slot's **behavior** (hittable + AI) | — | ❌ collision + AI are spawn-initialized engine state, not flat data |
| Patch engine code to inject a hook | write to a recompiled code page via PINE | ❌ **crashes PCSX2** — code-patching is off the table |

---

## 5. Bosses on regular floors (investigation, parked)

What actually happens when you put a boss in a normal floor's roster (corrected after deeper testing):

- A boss in a **mixed** roster (e.g. `20,83`) **loads fine** — but the boss spawns at the **origin (0,0)**
  (displaced) and is **frozen** (its AI is arena-scripted and has nothing valid to do on a normal floor).
- The earlier "boss black-screens the load" was **not** an index/field gate — it was the **spawn-once retry
  trap**: bosses ship with `SpawnCap = 2` (spawn-once, §3), so a *single-species* boss roster is an
  all-once pool that `ArrangementPos` can never fill → it retries ~65000× and hangs. (The single-species
  roster path now forces `SpawnCap = 0` to avoid this.) Regularizing `AttackPower`/`ModelCode`/etc. never
  mattered for the hang — it was the pool, not the record.
- The displacement + freeze are **script-driven** (see §5c), not field-driven. Regularizing the boss
  attack-block (`AttackPower`, elem mults, `Unk098`, steal) does **not** fix the spawn location.
- `ModelCodeCopy` (`0x040`) selects the behavior script and is **bound at spawn**; changing it afterward
  does nothing, and nulling `SpeciesDataPtr (0x4C)` afterward doesn't recover it either.
- Achievable today: boss **mesh** on a normal floor via the carrier-index trick (regular index whose
  `ModelCode` = the boss's), which gives correct placement but **regular AI**. A fully-working boss
  (real AI + valid position + defeat) needs editing the boss's behavior script — see §5c.
- Note: the existing `FixModelRedirectSpawnPositions` does **not** actually relocate enemies (the reads
  look corrected but the writes don't take).

---

## 5c. Boss spawn positioning is script-driven (`CRunScript` / `.stb`)

Every spawned enemy runs a behavior script at spawn; the boss's does the (arena) repositioning.

- `SetupViewMonstor`, at the end of spawn, calls `BtSetEventScript(...)` then
  **`CRunScript::check_program(script, 1)` → `CRunScript::run(script, 1)`** (`0x23dff0` / `0x23de70`).
  So each enemy runs **program label 1** (its spawn/init routine) once at spawn.
- Programs in a `CRunScript` are keyed by a **label**, not a sequential index. Observed labels (from `run`
  call sites): **`1` = spawn/init** (run by `SetupViewMonstor`); **`0x32`/`0x64`/`0x6e`/`0x78`** = per-frame
  AI (run by `Step`/`MoveCheck` at `0x1ddf54`…). So the spawn routine is **separate** from the AI programs.
- Per-slot `CRunScript` object: `MonstorUnit + slot*0x48 + 0x54DD0` (PCSX2 `0x21E4D5A0 + slot*0x48`).
  Its program table ptr is at `[script+0x3C]`; that table has `[+0x0C]` = offset to entries,
  `[+0x10]` = program count, entries stride 8 = `(label:int, codeOffset:int)`.
- The scripts are real files: **`dun\monstor\<code>.stb`** (e.g. `c16a.stb` = MinotaurJoe @ data.dat
  `0x1AFE5800`; `e03a.stb` = Skeleton). Extract from the ISO at `2622*2048 + datadatOffset`.
  STB format: `"STB\0"` magic; program table at file `+0x40`; count at `+0x10`; entries stride 8
  `(label, codeOffset)`.
- Boss vs regular: **both have the same 4 program labels** (`1, 100, 110, 120`). The difference is the
  boss's **label-1 is much bigger** (`c16a` `0xFDC` bytes vs `e03a` `0x50C`) — it does the extra arena
  setup. So you can't just skip label-1 globally (regulars need it); you'd have to neuter the reposition
  *command inside* the boss's label-1.
- The arena coordinates (`685.7, 1481.2, -5.0`, seen in the boss's loaded `SpeciesDataPtr` block) are
  **not literals in `c16a.stb`** — label-1 **reads a boss-arena spawn point from the engine/map**, which
  resolves to `(0,0)` on a normal floor. So the fix is either (a) neuter that read/warp op in label-1, or
  (b) supply a valid boss spawn point in the map field it reads.
- **Both require decoding the STB script VM** (`run__10CRunScript` dispatches opcodes — likely to the
  `_XXX__FP12RS_STACKDATA` script functions via an opcode→function table). Then apply the fix by editing
  `c16a.stb` in `data.dat` (static; the archive is sector-mapped) — a RAM patch is timing-sensitive since
  label-1 runs at spawn during floor load. **This is the parked next step.**

---

## 5b. Live slot conversion (tested, not viable for a full re-skin)

Tested transplanting one spawned enemy into another species by copying render-object data slot→slot.

- The `0x3510` render object is **mostly shared/instance state**: diffing two species' render objects,
  only **16 of 3396 words differ by species** (14 are pointers into that session's loaded mesh/anim data
  in `0x010xxxxx`; 2 are species fields). Same-species slots differ by just ~9 words (position / anim phase).
- So copying those ~16 words (offsets `0xb4,0xbc,0xc0,0xc4,0xd4,0xd8,0x2cc,0x2d0,0x2e4,0x340,0x344,0x364,0x3c0,0x3c4,0xc40,0xc74`)
  from a donor slot of species Y into slot X, plus `EnemySpeciesId`/`SpeciesParamPtr`, **cosmetically re-skins X to Y** (model + name), position preserved — cheap (~few dozen writes).
- **But the converted enemy is un-hittable, can't attack, and keeps the original species' AI.** Collision
  (slot `EntityScale`/reticle/lock-on + the per-species collision mesh) and the **AI state machine** are
  initialized by the engine at spawn — they don't transplant via memory writes. So this is **cosmetic only**.
- A **donor slot is required** because the 14 model words are *runtime pointers* into where the mesh loaded
  this session — not known statically and not recomputable without re-running `SetupViewMonstor`.
- **Conclusion:** a live slot can be *re-skinned* (appearance), not *re-bodied*. A truly functional change
  of a live enemy's species needs re-spawning it via `SetupViewMonstor` — engine code we can't invoke
  (PINE code-patching crashes). The conversion code was implemented, tested, and removed.

## 6. `EnemyModelInjector` API (crash-free, data-only)

All edits are data writes; nothing patches code. Roster/`SpawnCap` edits hit the static species/layout
tables and take effect on the **next floor load** — set them at the floor-select screen or between floors.

- `SetSpawnRosterToSpecies(tableIndex, population=0)` — every spawn = one species (boss indices are
  de-sentineled `AttackPower 65535→100` so they don't black-screen, but their AI still won't work on a normal floor).
- `SetSpawnRosterMix(tableIndices[], spawnOnce[], population=0)` — multi-species roster; `spawnOnce[i]`
  writes that species' `SpawnCap` (`2`=once / `0`=repeatable). Even internal weights (unused anyway).
- `SetPopulationTarget(pop)` — writes the count globals `0x21D56494/9C/A0`.
- `CapSpeciesOnFloor(tableIndex, keepN)` — **post-spawn**, keep N of a species and remove extras via
  `RenderStatus = -1`. Lossy (shrinks the floor total); the general "reduce enemies" tool.
- `RosterIndexTest(bossTableIndex)` / `SetCarrierBossAI(...)` — boss-via-carrier experiments (§5).

### Options-tab UI
- **Population** box → `SetPopulationTarget` (0 = leave alone).
- **TableIdx** box syntax: `20` (single) · `20,60` (mix) · `20!,60` (Gyon spawn-once + Cursed Rose).
- **Set roster** → roster from the box. **Index test** → boss-mesh-via-Dasher. **Cap TableIdx to 1** → post-spawn removal.

### Dead code (kept, inert)
The MIPS code-cave + `OpA_MotionProcess` hook path (`EnableCodeHook=false`) is retained for reference but
disabled — PINE writes to the live code page crash PCSX2, so the hook is never armed. See
`tools/build_cave.py`. Use a **data-pointer hook** if a runtime trigger is ever needed.

---

## 7. TableIndex vs Id vs EID — three numbering schemes

- **`BtEnemyLayout.Id` field** and `SetupBaseModel`'s arg are the **physical record index** = `EnemyDefaults.TableIndex` (e.g. Gyon = **20**).
- **`EnemyDefaults.Id`** is the game's enemy id (Gyon = 24); not the same as TableIndex (the table isn't stored sequentially).
- **Live slot `EnemySpeciesId` (`0x42`)** = the record's `EnemySpeciesId` (`+0x7C`) = the EID (Gyon = 24).
- `ModelCode` follows `e<NN>a` for many mid-range species but **not all** (bosses use `cNNx`; several remap) — always resolve via `EnemyData.cs` / the species table, never by formatting the id.
