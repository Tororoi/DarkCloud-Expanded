#!/usr/bin/env python3
"""Export function symbols (vaddr  name) from SCUS_971.11's .symtab.
Used to apply names to the dun.bin overlay program in Ghidra (the ELF's own
symbols load automatically for the main image; the overlay does not).

  python3 tools/ghidra/export_symbols.py            # all funcs -> tools/ghidra/symbols.txt
  python3 tools/ghidra/export_symbols.py 0x1da0000 0x1dd0000   # only this vaddr range (e.g. dun overlay)
"""
import os
import struct, sys

# Extracted Dark Cloud disc dir; required — see .env.sample.
DC1_DATA_DIR = os.environ.get("DC1_DATA_DIR")
if not DC1_DATA_DIR: raise SystemExit("Set $DC1_DATA_DIR to your extracted Dark Cloud disc dir (see .env.sample)")
ELF = os.path.join(DC1_DATA_DIR, "SCUS_971.11")
SYM_OFF, SYM_SZ, STR_OFF = 0x1cd5e0, 0x31610, 0x1a2510
elf = open(ELF, "rb").read()
lo = int(sys.argv[1], 16) if len(sys.argv) > 1 else 0
hi = int(sys.argv[2], 16) if len(sys.argv) > 2 else 0xFFFFFFFF
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "symbols.txt")   # next to this script
n = 0
with open(out, "w") as f:
    for o in range(SYM_OFF, SYM_OFF + SYM_SZ, 16):
        nm, val, size, info, _, _ = struct.unpack("<IIIBBH", elf[o:o+16])
        if (info & 0xf) != 2 or not val:        # STT_FUNC only
            continue
        if not (lo <= val < hi):
            continue
        e = elf.index(b"\0", STR_OFF + nm)
        name = elf[STR_OFF + nm:e].decode("latin1")
        f.write("0x%08x %s\n" % (val, name))
        n += 1
print("wrote %d symbols to %s" % (n, out))
