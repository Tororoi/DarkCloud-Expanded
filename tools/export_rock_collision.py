#!/usr/bin/env python3
"""Export the fishing collision to the DCFC binary the mod reads (CustomFishingSpot.AddMeshTriangles):
the viewer's rock collision (brownboo_viewer.col_rocks). Format: 'DCFC', uint version, uint mapNo, uint
triCount, then triCount * 9 floats (3 verts x,y,z per triangle; the mod computes the plane normal itself).

Run: python3 tools/export_rock_collision.py   ->  Resources/FishingCollision/brownboo_14.bin
"""
import os, sys, struct
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import brownboo_viewer as bv   # importing runs the generator; col_rocks is a module global

MAP_NO = 14
OUT = os.path.join(HERE, "..", "Dark Cloud Improved Version", "Resources", "FishingCollision", f"brownboo_{MAP_NO}.bin")
OUT = os.path.normpath(OUT)

tris = list(bv.col_rocks)
with open(OUT, "wb") as f:
    f.write(b"DCFC")
    f.write(struct.pack("<III", 1, MAP_NO, len(tris)))   # version, mapNo, triCount
    for t in tris:
        for v in t:
            f.write(struct.pack("<fff", float(v[0]), float(v[1]), float(v[2])))
print(f"wrote {len(tris)} tris ({len(bv.col_rocks)} rock"
      f"= {16 + len(tris)*9*4} bytes -> {OUT}")
