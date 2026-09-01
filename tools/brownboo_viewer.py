#!/usr/bin/env python3
"""Brownboo (map s04) fishing collision-vs-mesh 3D viewer generator.

Decodes the town's exact meshes from gedit/s04/scene.scn, places the instanced meshes (houses,
ladders, plants) from gedit/s04/mapinfo.cfg, builds the fishing collision (rock caps/skirts +
shore perimeter) and the cast rect, and writes a self-contained interactive HTML viewer
(brownboo_viewer.html) next to this script. Requires tools/extract_scene_mesh.py and a local
data.hed/hd2/dat extraction (see extract_scene_mesh.DAT_DIR).

Run: python3 tools/brownboo_viewer.py   ->  tools/brownboo_viewer.html (+ .json)
"""
import os, struct, re, sys, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract_scene_mesh import load_scene, parse_mds, read_verts, read_tris, xform, extract_mesh
from scene_viewer_html import build_html

HERE = os.path.dirname(os.path.abspath(__file__))
# The generated viewer embeds game scene geometry -> untracked game_data/brownboo/ (never committed).
OUT = os.path.join(HERE, "..", "game_data", "brownboo")
os.makedirs(OUT, exist_ok=True)
HTML_NAME = "brownboo_viewer.html"

scn = load_scene('gedit/s04/scene.scn')
BROWNBOO_MESHES = extract_mesh(scn)

def tris_where(pred):
    out = []
    for name, (v, ts) in BROWNBOO_MESHES.items():
        if pred(name):
            for a, b, c in ts: out.append([list(v[a]), list(v[b]), list(v[c])])
    return out

craterids = [f's04g01{n:02d}' for n in range(2, 17)] + ['s040101']
# Layer predicates over scene-node NAMES. Kept as named predicates (not inline lambdas) so the same rule
# builds both the flat per-layer triangle lists AND the per-node labels/borders (see nodelabels below).
def is_pond(n):    return n == 's04g0117__s1'                       # the POND BOTTOM (its own toggle now)
def is_shore(n):   return n.startswith('s04g0117') and not is_pond(n)
PRED = {
    'watersurf':   lambda n: 'czapp' in n,
    'shore':       is_shore,
    'pond':        is_pond,
    # The boardwalk structure splits cleanly BY NODE (like the za01 foam split below), verified from the
    # mesh: s04g03a* is the DECK — the horizontal walking planks (150+ horizontal tris) plus their railings;
    # s04g03b* is the above-water STILTS — all-vertical posts, 88% below the deck (y<7). s04g0401/402/403/404
    # (y -15..0) are the same posts continued UNDERWATER (also the collision-stilt source at col_stilts).
    'boardwalk':   lambda n: n.startswith('s04g03a'),
    'stilts':      lambda n: n.startswith('s04g03b'),            # above-water posts (the garbled-texture mesh)
    'stilts_base': lambda n: re.match(r's04g0?40[1-4]$', n) is not None,  # underwater posts (0401/402/403/404)
    'rock':        lambda n: n.startswith('iwa'),
    'fence':       lambda n: n.startswith('st0'),
    'crater':      lambda n: any(n.startswith(c) for c in craterids),
    # NOTE: enter__s is NOT here — it is a CHILD of the crater wall s04g0102__s, so its own matrix alone
    # (what tris_where()/extract_mesh uses) puts it underground. It's built with full parent accumulation below.
}
visual = {k: tris_where(p) for k, p in PRED.items()}

# ---- instanced meshes placed from mapinfo.cfg ----
cfg = load_scene('gedit/s04/mapinfo.cfg').decode('latin1', 'replace')
lines = cfg.splitlines(); placements = []; i = 0
while i < len(lines):
    m = re.match(r'\s*(GROUND|WATER)\s+"([^"]+)"', lines[i])
    if m and m.group(2).startswith('s04'):
        nums = []; j = i+1
        while j < len(lines) and len(nums) < 2 and j-i < 14:
            parts = [p.strip() for p in re.split(r'[,\t]', lines[j].split('//')[0]) if p.strip()]
            if parts and all(re.match(r'^-?\d+\.?\d*$', p) for p in parts) and len(parts) >= 3:
                nums.append([float(x) for x in parts[:3]])
            j += 1
        if len(nums) >= 2: placements.append((m.group(2), nums[0], nums[1]))
        i = j
    else: i += 1
# SCN sub-file directory
scndir = {}; o = 0x10
for _ in range(40):
    nm = scn[o:o+16].split(b'\x00')[0].decode('latin1', 'replace')
    if not nm or not nm[0].isalnum(): break
    off, size = struct.unpack_from('<II', scn, o+0x10); scndir.setdefault(nm, []).append((off, size)); o += 0x30
def subfile_mesh(off, size, skip=None):
    end = off+size; V = []; Tr = []
    for m in re.finditer(rb'MDS\x00', scn[off:end]):
        mds = off+m.start(); nodes = parse_mds(scn, mds)
        if nodes:
            for name, mo, mat in nodes:
                if skip and skip(name): continue   # e.g. the reusable door template (obj*) in house blocks
                fo = next((c for c in (mo, mds+mo) if mo and 0 < c < len(scn) and scn[c:c+3] == b'MDT'), None)
                if not fo: continue
                b = len(V); V += [xform(mat, v) for v in read_verts(scn, fo)]; Tr += [(a+b, c+b, d+b) for a, c, d in read_tris(scn, fo)]
        else:
            for mm in re.finditer(rb'MDT\x00', scn[mds:end]):
                fo = mds+mm.start(); b = len(V); V += [list(v) for v in read_verts(scn, fo)]; Tr += [(a+b, c+b, d+b) for a, c, d in read_tris(scn, fo)]; break
    return V, Tr
def placeY(v, pos, ry):
    th = math.radians(ry); c = math.cos(th); s = math.sin(th)
    return [[x*c+z*s+pos[0], y+pos[1], -x*s+z*c+pos[2]] for x, y, z in v]

def _compose(a, b):
    """Column-major 4x4 product a*b (xform convention: out.x = m[0]x+m[4]y+m[8]z+m[12])."""
    r = [0.0] * 16
    for c in range(4):
        for row in range(4):
            r[c*4+row] = sum(a[k*4+row] * b[c*4+k] for k in range(4))
    return r

def subtree_meshes(off, size, want):
    """{node_name: [world (house-LOCAL) tris]} for nodes whose name matches want(name), with the FULL
    parent-chain transform accumulated. The house doors/windows (obj23-32) hang off `null3_*` locator
    nodes several levels deep, so their own matrix alone (what subfile_mesh uses) puts them at the origin;
    accumulating the parents lands them on the correct wall."""
    end = off + size
    for m in re.finditer(rb'MDS\x00', scn[off:end]):
        mds = off + m.start()
        ver, cnt, tbl = struct.unpack_from('<3I', scn, mds+4)
        if not (0 < cnt < 400):
            continue
        nodes = []
        for i in range(cnt):
            b = mds + tbl + i*0x70
            nm = scn[b+8:b+8+16].split(b'\x00')[0].decode('latin1', 'replace')
            mo = struct.unpack_from('<i', scn, b+0x28)[0]
            par = struct.unpack_from('<i', scn, b+0x2c)[0]
            mat = list(struct.unpack_from('<16f', scn, b+0x30))
            nodes.append((nm, mo, par, mat))
        world = [None] * cnt
        def wm(i):
            if world[i] is None:
                nm, mo, par, mat = nodes[i]
                world[i] = mat if par < 0 or par >= cnt else _compose(wm(par), mat)
            return world[i]
        out = {}
        for i, (nm, mo, par, mat) in enumerate(nodes):
            if mo == 0 or not want(nm):
                continue
            fo = next((c for c in (mo, mds+mo) if 0 < c < len(scn) and scn[c:c+3] == b'MDT'), None)
            if not fo:
                continue
            M = wm(i); wv = [xform(M, v) for v in read_verts(scn, fo)]
            out.setdefault(nm, []).extend([[wv[a], wv[b], wv[c]] for a, b, c in read_tris(scn, fo)])
        return out
    return {}

def accum_extract(want):
    """{node_name: [world tris]} for matching nodes ANYWHERE in scene.scn, with full parent-chain transforms.
    Unlike extract_mesh (own matrix only, right for root nodes), this resolves nodes parented under another —
    e.g. enter__s, a child of the crater wall s04g0102__s."""
    out = {}
    for m in re.finditer(rb'MDS\x00', scn):
        mds = m.start()
        try:
            ver, cnt, tbl = struct.unpack_from('<3I', scn, mds+4)
        except struct.error:
            continue
        if not (0 < cnt < 400) or mds + tbl + cnt*0x70 > len(scn):
            continue
        nodes = []
        for i in range(cnt):
            b = mds + tbl + i*0x70
            nm = scn[b+8:b+8+16].split(b'\x00')[0].decode('latin1', 'replace')
            mo = struct.unpack_from('<i', scn, b+0x28)[0]
            par = struct.unpack_from('<i', scn, b+0x2c)[0]
            mat = list(struct.unpack_from('<16f', scn, b+0x30))
            nodes.append((nm, mo, par, mat))
        world = [None] * cnt
        def wm(i):
            if world[i] is None:
                nm, mo, par, mat = nodes[i]
                world[i] = mat if (par < 0 or par >= cnt) else _compose(wm(par), mat)
            return world[i]
        for i, (nm, mo, par, mat) in enumerate(nodes):
            if mo == 0 or not want(nm):
                continue
            fo = next((c for c in (mo, mds+mo) if 0 < c < len(scn) and scn[c:c+3] == b'MDT'), None)
            if not fo:
                continue
            M = wm(i); wv = [xform(M, v) for v in read_verts(scn, fo)]
            out.setdefault(nm, []).extend([[wv[a], wv[b], wv[c]] for a, b, c in read_tris(scn, fo)])
    return out

# obj23-32 in the house sub-files are the DOORS (obj23/25/27/31, ~143 tris, floor-to-eave) and WINDOWS
# (obj24/26/28/32, ~32 tris, up on the wall). subfile_mesh drops them (skip obj*) because without the
# parent chain they collapse to the origin; subtree_meshes places them on the wall so we can show them.
# door/window obj numbers reused per house (obj29/30 are a door+window in s04h02, distinct from the
# same-named world shadow meshes, which live in other MDS blocks that subtree_meshes never touches).
def is_door(n):   return re.match(r'obj(23|25|27|29|31)(_\d)?$', n) is not None
def is_window(n): return re.match(r'obj(24|26|28|30|32)(_\d)?$', n) is not None
houses, ladders, plants, doors, windows = [], [], [], [], []
placed_labels = []   # [[centroid, name, bbox, layId], ...] for placed instances (labelled like scene nodes)
def _bbox_centroid(tris):
    ps = [p for tri in tris for p in tri]
    xs = [p[0] for p in ps]; ys = [p[1] for p in ps]; zs = [p[2] for p in ps]
    cen = [round(sum(xs)/len(xs), 1), round(sum(ys)/len(ys), 1), round(sum(zs)/len(zs), 1)]
    return cen, [round(min(xs), 1), round(min(ys), 1), round(min(zs), 1), round(max(xs), 1), round(max(ys), 1), round(max(zs), 1)]
used = {}
for name, pos, rot in placements:
    if name.startswith('s04g') or name.startswith('s04w'): continue
    idx = used.get(name, 0); used[name] = idx+1
    ents = scndir.get(name, [])
    if not ents: continue
    off, size = ents[min(idx, len(ents)-1)]
    # houses embed the door/window objs deep in the hierarchy — exclude them from the body pass and place
    # them separately (below) with full parent transforms.
    skip = (lambda n: n.startswith('obj')) if name.startswith('s04h') else None
    v, t = subfile_mesh(off, size, skip)
    if v:
        pv = placeY(v, pos, rot[1]); tris = [[pv[a], pv[b], pv[c]] for a, b, c in t]
        lay = 'houses' if name.startswith('s04h') else 'ladders' if name.startswith('s04r') else 'plants'
        (houses if lay == 'houses' else ladders if lay == 'ladders' else plants).extend(tris)
        cen, bb = _bbox_centroid(tris); placed_labels.append([cen, f"{name}#{idx}", bb, lay])
    if name.startswith('s04h'):
        for nm, tset in subtree_meshes(off, size, lambda n: is_door(n) or is_window(n)).items():
            flat = [p for tri in tset for p in tri]
            placed = placeY(flat, pos, rot[1])
            ptris = [placed[k:k+3] for k in range(0, len(placed), 3)]
            lay = 'doors' if is_door(nm) else 'windows'
            (doors if lay == 'doors' else windows).extend(ptris)
            cen, bb = _bbox_centroid(ptris); placed_labels.append([cen, f"{nm}@{name}#{idx}", bb, lay])
visual['houses'] = houses; visual['ladders'] = ladders; visual['plants'] = plants
visual['doors'] = doors; visual['windows'] = windows
print(f"doors: {len(doors)} tris, windows: {len(windows)} tris")

# entrance ramp (enter__s) — parented under the crater wall s04g0102__s, so build it with accumulation
entrance = []
for nm, tset in accum_extract(lambda n: n.startswith('enter')).items():
    entrance.extend(tset)
    cen, bb = _bbox_centroid(tset); placed_labels.append([cen, nm, bb, 'entrance'])
visual['entrance'] = entrance
print(f"entrance: {len(entrance)} tris (parent-accumulated)")

# ---- split the water-edge foam (za01) into outer-shore vs interior (stilt/plant rings) ----
# The foam splits cleanly BY NODE, no heuristic needed: s04w02__za01 IS the continuous outer-shore ring
# (verified: all 128 of its tris lie in the ~16-wide shoreline band), while s04w01__za01 is the stilt/
# plant foam (the sunburst rings around the boardwalk posts + the plant rings, 256 tris). Earlier
# proximity rules kept nicking real ring tris that pass near an edge-stilt; the node split never does.
visual['foam_outer'] = tris_where(lambda n: n.startswith('s04w02') and 'za01' in n)   # the shore ring
visual['foam_obj']   = tris_where(lambda n: n.startswith('s04w01') and 'za01' in n)   # stilt/plant foam
print("foam split:", len(visual['foam_outer']), "outer-shore ring +", len(visual['foam_obj']), "stilt/plant")

# ---- per-node labels (name + highlighted bounding-box border), drawn as an overlay like the fishing
# coord labels — NOT as extra checkboxes. Each carries the LAY layer id it belongs to, so a label only
# shows when both the "node labels" toggle and that node's layer are on. Scene nodes are mapped to their
# layer via PRED (+ the foam node-split); placed instances (houses/doors/windows/…) were collected above.
LAYMAP = {'watersurf': 'watersurf', 'shore': 'shore', 'pond': 'pond', 'boardwalk': 'board',
          'stilts': 'stilts', 'stilts_base': 'stiltsbase', 'rock': 'rock', 'fence': 'fence',
          'crater': 'crater', 'entrance': 'entrance'}
def scene_layid(name):
    for k, p in PRED.items():
        if p(name):
            return LAYMAP.get(k)
    if name.startswith('s04w02') and 'za01' in name: return 'foamouter'
    if name.startswith('s04w01') and 'za01' in name: return 'foamobj'
    return None
nodelabels = list(placed_labels)
for name, (v, ts) in BROWNBOO_MESHES.items():
    lay = scene_layid(name)
    if not v or lay is None:
        continue
    xs = [p[0] for p in v]; ys = [p[1] for p in v]; zs = [p[2] for p in v]
    cen = [round(sum(xs)/len(xs), 1), round(sum(ys)/len(ys), 1), round(sum(zs)/len(zs), 1)]
    bb = [round(min(xs), 1), round(min(ys), 1), round(min(zs), 1), round(max(xs), 1), round(max(ys), 1), round(max(zs), 1)]
    nodelabels.append([cen, name, bb, lay])
print(f"node labels: {len(nodelabels)} ({len(placed_labels)} placed + {len(nodelabels)-len(placed_labels)} scene)")

# ---- collision (rocks) ----
def hull(pts):
    pts = sorted(set((round(x, 1), round(z, 1)) for x, z in pts))
    if len(pts) < 3: return pts
    cr = lambda o, a, b: (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
    lo = []
    for p in pts:
        while len(lo) >= 2 and cr(lo[-2], lo[-1], p) <= 0: lo.pop()
        lo.append(p)
    up = []
    for p in reversed(pts):
        while len(up) >= 2 and cr(up[-2], up[-1], p) <= 0: up.pop()
        up.append(p)
    return lo[:-1]+up[:-1]
def decim(h, n): return h if len(h) <= n else [h[int(i*len(h)/n)] for i in range(n)]

# ---- UNSIMPLIFIED collision reference: exact mesh triangles, clipped to the collision height band ----
CY_LO, CY_HI = -9, 54        # full collision height band (CY_HI tracks BOX_TOP below)
WATER = 0                    # WaterLevel — perimeter/plants/houses collision caps here
def clip_y(tri, lo, hi):
    """Clip one triangle to the slab lo <= y <= hi; return fan-triangulated pieces (possibly empty)."""
    def cut(poly, above, yv):
        out = []
        for i in range(len(poly)):
            a = poly[i]; b = poly[(i+1) % len(poly)]
            ain = (a[1] >= yv) if above else (a[1] <= yv)
            bn  = (b[1] >= yv) if above else (b[1] <= yv)
            if ain: out.append(a)
            if ain != bn:
                t = (yv - a[1]) / (b[1] - a[1])
                out.append([a[0]+t*(b[0]-a[0]), yv, a[2]+t*(b[2]-a[2])])
        return out
    poly = cut([list(v) for v in tri], True, lo)
    if len(poly) < 3: return []
    poly = cut(poly, False, hi)
    if len(poly) < 3: return []
    return [[poly[0], poly[i], poly[i+1]] for i in range(1, len(poly)-1)]
def clip_group(tris, lo=CY_LO, hi=CY_HI):
    out = []
    for t in tris: out += clip_y(t, lo, hi)
    return out
def cap_at(tris, y):
    """Fill the open cross-section where `tris` are sliced by the plane Y=y (a rock clipped at the box top):
    collect every triangle-edge crossing at that height, order them around the centroid and fan-triangulate
    into a flat horizontal cap — so a downward raycast (bobber/hook) lands on it instead of falling through
    the opening. Returns [] if nothing crosses y (the rock is entirely below the box top)."""
    pts = []; seen = set()
    for t in tris:
        for i in range(3):
            a = t[i]; b = t[(i+1) % 3]
            if (a[1]-y)*(b[1]-y) < 0:                      # edge straddles the plane
                s = (y-a[1])/(b[1]-a[1])
                px, pz = a[0]+s*(b[0]-a[0]), a[2]+s*(b[2]-a[2])
                k = (round(px, 2), round(pz, 2))           # shared edges yield the same crossing twice
                if k not in seen: seen.add(k); pts.append((px, pz))
    if len(pts) < 3: return []
    cx = sum(p[0] for p in pts)/len(pts); cz = sum(p[1] for p in pts)/len(pts)
    pts.sort(key=lambda p: math.atan2(p[1]-cz, p[0]-cx))
    return [[[a[0], y, a[1]], [cx, y, cz], [b[0], y, b[1]]]
            for a, b in ((pts[i], pts[(i+1) % len(pts)]) for i in range(len(pts)))]
def line_x(p1, p2, p3, p4):
    (x1, y1), (x2, y2), (x3, y3), (x4, y4) = p1, p2, p3, p4
    d = (x1-x2)*(y3-y4)-(y1-y2)*(x3-x4)
    if abs(d) < 1e-9: return None
    t = ((x1-x3)*(y3-y4)-(y1-y3)*(x3-x4))/d
    return (x1+t*(x2-x1), y1+t*(y2-y1))
def inset_polygon(pts, dd):
    """Offset a closed polygon inward by dd (edge-normal offset + adjacent-edge intersection)."""
    n = len(pts); cx0 = sum(p[0] for p in pts)/n; cz0 = sum(p[1] for p in pts)/n
    edges = []
    for i in range(n):
        a = pts[i]; b = pts[(i+1) % n]; ex, ez = b[0]-a[0], b[1]-a[1]
        nx, nz = -ez, ex; L = math.hypot(nx, nz) or 1; nx /= L; nz /= L
        mx, mz = (a[0]+b[0])/2, (a[1]+b[1])/2
        if nx*(cx0-mx)+nz*(cz0-mz) < 0: nx, nz = -nx, -nz     # inward
        edges.append(((a[0]+nx*dd, a[1]+nz*dd), (b[0]+nx*dd, b[1]+nz*dd)))
    out = []
    for i in range(n):
        p = line_x(edges[(i-1) % n][0], edges[(i-1) % n][1], edges[i][0], edges[i][1])
        out.append(list(p) if p else list(edges[i][0]))
    return out

def _tri_height_at(tri, x, z):
    """Y of a triangle's plane at (x,z) via barycentric, or None if (x,z) is outside its XZ projection."""
    (x1, y1, z1), (x2, y2, z2), (x3, y3, z3) = tri
    d = (z2-z3)*(x1-x3) + (x3-x2)*(z1-z3)
    if abs(d) < 1e-9: return None                     # degenerate in XZ (a vertical side)
    a = ((z2-z3)*(x-x3) + (x3-x2)*(z-z3))/d
    b = ((z3-z1)*(x-x3) + (x1-x3)*(z-z3))/d
    c = 1 - a - b
    if a < -1e-6 or b < -1e-6 or c < -1e-6: return None
    return a*y1 + b*y2 + c*y3

def _column_span(tris, x, z):
    """min/max surface Y of the rock over the vertical column at (x,z), or (None,None) if none covers it."""
    lo = hi = None
    for t in tris:
        y = _tri_height_at(t, x, z)
        if y is not None:
            if lo is None or y < lo: lo = y
            if hi is None or y > hi: hi = y
    return lo, hi

def _flood_components(inside, nx, nz):
    """8-connected components of the True cells in `inside` — one per pillar of a rock."""
    seen = [[False]*nz for _ in range(nx)]; comps = []
    for i in range(nx):
        for j in range(nz):
            if inside[i][j] and not seen[i][j]:
                stack = [(i, j)]; seen[i][j] = True; comp = []
                while stack:
                    ci, cj = stack.pop(); comp.append((ci, cj))
                    for di in (-1, 0, 1):
                        for dj in (-1, 0, 1):
                            ni, nj = ci+di, cj+dj
                            if 0 <= ni < nx and 0 <= nj < nz and inside[ni][nj] and not seen[ni][nj]:
                                seen[ni][nj] = True; stack.append((ni, nj))
                comps.append(comp)
    return comps

def _lathe(pv, cx, cz, ytop, ybottom, nsides, nlevels, margin):
    """Fit ONE smooth low-poly tapered tube (a lathe) to a pillar's vertex cloud `pv`, centred at (cx,cz),
    from `ybottom` up to `ytop`. Per (height level, angle sector) we take the pillar's max radius, enforce
    a downward taper (a level is at least as wide as the one above → an enveloping, monotone pillar), fill
    empty sectors and smooth the rings so the surface is SMOOTH (no spikes) and CLOSED (no gaps). Returns
    the tube walls + a flat top and bottom cap. `margin` pushes every radius out so the shell never sits
    inside the visual rock."""
    twopi = 2*math.pi
    levels = [ybottom + (ytop-ybottom)*k/(nlevels-1) for k in range(nlevels)]
    R = [[0.0]*nsides for _ in range(nlevels)]; seen = [[False]*nsides for _ in range(nlevels)]
    for (x, y, z) in pv:
        yy = ytop if y > ytop else y
        r = math.hypot(x-cx, z-cz)
        a = int((math.atan2(z-cz, x-cx) % twopi)/twopi*nsides) % nsides
        k = min(range(nlevels), key=lambda kk: abs(levels[kk]-yy))
        if r > R[k][a]: R[k][a] = r; seen[k][a] = True
    for k in range(nlevels-2, -1, -1):                      # taper: a lower ring is >= the one above it
        for a in range(nsides):
            if R[k][a] < R[k+1][a]:
                R[k][a] = R[k+1][a]
                if seen[k+1][a]: seen[k][a] = True
    for k in range(nlevels):                                # fill empty angle sectors (circular)
        if not any(seen[k]):
            for kk in list(range(k+1, nlevels)) + list(range(k-1, -1, -1)):
                if any(seen[kk]): R[k] = R[kk][:]; break
            continue
        for a in range(nsides):
            if not seen[k][a]:
                for d in range(1, nsides):
                    l, rt = (a-d) % nsides, (a+d) % nsides
                    if seen[k][l] and seen[k][rt]: R[k][a] = (R[k][l]+R[k][rt])/2; break
                    if seen[k][l]: R[k][a] = R[k][l]; break
                    if seen[k][rt]: R[k][a] = R[k][rt]; break
    for _ in range(2):                                      # smooth each ring circularly (kill spikes)
        for k in range(nlevels):
            R[k] = [(R[k][(a-1) % nsides] + 2*R[k][a] + R[k][(a+1) % nsides])/4 for a in range(nsides)]
    for k in range(nlevels):
        for a in range(nsides): R[k][a] += margin
    def pt(k, a):
        ang = twopi*a/nsides
        return [cx + R[k][a]*math.cos(ang), levels[k], cz + R[k][a]*math.sin(ang)]
    out = []
    for k in range(nlevels-1):                             # tube walls
        for a in range(nsides):
            b = (a+1) % nsides
            out.append([pt(k, a), pt(k, b), pt(k+1, b)]); out.append([pt(k, a), pt(k+1, b), pt(k+1, a)])
    tc = [cx, levels[-1], cz]; bc = [cx, levels[0], cz]
    for a in range(nsides):                                # flat top + bottom caps (closed → no gaps)
        b = (a+1) % nsides
        out.append([pt(nlevels-1, a), tc, pt(nlevels-1, b)])
        out.append([pt(0, b), bc, pt(0, a)])
    return out

def build_rock_smooth(tris, cell, water, ycap, ybottom, nsides, nlevels, margin, lift):
    """Collision for a rock as one smooth tapered LATHE per pillar. A rock may be a DOLMEN (2 pillars + a
    lintel, e.g. iwa01/iwa02): we detect the pillar footprints (columns with rock below `ycap`), split them
    into connected components (each = one pillar), and lathe each — so the lintel (above ycap) is dropped
    and the archway between the pillars stays an open TUNNEL. Smooth by construction (no spiky height-field
    sampling), closed (no gaps), low-poly, and extended down to `ybottom` below the water."""
    ps = [p for t in tris for p in t]
    minx = min(p[0] for p in ps); minz = min(p[2] for p in ps)
    maxx = max(p[0] for p in ps); maxz = max(p[2] for p in ps)
    xs = [minx + i*cell for i in range(int((maxx-minx)/cell)+2)]
    zs = [minz + j*cell for j in range(int((maxz-minz)/cell)+2)]
    nx, nz = len(xs), len(zs)
    inside = [[False]*nz for _ in range(nx)]
    for i in range(nx):
        for j in range(nz):
            lo, _ = _column_span(tris, xs[i], zs[j])
            inside[i][j] = lo is not None and lo < ycap        # a pillar base is under this column
    comps = _flood_components(inside, nx, nz)
    cid = {}
    for idx, comp in enumerate(comps):
        for (i, j) in comp: cid[(i, j)] = idx
    verts = list({tuple(p) for t in tris for p in t})
    groups = [[] for _ in comps]
    for v in verts:
        if v[1] >= ycap: continue                              # skip lintel verts
        i = max(0, min(nx-1, int((v[0]-minx)/cell))); j = max(0, min(nz-1, int((v[2]-minz)/cell)))
        c = cid.get((i, j))
        if c is not None: groups[c].append(v)
    out = []
    for g in groups:
        if len(g) < 6: continue
        cx = sum(p[0] for p in g)/len(g); cz = sum(p[2] for p in g)/len(g)
        ytop = min(max(p[1] for p in g), ycap) + lift
        out += _lathe(g, cx, cz, ytop, ybottom, nsides, nlevels, margin)
    return out

def _min_face_flatness(tris, water):
    """min |normal.Y|/|normal| over ABOVE-water faces (for reporting how steep the pillar sides get)."""
    m = 1.0
    for (a, b, c) in tris:
        if (a[1]+b[1]+c[1])/3.0 <= water + 1e-3: continue
        ux, uy, uz = b[0]-a[0], b[1]-a[1], b[2]-a[2]
        vx, vy, vz = c[0]-a[0], c[1]-a[1], c[2]-a[2]
        nx_, ny_, nz_ = uy*vz-uz*vy, uz*vx-ux*vz, ux*vy-uy*vx
        L = math.sqrt(nx_*nx_ + ny_*ny_ + nz_*nz_)
        if L > 1e-9: m = min(m, abs(ny_)/L)
    return m

# rock collision = one SMOOTH tapered lathe per pillar (build_rock_smooth): dolmen lintels dropped above
# ycap so archways stay open TUNNELS, low-poly & closed (no gaps/spikes), extended down to YBOTTOM.
ROCK_CELL, ROCK_MARGIN, ROCK_SIDES, ROCK_LEVELS, ROCK_LIFT = 8.0, 2.0, 12, 5, 2.0
SMALL_CELL, SMALL_MARGIN, SMALL_SIDES, SMALL_LEVELS, SMALL_LIFT = 6.0, 1.5, 10, 4, 1.5
YCAP, YBOTTOM = 40.0, -9.0        # ignore geometry above 40 (the lintel); extend collision down to -9
# rock collision: each rock's hand-simplified Blender mesh as FROZEN in brownboo_rock_data.py — exactly what
# the Patch ISO bake appends to s04g01_a — with the smooth lathe as a fallback for a rock absent from it.
sys.path.insert(0, os.path.join(HERE, 'iso_patch', 'collision'))
from brownboo_rock_data import ROCKS as _ROCKS
_ROCK_DATA = dict(_ROCKS)
col_rocks = []
_rock_src = []
for _nm, (_v, _ts) in BROWNBOO_MESHES.items():
    if not _nm.startswith('iwa'): continue
    _base = _nm.split('__')[0]
    _rt = [[_v[a], _v[b], _v[c]] for a, b, c in _ts]
    if 'rock_' + _base in _ROCK_DATA:
        _r = [[list(p) for p in t] for t in _ROCK_DATA['rock_' + _base]]; _src = 'blender'
    elif _base == 'iwa03':
        _r = build_rock_smooth(_rt, SMALL_CELL, WATER, YCAP, YBOTTOM, SMALL_SIDES, SMALL_LEVELS, SMALL_MARGIN, SMALL_LIFT); _src = 'lathe'
    else:
        _r = build_rock_smooth(_rt, ROCK_CELL, WATER, YCAP, YBOTTOM, ROCK_SIDES, ROCK_LEVELS, ROCK_MARGIN, ROCK_LIFT); _src = 'lathe'
    col_rocks += _r; _rock_src.append(f'{_base}={len(_r)}({_src})')
_flat = _min_face_flatness(col_rocks, WATER)
print(f"[rock collision] {len(col_rocks)} tris  [{', '.join(_rock_src)}]  min|ny|/l(above water)={_flat:.3f}")
col_stilts = clip_group(tris_where(lambda n: n.startswith('s04g0401') or n in ('s04g402', 's04g403', 's04g404')))  # -9..44
col_plants = clip_group(plants, hi=WATER)   # placed s04a01, capped at water
col_build  = clip_group(houses, hi=WATER)   # placed s04h*,  capped at water
# perimeter: the traced shoreline inset 20 units inward, extruded to a vertical wall CY_LO..WATER
perim = [-243,-72,-147,-250,-115,-271,-91,-281,-37,-294,55,-296,71,-292,164,-239,218,-176,266,-68,287,10,295,24,291,95,285,108,285,131,249,169,205,204,179,214,98,232,76,232,10,245,-67,230,-192,160,-248,59,-258,25,-251,-31,-235,-69]
Pin = inset_polygon([(perim[i], perim[i+1]) for i in range(0, len(perim), 2)], 20)
col_perim = []
for i in range(len(Pin)):
    a = Pin[i]; b = Pin[(i+1) % len(Pin)]
    col_perim.append([[a[0], CY_LO, a[1]], [b[0], CY_LO, b[1]], [b[0], WATER, b[1]]])
    col_perim.append([[a[0], CY_LO, a[1]], [b[0], WATER, b[1]], [a[0], WATER, a[1]]])
# Fishing rect as a 3D BOX filled with a 6-unit point grid: from fish depth (WaterLevel - fishDepth)
# up to BOX_TOP (arbitrary for now — later = the height at which bobber/hook collisions matter).
# edges (compass: E=+X, W=-X, N=-Z, S=+Z): W=-320, E=310, N=-260, S=300
RECT_X1, RECT_Z1, RECT_X2, RECT_Z2 = -250, -240, 250, 240
FISH_DEPTH = 0 - 6      # WaterLevel 0 - fishDepth 6
BOX_TOP = 54            # TODO: set to the real bobber/hook collision height
def frange(a, b, step):
    out = []; v = a
    while v <= b + 1e-6: out.append(round(v, 3)); v += step
    return out
fishbox = [[x, y, z] for x in frange(RECT_X1, RECT_X2, 10)
                     for y in frange(FISH_DEPTH, BOX_TOP, 10)
                     for z in frange(RECT_Z1, RECT_Z2, 10)]
# coordinate labels (shown with the fishing box): origin, the trigger + sign, and the 4 rect corners (at top)
fishlabels = [[[0, 0, 0], "0,0"], [[212, 12, -53], "trigger (212,12,-53)"], [[212, 9, -61], "sign (212,9,-61)"]]
for cxx, czz in [(RECT_X1, RECT_Z1), (RECT_X2, RECT_Z1), (RECT_X2, RECT_Z2), (RECT_X1, RECT_Z2)]:
    fishlabels.append([[cxx, BOX_TOP, czz], f"{cxx},{czz}"])

# The fishing trigger point (the "!" marker) and its interaction radius, as a solid sphere.
# Must match the Brownboo Spot in CustomFishingSpot.cs (tx,ty,tz + InteractRadius).
def uv_sphere(cx0, cy0, cz0, r, rings=10, segs=14):
    def pt(i, j):
        th = math.pi * i / rings; ph = 2 * math.pi * j / segs
        return [cx0 + r*math.sin(th)*math.cos(ph), cy0 + r*math.cos(th), cz0 + r*math.sin(th)*math.sin(ph)]
    tris = []
    for i in range(rings):
        for j in range(segs):
            a, b, c, d = pt(i, j), pt(i+1, j), pt(i+1, j+1), pt(i, j+1)
            tris.append([a, b, c]); tris.append([a, c, d])
    return tris
fishpoint = uv_sphere(212, 12, -53, 10)   # trigger (212,12,-53), InteractRadius 10 — must match CustomFishingSpot.cs

# The fishing SIGN mesh (kanban.mds), rendered at its baked scene position (212,9,-61), identity rotation.
# Verts are local (origin-centred); the scene/mapinfo place it there, so we translate the local verts to match.
SIGN_POS = (212, 9, -61)
_kb = open(os.path.join(HERE, "..", "game_data", "fishsign", "kanban.mds"), "rb").read()
_sv = read_verts(_kb, 0x80)               # kanban MDT is at 0x80
sign_mesh = [[[_sv[i][0] + SIGN_POS[0], _sv[i][1] + SIGN_POS[1], _sv[i][2] + SIGN_POS[2]] for i in (a, b, c)]
             for a, b, c in read_tris(_kb, 0x80)]
print(f"sign mesh: {len(sign_mesh)} tris at {SIGN_POS}")

# ---- VANILLA native cpoly, dumped live from RAM by GeoramaProbe.DumpCPolyFile ----
# This is the EXACT collision the town already loads (PickUpPoly) at fishing-spot load. Splitting by
# the triangle's (NORMALIZED) normal.Y shows what KIND of collision exists where: floor-ish (|ny|>0.7)
# is what the hook/bobber raycast honours; wall-ish (|ny|<0.3) is what would contain fish.
#
# The total cpoly count is capped (1024); every native poly we DON'T need is a slot the fishing
# collision can reuse. We flag three reclaimable groups (mirror these thresholds in the mod's
# native-cpoly compaction):
#   above box-top : entirely above BOX_TOP  -> irrelevant to bobber/hook (they only matter near water)
#   NE corner     : x>NE_X & z<NE_Z         -> the unreachable north-east pocket
#   ladder-tops   : within LAD_R of a ladder & above LAD_Y -> the shafts/platforms climbing out of water
# The mod KEEPS the native walls in the fishing cpoly (since 2026-09; they contain the fish); its only
# reclaim is the FLOOR platforms sitting on top of the in-water ladders — floors the bobber/hook would
# otherwise catch on. Near a ladder AND entirely above LAD_Y (pond floor near a ladder base is low-Y, so it stays).
# Mirrors the mod's IsLadderTopFloor (LADDER positions / radius / height must match CustomFishingSpot.cs).
LAD_POS = [(p[1][0], p[1][2]) for p in placements if p[0].startswith('s04r')]
LAD_R, LAD_Y = 45, 25   # top platforms lean out up to ~42u from the base position
def van_cut(cx, cy, cz, miny):
    return miny >= LAD_Y and any(math.hypot(cx-lx, cz-lz) < LAD_R for lx, lz in LAD_POS)
van_floor, van_wall, van_mid, van_dropped = [], [], [], []
CPOLY_CSV = os.path.join(OUT, 'vanilla_cpoly.csv')   # dumped by GeoramaProbe into game_data/brownboo/
if os.path.exists(CPOLY_CSV):
    with open(CPOLY_CSV) as fh:
        next(fh, None)   # header row
        for ln in fh:
            p = ln.strip().split(',')
            if len(p) < 12: continue
            try: f = [float(x) for x in p]
            except ValueError: continue
            tri = [[f[0], f[1], f[2]], [f[3], f[4], f[5]], [f[6], f[7], f[8]]]
            cx = (f[0]+f[3]+f[6])/3; cy = (f[1]+f[4]+f[7])/3; cz = (f[2]+f[5]+f[8])/3
            miny = min(f[1], f[4], f[7])
            nl = math.hypot(f[9], f[10], f[11]) or 1; ny = abs(f[10]/nl)     # NORMALIZE the raw normal
            if ny < 0.3:
                van_wall.append(tri)                         # a wall — dropped wholesale by the mod
            elif van_cut(cx, cy, cz, miny):
                van_dropped.append(tri)                      # floor/slope on a ladder top — also reclaimed
            else:
                (van_floor if ny > 0.7 else van_mid).append(tri)
    kept = len(van_floor)+len(van_mid)
    print(f"vanilla cpoly: KEEP {kept} floor/slope ({len(van_floor)} floor + {len(van_mid)} slope);"
          f" DROP {len(van_wall)} walls + {len(van_dropped)} ladder-top floors")
else:
    print("vanilla cpoly: (no vanilla_cpoly.csv — start fishing in Brownboo to dump it)")

# ---- base-ground GRID, dumped live from RAM (GeoramaProbe.DumpGroundGrid) = the accurate pond bottom ----
# CSV: area,i,j,worldX,worldZ,height,code. Reconstruct the surface by connecting each cell to its +i/+j
# neighbours into quads. Split at the waterline so the underwater BOWL (the pond bottom the collision
# gather skips) reads distinctly from the dry land.
import csv as _csv
grid_bottom, grid_land = [], []
GRID_CSV = os.path.join(OUT, 'ground_grid.csv')
if os.path.exists(GRID_CSV):
    cells = {}
    with open(GRID_CSV) as fh:
        for r in _csv.DictReader(fh):
            cells[(int(r['area']), int(r['i']), int(r['j']))] = \
                (float(r['worldX']), float(r['worldZ']), float(r['height']), int(r['code']))
    for (a, i, j), (wx, wz, h, code) in cells.items():
        c10 = cells.get((a, i+1, j)); c01 = cells.get((a, i, j+1)); c11 = cells.get((a, i+1, j+1))
        if not (c10 and c01 and c11): continue
        p00 = [wx, h, wz]; p10 = [c10[0], c10[2], c10[1]]; p01 = [c01[0], c01[2], c01[1]]; p11 = [c11[0], c11[2], c11[1]]
        dst = grid_bottom if max(h, c10[2], c01[2], c11[2]) <= WATER + 1 else grid_land
        dst.append([p00, p10, p11]); dst.append([p00, p11, p01])
    print(f"ground grid: {len(grid_bottom)} bottom tris + {len(grid_land)} land tris from {len(cells)} cells")
else:
    print("ground grid: (no ground_grid.csv — fish in Brownboo to dump it)")

D = {'visual': visual, 'col_rocks': col_rocks, 'col_stilts': col_stilts, 'col_plants': col_plants,
     'col_build': col_build, 'col_perim': col_perim,
     'van_floor': van_floor, 'van_wall': van_wall, 'van_mid': van_mid, 'van_dropped': van_dropped,
     'grid_bottom': grid_bottom, 'grid_land': grid_land,
     'fishbox': fishbox, 'fishlabels': fishlabels, 'fishpoint': fishpoint, 'sign': sign_mesh,
     'nodelabels': nodelabels}
LAY = [
    ('foamouter','foam: outer shore','D.visual.foam_outer','[120,175,205]',0.6,'#adf'),
    ('foamobj','foam: interior (stilts/plants)','D.visual.foam_obj','[80,105,125]',0.5,'#7ab'),
    ('watersurf','water surface','D.visual.watersurf','[40,110,140]',0.30,'#8bd'),
    ('shore','shore ring','D.visual.shore','[95,82,60]',1,'#ccc'),
    ('pond','pond BOTTOM (s04g0117__s1)','D.visual.pond','[70,150,200]',1,'#4bd'),
    ('board','boardwalk deck','D.visual.boardwalk','[70,85,110]',1,'#ccc'),
    ('stilts','stilts (above water)','D.visual.stilts','[235,120,60]',1,'#f95'),
    ('stiltsbase','stilts (underwater)','D.visual.stilts_base','[110,70,45]',0.7,'#c85'),
    ('rock','rock','D.visual.rock','[125,98,72]',1,'#ccc'),
    ('fence','fence','D.visual.fence','[75,75,62]',1,'#ccc'),
    ('houses','houses','D.visual.houses','[95,72,52]',1,'#c96'),
    ('doors','doors (obj23/25/27/31)','D.visual.doors','[200,140,70]',1,'#e94'),
    ('windows','windows (obj24/26/28/32)','D.visual.windows','[110,190,220]',1,'#7ce'),
    ('ladders','ladders','D.visual.ladders','[150,140,175]',1,'#bbf'),
    ('plants','plants','D.visual.plants','[110,160,90]',1,'#ad6'),
    ('crater','crater (full)','D.visual.crater','[58,54,50]',1,'#ccc'),
    ('entrance','entrance ramp (enter__s)','D.visual.entrance','[180,175,90]',1,'#dd8'),
    ('sign','FISHING SIGN mesh','D.sign','[235,205,120]',1,'#eca'),
    ('fishpoint','trigger ! + radius','D.fishpoint','[255,175,210]',0.95,'#fbd'),
    ('crock','COLL rocks','D.col_rocks','[150,25,25]',0.9,'#a33'),
    ('cstilt','COLL stilts','D.col_stilts','[255,150,40]',0.85,'#fa4'),
    ('cplant','COLL plants','D.col_plants','[80,230,120]',0.9,'#5e8'),
    ('cbuild','COLL buildings','D.col_build','[205,120,255]',0.7,'#c8f'),
    ('cperim','COLL perimeter (inset 20)','D.col_perim','[60,210,255]',0.5,'#3df'),
    ('vfloor','VANILLA floor (KEPT) [live dump]','D.van_floor','[90,200,255]',0.8,'#5cf'),
    ('vwall','VANILLA wall (KEPT since 2026-09) [live dump]','D.van_wall','[255,90,160]',0.8,'#f6a'),
    ('vmid','VANILLA slope (KEPT) [live dump]','D.van_mid','[255,215,80]',0.8,'#fd5'),
    ('vcut','VANILLA ladder-top floor (DROPPED) [live dump]','D.van_dropped','[120,120,130]',0.7,'#999'),
    ('gridbot','ground grid: pond BOTTOM','D.grid_bottom','[40,180,190]',0.85,'#3cc'),
    ('gridland','ground grid: land','D.grid_land','[90,110,90]',0.7,'#7a7'),
]
# vanilla layers ON by default (seeing the native collision is the point); mod-collision drafts + clutter OFF
_on = ("vfloor", "vmid", "sign", "fishpoint")   # vanilla floor/slope + the sign mesh + trigger on by default
# ---- render via the shared scene viewer (tools/scene_viewer_html.py): z-buffer occlusion, layer
# toggles, node labels, cursor->world coord readout, poly picking. LAY colours are JSON list-literal
# strings (they were embedded into JS) -> parse them back to lists here. ----
def _resolve(ref):
    obj = D
    for p in ref.split('.')[1:]: obj = obj[p]
    return obj
layers = [{'key': key, 'label': lb, 'tris': _resolve(src),
           'color': json.loads(c) if isinstance(c, str) else c, 'alpha': a, 'border': lc,
           'on': key in _on} for key, lb, src, c, a, lc in LAY]
# toggle-panel folders (scene_viewer_html folder UI)
_GROUP = {**{k: 'Water & foam' for k in ('foamouter', 'foamobj', 'watersurf')},
          **{k: 'Custom collision drafts' for k in ('crock', 'cstilt', 'cplant', 'cbuild', 'cperim')},
          **{k: 'Vanilla collision' for k in ('vfloor', 'vwall', 'vmid', 'vcut')},
          **{k: 'Ground grid dump' for k in ('gridbot', 'gridland')},
          **{k: 'Fishing spot' for k in ('sign', 'fishpoint')}}
for L in layers:
    L['group'] = _GROUP.get(L['key'], 'Scene meshes')
# LOD comparison toggles (shared helper). Scanned: s04 ships NO _1/_2 variant meshes (only the five
# e-towns carry LOD chains), so this is empty today — the folder auto-appears if the data ever has them.
from georama_parts import lod_layers
layers += lod_layers('gedit/s04/scene.scn', r's04[a-z]\d')

# ---- NATIVE ground collision variants: s04g01_a = PLAYER collision, s04g01_v = CAMERA collision.
#      Verified 2026-08 via LoadMapObject @0x19B790: the loader resolves variants by TABLE SLOT, not name —
#      header +0x14 -> LoadCollisionFile -> frame +0xD0 (player), header +0x20 -> frame +0xDC (camera).
#      Every other town names the +0x20 slot `_c`; Brownboo just labels it `_v`. The camera mesh (nodes
#      v/obj56, walls to y250) is town-wide — the houses' camera hulls are baked into it, not per-part.
#      s04g01's mapinfo placement is the origin, so local == world. One toggle per node.
import scene_placed
from georama_collision import parse_coll_mdt
def _native_coll(sub_name, suf):
    directory = scene_placed.scn_directory_map(scn)
    off, size = directory[sub_name]
    sub = scn[off:off + size]
    m = next(re.finditer((re.escape(sub_name) + suf + r'\.mds\x00').encode(), sub), None)
    if not m: return []
    vo = struct.unpack_from('<I', sub, m.end() + 3)[0]
    if not (0 < vo < len(sub) and sub[vo:vo + 3] == b'MDS'): return []
    mds = off + vo; nodes, wm = scene_placed._accum(scn, mds); out = []
    for i, (nn, mo, par, mat) in enumerate(nodes):
        if mo == 0: continue
        fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c + 3] == b'MDT'), None)
        if not fo: continue
        M = wm(i)
        tris = [[list(xform(M, a)), list(xform(M, b)), list(xform(M, c))] for a, b, c in parse_coll_mdt(scn, fo)]
        if tris: out.append((nn, tris))
    return out
_nc_counts = {}
for _suf, _kind, _col, _bord in (('_a', 'player', [255, 120, 220], '#f7c'),
                                 ('_v', 'CAMERA', [80, 200, 255], '#5bf')):
    for _nn, _tris in _native_coll('s04g01', _suf):
        _key = f'nc{_suf}_{_nn}'
        layers.append({'key': _key, 'label': f's04g01{_suf} {_nn} ({len(_tris)}) [{_kind}]',
                       'tris': _tris, 'color': _col, 'alpha': 0.6, 'border': _bord, 'on': False,
                       'group': 'Native collision (_a player / _v camera)'})
        xs = [p[0] for t in _tris for p in t]; ys = [p[1] for t in _tris for p in t]; zs = [p[2] for t in _tris for p in t]
        nodelabels.append([[(min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (min(zs) + max(zs)) / 2],
                           f's04g01{_suf}:{_nn}',
                           [[min(xs), min(ys), min(zs)], [max(xs), max(ys), max(zs)]], _key])
        _nc_counts[f'{_suf}:{_nn}'] = len(_tris)
print("native collision:", _nc_counts)

# ---- FISHING GATHER replica: exactly what _LOAD_FISHING_DATA's PickUpPoly collects at the CURRENT cast
#      rect (RE'd 2026-09, tools/ghidra: _LOAD_FISHING_DATA -> PickUpPoly__11CEditGround(box = rect x/z,
#      y +-1000, flag 0) -> per part CheckBox (x/z only) -> PickUpNearPoly__13CCollisionMDT = pure per-poly
#      AABB-vs-box test, NO attribute filter; then the 4 CEditArea grids emit 2 tris per in-rect cell whose
#      code != 0x81 (the pond cells are 0x81 -> skipped, so Brownboo's gather is s04g01_a polys only). The
#      loader HANGS if the count exceeds 0x400 = 1024 (its stack buffer) — the label shows the headroom.
#      Grid term: replicated from ground_grid.csv when that dump exists, else 0 (none in Brownboo anyway).
def _fishing_gather(x1, z1, x2, z2):
    polys = []
    for _sub in scene_placed.scn_directory_map(scn):
        for _nn, _tris in _native_coll(_sub, '_a'):
            for _t in _tris:
                _xs = [q[0] for q in _t]; _zs = [q[2] for q in _t]
                if max(_xs) < x1 or min(_xs) > x2 or max(_zs) < z1 or min(_zs) > z2: continue
                polys.append(_t)
    grid = 0
    if os.path.exists(GRID_CSV):      # cells whose origin sits in the rect (+1 cell margin), code != 0x81
        with open(GRID_CSV) as fh:
            for r in _csv.DictReader(fh):
                wx, wz, h, code = float(r['worldX']), float(r['worldZ']), float(r['height']), int(r['code'])
                if code == 0x81 or wx < x1 - 20 or wx > x2 + 20 or wz < z1 - 20 or wz > z2 + 20: continue
                grid += 2
    return polys, grid
_gath, _gath_grid = _fishing_gather(RECT_X1, RECT_Z1, RECT_X2, RECT_Z2)
_gath_n = len(_gath) + _gath_grid
layers.append({'key': 'fishgather',
               'label': f'FISHING GATHER replica @ rect ({RECT_X1},{RECT_Z1})-({RECT_X2},{RECT_Z2}): '
                        f'{_gath_n} of 1024 cap ({len(_gath)} s04g01_a polys'
                        + (f' + {_gath_grid} grid tris' if _gath_grid else '') + ')',
               'tris': _gath, 'color': [255, 230, 90], 'alpha': 0.75, 'border': '#fe7', 'on': True,
               'group': 'Fishing gather'})
print(f"fishing gather replica: {_gath_n} polys of 1024 ({len(_gath)} from s04g01_a, {_gath_grid} grid)"
      + ("  (grid term needs ground_grid.csv — absent)" if not os.path.exists(GRID_CSV) else ""))

# ---- CUSTOM camera collision: our authored version of s04g01_v obj56 (vanilla − removals + additions,
#      tools/brownboo_camera_collision.py — the future ISO bake reads the same module). ----
from brownboo_camera_collision import custom_obj56_tris, custom_h01_v_nodes
_cus = custom_obj56_tris()
layers.append({'key': 'nc_custom56', 'label': f'CUSTOM obj56 ({len(_cus)}) [CAMERA]',
               'tris': _cus, 'color': [120, 255, 140], 'alpha': 0.7, 'border': '#6f8', 'on': False,
               'group': 'Native collision (_a player / _v camera)'})
print(f"custom obj56: {len(_cus)} tris")

# ---- IWA01 tunnel-rock hull: REPLACES the lumpy obj56 iwa01 selection with a rock-centred circle wall +
#      native tunnel interior + entrance collars extended along their own taper to meet the circle. ----
from brownboo_camera_collision import iwa01_ring_tris, iwa01_ring_obj56, custom_obj56_full
_ibuild = iwa01_ring_tris()
_iobj56 = iwa01_ring_obj56()
_ifull  = custom_obj56_full()
# THE SHIPPED obj56: vanilla obj56 with ONLY the iwa01 rock replaced by the CSG hull (central cylinder
# left VANILLA) — exactly what iwa01_ring_obj56 bakes into s04g01_v. Default ON so this is what you verify.
layers.append({'key': 'nc_iwa01', 'label': f'BAKED obj56 ({len(_iobj56)}: vanilla cylinders + rock hull) [CAMERA]',
               'tris': _iobj56, 'color': [120, 255, 200], 'alpha': 0.7, 'border': '#6fc', 'on': True,
               'group': 'Native collision (_a player / _v camera)'})
# reference only: obj56 with BOTH edits (adds the central-cylinder simplification — the gap-at-bottom). NOT shipped.
layers.append({'key': 'nc_obj56_full', 'label': f'obj56 both-edits ref ({len(_ifull)}: +cylinder simplify) NOT shipped',
               'tris': _ifull, 'color': [120, 200, 255], 'alpha': 0.6, 'border': '#6cf', 'on': False,
               'group': 'Native collision (_a player / _v camera)'})
layers.append({'key': 'nc_iwa01_build', 'label': f'IWA01 hull only ({len(_ibuild)}: CSG cylinder-tunnel)',
               'tris': _ibuild, 'color': [255, 120, 255], 'alpha': 0.8, 'border': '#f7f', 'on': False,
               'group': 'Native collision (_a player / _v camera)'})
print(f"obj56 SHIPPED (vanilla cyls + rock hull)={len(_iobj56)}  both-edits ref={len(_ifull)}  hull build={len(_ibuild)} tris")


# ---- AUTHORED s04h01_v: the building's full visual mesh as camera collision, kd-split into <=100-tri
#      nodes (part-LOCAL in the module; shown here world-placed at instance #0's mapinfo placement — a
#      baked `_v` applies to every instance automatically). One toggle per node for isolate/select work. ----
_h01_place = next(((pos, rot) for name, pos, rot in placements if name == 's04h01'), None)
if _h01_place:
    _pos, _rot = _h01_place
    for _nn, _ltris in custom_h01_v_nodes():
        _flat = [p for t in _ltris for p in t]
        _pl = placeY(_flat, _pos, _rot[1])
        _wtris = [_pl[k:k + 3] for k in range(0, len(_pl), 3)]
        layers.append({'key': f'hv_{_nn}', 'label': f's04h01_v {_nn} ({len(_wtris)})',
                       'tris': _wtris, 'color': [255, 200, 90], 'alpha': 0.7, 'border': '#fc5', 'on': False,
                       'group': 'Custom s04h01_v (authoring, inst #0)'})
        _cen, _bb = _bbox_centroid(_wtris)
        nodelabels.append([_cen, _nn, _bb, f'hv_{_nn}'])
    print(f"s04h01_v authoring: {sum(len(l['tris']) for l in layers if l['key'].startswith('hv_'))} tris "
          f"in {sum(1 for l in layers if l['key'].startswith('hv_'))} nodes @ inst #0 {_pos}")
html = build_html(
    title="Brownboo COMPLETE",
    layers=layers, node_labels=nodelabels, points=fishbox, point_labels=fishlabels,
    points_label="fishing box + coords", coord_note="water y=0", points_on=False)
open(os.path.join(OUT, HTML_NAME), 'w').write(html)
print("visual:", {k: len(v) for k, v in visual.items()})
