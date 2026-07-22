#!/usr/bin/env python3
"""Symbol-annotated MIPS disassembler for SCUS_971.11 (PS2 EE / MIPS32 LE).
Usage: python3 tools/dcdis.py <hexvaddr> [num_insns]
       python3 tools/dcdis.py sym <substring>      # search symbols by name
       python3 tools/dcdis.py xref <hexoffset> [base_reg]  # find lwc1/swc1 at given offset
"""
import os
import struct, sys, bisect
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN

# Extracted Dark Cloud disc dir; required — see .env.sample.
DC1_DATA_DIR = os.environ.get("DC1_DATA_DIR")
if not DC1_DATA_DIR: raise SystemExit("Set $DC1_DATA_DIR to your extracted Dark Cloud disc dir (see .env.sample)")

ELF = os.path.join(DC1_DATA_DIR, "SCUS_971.11")
elf = open(ELF, "rb").read()
SYM_OFF, SYM_SZ, STR_OFF = 0x1cd5e0, 0x31610, 0x1a2510
VA_BASE, FILE_BASE, MAIN_SIZE = 0x100000, 0x100, 0x1a2380

def sname(o):
    return elf[STR_OFF+o:elf.index(b"\0", STR_OFF+o)].decode("latin1")

SY = []
for o in range(SYM_OFF, SYM_OFF+SYM_SZ, 16):
    nm, val, size, info, _, _ = struct.unpack("<IIIBBH", elf[o:o+16])
    if (info & 0xf) == 2 and val:
        SY.append((val, size, sname(nm)))
SY.sort()
vals = [v for v, _, _ in SY]

def sym(va):
    i = bisect.bisect_right(vals, va) - 1
    if i < 0:
        return hex(va)
    base, size, nm = SY[i]
    return nm if va == base else "%s+0x%x" % (nm, va-base)

def fileoff(va):
    return va - VA_BASE + FILE_BASE

md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 | CS_MODE_LITTLE_ENDIAN)
md.detail = False

def disasm(va, n=80):
    fo = fileoff(va)
    code = elf[fo:fo+n*4]
    addr = va
    i = 0
    while i < len(code):
        word = struct.unpack_from("<I", code, i)[0]
        chunk = code[i:i+4]
        done = False
        for ins in md.disasm(chunk, addr):
            tgt = ""
            if ins.mnemonic in ("jal", "j", "bal"):
                m = ins.op_str.replace("0x", "")
                try:
                    t = int(ins.op_str.split()[-1], 16)
                    tgt = "  ; " + sym(t)
                except Exception:
                    pass
            print("0x%08x  %08x  %-8s %s%s" % (addr, word, ins.mnemonic, ins.op_str, tgt))
            done = True
            break
        if not done:
            print("0x%08x  %08x  .word   (undecoded)" % (addr, word))
        addr += 4
        i += 4
        if i >= (n*4):
            break

def search_sym(s):
    s = s.lower()
    for v, sz, nm in SY:
        if s in nm.lower():
            print("0x%08x  size=0x%-5x  %s" % (v, sz, nm))

def xref(off, reg=None):
    # scan main .text for lwc1/swc1/lw/sw at the given immediate offset
    md2 = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 | CS_MODE_LITTLE_ENDIAN)
    needle = "0x%x(" % off if off >= 0 else "-0x%x(" % (-off)
    alt = "%d(" % off
    va = VA_BASE
    end = VA_BASE + MAIN_SIZE
    fo = FILE_BASE
    hits = 0
    for addr in range(va, end, 4):
        word = struct.unpack_from("<I", elf, fileoff(addr))[0]
        for ins in md2.disasm(struct.pack("<I", word), addr):
            if ins.mnemonic in ("lwc1", "swc1", "lw", "sw", "lh", "sh"):
                ops = ins.op_str
                if (needle in ops or alt in ops):
                    if reg is None or ("(%s)" % reg) in ops:
                        print("0x%08x  %-6s %-22s ; %s" % (addr, ins.mnemonic, ops, sym(addr)))
                        hits += 1
            break
    print("# %d hits" % hits)

if __name__ == "__main__":
    a = sys.argv
    if len(a) >= 2 and a[1] == "sym":
        search_sym(a[2])
    elif len(a) >= 2 and a[1] == "xref":
        xref(int(a[2], 16), a[3] if len(a) > 3 else None)
    else:
        va = int(a[1], 16)
        n = int(a[2]) if len(a) > 2 else 80
        disasm(va, n)
