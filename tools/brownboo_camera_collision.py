#!/usr/bin/env python3
"""Brownboo (s04) CAMERA-collision authoring — the custom version of s04g01_v's `obj56` node.

s04g01_v is the town's CAMERA collision (LoadMapObject @0x19B790 fills the camera frame +0xDC from part-header
slot +0x20 — the slot every other town names `_c`; Brownboo labels it `_v`). Node `v` (404 tris) is the terrain
camera hull (untouched); node `obj56` (900 tris) carries the structures — including the big central cylinder —
and is what we simplify here.

Directed edits, Queens-style: `_RM` holds exact vanilla tris to REMOVE (matched by rounded-vertex key),
`_ADD` holds authored replacement tris. custom_obj56_tris() = vanilla obj56 − _RM + _ADD.
Shared by tools/brownboo_viewer.py (the "custom obj56" toggle) and the future ISO bake.

Run standalone for a summary: python3 tools/brownboo_camera_collision.py
"""
import os, re, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import scene_placed
from extract_scene_mesh import load_scene, xform, read_verts, read_tris
from georama_collision import parse_coll_mdt


def _rmkey(t):
    return tuple(sorted(tuple(round(c, 1) for c in p) for p in t))


def vanilla_v_nodes(scn=None):
    """[(node_name, tris)] of s04g01_v (world == local: s04g01's mapinfo placement is the origin).
    scn: scene.scn BYTES to decode from (e.g. read out of a patched ISO); None = the local extraction
    (needs DC1_DATA_DIR — the viewer/authoring path)."""
    if scn is None:
        scn = load_scene('gedit/s04/scene.scn')
    DIR = scene_placed._scndir(scn)
    off, size = DIR['s04g01']
    sub = scn[off:off + size]
    m = next(re.finditer(rb's04g01_v\.mds\x00', sub), None)
    if m is None:
        return []
    vo = struct.unpack_from('<I', sub, m.end() + 3)[0]
    mds = off + vo
    nodes, wm = scene_placed._accum(scn, mds)
    out = []
    for i, (nn, mo, par, mat) in enumerate(nodes):
        if mo == 0:
            continue
        fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c + 3] == b'MDT'), None)
        if not fo:
            continue
        M = wm(i)
        tris = [[list(xform(M, a)), list(xform(M, b)), list(xform(M, c))] for a, b, c in parse_coll_mdt(scn, fo)]
        if tris:
            out.append((nn, tris))
    return out


def _parse(raw):
    out = []
    for ln in raw.strip().splitlines():
        xs = [float(x) for x in ln.split(',')]
        out.append([xs[0:3], xs[3:6], xs[6:9]])
    return out


# ---- REMOVE: the big central cylinder's tall side walls (y≈7-14 -> 200, radius ≈75 ring around the
#      centre) — first pass of the simplification (user-selected 2026-08). ----
_RM = _parse("""
40.66,10,-63.62, 54.84,200,-53.71, 54.84,7,-53.71
40.66,10,-63.62, 40.66,200,-63.62, 54.84,200,-53.71
54.84,7,-53.71, 64.98,200,-42.04, 64.98,8,-42.04
54.84,7,-53.71, 54.84,200,-53.71, 64.98,200,-42.04
64.98,8,-42.04, 71.07,200,-25.48, 71.07,10,-25.48
64.98,8,-42.04, 64.98,200,-42.04, 71.07,200,-25.48
71.07,10,-25.48, 75.09,200,-7.88, 75.09,10,-7.88
71.07,10,-25.48, 71.07,200,-25.48, 75.09,200,-7.88
75.99,200,8.68, 75.09,10,-7.88, 75.09,200,-7.88
75.99,200,8.68, 75.99,8,8.68, 75.09,10,-7.88
72.37,200,24.53, 75.99,8,8.68, 75.99,200,8.68
72.37,200,24.53, 72.37,11,24.53, 75.99,8,8.68
64.23,200,39.68, 72.37,11,24.53, 72.37,200,24.53
64.23,200,39.68, 64.23,8,39.68, 72.37,11,24.53
52.98,200,53.79, 64.23,8,39.68, 64.23,200,39.68
52.98,200,53.79, 52.98,8,53.79, 64.23,8,39.68
40.69,200,65.82, 52.98,8,53.79, 52.98,200,53.79
40.69,200,65.82, 40.69,10,65.82, 52.98,8,53.79
26.03,200,72.88, 26.03,8,72.88, 40.69,10,65.82
26.03,200,72.88, 40.69,10,65.82, 40.69,200,65.82
9.02,200,74.96, 26.03,8,72.88, 26.03,200,72.88
9.02,200,74.96, 9.02,10,74.96, 26.03,8,72.88
-26.03,200,72.88, -26.03,8,72.88, -9.02,10,74.96
-26.03,200,72.88, -9.02,10,74.96, -9.02,200,74.96
-40.69,200,65.82, -40.69,10,65.82, -26.03,8,72.88
-40.69,200,65.82, -26.03,8,72.88, -26.03,200,72.88
-52.98,200,53.79, -40.69,10,65.82, -40.69,200,65.82
-52.98,200,53.79, -52.98,10,53.79, -40.69,10,65.82
-72.37,200,24.53, -72.37,9,24.53, -64.23,10,39.68
-72.37,200,24.53, -64.23,10,39.68, -64.23,200,39.68
-75.99,200,8.68, -72.37,9,24.53, -72.37,200,24.53
-75.99,200,8.68, -75.99,12,8.68, -72.37,9,24.53
-75.99,12,8.68, -75.99,200,8.68, -75.09,200,-7.88
-75.99,12,8.68, -75.09,200,-7.88, -75.09,14,-7.88
-75.09,14,-7.88, -71.07,200,-25.48, -71.07,14,-25.48
-75.09,14,-7.88, -75.09,200,-7.88, -71.07,200,-25.48
-71.07,14,-25.48, -71.07,200,-25.48, -66.02,200,-42.04
-71.07,14,-25.48, -66.02,200,-42.04, -66.02,10,-42.04
-66.02,10,-42.04, -54.84,200,-53.71, -54.84,10,-53.71
-66.02,10,-42.04, -66.02,200,-42.04, -54.84,200,-53.71
-54.84,10,-53.71, -54.84,200,-53.71, -40.66,200,-63.62
-54.84,10,-53.71, -40.66,200,-63.62, -40.66,13,-63.62
-40.66,13,-63.62, -40.66,200,-63.62, -24.39,200,-71.45
-40.66,13,-63.62, -24.39,200,-71.45, -24.39,13,-71.45
-24.39,13,-71.45, -24.39,200,-71.45, -8.13,200,-77.2
-24.39,13,-71.45, -8.13,200,-77.2, -8.13,7,-77.2
-8.13,7,-77.2, -8.13,200,-77.2, 8.13,200,-77.2
-8.13,7,-77.2, 8.13,200,-77.2, 8.13,8,-77.2
8.13,8,-77.2, 8.13,200,-77.2, 24.39,200,-71.45
8.13,8,-77.2, 24.39,200,-71.45, 24.39,10,-71.45
""")

def _tnormal(t):
    a, b, c = t
    u = [b[i] - a[i] for i in range(3)]
    v = [c[i] - a[i] for i in range(3)]
    return [u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]]


def _q(a, b, c, d, want):
    """Quad a-b-c-d -> 2 tris wound so normal·want >= 0 (camera collision is one-sided, faces the play area)."""
    out = []
    for t in ([a, b, c], [a, c, d]):
        n = _tnormal(t)
        out.append(t if n[0]*want[0] + n[1]*want[1] + n[2]*want[2] >= 0 else [t[0], t[2], t[1]])
    return [[list(p) for p in t] for t in out]


# ---- s04h01 roof-cone extension -> cylinder-above (2nd pass). The kept h01 roof band (inner octagon
#      world y42.32 r~23, outer decagon y49.5 r~50-53) is EXTENDED outward at its own per-direction slope
#      until it reaches the old cylinder ring; the cylinder is then rebuilt from that meet height UP to
#      y200. Everything the building had above the band is dropped from the h01 `_v` (H01_KEEP_BELOW), so
#      the cone+cylinder shell encloses it. All generated in WORLD space (obj56 is origin-placed).
import math

_CONE_INNER = [(8.99, 21.44), (21.58, 8.94), (21.52, -8.8), (8.86, -21.38),
               (-8.99, -21.44), (-21.58, -8.94), (-21.52, 8.8), (-8.86, 21.38)]        # world y 42.32
_CONE_OUTER = [(49.12, 20.35), (20.6, 48.53), (10.45, 48.46), (-9.85, 48.34), (-20, 48.28),
               (-48.88, 19.75), (-49.12, -20.35), (-20.6, -48.53), (20, -48.28), (48.88, -19.75)]  # y 49.5
_CONE_Y0, _CONE_Y1 = 42.32, 49.5
# The old cylinder ring (xz of the removed walls), fin detours replaced by their closing chords.
_CYL_RING = [(75.99, 8.68), (72.37, 24.53), (64.23, 39.68), (52.98, 53.79), (40.69, 65.82),
             (26.03, 72.88), (9.02, 74.96), (-9.02, 74.96), (-26.03, 72.88), (-40.69, 65.82),
             (-52.98, 53.79), (-64.23, 39.68), (-72.37, 24.53), (-75.99, 8.68), (-75.09, -7.88),
             (-71.07, -25.48), (-66.02, -42.04), (-54.84, -53.71), (-40.66, -63.62), (-24.39, -71.45),
             (-8.13, -77.2), (8.13, -77.2), (24.39, -71.45), (40.66, -63.62), (54.84, -53.71),
             (64.98, -42.04), (71.07, -25.48), (75.09, -7.88)]
_CYL_TOP = 200.0
# ring segments already walled full-height by the fin-closing chords below -> no cylinder wall there
_CHORD_SEGS = {((24.39, -71.45), (40.66, -63.62)), ((9.02, 74.96), (-9.02, 74.96)),
               ((-52.98, 53.79), (-64.23, 39.68))}


def _ray_r(poly, th):
    """Radius where the ray from the origin at angle th crosses the closed 2D polygon."""
    dx, dz = math.cos(th), math.sin(th)
    best = None
    for k in range(len(poly)):
        (x1, z1), (x2, z2) = poly[k], poly[(k + 1) % len(poly)]
        ex, ez = x2 - x1, z2 - z1
        # ray t*(dx,dz) vs segment P1 + u*(ex,ez):  t = cross(P1,E)/cross(D,E), u = cross(P1,D)/cross(D,E)
        den = dx * ez - dz * ex
        if abs(den) < 1e-9:
            continue
        t = (x1 * ez - z1 * ex) / den
        u = (x1 * dz - z1 * dx) / den
        if -1e-6 <= u <= 1 + 1e-6 and t > 0:
            best = t if best is None else min(best, t)
    return best


def _ang(x, z):
    a = math.atan2(z, x)
    return a + 2 * math.pi if a < 0 else a


def _cone_cyl_tris():
    """(annulus tris, cylinder-above tris): the roof-cone extension out to the old cylinder ring at the
    per-direction slope, plus the cylinder walls from the meet height up to y200 (chord segments skipped).
    Annulus faces UP, walls face OUTWARD (the play-area side)."""
    A = sorted(([x, _CONE_Y1, z] for x, z in _CONE_OUTER), key=lambda p: _ang(p[0], p[2]))
    B = []
    for x, z in _CYL_RING:
        th = _ang(x, z)
        r_c = math.hypot(x, z)
        r_o = _ray_r(_CONE_OUTER, th) or 51.0
        r_i = _ray_r(_CONE_INNER, th) or 23.2
        y = _CONE_Y1 + (_CONE_Y1 - _CONE_Y0) / (r_o - r_i) * (r_c - r_o)
        B.append([x, y, z])
    B.sort(key=lambda p: _ang(p[0], p[2]))
    # stitch the two rings by merged angular walk
    ann = []
    na, nb = len(A), len(B)
    i = j = 0
    aa = [_ang(p[0], p[2]) for p in A]
    ba = [_ang(p[0], p[2]) for p in B]
    while i < na or j < nb:
        ai, bj = A[i % na], B[j % nb]
        a_next = aa[(i + 1) % na] + (2 * math.pi if i + 1 >= na else 0)
        b_next = ba[(j + 1) % nb] + (2 * math.pi if j + 1 >= nb else 0)
        if i < na and (j >= nb or a_next <= b_next):
            t = [ai, B[j % nb], A[(i + 1) % na]]; i += 1
        else:
            t = [ai, B[j % nb], B[(j + 1) % nb]]; j += 1
        n = _tnormal(t)
        ann.append(t if n[1] >= 0 else [t[0], t[2], t[1]])     # face UP
    # cylinder walls: meet height -> y200, outward
    walls = []
    for k in range(len(B)):
        p1, p2 = B[k], B[(k + 1) % len(B)]
        seg = ((round(p1[0], 2), round(p1[2], 2)), (round(p2[0], 2), round(p2[2], 2)))
        if seg in _CHORD_SEGS or (seg[1], seg[0]) in _CHORD_SEGS:
            continue
        mx, mz = (p1[0] + p2[0]) / 2, (p1[2] + p2[2]) / 2
        walls += _q(p1, p2, [p2[0], _CYL_TOP, p2[2]], [p1[0], _CYL_TOP, p1[2]], [mx, 0, mz])
    return ann, walls


_ANN, _CYLW = _cone_cyl_tris()

# ---- ADD: authored replacement tris = the h01 roof-cone extension annulus + the cylinder from the meet
#      height up. (The three fin bump-out chord walls that used to lead this list were removed — no longer
#      wanted, and they never shipped once obj56 went vanilla-cylinder + rock-hull only.) ----
_ADD = _ANN + _CYLW

# ---- s04h01 `_v` edits: drop everything fully ABOVE the kept roof band (the cone+cylinder shell covers
#      it). LOCAL-space rule (part sits at mapinfo y=-10, so local y = world y + 10). ----
H01_KEEP_BELOW = _CONE_Y1 + 10.0 - 0.1        # local: tris with ALL verts >= this are removed



# ---- s04h01 LEG template (user-selected 2026-08, WORLD coords): one of the structure's 8 support legs.
#      The mesh is 8-fold rotationally symmetric (about the y axis, 45-degree steps) to within ~0.2 units,
#      so the other 7 legs are found by rotating this template and matching pool-tri centroids (+-1.5).
#      Each leg becomes ONE contiguous node: a leg split across kd nodes gave those nodes huge bounding
#      boxes, the near-box gather pulled everything at once, and the 400-poly camera buffer overflowed ->
#      silent truncation = camera clipping. Tight per-leg bboxes keep the gather small.
_LEG_TMPL = _parse("""
-21.52,42.32,8.8, -48.88,49.5,19.75, -21.58,42.32,-8.94
-21.58,42.32,-8.94, -48.88,49.5,19.75, -49.12,49.5,-20.35
-24.46,37.52,10.13, -40.82,29.29,6.2, -24.46,37.52,-10.13
-41.75,37.52,17.29, -45.84,29.26,12.17, -24.46,37.52,10.13
-45.84,29.26,-12.17, -41.75,37.52,-17.29, -24.46,37.52,-10.13
-24.46,37.52,-10.13, -40.82,29.29,6.2, -40.82,29.29,-6.2
-24.46,37.52,10.13, -45.84,29.26,12.17, -40.82,29.29,6.2
-24.46,37.52,-10.13, -40.82,29.29,-6.2, -45.84,29.26,-12.17
-43.85,40.4,-18.16, -45.84,29.26,-12.17, -50.76,29.26,-11.9
-50.76,29.26,-11.9, -55.67,29.26,-11.63, -45.95,43.28,-19.03
-41.75,37.52,17.29, -50.76,29.26,11.9, -45.84,29.26,12.17
-43.85,40.4,18.16, -55.67,29.26,11.63, -50.76,29.26,11.9
-60.84,29.33,5.95, -55.67,29.26,11.63, -45.95,43.28,19.03
-45.95,43.28,-19.03, -55.67,29.26,-11.63, -60.84,29.33,-5.95
-40.82,29.29,6.2, -43.34,26.78,-5.63, -40.82,29.29,-6.2
-43.34,26.78,5.63, -40.82,29.29,6.2, -45.84,29.26,12.17
-45.84,29.26,-12.17, -40.82,29.29,-6.2, -43.34,26.78,-5.63
-40.82,29.29,6.2, -43.34,26.78,5.63, -43.34,26.78,-5.63
-50.76,29.26,-11.9, -45.84,29.26,-12.17, -47.13,26.81,-9.11
-43.34,26.78,5.63, -45.84,29.26,12.17, -47.13,26.81,9.11
-45.84,29.26,-12.17, -43.34,26.78,-5.63, -47.13,26.81,-9.11
-51.15,26.81,9, -45.84,29.26,12.17, -50.76,29.26,11.9
-51.15,26.81,9, -47.13,26.81,9.11, -45.84,29.26,12.17
-51.15,26.81,-9, -55.67,29.26,-11.63, -50.76,29.26,-11.9
-50.76,29.26,-11.9, -47.13,26.81,-9.11, -51.15,26.81,-9
-55.18,26.81,8.89, -50.76,29.26,11.9, -55.67,29.26,11.63
-51.15,26.81,9, -50.76,29.26,11.9, -55.18,26.81,8.89
-55.18,26.81,-8.89, -55.67,29.26,-11.63, -51.15,26.81,-9
-55.18,26.81,-8.89, -60.84,29.33,-5.95, -55.67,29.26,-11.63
-58.87,26.83,5.31, -55.67,29.26,11.63, -60.84,29.33,5.95
-55.18,26.81,8.89, -55.67,29.26,11.63, -58.87,26.83,5.31
-58.87,26.83,-5.31, -60.84,29.33,5.95, -60.84,29.33,-5.95
-58.87,26.83,-5.31, -60.84,29.33,-5.95, -55.18,26.81,-8.89
-58.87,26.83,5.31, -60.84,29.33,5.95, -58.87,26.83,-5.31
-41.76,28.88,5.76, -51.26,11.65,-2.93, -41.76,28.88,-5.76
-46.08,28.88,10.08, -54.33,11.35,6.5, -41.76,28.88,5.76
-41.76,28.88,-5.76, -54.33,11.35,-6.5, -46.08,28.88,-10.08
-50.4,28.88,-10.08, -46.08,28.88,-10.08, -54.33,11.35,-6.5
-50.4,28.88,10.08, -57.06,11.35,6.5, -46.08,28.88,10.08
-50.4,28.88,-10.08, -57.06,11.35,-6.5, -54.72,28.88,-10.08
-50.4,28.88,10.08, -54.72,28.88,10.08, -59.79,11.35,6.5
-62.69,11.57,2.99, -54.72,28.88,10.08, -59.04,28.88,5.76
-54.72,28.88,-10.08, -62.69,11.57,-2.99, -59.04,28.88,-5.76
-59.04,28.88,-5.76, -62.69,11.57,2.99, -59.04,28.88,5.76
-41.76,28.88,5.76, -51.26,11.65,2.93, -51.26,11.65,-2.93
-41.76,28.88,5.76, -54.33,11.35,6.5, -51.26,11.65,2.93
-41.76,28.88,-5.76, -51.26,11.65,-2.93, -54.33,11.35,-6.5
-46.08,28.88,10.08, -57.06,11.35,6.5, -54.33,11.35,6.5
-50.4,28.88,-10.08, -54.33,11.35,-6.5, -57.06,11.35,-6.5
-50.4,28.88,10.08, -59.79,11.35,6.5, -57.06,11.35,6.5
-54.72,28.88,-10.08, -57.06,11.35,-6.5, -59.79,11.35,-6.5
-62.69,11.57,2.99, -59.79,11.35,6.5, -54.72,28.88,10.08
-54.72,28.88,-10.08, -59.79,11.35,-6.5, -62.69,11.57,-2.99
-59.04,28.88,-5.76, -62.69,11.57,-2.99, -62.69,11.57,2.99
-54.33,11.35,-6.5, -55.39,-5.58,-4.32, -57.06,11.35,-6.5
-57.06,11.35,6.5, -57.37,-5.58,4.32, -54.33,11.35,6.5
-57.37,-5.58,-4.32, -59.79,11.35,-6.5, -57.06,11.35,-6.5
-57.06,11.35,6.5, -59.79,11.35,6.5, -59.36,-5.58,4.32
-62.69,11.57,-2.99, -59.79,11.35,-6.5, -59.36,-5.58,-4.32
-61.73,-5.55,1.94, -59.79,11.35,6.5, -62.69,11.57,2.99
-62.69,11.57,-2.99, -61.73,-5.55,1.94, -62.69,11.57,2.99
-54.33,11.35,6.5, -57.37,-5.58,4.32, -55.39,-5.58,4.32
-57.06,11.35,-6.5, -55.39,-5.58,-4.32, -57.37,-5.58,-4.32
-59.36,-5.58,4.32, -57.37,-5.58,4.32, -57.06,11.35,6.5
-57.37,-5.58,-4.32, -59.36,-5.58,-4.32, -59.79,11.35,-6.5
-61.73,-5.55,1.94, -59.36,-5.58,4.32, -59.79,11.35,6.5
-62.69,11.57,-2.99, -59.36,-5.58,-4.32, -61.73,-5.55,-1.94
-62.69,11.57,-2.99, -61.73,-5.55,-1.94, -61.73,-5.55,1.94
-53.01,-5.55,-1.94, -53.01,-5.55,1.94, -55.08,-10,1.94
-53.01,-5.55,1.94, -55.39,-5.58,4.32, -55.08,-10,1.94
-55.39,-5.58,-4.32, -53.01,-5.55,-1.94, -55.08,-10,-1.94
-57.37,-5.58,-4.32, -55.39,-5.58,-4.32, -55.08,-10,-1.94
-57.06,-10,1.94, -55.39,-5.58,4.32, -57.37,-5.58,4.32
-57.06,-10,-1.94, -59.36,-5.58,-4.32, -57.37,-5.58,-4.32
-59.36,-5.58,4.32, -59.04,-10,1.94, -57.37,-5.58,4.32
-53.01,-5.55,-1.94, -55.08,-10,1.94, -55.08,-10,-1.94
-61.73,-5.55,1.94, -59.04,-10,1.94, -59.36,-5.58,4.32
-61.73,-5.55,-1.94, -59.36,-5.58,-4.32, -59.04,-10,-1.94
-61.73,-5.55,-1.94, -59.04,-10,-1.94, -61.73,-5.55,1.94
-57.06,-10,1.94, -55.08,-10,1.94, -55.39,-5.58,4.32
-57.37,-5.58,-4.32, -55.08,-10,-1.94, -57.06,-10,-1.94
-57.37,-5.58,4.32, -59.04,-10,1.94, -57.06,-10,1.94
-57.06,-10,-1.94, -59.04,-10,-1.94, -59.36,-5.58,-4.32
-61.73,-5.55,1.94, -59.04,-10,-1.94, -59.04,-10,1.94
-53.78,43.7,-9.41, -60.84,29.33,-5.95, -60.84,29.33,5.95
-53.78,43.7,9.41, -60.84,29.33,5.95, -45.95,43.28,19.03
-45.95,43.28,-19.03, -60.84,29.33,-5.95, -53.78,43.7,-9.41
-53.78,43.7,-9.41, -60.84,29.33,5.95, -53.78,43.7,9.41
-53.78,43.7,9.41, -45.95,43.28,19.03, -43.2,52.54,12.63
-43.2,52.54,-12.63, -45.95,43.28,-19.03, -53.78,43.7,-9.41
-43.2,52.54,-12.63, -53.78,43.7,-9.41, -53.78,43.7,9.41
-43.2,52.54,12.63, -45.95,43.28,19.03, -41.34,52.54,17.12
-43.2,52.54,-12.63, -53.78,43.7,9.41, -43.2,52.54,12.63
-43.2,52.54,-12.63, -41.34,52.54,-17.12, -45.95,43.28,-19.03
-43.85,40.4,18.16, -45.95,43.28,19.03, -55.67,29.26,11.63
-43.85,40.4,18.16, -50.76,29.26,11.9, -41.75,37.52,17.29
-43.85,40.4,-18.16, -41.75,37.52,-17.29, -45.84,29.26,-12.17
-50.76,29.26,-11.9, -45.95,43.28,-19.03, -43.85,40.4,-18.16
""")


def _cen3(t):
    return [(t[0][0] + t[1][0] + t[2][0]) / 3, (t[0][1] + t[1][1] + t[2][1]) / 3,
            (t[0][2] + t[1][2] + t[2][2]) / 3]


def _leg_clusters(pool, tol=1.5):
    """(legs, rest): pool split into 8 per-leg tri lists (template rotated k*45deg, centroid match within
    tol) and the unmatched remainder. Pool is LOCAL space; the template is WORLD (viewer) -> +10 y."""
    rots = []
    for k in range(8):
        th = k * math.pi / 4
        c, sn = math.cos(th), math.sin(th)
        rots.append([( p[0]*c - p[2]*sn, p[1], p[0]*sn + p[2]*c )
                     for p in ([_cen3([[q[0], q[1] + 10.0, q[2]] for q in t]) for t in _LEG_TMPL])])
    legs = [[] for _ in range(8)]
    rest = []
    for t in pool:
        cx, cy, cz = _cen3(t)
        best_k, best_d = None, tol
        for k in range(8):
            for rx, ry, rz in rots[k]:
                d = max(abs(cx - rx), abs(cy - ry), abs(cz - rz))
                if d < best_d:
                    best_d, best_k = d, k
        (legs[best_k] if best_k is not None else rest).append(t)
    return legs, rest


def custom_h01_v_nodes(max_tris=100, scn=None):
    """The authored s04h01 `_v`: full visual pool minus everything fully above the roof band; the 8 support
    legs each become ONE contiguous node (tight bbox -> small camera gather), remainder kd-split."""
    pool = [t for _, tris in part_v_nodes('s04h01', 10 ** 9, scn=scn) for t in tris]
    keep = [t for t in pool if min(p[1] for p in t) < H01_KEEP_BELOW]
    legs, rest = _leg_clusters(keep)
    out = [(f'h01leg{k}', tris) for k, tris in enumerate(legs) if tris]
    out += [(f'h01v{i:02d}', bk) for i, bk in enumerate(_kd_split(rest, max_tris))]
    return out


def _kd_split(tris, max_tris=100):
    """Split along the longest centroid axis into ceil(n/max_tris) spatially-compact leaves. Unlike a plain
    median split (which halves to powers of two and can land leaves at ~max/2), each cut hands both sides a
    PROPORTIONAL share of the leaves they need, so every leaf ends up just under max_tris."""
    import math as _m

    def cen(t):
        return ((t[0][0]+t[1][0]+t[2][0])/3, (t[0][1]+t[1][1]+t[2][1])/3, (t[0][2]+t[1][2]+t[2][2])/3)

    def rec(ts):
        k = _m.ceil(len(ts) / max_tris)                 # leaves this subtree must produce
        if k <= 1:
            return [ts]
        cs = [cen(t) for t in ts]
        axis = max(range(3), key=lambda a: max(c[a] for c in cs) - min(c[a] for c in cs))
        order = sorted(range(len(ts)), key=lambda i: cs[i][axis])
        kl = k // 2                                      # left subtree's leaf share
        mid = round(len(ts) * kl / k)
        return rec([ts[i] for i in order[:mid]]) + rec([ts[i] for i in order[mid:]])

    return rec(list(tris))


def part_v_nodes(sub_name='s04h01', max_tris=100, scn=None):
    """Authored `_v` (camera collision) for a building part: the part's FULL visual mesh in PART-LOCAL space
    (all nodes, parent-chain accumulated — doors/windows land on their walls), pooled and kd-split into
    <=max_tris nodes. Local space on purpose: a baked `_v` variant is loaded per part and transformed by
    each mapinfo placement, so one mesh serves every instance. Returns [('h01v00', tris), ...]."""
    if scn is None:
        scn = load_scene('gedit/s04/scene.scn')
    DIR = scene_placed._scndir(scn)
    off, size = DIR[sub_name]
    m = next(re.finditer(rb'MDS\x00', scn[off:off + size]))
    mds = off + m.start()
    nodes, wm = scene_placed._accum(scn, mds)
    pool = []
    for i, (nn, mo, par, mat) in enumerate(nodes):
        if mo == 0:
            continue
        fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c + 3] == b'MDT'), None)
        if not fo:
            continue
        M = wm(i)
        v = [xform(M, p) for p in read_verts(scn, fo)]
        pool += [[list(v[a]), list(v[b]), list(v[c])] for a, b, c in read_tris(scn, fo)]
    stem = sub_name[3:]                                     # 's04h01' -> 'h01'
    return [(f'{stem}v{i:02d}', bk) for i, bk in enumerate(_kd_split(pool, max_tris))]


# ---- ROCK SHELLS (2026-08): replace the three dense/concave rock camera hulls in obj56 with coarse CONVEX
#      octagon prisms. The rocks are the only structures the camera leaks through: 96-188 lumpy tris each
#      (median edge 10-40u, multi-lobed/re-entrant) — the swept-slide resolves ONE plane + one corner-verify
#      per frame, and dense concave contact sets defeat it (same failure family as the documented e03h11
#      turrets). Houses (32-tri boxes) and the cylinder (16 huge facets) hold fine. Shell = support-plane
#      octagon (never cuts inside the hull's footprint) extruded ymin..ymax+2 with a top fan: ~22 tris of
#      30-60u facets per rock. Selection: obj56 tris fully inside the rock VISUAL's padded bbox, excluding
#      r<=83 of the origin (central-cylinder guard).
_ROCK_SEL = {                     # visual-node bbox (from iwa01/02/03__s) + 8 pad, baked as literals
    'iwa01': (-212.1, -81.5, -96.1, 34.0),
    'iwa02': (-55.7, 69.7, 126.6, 182.3),
    'iwa03': (177.1, 252.2, -88.9, -9.9),
}


def _rock_sel_tris(obj56, box):
    x0, x1, z0, z1 = box
    return [t for t in obj56 if all(x0 <= p[0] <= x1 and z0 <= p[2] <= z1 for p in t)
            and all(math.hypot(p[0], p[2]) > 83 for p in t)]


def _shell_prism(tris, pad=1.0):
    """Support-plane octagon prism around the tri set: 8 side quads + top fan (~22 tris, big facets)."""
    pts = [p for t in tris for p in t]
    ymin = min(p[1] for p in pts)
    ymax = max(p[1] for p in pts) + 2.0
    dirs = [(math.cos(k * math.pi / 4), math.sin(k * math.pi / 4)) for k in range(8)]
    sup = [max(p[0] * dx + p[2] * dz for p in pts) + pad for dx, dz in dirs]
    # octagon vertices = intersections of adjacent support planes
    ring = []
    for k in range(8):
        (ax, az), sa = dirs[k], sup[k]
        (bx, bz), sb = dirs[(k + 1) % 8], sup[(k + 1) % 8]
        det = ax * bz - az * bx
        ring.append(((sa * bz - az * sb) / det, (ax * sb - sa * bx) / det))
    out = []
    for k in range(8):
        (x0, z0), (x1, z1) = ring[k], ring[(k + 1) % 8]
        mx, mz = (x0 + x1) / 2, (z0 + z1) / 2
        out += _q([x0, ymin, z0], [x1, ymin, z1], [x1, ymax, z1], [x0, ymax, z0], [mx, 0, mz])
    for k in range(1, 7):                                   # top fan (faces up)
        (x0, z0), (x1, z1), (x2, z2) = ring[0], ring[k], ring[k + 1]
        t = [[x0, ymax, z0], [x1, ymax, z1], [x2, ymax, z2]]
        out.append(t if _tnormal(t)[1] >= 0 else [t[0], t[2], t[1]])
    return out


def rock_shell_obj56(scn=None):
    """(tris, removed, shells): vanilla obj56 with the three rock hulls swapped for coarse convex shells."""
    obj56 = next(t for nn, t in vanilla_v_nodes(scn) if nn == 'obj56')
    removed = []
    shells = []
    keep = list(obj56)
    for name, box in _ROCK_SEL.items():
        sel = _rock_sel_tris(keep, box)
        if not sel:
            raise SystemExit(f'rock shell: no hull tris matched for {name} (drift?)')
        selkeys = set(_rmkey(t) for t in sel)
        keep = [t for t in keep if _rmkey(t) not in selkeys]
        removed += sel
        shells += _shell_prism(sel)
    return keep + shells, removed, shells


# ---- IWA01 tunnel-rock camera hull (2026-08): REPLACES the lumpy obj56 iwa01 selection with a smooth
#      circle wall centred on the ROCK, keeping the native tunnel interior so the camera can pass through,
#      and extending the two entrance collars OUTWARD ALONG THEIR OWN TAPER to meet the circle (seamless
#      funnels that catch the camera at the wide circle opening and channel it into the narrow native mouth).
#      Pure REPLACE: iwa01_selection removed, this added. Base y-15 (clips the in-water base — approved).
_IWA01_TOP = 92.0
_IWA01_BOT = -15.0
_IWA01_PAD = 4.0


def _P(raw):
    out = []
    for ln in raw.strip().splitlines():
        if ln.strip():
            x = [float(v) for v in ln.split(',')]
            out.append([x[0:3], x[3:6], x[6:9]])
    return out


_IWA01_WEST = _P("""
-167.92,29.7,-52.47, -161.65,34.59,-41.16, -169.79,40.38,-42.83
-161.65,34.59,-41.16, -172.38,40.35,-31.57, -169.79,40.38,-42.83
-161.65,34.59,-41.16, -164.94,34.49,-29.88, -172.38,40.35,-31.57
-172.38,40.35,-31.57, -164.94,34.49,-29.88, -166.39,28.13,-23.63
-172.38,40.35,-31.57, -166.39,28.13,-23.63, -174.07,29.7,-23.15
-174.07,29.7,-23.15, -166.39,28.13,-23.63, -170.04,16.08,-23.01
-174.07,29.7,-23.15, -170.04,16.08,-23.01, -177.77,17.67,-22.52
-177.77,17.67,-22.52, -170.04,16.08,-23.01, -167.96,8,-28.08
-177.77,17.67,-22.52, -167.96,8,-28.08, -182.73,6.74,-30.49
-182.73,6.74,-30.49, -167.96,8,-28.08, -163.51,8,-47.58
-182.73,6.74,-30.49, -163.51,8,-47.58, -178.53,6.89,-51.9
-178.53,6.89,-51.9, -163.51,8,-47.58, -163.52,16.01,-51.6
-178.53,6.89,-51.9, -163.52,16.01,-51.6, -171.69,17.54,-55.71
-171.69,17.54,-55.71, -159.62,28.25,-48.39, -167.92,29.7,-52.47
-171.69,17.54,-55.71, -163.52,16.01,-51.6, -159.62,28.25,-48.39
-167.92,29.7,-52.47, -159.62,28.25,-48.39, -161.65,34.59,-41.16
""")
_IWA01_EAST = _P("""
-123.04,38.75,-36.34, -125.26,38.74,-25.25, -131.68,35.04,-26.03
-123.04,38.75,-36.34, -131.68,35.04,-26.03, -128.7,35.07,-37.26
-123.04,38.75,-36.34, -128.7,35.07,-37.26, -126.84,28.52,-43.55
-123.04,38.75,-36.34, -126.84,28.52,-43.55, -120.34,28.3,-44.21
-120.34,28.3,-44.21, -122.29,15.91,-44.15, -115.44,16.18,-44.75
-120.34,28.3,-44.21, -126.84,28.52,-43.55, -122.29,15.91,-44.15
-115.44,16.18,-44.75, -124.52,8,-38.68, -112.93,5.12,-37.63
-115.44,16.18,-44.75, -122.29,15.91,-44.15, -124.52,8,-38.68
-112.93,5.12,-37.63, -124.52,8,-38.68, -128.97,8,-19.18
-112.93,5.12,-37.63, -128.97,8,-19.18, -117.04,4.94,-16.3
-129.17,15.9,-16.07, -117.04,4.94,-16.3, -128.97,8,-19.18
-129.17,15.9,-16.07, -119.65,16.18,-11.88, -117.04,4.94,-16.3
-133.43,28.46,-19.13, -123.62,28.93,-15.17, -119.65,16.18,-11.88
-133.43,28.46,-19.13, -119.65,16.18,-11.88, -129.17,15.9,-16.07
-131.68,35.04,-26.03, -125.26,38.74,-25.25, -123.62,28.93,-15.17
-131.68,35.04,-26.03, -123.62,28.93,-15.17, -133.43,28.46,-19.13
""")
_IWA01_INTERIOR = _P("""
-128.7,35.07,-37.26, -131.68,35.04,-26.03, -164.94,34.49,-29.88
-128.7,35.07,-37.26, -164.94,34.49,-29.88, -161.65,34.59,-41.16
-159.62,28.25,-48.39, -128.7,35.07,-37.26, -161.65,34.59,-41.16
-159.62,28.25,-48.39, -126.84,28.52,-43.55, -128.7,35.07,-37.26
-164.94,34.49,-29.88, -131.68,35.04,-26.03, -133.43,28.46,-19.13
-164.94,34.49,-29.88, -133.43,28.46,-19.13, -166.39,28.13,-23.63
-133.43,28.46,-19.13, -170.04,16.08,-23.01, -166.39,28.13,-23.63
-133.43,28.46,-19.13, -129.17,15.9,-16.07, -170.04,16.08,-23.01
-128.97,8,-19.18, -170.04,16.08,-23.01, -129.17,15.9,-16.07
-128.97,8,-19.18, -167.96,8,-28.08, -170.04,16.08,-23.01
-124.52,8,-38.68, -167.96,8,-28.08, -128.97,8,-19.18
-124.52,8,-38.68, -163.51,8,-47.58, -167.96,8,-28.08
-163.51,8,-47.58, -124.52,8,-38.68, -122.29,15.91,-44.15
-163.51,8,-47.58, -122.29,15.91,-44.15, -163.52,16.01,-51.6
-122.29,15.91,-44.15, -159.62,28.25,-48.39, -163.52,16.01,-51.6
-122.29,15.91,-44.15, -126.84,28.52,-43.55, -159.62,28.25,-48.39
""")

# vanilla obj56 tris inside the _ROCK_SEL['iwa01'] bbox that must SURVIVE the removal: the low walkway/
# bank strip running through and past both tunnel mouths (y~9-14 path collision + its water-side skirts).
# Matched by centroid (tolerance), not _rmkey — one tri's coords land on a 0.05 rounding boundary.
_IWA01_KEEP = _P("""
-87.73,9,29.73, -92.08,-10,10.68, -87.73,-10,29.73
-87.73,9,29.73, -92.08,12,10.68, -92.08,-10,10.68
-92.08,12,10.68, -89.97,-10,-10.28, -92.08,-10,10.68
-92.08,12,10.68, -89.97,14,-10.28, -89.97,-10,-10.28
-89.97,-10,-10.28, -89.97,14,-10.28, -110.47,11,-11.73
-110.47,11,-11.73, -94.15,-15,-10.14, -89.97,-10,-10.28
-106.02,11,-31.23, -85.52,14,-29.78, -85.52,-10,-29.78
-85.52,-10,-29.78, -89.49,-15,-33.18, -106.02,11,-31.23
-89.97,14,-10.28, -85.52,14,-29.78, -106.02,11,-31.23
-89.97,14,-10.28, -106.02,11,-31.23, -110.47,11,-11.73
-110.47,11,-11.73, -106.02,11,-31.23, -124.52,10,-38.68
-110.47,11,-11.73, -124.52,10,-38.68, -128.97,10,-19.18
-128.97,10,-19.18, -144.01,10,-43.13, -148.46,10,-23.63
-128.97,10,-19.18, -124.52,10,-38.68, -144.01,10,-43.13
-148.46,10,-23.63, -163.51,10,-47.58, -167.96,10,-28.08
-148.46,10,-23.63, -144.01,10,-43.13, -163.51,10,-47.58
-167.96,10,-28.08, -163.51,10,-47.58, -184.01,9,-49.03
-167.96,10,-28.08, -184.01,9,-49.03, -188.46,9,-29.53
-188.46,9,-29.53, -202.51,11,-55.48, -206.96,11,-35.98
-188.46,9,-29.53, -184.01,9,-49.03, -202.51,11,-55.48
-188.46,9,-29.53, -206.96,11,-35.98, -206.96,-10,-35.98
-202.51,-10,-55.48, -202.51,11,-55.48, -184.01,9,-49.03
-206.96,-10,-35.98, -204.1,-15,-33.79, -188.46,9,-29.53
-184.01,9,-49.03, -200.13,-15,-57.17, -202.51,-10,-55.48
""")


def _iwa01_keep_match(tris):
    """Subset of `tris` whose centroid is within 0.2 of an _IWA01_KEEP centroid."""
    cen = lambda t: [sum(p[i] for p in t) / 3 for i in range(3)]
    kc = [cen(t) for t in _IWA01_KEEP]
    return [t for t in tris if any(max(abs(a - b) for a, b in zip(cen(t), c)) < 0.2 for c in kc)]


def _v3(a, o, b):
    return [a[i] + o * b[i] for i in range(3)]

def _dot(a, b): return sum(a[i] * b[i] for i in range(3))
def _sub(a, b): return [a[i] - b[i] for i in range(3)]
def _cross(a, b): return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]
def _norm(a):
    L = math.sqrt(_dot(a, a)) or 1.0
    return [c / L for c in a]


def _iwa01_visual_circle():
    """(cx, cz, R): circle centred on the VISUAL rock (iwa01__s), radius enclosing its ABOVE-WATER xz
    silhouette + pad. The rock's widest spread (r~61) is all submerged base — clipping that is fine (the
    camera never dips there) and sizing on the y>0 silhouette (r~50) keeps the hull ~11 units narrower."""
    from extract_scene_mesh import extract_mesh
    v, _ = extract_mesh(load_scene('gedit/s04/scene.scn'))['iwa01__s']
    cx = (min(p[0] for p in v) + max(p[0] for p in v)) / 2
    cz = (min(p[2] for p in v) + max(p[2] for p in v)) / 2
    R = max(math.hypot(p[0] - cx, p[2] - cz) for p in v if p[1] > 0) + _IWA01_PAD
    return cx, cz, R


def _collar_pairs(ent, ivk):
    """Ordered [(outer, inner)] around the tunnel cross-section: inner = verts shared with the interior,
    outer = the flared rim; each outer paired to the nearest inner it shares a triangle with; ordered by
    angle around the tunnel axis (inner-centroid -> outer-centroid)."""
    ev = {}
    for t in ent:
        for p in t:
            ev[tuple(round(c, 2) for c in p)] = p
    inner = {k: v for k, v in ev.items() if tuple(round(c, 2) for c in v) in ivk}
    outer = {k: v for k, v in ev.items() if k not in inner}
    pair = {}
    for ok, ov in outer.items():
        cand = set()
        for t in ent:
            tk = [tuple(round(c, 2) for c in p) for p in t]
            if ok in tk:
                cand |= {k for k in tk if k in inner}
        if cand:
            pair[ok] = min(cand, key=lambda ck: math.dist(ov, inner[ck]))
    ic = [sum(inner[k][i] for k in inner) / len(inner) for i in range(3)]
    oc = [sum(outer[k][i] for k in outer) / len(outer) for i in range(3)]
    axis = _norm(_sub(oc, ic))
    u = _norm(_cross(axis, [0, 1, 0]))
    w = _cross(axis, u)
    ang = lambda v: math.atan2(_dot(_sub(v, oc), w), _dot(_sub(v, oc), u))
    order = sorted((k for k in outer if k in pair), key=lambda k: ang(outer[k]))
    return [(outer[k], inner[pair[k]]) for k in order]


def _extend_to_circle(o, iv, cx, cz, R):
    """Continue the collar taper (iv -> o) until the xz-radius from (cx,cz) reaches R (the circle)."""
    d = _sub(o, iv)
    ox, oz, dx, dz = o[0] - cx, o[2] - cz, d[0], d[2]
    a = dx * dx + dz * dz
    b = 2 * (ox * dx + oz * dz)
    c = ox * ox + oz * oz - R * R
    disc = b * b - 4 * a * c
    if a < 1e-9 or disc < 0:
        return None
    t = (-b + math.sqrt(disc)) / (2 * a)
    return _v3(o, t, d)


def _iwa01_build():
    """The iwa01 camera hull: the cylinder shell around the rock MINUS the flared tunnel cutter (exact
    CSG difference), top & bottom caps stripped. Authored in tools/export_iwa01_blender.py and FROZEN to
    tools/iwa01_hull_data.py as a plain tri literal, so the bake needs no CSG deps. Re-run that generator
    (needs trimesh+manifold3d) to regenerate the frozen data after tweaking the funnel/cylinder params."""
    from iwa01_hull_data import IWA01_HULL
    return [[list(p) for p in t] for t in IWA01_HULL]


def iwa01_ring_tris():
    return _iwa01_build()


def iwa01_ring_obj56(scn=None):
    """Vanilla obj56 with the lumpy iwa01 selection REPLACED by the circle-hull build. The walkway/bank
    strip through the tunnel (_IWA01_KEEP) survives the bbox removal — the hull doesn't cover it."""
    obj56 = next(t for nn, t in vanilla_v_nodes(scn) if nn == 'obj56')
    sel = _rock_sel_tris(obj56, _ROCK_SEL['iwa01'])
    selk = set(_rmkey(t) for t in sel) - set(_rmkey(t) for t in _iwa01_keep_match(sel))
    keep = [t for t in obj56 if _rmkey(t) not in selk]
    return [list(map(list, t)) for t in keep] + _iwa01_build()


def custom_obj56_tris(scn=None):
    """Vanilla obj56 minus _RM plus _ADD. Raises if any _RM entry fails to match (drift guard)."""
    van = next((tris for nn, tris in vanilla_v_nodes(scn) if nn == 'obj56'), [])
    keys = set(_rmkey(t) for t in _RM)
    out = [t for t in van if _rmkey(t) not in keys]
    removed = len(van) - len(out)
    if removed != len(keys):
        raise SystemExit(f'obj56 removal drift: {removed} matched of {len(keys)} keys')
    return out + [[list(p) for p in t] for t in _ADD]


def custom_obj56_full(scn=None):
    """Vanilla obj56 with BOTH obj56 edits applied: the central-cylinder simplification (- _RM + _ADD)
    AND the iwa01 rock replaced by the CSG hull (- iwa01 selection + _iwa01_build). The two removals are
    disjoint regions (cylinder vs rock); a combined drift guard trips if either fails to match cleanly."""
    van = next((tris for nn, tris in vanilla_v_nodes(scn) if nn == 'obj56'), [])
    rm_keys = set(_rmkey(t) for t in _RM)
    sel = _rock_sel_tris(van, _ROCK_SEL['iwa01'])
    iwa_keys = set(_rmkey(t) for t in sel) - set(_rmkey(t) for t in _iwa01_keep_match(sel))
    keep = [t for t in van if _rmkey(t) not in rm_keys and _rmkey(t) not in iwa_keys]
    removed = len(van) - len(keep)
    if removed != len(rm_keys) + len(iwa_keys):
        raise SystemExit(f'obj56 full removal drift: {removed} removed vs '
                         f'{len(rm_keys)}(_RM)+{len(iwa_keys)}(iwa01) expected')
    return [list(map(list, t)) for t in keep] + [[list(p) for p in t] for t in _ADD] + _iwa01_build()


if __name__ == '__main__':
    van = next((tris for nn, tris in vanilla_v_nodes() if nn == 'obj56'), [])
    cus = custom_obj56_tris()
    print(f'obj56: vanilla {len(van)} -> custom {len(cus)} ({len(_RM)} removed, {len(_ADD)} added)')
    hv = custom_h01_v_nodes()
    print(f's04h01_v custom: {sum(len(t) for _, t in hv)} tris in {len(hv)} nodes '
          f'(max {max(len(t) for _, t in hv)}/node)')
    ann, cyl = _ANN, _CYLW
    ys = sorted(set(round(p[1], 1) for t in ann for p in t if p[1] > 50))
    print(f'annulus {len(ann)} tris (meet heights {ys[0]}..{ys[-1]}), cylinder-above {len(cyl)} tris')
