#!/usr/bin/env python3
"""Static code-cave analysis for the Dark Cloud ELF (SCUS_971.11).

Finds address ranges that no symbol claims ("unclaimed gaps") and classifies
them, since named tables often contain legitimate zero entries the game reads.

  CODE/DATA + ALL-ZERO  -> linker padding, safest small caves
  BSS gap               -> unnamed slack between named buffers, good candidates
  CODE/DATA + nonzero   -> unnamed data (VU microcode, rodata) - NOT caves

Memory map recap (from program/section headers):
  0x100000 - 0x2A2380   file-backed code+data ("main")
  0x2A2380 - 0x1DABD00  BSS (named buffers; gaps between them are candidates)
  0x1DABD00 - 0x1F06B00 overlay region (title/dun swap in here; tail may be free)
  0x1F06B00 - 0x1F80000 heap  (watch high-water mark at runtime)
  0x1F80000 - 0x2000000 stack (grows down; bottom may be free)

Usage:
  find_code_caves.py [--elf PATH] [--min N]            report to stdout
  find_code_caves.py --seed OUT.txt                    write/refresh CANDIDATE
                                                       seed entries into the
                                                       CodeCaveFindings.txt read
                                                       by CodeCaveScanner.cs
  find_code_caves.py --annotate 0xADDR                 nearest symbols to addr

Seeding never overwrites runtime findings: existing non-CANDIDATE entries in
OUT.txt are preserved verbatim; only missing CANDIDATE lines are added.
"""
import argparse
import bisect
import struct
import sys
from pathlib import Path

DEFAULT_ELF = Path.home() / "ROMs/dc_extracted/SCUS_971.11"

MAIN_ADDR = 0x100000
MAIN_FILE_OFF = 0x100
MAIN_FILE_END = 0x2A2380   # end of file-backed bytes (virtual address)
BSS_END = 0x1DABD00
OVERLAY_END = 0x1F06B00
HEAP_END = 0x1F80000
RAM_END = 0x2000000

SYM_OFF, SYM_SIZE, STR_OFF = 0x1CD5E0, 0x31610, 0x1A2510


def load_symbols(elf):
    syms = []
    for o in range(SYM_OFF, SYM_OFF + SYM_SIZE, 16):
        nm, val, sz, info, other, shndx = struct.unpack_from("<IIIBBH", elf, o)
        if val < MAIN_ADDR or val >= RAM_END:
            continue
        end = elf.index(b"\0", STR_OFF + nm)
        syms.append((val, sz, elf[STR_OFF + nm:end].decode(errors="replace")))
    syms.sort()
    return syms


def coverage(syms):
    """Merge symbols into covered intervals; symbols without size cover 4 bytes."""
    cov = []
    for val, sz, name in syms:
        end = val + max(sz, 4)
        if cov and val <= cov[-1][1]:
            if end > cov[-1][1]:
                cov[-1] = (cov[-1][0], end, cov[-1][2])
        else:
            cov.append((val, end, name))
    return cov


def find_gaps(elf, min_size):
    cov = coverage(load_symbols(elf))
    if cov[-1][1] < RAM_END:  # tail after the last symbol (upper stack region)
        cov.append((RAM_END, RAM_END, "(end of EE RAM)"))
    gaps = []
    for a, b in zip(cov, cov[1:]):
        start, size = a[1], b[0] - a[1]
        if size < min_size or start >= RAM_END:
            continue
        if start + size <= MAIN_FILE_END:
            chunk = elf[MAIN_FILE_OFF + start - MAIN_ADDR:
                        MAIN_FILE_OFF + start - MAIN_ADDR + size]
            kind = "PAD-ZERO" if not any(chunk) else "DATA"
        elif start < BSS_END:
            kind = "BSS-GAP"
        elif start < OVERLAY_END:
            kind = "OVERLAY"
        elif start < HEAP_END:
            kind = "HEAP"
        else:
            kind = "STACK"
        gaps.append((start, size, kind, a[2], b[2]))
    return gaps


def annotate(elf, addr):
    syms = load_symbols(elf)
    addrs = [s[0] for s in syms]
    i = bisect.bisect_right(addrs, addr)
    for j in range(max(0, i - 3), min(len(syms), i + 3)):
        val, sz, name = syms[j]
        mark = "  <-- " + hex(addr) if j == i - 1 else ""
        print(f"  {val:#010x} size={sz:#7x} {name}{mark}")


def seed_lines(gaps):
    """CANDIDATE entries worth runtime verification (skip DATA: real unnamed data)."""
    lines = []
    for start, size, kind, prev, nxt in gaps:
        if kind == "DATA":
            continue
        # HEAP/STACK/OVERLAY are huge single ranges; seed them whole so the
        # runtime scanner reports which sub-runs stay clean.
        note = f"static:{kind} after={prev} before={nxt}"
        lines.append((start, size, note))
    return lines


def merge_seed(out_path, lines):
    existing = []
    have = set()
    if out_path.exists():
        for ln in out_path.read_text().splitlines():
            existing.append(ln)
            parts = ln.split()
            if len(parts) >= 2 and not ln.startswith("#"):
                try:
                    have.add(int(parts[0], 16))
                except ValueError:
                    pass
    added = 0
    with out_path.open("a") as fh:
        if not existing:
            fh.write("# seeded by tools/find_code_caves.py -- CodeCaveScanner.cs "
                     "manages this file from here on\n")
        for start, size, note in lines:
            if start in have:
                continue
            fh.write(f"0x{start:08X} 0x{size:06X} CANDIDATE sweeps=0 sessions=0 "
                     f"note={note}\n")
            added += 1
    print(f"seeded {added} new CANDIDATE entries into {out_path}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--elf", type=Path, default=DEFAULT_ELF)
    ap.add_argument("--min", type=lambda s: int(s, 0), default=0x40)
    ap.add_argument("--seed", type=Path)
    ap.add_argument("--annotate", type=lambda s: int(s, 0))
    args = ap.parse_args()

    elf = args.elf.read_bytes()
    if args.annotate is not None:
        annotate(elf, args.annotate)
        return

    gaps = find_gaps(elf, args.min)
    if args.seed:
        merge_seed(args.seed, seed_lines(gaps))
        return

    by_kind = {}
    for g in gaps:
        by_kind.setdefault(g[2], []).append(g)
    for kind in ("PAD-ZERO", "BSS-GAP", "OVERLAY", "HEAP", "STACK", "DATA"):
        rows = by_kind.get(kind, [])
        total = sum(r[1] for r in rows)
        print(f"\n== {kind}: {len(rows)} gaps, {total:#x} ({total}) bytes ==")
        for start, size, _, prev, nxt in sorted(rows, key=lambda r: -r[1])[:40]:
            print(f"  {start:#010x} len={size:#7x} ({size:6d}) "
                  f"after={prev[:36]:38s} before={nxt[:36]}")


if __name__ == "__main__":
    main()
