# STB script VM & the Ice Queen fight (Dark Cloud, SCUS_971.11)

Reverse-engineering of the monster-script bytecode VM (`.stb` files) and the Ice Queen (c13a)
boss fight, captured while making bosses run on normal floors. Companion to
`engine-symbols-and-collision.md` (the ELF has a full `.symtab` — resolve everything by name).

## 1. The STB VM is fully symbolized — `exe__10CRunScript`

The script interpreter is `exe__10CRunScriptFv` (the dispatch loop) **@ `0x23e080`**. Supporting
funcs (all in `.symtab`): `run__10CRunScript` `0x23de70`, `resume` `0x23de40`,
`call_func`/`ret_func`, `push_int/float/str/ptr`, `pop`, `GetStackInt/Float/String` (×3 copies at
`0x18a350`/`0x1bb930`/`0x1e1640`), `SetStack`, `chk_int` `0x23d740`, `is_true` `0x23d7b0`,
`load`/`reload`, `check_program`, `skip`.

### Instruction encoding — `vmcode_t` is **12 bytes**
`(op:u32 @+0, a1:u32 @+4, a2:u32 @+8)`. Dispatch (exe @0x23e080):
```
a1 = vmcode->op            ; lw 0(vmcode)
if (a1 >= 31) return
handler = *(0x29fb80 + a1*4)   ; jump table, 31 entries
jr handler
```
Normal advance = `vmcode += 12` then re-dispatch (loop tail @ `0x23f554`).

### Opcode table (jump table @ `0x29fb80`, ops 0–30)
| op | meaning | notes |
|---|---|---|
| 0, 22 | nop / advance | (same handler as the loop tail) |
| 1 | push typed | type @+8 (1/2/4/8/16/32 = int/float/str/ptr/…), value @+4 |
| 3 | **push** | type @+4 (1=int, 2=float), value @+8 |
| 5–8 | compare (`==`,`!=`,`>`,`<`) | pop 2, push bool |
| 9,10 | DIV, MOD | (with div/modby0error) |
| 11,12,13,26 | unary (pop→push int/float) | |
| 14 | binary op | pop 2, push |
| 15 | **RET** | ret_func |
| 16 | **pop** (discard 1) | |
| 17 | **BR_IF_FALSE** | pop cond; if false, jump to **target @+4** |
| 18 | **BR_IF_TRUE** | pop cond; if true, jump to target @+4 |
| 4 | **JMP** | unconditional; target relocated at load (often 0 in file) |
| 19, 27 | **call_func** | call a sub-program |
| 21 | **ext** (external command) | count @+4; **cmdId = first pushed value**; dispatched via the monstor command table |
| 23 | **YIELD** | advance +12 then return from exe = wait one frame |
| 24, 25 | compare (2-operand) | |
| 29, 30 | sin, cos | |

### Commands (op 21 = ext)
A command is emitted as: `push(cmdId); push(arg)…; ext(count = cmdId+args)`. The cmdId resolves
through the **monstor command table @ vaddr `0x2918a0`** (8-byte `(fnPtr, cmdId)` records; handlers
demangle to `_SET_GLOBAL_INT__FP12RS_STACKDATAi` etc.). Useful cmdIds: `0x24`=_SET_POSITION
(`arg1→worldX, arg2→height, arg3→worldY but NEGATED`), `0xc8`=_SET_MOTION, `0xdc`=_SET_GLOBAL_INT,
`0xdd`=_GET_GLOBAL_INT, `0x0a`=_GET_DISTANCE, `0x0b`=_GET_POSITION.

### Disassembly gotchas
- Each program/label has its **own 12-byte grid** at a different file alignment (init vs AI differ).
  Anchor on the **codeOff (`+0x54` in the STB header)**; for c13a the AI grid starts at `0x2914`.
- `b.find` of the `(3,1,cmdId)` push pattern yields **false matches** off-grid — only trust offsets
  on the program's 12-byte grid.
- **JMP/branch targets are relocated at load** — the static file stores 0 (JMP) or file offsets
  (BR_IF_FALSE a1). Can't always follow them statically.

## 2. Global-int companion protocol

`_SET/_GET_GLOBAL_INT` (cmd `0xdc`/`0xdd`); the array is at runtime **`0x21D8FC80`** (64 ints,
`global[i] = base + i*4`). The Ice Queen and her companions coordinate purely through it — she never
positions, moves, or spawns them; she only flips flags they read.

| companion (STB / TableIndex / eid) | reads | role |
|---|---|---|
| **korinoya** (c13_korinoya / **idx 76 / eid 84**) | [2] | the ice-arrow EMITTER — has `_SET_MOVE` + `_STATUS_GET_HITDMG_VOL`; sets [0]=1, [3]=1, [2]. ⚠ EnemyData.cs mislabels idx 76 "kori"/"Ice Arrow" (4-char truncation; its real ModelCode is `korinoya`, STB `c13_korinoya`). **Its floor-slot is STATIONARY** (see §5) — the visible homing arrow is a render-layer transform, not this slot's position. |
| kori (c13_kori / idx 102) | [3] | static ice **source/effect** — no movement, no damage |
| baria (c13_baria / idx 101) — **shield** | [0],[1] | [0]=5, [1]=2/0 |
| i_meteo (c13_i_meteo / idx 103) — **meteor** | [4] | player-relative; carries own `_SET_DMG_COL` damage |
| i_tatumaki (c13_i_tatumaki / idx 104) — **tornado** | [1] | static `_SET_DMG_COL` box |
| reiki (c13_reiki / idx 92) | [5] | ends the cycle → loop-back; sets [0]=0, [5]=0 |
| tatumaki (c13_tatumaki / **not a roster species**) | [1] | spawned by the boss-floor setup, not the roster |
| **Ice Queen (c13a / idx 80)** | reads [0],[6],[7],[8],[3],[4],[5] | sets [0]–[8] |

`global[0]` is the **master "who's acting" handshake**: korinoya=1, kori=3, i_meteo=4, baria=5; it
cycles `0→1→3→4→…→0` and **her dispatch advances on it**. So the companions must *ack* (`[0]`/their
flag) for her to progress. `[6]/[7]/[8]` have no companion reader — they are her **internal phase
counters** (she cycles them `0→1→2→0` herself in the genuine fight).

**baria, korinoya, and reiki hardcode the boss as slot 0** (`_GET_MONSTOR_POS(0)` etc.) to position
themselves relative to Ice Queen. The roster places her in a random slot, so these three must have
those slot args **patched to her real slot** (see §4) or they can't position/fire/ack.

## 3. Ice Queen AI = a phased dispatch loop (the stall)

Her AI program (grid @0x2914) is a **loop**: each iteration evaluates conditions, runs ONE phase,
then `JMP`s back to the top. Phase order (by the `_SET_GLOBAL_INT` she emits):
```
init: reset global[0..8]=0
block 1: set[6]=1, set[7]=1, set[8]=1, CHK_ROTATION→set[2]=1   (each + SET_MOTION)
block 2: set[6]=1, set[7]=1, set[8]=1
   SET_GLOBAL[3]=1   ← ICE ARROW   (@0x3a60)
   SET_GLOBAL[4]=1   ← METEOR      (@0x3b98)
   set[0]=0, set[3]=2
   GET[5]→set[5]=1   ← reiki
   SET_MOTION(20/21)
```
She uses `_GET_DISTANCE ×3`, `_GET_POSITION ×4`, `_CHK_USER_INNER_PRODUCT`, `_GET_RANDF` — so phase
selection is geometry/state-driven.

**Genuine Shipwreck fight:** reaches `[3]/[4]` freely (ice arrow + meteor fire at 100% HP, dist
24–77 — NOT distance/HP gated); she walks/chases the player.

**Spawned on a normal floor (chest):** she emits `[6],[2],[7],[8]` (block 1) then loops, never
advancing to `[3]/[4]`. **Root cause:** her dispatch advances on `global[0]` (the master handshake),
which only changes when companions *ack* it. baria/korinoya/reiki hardcode the boss at slot 0; the
roster puts her in a random slot, so those companions look up the wrong slot, can't activate, and
never ack `[0]` → it sticks at 0 → she loops block 1. (Earlier dead-end theories — patching the
`BR_IF_FALSE` at 0x3538/0x39d0, which are unreached after a `JMP`-to-top; and "korinoya is missing"
— were wrong: korinoya IS idx 76, just slot-mis-referenced.)

### Resolution
1. **Slot-patch** baria/korinoya/reiki `_GET_MONSTOR` args to her real slot (like the shield fix).
   This lets korinoya ack the handshake and the dispatch loop runs end-to-end (no stall).
2. **korinoya the arrow** (see §5 — the "Dran-class movement limit" theory here was WRONG): its
   floor-slot position never moves even in the genuine fight. The arrow is a render-layer transform
   on the CCharacter's CFrame, decoupled from the floor slot.
3. **Global stand-ins** drive the rotation so the *other* attacks fire and she keeps looping:
   - korinoya `[2]→ set [3]=1,[0]=1,[2]=0` (fires the kori icicle effect + advances the handshake),
   - reiki `[5]→ set [5]=0,[0]=0` **and reset `[6]/[7]/[8]=0`** (ends the shield phase + lets block 1
     re-run so the rotation loops instead of stalling after one wave).

## 4. What works (Ice Queen, data-only, in the mod)

`EnemyModelInjector.cs` / `BossScriptPatcher`:
- **Spawn** via `BossInfo(80) = (codeOff 0x2914, initOff 0x2B44, runScriptOff 0x288C)`.
- **Death + fade**: `CollapseMotion(80)=11`; label-120 `_RUN_SCRIPT` → `_SET_MOTION(collapse)`.
- **Reachable placement**: translate the whole fight onto an active **chest tile** (chests are
  guaranteed walkable, available pre-spawn). `_SET_POSITION` rewrite: **argX = chestX, argY =
  −(chestY + clearance)** because **arg3/Y is world-Y negated** (arg1/X is direct, arg2 = height).
  Write ONCE per floor (rewriting every tick freezes the running interpreter). `ChestClearOffsetY`
  nudges her off the chest object.
- **Companion slot-patches** (`PatchShieldTarget`): baria (codeOff 0x874), korinoya (codeOff 0x7D0,
  slot args @0xB3C+0x14 / 0xBD8+0x14), and reiki (codeOff 0x3AC — shares it with i_tatumaki, so
  disambiguate) all hardcode the boss as slot 0; we rewrite those `_GET_MONSTOR` slot args to her
  real slot so they track her.
- **Handshake stand-ins** (`KorinoyaStandIn`): drive `[2]`→`[3]/[0]` (icicle + advance) and
  `[5]`→`[0]=0` + reset `[6]/[7]/[8]` (loop back), so her rotation cycles.
- **Miniboss exclusion**: `Enemies.BossEnemies` (incl. IceQueen 113 + IceArrow 84) filtered out of
  `MiniBoss` eligibility so the fight entities aren't scaled/buffed.

**Net result:** spawns on walkable ground, reachable, killable, dies with collapse/fade; shield
cycles; the rotation loops (meteor + tornado + ice-arrow icicle effect). The phase state-machine
deadlock is **solved**; the remaining gap is the ice-arrow homing flight — see §5.

## 5. The ice arrow is decoupled from the floor slot (genuine-arena log, map 800)

A full capture of the **genuine** Ice Queen fight (`[IQmap]`/`[IQobs]`/`[IQhome]` diagnostics) overturned
the earlier "korinoya is the flying projectile, its slot position is the arrow" model:

- **The genuine boss arena is a FIXED map**: `map_no=800`, and the `mapparts` pointer is **NULL**
  (`NowDngMap`→`MainDungeonMap`, header zeroed). So `SearchiDoPutArea`/the MAPPARTS tile grid only
  applies to *random* floors; the arena's geometry is a prebuilt map + collision mesh, not tiles.
- **korinoya fires the arrow ~7× in the fight** (motion 0→1, `global[3]` 0→1→3→0 cycling, player takes
  hits) while its **floor-slot `LocationX/Y` stays frozen at (501,10,884) for all samples**. No new
  projectile slot spawns. So eid 84 is a **stationary emitter**; the visible homing arrow is a
  render-layer transform, NOT the floor-slot position. This is why translating/tracking the floor slot
  never reproduced or even measured the homing (and why the "Dran movement limit" idea was wrong).

**Where the real moving position lives** (RE'd from `_GET_POSITION`/`_GET_MONSTOR_POS`, both
`@0x1e1df0`/`0x1e4920`): the script reads positions from the **CCharacter array**, not the floor-slot
array. `_GET_POSITION(-1)` = self, `_GET_POSITION(-2)` = player (`@0x21EA1D30`). Self resolves to:
```
CCharacter = MainMonstorUnit(0x21DF87D0) + slot*0x3510 + 0x1FCD0      ; gp-0x6320 = MainMonstorUnit
worldPos   = virtual getter (vtable+0xA0 -> +0xA0) = GetWorldPosition  ; matrix-derived
           : CCharacter+0xBC -> +0x110 = CFrame; local matrix @ CFrame+0x150 (sceVu0 LW-matrix * pos)
```
So `_SET_MOVE` (homing) drives this CCharacter, not the floor slot. **The live position is the float
triple right inside the CCharacter at +0x10 = (X, Z/height, Y)** (not down the CFrame pointer chain —
that was a dead end; the matrix is derived from this). Confirmed two ways: korinoya idle reads exactly
`(0,10,-380)` = its scripted `_SET_POSITION(0,10,380)` (Y negated), and Ice Queen reads her floor Y.
Documented as `EnemyAddresses.CharObjects` (`Base + slot*0x3510 + 0x1FCD0 + 0x10`).

**The arrow flies and homes — confirmed in BOTH the genuine arena and a random floor.** korinoya's
`+0x10` position launches from its AI idle `(0,10,-380)` (or repositions onto Ice Queen's char pos
first), arcs (Z 10→20→10), tracks the player across the floor, then resets and re-fires. The ice arrow
IS this CCharacter position moving; it homes to the player (`_GET_POSITION(-2)` @0x21EA1D30),
**independent of where it launches from**. The floor slot is irrelevant.

**The "stuck arrow" was never the translation — it was the AI rotation stalling.** A random-floor run
with companion translation ON worked fine (handshake cycled 0→1→3→4→5→0 repeatedly, 6 arrow fires,
korinoya repositioned onto translated Ice Queen at (38,180) and flew out homing). The arrow launches
from korinoya's *AI-loop* position, not the init `_SET_POSITION` we translate, so translating korinoya
doesn't break it. The real variable is whether her geometry/distance-driven phase loop **advances** to
the move phase. When the rotation runs end-to-end the arrow works; old "stuck" reports were the
rotation stalling at block 1 before reaching the move. Remaining polish: icicle render on freeze, minor
AI/visual roughness — not position/movement issues.
