# The `.stb` script format and its VM

Dark Cloud's town and monster logic runs on a small stack VM (`CRunScript`, interpreter
`exe__10CRunScript` @ `0x23E080`). This document is the format, reversed from the interpreter rather than
inferred from usage — every time we guessed at it from example bytecode we got it wrong, usually in a way
that only showed up as a freeze three test cycles later.

Everything here is verified against `gedit\e01\event.stb` (Norune, `data.dat` @ `0x2662D000`).

---

## 1. File header

| offset | field |
|---|---|
| `+0x00` | ? |
| `+0x04` | ? |
| `+0x08` | **`codeBase`** — file offset where code begins (`0xE8` in e01) |
| `+0x0C` | **`labelTable`** — file offset of the label table (`0x40` in e01) |
| `+0x10` | **`labelCount`** |

Confirmed by `check_program__10CRunScript`, which walks `header + *(header+0xC)` for `*(header+0x10)`
entries, and by `load__10CRunScript`, which caches `this->codeBase = header + *(header+8)`.

**`codeBase` is the origin for every offset the bytecode carries** — strings, jump targets, and funcdata
pointers are all relative to it, *not* to the start of the file. Getting this wrong is why our first
string pushes decoded as `"1/chara/"` instead of `"chara/c01d_turi.chr"`.

## 2. Label table

`labelCount` entries of 8 bytes, at `labelTable`:

```
+0x00  u32  label id        (e.g. 256, 134, 133)
+0x04  u32  offset of the label's FUNCDATA   (a FILE offset, not codeBase-relative)
```

## 3. `funcdata` — 56 bytes, and a label is just a function

**A label's table entry does not point at code. It points at a `funcdata` struct**, and the code follows
it. `call_func__10CRunScript` (`0x23DBB0`) gives the layout:

```c
this->varsBase = sp - fd[3] * 8;                          // fd[3] = ARGUMENT count
this->sp       = varsBase + fd[2] * 8;                    // fd[2] = TOTAL locals (frame size)
memset(varsBase + fd[3]*8, 0, (fd[2] - fd[3]) * 8);       // zero the non-argument locals
return this->codeBase + *fd;                              // fd[0] = CODE offset (codeBase-relative)
```

| offset | field |
|---|---|
| `+0x00` | **code offset**, relative to `codeBase` |
| `+0x04` | ? (always 0 in the labels checked) |
| `+0x08` | **local count** (frame size, in 8-byte stack slots) |
| `+0x0C` | **argument count** (args occupy the first locals; the rest are zeroed on entry) |
| … | padding to 0x38 |

`sizeof(funcdata) == 56`, verified across every label in e01: `codeBase + fd[0] == labelOffset + 56`
without exception.

```
 label   funcOff    fd[0]   code       locals  args
   256  0x083D4     33572  0x0840C         27     0
   134  0x0F2F4     62020  0x0F32C         10     0
   133  0x111E0     69936  0x11218          1     0
     1  0x10688     67032  0x106C0          1     0
```

So the "mysterious 4-slot header" we worked around for weeks is not a header at all — it is the tail of
the funcdata struct. `header[0].op` "being the local count" is simply `fd[2]`, which happens to sit at
`funcOff + 8`.

**To synthesise a label:** write code at `funcOff + 56`, and set `fd[2]` (at `funcOff + 8`) to the number
of locals you use. `fd[0]` already points there, so it needs no change.

## 4. The call stack (`RS_CALLDATA`)

12 bytes per frame, pushed by `call_func` and popped by `ret_func`:

```
+0x00  return PC  (a vmcode_t*)
+0x04  saved varsBase
+0x08  saved funcdata*
```

## 5. Instructions

12 bytes each: `{ u32 op, u32 a1, u32 a2 }`. **Instructions are 12-byte aligned relative to their own
function's start, not to the file** — different labels sit at different phases mod 12, which is why a
naive `range(0, len, 12)` scan silently misses half the script.

| op | name | operands / effect |
|---|---|---|
| 1 | **PUSHVAR** | `a1` = var index, **`a2` = addressing mode**. Pushes the *value*. |
| 2 | **PUSHVARREF** | same, but pushes a *pointer* to the var (stack type 3) |
| 3 | **PUSHCONST** | `a1` = type: 1 = int (`a2`), 2 = float (`a2` = IEEE bits), 3 = string (`a2` = offset from `codeBase`) |
| 4 | **POP** | `sp -= 8`. **Not a jump** — we had this wrong. |
| 15 | **RET** | |
| 16 | **JMP** | `pc = codeBase + a1` |
| 17 | **BR_FALSE** | pops; branches to `codeBase + a1` if false |
| 18 | **BR_TRUE** | pops; branches to `codeBase + a1` if true |
| 19 | **CALL_FUNC** | `a2` = offset (from `codeBase`) of a **funcdata**, *not* of code |
| 21 | **EXT** | `sp -= a1 * 8`, then dispatch. **`a1` counts the command id itself.** |
| 23 | **YIELD** | suspend until next frame |
| 24 / 25 | AND / OR | pop two, push result |

### The addressing mode is in `a2`

`exe()` case 1 and case 2 switch on **`a2`**, not `a1`:

- `1` = direct — `vars[a1]`
- `2, 4, 8, 0x10, 0x20` = indirect / array forms, which pop an index first

**`a2 = 0` matches nothing, so the instruction pushes *nothing at all*.** The stack then runs short, `EXT`
reads garbage as the command id, and the VM derails. That is what froze the game the first time we tried
the bait menu.

### Type 3 is "pointer", not "string"

`push_str` does `push(codeBase + a2)` — a string operand is just a pointer into the file. The same type 3
is what `PUSHVARREF` pushes for a variable address. This matters because it is the only way a command can
return anything.

## 6. Commands (`EXT`) and how they return values

The dispatch table is an array of `{ handler, id }` — **handler first**. Reading it the other way round
shifts every command by one and turns `_LOAD_FISHING_DATA` (998) into `_LOAD_MAIN_CHARA` (999). Anchor on
the known pair `{ handler = 0x1969A0, id = 998 }`; there are 305 commands, ids 1..1000.

`EXT` **pushes no result**. Commands return values by writing through a pointer argument:

```c
void SetStack(RS_STACKDATA *arg, int value) {
    if (arg->type == 3)                // must be a POINTER
        *(int *)(arg->value + 4) = value;
}
```

So `_GET_RANDOM(&out, max)`, `_GOTO_CHANGE_ESA(&chosenItem)`, `_LOAD_SYNC(&stillBusy)`. Any "return" in
this VM is an out-parameter.

## 7. Waiting on the engine

Two commands report "am I still busy?", and both are the out-pointer shape:

| id | command | reports |
|---|---|---|
| 34 | `_LOAD_SYNC` | a background disc read is still in flight (`ReadBGSync`) |
| 502 | `_CHECK_FADE` | a fade is still running (`EdFadeOutCheck`) |

The idiom is a four-instruction loop. Norune's `call_func 400` — opaque until the funcdata format fell
out — is exactly this:

```
L:  EXT 34 (&v)        ; still loading?
    PUSHVAR v
    BR_FALSE done
    YIELD
    JMP L
done:
```

**Do not count frames instead.** `_LOAD_ITEM` builds an item frame out of the read buffer, and if the read
has not landed it builds one out of nothing; the game then calls through a garbage pointer and dies with
*"Jump to unaligned address (PC: 0x00000013)"*. A 5-frame spin lost that race; 10 might. It is a crash,
not a cosmetic glitch.

### Why `GetReadBGFile` is a trap

`_LOAD_ITEM` gates on `GetReadBGFile(i) != NULL`, which tests `bg_read_info[i][0]` — and `LoadFileBG` sets
that at **queue** time. `ReadBG` shows the real state machine:

```c
entry[0] = 1                    // queued            (LoadFileBG)
entry[1] = sceCdRead handle     // read issued       (ReadBG)
entry[2] = 1                    // ACTUALLY COMPLETE (ReadBG, after sceCdSync, no error)
```

`bg_read_info` is at `0x01CBB0C0` (MMU `0x21CBB0C0`), 32 slots of `0x9C` bytes. `entry[2]` is the only
honest completion flag — but a script should just use `_LOAD_SYNC`, which checks it for you.

## 7b. Reading the pad — X is `0x20` to a script, not `0x40`

`_GET_PADDOWN(&out)` does **not** hand back the raw pad. It pipes it through `exch_ok_cancel`, which swaps
bits `0x20` and `0x40`:

```c
v = pad & ~0x60;
if (pad & 0x20) v |= 0x40;      // Circle -> reads as 0x40
if (pad & 0x40) v |= 0x20;      // Cross  -> reads as 0x20
```

So engine code (`EdMoveChara`, item pickups, ladders) tests **`PadDown(0x40)` for Cross**, but a *script*
testing `0x40` is testing **Circle**. Scripts want `0x20` for X. This is not a display-language thing —
it is an unconditional swap in the command handler.

## 7c. Prompting for X: the `simple_event` idiom

A type-3 event point fires its label **the instant the player is in range** — `EdMoveChara` has no button
check for it (only item and ladder points test the pad). So a "walk up, see a `!`, press X" interaction is
built in the SCRIPT, not the point, and it hangs off the same rule as everything else here:

> **A script that returns without yielding is a `simple_event`** — `EdEventInit` runs it, sees it finish,
> and never enters event mode. The player keeps walking.

Which means a script can run *every frame* while you stand near a point, cheaply, and only commit when it
wants to:

```
_DRAW_EXCLAMATION_MARK()      // command 10 — a PER-FRAME flag (EdEventInit clears it), so re-assert it
_GET_PADDOWN(&v)              // command 1
if (!(v & 0x20)) RET          // no X -> return WITHOUT yielding: still just a simple event
<the real thing>              // yields -> promoted to a real event, GameMode = 0xE
```

Interaction radii, read off every town's points: **doors (type 1) = 10**, **item pickups (type 2) = 15**.
Those are the two things you walk up to and press X on, so they are the right scale for a prompt.

## 8. Runtime

| what | where |
|---|---|
| town event VM object | `CRunScript` @ `0x21D4A430` (`EdEventInit` calls `reload__10CRunScript(0x1d4a430, …)`) |
| current frame's locals | `*(CRunScript + 0x28)` — an array of 8-byte `{ type, value }` |
| loaded town script | see `TownScript.Base()` |

A local is an `RS_STACKDATA`: `+0x00` type (1 = int), `+0x04` value. The mod can read and write them, but
be careful — they belong to *whatever event is currently running*, so writing one unconditionally will
corrupt an unrelated town event's frame.

---

## Practical notes for synthesising scripts

These are the things that cost us a test cycle each:

- **A script with no `YIELD` is not an event.** `EdEventInit` *runs* the script; if it returns without
  yielding, the engine flags it `simple_event` and never enters event mode — so `EventMode` never runs,
  and nothing ever reads the event's return code. Yield at least once.
- **Labels tile the code region.** A label's code runs until the *next* label's funcdata offset, so
  adjacent spare labels can be merged into one arena and written straight through. That is the only way a
  script bigger than one label fits.
- **RETIRE every label an arena swallows.** The later labels in the run keep their table entries, and their
  `codeOffset` now points into the *middle of your bytecode*. If the town ever dispatches one, the VM reads
  your data as a `funcdata`, takes a garbage code offset out of it, and jumps into nowhere. Rewrite their
  ids to something nothing requests. And give your own script a fresh id too (nothing real goes above 310),
  so a town event cannot dispatch *into* it either.
- **Label 256 is not spare.** It is the fishing script in Norune, but in an ordinary town it is the town's
  own script — overwriting it leaves the screen black on load.
- **`_CLEAR_EVENT_BUFF` is a bump-allocator RESET, not a tidy-up.** It rewinds `EdEventBuffer` to its base,
  and the fields it rewinds belong to the allocator at `0x1D1B360` — the same one `_LOAD_FISHING_DATA`
  allocates `fishing.pak` and the fish from. The item-load recipe (`_CLEAR_EVENT_BUFF` →
  `_ACTIVE_FILE_BUFFER` → `_LOAD_ITEM`) therefore **must not run after a fishing load**: it drops the new
  item on top of the fishing data. The session keeps working, because that memory is already in hand — but
  the arena is corrupt, and the next thing to allocate from it (area streaming, when the player walks far
  enough) crashes. Norune loads bait only from label 134, and this is why.
- **Fishing borrows town memory; put it back on exit.** Loading the 1.8 MB fishing model and the bait
  disturbs two shared resources the town owns. `_CLEAR_VILLAGER_BUFF` (which Norune calls on entry to make
  room) rewinds the NPC buffer — do NOT call it, or the model lands on live villager data and the game
  crashes when you walk to where an NPC stands. And the texture manager reuses blocks, so a session
  overwrites one villager's texture block (a single NPC renders garbled until the area reloads). The exit
  script must **`_LOAD_VILLAGER` (57)** — a no-arg full reload of the map's NPCs and their textures — behind
  the fade, followed by a `_LOAD_SYNC` wait. This is what Norune's un-ported exit helpers do.
- **`EventMode`'s `default:` branch is `GameMode = 1`.** A script that returns without setting a return
  code silently drops the player back to walking. That is how quitting fishing is implemented; it is also
  why a bait script that just returns will end the session.
