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
import os, sys, re, math, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'iso_patch'))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'iso_patch', 'collision'))
import scene_placed
import bake_player_camera_collision as _bscc
from scene_placed import placed_meshes
from scene_viewer_html import build_html
from extract_scene_mesh import load_scene, read_verts, read_tris, xform
import carve_ladder
import canal_visual_cap as _cap
from georama_parts import lod_models, lod_layers
from queens_hcam import candidate_tris as _hcam_candidates, split_tris as _hcam_split
from georama_parts import part_models
from georama_default import default_layout
from georama_collision import collision_local, parse_coll_mdt
from queens_fishing_collision import fishing_collision_tris

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

# ---- time-of-day canal tide levels (2026-08 low-tide-fishing chart) — arch crown underside Y=60 ----
# LOW = morning (6, canal floor walkable/fishable), MEDIUM = afternoon + night (31), HIGH = dusk (52).
# Each tide layer is a COPY of the real canal water mesh (mizu__a01, world baseline Y=30 — the same
# mesh CanalTide moves via its CFrame) shifted to that tide's level, so extent matches the game.
MIZU_BASE_Y = 30.0
MIZU_TRIS = []
for pm in PLACED:
    if pm['name'] == 'mizu__a01':
        _v = pm['verts']
        MIZU_TRIS = [[list(_v[a]), list(_v[b]), list(_v[c])] for a, b, c in pm['tris']]
TIDES = [('morning (LOW)', 6.0, [70,170,150], '#6fc', True),
         ('afternoon+night', 31.0, [70,150,175], '#6cf', True),
         ('dusk (HIGH)', 52.0, [90,120,210], '#89f', True)]
for tname, ty, col, bd, on in TIDES:
    _dy = ty - MIZU_BASE_Y
    layers.append({'key': 'tide_' + re.sub(r'[^a-z0-9]+', '_', tname.lower()).strip('_'),
                   'label': f'tide: {tname} y={ty:.0f} (mizu__a01 copy)',
                   'tris': [[[p[0], p[1] + _dy, p[2]] for p in t] for t in MIZU_TRIS],
                   'color': col, 'alpha': 0.55, 'border': bd, 'on': on})

# ---- fishing-sign (kanban) mesh loader, shared by the existing Queens sign AND the new canal-floor
#      sign. Same real mesh the ISO patcher injects (game_data/fishsign/kanban.mds). Matches the GAME's
#      mapinfo Y-rotation (CONFIRMED in-game once the format bug was fixed; the angle is negated vs the raw
#      matrix): ry 0=south / 90=WEST / 180=north / 270=east.
_KB = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "game_data", "fishsign", "kanban.mds"), "rb").read()
_KB_VERTS, _KB_TRIS = read_verts(_KB, 0x80), read_tris(_KB, 0x80)
def kanban_mesh(pos, ry):
    th = math.radians(ry); c = math.cos(th); s = math.sin(th)
    def P(v):
        x, y, z = v[0], v[1], v[2]
        return [x*c + z*s + pos[0], y + pos[1], -x*s + z*c + pos[2]]
    return [[P(_KB_VERTS[i]) for i in (a, b, c)] for a, b, c in _KB_TRIS]
CANAL_SIGN_POS, CANAL_SIGN_RY = (800.0, 0.0, 0.0), -90   # under eastern bridge, facing WEST (IsoPatcher CANAL_SIGN_*)

# ---- LOW-TIDE FISHING proposals (canal-lowtide-fishing-plan.md): carved Factory ladder on the
#      south canal wall centred at x=705, + the canal-floor fishing sign under the bridge facing west.
#      Ladder from tools/carve_ladder.py (donor e05a01 'hasigo1'); sign = the real kanban mesh.
layers.append({'key': 'ladder', 'label': "PROPOSED ladder (e05 'hasigo1' trimmed to 70)",
               'tris': carve_ladder.placed_ladder_tris(),
               'color': [220,220,230], 'alpha': 1.0, 'border': '#fff', 'on': True})
layers.append({'key': 'newsign', 'label': 'PROPOSED canal-floor sign (real kanban, faces west)',
               'tris': kanban_mesh(CANAL_SIGN_POS, CANAL_SIGN_RY),
               'color': [230,180,90], 'alpha': 1.0, 'border': '#fc6', 'on': True})

# ---- SHIPPED canal west-end visual cap (tools/iso_patch/canal_visual_cap.py): 2 tris @ y=50 closing the
#      look-up gap from the low-tide canal floor. Drawn EXACTLY as authored (host node grid1__n; NW/NE
#      corners reused from its slant-wall records, SE/SW appended copying the x-twin's UV/normal), so the
#      added geometry can be inspected against the surrounding walls.
layers.append({'key': 'canalcap', 'label': 'SHIPPED canal west-end cap (2 tris, grid1__n @ y=50)',
               'tris': [[list(p) for p in t] for t in _cap.CAP_TRIS],
               'color': [240, 120, 200], 'alpha': 0.85, 'border': '#f6c', 'on': True})

# ---- SHIPPED cast-collision geometry (camera_norm_side.s FishLineClamp v4/v5 + QueensDragCheck):
#      the EXACT planes/boxes the ISO-baked fishing patches enforce, for visual verification.
#      • canal WALL clamp planes z=+-48 (LOW TIDE ONLY, and only walls the rod is inside of)
#      • drag-uncast planes z=+-49.5 (all tides, waiting state)
#      • 6 bridge boxes (table @0x2293C0): 2 bridges (obj40/obj44) x { legsS, legsN, arch/deck }
def _quadz(z, x0, x1, y0, y1):
    return [[[x0,y0,z],[x1,y0,z],[x1,y1,z]],[[x0,y0,z],[x1,y1,z],[x0,y1,z]]]
def _box(xa,xb,za,zb,ylo,yhi):
    t=[]
    t+= [[[xa,ylo,za],[xb,ylo,za],[xb,yhi,za]],[[xa,ylo,za],[xb,yhi,za],[xa,yhi,za]]]
    t+= [[[xa,ylo,zb],[xb,ylo,zb],[xb,yhi,zb]],[[xa,ylo,zb],[xb,yhi,zb],[xa,yhi,zb]]]
    t+= [[[xa,ylo,za],[xa,ylo,zb],[xa,yhi,zb]],[[xa,ylo,za],[xa,yhi,zb],[xa,yhi,za]]]
    t+= [[[xb,ylo,za],[xb,ylo,zb],[xb,yhi,zb]],[[xb,ylo,za],[xb,yhi,zb],[xb,yhi,za]]]
    t+= [[[xa,yhi,za],[xb,yhi,za],[xb,yhi,zb]],[[xa,yhi,za],[xb,yhi,zb],[xa,yhi,zb]]]
    return t
_wall=[]
for z in (47.0,-47.0): _wall+=_quadz(z,-240,900,-15,60)
layers.append({'key': 'castwall', 'label': 'CAST wall clamp z=±47 (flight, LOW TIDE, outward-crossing only; 1u padded)',
               'tris': _wall, 'color': [255,80,80], 'alpha': 0.35, 'border': '#f55', 'on': True})
_drag=[]
for z in (49.5,-49.5): _drag+=_quadz(z,-240,900,-15,60)
layers.append({'key': 'castdrag', 'label': 'DRAG uncast planes z=±49.5 (waiting, all tides)',
               'tris': _drag, 'color': [255,170,60], 'alpha': 0.3, 'border': '#fa6', 'on': False})
# USER-AUTHORED support boxes (verbatim from the baked table, rows 0-3): legs only, y 0..47
_BOXES=[(-73.07,-22.93,35,50,0,47),(-73.07,-22.93,-50,-36,0,47),
        (774.93,825.07,35,50,0,47),(774.93,825.07,-50,-36,0,47)]
_bx=[]
for b in _BOXES: _bx+=_box(*b)
layers.append({'key': 'castboxes', 'label': 'CAST bridge-support boxes (stop-dead in flight + uncast on drag)',
               'tris': _bx, 'color': [120,220,255], 'alpha': 0.4, 'border': '#8df', 'on': True})


# ---- CUSTOM COLLISION BAKE — regrouped into the EXACT nodes the ISO bake writes (bscc.grouped_collision):
#      all non-trigger tris of a frame are pooled and kd_split into <=100-poly nodes, so nearby polys share a
#      node (tight per-node bbox = free runtime gather culling). Four toggles: custom CAMERA collision (_c),
#      custom PLAYER collision (_a), plus perimeter walls and invisible walls broken out on their own. Each
#      split node still gets its own node-label box (gated on its layer) so "node labels" reveals the grouping.
_scn_bytes = _bscc.load_scene('gedit/e03/scene.scn')
_G = _bscc.grouped_collision(PLACED, _scn_bytes, 'e03', 100)

def _add_nodes(named, layerkey, prefix=''):
    tris = []
    for mn, bk in named:
        tris += bk
        vs = [p for t in bk for p in t]
        cen = [sum(v[i] for v in vs) / len(vs) for i in range(3)]
        bb = [min(v[0] for v in vs), min(v[1] for v in vs), min(v[2] for v in vs),
              max(v[0] for v in vs), max(v[1] for v in vs), max(v[2] for v in vs)]
        nodelabels.append([cen, f'{prefix}{mn}', bb, layerkey])
    return tris

# custom CAMERA collision (_c): ONE TOGGLE PER NODE, in a "Camera _c nodes" folder, each a distinct colour and
# with its own gated node-label — so specific parts can be isolated, identified by node name, and pointed at for
# directed merging. The folder master checkbox toggles the whole set; individual checkboxes isolate one node.
import colorsys as _colorsys
def _node_color(i):
    r, g, b = _colorsys.hsv_to_rgb((0.61803399 * i) % 1.0, 0.62, 1.0)   # golden-ratio hue hop = well-separated
    return [int(r * 255), int(g * 255), int(b * 255)]
_cam_tris = []
for _i, (_mn, _bk) in enumerate(_G['camera']):
    _cam_tris += _bk
    _vs = [p for t in _bk for p in t]
    _cen = [sum(v[k] for v in _vs) / len(_vs) for k in range(3)]
    _bb = [min(v[0] for v in _vs), min(v[1] for v in _vs), min(v[2] for v in _vs),
           max(v[0] for v in _vs), max(v[1] for v in _vs), max(v[2] for v in _vs)]
    _nk = f'cam_{_mn}'
    nodelabels.append([_cen, _mn, _bb, _nk])
    layers.append({'key': _nk, 'label': f'{_mn} ({len(_bk)} tris)', 'tris': _bk, 'color': _node_color(_i),
                   'alpha': 0.6, 'border': '#5bf', 'on': False, 'group': 'Camera _c nodes'})

# REVERTED-ARCH highlight (2026-08): obj34 gatehouse / obj43 / obj45 / obj33 `_c` customs were reverted
# to vanilla (match the visual meshes) as the baseline for the corner-ROUNDING approach. This layer pulls
# those four regions out of the live-baked camera pool so the revert is verifiable at a glance.
_REVERT_BOXES=[(150,350,-177,-74),(-513,-374,-104,104),(-513,50,123,653),(1250,1622,-54,276)]
def _in_revert(t):
    cx=sum(p[0] for p in t)/3; cz=sum(p[2] for p in t)/3
    return any(x0-5<=cx<=x1+5 and z0-5<=cz<=z1+5 for x0,x1,z0,z1 in _REVERT_BOXES)
_camhl=[t for t in _cam_tris if _in_revert(t)]
layers.append({'key': 'ccol_rev', 'label': f'REVERTED arches obj34/43/45/33 — now vanilla _c ({len(_camhl)} tris)',
               'tris': _camhl, 'color': [120,255,140], 'alpha': 0.65, 'border': '#6f8', 'on': True})
print(f"reverted-arch regions: {len(_camhl)} camera tris (vanilla again)")

# custom PLAYER collision (_a): per ground sub, pooled + split (= the camera set + canal invisible walls +
# railings; the loading-zone triggers below are also part of `_a` but kept on their own toggle).
_ply_tris, _ply_nodes = [], 0
for _sub in _G['subs']:
    _named, _trigs = _G['player'][_sub]
    _ply_tris += _add_nodes(_named, 'plycol', f'{_sub}:')
    _ply_nodes += len(_named)
layers.append({'key': 'plycol', 'label': f'custom player collision _a ({_ply_nodes} nodes, {len(_ply_tris)} tris)',
               'tris': _ply_tris, 'color': [120, 255, 160], 'alpha': 0.5, 'border': '#6f9', 'on': True})

# perimeter walls (both frames) + invisible walls (player-only: canal containment + railings), broken out
_PERIM = _G['sets']['perimeter']
layers.append({'key': 'perimeter', 'label': f'perimeter walls ({len(_PERIM)})', 'tris': _PERIM,
               'color': [255, 90, 40], 'alpha': 0.6, 'border': '#f62', 'on': False})
_INV = _G['sets']['invisible']
layers.append({'key': 'invwalls', 'label': f'invisible walls ({len(_INV)})', 'tris': _INV,
               'color': [235, 40, 120], 'alpha': 0.7, 'border': '#e28', 'on': False})
print(f"camera _c: {len(_G['camera'])} nodes / {len(_cam_tris)} tris; "
      f"player _a: {_ply_nodes} nodes / {len(_ply_tris)} tris (+3 trigger nodes)")
print(f"perimeter {len(_PERIM)} + invisible {len(_INV)} tris (broken out)")

# ---- FISHING-RECT camera polys: the CAMERA `_c` tris whose footprint is inside the cast/gather RECT. This is
#      the set that feeds the runtime camera-collision gather, which caps at ~409 polys and SATURATES here — so
#      the native swept-slide silently drops walls (incl. the canal walls) and the camera passes through. This
#      toggle isolates them so we can see which GROUPS to simplify/remove to get back under budget. Each source
#      kd-node that has polys in the rect gets a label "<node> (<in-rect count>)" gated on this layer, so turning
#      on node-labels names every group and its poly count — point at the dense ones and I'll simplify them.
_rx1, _rz1, _rx2, _rz2 = RECT
def _tri_in_rect(t):
    cx = (t[0][0] + t[1][0] + t[2][0]) / 3.0
    cz = (t[0][2] + t[1][2] + t[2][2]) / 3.0
    return _rx1 <= cx <= _rx2 and _rz1 <= cz <= _rz2
_fr_tris = []
for _mn, _bk in _G['camera']:
    _inb = [t for t in _bk if _tri_in_rect(t)]
    if not _inb:
        continue
    _fr_tris += _inb
    _vs = [p for t in _inb for p in t]
    _cen = [sum(v[i] for v in _vs) / len(_vs) for i in range(3)]
    _bb = [min(v[0] for v in _vs), min(v[1] for v in _vs), min(v[2] for v in _vs),
           max(v[0] for v in _vs), max(v[1] for v in _vs), max(v[2] for v in _vs)]
    nodelabels.append([_cen, f'{_mn} ({len(_inb)})', _bb, 'fishrect'])
# Worst-case runtime gather: the engine gathers `_c` within ~±160 of the eye, into a buffer that caps ~409 and
# saturates -> drops walls. Scan candidate box centres over the fishing area for the densest ±160 box: THAT number
# (not the whole-rect count) is what must stay under ~409 for the swept-slide to keep holding the canal walls.
def _worst_gather_box(tris, half=160):
    cen = [[(t[0][i]+t[1][i]+t[2][i])/3.0 for i in (0, 2)] for t in tris]
    best, at = 0, None
    for cx in range(-200, 901, 40):
        for cz in range(-100, 151, 40):
            c = sum(1 for p in cen if abs(p[0]-cx) < half and abs(p[1]-cz) < half)
            if c > best:
                best, at = c, (cx, cz)
    return best, at
_wbox, _wat = _worst_gather_box(_fr_tris)
layers.append({'key': 'fishrect',
               'label': f'CAM _c in fish rect ({len(_fr_tris)} tris; worst ±160 box {_wbox}/~409)',
               'tris': _fr_tris, 'color': [255, 210, 60], 'alpha': 0.8, 'border': '#fc0', 'on': False})
print(f"CAM _c inside fish rect: {len(_fr_tris)} tris; worst ±160 gather box = {_wbox} tris at {_wat} "
      f"(cap ~409 — THIS is the number that must stay under, not the rect total)")

# ---- LOADING-ZONE trigger quads (the town exits): attribute-tagged collision polys in the vanilla ground
#      `_a`. EdEventPointCpPoly gathers these around each event point; GetEventPoly reads the destination from
#      the hit poly's colour tag (+0x40 short). The bake must carry them over (bake_player_camera_collision
#      .trigger_nodes) or the exits stop working — this is exactly what broke when we dropped `_a`.
_TRIGS = _bscc.trigger_nodes(_bscc.load_scene('gedit/e03/scene.scn'))
_trig_tris, _dests = [], []
for _sub, _tns in _TRIGS.items():
    for _tn, _tt, _te in _tns:
        _trig_tris += _tt
        _dests.append(f'{_tn} dest={struct.unpack_from("<H", _te[0], 0)[0]}')
layers.append({'key': 'loadzones', 'label': f'loading-zone triggers: {", ".join(_dests)}', 'tris': _trig_tris,
               'color': [255, 240, 40], 'alpha': 0.95, 'border': '#ff0', 'on': True})
print(f"loading zones: {len(_trig_tris)} trigger tris — {'; '.join(_dests)}")

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
sign_mesh = kanban_mesh(SIGN_POS, SIGN_RY)
print(f"sign mesh: {len(sign_mesh)} tris at {SIGN_POS} ry{SIGN_RY}")

layers.append({'key': 'sign', 'label': 'fishing sign (faces N)',
               'tris': sign_mesh,
               'color': [210,160,90], 'alpha': 1.0, 'border': '#e94', 'on': True})

# ---- fishing-sign COLLISION: the single solid panel the ISO patcher bakes (BuildKanbanCollision).
# Must match IsoPatcher.BuildKanbanCollision Box(-6.5, 6.5, 0, 16, -1, 2), placed like the sign (ry 180).
def _ps(v):
    th = math.radians(SIGN_RY); c = math.cos(th); s = math.sin(th)
    x, y, z = v[0], v[1], v[2]
    return [x*c + z*s + SIGN_POS[0], y + SIGN_POS[1], -x*s + z*c + SIGN_POS[2]]
def _box_tris(x0, x1, y0, y1, z0, z1):
    v = [[x0,y0,z0],[x1,y0,z0],[x1,y0,z1],[x0,y0,z1],[x0,y1,z0],[x1,y1,z0],[x1,y1,z1],[x0,y1,z1]]
    faces = [(0,1,2),(0,2,3),(4,6,5),(4,7,6),(0,4,5),(0,5,1),(3,2,6),(3,6,7),(0,3,7),(0,7,4),(1,5,6),(1,6,2)]
    return [[_ps(v[a]), _ps(v[b]), _ps(v[c])] for a, b, c in faces]
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
    GPARTS[nm] = {'tris': gtris, 'kind': kind, 'fw': fw, 'fd': fd, '_ctr': (cxm, miny, czm)}
    # native `_a` collision hull, centered on the SAME (cxm,czm,miny) as the visual so it tracks the placed
    # part (fallback for parts without a baked wall collision, e.g. roads/trees)
    if nm in COLL:
        GPARTS[nm]['ctris'] = [[[p[0] - cxm, p[1] - miny, p[2] - czm] for p in t] for t in COLL[nm]]
_dl = default_layout('gedit/e03/gdata0.edt')
GDEFAULT = [{'name': b['name'], 'x': b['x'], 'y': b['y'], 'z': b['z'], 'rot': b['rot']} for b in _dl['buildings']]
GDEFAULT += [{'name': 'e03t01', 'x': t['x'], 'y': t['y'], 'z': t['z'], 'rot': t['rot']} for t in _dl['trees']]
GROADS = [{'x': r['x'], 'y': r['y'], 'z': r['z']} for r in _dl['roads']]
GEORAMA = {'parts': GPARTS, 'regions': GREGIONS, 'default': GDEFAULT, 'roads': GROADS, 'cell': 100}

# ---- GEORAMA (buildings) — under the current scheme buildings keep their VANILLA collision (player `_a` +
#      camera `_c`); nothing is baked for them. The georama editor's "georama collision" toggle therefore shows
#      GPARTS['ctris'] = the native `_a` player hull (set above), and "vanilla cam coll _c" shows the `_c` below.
# ---- VANILLA CAMERA COLLISION (`_c` variant = part+0x20 -> camera frame +0xdc): the COARSE hull the vanilla
#      game uses for the CAMERA (10-16 tris/building, far simpler than the `_a` player collision — the "missing
#      piece": vanilla keeps detail only for the player). Two toggles: static scene assets (grounds, world) and
#      georama buildings (attached to GPARTS.camtris, so the "vanilla cam coll" georama toggle moves it w/ parts).
_scn = load_scene('gedit/e03/scene.scn'); _DIR = scene_placed._scndir(_scn)
def _variant_coll(name, suf):
    if name not in _DIR: return None
    off, size = _DIR[name]; sub = _scn[off:off + size]
    m = next(re.finditer((re.escape(name) + suf + r'\.mds\x00').encode(), sub), None)
    if not m: return None
    vo = struct.unpack_from('<I', sub, m.end() + 3)[0]
    if not (0 < vo < len(sub) and sub[vo:vo + 3] == b'MDS'): return None
    mds = off + vo; nodes, wm = scene_placed._accum(_scn, mds); tris = []
    for i, (nn, mo, par, mat) in enumerate(nodes):
        if mo == 0: continue
        fo = next((c for c in (mo, mds + mo) if 0 < c < len(_scn) and _scn[c:c + 3] == b'MDT'), None)
        if not fo: continue
        M = wm(i)
        for a, b, c in parse_coll_mdt(_scn, fo):
            tris.append([list(xform(M, a)), list(xform(M, b)), list(xform(M, c))])
    return tris
_vcam_static = []
for _g in ('e03g04', 'e03g05'):        # mapinfo GROUND parts are origin-placed, so local == world
    _t = _variant_coll(_g, r'_c')
    if _t: _vcam_static += _t
layers.append({'key': 'vcam_static', 'label': f'vanilla cam coll _c: static assets ({len(_vcam_static)})',
               'tris': _vcam_static, 'color': [80, 200, 255], 'alpha': 0.5, 'border': '#5bf', 'on': False})
_vgeo = 0
for _bn in [n for n in _DIR if re.match(r'e03h\d\d$', n)]:
    _part = GPARTS.get(_bn)
    if not _part or '_ctr' not in _part: continue
    _t = _variant_coll(_bn, r'_c')
    if not _t: continue
    _cxm, _my, _czm = _part['_ctr']
    _part['camtris'] = [[[q[0] - _cxm, q[1] - _my, q[2] - _czm] for q in t] for t in _t]
    _vgeo += len(_t)
print(f"vanilla camera coll (_c): {len(_vcam_static)} static tris + {_vgeo} georama tris")

# ---- simplified VISUAL LODs (h04/h05/h08/h09): candidate bases for more-detailed camera meshes.
#      Attached per-part (same centering as the visual) so the georama toggles overlay them on the
#      placed buildings — 'simplified visual LOD1/LOD2' checkboxes in the georama panel.
for _bn, _lv in lod_models('gedit/e03/scene.scn', r'e03h(04|05|08|09)$').items():
    _part = GPARTS.get(_bn)
    if not _part or '_ctr' not in _part:
        continue
    _cxm, _my, _czm = _part['_ctr']
    for _l in ('1', '2'):
        if _l in _lv:
            _part[f'lod{_l}tris'] = [[[q[0] - _cxm, q[1] - _my, q[2] - _czm] for q in t] for t in _lv[_l]]
            print(f"  LOD{_l} overlay {_bn}: {len(_lv[_l])} tris")

# ---- CUSTOM `_c` CANDIDATES (h04/h05/h08/h09): LOD2 base + FULL-mesh features LOD2 flattens —
#      the curved roof (upper zone of the main body node), the canopies (hiyoke*) and the round
#      roof chimney-pillar (entotu; h04/h05/h08 only). Preview overlay first; bake comes after review.
for _bn, _ct in _hcam_candidates().items():
    _part = GPARTS.get(_bn)
    if not _part or '_ctr' not in _part:
        continue
    _cxm, _my, _czm = _part['_ctr']
    _part['candtris'] = [[[q[0] - _cxm, q[1] - _my, q[2] - _czm] for q in t] for t in _ct]
    print(f"  _c candidate {_bn}: {len(_ct)} tris in {len(_hcam_split(_ct))} bake nodes")

# ---- LOD comparison layers (shared helper): buildings h01-h12 ship _0/_1/_2 (full/medium/low),
#      trees t01/t02 ship _0/_2. One toggle per level, world-placed at the default-layout position;
#      assets not in the default layout (h01, t02) line up in a showroom row outside the SW corner.
_inst_of = {}
for _o in GDEFAULT:
    _inst_of.setdefault(_o['name'], []).append(_o)
_lod = lod_layers('gedit/e03/scene.scn', r'e03[ht]\d', _inst_of)
layers += _lod
print(f"LOD compare: {len(_lod)} layers")

# ---- toggle-panel folders (scene_viewer_html folder UI): folder master checkbox = whole-group
#      grey-out without touching individual toggle states
def _group_of(key):
    if key in ('camcol', 'plycol', 'perimeter', 'invwalls', 'loadzones'): return 'Custom collision bake'
    if key.startswith('van') or key == 'vcam_static': return 'Vanilla collision'
    if key.startswith(('ws', 'tide_')) or key == 'water': return 'Water & tides'
    if key in ('ladder', 'newsign'): return 'Low-tide fishing proposal'
    if key.startswith('fc_') or key in ('trigger', 'sign', 'signcol'): return 'Fishing spot'
    if key.startswith('cam'): return 'Camera debug'
    if key.startswith('lod_'): return 'LOD compare (full/medium/low)'
    return 'Scene meshes'
for _lyr in layers:
    _lyr.setdefault('group', _group_of(_lyr['key']))

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
