#!/usr/bin/env python3
"""Assemble brownboo_14.bin from a MIX of hand-simplified Blender rocks and viewer-generated placeholders,
so the full rock collision is always complete while rocks are edited one at a time.

For each rock (iwa01/iwa02/iwa03): if tools/rock_obj/<rock>_simple.obj exists, use that (your Blender
mesh); otherwise fall back to the viewer's smooth lathe for that rock. Writes the combined DCFC .bin.

Run:  python3 tools/assemble_rock_collision.py
"""
import os, sys, struct, io, contextlib

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import import_rocks_obj as imp

MAP_NO = 14
ROCKS = ["iwa01", "iwa02", "iwa03"]
OUT = os.path.normpath(os.path.join(HERE, "..", "Dark Cloud Improved Version",
                                    "Resources", "FishingCollision", f"brownboo_{MAP_NO}.bin"))

def simple_path(rock):
    return os.path.join(HERE, "rock_obj", f"{rock}_simple.obj")

need_lathe = [r for r in ROCKS if not os.path.exists(simple_path(r))]
lathe = {}
if need_lathe:
    with contextlib.redirect_stdout(io.StringIO()):
        import brownboo_viewer as bv                 # importing builds GOT + the lathes
    for nm, (v, ts) in bv.GOT.items():
        base = nm.split("__")[0]
        if base not in need_lathe:
            continue
        rt = [[v[a], v[b], v[c]] for a, b, c in ts]
        if base == "iwa03":
            lathe[base] = bv.build_rock_smooth(rt, bv.SMALL_CELL, bv.WATER, bv.YCAP, bv.YBOTTOM,
                                               bv.SMALL_SIDES, bv.SMALL_LEVELS, bv.SMALL_MARGIN, bv.SMALL_LIFT)
        else:
            lathe[base] = bv.build_rock_smooth(rt, bv.ROCK_CELL, bv.WATER, bv.YCAP, bv.YBOTTOM,
                                               bv.ROCK_SIDES, bv.ROCK_LEVELS, bv.ROCK_MARGIN, bv.ROCK_LIFT)

all_tris = []
for r in ROCKS:
    if os.path.exists(simple_path(r)):
        t = imp.load_obj(simple_path(r)); src = "BLENDER"
    else:
        t = lathe.get(r, []); src = "lathe placeholder"
    all_tris += t
    print(f"{r}: {len(t):4} tris  ({src})")

with open(OUT, "wb") as f:
    f.write(b"DCFC")
    f.write(struct.pack("<III", 1, MAP_NO, len(all_tris)))
    for tri in all_tris:
        for v in tri:
            f.write(struct.pack("<fff", float(v[0]), float(v[1]), float(v[2])))

ys = [v[1] for tri in all_tris for v in tri]
print(f"\nTOTAL {len(all_tris)} tris  (+326 floors = {len(all_tris)+326}, cap 1228)  Y[{min(ys):.1f},{max(ys):.1f}]")
print(f"-> {OUT}")
