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
| Re-skin a live slot to another species | copy its `0x3510` render object | ❌ impractical (~13.5 KB/slot over PINE; no single model pointer) |
| Patch engine code to inject a hook | write to a recompiled code page via PINE | ❌ **crashes PCSX2** — code-patching is off the table |

---

## 5. Bosses on regular floors (investigation, parked)

- Spawning a boss species directly via the roster **black-screens the floor load**. The gate is the
  **record INDEX**, not the record's field values — regularizing `AttackPower`/`ModelCodeCopy`/`EnemySpeciesId`
  on a boss index still hung; spawning a boss's **mesh through a regular carrier index** (e.g. Dasher,
  TableIndex 3, with the boss's `ModelCode` copied into Dasher's record) **loads fine**.
- A roster-spawned boss's AI is **arena-scripted**: its `SpeciesDataPtr` (slot `0x4C`) behavior block holds
  **hardcoded arena world coordinates**, so on a normal floor it freezes / soft-locks. `ModelCodeCopy`
  (`0x040`) selects the AI dispatch and is **bound at spawn** — changing it afterward does nothing; nulling
  `SpeciesDataPtr (0x4C)` afterward also doesn't recover it.
- Conclusion: boss mesh on a normal floor is achievable (carrier-index trick, regular AI); a fully-working
  boss (real AI + defeat behavior + valid positions) needs **behavior-script-table** work — a future task.
- Note: the existing `FixModelRedirectSpawnPositions` does **not** actually relocate enemies (the reads
  look corrected but the writes don't take).

---

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
