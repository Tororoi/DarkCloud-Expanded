#!/usr/bin/env python3
"""Export Brownboo's rock meshes (iwa01/iwa02/iwa03 from gedit/s04/scene.scn) as individual OBJ files for
editing in Blender. The idea: simplify each rock into a low-poly COLLISION mesh in Blender, then hand the
result back and we convert it to the DCFC .bin the mod loads (import_rocks_obj.py, to be added).

Coordinates are the game's RAW world space (X, Y-up, Z), in game units — the same space the viewer and the
fishing collision use. WaterLevel = y=0. So in Blender, treat Y as UP.
  * On IMPORT in Blender, set Forward = -Z, Up = Y (Blender's OBJ import default is usually Y-up already).
  * On EXPORT from Blender, use the SAME axis setting so the coordinates round-trip unchanged.
Reference marks (game units): water surface y=0, the dolmen lintels sit above y~40, pillar bases reach
y~-15. The fishing spot is at world (74, -20); these rocks are to its west/north.

Run:  python3 tools/export_rocks_obj.py   ->  tools/rock_obj/iwaNN.obj  (one per rock)
"""
import os
from extract_scene_mesh import load_scene, extract_mesh

OUT = os.path.join(os.path.dirname(__file__), 'rock_obj')
os.makedirs(OUT, exist_ok=True)

scn = load_scene('gedit/s04/scene.scn')
GOT = extract_mesh(scn)

wrote = []
for name, (verts, tris) in sorted(GOT.items()):
    if not name.startswith('iwa'):
        continue
    base = name.split('__')[0]                       # iwa01__s -> iwa01
    path = os.path.join(OUT, base + '.obj')
    ys = [p[1] for p in verts]; xs = [p[0] for p in verts]; zs = [p[2] for p in verts]
    with open(path, 'w') as f:
        f.write(f"# Brownboo rock '{name}' from gedit/s04/scene.scn — game world coords, Y up, WaterLevel y=0\n")
        f.write(f"# {len(verts)} verts, {len(tris)} tris | X[{min(xs):.1f},{max(xs):.1f}] "
                f"Y[{min(ys):.1f},{max(ys):.1f}] Z[{min(zs):.1f},{max(zs):.1f}]\n")
        f.write(f"o {base}\n")
        for x, y, z in verts:
            f.write(f"v {x:.4f} {y:.4f} {z:.4f}\n")
        for a, b, c in tris:                          # OBJ faces are 1-indexed
            f.write(f"f {a+1} {b+1} {c+1}\n")
    wrote.append((base, len(verts), len(tris), path))

for base, nv, nt, path in wrote:
    print(f"{base}: {nv:4} verts  {nt:4} tris  -> {path}")
print(f"\n{len(wrote)} rock OBJ(s) in {OUT}")
print("Blender: import Y-up; keep the same axis on re-export so coords round-trip. WaterLevel = y=0.")
