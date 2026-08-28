#!/usr/bin/env python3
"""Yellow Drops crescent-pond ground redesign v3 — INCREMENTAL step 1 (viewer proposal, not baked).

Per user direction: no synthesized profile. Take the EXISTING `grid8` strip (subfile s1301) exactly
as it is — its real cross-sections, asymmetric crown, heights and skirts — and BEND IT OUTWARD:
every strip vertex is pushed along the chord-normal (the NE bulge direction) by a smooth bump that
is ZERO at both junction cuts, so the ends stay welded to the existing stub paths and only the belly
of the C sweeps wider.

    d' = d + BULGE_ADD * sin(pi * s)      s = normalized position along the junction chord

Knob: BULGE_ADD (extra bulge at the chord midpoint; the strip's current sagitta is ~108).
Later steps (crescent closure, facing) build on this warped strip.
"""
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from scene_placed import placed_meshes

# ---- knobs ------------------------------------------------------------------------------------
BULGE_ADD = 80.0             # extra outward bulge at the middle of the C (0 = untouched strip)
LOWER_DELTA = -8.0           # step 2: drop the pond-floor section so its lowest tops (y16) reach y8
                             # (= the lowest of the user's reference walkway tris at x~-403..-497)
NW_A = (377.0, 415.0); NW_B = (475.0, 339.0)   # NW junction cut (xz)
SE_A = (741.0, 755.0); SE_B = (820.0, 706.0)   # SE junction cut (xz)
REGION = (360.0, 840.0, 280.0, 770.0)          # x0,x1,z0,z1 — the strip's tris live here

_P0 = ((NW_A[0] + NW_B[0]) / 2, (NW_A[1] + NW_B[1]) / 2)
_P1 = ((SE_A[0] + SE_B[0]) / 2, (SE_A[1] + SE_B[1]) / 2)
_dx, _dz = _P1[0] - _P0[0], _P1[1] - _P0[1]
_L2 = _dx * _dx + _dz * _dz
_nx, _nz = -_dz / math.sqrt(_L2), _dx / math.sqrt(_L2)
# point the normal at the strip's OWN belly (sample: the strip's mid-arc runs through ~(763,433))
if (763 - _P0[0]) * _nx + (433 - _P0[1]) * _nz < 0:
    _nx, _nz = -_nx, -_nz


def strip_tris():
    """The existing grid8 strip tris (world-placed) inside REGION."""
    x0, x1, z0, z1 = REGION
    out = []
    for m in placed_meshes('gedit/s13/scene.scn', 'gedit/s13/mapinfo.cfg'):
        if m['name'] != 'grid8':
            continue
        verts = m.get('verts'); tris = m.get('tris') or []
        for t in tris:
            p = [verts[i] for i in t] if isinstance(t[0], int) else t
            c = [(p[0][k] + p[1][k] + p[2][k]) / 3 for k in range(3)]
            if x0 <= c[0] <= x1 and z0 <= c[2] <= z1:
                out.append([list(q) for q in p])
    return out


def warp_vert(p):
    s = ((p[0] - _P0[0]) * _dx + (p[2] - _P0[1]) * _dz) / _L2      # along-chord parameter
    s = max(0.0, min(1.0, s))
    d = BULGE_ADD * math.sin(math.pi * s)                           # bump: 0 at both junctions
    return [p[0] + d * _nx, p[1], p[2] + d * _nz]


def bent_strip_tris():
    return [[warp_vert(p) for p in t] for t in strip_tris()]


# ---- step 2: lower the pond-floor section (user-selected, resolved to PRE-warp xz columns) -----
# Whole vertex COLUMNS (top + skirt share xz) drop by LOWER_DELTA; cross-section/crown untouched.
# Neighbouring un-lowered tris share these columns, so the adjoining walkway tris become ramps.
LOWER_XZ = [
    (530.0, 439.0), (532.0, 429.0), (564.0, 344.0), (566.0, 337.0),
    (624.0, 473.0), (628.0, 465.0), (678.0, 387.0), (682.0, 380.0),
    (702.0, 525.0), (709.0, 511.0), (718.0, 672.0), (722.0, 581.0),
    (734.0, 664.0), (736.0, 573.0), (753.0, 449.0), (763.0, 433.0),
    (797.0, 548.0), (800.0, 640.0), (814.0, 542.0), (814.0, 635.0),
]


def _lower_amount(p):
    """Y offset for a pre-warp vertex (matched by xz column, tol 0.5)."""
    for kx, kz in LOWER_XZ:
        if abs(p[0] - kx) <= 0.5 and abs(p[2] - kz) <= 0.5:
            return LOWER_DELTA
    return 0.0


def lowered_tris():
    """Step 1 + step 2: the bent strip with the pond-floor columns lowered (ramps at the seams)."""
    out = []
    for t in strip_tris():
        out.append([(lambda w, d: [w[0], w[1] + d, w[2]])(warp_vert(p), _lower_amount(p)) for p in t])
    return out


# ---- step 3: smooth the CONVEX-side edge into a consistent arc --------------------------------
# The user-selected edge chain (NW->SE, post-warp coords): (473.72,418.22) fixed, (567.96,400.93),
# (678.51,418.34), (755.77,471.08), (767.02,535.85), (746.89,643.02) fixed. Circle fitted THROUGH
# the two fixed endpoints, least-squares over the interior: center (581.71,565.37), R=182.52.
# Interior edge columns project radially onto the arc; each PAIRED CROWN column shifts by the same
# delta so the edge band keeps its cross-section (E1 moves 17.5 — past the crown row otherwise).
# NOTE: targets are POST-warp coords fitted at BULGE_ADD=80 — refit if BULGE_ADD changes.
# Keys are PRE-warp xz columns; values REPLACE the post-warp xz (y untouched).
# Step 3b: interior verts RESPACED uniformly in angle between the fixed endpoints, so all five
# edge segments have equal chord length (95.4; they were 99/119/89/62/107) for an even rounded look.
SMOOTH_MOVES = {
    (530.0, 439.0): (562.68, 383.84),   # edge  (567.96,400.93)
    (532.0, 429.0): (563.17, 375.35),   # crown (568.45,392.44) same delta
    (624.0, 473.0): (656.84, 399.03),   # edge  (678.51,418.34)
    (628.0, 465.0): (660.57, 391.30),   # crown (682.24,410.61) same delta
    (702.0, 525.0): (730.48, 459.63),   # edge  (755.77,471.08)
    (709.0, 511.0): (737.99, 445.12),   # crown (763.28,456.57) same delta
    (722.0, 581.0): (763.50, 549.11),   # edge  (767.02,535.85)
    (736.0, 573.0): (776.57, 542.04),   # crown (780.09,528.78) same delta
}


def _smooth_xz(p):
    """Post-warp xz override for a pre-warp vertex column (tol 0.5), or None."""
    for (kx, kz), tgt in SMOOTH_MOVES.items():
        if abs(p[0] - kx) <= 0.5 and abs(p[2] - kz) <= 0.5:
            return tgt
    return None


def arc_tris():
    """Steps 1-3: bent + lowered + convex edge smoothed to the fitted arc."""
    out = []
    for t in strip_tris():
        tri = []
        for p in t:
            w = warp_vert(p)
            w[1] += _lower_amount(p)
            s = _smooth_xz(p)
            if s is not None:
                w[0], w[2] = s
            tri.append(w)
        out.append(tri)
    return out


# ---- step 4: spline-ladder rebuild — step 3 is the LOW-POLY CAGE of this ----------------------
# The strip is a ladder of cross-section stations along 4 longitudinal rows (verified against the
# real mesh connectivity): outer edge / outer crown / inner crown / inner edge, plus skirt-bottom
# rows sharing the edge rows' xz. Each row (after the step 1-3 transforms) is treated as a control
# polyline: SUBDIV_LEVELS rounds of interpolating 4-point subdivision insert on-spline midpoints
# (control points kept — chain ENDS stay welded to the junction stubs), then the bands are
# re-triangulated as a clean ladder. Cross-section profiles are preserved verbatim per station.
SUBDIV_LEVELS = 1            # 1 = 2x stations along the path, 2 = 4x, ...
CHAINS = {                   # pre-warp stations, NW -> SE (termini = junction-cut verts)
 'E_out': [(475,23,339),(566,16,337),(682,16,380),(763,16,433),(814,16,542),(814,16,635),(820,16,706)],
 'C_out': [(471,30,346),(564,20,344),(678,20,387),(753,20,449),(797,20,548),(800,20,640),(808,20,716)],
 'C_in':  [(380,30,399),(454,30,431),(532,20,429),(628,20,465),(709,20,511),(736,20,573),(734,20,664),(754,20,748)],
 'E_in':  [(377,23,415),(452,23,440),(530,16,439),(624,16,473),(702,16,525),(722,16,581),(718,16,672),(741,16,755)],
}
_BOTTOM_Y = -10.0


def _xform(p):
    """The full step 1-3 map for one pre-warp vertex."""
    w = warp_vert(p)
    w[1] += _lower_amount(p)
    s = _smooth_xz(p)
    if s is not None:
        w[0], w[2] = s
    return w


def _resample(pts, levels, snap=None):
    """Interpolating 4-point subdivision: keeps control points, inserts on-spline midpoints.
    `snap(mid, parent1, parent2)` may adjust each inserted midpoint (e.g. onto the fitted arc)."""
    for _ in range(levels):
        out = [pts[0]]
        n = len(pts)
        for i in range(n - 1):
            p0, p1, p2, p3 = pts[max(i-1, 0)], pts[i], pts[i+1], pts[min(i+2, n-1)]
            m = [-0.0625*p0[k] + 0.5625*p1[k] + 0.5625*p2[k] - 0.0625*p3[k] for k in range(3)]
            if snap is not None:
                m = snap(m, p1, p2)
            out.append(m)
            out.append(pts[i + 1])
        pts = out
    return pts


_ARC = (581.71, 565.37, 182.52)      # step-3 fitted circle (convex edge = E_in after the bulge)


def _arc_snap(m, p1, p2):
    """If both parent controls sit ON the fitted arc, the cage means 'circle' — put the midpoint there."""
    cx, cz, R = _ARC
    if abs(math.hypot(p1[0]-cx, p1[2]-cz) - R) < 0.6 and abs(math.hypot(p2[0]-cx, p2[2]-cz) - R) < 0.6:
        r = math.hypot(m[0]-cx, m[2]-cz)
        if r > 1.0:
            m = [cx + (m[0]-cx)*R/r, m[1], cz + (m[2]-cz)*R/r]
    return m


def _band(rowA, rowB, up=None, out_dir=None):
    """Triangulate between two resampled rows (equal or unequal length) by greedy zipping."""
    tris, i, j = [], 0, 0

    def emit(a, b, c):
        u = [b[k]-a[k] for k in range(3)]; v = [c[k]-a[k] for k in range(3)]
        n = [u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]]
        ref = up if up is not None else out_dir
        if ref is not None and (n[0]*ref[0] + n[1]*ref[1] + n[2]*ref[2]) < 0:
            a, c = c, a
        tris.append([list(a), list(b), list(c)])

    while i < len(rowA) - 1 or j < len(rowB) - 1:
        if j == len(rowB) - 1:
            adv_a = True
        elif i == len(rowA) - 1:
            adv_a = False
        else:   # advance the side whose next diagonal is shorter
            da = sum((rowA[i+1][k]-rowB[j][k])**2 for k in range(3))
            db = sum((rowB[j+1][k]-rowA[i][k])**2 for k in range(3))
            adv_a = da <= db
        if adv_a:
            emit(rowA[i], rowA[i+1], rowB[j]); i += 1
        else:
            emit(rowB[j], rowB[j+1], rowA[i]); j += 1
    return tris


def pond_tris():
    """Cumulative proposal: steps 1-3 cage, spline-resampled and rebuilt at 2^SUBDIV_LEVELS density."""
    rows = {name: _resample([_xform([float(x), float(y), float(z)]) for x, y, z in ch], SUBDIV_LEVELS,
                            snap=_arc_snap if name == 'E_in' else None)
            for name, ch in CHAINS.items()}
    for name in ('E_out', 'E_in'):   # skirt-bottom rows: same xz columns, bottom heights
        rows[name + '_b'] = _resample([_xform([float(x), _BOTTOM_Y, float(z)]) for x, y, z in CHAINS[name]],
                                      SUBDIV_LEVELS, snap=_arc_snap if name == 'E_in' else None)
    UP = [0.0, 1.0, 0.0]
    out = []
    out += _band(rows['E_out'], rows['C_out'], up=UP)
    out += _band(rows['C_out'], rows['C_in'], up=UP)
    out += _band(rows['C_in'], rows['E_in'], up=UP)
    for e, c in (('E_out', 'C_out'), ('E_in', 'C_in')):   # skirts face away from the path centre
        m = len(rows[e]) // 2
        od = [rows[e][m][0] - rows[c][m][0], 0.0, rows[e][m][2] - rows[c][m][2]]
        out += _band(rows[e], rows[e + '_b'], out_dir=od)
    # junction-stub tris (touch verts outside the ladder) pass through with the step 1-3 transform
    ladder = set()
    for ch in CHAINS.values():
        for x, y, z in ch:
            ladder.add((round(float(x), 2), round(float(y), 2), round(float(z), 2)))
    for x, y, z in CHAINS['E_out'] + CHAINS['E_in']:
        ladder.add((round(float(x), 2), round(_BOTTOM_Y, 2), round(float(z), 2)))
    for t in strip_tris():
        if any((round(p[0], 2), round(p[1], 2), round(p[2], 2)) not in ladder for p in t):
            out.append([_xform(p) for p in t])
    return out


if __name__ == '__main__':
    cur = strip_tris()
    print(f'strip: {len(cur)} tris; BULGE_ADD={BULGE_ADD}')
    # report the midpoint shift of a few surface verts
    for t in cur[:1]:
        for p in t:
            w = warp_vert(p)
            print('  ', [round(c, 1) for c in p], '->', [round(c, 1) for c in w])
