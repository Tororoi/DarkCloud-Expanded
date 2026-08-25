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

# ---- ADD: authored replacement tris.
#      1st pass: close the three kept ring bump-outs (-z fin, +z fin, SW fin) with a chord wall each
#                (y10..200, outward) — 6 tris.
#      2nd pass: the h01 roof-cone extension annulus + the cylinder from the meet height up. ----
_ADD = (
    _q([24.39, 10, -71.45], [40.66, 10, -63.62], [40.66, 200, -63.62], [24.39, 200, -71.45], [0.43, 0, -0.90])
    + _q([9.02, 10, 74.96], [-9.02, 10, 74.96], [-9.02, 200, 74.96], [9.02, 200, 74.96], [0, 0, 1])
    + _q([-52.98, 10, 53.79], [-64.23, 10, 39.68], [-64.23, 200, 39.68], [-52.98, 200, 53.79], [-0.78, 0, 0.62])
    + _ANN + _CYLW
)

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


def custom_obj56_tris(scn=None):
    """Vanilla obj56 minus _RM plus _ADD. Raises if any _RM entry fails to match (drift guard)."""
    van = next((tris for nn, tris in vanilla_v_nodes(scn) if nn == 'obj56'), [])
    keys = set(_rmkey(t) for t in _RM)
    out = [t for t in van if _rmkey(t) not in keys]
    removed = len(van) - len(out)
    if removed != len(keys):
        raise SystemExit(f'obj56 removal drift: {removed} matched of {len(keys)} keys')
    return out + [[list(p) for p in t] for t in _ADD]


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
