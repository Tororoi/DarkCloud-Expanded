#!/usr/bin/env python3
"""Export Yellow Drops (map 23) custom fishing collision to the DCFC binary the mod reads
(FishingCollision.AddMeshTriangles -> Resources/FishingCollision/yellowdrops_23.bin).

Content = the west-bank fish walls (yellowdrops_pond.westbank_fish_walls: the bulged waterline,
bank top down to FISH_WALL_BOTTOM). Format identical to export_queens_collision.py.

Run: python3 tools/export_yellowdrops_collision.py -> game_data/yellowdrops/yellowdrops_23.bin
(untracked; the csproj Links it into Resources/FishingCollision/ at build, guarded by Exists()).
"""
import os, sys, struct
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from yellowdrops_pond import westbank_fish_walls

MAP_NO = 23
OUT = os.path.normpath(os.path.join(HERE, "..", "game_data", "yellowdrops", f"yellowdrops_{MAP_NO}.bin"))
os.makedirs(os.path.dirname(OUT), exist_ok=True)

tris = westbank_fish_walls()
with open(OUT, "wb") as f:
    f.write(b"DCFC")
    f.write(struct.pack("<III", 1, MAP_NO, len(tris)))   # version, mapNo, triCount
    for t in tris:
        for v in t:
            f.write(struct.pack("<fff", float(v[0]), float(v[1]), float(v[2])))
print(f"wrote {len(tris)} fish-wall tris = {16 + len(tris)*9*4} bytes -> {OUT}")
