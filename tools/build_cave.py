#!/usr/bin/env python3
"""
build_cave.py — generate the runtime enemy model/AI re-skin code cave for
Dark Cloud (USA, SCUS_971.11, v1.02) on PCSX2.

Background (see EnemyAddresses.cs / BtEnemyLayout + the RE notes):
  Enemy slots and the model/scale blocks live inside one global object,
  CMonstorUnit "MainMonstorUnit" (native 0x01DF87D0; ptr also at [gp-0x6320]).
  Re-skinning a slot to ANY species at runtime is a two-phase operation:

    PRELOAD (mode 1, does disc I/O — run at a safe moment, e.g. floor entry):
       SetupBaseModel(this, N, tableIndex, 0x26, MonstorModelBuffer)   @ 0x001DFE90
         -> loads species `tableIndex`'s mesh + 0x9C species record into model block N

    INSTANTIATE (mode 2, no disc I/O — safe during gameplay):
       slot[N].RenderStatus = -1            ; free only slot N
       SetupViewMonstor(this, N, &pos, 0)   @ 0x001E02B0
         -> builds the live monster from model block N into the freed slot

  SetupViewMonstor auto-targets the first slot whose RenderStatus(+0)==-1, so freeing
  only N lands it in N (assuming no lower-numbered slot is also free).

Hook: a 1-instruction detour at OpA_MotionProcess (per-frame, dungeon). We overwrite
ONE instruction with `j cave`; its delay slot (the original next instruction) runs
naturally, the cave reproduces the overwritten instruction and returns to HOOK+8.

Addresses below are PS2-NATIVE. PCSX2 = native | 0x20000000 (PINE PhysAddr masks the
top nibble, so writing to either form hits the same RAM).

Usage: edit CAVE_BASE to a debugger-verified free RAM region (>= 0x200 bytes), run:
    python3 tools/build_cave.py
and paste the emitted hex into EnemyModelInjector.cs (or just change CaveBase there —
the C# relocates the same template itself).
"""
from keystone import Ks, KS_ARCH_MIPS, KS_MODE_MIPS32, KS_MODE_LITTLE_ENDIAN
import capstone

# ---- addresses (NATIVE PS2, as seen in a live dungeon savestate) ----
# NOTE: the dun overlay is copied into RAM WITH its 0x80 file header, so every dun-overlay
# symbol is at (symbol + 0x80) at runtime. Main-segment functions (SetupBaseModel/SetupViewMonstor)
# are NOT shifted. Verified against eeMemory dumps.
#
# Hook site = OpA_MotionProcess EPILOGUE. The top of that function is first-frame init that the
# common path skips (beqz), so it is NOT per-frame. Every call instead funnels through the single
# `jr ra` epilogue (symbol 0x01DB73C0), whose FP-restore run is reached every frame. We hook the
# `lwc1 $f26,0x18($sp)` at symbol 0x01DB73A0 -> runtime 0x01DB7420; its delay slot
# (`lwc1 $f25,0x14($sp)`) is an order-independent restore, safe to run before the reproduced insn.
CAVE_BASE  = 0x01F70000   # PARAM block here (0x20 bytes); code at +0x20. Zero in both DBC dumps.
HOOK_ADDR  = 0x01DB7420   # runtime; OpA_MotionProcess epilogue, original = `lwc1 $f26,0x18($sp)`
DISPLACED  = 0xC7BA0018   # the original word at HOOK_ADDR (reproduced by the cave)
HOOK_RET   = HOOK_ADDR + 8 # return past HOOK and its delay slot (HOOK+4 runs as the delay slot)
THIS_PTR   = 0x01DF87D0   # MainMonstorUnit (CMonstorUnit*); hardcoded — [gp-0x6320] is not it at runtime
SETUPBASE  = 0x001DFE90   # CMonstorUnit::SetupBaseModel(this,a1,a2,a3,t0)  [t0 = CDataAlloc2*]
SETUPVIEW  = 0x001E02B0   # CMonstorUnit::SetupViewMonstor(this,a1,a2=&pos,a3)
MODELBUF   = 0x01F066D0   # MonstorModelBuffer (the CDataAlloc2* allocator)
THIS_GPOFF = -0x6320      # [gp+THIS_GPOFF] -> MainMonstorUnit (this)
SLOTS_OFF  = 0x1E3D0      # this + N*0x190 + SLOTS_OFF = live slot N (RenderStatus at +0)
SLOT_STRIDE= 0x190
# PARAM layout (relative to CAVE_BASE):
#   +0x00 trigger (write !=0 to fire; cave clears)
#   +0x04 mode    (1=preload SetupBaseModel, 2=instantiate free+SetupViewMonstor)
#   +0x08 N       (model-block index / live slot to free)
#   +0x0C T       (tableIndex, mode 1)
#   +0x10..0x1C   pos float[3] (mode 2)
# --------------------------------
CODE = CAVE_BASE + 0x20
POS  = CAVE_BASE + 0x10
def hi(a): return (a >> 16) & 0xFFFF
def lo(a): return a & 0xFFFF

asm = f"""
    addiu $sp, $sp, -0x70
    sw  $ra, 0x00($sp)
    sw  $at, 0x04($sp)
    sw  $v0, 0x08($sp)
    sw  $v1, 0x0c($sp)
    sw  $a0, 0x10($sp)
    sw  $a1, 0x14($sp)
    sw  $a2, 0x18($sp)
    sw  $a3, 0x1c($sp)
    sw  $t0, 0x20($sp)
    sw  $t1, 0x24($sp)
    sw  $t2, 0x28($sp)
    sw  $t5, 0x2c($sp)
    sw  $s0, 0x30($sp)
    sw  $s1, 0x34($sp)
    sw  $s2, 0x38($sp)
    mfhi $t1
    sw  $t1, 0x3c($sp)
    mflo $t1
    sw  $t1, 0x40($sp)

    lui $v0, 0x{hi(CAVE_BASE):04x}
    ori $v0, $v0, 0x{lo(CAVE_BASE):04x}
    lw  $t1, 0x1c($v0)           # HEARTBEAT: ++[PARAM+0x1C] every frame (diagnostic)
    addiu $t1, $t1, 1
    sw  $t1, 0x1c($v0)
    lw  $v1, 0x00($v0)           # trigger
    beq $v1, $zero, cave_exit
    nop
    sw  $zero, 0x00($v0)          # clear trigger
    lw  $t5, 0x04($v0)            # mode
    lw  $s1, 0x08($v0)            # N
    lw  $s2, 0x0c($v0)            # T
    lui $s0, 0x{hi(THIS_PTR):04x}    # this = MainMonstorUnit (0x{THIS_PTR:08x})
    ori $s0, $s0, 0x{lo(THIS_PTR):04x}

    ori $at, $zero, 1
    beq $t5, $at, do_preload
    nop
    ori $at, $zero, 2
    beq $t5, $at, do_instantiate
    nop
    b   cave_exit
    nop

do_preload:                       # SetupBaseModel(this, N, T, 0x26, MonstorModelBuffer)  (disc I/O)
    addu  $a0, $s0, $zero
    addu  $a1, $s1, $zero
    addu  $a2, $s2, $zero
    addiu $a3, $zero, 0x26
    lui   $t0, 0x{hi(MODELBUF):04x}
    ori   $t0, $t0, 0x{lo(MODELBUF):04x}
    jal   0x{SETUPBASE:x}
    nop
    b   cave_exit
    nop

do_instantiate:                   # free slot N, then SetupViewMonstor(this, N, &pos, 0)  (no disc I/O)
    ori   $v1, $zero, 0x{SLOT_STRIDE:x}
    mult  $s1, $v1
    mflo  $t1
    lui   $at, 0x{hi(SLOTS_OFF):04x}
    ori   $at, $at, 0x{lo(SLOTS_OFF):04x}
    addu  $t1, $t1, $at
    addu  $t1, $s0, $t1           # &slot[N]
    addiu $t2, $zero, -1
    sw    $t2, 0x0($t1)           # RenderStatus = -1 (free slot N)
    addu  $a0, $s0, $zero
    addu  $a1, $s1, $zero
    lui   $a2, 0x{hi(POS):04x}
    ori   $a2, $a2, 0x{lo(POS):04x}
    addiu $a3, $zero, 0
    jal   0x{SETUPVIEW:x}
    nop

cave_exit:
    lw  $t1, 0x40($sp)
    mtlo $t1
    lw  $t1, 0x3c($sp)
    mthi $t1
    lw  $ra, 0x00($sp)
    lw  $at, 0x04($sp)
    lw  $v0, 0x08($sp)
    lw  $v1, 0x0c($sp)
    lw  $a0, 0x10($sp)
    lw  $a1, 0x14($sp)
    lw  $a2, 0x18($sp)
    lw  $a3, 0x1c($sp)
    lw  $t0, 0x20($sp)
    lw  $t1, 0x24($sp)
    lw  $t2, 0x28($sp)
    lw  $t5, 0x2c($sp)
    lw  $s0, 0x30($sp)
    lw  $s1, 0x34($sp)
    lw  $s2, 0x38($sp)
    addiu $sp, $sp, 0x70
    .word 0x{DISPLACED:08x}        # reproduce the overwritten HOOK instruction
    j   0x{HOOK_RET:x}             # return past HOOK + its delay slot
    nop
"""
ks = Ks(KS_ARCH_MIPS, KS_MODE_MIPS32 | KS_MODE_LITTLE_ENDIAN)
code = bytes(ks.asm(asm, CODE)[0])
# Single-instruction detour: keystone auto-pads `j` with a delay-slot nop, so take only
# the 4-byte jump word. The real delay slot is the game's original next instruction.
hook = bytes(ks.asm(f"j 0x{CODE:x}", HOOK_ADDR)[0])[:4]

if __name__ == "__main__":
    print(f"CAVE_BASE (native) = 0x{CAVE_BASE:08x}  | PCSX2 0x{CAVE_BASE|0x20000000:08x}")
    print(f"CODE start         = 0x{CODE:08x}  | {len(code)} bytes")
    print(f"HOOK_ADDR          = 0x{HOOK_ADDR:08x}  (write {len(hook)} bytes = `j 0x{CODE:x}`)")
    print(f"\ncave  (write at native 0x{CODE:08x}):\n{code.hex()}")
    print(f"\nhook  (write at native 0x{HOOK_ADDR:08x}):\n{hook.hex()}")
    print("\n--- verify ---")
    md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 | capstone.CS_MODE_LITTLE_ENDIAN)
    md.skipdata = True
    for ins in md.disasm(code, CODE):
        print("%08x  %-9s %s" % (ins.address, ins.mnemonic, ins.op_str))
