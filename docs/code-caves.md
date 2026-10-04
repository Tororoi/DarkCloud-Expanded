# Code caves — finding safe free memory for custom features

Goal: locate EE RAM regions the game never touches, usable for persisting custom
atla state, sidequest data, or injected code.

## The system (two halves)

**Static:** `tools/analysis/find_code_caves.py` parses the ELF (`~/ROMs/dc_extracted/SCUS_971.11`)
symbol table and reports address ranges no symbol claims, classified by segment.
`--seed <findings file>` writes them as CANDIDATE entries; `--annotate 0xADDR`
shows the nearest symbols to any address.

**Runtime:** `CodeCaveScanner.cs` (thread started with the others in
`SessionController.TitleMenu`, only while `DebugDiagnostics.Enabled`) passively sweeps all 32MB of EE RAM every ~45s in 8KB
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

## Dead functions as hosts

When the ELF cave band is full, a cave can take the body of a function nothing reaches in retail: its first word
becomes `jr ra` (so any caller returns at once) and the cave starts at +8. In use: `MemoryMapDump` in dun.bin
(printf-only, 332 B — `DunCave`, its callers nop'd) and `DebugInfomationDraw` in the main ELF (the debug
overlay behind `DebugStatus`, 3,952 B — `DebugInfoCave`, docs/shot-slot-sharing.md). Prove deadness by
scanning both binaries for `jal`/`j` words and pointer values naming the function (symtab entries past the loaded
segments do not count); dun symbols past dun.bin's 100,224 B (e.g. `Setsumei` at 0x1DC6E70) belong to other overlays
and are not hosts.

## Executing code from a cave

A cave found here can hold **DATA** freely. Running our own **native code** from one
was long believed to crash PCSX2 — but it works if the cave is written cold and reached
via a data-driven indirect call (repointing a dispatch-table entry), not an in-place
`j cave`. See [`cave-code-execution.md`](cave-code-execution.md).

---

# The mod's cave map

What follows is the reader's overview of every region the mod claims. The constants themselves (addresses, sizes,
word layouts, who writes what and when) live in the `Addresses/` files named below — those files ARE the map and
are kept exact; this section only says how the pieces fit. Addresses here are **guest** (what the game sees); the
C# `Memory.*` calls take the MMU form, guest + 0x20000000.

## One proven-clean heap tail: 0x01F10000 – 0x01FB4300

Swept clean by `CodeCaveScanner` across ~70 sessions and claimed as one `ModReserved` range. Every runtime cave
below the ELF segment is carved from it, in address order (`CodeCaveAddresses.cs`, class `CodeCaves`):

| guest | what | file |
|---|---|---|
| `0x1F10000 – 0x1F10100` | **the PNACH mailbox page** — 4-byte flag words the mod writes and the PNACH conditionals / cold-patched engine code read. FULL (`Mailbox.NextFree` = +0x100). | `MailboxAddresses.cs` |
| `0x1F10100 – 0x1F18100` | HarderEnemyAI per-species STB stubs, 32 × 0x400; the top eight slots are the Solar Flash programs | `CodeCaves.AiStub*` |
| `0x1F18300 / 0x1F18400` | Queens waterfall spray bias + emitter table (town only) | `CodeCaves.QueensSpray*` |
| `0x1F19000 – 0x1F1A800` | Mirage decoy redirect: per-slot target pointer table, decoy position, and the two cold-copied engine functions (`_GET_DISTANCE`, `_GET_POSITION`) reached through the STB dispatch table | `CodeCaves.PtrTable`, `DistCave`, `PosCave` |
| `0x1F19500 – 0x1FAE400` | CharacterClone: root CFrame, node pool (96 bones), cloth list/objects/buffers/anchors/bounds, motion channels, per-bone skinning and animation matrices, the weapon tree, and the software-skinned MeshCave (0x58000, sized for Goro). The Divine Beast cat and the Angel Gear prop share these caves top/bottom while Xiao is up; the cat also borrows the cloth caves as overflow. | `CodeCaves.*Cave`, docs/character-clone-footprints.md |
| `0x1FAE600 – 0x1FB0000` | **the runtime-data span** under the ELF segment: the water-redraw words, the cat copy queue and pair table, the borrowed-shot and shared-shot blocks, the lock-on factor table, and the per-feature words the dead-function caves read (charge radii, bomb reaction, camera pin, blade fall, WHP bill, magic-circle table, call request, pellet contacts, spear column, follow table, stars gate …). Pages that already carry runtime-written words, so PINE writes here are safe. | `WaterRedrawAddresses.cs`, `CodeCaves` (from `CatCopyQueue` on) |
| `0x1FB0000 – 0x1FB4000` | **the ELF cave segment** — ISO-baked code, loader-loaded, 16 KB-aligned. No runtime write may touch these pages. FULL. | `ElfCaveAddresses.cs` |
| `0x1FB4000 – 0x1FB4300` | **the cat block** — `BobberPtr` at +0 (the cold-patched FishLineStep reads it), the Divine Beast cat's runtime words from +0x94 to +0x2A8; the page ends at +0x300 | `CatBlockAddresses.cs` |

`0x01400000` (EnemyModelInjector's parameter block, deep in main BSS) is **not** part of this map: it was never swept,
is deliberately not in `ModReserved`, and the feature stays disabled until it is given a verified cave.

## Dead-function hosts

The ELF segment being full, later caves take the body of a function nothing reaches in retail: the first word becomes
`jr ra` (plus the return value its one caller expects) and the cave starts at +8. `DeadFunctionCaveAddresses.cs`
holds four hosts:

- `DunCave` — `MemoryMapDump` in dun.bin (0x1DAC070, 332 B): the cat guard-bypass.
- `DebugIfCave` — `DebugInfomationIF` (0x1B47C0, 3,712 B): the magic circles, call request, pellet sprite/plant/contact,
  gem damage, second-effect step/draw, blade spin, spear column (enemy, player and shot), no-drain, rock shadow,
  fall drive, confuse name.
- `DebugInfoCave` — `DebugInfomationDraw` (0x1B3780, 3,952 B): shared shots, steel level-up, guard mask, auto-guard,
  stride scale, camera pin, lunge gravity, blade fall, WHP bill, guard crush, follow.
- `DebugItemCave` — `DebugItemGetKey` / `DebugItemGetDraw` (0x22B240 / 0x22B5B0): confuse proc, stars step/draw.

Several of these form a once-a-frame **chain** hanging off the dungeon camera pass's epilogue (camera pin → blade fall
→ fall drive → follow → blade spin → WHP bill → call request): each cave's exit jumps to the next, so the engine's own
frame does the work and no mod thread races it.

Two older addresses stay in the files because the ISO patcher must recognise them: `DebugIfCave.GuardCrushFirst`
(an earlier guard-crush home) and `DebugInfoCave.PelletSprite` (an earlier sprite cave; the live one is
`DebugIfCave.PelletSprite`). The patcher accepts a hook that still points at either and re-aims it.

# The rules

## PINE write safety (page isolation)

A PINE write — any write from outside the EE thread — into a host page from which PCSX2's recompiler has executed code
trips the page's write protection and **SIGBUSes the PINE server thread**: the emulator dies within a second. Host
pages are 16 KB on Apple Silicon (4 KB on Intel), so a "code-free 4 KB neighbour" is not safe either. Hence:

- The ELF cave segment `0x1FB0000..0x1FB4000` starts 16 KB-aligned and its pages hold **no runtime-written data**. The
  `0x1FB2000..0x1FB4000` half is reserved for segment growth or ISO-baked read-only data only — never a runtime word.
- The segment can **never grow past 0x1FB4000**: the cat block and `BobberPtr` are runtime data on the next page.
- Runtime-written words go in the mailbox page or the runtime-data span under the segment — pages that hold no code
  (or that already carry runtime writes). A word a cave reads is still data; it goes there, never beside the cave.
- Patch-time data in a code page is fine (the glow palette tables at `ElfCave.CatGlowPalTables` are written into the
  ISO, never at runtime).
- A hot EE instruction is never poked at runtime. In-place code edits are applied **cold** (before the code first
  runs this session); overlay code goes through the ISO (`DunPatches`) or the PNACH.

The same rule is why `Memory.WriteByteArray` writes that must land atomically (STB bytes under a parked script PC,
tables read mid-frame) go through the batch writers: a per-byte round trip spans most of a frame.

## Address-ordered tables with size and end

Every registry (`ElfCave`, the dead-function hosts, `CodeCaves`) lists its caves in **address order**, each with its
size and end address on the line. Bin-backed sizes are the `.bin` file's byte size (`Resources/isoPatch`); hand-built
sizes are the instruction-word count × 4. A multi-entry bin occupies its whole span, not just the labelled entries
(`CameraNormSideBank`, 2,128 B). Overlap checks run against a **patched** ELF — a vanilla ELF does not yet contain the
neighbouring bins, so a check against it proves nothing.

## NextFree discipline

Each registry ends in a `NextFree`. To claim space: take `NextFree`, move it, and add the cave to the table beside
its neighbours. Never place a cave or a mailbox word from a patch-local literal; never write a bare `0x21F100xx`
outside `Mailbox`. A map that exists only as prose does not prevent collisions; a constant does. Both `Mailbox.NextFree`
and `ElfCave.NextFree` are currently at their bands' ends: new runtime words go to the free band under the ELF
segment (`CodeCaves`, from `0x21FAF4D0`), new code to a dead-function host.

## Capacity lives with the cave

A cave's size or slot count is declared beside its address (`AiStubMaxSlots`, `MaxNodes`, `ClothObjSlots`,
`ClothBufSize`, `MeshCaveSize` …), never back-computed from the gap to the next cave, and the code that fills it
bounds-checks against that constant (`CharacterClone` refuses to spawn past `MaxCloneNodes`). The per-bone clone
buffers are sized for the **largest** character, because three of them scale with bone count and sit immediately
before their neighbours.

## Registering with the scanner

Anything added to the map is also added to `CodeCaveScanner.ModReserved`, so the sweeper does not flag the mod's own
writes — except a region that has not been verified, which is left out on purpose so the scanner keeps reporting
the truth about it (the EnemyModelInjector block).

## The monster-pool carve

Caves that need engine objects at runtime (a borrowed shot config's region, the resident stars instance) carve them
from the floor's monster model pool (`ShotEffectPack.MonsterPoolAlloc`, a `CDataAlloc2`) **once per floor**, sign the
region (`"BSHT"` + a mark, 16 B below the base) and record the pool's used counter right after the carve. While the
live counter still equals the mark the region is the pool's top and is reused; once the loader has moved past it the
region is re-carved. The mod never allocates from the pool itself — it posts a request and the cave carves on the
engine's frame.

# Lessons behind the rules

Incidents that produced the rules above. They are recorded here so the code can state the rule alone.

**The Queens SIGBUS.** The ELF segment's first home was `0x01FAE700`, sharing a page with the water-redraw words at
`0x01FAE600–610`. `QueensSpray` ran every Queens frame, and the next `MizuRedrawTexGroup` write faulted at `0x1FAE60C`
— every Queens session. The segment moved to the 16 KB-aligned `0x01FB0000`, and the town-camera scratch words moved
off their code page into the mailbox page (`Mailbox.CameraStick` / `CameraEprev`). Angel Gear's gauge word, first
placed inside the segment, killed PCSX2 the first time the shield wrote it; it lives at `Mailbox.ShieldGaugeRate`.

**Growing the band past 0x1FB4000.** Extending the segment to `0x1FB4300` put code on the cat block's page; the cat's
PINE writes hit a compiled page and PCSX2 died on the first cat shot. The band-full remedies are head/tail splits
(`#SPLIT` in `build_ee_stubs.py`: `BorrowedShotsEnter` + `BorrowedShotsEnterTail`) and dead-function hosts.

**0x228BB0–0x22A210 is live.** The caves' first home in the main ELF was believed dead ("the CharaChange screen this
mod never reaches"). It is the dungeon SELECT quick-menu's character-change screen — `CharaChangeLoop` / `CharaChangeKey`
/ `CharaChangeDraw` — called from the dun.bin overlay (file offset 0x1DD0), which is why main-ELF-only xref analysis
mislabelled it unreachable. The caves broke the SELECT menu. The region stays byte-for-byte vanilla, and deadness is
now proven by scanning **both** binaries (see "Dead functions as hosts" above).

**The overlap that byte-verified clean.** `PatchIdleMotionOverride` was first placed at what is now `FishLineSplit+0x40`,
inside `fishlineSplitCaves.bin`; every Queens fishing session hung on a black screen. The placement check had run on a
vanilla ELF, where the fishline bin does not exist. Hence the address-ordered table with sizes, and checks against a
patched ELF.

**Raw literals and the squatting flag.** Thirteen mailbox slots once existed only in a header comment and were written
as raw `0x21F100xx` literals across five feature files. A reserved-but-unimplemented fishing flag sat on Mirage's
scene-gate slot; wiring it up would have read as "decoy up" and NOP'd the chara-loop gates with no clone present.
Every slot became a constant and `NextFree` the only way to claim one.

**CanalEvict at +0x40.** The canal evict flag was first given mailbox +0x40 — the word `town_camera_collision.s` uses
as its smoothed right-stick scratch (that stub grabbed the page directly, bypassing the allocator). A non-zero stick
read as a set evict flag and false-warped the player to the dock, while the mod's per-tick flag writes stomped the
camera's stick. The flag moved to +0x60, past the stub's E_prev quad, and +0x40/+0x50–5F are marked external.

**The cat outgrew the mailbox page.** The Divine Beast cat's words were first laid out in the mailbox page from +0x94
and grew past +0x100 — which is `AiStubBase`. Clip frames, range, hit slot and diagnostics were shared with, and wiped
by, the AI stubs. The words moved as a block (same offsets, new base) to the spare span at `0x1FB4000`
(`CatBlock.CatBase`). A feature that needs more than a few words gets its own block, never a bigger mailbox.

**The pair table over the shot block.** The cat copy queue's find/replace pair table once sat directly after its jobs,
which end at `0x21FAEF30` — 16 bytes short of `BorrowedShotBlock`. Every cat build wrote the texture pairs over the
borrowed shot's config: Babel's shockwave never entered, and a re-request built the effect from the trashed config and
crashed. The table lives at `CatCopyPairs`, named by the job.

**Unbounded growth inside the band.** Two silent overruns shaped the "capacity lives with the cave" rule: HarderEnemyAI's
stubs grew unbounded toward `PtrTable` (now capped at 32 slots), and the clone's per-bone buffers, sized for Ungaga's
67 bones, were overrun by Xiao's 79 — scribbling over the grafted weapon's root CFrame.

**Making room for Goro.** `MeshCave` was 0x34000 and excluded Goro, Ruby and Osmond. The room to size it for Goro
(0x57B30 → 0x58000) came from capping the AI stubs at 32 slots, trimming the node pool 128 → 96 bones (Osmond's 84 is
the real maximum) and packing the decoy tables out of a 0x10000 hole.

**The first hot-code poke.** The first value-changing PINE poke to a hot instruction (the 7 Branch Sword's status-break
path) killed the emulator the same second; identical-value writes were benign. The 7BS effect is applied post-hoc on
the SynthSphere (`SevenBranchSword.SevenfoldRiteEffect`), not by patching `SetStatusBreak`. Everything in
`WeaponAddresses.cs` is read or poked as **data**.

**The water redraw is moved, not duplicated.** A second `jal` to the water draw overflowed the shared per-frame VIF1
packet buffer and crashed; the redraw code relocates the draw to after the character inside MainDraw/DrawWater's own
footprint, and only two words of state (`WaterRedraw.*`) are runtime data.

**Retired spans.** Mailbox +0x70–0x7C once held freeze-hunt diagnostics (alloc probe / breadcrumb / shadow-skip), now
the town-swap words; `0x21FAF4D0..0x21FAF830` held two Solar Flash blocks and `0x21FAFC90..0x21FAFCB0` the follow
cave's first single entry — all free now and listed as such in `CodeCaves`. `XiaoMeleeFlinch` sat at `0x1FB1FA0`
before `CatGuardBypass` moved to dun.bin; the patcher re-hooks an ISO patched then.
