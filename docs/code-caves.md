# Code caves — finding safe free memory for custom features

Goal: locate EE RAM regions the game never touches, usable for persisting custom
atla state, sidequest data, or injected code.

## The system (two halves)

**Static:** `tools/find_code_caves.py` parses the ELF (`~/ROMs/dc_extracted/SCUS_971.11`)
symbol table and reports address ranges no symbol claims, classified by segment.
`--seed <findings file>` writes them as CANDIDATE entries; `--annotate 0xADDR`
shows the nearest symbols to any address.

**Runtime:** `CodeCaveScanner.cs` (thread started with the others in
`MainMenuThread.TitleMenu`) passively sweeps all 32MB of EE RAM every ~45s in 8KB
PINE batches, tracking 256-byte chunks. A chunk that is all-zero in every sweep is
clean; one that changes or holds data is not. Small entries (≤8KB) are re-verified
byte-precisely so seeds smaller than a chunk aren't polluted by neighbours.

Findings accumulate in **`CodeCaveFindings.txt`** (next to the exe, one file,
merged across sessions — never overwritten):

- `CANDIDATE` — static analysis only, not yet confirmed at runtime
- `CLEAN` — zero + unwritten in every sweep so far (`sweeps=`/`sessions=` counters
  show confidence; the header counts sweeps per mode — check dungeon coverage!)
- `REJECTED` — observed nonzero/written; kept in the file with `dirtied=date+offset`

## Memory map (from ELF program headers)

| range | what |
|---|---|
| `0x100000–0x2A2380` | file-backed code+data ("main") |
| `0x2A2380–0x1DABD00` | BSS — named buffers; unclaimed gaps between them are cave candidates |
| `0x1DABD00–0x1F06B00` | overlay region (title/dun swap here; dun symbols shift +0x80) |
| `0x1F06B00–0x1F80000` | heap (~486KB; tail above high-water mark may be free) |
| `0x1F80000–0x2000000` | stack (grows down; bottom may be free) |

## Best static candidates (pre-runtime-verification)

- ~26KB of unnamed BSS slack in `0x2A3700–0x2AA000` (CD/SIF subsystem area);
  biggest single gaps: `0x2A7304` (+0xC3C), `0x2A8C04` (+0xBEC), `0x2A4104` (+0x80C),
  `0x2A5AC4/0x2A62C4/0x2A6AC4` (3×0x7FC)
- heap tail `0x1F06B04` (+0x794FC) and stack bottom `0x1F80004` (+0x7FFFC) — huge,
  but only the runtime scanner can say which sub-ranges are safe
- named-table zero padding (e.g. `AttachList+0x621`, `ItemPutListTblN+0x418`) is
  **not** safe: those are real zero entries the game reads

## Caveats

- A sampling scanner proves *never written*, not *never read/executed*. Prefer
  regions whose static note shows they sit between unrelated buffers.
- Note: the mod itself writes game RAM (PNACH flag `0x1F10024`, patches) — those
  chunks self-reject, which is correct.
- Guest addresses in the findings file match ELF/Ghidra; add `0x20000000` for
  PINE/mod access.
