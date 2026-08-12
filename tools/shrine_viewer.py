#!/usr/bin/env python3
"""Dark Shrine (opening summoning chamber, opdat/dungeon/i01h12*) 3D viewer generator.

The shrine ships as LOOSE MDS files (not a scene.scn): i01h12.mds room + i01h12_w.mds water +
i01h12e1.mds effect dressing + i01h12_a.mds collision (floor-only `yuka_a`), placed by
opdat/opinfo.cfg (all GND/BLD at 0,0,0 -> node matrices ARE world). This viewer decodes each MDS
with the strict shared tooling (mds_surgery node table + extract_scene_mesh MDT readers), applies
the parent-chain node transforms, buckets meshes into layers (structure / urn / liquid / torches /
water / effects / collision), and overlays the opinfo.cfg FIRE torch positions as markers.

Run: python3 tools/shrine_viewer.py  ->  game_data/darkshrine/shrine_viewer.html
Requires $DC1_DATA_DIR (see .env.sample).
"""
import os, re, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mds_surgery import Mds
from extract_scene_mesh import read_verts, read_tris, xform
from georama_collision import parse_coll_mdt
from scene_viewer_html import build_html

D = os.environ.get('DC1_DATA_DIR')
if not D: raise SystemExit("Set $DC1_DATA_DIR to your extracted Dark Cloud disc dir (see .env.sample)")

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "game_data", "darkshrine")   # embeds game geometry -> untracked
os.makedirs(OUT, exist_ok=True)
HTML_NAME = "shrine_viewer.html"

# ── data.dat loader (same index walk as the stb tooling) ─────────────────────────────────────────
_hed = open(os.path.join(D, 'data.hed'), 'rb').read()
_hd2 = open(os.path.join(D, 'data.hd2'), 'rb').read()
_dat = open(os.path.join(D, 'data.dat'), 'rb')

def load(rel):
    for i in range(len(_hed) // 80):
        n = _hed[i*80:i*80+80].split(b'\0')[0].decode('latin1').replace('\\', '/')
        if n.lower() == rel.lower():
            off, sz, _ = struct.unpack_from('<III', _hd2, 16 + i*32)
            _dat.seek(off)
            return _dat.read(sz)
    raise SystemExit(f"not found in data.dat: {rel}")

# ── node-matrix parent chain (same layout xform() expects: column-major 4x4) ─────────────────────
def matmul(P, L):
    M = [0.0]*16
    for c in range(4):
        for r in range(4):
            M[c*4+r] = sum(P[k*4+r] * L[c*4+k] for k in range(4))
    return M

def world_meshes(mds_bytes, collision=False):
    """[(nodeName, worldVerts, tris)] for every mesh node, parent-chain transforms applied.
    collision=True parses the 5-int-record COLLISION MDT layout instead of the visual one
    (see town-collision-format / CreateCollisionMDT)."""
    m = Mds(mds_bytes)
    world = {}
    out = []
    for nd in m.nodes:                       # table order: parents precede children in practice
        pm = world.get(nd.parent)
        wm = matmul(pm, nd.mat) if pm is not None else list(nd.mat)
        world[nd.idx] = wm
        if not nd.mesh_off:
            continue
        if collision:
            ctris = parse_coll_mdt(mds_bytes, nd.mesh_off)
            if ctris:
                wv = [xform(wm, v) for t in ctris for v in t]
                out.append((nd.name, wv, [(i*3, i*3+1, i*3+2) for i in range(len(ctris))]))
            continue
        vs = read_verts(mds_bytes, nd.mesh_off)
        ts = read_tris(mds_bytes, nd.mesh_off)
        if vs and ts:
            wv = [xform(wm, v) for v in vs]
            out.append((nd.name, wv, ts))
    return out

# ── layer bucketing ──────────────────────────────────────────────────────────────────────────────
FILES = [   # (relpath, layer-override or None = classify per node)
    ('opdat/dungeon/i01h12.mds',   None),
    ('opdat/dungeon/i01h12_w.mds', 'water'),
    ('opdat/dungeon/i01h12e1.mds', 'effects'),
    ('opdat/dungeon/i01h12_a.mds', 'collision'),
]
LAYERS_SPEC = [
    # key,        label,                                   color,          border, on
    ('structure', 'shrine structure (i01h12)',             [120, 105, 95], '#cba', True),
    ('urn',       'the urn (tubo__m, glow-flagged)',       [200, 150, 60], '#fc6', True),
    ('liquid',    'liquid (ekitai1-3, OBJ_ROT-spun)',      [90, 200, 170], '#6ec', True),
    ('water',     'water plane (i01h12_w)',                [70, 130, 220], '#5af', True),
    ('effects',   'effect dressing (i01h12e1)',            [200, 90, 200], '#d7d', False),
    ('collision', 'collision _a (yuka_a floor-only!)',     [230, 70, 70],  '#f55', False),
]

def classify(name):
    if name.startswith('tubo'):   return 'urn'
    if name.startswith('ekitai'): return 'liquid'
    return 'structure'

layer_tris = {k: [] for k, *_ in LAYERS_SPEC}
nodelabels = []
for rel, override in FILES:
    for name, wv, ts in world_meshes(load(rel), collision=(override == 'collision')):
        key = override or classify(name)
        tris = [[list(wv[a]), list(wv[b]), list(wv[c])] for a, b, c in ts if max(a, b, c) < len(wv)]
        layer_tris[key].extend(tris)
        if tris:
            xs = [p[0] for t in tris for p in t]; ys = [p[1] for t in tris for p in t]; zs = [p[2] for t in tris for p in t]
            nodelabels.append([[round(sum(xs)/len(xs), 2), round(max(ys) + 0.3, 2), round(sum(zs)/len(zs), 2)],
                               f"{name} ({len(tris)}t)"])

layers = [{'key': k, 'label': lbl, 'tris': layer_tris[k], 'color': col, 'alpha': 1.0,
           'border': bor, 'on': on} for k, lbl, col, bor, on in LAYERS_SPEC]

# ── FIRE torch markers from opinfo.cfg ───────────────────────────────────────────────────────────
points, point_labels = [], []
for ln in load('opdat/opinfo.cfg').decode('shift_jis', 'replace').splitlines():
    mm = re.match(r'\s*FIRE\s+([-\d.]+)\s*,\s*([-\d.]+)\s*,\s*([-\d.]+)', ln)
    if mm:
        p = [float(mm.group(1)), float(mm.group(2)), float(mm.group(3))]
        points.append(p)
        point_labels.append([p, f"FIRE ({p[0]:g},{p[1]:g},{p[2]:g})"])

html = build_html(
    title="Dark Shrine (opdat i01h12) — opening summoning chamber",
    layers=layers, node_labels=nodelabels, points=points, point_labels=point_labels,
    points_label="FIRE torches (opinfo.cfg)", coord_note="opening-local units")
open(os.path.join(OUT, HTML_NAME), 'w').write(html)

tot = sum(len(t) for t in layer_tris.values())
print(f"layers: {len(layers)}  triangles: {tot}  fire markers: {len(points)}")
for k, *_ in LAYERS_SPEC:
    print(f"  {k:10s} {len(layer_tris[k]):5d} tris")
print(f"-> {os.path.join(OUT, HTML_NAME)}")
