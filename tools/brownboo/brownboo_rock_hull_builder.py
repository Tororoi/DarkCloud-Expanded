#!/usr/bin/env python3
"""Regenerate the frozen iwa01 camera-collision hull (tools/brownboo/brownboo_rock_hull_data.py).

hull_tris() computes the finished hull headlessly — cylinder shell around the rock MINUS the flared tunnel
cutter (exact sequential CSG, needs trimesh+manifold3d) — from the authoring inputs kept in
tools/brownboo/brownboo_camera_collision.py (_IWA01_* tri blocks, _collar_pairs, _iwa01_visual_circle);
freeze_hull() writes it as a plain literal so the ISO bake needs no CSG deps.

  python3 tools/brownboo/brownboo_rock_hull_builder.py     # -> rewrites tools/brownboo/brownboo_rock_hull_data.py

(The interactive Blender-scene emitter this file grew from, and the shelved anti-climb corner-box cutter,
were removed 2026-09 — recoverable via git. The hull is FINALIZED; run this only to re-freeze after a
deliberate parameter change.)
"""
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'lib'))
import toolpath  # noqa: F401 — puts every tools/ subfolder on sys.path
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from brownboo_camera_collision import (
    _IWA01_WEST, _IWA01_EAST, _IWA01_INTERIOR, _iwa01_visual_circle,
    _IWA01_TOP, _IWA01_BOT, _collar_pairs,
)


def _weld(tris, prec=2):
    """Triangle list -> (verts, faces) with welded (deduplicated) verts, dropping degenerates. A
    welded mesh shares edges between neighbouring tris -> manifold, which the Boolean solver needs."""
    idx, verts, faces = {}, [], []
    for t in tris:
        f = []
        for p in t:
            k = tuple(round(c, prec) for c in p)
            if k not in idx:
                idx[k] = len(verts)
                verts.append([float(c) for c in p])
            f.append(idx[k])
        if len(set(f)) == 3:
            faces.append(f)
    return verts, faces


def _nonmanifold(faces):
    """Count undirected edges NOT shared by exactly two faces (0 == closed manifold)."""
    from collections import Counter
    ec = Counter()
    for f in faces:
        for i in range(3):
            a, b = f[i], f[(i + 1) % 3]
            ec[(a, b) if a < b else (b, a)] += 1
    return sum(1 for c in ec.values() if c != 2)


def _cyl_center(v):  # xz centroid
    return [sum(p[0] for p in v) / len(v), 0.0, sum(p[2] for p in v) / len(v)]


def _cylinder_tris(cx, cz, R, ybot, ytop, n=16):   # 16 segs ~= the vanilla building-cylinder coarseness
    """Closed, capped vertical cylinder (radius R about (cx,cz), ybot..ytop), outward/upward winding."""
    ring = [(cx + R * math.cos(2 * math.pi * k / n), cz + R * math.sin(2 * math.pi * k / n)) for k in range(n)]
    cbot, ctop = [cx, ybot, cz], [cx, ytop, cz]
    tris = []
    for k in range(n):
        (ax, az), (bx, bz) = ring[k], ring[(k + 1) % n]
        A, B = [ax, ybot, az], [bx, ybot, bz]
        C, D = [bx, ytop, bz], [ax, ytop, az]
        tris += [[A, B, C], [A, C, D]]                 # wall (outward)
        tris.append([cbot, B, A])                      # bottom cap (down)
        tris.append([ctop, D, C])                      # top cap (up)
    return tris


def _extend_along(v, axis, cx, cz, target):
    """Slide v along fixed direction `axis` until its xz-radius from (cx,cz) reaches `target`."""
    ox, oz, dx, dz = v[0] - cx, v[2] - cz, axis[0], axis[2]
    a = dx * dx + dz * dz
    b = 2 * (ox * dx + oz * dz)
    c = ox * ox + oz * oz - target * target
    disc = b * b - 4 * a * c
    if a < 1e-9 or disc < 0:
        return list(v)
    t = (-b + math.sqrt(disc)) / (2 * a)
    return [v[0] + t * axis[0], v[1] + t * axis[1], v[2] + t * axis[2]]


def _boundary_loops(verts, faces):
    """Ordered vertex-index loops around each open boundary (edges used by exactly one face)."""
    from collections import Counter, defaultdict
    ec = Counter()
    for f in faces:
        for i in range(3):
            a, b = f[i], f[(i + 1) % 3]
            ec[(a, b) if a < b else (b, a)] += 1
    bnd = [e for e, c in ec.items() if c == 1]
    adj = defaultdict(list)
    for a, b in bnd:
        adj[a].append(b)
        adj[b].append(a)
    used, loops = set(), []
    for s0, s1 in bnd:
        if (s0, s1) in used or (s1, s0) in used:
            continue
        loop, cur, prev = [s0], s0, None
        while True:
            nxt = None
            for x in adj[cur]:
                e = (cur, x) if cur < x else (x, cur)
                if e not in used and x != prev:
                    nxt = x
                    break
            if nxt is None:
                break
            used.add((cur, nxt) if cur < nxt else (nxt, cur))
            if nxt == loop[0]:
                break
            loop.append(nxt)
            prev, cur = cur, nxt
        loops.append(loop)
    return loops


def _entrance_dirs():
    """Map each collar OUTER-rim vertex -> the unit direction of ITS OWN entrance poly (inner->outer
    taper). Extending a rim vertex along this continues the exact angle of the entrance it sits on."""
    ivk = {tuple(round(c, 2) for c in p) for t in _IWA01_INTERIOR for p in t}
    dirs = {}
    for ent in (_IWA01_WEST, _IWA01_EAST):
        for o, iv in _collar_pairs(ent, ivk):
            d = [o[i] - iv[i] for i in range(3)]
            L = math.sqrt(sum(c * c for c in d)) or 1.0
            dirs[tuple(round(c, 2) for c in o)] = [c / L for c in d]
    return dirs


_FLARE_ARC = 55.0     # arc HALF-width (degrees) of the funnel mouth at the wall — the horizontal guide
                      # width. The mouths sit ~169° apart, so 55° per side leaves ~30° of wall between.
_FLARE_HALFH = 30.0   # vertical HALF-height of the wall opening (the Boolean clamps overshoot anyway).
_FLARE_RES = 16       # verts per funnel ring.
_FLARE_SWEEP = 5      # bands the rim->wall sweep is split into (more = smoother horn).


def _tnormal_local(t):
    e1 = [t[1][i] - t[0][i] for i in range(3)]
    e2 = [t[2][i] - t[0][i] for i in range(3)]
    return [e1[1] * e2[2] - e1[2] * e2[1], e1[2] * e2[0] - e1[0] * e2[2], e1[0] * e2[1] - e1[1] * e2[0]]
# The funnel is a clean analytic HORN: each rim vert maps to a point on a smooth ellipse (half-extents
# _FLARE_ARC x _FLARE_HALFH) on the wall band, and cross-sections sweep rim -> ellipse with the widening
# eased in as t^k. The exponent k is SOLVED at build time: smallest k whose whole surface clears the
# rock-face field — gentle taper early (through the rock zone at near-mouth width), rapid flare at the
# wall. No per-vert clamping, so the surface stays a smooth trumpet with no dents or ripples.


def _tunnel_solid_tris(cx, cz, R, margin=16.0):
    """Bore + both collars closed into a watertight CUTTER. Weld the passage surface, find its real open
    boundary loops (the two mouths), and flare each mouth out through the cylinder wall, WIDENING the
    cross-section (H/V split) and pinning the widened ring onto the wall band so the hole is cut at full
    width. ⚠ every face of the cutter that lies INSIDE the cylinder becomes a hull wall — so all closing
    geometry (outer prism, cap) must stay strictly OUTSIDE the wall, chords included: an arc-shaped ring's
    naive cap-fan centroid pulls INSIDE the wall (mean of a 130° arc sits ~20% inward) and its fan slices
    straight across the mouth (this exact bug shipped once — the tunnel read as fully closed). Hence the
    large margin and the centroid re-projection below. A Boolean Difference of the cylinder minus this
    cutter drills the tunnel: cavity walls = bore/collar/flare surface, exit holes = the widened mouths."""
    surf = ([list(map(list, t)) for t in _IWA01_INTERIOR]
            + [list(map(list, t)) for t in _IWA01_WEST]
            + [list(map(list, t)) for t in _IWA01_EAST])
    verts, faces = _weld(surf)
    tris = [[verts[i] for i in f] for f in faces]        # passage surface (welded, re-exploded to tris)

    try:                                                 # the visual rock mesh, for the exact k-solve below
        from extract_scene_mesh import extract_mesh, load_scene
        _rv, _rf = extract_mesh(load_scene('gedit/s04/scene.scn'))['iwa01__s']
        _rock = [[_rv[i] for i in f] for f in _rf]
    except Exception:
        _rock = []                                       # no extraction env -> no rock check

    def _in_rock(p):
        """Exact point-in-mesh parity test (+x ray) against the visual rock. Below-water points pass
        (clipping the submerged base is fine). Cell/silhouette approximations over-flagged by units and
        drove the widening exponent to its cap — this is the authoritative check."""
        if not _rock or p[1] < 0.5:
            return False
        cnt = 0
        for t in _rock:
            n = _tnormal_local(t)
            if abs(n[0]) < 1e-9:
                continue
            s = sum(n[i] * (t[0][i] - p[i]) for i in range(3)) / n[0]
            if s <= 0:
                continue
            q = [p[0] + s, p[1], p[2]]
            x0, y0 = t[0][1], t[0][2]; x1, y1 = t[1][1], t[1][2]; x2, y2 = t[2][1], t[2][2]
            det = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
            if abs(det) < 1e-12:
                continue
            a = ((q[1] - x0) * (y2 - y0) - (q[2] - y0) * (x2 - x0)) / det
            b = ((x1 - x0) * (q[2] - y0) - (y1 - y0) * (q[1] - x0)) / det
            if a >= 0 and b >= 0 and a + b <= 1:
                cnt += 1
        return cnt % 2 == 1

    dirs = _entrance_dirs()
    loops = _boundary_loops(verts, faces)
    cents = [[sum(verts[j][k] for j in lp) / len(lp) for k in range(3)] for lp in loops]
    for li, loop in enumerate(loops):
        other = cents[1 - li] if len(cents) == 2 else [cx, cents[li][1], cz]
        fb = [cents[li][0] - other[0], cents[li][1] - other[1], cents[li][2] - other[2]]  # fallback: tunnel axis
        Lf = math.sqrt(sum(c * c for c in fb)) or 1.0
        axis = [c / Lf for c in fb]                      # mouth axis, for the widening decomposition
        rc = cents[li]                                   # mouth centreline anchor
        rim = [verts[i] for i in loop]

        def _widen(p, wh, wv):
            """Scale p's offset from the mouth centreline: horizontal (xz) component by wh, vertical by wv."""
            along = sum((p[i] - rc[i]) * axis[i] for i in range(3))
            cpt = [rc[i] + along * axis[i] for i in range(3)]
            px, py, pz = p[0] - cpt[0], p[1] - cpt[1], p[2] - cpt[2]
            return [cpt[0] + wh * px, cpt[1] + wv * py, cpt[2] + wh * pz]

        def _proj_r(p, target):
            """Re-project p's xz radially (about the circle centre) to `target` radius, keeping y."""
            d = math.hypot(p[0] - cx, p[2] - cz) or 1.0
            return [cx + (p[0] - cx) * target / d, p[1], cz + (p[2] - cz) * target / d]

        # THE HORN (see the constants block): mouth-polar frame about the rim centroid, each rim vert
        # assigned an ellipse target at its own angular direction, cross-sections swept with widening
        # eased in as t^k. Radius grows linearly (reaches the wall regardless); only the SPREAD is eased,
        # so early cross-sections stay near mouth width while still travelling outward — the horn throat
        # passes the rock zone before the bell opens. k is solved below per mouth.
        # UNIFORM throat direction: every meridian extends along the MOUTH AXIS. A per-vert direction
        # field (native entrance dirs, snapped or interpolated) creases the surface along whichever
        # meridians deviate from their neighbours — the axis field is constant, so the throat is a clean
        # translated sweep of the mouth. Rock avoidance is the k-solver's job, not the throat paths'.
        rimP = _resample_closed(rim, _FLARE_RES)
        bc = math.degrees(math.atan2(rc[2] - cz, rc[0] - cx))
        yc = rc[1]
        pol = []                                         # per-vert [rim pt, dir, r0, phi]
        u_max = y_max = 1e-9
        for p in rimP:
            u0 = (math.degrees(math.atan2(p[2] - cz, p[0] - cx)) - bc + 180) % 360 - 180
            u_max = max(u_max, abs(u0)); y_max = max(y_max, abs(p[1] - yc))
        for p in rimP:
            u0 = (math.degrees(math.atan2(p[2] - cz, p[0] - cx)) - bc + 180) % 360 - 180
            phi = math.atan2((p[1] - yc) / y_max, u0 / u_max)  # direction about the mouth centre
            pol.append([list(p), axis, math.hypot(p[0] - cx, p[2] - cz), phi])

        def horn_pt(e, t, k):
            """Throat follows the vert's ENTRANCE-POLY direction (the proven rock-threading path); the
            ellipse spread blends in on top as t^k, so early cross-sections ride the native angles and
            the bell only opens once the surface is out of the rock zone."""
            p0, d, r0, phi = e
            r_t = r0 + (R + 2.0 - r0) * t
            base = _extend_along(p0, d, cx, cz, r_t)
            w = t ** k
            ub = (math.degrees(math.atan2(base[2] - cz, base[0] - cx)) - bc + 180) % 360 - 180
            u = ub + (_FLARE_ARC * math.cos(phi) - ub) * w
            y = base[1] + (yc + _FLARE_HALFH * math.sin(phi) - base[1]) * w
            b = math.radians(bc + u)
            return [cx + r_t * math.cos(b), y, cz + r_t * math.sin(b)]

        k = 1.0                                          # solve the gentlest k whose BELL clears the rock
        while k < 6.0:                                   # (t < 0.25 is throat, rim-locked, k-independent)
            ok = True
            for e in pol:
                for s in range(5, 20):
                    if _in_rock(horn_pt(e, s / 20.0, k)):
                        ok = False; break
                if not ok:
                    break
            if ok:
                break
            k += 0.25
        print(f'  horn mouth {li}: widening exponent k={k:.2f}')
        rings = [[horn_pt(e, s / _FLARE_SWEEP, k) for e in pol] for s in range(1, _FLARE_SWEEP + 1)]
        W = rings[-1]
        ring2 = [_proj_r(q, R + margin) for q in W]      # outer ring: caps safely outside the shell
        rings.append(ring2)
        tris += _bridge(rim, rings[0])                   # 8 -> _FLARE_RES merge-loft (manifold, no T-verts)
        for A, B in zip(rings, rings[1:]):
            m = len(A)
            for i in range(m):
                a, b = A[i], A[(i + 1) % m]
                c, dd = B[i], B[(i + 1) % m]
                tris += [[a, b, dd], [a, dd, c]]
        ec = _proj_r([sum(p[i] for p in ring2) / len(ring2) for i in range(3)], R + margin)
        for i in range(len(ring2)):                      # cap the outer ring -> closed cutter. The centroid
            tris.append([ec, ring2[i], ring2[(i + 1) % len(ring2)]])  # is re-projected onto the outer radius
    return tris                                          # so no cap chord dips back inside the wall


def _resample_closed(ring, N, aux=None):
    """Resample a closed polygon to N points, equally spaced by arclength (point 0 preserved). With `aux`
    (one vector per input vert, e.g. entrance directions), each output point also gets the arclength-
    blended, renormalised aux vector — so a per-vert direction field varies SMOOTHLY around the loop
    instead of snapping between exact-match verts and a fallback (which creases the swept surface along
    the lucky meridians). Returns points, or (points, aux_out) when aux is given."""
    n = len(ring)
    seg = [math.dist(ring[i], ring[(i + 1) % n]) for i in range(n)]
    total = sum(seg) or 1.0
    out, aout, k, acc = [], [], 0, 0.0
    for s in range(N):
        target = total * s / N
        while k < n - 1 and acc + seg[k] < target - 1e-9:
            acc += seg[k]; k += 1
        f = (target - acc) / (seg[k] or 1.0)
        a, b = ring[k], ring[(k + 1) % n]
        out.append([a[i] + f * (b[i] - a[i]) for i in range(3)])
        if aux is not None:
            da, db = aux[k], aux[(k + 1) % n]
            v = [da[i] + f * (db[i] - da[i]) for i in range(3)]
            L = math.sqrt(sum(c * c for c in v)) or 1.0
            aout.append([c / L for c in v])
    return (out, aout) if aux is not None else out


def _bridge(A, B):
    """Triangulate the annulus between two closed rings with different vert counts by merged arclength
    params — every boundary edge of A and B is used exactly once, so the result stays manifold."""
    def params(P):
        n = len(P)
        seg = [math.dist(P[i], P[(i + 1) % n]) for i in range(n)]
        total = sum(seg) or 1.0
        out, acc = [], 0.0
        for s in seg:
            out.append(acc / total); acc += s
        return out
    pa, pb = params(A), params(B)
    na, nb = len(A), len(B)
    tris, i, j = [], 0, 0
    while i < na or j < nb:
        nexta = pa[i + 1] if i + 1 < na else 1.0
        nextb = pb[j + 1] if j + 1 < nb else 1.0
        if j >= nb or (i < na and nexta <= nextb):
            tris.append([A[i % na], B[j % nb], A[(i + 1) % na]]); i += 1
        else:
            tris.append([A[i % na], B[j % nb], B[(j + 1) % nb]]); j += 1
    return tris


def hull_tris(round_to=2):
    """The finished iwa01 camera hull, computed headlessly (no Blender): cylinder shell MINUS the flared
    tunnel cutter MINUS the anti-climb corner box (sequential exact CSG differences), top/bottom caps
    stripped. Returns game-space triangles. Requires trimesh+manifold3d; used only to (re)freeze
    brownboo_rock_hull_data.py, never at bake time."""
    import trimesh
    cx, cz, R = _iwa01_visual_circle()
    cv, cf = _weld(_cylinder_tris(cx, cz, R, _IWA01_BOT, _IWA01_TOP))
    tv, tf = _weld(_tunnel_solid_tris(cx, cz, R))
    cyl = trimesh.Trimesh(vertices=cv, faces=cf, process=True); cyl.fix_normals()
    tun = trimesh.Trimesh(vertices=tv, faces=tf, process=True); tun.fix_normals()
    # (the anti-climb corner box was tried here — cyl-tun-box sequential difference — and dropped:
    #  squaring the bore's upper walls didn't help the camera in-game; its code was removed 2026-09.)
    hull = cyl.difference(tun, engine='manifold')
    ymin, ymax = hull.vertices[:, 1].min(), hull.vertices[:, 1].max()
    out = []
    for fi, f in enumerate(hull.faces):
        n = hull.face_normals[fi]
        cy = hull.vertices[f][:, 1].mean()
        if abs(n[1]) > 0.9 and (abs(cy - ymax) < 0.6 or abs(cy - ymin) < 0.6):
            continue                                     # drop the flat top/bottom cap faces
        if hull.area_faces[fi] < 0.05:
            continue                                     # drop Boolean sliver/degenerate faces
        out.append([[round(float(hull.vertices[i][k]), round_to) for k in range(3)] for i in f])
    return out


def freeze_hull(path):
    """Write the computed hull to a plain-literal Python module the bake can import with no CSG deps."""
    tris = hull_tris()
    with open(path, 'w') as fh:
        fh.write('# GENERATED by tools/brownboo/brownboo_rock_hull_builder.py (freeze_hull) — do not edit by hand.\n')
        fh.write('# iwa01 camera-collision hull: cylinder shell around the rock MINUS the flared tunnel\n')
        fh.write('# cutter (exact CSG difference), top & bottom caps stripped. Game-space triangles,\n')
        fh.write('# each [[x,y,z],[x,y,z],[x,y,z]]. Regenerate by running brownboo_rock_hull_builder.py.\n')
        fh.write('IWA01_HULL = [\n')
        for t in tris:
            fh.write(f'    {t!r},\n')
        fh.write(']\n')
    return len(tris)


if __name__ == '__main__':
    import os
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'brownboo_rock_hull_data.py')
    n = freeze_hull(out)
    print(f'froze {n} hull tris -> {out}')
