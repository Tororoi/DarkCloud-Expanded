# Shot-slot sharing — more than five monster shot configs on a floor

The monster shot pack (`NowShotEffect`, five `CSHOT_EFFECT` slots of 0xA160 bytes) is filled once at floor load: the
species loader enters each species' one or two shot configs and writes the slot each got into the species row; every
unit of the species carries the slot number and fires from it. A sixth config was refused and the species fired
nothing for the floor — worse, from a number that was no slot. With the enemy randomizer rostering more shooting
species, and two-shot species (Mr. Blare, Billy, Sam, Bishop Q, Heart, Black Knight and its mount) counting twice,
five was hit often.

The cave in `tools/stubs/shared_shots.s` shares the five slots among every config a floor needs. Nothing is grown:
the pack, its count, and the loader's dedupe stay vanilla. The mod side is `Enemies/SharedShots.cs` (arms the block,
reports the cave's events, charges the randomizer's budget); the ISO side is `ElfPatches.PatchSharedShots` and the
step hook in `DunPatches`.

## How it works

**Refused configs are remembered.** The loader's two pack calls (`SetupBaseModel` 0x1E01B0 / 0x1E0224, `jal
Entry__17CSHOT_EFFECT_PACK`) go through the cave's ENTER: a −1 becomes −(index + 2), which the loader stores in the
species row's +0x68/+0x6A like a slot number (it only skips the store for −1) and `SetupViewMonstor` copies to each
unit's block (+0xAC/+0xAE). The STB's `_SET_SHOT` only tests for −1, so a request for a shot in negative form is
posted as usual. Nothing else reads those fields (`CleanViewMonstor` writes −1 on a unit's cleanup).

**A firing unit acquires its config.** Step's two fire sites (0x1DEED0 / 0x1DEFD8, the `lui at,0x6` before the request
load) go through FIRE0/FIRE1. A slot number ≥ 0 costs the hook a stamp (the frame the slot fired — the LRU key) and the
`at` it replaced. A negative number with a pending request runs the acquire: a victim slot is picked — an empty one at
once; otherwise a QUIESCENT one (no sub-shot active, the eight flags at +0xA000) that is not the same unit's other
shot, least recently fired, with a slot no living unit refers to counting as never fired — and the config goes in.
No victim (every slot has a sub-shot in flight) → the request is cleared and that one shot is skipped.

**Every config is read from disc at most once per floor.** The victim's 0xA160 bytes (its IMAGE — pointers into the
pool, sub-shot state) are copied to an image store first. The wanted config comes back from its own image store when
it has been in the pack on this floor (a copy, no disc), else from disc: `Initialize__12CSHOT_EFFECT(slot)` then
`Entry__12CSHOT_EFFECT(slot, cfg, read_buffer, 0x26, our allocator, count)` — the vanilla entry, with a REGION carved
at the monster pool's top handed as the allocator: `[16 B signature][image store][the config's data]`, capped at the
rest of the pool. After the entry the pool's used counter moves past what the data took, and the slot's pristine image
is copied to the store. The pool is a bump allocator that never frees within a floor; regions and stores are permanent
for the floor and stale by their signature (magic "SHRG", the pool's counter right after the carve, the live counter
at or past it) once a new floor rewinds the pool. The count is 6 for a boss-class unit (monster type 2) and 2
otherwise, the loader's rule.

**References follow the swap.** Every species row and every unit block is rewritten: the victim's slot number → its
config's negative form; the wanted config's negative form → the slot. A unit that spawns later copies the row and is
right; a dormant unit whose config was swapped out re-acquires it when it fires.

**A free slot is filled early.** STEP is the dungeon step loop's chain head (`jal` at dun 0x1DB874C; it calls the
borrowed-shots keeper first, which runs the older chain). Each frame it looks for the first living unit whose shot is
in negative form and, when a slot is FREE — empty, or quiescent and referred to by no living unit — acquires the
config into it. One a frame, so the disc read lands before the first fire on most floors.

## Memory

The image store is the only new cost per config: 0xA160 bytes + 16 = 2,583 units of the pool (`SharedShots.
ImageUnits`), for a config swapped out at least once (a from-disc region carries its own). The config's data costs
what it always did — the species footprints the randomizer sums already include their shots — only later, at the
first acquire instead of the load. The randomizer charges every roster one image store per distinct config it carries
(`ImageCost` / `NoteConfigs` in both roster builders and the theme fit sum), reading the species table's two indices.

An entry needs headroom before it is attempted: block +0x18 / +0x1C (mod; 0 = 24,000 / 48,000 units for two / six
sub-shots) plus the store. The figures are conservative on purpose: `Alloc__14CDataAlloc2` answers an overflow with an
endless loop, and the allocator's cap is the whole rest of the pool, so an entry only hangs if the data exceeds
everything left. What an entry takes is deterministic per config and count: (mds + mot bytes) / 16 for the model and
motion copies, plus `count × CopyFrameVu1` (about 1.4–1.7 × the mds size per copy). Measured with six sub-shots:
t_boll 19,867, i_boll 20,540, f_boll_3 21,167, e115a_ex 21,235, b_boll 21,695, e114a_ex 23,822; with two the same
configs need about half. The event log prints each entry's units, so the thresholds can come down once more are seen.
Effect containers hold no textures (`.mds` + `.mot` + `.cfg` only), so the texture manager's block 0x26 is untouched
by an entry.

## The block and the log

`CodeCaves.SharedShotBlock` (0x1FAF200, 0x270 B, runtime data): the magic "SHRE" the mod writes last (without it the
cave leaves every refused config skipped at fire — the ISO alone stays harmless), the frame counter, counters (disc
entries, restores, skipped fires, no room), the two headroom words, five stamps, the 34-entry config table {image
store, mark}, a 16-entry event ring and its write index, and the allocator handed to the entry. `SharedShots` arms it
at start, zeroes the stamps on each new floor, logs the floor's pack layout and every config waiting outside it about
1.5 s after the load, and drains the ring: which config entered which slot over which, from disc (with the units it
took and the pool's state) or from its store, skips, and no-room events.

## Where the cave lives

The ELF cave band (0x1FB0000–0x1FB4000) is full and may not grow. The cave (2,864 B) is hosted in the body of
`DebugInfomationDraw` (main 0x1B3780, 3,952 B): the developers' on-screen debug overlay, gated by `DebugStatus` and
called only from dun.bin's `DrawProcess` behind a debug flag. Its first word becomes `jr ra` (the caller returns at
once) and the cave starts at +8 with a four-entry branch table at fixed offsets (`CodeCaves.DebugInfoCave`), so the
hooks never depend on the assembly's layout. The same pattern as `MemoryMapDump` in dun.bin (`DunCave`).

## Register notes for the hooks

- Fire sites: `a0` = unit + idx × 0x30 (the unit's shot records), `a3` = idx, `s5` = the unit; `at`/`v0`/`v1` are dead
  (`v1`/`v0` are re-set right after the return). The hook's `jal` leaves the vanilla delay slot `addu at,a0,at` in place
  and re-forms `at = a0 + 0x60000` before returning. The slow path saves a0–a3 and t0–t9 around its calls.
- Loader sites: the pack call's arguments are already in a0…t1; `v1` holds the config's index × 4, the cave's source
  for the negative form.
- The acquire runs inside `Step__12CMonstorUnit` or the step loop; the disc read is the same synchronous `LoadFile` +
  `wait_now_loading_vsync` the borrowed-shots keeper proved from the same loop.

## Lessons

- A `CSHOT_EFFECT` slot is a plain value: copying its 0xA160 bytes in and out swaps the config it holds, because
  everything it points at (frames, the sub-shot objects) is pool data that stays where it was for the floor.
- `Entry__12` does its own `LoadFile` from the config's name and needs the slot's config pointer at 0 — `Initialize`
  first. It returns 0 only for a non-empty slot or a null config.
- The pool overflow is a hang, never an error: check headroom before every entry and keep the allocator's cap honest.
