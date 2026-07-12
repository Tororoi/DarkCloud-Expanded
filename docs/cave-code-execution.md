# Executing custom code in a cave — cold cave + dispatch-table redirect

How to run our **own native EE code** in Dark Cloud without the recompiler crashing —
by hosting the code in a free cave and getting the game's *own* dispatcher to call it.
This overturns the long-held rule in this codebase that "cave execution crashes PCSX2."

Reverse-engineered / proven 2026-07-12 (Ungaga's Mirage `_GET_DISTANCE`). All addresses
are **native ELF** addresses; add `0x20000000` for the PCSX2/PINE EE address (repo
convention). See also [`code-caves.md`](code-caves.md) for *finding* the free memory.

---

## 1. The problem this solves

Some features need the game to run *modified* logic inside an existing engine function
(e.g. make `_GET_DISTANCE` read a per-enemy target instead of the global player). Two
techniques were previously the only safe ones, and both have hard limits:

- **In-place rewrite of existing code, applied cold** (ABS rollover, the Mirage aggro
  redirect). Safe, but you're stuck editing the function *in place*: you get exactly the
  instruction slots that are already there. Our `_GET_DISTANCE` rework needed one extra
  word than the branch window held, forcing a stack-frame spill of `param_1` — and *that*
  surgery broke enemy tracking. There was no clean way to fit it.
- **Cave + `j cave`** — writing native code to a free heap page and jumping into it from
  an in-place patch. This **crashed** every time (the "cave execution crashes PCSX2"
  rule). The recompiler chokes when an in-place `j` transfers into a page it treated as
  data and never invalidated as code.

## 2. The breakthrough

A cave **does** execute cleanly if you satisfy **two** conditions together:

1. **Write it cold.** Populate the cave via PINE at the cold window
   (`MainMenuThread.ApplyNewChanges`, in-game entry) — *before* anything executes. The
   recompiler has never touched the page, so when the EE first runs it, it compiles it
   fresh with no stale cache to trip over.
2. **Reach it via a data-driven indirect call, not an in-place jump.** Instead of
   patching a `j cave` into hot code, find a **function-pointer table** the game already
   dispatches through, and repoint one entry at the cave. The game's own `jalr` (a normal
   indirect call) enters the cave — the recompiler handles arbitrary indirect targets, so
   it just compiles and runs the cave.

The old failures were specifically the in-place `j cave` (condition 2 violated). Change
the *entry mechanism* to an indirect dispatch and the same cave runs fine.

This removes **all** in-place fit constraints — window size, delay slots, register
spills. Your cave function has the full ABI to itself.

## 3. The recipe

### a. Find a dispatch table

Look for where the engine stores pointers to the function you want to replace. For STB
external commands, they're dispatched through a table of 8-byte `{funcPtr, id}` entries.
Find the slot by searching the ELF for the function's address as a data word:

```
_GET_DISTANCE  (0x1E1D00)  funcPtr @ 0x2918A0   (PINE 0x202918A0)
_GET_POSITION  (0x1E1DF0)  funcPtr @ 0x2918A8   (PINE 0x202918A8)
```

### b. Byte-copy the original function into a cave

A compiled MIPS function is **self-contained** and relocation-free:
- `jal` targets are **absolute** within the 256 MB segment → work from anywhere in low RAM.
- `beq`/`bne`/`b` are **PC-relative** → a byte-copy preserves them (they branch within the copy).
- `gp`-relative loads and `lui/addiu` absolute-address idioms are position-independent.

So `memcpy(cave, func, size)` is a functionally-identical clone. `_GET_DISTANCE` and
`_GET_POSITION` are each `0xF0` bytes.

### c. Detour only the part you want to change

Rewrite the minimum. For `_GET_DISTANCE` we kept the whole vanilla body and only redirected
the player-address load in the `param==1` branch to a helper in the cave's free space:

```
cave+0x58  addiu a0, sp, 0x40    ; dest — unchanged
cave+0x5C  j     helper          ; was lui v0,0x1ea
cave+0x60  nop                   ; was addiu a1,v0,0x1d30  (j delay slot)
cave+0x64  jal   sceVu0CopyVector ; unchanged (helper jumps back here)
...
helper (cave+0x100):
  lw   t0, -0x6320(gp)   ; NowMonstorUnit
  lw   t0, 0x90(t0)      ; current enemy slot
  sll  t0, t0, 2         ; slot*4
  lui  a1, 0x01F3
  addu a1, a1, t0        ; PtrTable + slot*4
  lw   a1, 0(a1)         ; a1 = the per-slot target pointer (mod-maintained)
  j    cave+0x64         ; back into the sceVu0CopyVector jal
  nop
```

`a0` (set before the detour) is preserved through the helper, so the `jal` runs with the
right dest and our target. Note: because we own the cave, the helper can use *any* scratch
register (`t0`) and add as many instructions as it likes — none of the in-place fit pain.

### d. Repoint the dispatch slot (data write) at the cold window

```
Memory.WriteUInt(0x202918A0, caveGuestAddr);   // dispatch _GET_DISTANCE → cave
```

Do the copy + repoint from `ApplyNewChanges`. That's it — the STB VM now calls the cave.

## 4. Proving it before trusting it

De-risk with an **observable marker** before wiring real logic: byte-copy the function,
change one instruction to a clearly-visible effect, repoint, and watch. For `_GET_DISTANCE`
we rewrote `mov.s f12,f0` → `mtc1 $zero,f12` so the returned distance was always `0` —
enemies then attacked point-blank from any range. Seeing that (and no crash) confirmed the
whole function executed in the cave, `internal jal`s and all, before we built the real
per-slot helper.

## 5. Cost & where the cave lives

- Cave region used: `0x21F34000`, in the free gap `0x1F33400`–`0x1F40000` (~52 KB) inside
  the proven-clean band (`0x1F10100`–`0x1FB4300`; see [`code-caves.md`](code-caves.md)).
- Per hosted function: `0xF0` (copy) + ~`0x20` (helper) ≈ **`0x120` bytes**. Hosting both
  `_GET_DISTANCE` and `_GET_POSITION` is ~`0x240` bytes — **< 0.5%** of the gap.

## 6. When to use this vs. in-place

- **In-place cold rewrite** — still preferred when your change *fits* the existing
  instruction slots (a few words, no new registers). Zero extra memory, nothing to host.
- **Cave + dispatch redirect** — when the change doesn't fit in place (needs more words, a
  free register, or a proper frame), **and** the function is reached through a
  data-dispatch table you can repoint. Costs a few hundred bytes of cave; buys unlimited
  room and a clean function.

Reference implementation: `Mirage.ArmDistCave()` (Mirage.cs) + the STB dispatch addresses
in `DungeonAddresses.cs`.
