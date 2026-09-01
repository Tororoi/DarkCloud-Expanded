#!/usr/bin/env python3
"""Queens h06 (georama part) collision-surgery viewer.

Shows the CURRENT custom `_a` player collision (= the full visual mesh, single node, baked via
queens_parts.bin) as a selectable layer in PART-LOCAL coordinates, plus references: the vanilla
`_a` (obj1+hebi coarse hulls), the vanilla `_c` and the doubled-height `_c`.

Workflow: click / shift+click / shift+drag polys of the "custom _a" layer, copy the triangle list
from the panel, and hand it over with instructions (remove / replace) — edits land in
tools/queens/queens_snake_statue_surgery_data.py (PLAYER_COLLISION_REMOVE_TRIS / PLAYER_COLLISION_ADD_TRIS) and re-export via tools/queens/queens_snake_statue_collision.py.

Run: python3 tools/queens/queens_snake_statue_viewer.py -> game_data/queens/queens_snake_statue_viewer.html
"""
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'lib'))
import toolpath  # noqa: F401 — puts every tools/ subfolder on sys.path
import os, struct, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from extract_scene_mesh import load_scene
import scene_placed
from georama_collision import parse_coll_mdt
from scene_viewer_html import build_html
from queens_snake_statue_collision import full_visual_tris, apply_surgery, camera_hull_cylinder_tris, CAMERA_HULL_SCALE_Y, CAMERA_HULL_RADIUS_MUL, CAMERA_HULL_SEGMENTS

OUT = os.path.join(HERE, "..", "..", "game_data", "queens")
os.makedirs(OUT, exist_ok=True)

scn = load_scene('gedit/e03/scene.scn')
DIR = scene_placed.scn_directory_map(scn)
off, size = DIR['e03h06']
sub = scn[off:off + size]

vis = full_visual_tris(scn, DIR)
cand = apply_surgery(vis)   # what the bake ships (surgery applied)


def coll_block(slot):
    o = struct.unpack_from('<I', sub, slot)[0]
    cnt, tbl = struct.unpack_from('<II', sub, o + 8)
    tris = []
    for i in range(cnt):
        b = o + tbl + i * 0x70
        mo = struct.unpack_from('<i', sub, b + 0x28)[0]
        if mo:
            tris += [list(map(list, t)) for t in parse_coll_mdt(sub, o + mo)]
    return tris


van_a = coll_block(0x78)
van_c = coll_block(0xc0)
new_c = camera_hull_cylinder_tris(sub)   # exact same geometry the bake ships

layers = [
    {'key': 'acand', 'label': f'custom _a (BAKED: full visual, {len(cand)} tris) — SELECT here',
     'tris': cand, 'color': [255, 120, 120], 'alpha': 0.9, 'border': '#f88', 'on': True},
    {'key': 'van_a', 'label': f'vanilla _a (obj1+hebi, {len(van_a)} tris)',
     'tris': van_a, 'color': [120, 255, 140], 'alpha': 0.5, 'border': '#6f8', 'on': False},
    {'key': 'van_c', 'label': f'vanilla _c (cyl, {len(van_c)} tris)',
     'tris': van_c, 'color': [80, 200, 255], 'alpha': 0.4, 'border': '#5bf', 'on': False},
    {'key': 'new_c', 'label': f'BAKED _c (head-centered cylinder: {CAMERA_HULL_SEGMENTS}-gon, height x{CAMERA_HULL_SCALE_Y:g}, r x{CAMERA_HULL_RADIUS_MUL:g}, {len(new_c)} tris)',
     'tris': new_c, 'color': [255, 200, 80], 'alpha': 0.4, 'border': '#fc5', 'on': False},
]
html = build_html(
    title="Queens e03h06 — collision surgery (part-local coords)",
    layers=layers, coord_note="part-local, ground y=0")
path = os.path.join(OUT, "queens_snake_statue_viewer.html")
open(path, "w").write(html)
print(f"-> {os.path.normpath(path)}")
