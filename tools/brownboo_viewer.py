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

HERE = os.path.dirname(os.path.abspath(__file__))
# The generated viewer embeds game scene geometry -> untracked game_data/reference/ (never committed).
OUT = os.path.join(HERE, "..", "game_data", "reference")
os.makedirs(OUT, exist_ok=True)
HTML_NAME = "brownboo_viewer.html"

scn = load_scene('gedit/s04/scene.scn')
GOT = extract_mesh(scn)

def T(pred):
    out = []
    for name, (v, ts) in GOT.items():
        if pred(name):
            for a, b, c in ts: out.append([list(v[a]), list(v[b]), list(v[c])])
    return out

craterids = [f's04g01{n:02d}' for n in range(2, 17)] + ['s040101']
visual = {
    'watersurf':   T(lambda n: 'czapp' in n),
    'shore':       T(lambda n: n.startswith('s04g0117')),
    'boardwalk':   T(lambda n: n.startswith('s04g03')),
    'stilts':      T(lambda n: n.startswith('s04g0401') or n in ('s04g402', 's04g403', 's04g404')),
    'rock':        T(lambda n: n.startswith('iwa')),
    'fence':       T(lambda n: n.startswith('st0')),
    'crater':      T(lambda n: any(n.startswith(c) for c in craterids)),
}

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
houses, ladders, plants = [], [], []
used = {}
for name, pos, rot in placements:
    if name.startswith('s04g') or name.startswith('s04w'): continue
    idx = used.get(name, 0); used[name] = idx+1
    ents = scndir.get(name, [])
    if not ents: continue
    off, size = ents[min(idx, len(ents)-1)]
    # houses embed a reusable door template (obj*) at local origin — exclude it (it isn't there in-game)
    skip = (lambda n: n.startswith('obj')) if name.startswith('s04h') else None
    v, t = subfile_mesh(off, size, skip)
    if not v: continue
    pv = placeY(v, pos, rot[1]); tris = [[pv[a], pv[b], pv[c]] for a, b, c in t]
    (houses if name.startswith('s04h') else ladders if name.startswith('s04r') else plants).extend(tris)
visual['houses'] = houses; visual['ladders'] = ladders; visual['plants'] = plants

# ---- split the water-edge foam (za01) into outer-shore vs interior (stilt/plant rings) ----
# The foam splits cleanly BY NODE, no heuristic needed: s04w02__za01 IS the continuous outer-shore ring
# (verified: all 128 of its tris lie in the ~16-wide shoreline band), while s04w01__za01 is the stilt/
# plant foam (the sunburst rings around the boardwalk posts + the plant rings, 256 tris). Earlier
# proximity rules kept nicking real ring tris that pass near an edge-stilt; the node split never does.
visual['foam_outer'] = T(lambda n: n.startswith('s04w02') and 'za01' in n)   # the shore ring
visual['foam_obj']   = T(lambda n: n.startswith('s04w01') and 'za01' in n)   # stilt/plant foam
print("foam split:", len(visual['foam_outer']), "outer-shore ring +", len(visual['foam_obj']), "stilt/plant")

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
def _load_obj_tris(path):
    """Read triangles from a (hand-simplified, Blender) OBJ — the same meshes the mod loads. Faces may be
    tris/quads/ngons (fan-triangulated); only vertex indices are used."""
    vs = []; out = []
    for line in open(path):
        if line.startswith('v '):
            p = line.split(); vs.append([float(p[1]), float(p[2]), float(p[3])])
        elif line.startswith('f '):
            idx = []
            for tok in line.split()[1:]:
                vi = tok.split('/')[0]
                if vi: n = int(vi); idx.append(n-1 if n > 0 else len(vs)+n)
            for k in range(1, len(idx)-1):
                out.append([vs[idx[0]], vs[idx[k]], vs[idx[k+1]]])
    return out

# rock collision: prefer each rock's hand-simplified Blender mesh (tools/rock_obj/<rock>_simple.obj) —
# exactly what the mod loads via assemble_rock_collision.py — and fall back to the smooth lathe for rocks
# not yet done in Blender, so the viewer always matches the shipped .bin.
_ROCK_OBJ = os.path.join(HERE, 'rock_obj')
col_rocks = []
_rock_src = []
for _nm, (_v, _ts) in GOT.items():
    if not _nm.startswith('iwa'): continue
    _base = _nm.split('__')[0]
    _simple = os.path.join(_ROCK_OBJ, _base + '_simple.obj')
    _rt = [[_v[a], _v[b], _v[c]] for a, b, c in _ts]
    if os.path.exists(_simple):
        _r = _load_obj_tris(_simple); _src = 'blender'
    elif _base == 'iwa03':
        _r = build_rock_smooth(_rt, SMALL_CELL, WATER, YCAP, YBOTTOM, SMALL_SIDES, SMALL_LEVELS, SMALL_MARGIN, SMALL_LIFT); _src = 'lathe'
    else:
        _r = build_rock_smooth(_rt, ROCK_CELL, WATER, YCAP, YBOTTOM, ROCK_SIDES, ROCK_LEVELS, ROCK_MARGIN, ROCK_LIFT); _src = 'lathe'
    col_rocks += _r; _rock_src.append(f'{_base}={len(_r)}({_src})')
_flat = _min_face_flatness(col_rocks, WATER)
print(f"[rock collision] {len(col_rocks)} tris  [{', '.join(_rock_src)}]  min|ny|/l(above water)={_flat:.3f}")
col_stilts = clip_group(T(lambda n: n.startswith('s04g0401') or n in ('s04g402', 's04g403', 's04g404')))  # -9..44
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
RECT_X1, RECT_Z1, RECT_X2, RECT_Z2 = -320, -260, 310, 300
FISH_DEPTH = 0 - 6      # WaterLevel 0 - fishDepth 6
BOX_TOP = 54            # TODO: set to the real bobber/hook collision height
def frange(a, b, step):
    out = []; v = a
    while v <= b + 1e-6: out.append(round(v, 3)); v += step
    return out
fishbox = [[x, y, z] for x in frange(RECT_X1, RECT_X2, 10)
                     for y in frange(FISH_DEPTH, BOX_TOP, 10)
                     for z in frange(RECT_Z1, RECT_Z2, 10)]
# coordinate labels (shown with the fishing box): origin, the ! marker, and the 4 rect corners (at top)
fishlabels = [[[0, 0, 0], "0,0"], [[74, 10, -20], "74,10,-20 (!)"]]
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
fishpoint = uv_sphere(74, 10, -20, 10)   # trigger (74,10,-20), InteractRadius 10

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
# The mod drops ALL vertical walls wholesale (floors-only experiment), so the only remaining reclaim is
# the FLOOR platforms sitting on top of the in-water ladders — floors the bobber/hook would otherwise
# catch on. Near a ladder AND entirely above LAD_Y (pond floor near a ladder base is low-Y, so it stays).
# Mirrors the mod's IsLadderTopFloor (LADDER positions / radius / height must match CustomFishingSpot.cs).
LAD_POS = [(p[1][0], p[1][2]) for p in placements if p[0].startswith('s04r')]
LAD_R, LAD_Y = 45, 25   # top platforms lean out up to ~42u from the base position
def van_cut(cx, cy, cz, miny):
    return miny >= LAD_Y and any(math.hypot(cx-lx, cz-lz) < LAD_R for lx, lz in LAD_POS)
van_floor, van_wall, van_mid, van_dropped = [], [], [], []
CPOLY_CSV = os.path.join(HERE, 'vanilla_cpoly.csv')
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
GRID_CSV = os.path.join(HERE, 'ground_grid.csv')
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
     'fishbox': fishbox, 'fishlabels': fishlabels, 'fishpoint': fishpoint}
js = json.dumps(D, separators=(',', ':'))   # embedded directly in the self-contained HTML
LAY = [
    ('foamouter','foam: outer shore','D.visual.foam_outer','[120,175,205]',0.6,'#adf'),
    ('foamobj','foam: interior (stilts/plants)','D.visual.foam_obj','[80,105,125]',0.5,'#7ab'),
    ('watersurf','water surface','D.visual.watersurf','[40,110,140]',0.30,'#8bd'),
    ('shore','shore ring','D.visual.shore','[95,82,60]',1,'#ccc'),
    ('board','boardwalk','D.visual.boardwalk','[70,85,110]',1,'#ccc'),
    ('stilts','stilts','D.visual.stilts','[55,65,80]',1,'#ccc'),
    ('rock','rock','D.visual.rock','[125,98,72]',1,'#ccc'),
    ('fence','fence','D.visual.fence','[75,75,62]',1,'#ccc'),
    ('houses','houses','D.visual.houses','[95,72,52]',1,'#c96'),
    ('ladders','ladders','D.visual.ladders','[150,140,175]',1,'#bbf'),
    ('plants','plants','D.visual.plants','[110,160,90]',1,'#ad6'),
    ('crater','crater (full)','D.visual.crater','[58,54,50]',1,'#ccc'),
    ('fishpoint','! marker + radius','D.fishpoint','[255,175,210]',0.95,'#fbd'),
    ('crock','COLL rocks','D.col_rocks','[150,25,25]',0.9,'#a33'),
    ('cstilt','COLL stilts','D.col_stilts','[255,150,40]',0.85,'#fa4'),
    ('cplant','COLL plants','D.col_plants','[80,230,120]',0.9,'#5e8'),
    ('cbuild','COLL buildings','D.col_build','[205,120,255]',0.7,'#c8f'),
    ('cperim','COLL perimeter (inset 20)','D.col_perim','[60,210,255]',0.5,'#3df'),
    ('vfloor','VANILLA floor (KEPT)','D.van_floor','[90,200,255]',0.8,'#5cf'),
    ('vwall','VANILLA wall (DROPPED)','D.van_wall','[255,90,160]',0.8,'#f6a'),
    ('vmid','VANILLA slope (KEPT)','D.van_mid','[255,215,80]',0.8,'#fd5'),
    ('vcut','VANILLA ladder-top floor (DROPPED)','D.van_dropped','[120,120,130]',0.7,'#999'),
    ('gridbot','ground grid: pond BOTTOM','D.grid_bottom','[40,180,190]',0.85,'#3cc'),
    ('gridland','ground grid: land','D.grid_land','[90,110,90]',0.7,'#7a7'),
]
# vanilla layers ON by default (seeing the native collision is the point); mod-collision drafts + clutter OFF
_on = ("vfloor", "vmid")   # only the vanilla floor + slope layers on by default; everything else off
def layer_count(src):
    obj = D
    for p in src.split('.')[1:]: obj = obj[p]
    return len(obj)
_cnt = {f"t_{i}": layer_count(src) for i, lb, src, c, a, lc in LAY}   # poly (triangle) count per layer
checks = "".join(
    f'<label><input type=checkbox id=t_{i} {"checked" if i in _on else ""}> '
    f'<span style="color:{lc}">{lb}</span> <span style="color:#777">({_cnt[f"t_{i}"]})</span></label><br>'
    for i, lb, src, c, a, lc in LAY)
# fishrect is special: a hot-pink 3D point-grid box + coordinate labels (POINTS, not polys), handled in
# draw() not as a layer — shown with a point count and excluded from the poly total.
checks += (f'<label><input type=checkbox id=t_fishrect> <span style="color:#f4a">fishing box + coords</span> '
           f'<span style="color:#777">({len(D["fishbox"])} pts)</span></label><br>')
# overlay (not a layer): recolour any VISIBLE poly the bobber can't land on (|normal.Y| <= 0.2, too steep)
# that also sits above the water — the pass-through spots — bright pink, whatever layer it belongs to.
checks += '<label><input type=checkbox id=t_steep> <span style="color:#ff2db4">steep &amp; above-water (highlight)</span></label><br>'
checks += '<div style="margin-top:5px;border-top:1px solid #444;padding-top:4px">selected: <b id="tot" style="color:#fff">0</b> polys</div>'
cnt_js = json.dumps(_cnt, separators=(',', ':'))
pushes = "".join(f"if(on('t_{i}')&&{src}) L.push({{t:{src},c:{c},a:{a}}});\n" for i, lb, src, c, a, lc in LAY)
html = '''<div style="margin:0;background:#0d1117;color:#ddd;font-family:monospace;overflow:hidden">
<canvas id="c" style="display:block;cursor:grab;touch-action:none"></canvas>
<div style="position:fixed;top:8px;left:8px;font-size:11px;line-height:1.5;background:rgba(13,17,23,.85);padding:8px 10px;border-radius:6px;user-select:none">
<b>Brownboo COMPLETE</b><br><span style="color:#888">drag=rotate scroll=zoom &middot; compass: N=-Z E=+X</span><br>
CHECKS<div id="err" style="color:#f66"></div></div></div>
<script>try{
const D=JSON_DATA;
const CNT=CNT_DATA;
function updateTotal(){let s=0;for(const k in CNT){const e=document.getElementById(k);if(e&&e.checked)s+=CNT[k];}const t=document.getElementById('tot');if(t)t.textContent=s;}
const cv=document.getElementById('c'),cx=cv.getContext('2d');
let W,H;function resize(){W=cv.width=innerWidth||900;H=cv.height=innerHeight||700;}resize();addEventListener('resize',()=>{resize();draw();});
let yaw=0.6,pitch=0.55,zoom=1.5;   // +pitch = camera ABOVE the model, looking down
const WATER=0, STEEP_NY=0.2;   // bobber lands on |normal.Y|>0.2; <=0.2 is too steep to collide
const on=id=>document.getElementById(id).checked;
function layers(){const L=[];
PUSHES
return L;}
function rot(p){let x=p[0],y=p[1],z=p[2];let cy=Math.cos(yaw),sy=Math.sin(yaw);let x1=x*cy-z*sy,z1=x*sy+z*cy;
 let cp=Math.cos(pitch),sp=Math.sin(pitch);let y1=y*cp-z1*sp,z2=y*sp+z1*cp;return [x1,y1,z2];}
function draw(){
 cx.fillStyle='#0d1117';cx.fillRect(0,0,W,H);
 const f=Math.min(W,H)*0.5*zoom/300, all=[];
 const steep=on('t_steep');
 for(const L of layers()){ if(!L.t) continue; for(const tri of L.t){
   const r=[rot(tri[0]),rot(tri[1]),rot(tri[2])];
   const nz=(r[1][0]-r[0][0])*(r[2][1]-r[0][1])-(r[1][1]-r[0][1])*(r[2][0]-r[0][0]);
   const nx=(r[1][1]-r[0][1])*(r[2][2]-r[0][2])-(r[1][2]-r[0][2])*(r[2][1]-r[0][1]);
   const ny=(r[1][2]-r[0][2])*(r[2][0]-r[0][0])-(r[1][0]-r[0][0])*(r[2][2]-r[0][2]);
   const nlen=Math.hypot(nx,ny,nz)||1;
   let c=L.c,a=L.a;
   if(steep){
     // WORLD-space normal (not the rotated one) decides slope; centroid decides above-water
     const wnx=(tri[1][1]-tri[0][1])*(tri[2][2]-tri[0][2])-(tri[1][2]-tri[0][2])*(tri[2][1]-tri[0][1]);
     const wny=(tri[1][2]-tri[0][2])*(tri[2][0]-tri[0][0])-(tri[1][0]-tri[0][0])*(tri[2][2]-tri[0][2]);
     const wnz=(tri[1][0]-tri[0][0])*(tri[2][1]-tri[0][1])-(tri[1][1]-tri[0][1])*(tri[2][0]-tri[0][0]);
     const wl=Math.hypot(wnx,wny,wnz)||1, cy=(tri[0][1]+tri[1][1]+tri[2][1])/3;
     if(Math.abs(wny)/wl<=STEEP_NY && cy>WATER){ c=[255,45,180]; a=1; }
   }
   all.push({k:'t',r,c,a,depth:(r[0][2]+r[1][2]+r[2][2])/3,sh:0.4+0.6*Math.abs(nz/nlen)});
 }}
 if(on('t_fishrect')) for(const p of D.fishbox){const r=rot(p);all.push({k:'p',r,depth:r[2]});}
 all.sort((p,q)=>p.depth-q.depth);
 for(const o of all){
  if(o.k==='p'){cx.fillStyle='#ff1493';cx.fillRect(W/2+o.r[0]*f-1.4,H/2-o.r[1]*f-1.4,2.8,2.8);continue;}
  const pts=o.r.map(p=>[W/2+p[0]*f,H/2-p[1]*f]);
  cx.beginPath();cx.moveTo(pts[0][0],pts[0][1]);cx.lineTo(pts[1][0],pts[1][1]);cx.lineTo(pts[2][0],pts[2][1]);cx.closePath();
  const c=o.c;cx.fillStyle='rgba('+(c[0]*o.sh|0)+','+(c[1]*o.sh|0)+','+(c[2]*o.sh|0)+','+o.a+')';cx.fill();
 }
 if(on('t_fishrect')) drawLabels();
 drawCompass();
}
function drawLabels(){
 const f=Math.min(W,H)*0.5*zoom/300;
 cx.save();cx.font='bold 12px monospace';cx.textAlign='left';cx.textBaseline='middle';
 for(const lb of D.fishlabels){const r=rot(lb[0]);const x=W/2+r[0]*f,y=H/2-r[1]*f;
  cx.fillStyle='#ff1493';cx.beginPath();cx.arc(x,y,3,0,7);cx.fill();
  const w=cx.measureText(lb[1]).width;cx.fillStyle='rgba(0,0,0,.75)';cx.fillRect(x+4,y-7,w+5,14);
  cx.fillStyle='#ff9fd6';cx.fillText(lb[1],x+6,y);}
 cx.restore();
}
// Compass overlay: world convention N=-Z, S=+Z, E=+X, W=-X. Rotates with the view so you can call
// out headings. N is red + thicker.
function drawCompass(){
 const cxp=W-80,cyp=58,CR=34;
 cx.save();cx.font='bold 13px monospace';cx.textAlign='center';cx.textBaseline='middle';cx.lineWidth=1;
 cx.fillStyle='rgba(13,17,23,.7)';cx.beginPath();cx.arc(cxp,cyp,CR+13,0,7);cx.fill();
 const dirs=[['N',[0,0,-1],'#f66'],['E',[1,0,0],'#9cf'],['S',[0,0,1],'#999'],['W',[-1,0,0],'#9cf']];
 for(const d of dirs){
  const r=rot(d[1]);let sx=r[0],sy=-r[1];const L2=Math.hypot(sx,sy)||1;sx/=L2;sy/=L2;
  cx.strokeStyle=d[2];cx.lineWidth=d[0]=='N'?2.5:1.3;
  cx.beginPath();cx.moveTo(cxp,cyp);cx.lineTo(cxp+sx*CR,cyp+sy*CR);cx.stroke();
  cx.fillStyle=d[2];cx.fillText(d[0],cxp+sx*(CR+9),cyp+sy*(CR+9));
 }
 cx.restore();
}
draw();
let drag=false,px,py;
cv.addEventListener('pointerdown',e=>{drag=true;px=e.clientX;py=e.clientY;cv.style.cursor='grabbing';});
addEventListener('pointerup',()=>{drag=false;cv.style.cursor='grab';});
addEventListener('pointermove',e=>{if(!drag)return;yaw+=(e.clientX-px)*.01;pitch+=(e.clientY-py)*.01;px=e.clientX;py=e.clientY;draw();});
cv.addEventListener('wheel',e=>{e.preventDefault();zoom*=e.deltaY<0?1.1:0.9;draw();},{passive:false});
for(const cb of document.querySelectorAll('input')) cb.addEventListener('change',()=>{draw();updateTotal();});
updateTotal();
}catch(e){document.getElementById('err').textContent='ERR: '+e.message;}</script>'''
open(os.path.join(OUT, HTML_NAME), 'w').write(
    html.replace('CHECKS', checks).replace('PUSHES', pushes).replace('JSON_DATA', js).replace('CNT_DATA', cnt_js))
print("visual:", {k: len(v) for k, v in visual.items()})
