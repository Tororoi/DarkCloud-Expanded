#!/usr/bin/env python3
"""Queens (map e03) fishing-placement + collision 3D viewer generator.

Decodes gedit/e03/scene.scn with correct world placement (tools/scene_placed.py: mapinfo GROUND transforms
+ parent-chain accumulation + strict mdt_codec decode), groups meshes into layers, marks the CURRENT fishing
rect/trigger from CustomFishingSpot.cs (Queens canal, MapNo 2), and writes a self-contained interactive HTML
viewer via the shared renderer (tools/scene_viewer_html.py): per-layer show/hide, fill/wire/backface-cull,
node labels, a cursor->world (x,z) readout for picking sign/trigger spots, click / shift+click polygon
selection -> copyable triangle list, and a z-buffer fill (half-res while dragging/zooming).

Run: python3 tools/queens_viewer.py  ->  game_data/queens/queens_viewer.html
"""
import os, sys, re, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from scene_placed import placed_meshes
from scene_viewer_html import build_html
from extract_scene_mesh import load_scene, read_verts, read_tris
from georama_parts import part_models
from georama_default import default_layout
from georama_collision import collision_local, place_base
from queens_fishing_collision import fishing_collision_tris
from bake_terrain_camera_collision import kd_split
from invisible_walls import invisible_tris

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "game_data", "queens")   # embeds game geometry -> untracked
os.makedirs(OUT, exist_ok=True)
HTML_NAME = "queens_viewer.html"

PLACED = placed_meshes('gedit/e03/scene.scn', 'gedit/e03/mapinfo.cfg')   # [{name, inst, verts, tris, sub}]

# WATER_SURFACE regions (runtime CWater planes, NOT scene meshes) — the fishing canal is one of these.
# Format: WATER_SURFACE "name", tileX, tileZ / min(x,y,z) / max(x,y,z) / pos(x,y,z). Plane at Y=pos.y,
# spanning [min.x,max.x]+pos.x by [min.z,max.z]+pos.z. Returns one dict per plane (kept separate so the
# canal can be told apart from the small fountain-associated pools).
def water_surface_planes(cfg):
    lines = cfg.splitlines(); planes = []; i = 0
    def numrow(s):
        s = s.split('//')[0].strip()
        if not re.match(r'^-?\d', s): return None
        try: return [float(p) for p in re.split(r'[,\t ]+', s) if p]
        except ValueError: return None
    while i < len(lines):
        m = re.match(r'\s*WATER_SURFACE\s+"([^"]*)"', lines[i])
        if m:
            name, nums, j = m.group(1), [], i + 1
            while j < len(lines) and len(nums) < 3 and not re.match(r'\s*WATER_S', lines[j]):
                r = numrow(lines[j])
                if r and len(r) >= 3: nums.append(r[:3])
                j += 1
            if len(nums) >= 3:
                mn, mx, pos = nums
                x1, z1, x2, z2 = mn[0]+pos[0], mn[2]+pos[2], mx[0]+pos[0], mx[2]+pos[2]
                planes.append({'name': name, 'y': pos[1], 'x1': x1, 'z1': z1, 'x2': x2, 'z2': z2,
                               'w': abs(x2-x1), 'd': abs(z2-z1)})
            i = j
        else:
            i += 1
    return planes

def plane_quads(p, y=None):
    y = p['y'] if y is None else y
    a, b, c, d = [p['x1'],y,p['z1']], [p['x2'],y,p['z1']], [p['x2'],y,p['z2']], [p['x1'],y,p['z2']]
    return [[a, b, c], [a, c, d]]

WATER_PLANES = water_surface_planes(load_scene('gedit/e03/mapinfo.cfg').decode('latin1', 'replace'))
# canal = the largest-footprint plane (the fishing water at y=31); the small ones are the fountain pools.
CANAL = max(WATER_PLANES, key=lambda p: p['w'] * p['d'])

# ---- current Queens fishing placement (mirror CustomFishingSpot.cs spot 2 — adjust there) ----
WATER_Y = 31.0
RECT = (-240.0, -100.0, 900.0, 150.0)        # x1,z1,x2,z2 (fish/cast box)
TRIG = (250.0, 70.0, -70.0)                  # trigger (the "!" marker); radius 10
TRIG_R = 10.0
SIGN_POS = (250.0, 70.0, -64.0)              # baked sign position (QSIGN_* in IsoPatcher.cs): 6 units south (+Z) of the trigger
SIGN_RY  = 180                               # mapinfo ry (degrees): facing north (-Z), opposite Brownboo's +Z-facing ry 0

# ---- layer taxonomy (ordered; first match wins). Main geometry ON by default; clutter OFF. ----
def _num(prefix, n):
    m = re.match(prefix + r'(\d+)', n)
    return int(m.group(1)) if m else None

LAYERS_SPEC = [
    # key,         predicate,                                             color,           border, on
    ('ground',    lambda n: n.startswith('grid'),                        [90,110,90],     '#8a6', False),
    ('walls',     lambda n: n.startswith('kabe'),                        [130,120,100],   '#cb9', False),
    ('awnings',   lambda n: n.startswith('hiyoke'),                      [150,90,70],     '#d87', False),
    # palm trees = leaves (ha*), trunks (cyl*), bases (cube* except cube41 = lamppost)
    ('lamppost',  lambda n: _num('cube', n) == 41,                       [220,200,120],   '#ec8', False),
    ('palmtrees', lambda n: n.startswith('ha') or n.startswith('cyl')
                            or n.startswith('cube'),                     [90,160,90],     '#7c6', False),
    ('poles',     lambda n: re.match(r'k\d+__', n) is not None,          [190,170,120],   '#eb9', False),
    ('windows',   lambda n: n.startswith('win'),                         [110,190,220],   '#7ce', False),
    ('lights',    lambda n: n.startswith('light'),                       [220,200,120],   '#ec8', False),
    ('ships',     lambda n: _num('obj', n) in (38, 41),                  [170,140,110],   '#da7', False),
    ('bridges',   lambda n: _num('obj', n) in (40, 44),                  [180,150,120],   '#eb8', False),
    ('pipes',     lambda n: _num('obj', n) in (1, 9),                    [130,150,170],   '#9bd', False),
    ('shortwalls',lambda n: _num('obj', n) == 42,                        [160,130,100],   '#c96', False),
    ('structures',lambda n: n.startswith('obj'),                         [150,110,80],    '#c96', False),
]

def layer_of(name):
    for key, pred, *_ in LAYERS_SPEC:
        if pred(name):
            return key
    return 'other'

# Build per-layer world-triangle lists + per-node labels, one entry per PLACED instance.
layer_tris = {key: [] for key, *_ in LAYERS_SPEC}
layer_tris['other'] = []
layer_tris['water'] = []                       # WATER-command meshes (e03c* canal water)
nodelabels = []
for pm in PLACED:
    name, v, ts = pm['name'], pm['verts'], pm['tris']
    key = 'water' if pm.get('sub', '').startswith('e03c') else layer_of(name)
    tris = [[list(v[a]), list(v[b]), list(v[c])] for a, b, c in ts]
    layer_tris[key].extend(tris)
    xs = [p[0] for t in tris for p in t]; ys = [p[1] for t in tris for p in t]; zs = [p[2] for t in tris for p in t]
    cen = [round(sum(xs)/len(xs), 1), round(sum(ys)/len(ys), 1), round(sum(zs)/len(zs), 1)]
    bb = [round(min(xs),1), round(min(ys),1), round(min(zs),1), round(max(xs),1), round(max(ys),1), round(max(zs),1)]
    label = name if pm['inst'] == 0 else f"{name}#{pm['inst']}"
    nodelabels.append([cen, label, bb, key])

LABELS = {'ground':'ground / grids', 'walls':'walls (kabe)', 'awnings':'awnings (hiyoke)',
          'lamppost':'lamppost (cube41)', 'palmtrees':'palm trees (ha/cyl/cube)', 'poles':'poles (k1-10)',
          'windows':'windows', 'lights':'lights',
          'ships':'ships (obj38/41)', 'bridges':'bridges (obj40/44)', 'pipes':'pipes (obj1/9)',
          'shortwalls':'short walls (obj42)', 'structures':'structures (obj)'}
layers = []
for key, pred, color, border, on in LAYERS_SPEC:
    layers.append({'key': key, 'label': LABELS[key], 'tris': layer_tris[key],
                   'color': color, 'alpha': 1.0, 'border': border, 'on': on})
# water: the WATER-command canal meshes (e03c*) + the WATER_SURFACE runtime planes (fishing surface @ Y=31)
layers.append({'key': 'water', 'label': 'water meshes (e03c*)', 'tris': layer_tris['water'],
               'color': [40,130,150], 'alpha': 0.7, 'border': '#5cd', 'on': True})
# each WATER_SURFACE plane as its own layer so the canal is distinguishable from the small fountain pools
for idx, p in enumerate(WATER_PLANES):
    is_canal = p is CANAL
    tag = 'canal' if is_canal else (p['name'] or 'pool')
    layers.append({'key': f'ws{idx}',
                   'label': f"WATER_SURFACE #{idx}: {tag} y={p['y']:.1f} ({p['w']:.0f}x{p['d']:.0f})",
                   'tris': plane_quads(p), 'color': [50,150,170] if is_canal else [180,120,200],
                   'alpha': 0.45, 'border': '#6de' if is_canal else '#c9e',
                   'on': not is_canal})   # canal plane hidden by default; the tide layers below stand in for it

# ---- time-of-day canal tide levels (raise CANAL water Y) — bridge arch crown underside is Y=60 ----
# afternoon = current/low (y=31); morning & dusk slightly higher; night highest but barely under the arch.
TIDES = [('afternoon', 31.0, [70,150,175], '#6cf', True),
         ('morning/dusk', 45.0, [90,180,180], '#7dd', True),
         ('night (high)', 55.0, [90,120,210], '#89f', True)]
for tname, ty, col, bd, on in TIDES:
    layers.append({'key': f'tide_{tname.split("/")[0].split(" ")[0]}',
                   'label': f'tide: {tname} y={ty:.0f}', 'tris': plane_quads(CANAL, ty),
                   'color': col, 'alpha': 0.55, 'border': bd, 'on': on})

# base-ground collision (the `_a`/atari meshes of e03g04/e03g05) — SPLIT per node so we can see which
# terrain node is the canal-water containment vs the real terrain walls. These are the two origin STATIC
# parts the mod's CameraWallCollision currently EXCLUDES wholesale (the over-correction) — each node is its
# own toggle + label so we can decide which to actually drop.
BASE_COLL = place_base('gedit/e03/scene.scn', 'gedit/e03/mapinfo.cfg', r'e03g\d\d$')
_bgcol = {'e03g04': [235, 55, 55], 'e03g05': [235, 150, 40]}
for _nm, _tris in BASE_COLL.items():
    if not _tris: continue
    _vs = [p for t in _tris for p in t]
    _cen = [sum(v[i] for v in _vs)/len(_vs) for i in range(3)]
    _bb = [min(v[0] for v in _vs), min(v[1] for v in _vs), min(v[2] for v in _vs),
           max(v[0] for v in _vs), max(v[1] for v in _vs), max(v[2] for v in _vs)]
    _key = f'bg_{_nm}'
    layers.append({'key': _key, 'label': f'base-ground _a: {_nm} ({len(_tris)}) — camera EXCLUDES', 'tris': _tris,
                   'color': _bgcol.get(_nm, [220, 60, 60]), 'alpha': 0.55, 'border': '#e44', 'on': False})
    nodelabels.append([_cen, f'{_nm} (_a coll)', _bb, _key])
    print(f"base-ground {_nm}: {len(_tris)} tris  xz x[{_bb[0]:.0f},{_bb[3]:.0f}] z[{_bb[2]:.0f},{_bb[5]:.0f}]  (canal water z[-50,50])")

# ---- SPLIT camera-collision NODES: take the VISIBLE meshes of the specified terrain-structure nodes
#      (grid3*, obj40/44 bridges, obj1/9 pipes, obj6/33/34/43/45, kanban) and split a COPY of each into
#      <=100-poly collision nodes (kd_split). This is the geometry the camera would actually collide with;
#      NOT the flat ground _a and NOT the buildings. One colour per node so the split is visible. ON by default.
def _hue(i, n):
    h = (i / max(n, 1)) * 6.0
    x = 1 - abs(h % 2 - 1)
    r, g, b = [(1, x, 0), (x, 1, 0), (0, 1, x), (0, x, 1), (x, 0, 1), (1, 0, x)][int(h) % 6]
    return [int(60 + 195 * c) for c in (r, g, b)]

def _is_cam_node(nm):
    return (nm.startswith('grid3')
            or _num('obj', nm) in (40, 44, 1, 9, 6, 33, 34, 43, 45, 42))

# per specified node instance: its world visible tris -> kd_split -> <=100-poly collision nodes
_cam_srcs = []
for pm in PLACED:
    if not _is_cam_node(pm['name']):
        continue
    v, ts = pm['verts'], pm['tris']
    tris = [[list(v[a]), list(v[b]), list(v[c])] for a, b, c in ts]
    if tris:
        label = pm['name'] if pm['inst'] == 0 else f"{pm['name']}#{pm['inst']}"
        _cam_srcs.append((label, kd_split(tris, 100)))

_total_nodes = sum(len(b) for _, b in _cam_srcs)
_src_tris = sum(sum(len(bk) for bk in b) for _, b in _cam_srcs)

# Give every SOURCE a unique name up front: nodes that share a name (grid3_4_1_1_1_1_ x4, all inst=0 from
# placed_meshes) get an occurrence suffix _0/_1/... so the name alone identifies the source. Everything
# downstream (viewer keys, and eventually the baked collision MDS node names) keys off this unique name —
# no bare position or running-index needed to bind.
_namecount = {}
for _l, _ in _cam_srcs:
    _namecount[_l] = _namecount.get(_l, 0) + 1
_occ, _uniq = {}, []
for _l, _ in _cam_srcs:
    if _namecount[_l] > 1:
        _k = _occ.get(_l, 0); _occ[_l] = _k + 1
        _uniq.append(f'{_l}_{_k}')
    else:
        _uniq.append(_l)

_ni = 0
for _si, (_srcname, _buckets) in enumerate(_cam_srcs):
    _uname = _uniq[_si]
    for _bi, _bt in enumerate(_buckets):
        _vs = [p for t in _bt for p in t]
        _cen = [sum(v[i] for v in _vs) / len(_vs) for i in range(3)]
        _bb = [min(v[0] for v in _vs), min(v[1] for v in _vs), min(v[2] for v in _vs),
               max(v[0] for v in _vs), max(v[1] for v in _vs), max(v[2] for v in _vs)]
        _key = f'camnode_{_uname}_{_bi}'            # unique because _uname is unique
        layers.append({'key': _key, 'label': f'cam node {_uname}#{_bi} ({len(_bt)} polys)', 'tris': _bt,
                       'color': _hue(_ni, _total_nodes), 'alpha': 0.85, 'border': '#000', 'on': True})
        nodelabels.append([_cen, f'{_uname}#{_bi}', _bb, _key])
        _ni += 1
assert len(set(_uniq)) == len(_uniq), "source names still not unique"
print(f"split camera-collision nodes (from VISIBLE meshes): {_total_nodes} nodes (<=100 polys) "
      f"from {len(_cam_srcs)} uniquely-named sources, {_src_tris} tris")

# ---- INVISIBLE WALLS (player-only collision, baked as siblings of camcol so the camera never gathers them):
#      the canal containment + western top-perimeter walls. Shown as a distinct toggle. ON by default.
_INV = invisible_tris('e03')
layers.append({'key': 'invwalls', 'label': f'invisible walls: canal (PLAYER ONLY, {len(_INV)})', 'tris': _INV,
               'color': [235, 40, 120], 'alpha': 0.7, 'border': '#e28', 'on': True})
# generated perimeter wall (from the outer edge of the reference outline) + the reference for comparison
from perimeter_wall import perimeter_wall_tris, _parse as _pw_parse, _REF_E03
_PERIM = perimeter_wall_tris('e03')
layers.append({'key': 'perimwall', 'label': f'perimeter wall: generated (CAMERA + player, {len(_PERIM)})', 'tris': _PERIM,
               'color': [255, 90, 40], 'alpha': 0.6, 'border': '#f62', 'on': True})
_PREF = [[list(p) for p in t] for t in _pw_parse(_REF_E03)]
layers.append({'key': 'perimref', 'label': f'perimeter reference outline ({len(_PREF)})', 'tris': _PREF,
               'color': [120, 120, 255], 'alpha': 0.5, 'border': '#88f', 'on': False})
print(f"player-only: {len(_INV)} canal + {len(_PERIM)} perimeter-wall tris (ref {len(_PREF)})")

# ---- CUSTOM fishing collision (appended to cpoly at fishing time, exported to queens_2.bin): the two
#      bridge meshes + obj9 pipes (full, unsimplified) + the hand-authored canal containment walls. This is
#      what actually contains the fish, replacing the useless vanilla walls the mod drops. ----
FISHCOLL = fishing_collision_tris()
layers.append({'key': 'fc_bridges', 'label': f'fish-coll: bridges obj40/44 ({len(FISHCOLL["bridges"])})',
               'tris': FISHCOLL['bridges'], 'color': [90,160,230], 'alpha': 1.0, 'border': '#6af', 'on': False})
layers.append({'key': 'fc_pipes', 'label': f'fish-coll: pipes obj9 ({len(FISHCOLL["pipes"])})',
               'tris': FISHCOLL['pipes'], 'color': [120,200,220], 'alpha': 1.0, 'border': '#8ce', 'on': False})
layers.append({'key': 'fc_contain', 'label': f'fish-coll: canal walls ({len(FISHCOLL["contain"])})',
               'tris': FISHCOLL['contain'], 'color': [255,140,60], 'alpha': 0.85, 'border': '#f93', 'on': False})

# ---- VANILLA fishing cpoly: the exact polys PickUpPoly gathered at the spot, dumped live from RAM by
#      FishingCollision.DumpFullGather (needs CustomFishingSpot.Diagnostics=true + DC_DUMP_DIR set; writes
#      game_data/queens/vanilla_cpoly.csv). Split by NORMALISED |normal.Y| the same way the mod does: the
#      floors-only filter KEEPS |ny|>0.2 (floors+slopes) and DROPS |ny|<=0.2 (walls). Walls are what would
#      contain the fish, so seeing them is the point (the probe warned there are none near this spot).
def load_vanilla_cpoly(path):
    floors, slopes, walls = [], [], []
    if not os.path.exists(path):
        return None
    with open(path) as fh:
        next(fh, None)   # header
        for ln in fh:
            p = ln.strip().split(',')
            if len(p) < 12:
                continue
            try:
                f = [float(x) for x in p]
            except ValueError:
                continue
            tri = [[f[0], f[1], f[2]], [f[3], f[4], f[5]], [f[6], f[7], f[8]]]
            nl = math.hypot(f[9], f[10], f[11]) or 1.0
            ny = abs(f[10] / nl)
            (walls if ny <= 0.2 else floors if ny > 0.7 else slopes).append(tri)
    return floors, slopes, walls

VAN = load_vanilla_cpoly(os.path.join(OUT, 'vanilla_cpoly.csv'))
if VAN is not None:
    van_floors, van_slopes, van_walls = VAN
    tot_van = len(van_floors) + len(van_slopes) + len(van_walls)
    print(f"vanilla cpoly: {tot_van} polys — {len(van_floors)} floor + {len(van_slopes)} slope (KEEP) + "
          f"{len(van_walls)} wall (DROP)")
    layers.append({'key': 'vanfloor', 'label': f'vanilla cpoly: floors ({len(van_floors)}, kept)',
                   'tris': van_floors, 'color': [80,200,120], 'alpha': 0.85, 'border': '#5d8', 'on': False})
    layers.append({'key': 'vanslope', 'label': f'vanilla cpoly: slopes ({len(van_slopes)}, kept)',
                   'tris': van_slopes, 'color': [200,190,70], 'alpha': 0.85, 'border': '#dd6', 'on': False})
    layers.append({'key': 'vanwall', 'label': f'vanilla cpoly: walls ({len(van_walls)}, dropped)',
                   'tris': van_walls, 'color': [230,90,90], 'alpha': 0.85, 'border': '#e77', 'on': False})
else:
    print("vanilla cpoly: (no game_data/queens/vanilla_cpoly.csv — set Diagnostics=true + DC_DUMP_DIR, "
          "then fish Queens to dump it)")

# ---- trigger marker as a solid (triangulated) sphere layer ----
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

layers.append({'key': 'trigger', 'label': f'trigger ! (r{TRIG_R:.0f})', 'tris': tri_sphere(*TRIG, TRIG_R),
               'color': [255,110,180], 'alpha': 0.9, 'border': '#f7c', 'on': False})

# ---- fishing SIGN: the real kanban.mds mesh at its baked position, ry 180 (facing north) ----
# Same source the ISO patcher injects (carved from the user's ISO into game_data/fishsign/; untracked).
# Verts are LOCAL (origin-centred); the mapinfo places + rotates them, so we do the same here.
_kb = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "game_data", "fishsign", "kanban.mds"), "rb").read()
_sv = read_verts(_kb, 0x80)                  # kanban MDT is at 0x80
def _place_sign(v):
    th = math.radians(SIGN_RY); c = math.cos(th); s = math.sin(th)
    x, y, z = v[0], v[1], v[2]
    return [x*c + z*s + SIGN_POS[0], y + SIGN_POS[1], -x*s + z*c + SIGN_POS[2]]
sign_mesh = [[_place_sign(_sv[i]) for i in (a, b, c)] for a, b, c in read_tris(_kb, 0x80)]
print(f"sign mesh: {len(sign_mesh)} tris at {SIGN_POS} ry{SIGN_RY}")

layers.append({'key': 'sign', 'label': 'fishing sign (faces N)',
               'tris': sign_mesh,
               'color': [210,160,90], 'alpha': 1.0, 'border': '#e94', 'on': True})

# ---- fishing-sign COLLISION: the single solid panel the ISO patcher bakes (BuildKanbanCollision).
# Must match IsoPatcher.BuildKanbanCollision Box(-6.5, 6.5, 0, 16, -1, 2), placed like the sign (ry 180).
def _box_tris(x0, x1, y0, y1, z0, z1):
    v = [[x0,y0,z0],[x1,y0,z0],[x1,y0,z1],[x0,y0,z1],[x0,y1,z0],[x1,y1,z0],[x1,y1,z1],[x0,y1,z1]]
    faces = [(0,1,2),(0,2,3),(4,6,5),(4,7,6),(0,4,5),(0,5,1),(3,2,6),(3,6,7),(0,3,7),(0,7,4),(1,5,6),(1,6,2)]
    return [[_place_sign(v[a]), _place_sign(v[b]), _place_sign(v[c])] for a, b, c in faces]
sign_coll = _box_tris(-6.5, 6.5, 0, 16, -1, 2)
print(f"sign collision: {len(sign_coll)} tris (single panel)")
layers.append({'key': 'signcol', 'label': 'sign collision (kanban_a)',
               'tris': sign_coll,
               'color': [230,60,60], 'alpha': 0.35, 'border': '#e33', 'on': False})

# ---- markers: the current cast rect (outline points) + origin ----
def rect_points(x1, z1, x2, z2, y, per=40):
    pts = []
    for a in range(per + 1):
        t = a / per
        pts += [[x1 + (x2 - x1) * t, y, z1], [x1 + (x2 - x1) * t, y, z2],
                [x1, y, z1 + (z2 - z1) * t], [x2, y, z1 + (z2 - z1) * t]]
    return pts

points = rect_points(*RECT, WATER_Y) + [[0, WATER_Y, 0]]
point_labels = [
    [list(TRIG), f"trigger ({TRIG[0]:.0f},{TRIG[1]:.0f},{TRIG[2]:.0f}) r{TRIG_R:.0f}"],
    [list(SIGN_POS), f"sign ({SIGN_POS[0]:.0f},{SIGN_POS[1]:.0f},{SIGN_POS[2]:.0f})"],
    [[RECT[0], WATER_Y, RECT[1]], f"rect ({RECT[0]:.0f},{RECT[1]:.0f})"],
    [[RECT[2], WATER_Y, RECT[3]], f"rect ({RECT[2]:.0f},{RECT[3]:.0f})"],
    [[0, WATER_Y, 0], "0,0"],
]

# ---- CameraDiag samples: player (ref) + camera (campos) pairs, matched colour per pair ----
# Straight from the in-game log. Player = small sphere, camera = big sphere, thin line links the pair.
# Toggle each pair on/off to read how close the camera sits to the buildings behind it.
def line_tris(a, b, w=1.5):
    ax, ay, az = a; bx, by, bz = b
    dx, dz = bx - ax, bz - az; L = math.hypot(dx, dz) or 1.0
    px, pz = -dz / L * w, dx / L * w
    v0 = [ax+px, ay, az+pz]; v1 = [ax-px, ay, az-pz]; v2 = [bx-px, by, bz-pz]; v3 = [bx+px, by, bz+pz]
    return [[v0, v1, v2], [v0, v2, v3]]

CAMLOG = [   # (player x,y,z), (camera x,y,z), dist, colour
    ((1201.0, 142.0, -58.4), (1197.9, 147.0, -70.1), 12.1, [235, 45, 45]),
    ((403.4,  84.0,  81.4),  (403.2,  89.0, 110.1),  28.8, [45, 200, 80]),
    ((403.6,  84.0,  55.0),  (403.2,  89.0, 110.1),  55.1, [55, 130, 245]),
    ((214.9,  84.0,  55.0),  (214.5,  89.0, 110.2),  55.2, [240, 150, 35]),
    ((376.3,  84.0,  55.8),  (403.3,  89.0, 110.1),  60.7, [185, 80, 225]),
]
for i, (pl, cm, dist, col) in enumerate(CAMLOG, 1):
    tris = tri_sphere(pl[0], pl[1], pl[2], 5) + tri_sphere(cm[0], cm[1], cm[2], 8) + line_tris(pl, cm)
    layers.append({'key': f'cam{i}', 'label': f'pair {i}: player + camera (dist {dist:.0f})',
                   'tris': tris, 'color': col, 'alpha': 0.95, 'border': '#fff', 'on': False})
    point_labels.append([list(pl), f"P{i} PLAYER ({pl[0]:.0f},{pl[1]:.0f},{pl[2]:.0f})"])
    point_labels.append([list(cm), f"C{i} CAMERA ({cm[0]:.0f},{cm[1]:.0f},{cm[2]:.0f}) dist {dist:.0f}"])
print(f"camera-log pairs: {len(CAMLOG)} (small sphere=player, big sphere=camera, line links the pair)")

# ---- ray-cast player->camera against the town geometry: where's the actual wall vs where the camera parked?
def _ray_tri(o, d, a, b, c):
    e1 = [b[i]-a[i] for i in range(3)]; e2 = [c[i]-a[i] for i in range(3)]
    h = [d[1]*e2[2]-d[2]*e2[1], d[2]*e2[0]-d[0]*e2[2], d[0]*e2[1]-d[1]*e2[0]]
    det = sum(e1[i]*h[i] for i in range(3))
    if abs(det) < 1e-7: return None
    f = 1.0/det; s = [o[i]-a[i] for i in range(3)]
    u = f*sum(s[i]*h[i] for i in range(3))
    if u < -1e-4 or u > 1.0001: return None
    q = [s[1]*e1[2]-s[2]*e1[1], s[2]*e1[0]-s[0]*e1[2], s[0]*e1[1]-s[1]*e1[0]]
    v = f*sum(d[i]*q[i] for i in range(3))
    if v < -1e-4 or u+v > 1.0001: return None
    t = f*sum(e2[i]*q[i] for i in range(3))
    return t if t > 0.05 else None
# collect solid geometry (buildings/structures/collision), skip our markers, water, tide, grids
SKIP = {f'cam{i}' for i in range(1, len(CAMLOG)+1)} | {'water'} | {k for k in layer_tris if k.startswith('ws') or k.startswith('tide')}
walltris = [t for L in layers if L['key'] not in SKIP and not L['key'].startswith(('cam','ws','tide')) for t in L['tris']]
print(f"\n=== ray-cast player->camera vs {len(walltris)} town tris ===")
for i, (pl, cm, dist, _) in enumerate(CAMLOG, 1):
    d = [cm[j]-pl[j] for j in range(3)]; L = math.hypot(*d) or 1.0; d = [x/L for x in d]
    hits = [t for t in (_ray_tri(list(pl), d, *tri) for tri in walltris) if t is not None]
    nearest = min(hits) if hits else None
    beyond = [h for h in hits if h > dist+1]
    firstbeyond = min(beyond) if beyond else None
    print(f"  pair {i}: camera at dist {dist:.1f} | first wall along ray at "
          f"{('%.1f'%nearest) if nearest else 'NONE'} | first wall BEYOND camera at "
          f"{('%.1f'%firstbeyond) if firstbeyond else 'NONE'}")

# ---- georama editor data: parts (models + footprints), the 3 edit regions, and the default layout ----
# Grid cell = 100; regions RE'd from mapinfo EDITAREA (grids at Y=170/70/0); footprints per user rule
# (h06=2x2, others 3x3/3x2, trees/roads 1x1); default layout decoded from gdata0.edt.
GREGIONS = [
    {'id': 0, 'x0': 700.0,  'z0': 300.0,  'nx': 7,  'nz': 9, 'y': 170.0},
    {'id': 1, 'x0': -300.0, 'z0': -900.0, 'nx': 11, 'nz': 7, 'y': 70.0},
    {'id': 2, 'x0': -200.0, 'z0': 400.0,  'nx': 7,  'nz': 8, 'y': 0.0},
]
GFOOT = {'e03h06': (2, 2), 'e03h01': (3, 2), 'e03h02': (3, 2), 'e03h03': (3, 2)}   # rest -> 3x3
COLL = collision_local('gedit/e03/scene.scn', r'e03[hrt]\d')   # per-part `_a` collision, sub-file space
GPARTS = {}
for nm, d in part_models('gedit/e03/scene.scn', r'e03[hrt]\d').items():
    miny = d['bbox'][1]
    bx = [p[0] for t in d['tris'] for p in t]; bz = [p[2] for t in d['tris'] for p in t]
    cxm = (min(bx) + max(bx)) / 2; czm = (min(bz) + max(bz)) / 2
    gtris = [[[p[0] - cxm, p[1] - miny, p[2] - czm] for p in t] for t in d['tris']]
    kind = 'road' if nm.startswith('e03r') else 'tree' if nm.startswith('e03t') else 'bldg'
    fw, fd = (1, 1) if kind in ('road', 'tree') else GFOOT.get(nm, (3, 3))
    GPARTS[nm] = {'tris': gtris, 'kind': kind, 'fw': fw, 'fd': fd}
    # collision hull, centered on the SAME (cxm,czm,miny) as the visual so it tracks the placed part
    if nm in COLL:
        GPARTS[nm]['ctris'] = [[[p[0] - cxm, p[1] - miny, p[2] - czm] for p in t] for t in COLL[nm]]
_dl = default_layout('gedit/e03/gdata0.edt')
GDEFAULT = [{'name': b['name'], 'x': b['x'], 'y': b['y'], 'z': b['z'], 'rot': b['rot']} for b in _dl['buildings']]
GDEFAULT += [{'name': 'e03t01', 'x': t['x'], 'y': t['y'], 'z': t['z'], 'rot': t['rot']} for t in _dl['trees']]
GROADS = [{'x': r['x'], 'y': r['y'], 'z': r['z']} for r in _dl['roads']]
GEORAMA = {'parts': GPARTS, 'regions': GREGIONS, 'default': GDEFAULT, 'roads': GROADS, 'cell': 100}

# ---- CAMERA COLLISION the mod INCLUDES: building `_a` collision hulls, placed like the visible parts
# (gXform convention: rot*90deg). GREEN = exactly what the camera hugs; each building gets a node label.
def _geo_xform(tris, X, Y, Z, r):
    a = r * math.pi / 2; ca, sa = math.cos(a), math.sin(a)
    return [[[p[0]*ca - p[2]*sa + X, p[1] + Y, p[0]*sa + p[2]*ca + Z] for p in t] for t in tris]
_cam_incl = []
for _b in GDEFAULT:
    _part = GPARTS.get(_b['name'])
    if not _part or 'ctris' not in _part: continue
    _wt = _geo_xform(_part['ctris'], _b['x'], _b['y'], _b['z'], _b['rot'])
    _cam_incl += _wt
    _vs = [p for t in _wt for p in t]
    if _vs:
        _cen = [sum(v[i] for v in _vs)/len(_vs) for i in range(3)]
        _bb = [min(v[0] for v in _vs), min(v[1] for v in _vs), min(v[2] for v in _vs),
               max(v[0] for v in _vs), max(v[1] for v in _vs), max(v[2] for v in _vs)]
        nodelabels.append([_cen, f"{_b['name']} (_a)", _bb, 'camincl'])
layers.append({'key': 'camincl', 'label': f'CAMERA hugs: building _a collision ({len(_cam_incl)})',
               'tris': _cam_incl, 'color': [60, 220, 120], 'alpha': 0.5, 'border': '#3d8', 'on': False})
print(f"camera-included building collision: {len(_cam_incl)} tris across {len(GDEFAULT)} placed parts")

# ---- CAMERA COLLISION candidate (user-chosen): the FULL VISIBLE meshes (not _a) of a chosen object set —
# tall enough to actually stop the camera, where the player-height _a lets it fly over. This is the set we
# feed the camera. Toggle it to verify coverage before wiring the mod. GREEN.
_CAM_OBJ = ('obj40', 'obj44', 'obj1', 'obj9', 'obj6', 'obj33', 'obj34', 'obj43', 'obj45')
def _cam_match(nm):
    if nm.startswith('grid3'): return True
    return any(nm == o or nm.startswith(o + '__') for o in _CAM_OBJ)
_cam_vis, _matched = [], set()
for m in PLACED:                                   # scene structures (obj*/grid3) — world verts + tri indices
    nm = m.get('name', '')
    if _cam_match(nm):
        _matched.add(nm.split('__')[0])
        vs = m['verts']
        for tri in m['tris']:
            _cam_vis.append([vs[tri[0]], vs[tri[1]], vs[tri[2]]])
for _b in GDEFAULT:                                # georama BUILDINGS — full visible mesh (not _a)
    _part = GPARTS.get(_b['name'])
    if _part and _b['name'].startswith('e03h'):
        _cam_vis += _geo_xform(_part['tris'], _b['x'], _b['y'], _b['z'], _b['rot'])
_cam_vis += sign_mesh                              # kanban sign
layers.append({'key': 'camvis', 'label': f'CAMERA collision: VISIBLE meshes ({len(_cam_vis)})',
               'tris': _cam_vis, 'color': [60, 220, 120], 'alpha': 0.4, 'border': '#3d8', 'on': False})
print(f"camera collision (visible meshes): {len(_cam_vis)} tris; matched objects: {sorted(_matched)}")

for _lyr in layers:               # start with everything toggled OFF
    _lyr['on'] = False

html = build_html(
    title="Queens (e03) — fishing + georama editor",
    layers=layers, node_labels=nodelabels, points=points, point_labels=point_labels,
    points_label="cast rect + trigger", coord_note="water y=31", georama=GEORAMA)
open(os.path.join(OUT, HTML_NAME), 'w').write(html)

tot = sum(len(t) for t in layer_tris.values())
print(f"placed instances: {len(PLACED)}  layers: {len(layers)}  triangles: {tot}")
for key, *_ in LAYERS_SPEC:
    print(f"  {key:14s} {len(layer_tris[key]):5d} tris")
if layer_tris['other']:
    print(f"  WARNING: {len(layer_tris['other'])} unclassified tris dropped (no 'other' toggle)")
print(f"-> {os.path.join(OUT, HTML_NAME)}")
