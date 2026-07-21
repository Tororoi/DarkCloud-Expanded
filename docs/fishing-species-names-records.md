# Fishing: Species Pool, Names, and Records

How DC1 identifies fish species, where their names live, how the records list works, and what it takes to
add **new** fish. This is the reference for the custom-fish work (Priscleen and the planned additions).
Companion docs: [custom-fish-pipeline.md](custom-fish-pipeline.md), [fish-chr-modding.md](fish-chr-modding.md),
[priscleen-fish-files.md](priscleen-fish-files.md), [custom-fishing-spot.md](custom-fishing-spot.md).

All addresses are from `SCUS_971.11`; MMU = guest + 0x20000000. Runtime-only mod: we patch RAM, never the ISO.

## The species pool — 18 slots, hard-clamped

A fish is identified by a **species kind**, an integer. Everything keys off it, and **two engine functions
hard-clamp the kind to 0–17**, so there are exactly **18 species slots**:

| Function | Addr | Table | Clamp |
|----------|------|-------|-------|
| `GetFishFileName(kind)` — model path | `0x2412E0` | `name_419` @ guest `0x296570` (18 × ptr → `chara/fNNa.chr`) | `kind<0 \|\| kind>0x11 → return 0` |
| `GetFishMsgNo(kind)` — name message id | `0x1F2E10` | `FishMsg` @ `0x292AA0` = `[10..19, 0..7]` | `kind<0 \|\| kind>0x11 → return 0` |

`GetFishMsgNo(kind) = FishMsg[kind] + 30`, giving a message id in **30–49**. So kind → model and kind → name
are both fixed lookups, and any kind ≥ 18 returns empty for both.

### Which slots are used vs free

Reading `name_419` and cross-referencing the vanilla spawn tables (`FishingLoadFish` @ `0x1A88F0`, which
picks species per fishing area 0–4):

| kind | model | name (id) | spawned by |
|-----:|-------|-----------|-----------|
| 0 | f01a | Bobo (40) | area 3 |
| 1 | f02a | Gobbler (41) | areas 0,1 |
| 2 | f03a | Nonky (42) | areas 0,2 |
| 3 | f04a | Kaji (43) | area 3 |
| 4 | f05a | BakuBaku (44) | areas 1,2 |
| 5 | f06a | Mardan Garayan (45) | areas 2,4 |
| 6 | f07a | Gummy (46) | areas 0,2 |
| 7 | f08a | Niler (47) | area 0 |
| **8** | **f09a — MISSING** | **Umadakara (48)** | **none — the one free slot** |
| 9 | f10a | Umadakara (49) | area 1 |
| 10 | f11a | Tarton (30) | area 1 |
| 11 | f12a | Piccoly (31) | area 3 |
| 12 | f13a | Bon (32) | area 3 |
| 13 | f14a | Hamahama (33) | area 3 |
| 14 | f15a | Negie (34) | area 4 |
| 15 | f16a | Den (35) | area 4 |
| 16 | f17a | Heela (36) | area 4 |
| 17 | f18a | Baron Garayan (37) | areas 2,4 |

**Only species 8 is genuinely free** — its model `chara/f09a.chr` does not exist in the archive and no area
spawns it. Priscleen uses it. Note species 8 and 9 both display "Umadakara" (see the name-dedup gotcha
below); species **9** is the real, catchable Umadakara.

### Adding a new fish beyond species 8 — the extension strategy

With species 8 taken by Priscleen and both lookups clamped at 17, additional fish have no slot. To add them:

1. **Relocate + enlarge both tables** into a code cave and **widen the clamp** in each function. These are
   in-place EE-code edits (the mod's established idiom — cf. the ABS/Macho patches):
   - `GetFishFileName` @ `0x2412E0`: repoint the `name_419` base (its `lui/addiu`) at a larger table and
     raise the `0x11` bound. The new table entries are pointers to model-path strings (also placed in a cave).
   - `GetFishMsgNo` @ `0x1F2E10`: repoint `FishMsg` at a larger table and raise the `0x11` bound. New entries
     map each new kind to a **name message id**.
2. **Inject a name** per new species (see below) and a **model** per new fish (the `.chr` injection, as
   Priscleen does — see [priscleen-fish-files.md](priscleen-fish-files.md)).
3. **The records store needs no change** (see below) — it holds an arbitrary kind int, so records for new
   species work automatically once the kind resolves a name and model.

Give every custom fish (Priscleen included) a **fresh** name id rather than reusing a vanilla slot's text —
this sidesteps the dedup gotcha entirely.

## Names — the global system message buffer

Fish names are **not** in the per-town talk mes. `GetFishMsgNo(kind)` returns a message id (30–49) that the
renderer's `[fbfe]` fish-name placeholder looks up in the ClsMes **system buffer** (`MakeMesWinTbl_system`
@ `0x14EB70` reads `ClsMes+0x16E0` → `GetTextLineDataTop_system` @ `0x14F520`, which reads the buffer at
`ClsMes+0x17A4`). `EditInit` loads that buffer from the **global** file `meswin/system14e.bin` (English) via
`SetBuff_system` — the same for every town — so **fish names are available everywhere; only the catch
*template* (msg 2000) is town-specific** (see [custom-fishing-spot.md](custom-fishing-spot.md) for the
catch-bubble injection).

The `.mes` buffer format (cracked via `GetTextLineDataTop` @ `0x14F4B0`): `u16 count`, `u16` (SetBuff's
`+0x17A8`/`+0x17AC` delta), `count × {u16 id, u16 wordOff}`, then text. A message's text is at byte
`2*(count + wordOff + 1)`; glyphs are 16-bit meswin codes, `0xFF01` terminates. Decode/encode with
`tools/mes_decode.py`.

### The name-dedup gotcha (verified)

The mes format **deduplicates identical text**: two message ids whose text is byte-identical point to the
**same** `wordOff`. Species 8 (id 48) and species 9 (id 49) both read "Umadakara" and both have `wordOff =
1040`. So **a naive in-place text overwrite of species 8's name also renames the real Umadakara (species 9)**
— in its fishing area *and* the records list.

Correct approach: **repoint, don't overwrite.** Give the custom species a fresh name id (via the `FishMsg`
extension above) whose entry points at newly-injected text, leaving the shared vanilla bytes untouched. For a
name that does **not** share text with any other id, an in-place overwrite that fits the original byte span is
safe — but the repoint approach is collision-proof by construction and is the recommended default.

## Records — a top-64 leaderboard keyed by species

The fishing records ("fish ranking") live at `*SaveData + 0x1E0`: **64 entries × 0x10 bytes**, each
`{ int kind @+0x00, float size @+0x04, 8 trailing bytes preserved }`. Empty = size ≤ 0 or kind < 0. Kept
sorted size-descending.

- `SetFishingRank(size, saveData, kind)` @ `0x157DC0`: fills the first empty slot, or evicts the smallest
  when full, then bubble-sorts by size. **`kind` is the species** — the same value passed to
  `GetFishMsgNo(kind)` for the name.
- `GetFishingRank(saveData, index)` @ `0x157F40`: returns the record at slot `index` (0–63) or 0.
- The records view (`FishRecordViewBoard` @ `0x1F35F0`) shows a **flat ranked list** (rank + size per row),
  and resolves the selected entry's **name via `GetFishMsgNo(kind)`** → `system14e.bin`.

Key consequences for custom fish:

- **Records key on the species kind**, so two fish sharing a kind are indistinguishable in the list. Each
  fish needs its own kind to be globally distinct — which is why Priscleen must stay on the exclusive slot 8,
  and why new fish need new species ids.
- **The record store has no per-species cap.** `+0x00` is a raw 4-byte int over 64 slots; it stores any kind.
  So once a new species (18, 19, …) resolves a name and model via the widened tables, its records display and
  persist correctly with **no change to the records code**.

> The mod already deduplicates the list to one best-catch-per-species for the "Arise Mardan" magic bonus
> (`Fishing.cs` `UpdateFishRecordsAndAriseBonus`) — see [fishing rank-list notes]. That is mod logic layered
> on top of the vanilla leaderboard, not a change to how the engine keys records.
