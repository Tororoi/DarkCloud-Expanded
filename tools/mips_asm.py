#!/usr/bin/env python3
"""Thin keystone wrapper for authoring EE (r5900) MIPS to byte-patch into Dark Cloud.

Validated 2026-07-30 (see memory native-camera-functions): keystone assembles our subset
byte-identical to the game's conventions. Two rules this wrapper bakes in:

  * `.set noreorder` is prepended so WE own branch delay slots — otherwise keystone
    auto-inserts a nop into every delay slot and shoves our real instruction after it.
  * EE-specific ops keystone's MIPS32 can't encode (sq/lq @op 0x1F, MMI @op 0x1C — used in
    EE prologues) go in as raw `.word 0xXXXXXXXX`. keystone passes those through verbatim.

⛔ NEVER use keystone's `c.lt.s` / `c.le.s` / `c.eq.s` (cost me a full debugging session, FIX 8):
keystone emits the standard-MIPS func codes (C.LT=0x3C, C.LE=0x3E), but the R5900 FPU only
implements the ORDERED variants (C.OLT=0x34, C.OLE=0x36). PCSX2's EE recompiler does NOT set the
FP condition flag for 0x3C/0x3E, so the compare silently no-ops and every following bc1t/bc1f reads
a stale flag. Hand-encode the ordered form as `.word`: for `c.lt.s $fS,$fT` emit
0x46000034 | (fmt? S=0) | ft<<16 | fs<<11  (i.e. take keystone's word and subtract 8: 0x3C->0x34).
Vanilla proves it: CheckHit @0x149E0C is 0x46000834 (func 0x34), Ghidra mislabels it "c.lt.S".

R5900 FPU HAZARD (bit me hard — see memory native-camera-functions FIX 6): a `c.cond.s`
(c.lt.s/c.le.s/c.eq.s) that sets the FP condition flag must have AT LEAST ONE instruction
before the `bc1t`/`bc1f` that reads it — otherwise the branch reads a STALE flag and goes the
wrong way (PCSX2 reproduces this). keystone does NOT insert the gap. YOU must write:
    c.lt.s $f0, $f2
    nop                # <-- mandatory gap; vanilla always has a real instr here
    bc1f  label
    nop                # delay slot (separate concern)

SECOND EE HAZARD — `mtc1` LATENCY (also bit me, FIX 7): after `mtc1 $tN, $fM` (GPR->FPR move,
e.g. loading a float constant), a dependent FPU op (c.lt.s/sub.s/mul.s/add.s reading $fM) in the
VERY NEXT slot gets the STALE $fM. Put a `nop` (or independent instr) between them:
    lui   $t0, 0x42a0
    mtc1  $t0, $f2
    nop                # <-- mandatory gap; else the next FPU op reads the old $f2
    c.lt.s $f2, $f0

Assemble AT the target address so branch/label offsets and `jal`/`j` targets resolve.
VERIFY output by disassembling the bytes through Ghidra-EE (tools/ghidra), NOT capstone —
capstone's MIPS32 can't decode EE instructions.

  from mips_asm import assemble, assemble_words, csharp_words
  words = assemble_words(asm, base=0x14b830)   # list[int]
  blob  = assemble(asm, base=0x14b830)          # bytes

CLI:  python3 tools/mips_asm.py file.s 0x14b830   # prints words, writes file.bin
"""
import sys, struct
from keystone import Ks, KS_ARCH_MIPS, KS_MODE_MIPS32, KS_MODE_LITTLE_ENDIAN, KsError

_ks = Ks(KS_ARCH_MIPS, KS_MODE_MIPS32 | KS_MODE_LITTLE_ENDIAN)


def assemble(code: str, base: int = 0) -> bytes:
    """Assemble EE MIPS `code` at virtual address `base`; return raw little-endian bytes."""
    # keystone wants ASCII; comments may contain unicode (em-dashes etc.) — replace, don't fail.
    src = (".set noreorder\n" + code).encode("ascii", "replace").decode("ascii")
    try:
        enc, _ = _ks.asm(src, base)
    except KsError as e:
        raise SystemExit(f"mips_asm: keystone error at base 0x{base:x}: {e}\n--- source ---\n{code}")
    if enc is None:
        raise SystemExit("mips_asm: keystone produced no output (syntax error?)")
    if len(enc) % 4:
        raise SystemExit(f"mips_asm: output not word-aligned ({len(enc)} bytes)")
    return bytes(enc)


def assemble_words(code: str, base: int = 0) -> list:
    b = assemble(code, base)
    return [struct.unpack_from("<I", b, i)[0] for i in range(0, len(b), 4)]


def csharp_words(code: str, base: int = 0, indent: str = "    ") -> str:
    """Emit as a C# uint[] literal for pasting into IsoPatcher / CodeCaveFunctions."""
    ws = assemble_words(code, base)
    body = "".join(f"{indent}0x{w:08X}u,\n" for w in ws)
    return "new uint[]\n{\n" + body + "}"


if __name__ == "__main__":
    if len(sys.argv) < 3:
        raise SystemExit("usage: mips_asm.py <file.s> <base-hex>   e.g. 0x14b830")
    code = open(sys.argv[1]).read()
    base = int(sys.argv[2], 16)
    blob = assemble(code, base)
    out = sys.argv[1].rsplit(".", 1)[0] + ".bin"
    open(out, "wb").write(blob)
    for i, w in enumerate(struct.unpack(f"<{len(blob)//4}I", blob)):
        print(f"  0x{base + i * 4:06x}: 0x{w:08x}")
    print(f"# {len(blob)//4} words -> {out}")
