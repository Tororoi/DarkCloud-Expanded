# Ghidra decompilation toolchain (PS2 EE / Dark Cloud)

Decompile `SCUS_971.11` (main) and `dun.bin` (overlay) with proper EmotionEngine
instruction support. Base Ghidra MIPS misreads the EE 128-bit/COP2 ops and bails;
the **ghidra-emotionengine-reloaded** extension (`r5900:LE:32:default`) fixes that.

## Setup (already done on this machine)
- `brew install ghidra` (12.1.2) — pulls openjdk@21.
- EE extension installed to `/usr/local/Cellar/ghidra/12.1.2/libexec/Ghidra/Extensions/`
  from chaoticgd/ghidra-emotionengine-reloaded release matching the Ghidra version.

## Address mapping
- main (`SCUS_971.11`): ELF, loads at its own vaddrs; symbols load automatically.
- overlay (`dun.bin`): raw binary, image base **0x1DABC80** (so file+0x80 → vaddr 0x1DABD00).
  Symbols aren't in the raw file → `ApplySymbols.java` names funcs from `symbols.txt`
  (exported by `export_symbols.py` from the SCUS .symtab).

## Use
```
./decompile.sh main "ToanKey_Play__Fv"        # -> /tmp/decomp_main.txt
./decompile.sh dun  "SwordDmgCheck1__Ffi"      # -> /tmp/decomp_dun.txt
```

## Files
- `export_symbols.py` — dump (vaddr name) of all funcs from SCUS .symtab → symbols.txt
- `ApplySymbols.java`  — Ghidra postScript: name+disassemble dun.bin funcs from symbols.txt
- `DumpDecomp.java`    — Ghidra postScript: decompile a comma-separated name list to a file
- `decompile.sh`       — wrapper around analyzeHeadless for both images
