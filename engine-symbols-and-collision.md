# Engine symbols & movement/collision RE (Dark Cloud, SCUS_971.11)

Reference for reverse-engineering the EE code, captured while making bosses (esp. the
flyer **Dran**) behave on normal floors. The headline find: **the game executable ships
with a full symbol table**, which makes everything else tractable.

## 1. The ELF has a full `.symtab` (use this first!)

`ROMs/dc_extracted/SCUS_971.11` is a normal MIPS ELF **with section headers and a complete
symbol table** — including demangled C++ names. PCSX2's debugger reads these (that's why it
shows `CFrame::GetWorldPosition`, `CMonstorUnit::Step`, etc.).

- `.symtab` @ file offset `0x1cd5e0`, size `0x31610`, 16-byte entries.
- `.strtab` @ file offset `0x1a2510` (mangled C++ names, e.g. `Step__12CMonstorUnitFi`).
- `.shstrtab` @ `0x1a2480`. Section headers @ `0x2b120c` (16 sections).
- ~**4181** `STT_FUNC` symbols.

Sections of note: `main` (the loaded code/data, file `0x100`, vaddr `0x100000`, size
`0x1a2380`), `.relmain` / `.reltitle` / `.reldun` (relocations), and empty `title`/`dun`/`heap`
load stubs (overlays are loaded separately at runtime — see §2).

**Extract symbols** (symtab entry = `name:u32, value:u32, size:u32, info:u8, other:u8, shndx:u16`;
`info & 0xf == 2` ⇒ FUNC; `name` indexes `.strtab`):

```python
import struct, bisect
elf = open('SCUS_971.11','rb').read()
SYM_OFF, SYM_SZ, STR_OFF = 0x1cd5e0, 0x31610, 0x1a2510
def sname(o): return elf[STR_OFF+o:elf.index(b'\0',STR_OFF+o)].decode('latin1')
SY = []
for o in range(SYM_OFF, SYM_OFF+SYM_SZ, 16):
    nm,val,size,info,_,_ = struct.unpack('<IIIBBH', elf[o:o+16])
    if (info & 0xf) == 2 and val: SY.append((val, sname(nm)))
SY.sort(); vals=[v for v,_ in SY]
def sym(va): i=bisect.bisect_right(vals,va)-1; return SY[i][1] if i>=0 else hex(va)
```

vaddr ↔ file offset for the main segment: `fileoff = vaddr - 0x100000 + 0x100`.

**Disassembly:** capstone halts on R5900-specific opcodes (MMI/`lqc2`/`vmadd`…), so use a
hand MIPS decoder that *skips* unknown words and resolves `jal` targets through `sym()`. See
`/tmp/dcsym.py` style helper used during this work (covers lw/sw/lh/lb/addiu/branches/jal/
cop1 — enough to follow control flow and field offsets). Symbol-annotated `jal`s are the
fastest way to map a function's pipeline.

## 2. `dun.bin` dungeon overlay

`MWo3` overlay format. Header (first `0x80` bytes):
- `+0x00` magic `"MWo3"`; `+0x08` = **load vaddr `0x01dabd00`**; `+0x0c` main size `0x15dc0`;
  `+0x20` name `"dun.bin"`.
- Code starts at **file offset `0x80`** (loads at vaddr `0x1dabd00`). So
  `fileoff = vaddr - 0x1dabd00 + 0x80`. (This is the "+0x80 shift" noted elsewhere.)

The main ELF calls into the overlay (90 cross-calls); the overlay holds the **dungeon
collision mesh** (`CCollisionMDT`) and dungeon-flow state machine. Monster *code* does **not**
call dungeon collision directly — it goes through the shared collision helpers in `main`.

## 3. Position model: `loc` is a render cache of the `CFrame`

Two structures per enemy slot:
- **FloorSlots** array @ `0x21E16BA0`, stride `0x190`, 16 slots (`= G + 0x1e3d0`, where the
  monstor manager `G = MainMonstorUnit = 0x21DF87D0`). Gameplay fields (HP, facing, speed…).
- **Character/render object** @ `MainMonstorUnit + slot*0x3510 + 0x1fcd0`. A `CFrame`-backed
  object; its world matrix is the source of truth for position.

`FloorSlot+0x100/104/108` ("loc") is **written every frame by `CMonstorUnit::DrawMonstor`**
(via `CFrame::GetWorldPosition`, `0x128d60`) — it is a *cached copy* of the CFrame world
position, used by gameplay reads. The CFrame pointer is at **`FloorSlot+0xFC`**. Collision/
movement therefore operate on the **CFrame**, and `loc` follows. (A write-breakpoint on `loc`
lands in `DrawMonstor` — the render sync — not the mover; break on the CFrame translation
instead to catch the gameplay mover.)

Key transform helpers (named): `CFrame::GetWorldPosition` `0x128d60`, `CFrame::GetLWMatrix`
`0x1281b0`, `CFrame::SetPosition` `0x138fb0`, `CFrame::SetCollision` `0x12a190`,
`sceVu0ApplyMatrix` `0x121588`.

## 4. `CMonstorUnit` per-frame pipeline

`CMonstorUnit::Step(int)` @ **`0x1dd540`** drives each monster. Its call sequence (symbol-
annotated) includes: `run__10CRunScriptFi` (the **STB script interpreter / AI coroutine**,
called several times + `resume`), then movement/collision:

| addr (in Step) | calls | notes |
|---|---|---|
| `0x1de308` | `MoveCheck2` (`0x1dcdd0`) | gated — skipped if `[FloorSlot+0xA8] > 0` |
| `0x1de344` | `MoveChecMonster` (`0x1dd140`) | the normal mover (reads facing×speed); same gate |
| `0x1de4e4` | `CheckHit` (`0x149d50`) | **horizontal wall** collision vs floor polys |
| `0x1de928` | `CheckWidth` (`0x14af70`) | wall-width clamp |
| `0x1dea38` | `CheckHitVertical` (`0x14a080`) | **floor/ceiling** collision |

Other monster movers: `CMonstorUnit::MoveCheck` `0x1dc820`, `MoveCheck2` `0x1dcdd0`,
`MoveChecMonster` `0x1dd140`. Collision API: `MoveCheck` (`0x14a680`), `CheckHit`/`CheckHits`/
`CheckHitVertical`/`CheckWidth`, class `CCollision`/`CCollisionMDT` (`Intersection`,
`PickUpNearPoly`, `GetPolygon`, `LoadCollisionFile`).

### Per-slot collision gates (data-flippable)

- **`FloorSlot+0xA8`** — the `_STATUS_SET_COL_OFF` countdown (set by cmd `0x6c`, handler
  `0x1e2ef0`; arg = frame count). **`> 0` ⇒ Step skips `MoveCheck2` + `MoveChecMonster`** (the
  normal collision movers). Sibling fields in the same struct: `+0xA4` float (cmd `0x6a`
  `0x1e3500`), and `COL_ON` (cmd `0x6d` `0x1e2f50`) computes a radius from `+0x20/+0x24`.
  ⚠️ This is **not** the entity hitbox (Xiao's ranged hits Dran mid-air with it active) and
  **not** wall/terrain collision — holding it `=9000` did **not** make Dran phase walls.
- **`FloorSlot+0x88`** — the **`_STATUS_SET_FALL` flag** (set by cmd `_STATUS_SET_FALL`,
  handler `0x1e2bb0`; cleared by `CleanViewMonstor` `0x1dfacc`). Step reads it at `0x1de3bc`,
  `0x1de5d8`, `0x1de7cc`, `0x1deaa0`, `0x1dec10`; **`== 0` ⇒ skip `CheckHit`/`CheckWidth`**
  (the horizontal wall collision). **Flyers have it `0` already**, so that block is already
  skipped for Dran — i.e. `CheckHit`/`CheckWidth` are *not* what jams him.

The STB command handlers live in `main`; the monster command table is at **vaddr `0x2918a0`+**
(8-byte `(fnPtr, cmdId)` records). Examples: `0x20=_SET_MOVE` (`0x1e27c0`),
`0xD8=_SET_MONSTOR_MOVE` (`0x1e4ac0`), `0x6c=_STATUS_SET_COL_OFF` (`0x1e2ef0`),
`0x6d=_STATUS_SET_COL_ON` (`0x1e2f50`), `0xC8=_SET_MOTION` (`0x1e1710`).
`_SET_MONSTOR_MOVE` writes a facing vector to `FloorSlot+0x60/64/68` and speed to
`FloorSlot+0x80` (MovementBlend); `MoveChecMonster` consumes those.

## 5. Dran (c12a, TableIndex 78) flight — SHELVED (ranged-only)

Symptom: on a normal floor he hovers high over the player (loc height ~57; ground = 0) and
won't descend, so only **ranged** attacks (Xiao) reach him — melee (Toan) can't. He spawns
fine and is defeatable; spawn is intentionally left enabled (just omit him from a roster to
avoid a ranged-only fight).

**Conclusion (confirmed in-game): the problem is hover altitude, NOT wall collision.**
- He already **phases walls**: horizontal collision (`Step` → `CheckHit`/`CheckWidth`) is
  gated by the FALL flag `FloorSlot+0x88`, which is `0` for flyers. Forcing it to `1`
  re-enables collision and **freezes him** — proving both that `+0x88` is the wall gate and
  that the "stuck on a wall" look is hover, not a collision pin.
- Altitude is rewritten **every frame** via the CFrame hierarchy (loc = world matrix, not a
  single field), so any mod-side clamp (slower than 60 fps) is overwritten. No settable "fly
  height" field/command exists in the symbols. So altitude can't be held from C#.

Dead ends (all data-only, verified):
- Redirect flight/takeoff/charge `_SET_MOTION(0/7/2)` → ground motion: changes only the
  *animation*; he still propels off the ground (movement ≠ motion).
- Zero the flight/takeoff `_SET_MOVE` speeds in the STB: he still moves (separate mover).
- Hold `_STATUS_SET_COL_OFF` (`FloorSlot+0xA8`) high: gates the normal movers, not terrain — no phasing.
- Zero `FloorSlot+0x88` (FALL): already 0 for flyers; wall block already skipped.
- Clamp `CFrame+0x224` (+dirty `+0x24C`) or `loc` Z: overwritten per-frame; loc reads the
  world matrix, not that field.

**To resume:** break on the loc-height write (`FloorSlot+0x104`) and step *up* into the flight
code to find the **target-altitude value**, then patch it once in the STB (no per-frame fight).
That one-time STB altitude patch is the only viable data-only path to grounding him.

## Tooling notes
- Decode/annotate with the symbol table — never guess a function's purpose.
- R5900: capstone mis-decodes MMI/VU ops; use a hand decoder that skips unknown words.
- PCSX2 debugger: EE write-breakpoints resolve the indirect (`jalr`/vtable) calls that defeat
  static call-graph tracing. Registers shown rightmost column = the live 32-bit value.
