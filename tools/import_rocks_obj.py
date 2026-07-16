#!/usr/bin/env python3
"""Convert simplified rock OBJ(s) (edited in Blender) into the DCFC collision .bin the mod loads.
Counterpart to export_rocks_obj.py. Reads every .obj given (or all of tools/rock_obj/*.obj by default),
combines their triangles, and writes Resources/FishingCollision/brownboo_14.bin.

Coordinates must be the game's RAW world space (X, Y-up, Z), WaterLevel y=0 — i.e. exported from Blender
with the SAME axis setting used on import (see export_rocks_obj.py). Faces may be tris, quads or ngons
(fan-triangulated) and may carry texture/normal indices (f v/vt/vn) — only the vertex index is used.

Run:  python3 tools/import_rocks_obj.py [file1.obj file2.obj ...]
        -> Dark Cloud Improved Version/Resources/FishingCollision/brownboo_14.bin
"""
import os, sys, glob, struct

MAP_NO = 14
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "Dark Cloud Improved Version",
                                    "Resources", "FishingCollision", f"brownboo_{MAP_NO}.bin"))

def load_obj(path):
    """Return a flat list of triangles [[x,y,z],[x,y,z],[x,y,z]] from an OBJ file."""
    verts = []
    tris = []
    with open(path) as f:
        for line in f:
            if line.startswith("v "):
                p = line.split()
                verts.append([float(p[1]), float(p[2]), float(p[3])])
            elif line.startswith("f "):
                idx = []
                for tok in line.split()[1:]:
                    vi = tok.split("/")[0]           # v, v/vt, v/vt/vn, v//vn — take the vertex index
                    if vi == "": continue
                    n = int(vi)
                    idx.append(n - 1 if n > 0 else len(verts) + n)   # 1-based, or negative-relative
                for k in range(1, len(idx) - 1):      # fan-triangulate quads/ngons
                    tris.append([verts[idx[0]], verts[idx[k]], verts[idx[k + 1]]])
    return tris

def main():
    files = sys.argv[1:] or sorted(glob.glob(os.path.join(HERE, "rock_obj", "*.obj")))
    if not files:
        print("no OBJ files given and none found in tools/rock_obj/"); return
    all_tris = []
    for path in files:
        t = load_obj(path)
        all_tris += t
        print(f"{os.path.basename(path)}: {len(t)} tris")
    with open(OUT, "wb") as f:
        f.write(b"DCFC")
        f.write(struct.pack("<III", 1, MAP_NO, len(all_tris)))
        for tri in all_tris:
            for v in tri:
                f.write(struct.pack("<fff", float(v[0]), float(v[1]), float(v[2])))
    ys = [v[1] for tri in all_tris for v in tri]
    print(f"\nwrote {len(all_tris)} tris ({16 + len(all_tris)*36} bytes) -> {OUT}")
    if ys: print(f"Y range [{min(ys):.1f}, {max(ys):.1f}]  (WaterLevel y=0; sanity-check orientation)")

if __name__ == "__main__":
    main()
