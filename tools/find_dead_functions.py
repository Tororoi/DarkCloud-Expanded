#!/usr/bin/env python3
"""Find reclaimable ELF slack in SCUS_971.11: functions with NO static references
(no `jal` target, no data-word pointer) = dead code we can host custom code in, plus
inter-function padding. Also sizes a named set of functions (the camera code) so we can
judge how much room a more-efficient rewrite would free adjacent to them.

Usage: DC1_DATA_DIR=... python3 tools/find_dead_functions.py [name-substr ...]
A function is "dead" if its ENTRY is not the target of any jal AND not stored as a data
word anywhere (so it isn't dispatched via a pointer table either). jalr-only via a
computed pointer would be missed, but that's rare — treat hits as CANDIDATES to verify.
"""
import os, struct, sys

elf = os.path.join(os.environ["DC1_DATA_DIR"], "SCUS_971.11")
data = open(elf, "rb").read()
symfile = os.path.join(os.path.dirname(__file__), "ghidra", "symbols.txt")

# --- program segments: file-offset <-> vaddr ---
e_phoff = struct.unpack_from("<I", data, 0x1C)[0]
e_phnum = struct.unpack_from("<H", data, 0x2C)[0]
e_phe = struct.unpack_from("<H", data, 0x2A)[0]
segs = []
for i in range(e_phnum):
    o = e_phoff + i * e_phe
    t, po, va, pa, fs, ms = struct.unpack_from("<IIIIII", data, o)
    if t == 1:
        segs.append((po, va, fs))

def v2f(va):
    for po, v, fs in segs:
        if v <= va < v + fs:
            return po + (va - v)
    return None

# --- functions from symbols.txt (addr name), sorted, with sizes ---
funcs = []  # (addr, name)
for line in open(symfile):
    p = line.split()
    if len(p) < 2:
        continue
    try:
        a = int(p[0], 16)
    except ValueError:
        continue
    funcs.append((a, p[1]))
funcs.sort()
addrs = [a for a, _ in funcs]
name_of = {a: n for a, n in funcs}
def size_of(a):
    i = addrs.index(a)
    return (addrs[i + 1] - a) if i + 1 < len(addrs) else 0

func_set = set(addrs)

# --- scan for references, THREE ways ---
#   1. jal target (direct call)
#   2. data word == function entry (pointer stored in a static table)
#   3. lui/addiu(ori) idiom that MATERIALIZES a function address into a register (the common way
#      a function ptr gets installed into a runtime-built vtable/dispatch table — e.g. ToanKey_Play).
#      lui reg,HI ; addiu reg,reg,LO  → addr = (HI<<16) + sign_extend16(LO)
#      lui reg,HI ; ori   reg,reg,LO  → addr = (HI<<16) | LO
jal_targets = set()
data_ptr_targets = set()
materialized = set()
pending_lui = {}  # reg -> HI immediate
for po, va, fs in segs:
    for off in range(0, fs & ~3, 4):
        w = struct.unpack_from("<I", data, po + off)[0]
        op = w >> 26
        if op == 3:  # jal
            jal_targets.add((w & 0x03FFFFFF) << 2)
        if w in func_set:
            data_ptr_targets.add(w)
        if op == 0x0F:  # lui rt, imm
            rt = (w >> 16) & 0x1F
            pending_lui[rt] = w & 0xFFFF
        elif op == 0x09:  # addiu rt, rs, imm
            rs = (w >> 21) & 0x1F; rt = (w >> 16) & 0x1F; imm = w & 0xFFFF
            if rs in pending_lui:
                lo = imm - 0x10000 if imm & 0x8000 else imm
                materialized.add(((pending_lui[rs] << 16) + lo) & 0xFFFFFFFF)
            pending_lui.pop(rt, None)
        elif op == 0x0D:  # ori rt, rs, imm
            rs = (w >> 21) & 0x1F; rt = (w >> 16) & 0x1F
            if rs in pending_lui:
                materialized.add((pending_lui[rs] << 16) | (w & 0xFFFF))
            pending_lui.pop(rt, None)
        else:
            # any write to a reg breaks its pending lui (approx: clear rt of common op forms)
            rt = (w >> 16) & 0x1F
            pending_lui.pop(rt, None)

referenced = jal_targets | data_ptr_targets | materialized

# --- dead functions ---
dead = [(a, name_of[a], size_of(a)) for a in addrs
        if a not in referenced and size_of(a) > 0]
dead.sort(key=lambda t: -t[2])

print("=== TOP DEAD (zero-xref) FUNCTIONS by size ===")
total = 0
for a, n, s in dead[:30]:
    total += s
    print(f"  0x{a:06x}  {s:5d}B  {n}")
print(f"  ... {len(dead)} dead funcs total, {sum(s for _,_,s in dead)} bytes")

# --- contiguous dead runs (adjacent dead functions) ---
print("\n=== CONTIGUOUS DEAD RUNS (adjacent zero-xref funcs) ===")
runs = []
i = 0
deadset = {a for a, _, _ in dead}
while i < len(addrs):
    a = addrs[i]
    if a in deadset:
        j = i
        while j + 1 < len(addrs) and addrs[j + 1] in deadset:
            j += 1
        start = addrs[i]; end = addrs[j] + size_of(addrs[j])
        runs.append((start, end - start, j - i + 1))
        i = j + 1
    else:
        i += 1
runs.sort(key=lambda t: -t[1])
for start, sz, cnt in runs[:15]:
    print(f"  0x{start:06x}  {sz:5d}B  ({cnt} fn)  first={name_of[start]}")

# --- named functions of interest (camera) + their sizes ---
wants = sys.argv[1:] or ["Camera", "CheckHit", "CheckCameraWidth", "MoveCamera", "EdMoveChara", "Step__CCamera"]
print("\n=== NAMED FUNCTIONS (compaction candidates) ===")
for a, n in funcs:
    if any(w.lower() in n.lower() for w in wants):
        ref = "referenced" if a in referenced else "DEAD?"
        print(f"  0x{a:06x}  {size_of(a):5d}B  {ref:11s} {n}")
