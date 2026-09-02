#!/usr/bin/env python3
"""Yellow Drops (map s13) fishing-placement + collision 3D viewer generator.

Decodes the town's meshes from gedit/s13/scene.scn with CORRECT world placement (tools/lib/scene_placed.py):
mapinfo.cfg GROUND instance transforms (position + Y-rotation) + per-node parent-chain accumulation +
strict mdt_codec triangle decode. This fixes the earlier bare-extract_mesh pass, which left instanced
sub-files (buildings/entrances/decoration) stacked at their local origin and let the parse_mds heuristic
emit spurious "distorted" polys. Groups meshes into fishing-relevant layers, marks the CURRENT fishing
rect/trigger/stance from CustomFishingSpot.cs, and writes a self-contained interactive HTML viewer via the
shared renderer (tools/lib/scene_viewer_html.py): per-layer show/hide, fill/wire/backface-cull, node labels, a
cursor->world (x,z) readout for picking sign/trigger spots, and click / shift+click polygon selection ->
copyable triangle list (for fishing collision).

Run: python3 tools/yellowdrops/yellowdrops_viewer.py  ->  game_data/yellowdrops/yellowdrops_viewer.html
"""
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'lib'))
import toolpath  # noqa: F401 — puts every tools/ subfolder on sys.path
import os, sys, re, math, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import scene_placed
from scene_placed import placed_meshes
from scene_viewer_html import build_html
from extract_scene_mesh import load_scene, read_verts, read_tris, xform
from georama_collision import parse_coll_mdt

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "game_data", "yellowdrops")   # embeds game geometry -> untracked
os.makedirs(OUT, exist_ok=True)
HTML_NAME = "yellowdrops_viewer.html"

PLACED = placed_meshes('gedit/s13/scene.scn', 'gedit/s13/mapinfo.cfg')   # [{name, inst, verts, tris}]

# BAKED water raise (IsoPatcher YD_WATER_Y): the suimenn sheet ships at 4.25 — mirror it here so
# the rendered surface matches the game (the WATER_SURFACE plane + spot water move with it).
YD_WATER_RAISE = 4.25
for _pm in PLACED:
    if _pm['name'].startswith('suimenn'):
        for _v in _pm['verts']:
            _v[1] += YD_WATER_RAISE


# ---- current Yellow Drops fishing placement (mirror CustomFishingSpot.cs spot 23 — adjust there) ----
WATER_Y = 5.25    # spot water level (raised surface 4.25 + 1; IsoPatcher YD_WATER_Y)
RECT = (-692.0, -156.0, -378.0, 270.0)       # x1,z1,x2,z2 (cast box) — user-drawn via F+drag
TRIG = (-465.0, 30.0, 40.0)                  # trigger/sign on the y30 plateau by the west edge
STANCE = (-468.0, 30.0, 40.0)                # player stance beside the sign, facing WEST (yaw -pi/2)

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

# ---- DECORATION LABELS near the fishing area: one label per individual pillar / drop-spinner /
#      light / floating rock, so specific pieces can be named for move directions.
#      P=obj3 pillar, D=drop spinner (revol/naka), L=light (akari), R=floating rock (obj*__n).
DECOR_REGION = (-950.0, -330.0, -400.0, 480.0)      # x0,x1,z0,z1 around the west fishing pocket


def _decor_clusters(kind_pred, tol):
    cl = []
    for pm in PLACED:
        if not kind_pred(pm['name']):
            continue
        for v in pm['verts']:
            if not (DECOR_REGION[0] <= v[0] <= DECOR_REGION[1] and DECOR_REGION[2] <= v[2] <= DECOR_REGION[3]):
                continue
            for c in cl:
                if abs(v[0] - c[0] / c[3]) <= tol and abs(v[2] - c[2] / c[3]) <= tol:
                    c[0] += v[0]; c[1] = max(c[1], v[1]); c[2] += v[2]; c[3] += 1
                    break
            else:
                cl.append([v[0], v[1], v[2], 1])
    # merge overlapping clusters (two passes are plenty at these tolerances)
    for _ in range(2):
        merged = []
        for c in cl:
            for m2 in merged:
                if abs(c[0]/c[3] - m2[0]/m2[3]) <= tol and abs(c[2]/c[3] - m2[2]/m2[3]) <= tol:
                    m2[0] += c[0]; m2[1] = max(m2[1], c[1]); m2[2] += c[2]; m2[3] += c[3]
                    break
            else:
                merged.append(list(c))
        cl = merged
    out = [(c[0]/c[3], c[1], c[2]/c[3]) for c in cl]
    return sorted(out, key=lambda c: (round(c[2]/50), c[0]))    # stable naming: N->S bands, W->E


decor_pts, decor_labels = [], []
for _pref, _pred, _tol in (
        ('P', lambda n: n == 'obj3', 48),
        ('D', lambda n: n.startswith('revol') or n.startswith('naka'), 60),
        ('L', lambda n: n.startswith('akari'), 40),
        ('R', lambda n: re.match(r'obj[2-5]__n$', n) is not None, 60)):
    for i, (cx2, cy2, cz2) in enumerate(_decor_clusters(_pred, _tol), 1):
        tag = f'{_pref}{i}'
        decor_pts.append([cx2, cy2 + 6, cz2])
        decor_labels.append([[cx2, cy2 + 6, cz2], f'{tag} ({cx2:.0f},{cz2:.0f})'])
        print(f'  decor {tag}: ({cx2:.0f}, {cz2:.0f})  top y~{cy2:.0f}')

points = rect_points(*RECT, WATER_Y) + [list(STANCE), [0, WATER_Y, 0]] + decor_pts
point_labels = decor_labels + [
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

import yellowdrops_westbank_data as westbank

# ---- fishing SIGN: real kanban.mds at the baked YSIGN_* position (IsoPatcher). In-game ry +90
#      faces EAST (sceVu0RotMatrixY fold) and the viewer renders +90 east too (user-confirmed).
_KB = open(os.path.join(HERE, "..", "..", "game_data", "fishsign", "kanban.mds"), "rb").read()
_KB_V, _KB_T = read_verts(_KB, 0x80), read_tris(_KB, 0x80)
SIGN_POS, SIGN_RY_VIEW = (-465.0, 30.0, 40.0), 90    # +90 = faces EAST (viewer matches the game here)

def _sign_pt(v):
    th = math.radians(SIGN_RY_VIEW); c, sn = math.cos(th), math.sin(th)
    return [v[0]*c + v[2]*sn + SIGN_POS[0], v[1] + SIGN_POS[1], -v[0]*sn + v[2]*c + SIGN_POS[2]]

layers.append({'key': 'sign', 'label': 'fishing sign (kanban, faces E in-game)',
               'tris': [[_sign_pt(_KB_V[i]) for i in t] for t in _KB_T],
               'color': [210,160,90], 'alpha': 1.0, 'border': '#e94', 'on': True, 'group': 'Fishing sign'})
# sign collision: the panel IsoPatcher.BuildKanbanCollision bakes — Box(-6.5,6.5, 0,16, -1,2), sign-placed
def _sign_box(x0, x1, y0, y1, z0, z1):
    v = [[x0,y0,z0],[x1,y0,z0],[x1,y0,z1],[x0,y0,z1],[x0,y1,z0],[x1,y1,z0],[x1,y1,z1],[x0,y1,z1]]
    f = [(0,1,2),(0,2,3),(4,6,5),(4,7,6),(0,4,5),(0,5,1),(3,2,6),(3,6,7),(0,3,7),(0,7,4),(1,5,6),(1,6,2)]
    return [[_sign_pt(v[a]), _sign_pt(v[b]), _sign_pt(v[c])] for a, b, c in f]
layers.append({'key': 'signcol', 'label': 'sign collision (kanban_a panel)',
               'tris': _sign_box(-6.5, 6.5, 0, 16, -1, 2),
               'color': [230,60,60], 'alpha': 0.4, 'border': '#e33', 'on': False, 'group': 'Fishing sign'})

# ---- NATIVE collision: nested <sub>_a.mds (player) / <sub>_c.mds (camera) inside each placed ground
#      sub-file, world-placed with the sub's mapinfo GROUND transform (brownboo_viewer pattern).
_scnraw = load_scene('gedit/s13/scene.scn')
_scndirmap = scene_placed.scn_directory_map(_scnraw)
_placements = scene_placed._ground_placements(load_scene('gedit/s13/mapinfo.cfg').decode('latin1', 'replace'))

def _nested_coll(sub_name, suf):
    if sub_name not in _scndirmap:
        return []
    off, size = _scndirmap[sub_name]
    sub = _scnraw[off:off + size]
    m = next(re.finditer(re.escape((sub_name + suf).encode()) + rb'\.mds\x00', sub), None)
    if not m:
        return []
    vo = None      # the offset word's padding after the name varies — find the u32 that lands on 'MDS'
    for k in range(0, 12):
        if m.end() + k + 4 > len(sub):
            break
        cand = struct.unpack_from('<I', sub, m.end() + k)[0]
        if 0 < cand < len(sub) and sub[cand:cand + 3] == b'MDS':
            vo = cand
            break
    if vo is None:
        return []
    mds = off + vo
    nodes, wm = scene_placed._accum(_scnraw, mds)
    out = []
    for i, (nn, mo, par, mat) in enumerate(nodes):
        if mo == 0:
            continue
        fo = next((c for c in (mo, mds + mo) if 0 < c < len(_scnraw) and _scnraw[c:c + 3] == b'MDT'), None)
        if not fo:
            continue
        M = wm(i)
        tris = [[list(xform(M, a)), list(xform(M, b)), list(xform(M, c))] for a, b, c in parse_coll_mdt(_scnraw, fo)]
        if tris:
            out.append((nn, tris))
    return out

_coll_cache, _coll_counts, _inst_n = {}, {}, {}
for _cname, _cpos, _crot in _placements:
    _ci = _inst_n[_cname] = _inst_n.get(_cname, -1) + 1
    for _suf, _kind, _grp, _ccol, _cbord in (('_a', 'player', 'Player collision (_a)', [255,120,220], '#f7c'),
                                             ('_c', 'CAMERA', 'Camera collision (_c)', [80,200,255], '#5bf'),
                                             ('_v', 'CAMERA', 'Camera collision (_c)', [80,200,255], '#5bf')):
        if (_cname, _suf) not in _coll_cache:
            _coll_cache[(_cname, _suf)] = _nested_coll(_cname, _suf)
        for _nn, _tris in _coll_cache[(_cname, _suf)]:
            _w = [[scene_placed._place_y(pnt, _cpos, _crot[1]) for pnt in t] for t in _tris]
            _tag = f'{_cname}#{_ci}' if _inst_n[_cname] or _cname in ('s1305','s1306','s1309','s1310','s1311','s1312') else _cname
            layers.append({'key': f'nc{_suf}_{_cname}_{_ci}_{_nn}',
                           'label': f'{_tag}{_suf} {_nn} ({len(_w)}) [{_kind}]',
                           'tris': _w, 'color': _ccol, 'alpha': 0.55, 'border': _cbord, 'on': False,
                           'group': _grp})
            _coll_counts[f'{_cname}{_suf}'] = _coll_counts.get(f'{_cname}{_suf}', 0) + 1
print('native collision layers:', _coll_counts)

# ---- WEST BANK: player collision bulged with the same column moves as the visual ground, so the
#      player can walk the extended plateau (the crown-line wall + floor verts follow the bulge).
_wbm = westbank.wb_moves()
_bulge_a = []
for _nn, _tris in _coll_cache.get(('s1301', '_a'), []):     # s1301 is placed at the origin
    for t in _tris:
        c = [(t[0][k] + t[1][k] + t[2][k]) / 3 for k in range(3)]
        if not (-480 <= c[0] <= -250 and -220 <= c[2] <= 340):
            continue
        nt = [list(pnt) for pnt in t]
        for pnt in nt:
            for (kx, kz), dxw in _wbm.items():
                if abs(pnt[0] - kx) <= 0.5 and abs(pnt[2] - kz) <= 0.5:
                    pnt[0] += dxw
        _bulge_a.append(nt)
_fw = westbank.westbank_fish_walls()
_fw_bank = [t for t in _fw if t[0][0] < -370 and t[0][2] < 300 and t not in ()][:len(_fw) - len(westbank.PILLAR_BASE_TRIS)]
_fw_pill = _fw[len(_fw) - len(westbank.PILLAR_BASE_TRIS):]
layers.append({'key': 'fishcol_bank', 'label': f'FISH collision: bank walls (DCFC bin, {len(_fw_bank)} tris, to y{int(westbank.FISH_WALL_BOTTOM)})',
               'tris': _fw_bank, 'color': [255,60,120], 'alpha': 0.55, 'border': '#f36', 'on': True,
               'group': 'Fish collision (DCFC bin)'})
layers.append({'key': 'fishcol_pillars', 'label': f'FISH collision: P3/P4 pillar bases (DCFC bin, {len(_fw_pill)} tris)',
               'tris': _fw_pill, 'color': [255,140,60], 'alpha': 0.55, 'border': '#f93', 'on': True,
               'group': 'Fish collision (DCFC bin)'})
# camera wall (miti_c) bulged by the same z-profile the bake table uses
_z0, _z1 = westbank.WB_SPAN
_bulge_c = []
for _nn, _tris in _coll_cache.get(('s1301', '_c'), []):
    if _nn != 'miti_c':
        continue
    for t in _tris:
        c = [(t[0][k] + t[1][k] + t[2][k]) / 3 for k in range(3)]
        if not (-520 <= c[0] <= -270 and -260 <= c[2] <= 340):
            continue
        nt = [list(pnt) for pnt in t]
        for pnt in nt:
            if -445.0 <= pnt[0] <= -395.0 and _z0 < pnt[2] < _z1:
                pnt[0] += -westbank.WEST_BULGE * math.sin(math.pi * (pnt[2] - _z0) / (_z1 - _z0))
        _bulge_c.append(nt)
layers.append({'key': 'wb_ccol', 'label': f'WEST BANK: camera wall bulged ({len(_bulge_c)} tris)',
               'tris': _bulge_c, 'color': [90,220,255], 'alpha': 0.5, 'border': '#5cf', 'on': False,
               'group': 'West bank proposal'})
_ws = westbank.westbank_smooth()
for _k, _lbl, _cc, _bb in (('vis', 'smoothed WATERLINE (visual)', [255,180,60], '#fb5'),
                           ('acol', 'smoothed player collision', [255,90,190], '#f5b'),
                           ('ccol', 'smoothed camera wall + floor', [90,220,255], '#5cf')):
    layers.append({'key': f'wbs_{_k}', 'label': f'WEST BANK v2: {_lbl} ({len(_ws[_k])} tris)',
                   'tris': _ws[_k], 'color': _cc, 'alpha': 0.65, 'border': _bb, 'on': True,
                   'group': 'West bank v2 (smoothed)'})
layers.append({'key': 'wb_acol', 'label': f'WEST BANK: player collision bulged ({len(_bulge_a)} tris)',
               'tris': _bulge_a, 'color': [255,90,190], 'alpha': 0.55, 'border': '#f5b', 'on': False,
               'group': 'West bank proposal'})

# ---- CUSTOM doumu_c: the factory camera wall pulled radially IN by DOUMU_PULL so the camera can
#      hug the factory closer (vanilla ring r~378-402 vs the dome base ring at ~370-382).
from yellowdrops_camera_pillars import doumu_hug_xz, DOUMU_PULL
_doumu = []
for _nn, _tris in _coll_cache.get(('s1301', '_c'), []):
    if _nn != 'doumu_c':
        continue
    for t in _tris:
        nt = []
        for pnt in t:
            _nx, _nz = doumu_hug_xz(pnt[0], pnt[2])
            nt.append([_nx, pnt[1], _nz])
        _doumu.append(nt)
layers.append({'key': 'doumu_hug', 'label': f'BAKED doumu_c hugged closer (-{int(DOUMU_PULL)}) [CAMERA]',
               'tris': _doumu, 'color': [120,255,140], 'alpha': 0.6, 'border': '#6f8', 'on': True,
               'group': 'Camera collision (_c)'})

# ---- pillar camera hulls (BAKED): simplified vertical camera collision around the 2 inner
#      extru gate legs (tools/yellowdrops/yellowdrops_camera_pillars.py). Toggle against the visual
#      'archways' layer to check clip vs padding.
from yellowdrops_camera_pillars import pillar_hulls as _pillar_hulls
for _lbl, _dd in _pillar_hulls().items():
    layers.append({'key': f'pilcam_{_lbl}',
                   'label': f'{_lbl} camera hull ({len(_dd["tris"])} tris)',
                   'tris': _dd['tris'], 'color': [255,120,50],
                   'alpha': 0.45, 'border': '#f83',
                   'on': True, 'group': 'Pillar camera hulls (baked)'})

_wb = westbank.westbank_tris()
layers.append({'key':'westbank','label':f'WEST BANK: edge bulged out (+{int(westbank.WEST_BULGE)}) for camera room ({len(_wb)} tris)',
               'tris':_wb,
               'color':[255,180,60],'alpha':0.7,'border':'#fb5','on':True,'group':'West bank proposal'})
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
