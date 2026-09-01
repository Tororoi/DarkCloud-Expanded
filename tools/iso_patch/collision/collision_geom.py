#!/usr/bin/env python3
"""Pure triangle geometry for the collision bakes (no town data): box/plane predicates, rounded tri keys,
vector math, the coplanar-merge engine (simplify_coplanar) and authored-quad builders. Split out of
bake_player_camera_collision.py (2026-09)."""
import math


def _box(t, x0, x1, y0, y1, z0, z1, e=0.5):
    return all(x0 - e <= p[0] <= x1 + e and y0 - e <= p[1] <= y1 + e and z0 - e <= p[2] <= z1 + e for p in t)


def _plane_x(t, xv, z0, z1, y0, y1, e=0.5):        # tri coplanar with x=xv, within a z/y window
    return all(abs(p[0] - xv) < e and z0 - e <= p[2] <= z1 + e and y0 - e <= p[1] <= y1 + e for p in t)


def _plane_z(t, zv, x0, x1, y0, y1, e=0.5):        # tri coplanar with z=zv, within an x/y window
    return all(abs(p[2] - zv) < e and x0 - e <= p[0] <= x1 + e and y0 - e <= p[1] <= y1 + e for p in t)


def _horiz(t):                                     # near-horizontal face (a floor/top; |normal.y| > 0.7)
    e1 = [t[1][i] - t[0][i] for i in range(3)]; e2 = [t[2][i] - t[0][i] for i in range(3)]
    n = [e1[1]*e2[2] - e1[2]*e2[1], e1[2]*e2[0] - e1[0]*e2[2], e1[0]*e2[1] - e1[1]*e2[0]]
    return abs(n[1]) > 0.7 * (math.hypot(*n) or 1.0)


def _rmkey(t):
    return tuple(sorted(tuple(round(c) for c in p) for p in t))


def _wkey(t):
    """Winding-PRESERVING key: min cyclic rotation of the 3 rounded vertices. Reversing a tri's winding yields a
    DIFFERENT key, so a flip touches only tris matching the known-backwards winding — never their correct twin."""
    v = [tuple(round(c) for c in p) for p in t]
    return min((v[0], v[1], v[2]), (v[1], v[2], v[0]), (v[2], v[0], v[1]))


def _tnormal(t):
    a, b, c = t
    u = [b[i] - a[i] for i in range(3)]
    v = [c[i] - a[i] for i in range(3)]
    return [u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]]


def _dot3(a, b):
    return a[0]*b[0] + a[1]*b[1] + a[2]*b[2]


def _cross3(a, b):
    return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]


def _unit3(a):
    L = math.sqrt(_dot3(a, a)) or 1.0
    return [c / L for c in a]


def _pt_in_tri2(px, py, tt):
    (x0, y0), (x1, y1), (x2, y2) = tt
    d1 = (px-x1)*(y0-y1) - (x0-x1)*(py-y1)
    d2 = (px-x2)*(y1-y2) - (x1-x2)*(py-y2)
    d3 = (px-x0)*(y2-y0) - (x2-x0)*(py-y0)
    return not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0))


def _greedy_rects(cov):
    """Cover the True cells of a boolean grid with a small set of maximal rectangles (extend right, then down).
    Preserves holes: an uncovered cell (e.g. a doorway) is never spanned. Not provably minimal but near it."""
    nx, ny = len(cov), len(cov[0])
    used = [[False]*ny for _ in range(nx)]
    out = []
    for i in range(nx):
        for j in range(ny):
            if not cov[i][j] or used[i][j]:
                continue
            i1 = i
            while i1+1 < nx and cov[i1+1][j] and not used[i1+1][j]:
                i1 += 1
            j1 = j
            while j1+1 < ny and all(cov[ii][j1+1] and not used[ii][j1+1] for ii in range(i, i1+1)):
                j1 += 1
            for ii in range(i, i1+1):
                for jj in range(j, j1+1):
                    used[ii][jj] = True
            out.append((i, j, i1, j1))
    return out


def _plane_quad(u, v, nn, d, a0, b0, a1, b1):
    """Reconstruct a rectangle [a0,a1]x[b0,b1] of the plane {p : p.nn == d} back to 3D via the orthonormal
    in-plane basis (u,v): p = a*u + b*v + d*nn. Two tris, wound so the face normal matches nn."""
    def P(a, b):
        return [a*u[i] + b*v[i] + d*nn[i] for i in range(3)]
    A, B, C, D = P(a0, b0), P(a1, b0), P(a1, b1), P(a0, b1)
    t1, t2 = [A, B, C], [A, C, D]
    if _dot3(_tnormal(t1), nn) < 0:                 # keep the group's one-sided facing
        t1, t2 = [A, C, B], [A, D, C]
    return [t1, t2]


def simplify_coplanar(tris, snap=5.0, outward=0.0, top=None, keep_windows=False):
    """Merge coplanar tris (of ANY plane orientation — vertical facades, flat tops, sloped roofs/banks) into
    minimal rectangles, preserving holes (doorways etc.). Camera-only decimation: a tessellated planar face made
    of many small tris collapses to a few big quads, which is what keeps the runtime camera-gather buffer (~409
    poly cap) from saturating over the canal. Each plane is worked in its own orthonormal in-plane basis (u,v);
    2D coords snap to a `snap`-unit grid so faint tessellation slants fuse — harmless for camera collision.

    For a VERTICAL facade (in-plane up-axis v ~ world-up) each occupied column is extended UP to the group's
    tallest point, so a jagged per-structure roofline becomes one flat top — the "extend the walls to full length"
    step. Only fills UPWARD from each column's lowest wall cell, so ground-level doorway gaps survive; columns with
    no wall at all (true gaps between separate structures) stay open. Returns the new tri list."""
    def sn(x):
        return round(x / snap) * snap
    groups, out = {}, []
    for t in tris:
        n = _tnormal(t)
        if math.sqrt(_dot3(n, n)) < 1e-9:
            out.append(t); continue                 # degenerate — leave it
        nn = _unit3(n)
        d = _dot3(nn, t[0])
        # Coarse plane key (normal to 0.1, offset to the snap grid): the source facades are slightly non-planar
        # (verts wobble ~0.5u, so per-tri normals wobble a few degrees). A tight key splits one wall into many
        # singleton "planes" that never merge; 0.1 fuses the wobble into one plane. Reconstruction rides the
        # group's representative plane, flattening the <1u wobble — invisible to camera collision.
        key = (tuple(round(c, 1) + 0.0 for c in nn), round(d / snap) * snap)
        groups.setdefault(key, (nn, d, []))[2].append(t)
    for (nn, d, g) in groups.values():
        if len(g) < 2:
            out.extend(g); continue
        # Author the merged plane OUTWARD (behind the visual mesh): nn faces the play area, so the vertex furthest
        # opposite nn is the outermost source point. Place the plane there (minus `outward` margin) so the merged
        # camera wall never protrudes in FRONT of the rendered wall — the camera can ride flush to the visual mesh
        # without the collision clipping it early. Costs a hair of depth behind the wall, invisible to the camera.
        d = min(_dot3(nn, p) for t in g for p in t) - outward
        # In-plane basis derived from world-up: v = steepest-ascent direction (world-up projected into the plane),
        # u = horizontal in-plane. So v is the up-axis for EVERY vertical facade (flatten works regardless of which
        # way the wall faces), and the basis lines up with the natural horizontal-rows/up-columns tessellation of
        # facades and sloped roofs alike. Degenerate only for a (near-)horizontal plane, where there's no up-in-
        # plane — fall back to a world-x basis and skip flatten (floors/ceilings have no roofline).
        vp = [-_dot3([0.0, 1.0, 0.0], nn) * nn[i] for i in range(3)]
        vp[1] += 1.0                                  # world-up minus its out-of-plane component
        if _dot3(vp, vp) < 1e-6:
            u = _unit3([1.0 - nn[0]*nn[0], -nn[0]*nn[1], -nn[0]*nn[2]])
            v = _cross3(nn, u)
        else:
            v = _unit3(vp)
            u = _unit3(_cross3(v, nn))
        tri2d = [[(sn(_dot3(p, u)), sn(_dot3(p, v))) for p in t] for t in g]
        us = sorted({p[0] for tt in tri2d for p in tt})
        vs = sorted({p[1] for tt in tri2d for p in tt})
        if len(us) < 2 or len(vs) < 2:
            out.extend(g); continue
        cov = [[any(_pt_in_tri2((us[i]+us[i+1])/2, (vs[j]+vs[j+1])/2, tt) for tt in tri2d)
                for j in range(len(vs)-1)] for i in range(len(us)-1)]
        if v[1] > 0.9:                               # VERTICAL facade — flatten the roofline to the group max
            for col in cov:
                occ = [j for j, c in enumerate(col) if c]
                if occ:
                    # keep_windows: fill only from the HIGHEST wall cell up — flattens crenellations but leaves
                    # every mid-wall opening (arcade arches / windows) intact. Default fills from the lowest cell
                    # up (only ground doorways survive), which is right for solid facades.
                    for j in range(max(occ) if keep_windows else min(occ), len(col)):
                        col[j] = True
        new = []
        for (i0, j0, i1, j1) in _greedy_rects(cov):
            b1 = vs[j1 + 1]
            if top is not None and v[1] > 0.9 and abs(b1 - vs[-1]) < 1e-6:
                b1 = top                              # force a VERTICAL facade's flattened top to a shared world-y
            new += _plane_quad(u, v, nn, d, us[i0], vs[j0], us[i1 + 1], b1)
        out.extend(new if len(new) < len(g) else g)
    return out


def _plane_region(axis, off, x=None, y=None, z=None):
    """Selector: tris lying on the plane {coord[axis] == off} (all verts within 2u) whose centroid falls in the
    optional x/y/z ranges. Used to target one specific wall for a directed merge."""
    def sel(t):
        if not all(abs(p[axis] - off) < 2 for p in t):
            return False
        c = [(t[0][i] + t[1][i] + t[2][i]) / 3.0 for i in range(3)]
        for i, rng in ((0, x), (1, y), (2, z)):
            if rng and not (rng[0] <= c[i] <= rng[1]):
                return False
        return True
    return sel


def _dir_quad(a, b, c, d, want):
    """Quad (a,b,c,d) as two tris, wound so its face normal points the same way as `want`."""
    tt = [[a, b, c], [a, c, d]]
    return tt if _dot3(_tnormal(tt[0]), want) >= 0 else [[a, c, b], [a, d, c]]


def _quad(a, b, c, d):
    """Two tris (a,b,c,d wound in order) for an explicit authored quad."""
    return [[a, b, c], [a, c, d]]
