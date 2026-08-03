#!/usr/bin/env python3
"""Yellow Drops (map s13) fishing-placement + collision 3D viewer generator.

Decodes the town's meshes from gedit/s13/scene.scn with CORRECT world placement (tools/scene_placed.py):
mapinfo.cfg GROUND instance transforms (position + Y-rotation) + per-node parent-chain accumulation +
strict mdt_codec triangle decode. This fixes the earlier bare-extract_mesh pass, which left instanced
sub-files (buildings/entrances/decoration) stacked at their local origin and let the parse_mds heuristic
emit spurious "distorted" polys. Groups meshes into fishing-relevant layers, marks the CURRENT fishing
rect/trigger/stance from CustomFishingSpot.cs, and writes a self-contained interactive HTML viewer via the
shared renderer (tools/scene_viewer_html.py): per-layer show/hide, fill/wire/backface-cull, node labels, a
cursor->world (x,z) readout for picking sign/trigger spots, and click / shift+click polygon selection ->
copyable triangle list (for fishing collision).

Run: python3 tools/yellowdrops_viewer.py  ->  game_data/yellowdrops/yellowdrops_viewer.html
"""
import os, sys, re, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from scene_placed import placed_meshes
from scene_viewer_html import build_html

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "game_data", "yellowdrops")   # embeds game geometry -> untracked
os.makedirs(OUT, exist_ok=True)
HTML_NAME = "yellowdrops_viewer.html"

PLACED = placed_meshes('gedit/s13/scene.scn', 'gedit/s13/mapinfo.cfg')   # [{name, inst, verts, tris}]

# ---- current Yellow Drops fishing placement (mirror CustomFishingSpot.cs spot 23 — adjust there) ----
WATER_Y = 1.0
RECT = (-609.0, -444.0, -409.0, -244.0)      # x1,z1,x2,z2 (cast box)
TRIG = (-575.0, 9.0, -286.0)                 # trigger (the "!" marker), InteractRadius 10
STANCE = (-582.9, 9.6, -276.8)               # player stance

# ---- layer taxonomy (ordered; first match wins). Fishing-relevant layers ON by default; clutter OFF. ----
LAYERS_SPEC = [
    # key,          predicate,                                                       color,           border, on
    ('watersurf',   lambda n: n.startswith('suimenn'),                               [40,130,120],   '#5db', True),
    ('liquidbottom',lambda n: n.startswith('grid4__cf'),                             [70,150,120],   '#5d8', True),
    ('ground',      lambda n: re.match(r'grid(8|9|10|11)\b', n) is not None,         [90,110,90],    '#8a6', True),
    ('factoryarch', lambda n: n.startswith('iriguti'),                               [200,180,80],   '#dd6', True),
    ('floatrocks',  lambda n: re.match(r'obj[2-5]__n$', n) is not None,              [120,120,150],  '#99c', False),
    ('pillars',     lambda n: n == 'obj3',                                           [175,150,110],  '#db9', True),
    ('outerwalls',  lambda n: n.startswith('face'),                                  [130,120,100],  '#cb9', True),
    ('archways',    lambda n: n.startswith('isi') or n.startswith('extru'),          [190,160,120],  '#eb8', False),
    ('roof',        lambda n: n.startswith('yane') or n.startswith('bou') or n.startswith('eda'),  [150,80,60], '#d86', False),
    ('window',      lambda n: n.startswith('mado'),                                  [110,190,220],  '#7ce', False),
    ('door',        lambda n: n.startswith('door'),                                  [200,140,70],   '#e94', False),
    ('building',    lambda n: n.startswith('house') or n.startswith('bool13') or n.startswith('waku')
                              or n in ('obj8','obj11') or n.startswith('torus') or n.startswith('cyl'), [150,110,80], '#c96', False),
    ('drops',       lambda n: n.startswith('tama'),                                  [235,210,90],   '#ec6', False),
    ('lights',      lambda n: n.startswith('light') or n.startswith('akari') or n.startswith('hikari') or n.startswith('tuki'), [220,200,120], '#ec8', False),
    ('factory',      lambda n: n == 'sphere27',                                     [90,105,135],   '#9ac', False),
    ('factoryroof',  lambda n: n.startswith('sphere'),                              [120,95,150],   '#b8e', False),
    ('factorywindows', lambda n: n == 'doumu_c' or n in ('obj22__n','obj28__n'),    [110,180,210],  '#8cd', False),
    ('decor',       lambda n: n.startswith('revol') or n.startswith('naka') or n.startswith('cube') or n.startswith('kage'), [110,100,120], '#a9c', False),
]

def layer_of(name):
    for key, pred, *_ in LAYERS_SPEC:
        if pred(name):
            return key
    return 'other'

# Build per-layer world-triangle lists + per-node labels, one entry per PLACED instance.
layer_tris = {key: [] for key, *_ in LAYERS_SPEC}
layer_tris['other'] = []
nodelabels = []
_inst_count = {}
for pm in PLACED:
    name, v, ts = pm['name'], pm['verts'], pm['tris']
    key = layer_of(name)
    tris = [[list(v[a]), list(v[b]), list(v[c])] for a, b, c in ts]
    layer_tris[key].extend(tris)
    xs = [p[0] for t in tris for p in t]; ys = [p[1] for t in tris for p in t]; zs = [p[2] for t in tris for p in t]
    cen = [round(sum(xs)/len(xs), 1), round(sum(ys)/len(ys), 1), round(sum(zs)/len(zs), 1)]
    bb = [round(min(xs),1), round(min(ys),1), round(min(zs),1), round(max(xs),1), round(max(ys),1), round(max(zs),1)]
    label = name if pm['inst'] == 0 else f"{name}#{pm['inst']}"
    nodelabels.append([cen, label, bb, key])

LABELS = {'watersurf':'water surface (suimenn)', 'liquidbottom':'liquid BOTTOM (grid4)',
          'ground':'ground', 'outerwalls':'outer walls (faces)', 'pillars':'pillars (obj3)',
          'terrain':'terrain / land', 'factoryarch':'factory entrance arches', 'floatrocks':'floating rocks',
          'archways':'archways (isi/extru)', 'roof':'roofs', 'window':'windows', 'door':'doors',
          'building':'buildings', 'drops':'drops (tama)', 'lights':'lights',
          'factory':'factory', 'factoryroof':'factory roof', 'factorywindows':'factory windows',
          'decor':'decoration', 'other':'other / unclassified'}
COLORS = {key: (c, brd) for key, pred, c, brd, on in LAYERS_SPEC}
layers = []
for key, pred, color, border, on in LAYERS_SPEC:
    layers.append({'key': key, 'label': f"{LABELS[key]}", 'tris': layer_tris[key],
                   'color': color, 'alpha': 1.0 if key not in ('watersurf','liquidbottom') else 0.45,
                   'border': border, 'on': on})
# ---- trigger marker as a SOLID (triangulated) sphere layer, not a point cloud ----
def tri_sphere(cx, cy, cz, r, n=9):
    def P(i, j):
        th = math.pi * i / n; ph = 2 * math.pi * j / (2 * n)
        return [cx + r*math.sin(th)*math.cos(ph), cy + r*math.cos(th), cz + r*math.sin(th)*math.sin(ph)]
    tris = []
    for i in range(n):
        for j in range(2 * n):
            a, b, c, d = P(i, j), P(i+1, j), P(i+1, j+1), P(i, j+1)
            tris += [[a, b, c], [a, c, d]]
    return tris

layers.append({'key': 'trigger', 'label': 'trigger ! (sphere)', 'tris': tri_sphere(*TRIG, 10),
               'color': [255,110,180], 'alpha': 0.9, 'border': '#f7c', 'on': True})

# ---- markers: the current cast rect (outline points) + stance; trigger is a mesh layer above ----
def rect_points(x1, z1, x2, z2, y, per=40):
    pts = []
    for a in range(per + 1):
        t = a / per
        pts += [[x1 + (x2 - x1) * t, y, z1], [x1 + (x2 - x1) * t, y, z2],
                [x1, y, z1 + (z2 - z1) * t], [x2, y, z1 + (z2 - z1) * t]]
    return pts

points = rect_points(*RECT, WATER_Y) + [list(STANCE), [0, WATER_Y, 0]]
point_labels = [
    [list(TRIG), f"trigger ({TRIG[0]:.0f},{TRIG[1]:.0f},{TRIG[2]:.0f})"],
    [list(STANCE), f"stance ({STANCE[0]:.0f},{STANCE[1]:.0f},{STANCE[2]:.0f})"],
    [[RECT[0], WATER_Y, RECT[1]], f"rect ({RECT[0]:.0f},{RECT[1]:.0f})"],
    [[RECT[2], WATER_Y, RECT[3]], f"rect ({RECT[2]:.0f},{RECT[3]:.0f})"],
    [[0, WATER_Y, 0], "0,0"],
]

# toggle-panel folders (scene_viewer_html folder UI)
for L in layers:
    L['group'] = ('Water' if L['key'] in ('watersurf', 'liquidbottom')
                  else 'Fishing spot' if L['key'] == 'trigger' else 'Scene meshes')

# LOD comparison toggles (shared helper). Scanned: s13 ships NO _1/_2 variant meshes (only the five
# e-towns carry LOD chains), so this is empty today — the folder auto-appears if the data ever has them.
from georama_parts import lod_layers
layers += lod_layers('gedit/s13/scene.scn', r's13\d\d')

html = build_html(
    title="Yellow Drops (s13) — fishing placement + collision",
    layers=layers, node_labels=nodelabels, points=points, point_labels=point_labels,
    points_label="cast rect + trigger + stance", coord_note="liquid y=1")
open(os.path.join(OUT, HTML_NAME), 'w').write(html)

tot = sum(len(t) for t in layer_tris.values())
print(f"placed instances: {len(PLACED)}  layers: {len(layers)}  triangles: {tot}")
for key, *_ in LAYERS_SPEC:
    print(f"  {key:14s} {len(layer_tris[key]):5d} tris")
if layer_tris['other']:
    print(f"  WARNING: {len(layer_tris['other'])} unclassified tris dropped (no 'other' toggle)")
print(f"-> {os.path.join(OUT, HTML_NAME)}")
