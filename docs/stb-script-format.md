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
- **Label 256 is not spare.** It is the fishing script in Norune, but in an ordinary town it is the town's
  own script — overwriting it leaves the screen black on load.
- **`EventMode`'s `default:` branch is `GameMode = 1`.** A script that returns without setting a return
  code silently drops the player back to walking. That is how quitting fishing is implemented; it is also
  why a bait script that just returns will end the session.
