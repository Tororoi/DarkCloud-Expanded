#!/usr/bin/env python3
"""Export the viewer's rock collision (brownboo_viewer.col_rocks) to the DCFC binary the mod reads
(CustomFishingSpot.AddMeshTriangles). Format: 'DCFC', uint version, uint mapNo, uint triCount, then
triCount * 9 floats (3 verts x,y,z per triangle; the mod computes the plane normal itself).

Run: python3 tools/export_rock_collision.py   ->  Resources/FishingCollision/brownboo_14.bin
"""
import os, sys, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import brownboo_viewer as bv   # importing runs the generator; col_rocks is a module global

MAP_NO = 14
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..",
                   "Dark Cloud Improved Version", "Resources", "FishingCollision", f"brownboo_{MAP_NO}.bin")
OUT = os.path.normpath(OUT)

tris = bv.col_rocks
with open(OUT, "wb") as f:
    f.write(b"DCFC")
    f.write(struct.pack("<III", 1, MAP_NO, len(tris)))   # version, mapNo, triCount
    for t in tris:
        for v in t:
            f.write(struct.pack("<fff", float(v[0]), float(v[1]), float(v[2])))
print(f"wrote {len(tris)} rock tris ({16 + len(tris)*9*4} bytes) -> {OUT}")
