#!/usr/bin/env python3
"""Yellow Drops WEST BANK bulge + fish walls (the part of the pond work that SHIPPED).

Bulge the west bank edge (x~-424..-388, z -142..275, user-marked) westward for camera room behind
a player standing at the water's edge: edge columns move -x by WEST_BULGE*sin(pi*s) along the
section; paired crown columns follow; section ends stay welded. WB_SUBDIV inserts stations for a
smoother waterline (2x density). Consumers:
  tools/bake_yellowdrops_westbank.py         (bakes game_data/yellowdrops/yellowdrops_westbank_ground.bin)
  tools/build_fishing_collision.py      (DCFC fish walls -> yellowdrops_23.bin)
  tools/yellowdrops_viewer.py           (proposal/verification layers)

(The crescent-pond redesign that used to live in this file — steps 1-10 — was REVERTED by user
decision and deleted 2026-09; recoverable via git as tools/yellowdrops_pond.py.)
"""
import math, re, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from scene_placed import placed_meshes

# ---- west bank bulge: more land behind the player standing at the water's edge ----------------
# Bulge the west bank edge (x~-424..-388, z -142..275, user-marked) westward: edge columns move
# -x by WEST_BULGE*sin(pi*s) along the section; paired crown columns follow; section ends stay
# welded. Spans the grid10/grid11 node seam at z~-23 — column-keyed moves keep it closed.
WEST_BULGE = 70.0
_WB_STATIONS = [           # (edge column xz, paired crown column xz); ends not listed = fixed
    ((-424.0, -23.0), (-417.0, -24.0)),
    ((-413.0, 102.0), (-404.0, 103.0)),
    ((-388.0, 206.0), (-378.0, 206.0)),
]
WB_SPAN = (-145.0, 274.0)


def wb_moves():
    """{(x,z) column: dx} — the west-bank bulge moves, reusable on visual AND collision meshes."""
    z0, z1 = WB_SPAN
    moves = {}
    for edge, crown in _WB_STATIONS:
        dxw = -WEST_BULGE * math.sin(math.pi * (edge[1] - z0) / (z1 - z0))
        moves[edge] = dxw
        moves[crown] = dxw
    return moves


FISH_WALL_BOTTOM = -24.0     # fish swim at WaterLevel-8 = y-7; walls run bank-top down to here


# P3/P4 pillar-base fish collision: the pillars' base collision tris VERBATIM (user-extracted),
# re-appended via the DCFC bin. Still NEEDED after native walls stopped being stripped (2026-09):
# checked offline, the native s13 `_a` in the bank band only exists at y 30..126 (the crown wall above
# the bank top) — nothing native reaches into the water where the fish are (y ~ -4), and the pillar
# bases have no `_a` duplicate at all.
PILLAR_BASE_TRIS = [
    [[-561.8, -20.64, 51.68], [-581.76, -20.64, 53.74], [-565.42, -3.28, 59.78]],
    [[-565.42, -3.28, 59.78], [-576.57, -3.28, 60.93], [-561.84, 13.44, 56.8]],
    [[-565.42, -3.28, 59.78], [-581.76, -20.64, 53.74], [-576.57, -3.28, 60.93]],
    [[-561.84, 13.44, 56.8], [-576.57, -3.28, 60.93], [-574.64, 13.44, 58.12]],
    [[-581.14, -3.28, 71.15], [-578.2, -20.64, 88.32], [-574.58, -3.28, 80.22]],
    [[-579.9, 13.44, 69.87], [-574.58, -3.28, 80.22], [-572.36, 13.44, 80.3]],
    [[-576.57, -3.28, 60.93], [-581.14, -3.28, 71.15], [-574.64, 13.44, 58.12]],
    [[-589.96, -20.64, 72.06], [-578.2, -20.64, 88.32], [-581.14, -3.28, 71.15]],
    [[-581.14, -3.28, 71.15], [-574.58, -3.28, 80.22], [-579.9, 13.44, 69.87]],
    [[-581.76, -20.64, 53.74], [-589.96, -20.64, 72.06], [-576.57, -3.28, 60.93]],
    [[-576.57, -3.28, 60.93], [-589.96, -20.64, 72.06], [-581.14, -3.28, 71.15]],
    [[-574.64, 13.44, 58.12], [-581.14, -3.28, 71.15], [-579.9, 13.44, 69.87]],
    [[-578.2, -20.64, 88.32], [-558.24, -20.64, 86.26], [-574.58, -3.28, 80.22]],
    [[-574.58, -3.28, 80.22], [-558.24, -20.64, 86.26], [-563.43, -3.28, 79.07]],
    [[-558.86, -3.28, 68.85], [-561.8, -20.64, 51.68], [-565.42, -3.28, 59.78]],
    [[-574.58, -3.28, 80.22], [-563.43, -3.28, 79.07], [-572.36, 13.44, 80.3]],
    [[-550.04, -20.64, 67.94], [-561.8, -20.64, 51.68], [-558.86, -3.28, 68.85]],
    [[-558.24, -20.64, 86.26], [-550.04, -20.64, 67.94], [-563.43, -3.28, 79.07]],
    [[-563.43, -3.28, 79.07], [-550.04, -20.64, 67.94], [-558.86, -3.28, 68.85]],
    [[-572.36, 13.44, 80.3], [-563.43, -3.28, 79.07], [-559.55, 13.44, 78.98]],
    [[-558.86, -3.28, 68.85], [-565.42, -3.28, 59.78], [-554.29, 13.44, 67.23]],
    [[-563.43, -3.28, 79.07], [-558.86, -3.28, 68.85], [-559.55, 13.44, 78.98]],
    [[-554.29, 13.44, 67.23], [-565.42, -3.28, 59.78], [-561.84, 13.44, 56.8]],
    [[-559.55, 13.44, 78.98], [-558.86, -3.28, 68.85], [-554.29, 13.44, 67.23]],
    [[-563.31, -5.96, 160.04], [-557.98, -20.54, 147.26], [-566.17, -5.96, 150.48]],
    [[-552.96, -20.54, 164.04], [-557.98, -20.54, 147.26], [-563.31, -5.96, 160.04]],
    [[-563.31, -5.96, 160.04], [-566.17, -5.96, 150.48], [-560.84, 8.09, 161.2]],
    [[-579.87, -5.96, 165.0], [-570.15, -5.96, 167.3], [-580.32, 8.09, 167.03]],
    [[-582.02, -20.54, 172.74], [-564.98, -20.54, 176.78], [-579.87, -5.96, 165.0]],
    [[-570.15, -5.96, 167.3], [-552.96, -20.54, 164.04], [-563.31, -5.96, 160.04]],
    [[-579.87, -5.96, 165.0], [-564.98, -20.54, 176.78], [-570.15, -5.96, 167.3]],
    [[-570.15, -5.96, 167.3], [-563.31, -5.96, 160.04], [-568.9, 8.09, 169.74]],
    [[-564.98, -20.54, 176.78], [-552.96, -20.54, 164.04], [-570.15, -5.96, 167.3]],
    [[-560.84, 8.09, 161.2], [-566.17, -5.96, 150.48], [-564.2, 8.09, 149.95]],
    [[-568.9, 8.09, 169.74], [-563.31, -5.96, 160.04], [-560.84, 8.09, 161.2]],
    [[-580.32, 8.09, 167.03], [-570.15, -5.96, 167.3], [-568.9, 8.09, 169.74]],
    [[-557.98, -20.54, 147.26], [-575.02, -20.54, 143.22], [-566.17, -5.96, 150.48]],
    [[-566.17, -5.96, 150.48], [-575.02, -20.54, 143.22], [-575.88, -5.96, 148.18]],
    [[-575.02, -20.54, 143.22], [-587.04, -20.54, 155.96], [-575.88, -5.96, 148.18]],
    [[-582.73, -5.96, 155.44], [-582.02, -20.54, 172.74], [-579.87, -5.96, 165.0]],
    [[-587.04, -20.54, 155.96], [-582.02, -20.54, 172.74], [-582.73, -5.96, 155.44]],
    [[-575.88, -5.96, 148.18], [-587.04, -20.54, 155.96], [-582.73, -5.96, 155.44]],
    [[-575.88, -5.96, 148.18], [-582.73, -5.96, 155.44], [-575.63, 8.09, 147.24]],
    [[-582.73, -5.96, 155.44], [-579.87, -5.96, 165.0], [-583.69, 8.09, 155.78]],
    [[-583.69, 8.09, 155.78], [-579.87, -5.96, 165.0], [-580.32, 8.09, 167.03]],
    [[-575.63, 8.09, 147.24], [-582.73, -5.96, 155.44], [-583.69, 8.09, 155.78]],
    [[-564.2, 8.09, 149.95], [-575.88, -5.96, 148.18], [-575.63, 8.09, 147.24]],
    [[-566.17, -5.96, 150.48], [-575.88, -5.96, 148.18], [-564.2, 8.09, 149.95]],
]


def westbank_fish_walls():
    """Player-collision wall band along the bulged west-bank waterline, bank top down to
    FISH_WALL_BOTTOM, so fish can't swim through the visual skirts (appended to the fishing
    cpoly gather via Resources/FishingCollision/yellowdrops_23.bin — DCFC, map 23).
    Derived from wb_moves(), so it tracks WEST_BULGE."""
    chain = [(-485.0, 16.0, -197.0)]                             # existing shoreline vert NW of the section
    et = WB_ROWS['edge_top']
    for a, b in zip(et, et[1:]):                                 # stations + WB_SUBDIV midpoints,
        chain.append(list(a))                                    # all on the smooth profile
        for k in range(1, WB_SUBDIV + 1):
            t = k / (WB_SUBDIV + 1.0)
            chain.append([a[j] + (b[j] - a[j]) * t for j in range(3)])
    chain.append(list(et[-1]))
    chain = [[c[0] + (wb_profile(c[2]) if i > 0 else 0.0), c[1], c[2]] for i, c in enumerate(chain)]
    out = []
    for (ax, ay, az), (bx, by, bz) in zip(chain, chain[1:]):
        ab, bb = [ax, FISH_WALL_BOTTOM, az], [bx, FISH_WALL_BOTTOM, bz]
        out.append([[ax, ay, az], [bx, by, bz], bb])
        out.append([[ax, ay, az], bb, ab])
    out += [[list(p) for p in t] for t in PILLAR_BASE_TRIS]
    return out


WB_SUBDIV = 1                # smoothed waterline: extra stations per segment (1 = 2x density,
                             # matching the area's native poly density per user review)

# vanilla west-bank rows (world coords), NW -> SE. edge/crown/cam rows move by the bulge profile;
# the inland row is fixed. Camera wall = miti_c's own line (independent of the ground columns).
WB_ROWS = {
    'edge_top': [(-426.0,23.0,-142.0), (-424.0,23.0,-23.0), (-413.0,23.0,102.0), (-388.0,23.0,206.0), (-382.0,23.0,275.0)],
    'edge_bot': [(-427.0,-10.0,-142.0), (-424.0,-10.0,-23.0), (-413.0,-10.0,102.0), (-388.0,-10.0,206.0), (-382.0,-10.0,275.0)],
    'crown':    [(-420.0,30.0,-148.0), (-417.0,30.0,-24.0), (-404.0,30.0,103.0), (-378.0,30.0,206.0), (-369.0,30.0,273.0)],
    'inland':   [(-333.0,30.0,-134.0), (-335.0,30.0,-18.0), (-319.0,30.0,103.0), (-300.0,30.0,201.5), (-281.0,30.0,299.0)],
    'cam':      [(-441.0,30.0,-134.0), (-441.0,30.0,-22.0), (-430.0,30.0,103.0), (-405.0,30.0,208.0), (-398.0,30.0,275.0)],
}


def wb_profile(z):
    z0, z1 = WB_SPAN
    if not (z0 < z < z1):
        return 0.0
    return -WEST_BULGE * math.sin(math.pi * (z - z0) / (z1 - z0))


def _wb_row(name, bulge=True):
    """Subdivided row: WB_SUBDIV lerped stations per segment, then the bulge profile per-z."""
    row = WB_ROWS[name]
    out = []
    for a, b in zip(row, row[1:]):
        out.append(list(a))
        for k in range(1, WB_SUBDIV + 1):
            t = k / (WB_SUBDIV + 1.0)
            out.append([a[j] + (b[j] - a[j]) * t for j in range(3)])
    out.append(list(row[-1]))
    if bulge:
        for pnt in out:
            pnt[0] += wb_profile(pnt[2])
    return out


def westbank_smooth():
    """Smoothed (subdivided) west-bank proposal: {'vis','acol','ccol'} tri lists. The bake for this
    needs MDT rebuilds (vert count grows) — preview first, then extend the ISO pipeline."""
    UP = [0.0, 1.0, 0.0]
    et, eb = _wb_row('edge_top'), _wb_row('edge_bot')
    cr, inl = _wb_row('crown'), _wb_row('inland', bulge=False)
    vis = []
    vis += _zip4(et, eb, UP)                         # waterline skirt
    vis += _zip4(cr, et, UP)                         # crown -> edge band
    vis += _zip4(inl, cr, UP)                        # plateau band
    acol = []
    cr_hi = [[pnt[0], 126.0, pnt[2]] for pnt in cr]  # crown collision wall, world y30..126
    acol += _zip4(cr, cr_hi, UP)
    acol += _zip4(inl, cr, UP)                       # walkable floor
    cam = _wb_row('cam')
    cam_lo = [[pnt[0], -10.0, pnt[2]] for pnt in cam]
    ccol = []
    ccol += _zip4(cam, cam_lo, UP)                   # camera wall, y30..-10
    ccol += _zip4(inl, cam, UP)                      # camera floor (the tris that stretch with it)
    return {'vis': vis, 'acol': acol, 'ccol': ccol}


def westbank_tris(region=(-540.0, -240.0, -280.0, 420.0)):
    moves = wb_moves()
    x0, x1, zz0, zz1 = region
    out = []
    for msh in placed_meshes('gedit/s13/scene.scn', 'gedit/s13/mapinfo.cfg'):
        if re.match(r'grid(8|9|10|11)\b', msh['name']) is None:
            continue
        verts = msh['verts']
        for tr in msh['tris']:
            pts = [list(verts[i]) for i in tr]
            c = [(pts[0][k] + pts[1][k] + pts[2][k]) / 3 for k in range(3)]
            if not (x0 <= c[0] <= x1 and zz0 <= c[2] <= zz1):
                continue
            for pt in pts:
                for (kx, kz), dxw in moves.items():
                    if abs(pt[0] - kx) <= 0.5 and abs(pt[2] - kz) <= 0.5:
                        pt[0] += dxw
            out.append(pts)
    return out

# corner rounding (user-marked sharp angles): pull each E_out junction-corner COLUMN inward along
# its bisector; the spline midpoints on both adjacent edges turn the kink into a soft 3-segment
# curve. xz moves applied to every vert in the column (tops, bottoms, raw stub tris alike).


def _zip4(rowA, rowB, up):
    tris = []
    for i in range(len(rowA) - 1):
        a0, a1, b0, b1 = rowA[i], rowA[i + 1], rowB[i], rowB[i + 1]
        if sum((a0[k] - b1[k]) ** 2 for k in range(3)) <= sum((a1[k] - b0[k]) ** 2 for k in range(3)):
            quads = ([a0, a1, b1], [a0, b1, b0])
        else:
            quads = ([a0, a1, b0], [a1, b1, b0])
        for tri in quads:
            u = [tri[1][k] - tri[0][k] for k in range(3)]; v = [tri[2][k] - tri[0][k] for k in range(3)]
            n = [u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]]
            if n[0]*up[0] + n[1]*up[1] + n[2]*up[2] < 0:
                tri = [tri[0], tri[2], tri[1]]
            tris.append([list(pt) for pt in tri])
    return tris


