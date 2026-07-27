#!/usr/bin/env python3
"""Export Queens (map 2) custom fishing collision to the DCFC binary the mod reads
(FishingCollision.AddMeshTriangles -> Resources/FishingCollision/queens_2.bin).

Format: 'DCFC', uint version, uint mapNo, uint triCount, then triCount * 9 floats (3 verts x,y,z per
triangle; the mod computes the plane normal itself). Content = the two bridge meshes + obj9 pipes (full,
unsimplified) + the canal containment walls (see tools/queens_fishing_collision.py).

Run: python3 tools/export_queens_collision.py  ->  game_data/queens/queens_2.bin

Output goes to the UNTRACKED game_data/ tree (it embeds ISO-derived bridge/pipe geometry, so it must not be
committed); the csproj Links it into Resources/FishingCollision/queens_2.bin at build time, guarded by
Exists() so a clean checkout still builds.
"""
import os, sys, struct
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from queens_fishing_collision import fishing_collision_tris

MAP_NO = 2
OUT = os.path.normpath(os.path.join(HERE, "..", "game_data", "queens", f"queens_{MAP_NO}.bin"))
os.makedirs(os.path.dirname(OUT), exist_ok=True)

g = fishing_collision_tris()
tris = g['all']
with open(OUT, "wb") as f:
    f.write(b"DCFC")
    f.write(struct.pack("<III", 1, MAP_NO, len(tris)))   # version, mapNo, triCount
    for t in tris:
        for v in t:
            f.write(struct.pack("<fff", float(v[0]), float(v[1]), float(v[2])))
print(f"wrote {len(tris)} tris (bridges {len(g['bridges'])} + pipes {len(g['pipes'])} + contain "
      f"{len(g['contain'])}) = {16 + len(tris)*9*4} bytes -> {OUT}")
