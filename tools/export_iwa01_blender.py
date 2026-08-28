#!/usr/bin/env python3
"""Generate a self-contained Blender script for hand-authoring the iwa01 camera-collision hull.

Run this once (needs DC1_DATA_DIR via .env) to bake the geometry into a standalone Blender .py
with no external deps. Open Blender, load the emitted script in the Text Editor, and Run — you get
TWO watertight solid objects to combine with a Boolean modifier:

  cylinder_solid   the enclosing circle as a closed, capped cylinder (the shell around the rock)
  tunnel_solid     the tunnel passage (bore + both collars) as a closed tube, each mouth extended
                   out PAST the cylinder wall and capped, so a Boolean cuts clean through

  python3 tools/export_iwa01_blender.py            # -> game_data/blender/iwa01_scene.py

Both are manifold (welded verts, every edge shared by exactly two faces) so Blender's exact Boolean
solver produces a clean result: Difference (cylinder minus tunnel) carves the passage; Union merges
them. Recalculate normals (Shift+N) after Booleaning if faces look inside-out.
"""
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
    tunnel cutter via an exact CSG difference, top/bottom caps stripped. Returns game-space triangles.
    Requires trimesh+manifold3d; used only to (re)freeze iwa01_hull_data.py, never at bake time."""
    import trimesh
    cx, cz, R = _iwa01_visual_circle()
    cv, cf = _weld(_cylinder_tris(cx, cz, R, _IWA01_BOT, _IWA01_TOP))
    tv, tf = _weld(_tunnel_solid_tris(cx, cz, R))
    cyl = trimesh.Trimesh(vertices=cv, faces=cf, process=True); cyl.fix_normals()
    tun = trimesh.Trimesh(vertices=tv, faces=tf, process=True); tun.fix_normals()
    hull = cyl.difference(tun, engine='manifold')
    ymin, ymax = hull.vertices[:, 1].min(), hull.vertices[:, 1].max()
    out = []
    for fi, f in enumerate(hull.faces):
        n = hull.face_normals[fi]
        cy = hull.vertices[f][:, 1].mean()
        if abs(n[1]) > 0.9 and (abs(cy - ymax) < 0.6 or abs(cy - ymin) < 0.6):
            continue                                     # drop the flat top/bottom cap faces
        out.append([[round(float(hull.vertices[i][k]), round_to) for k in range(3)] for i in f])
    return out


def freeze_hull(path):
    """Write the computed hull to a plain-literal Python module the bake can import with no CSG deps."""
    tris = hull_tris()
    with open(path, 'w') as fh:
        fh.write('# GENERATED by tools/export_iwa01_blender.py (freeze_hull) — do not edit by hand.\n')
        fh.write('# iwa01 camera-collision hull: cylinder shell around the rock MINUS the flared tunnel\n')
        fh.write('# cutter (exact CSG difference), top & bottom caps stripped. Game-space triangles,\n')
        fh.write('# each [[x,y,z],[x,y,z],[x,y,z]]. Regenerate by running export_iwa01_blender.py.\n')
        fh.write('IWA01_HULL = [\n')
        for t in tris:
            fh.write(f'    {t!r},\n')
        fh.write(']\n')
    return len(tris)


def main():
    cx, cz, R = _iwa01_visual_circle()

    cyl_v, cyl_f = _weld(_cylinder_tris(cx, cz, R, _IWA01_BOT, _IWA01_TOP))
    tun_v, tun_f = _weld(_tunnel_solid_tris(cx, cz, R))

    objs = {
        'cylinder_solid': (cyl_v, cyl_f, (0.60, 0.62, 0.68)),
        'tunnel_solid':   (tun_v, tun_f, (0.20, 0.70, 0.90)),
    }

    out_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'game_data', 'blender')
    os.makedirs(out_dir, exist_ok=True)
    out_path = os.path.join(out_dir, 'iwa01_scene.py')

    with open(out_path, 'w') as fh:
        fh.write(_HEADER)
        fh.write('OBJECTS = {\n')
        for name, (verts, faces, colour) in objs.items():
            fh.write(f'  {name!r}: dict(\n')
            fh.write(f'    verts={verts!r},\n')
            fh.write(f'    faces={faces!r},\n')
            fh.write(f'    colour={colour!r}),\n')
        fh.write('}\n')
        fh.write(_BODY)

    print(f'wrote {out_path}')
    print(f'  circle centre ({cx:.1f},{cz:.1f}) R={R:.1f}  y[{_IWA01_BOT},{_IWA01_TOP}]')
    print(f'  cylinder_solid: {len(cyl_v)} verts, {len(cyl_f)} tris, non-manifold edges: {_nonmanifold(cyl_f)}')
    print(f'  tunnel_solid:   {len(tun_v)} verts, {len(tun_f)} tris, non-manifold edges: {_nonmanifold(tun_f)}')
    print('  emitted script cuts iwa01_hull = cylinder_solid MINUS tunnel_solid (Boolean, applied)')

    # freeze the SAME cut headlessly so the bake needs no CSG deps
    data_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'iwa01_hull_data.py')
    try:
        nfaces = freeze_hull(data_path)
        print(f'  froze {nfaces}-tri hull -> {data_path}')
    except ImportError as e:
        print(f'  [skip freeze] {e} (pip install trimesh manifold3d scipy networkx to enable)')


# ── Blender-side template (the emitted script is DATA above + this code) ──────────────────────

_HEADER = '''"""GENERATED by tools/export_iwa01_blender.py — do not edit by hand; re-run the generator.

Open in Blender's Text Editor and press Run. It builds `cylinder_solid` (the closed shell around the
rock) and `tunnel_solid` (the passage as a closed cutter — bore + collars, each mouth flared along
its OWN entrance-poly angle out through the wall), then AUTOMATICALLY cuts the hole: it Boolean-
Differences the cylinder by the tunnel, applies it, and deletes the cylinder's top & bottom cap faces,
leaving `iwa01_hull` — the OPEN wall with two flared tunnel openings plus the bored passage walls.
That is the finished camera-collision mesh. cylinder_solid + tunnel_solid are kept (hidden) for
reference. Recalculate normals (Shift+N) on iwa01_hull if any face looks inside-out.

Coordinates are RAW GAME SPACE mapped game (x,y,z) -> Blender (x, z, y) so the rock stands upright
(game +Y up becomes Blender +Z up). The export-back note at the bottom maps your result back.
"""
import bpy, bmesh

'''

_BODY = '''

def _g2b(p):
    """game (x,y,z) -> Blender (x, z, y): game Y-up becomes Blender Z-up."""
    return (p[0], p[2], p[1])


def _ensure_collection(name):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    return col


def _clear(col):
    for ob in list(col.objects):
        bpy.data.objects.remove(ob, do_unlink=True)


def _mat(name, rgb):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = False
    m.diffuse_color = (rgb[0], rgb[1], rgb[2], 1.0)
    return m


def _recalc_outward(me):
    """Make face normals coherent and outward — the hand-authored bore/collar tris and the generated
    loft/cap tris don't share a global winding, and the EXACT Boolean needs a consistently-wound solid
    or it treats one mouth as closed. Weld first so shared edges connect, then recalc."""
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.01)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    me.update()


def _make(col, name, verts, faces, rgb):
    me = bpy.data.meshes.new(name)
    me.from_pydata([_g2b(v) for v in verts], [], [list(f) for f in faces])
    me.validate()
    me.update()
    _recalc_outward(me)
    ob = bpy.data.objects.new(name, me)
    ob.data.materials.append(_mat(name + '_mat', rgb))
    ob.color = (rgb[0], rgb[1], rgb[2], 1.0)
    ob.show_wire = True
    col.objects.link(ob)
    return ob


def _cut_hull(col, base, cutter):
    """iwa01_hull = base (cylinder) MINUS cutter (tunnel), Boolean-applied. base/cutter left hidden."""
    hull = base.copy()
    hull.data = base.data.copy()
    hull.name = 'iwa01_hull'
    hull.data.materials.clear()
    hull.data.materials.append(_mat('iwa01_hull_mat', (0.80, 0.78, 0.55)))
    hull.color = (0.80, 0.78, 0.55, 1.0)
    hull.show_wire = True
    col.objects.link(hull)

    m = hull.modifiers.new('iwa01_cut', 'BOOLEAN')
    m.operation = 'DIFFERENCE'
    m.solver = 'EXACT'
    m.object = cutter
    bpy.context.view_layer.objects.active = hull
    for ob in bpy.context.selected_objects:
        ob.select_set(False)
    hull.select_set(True)
    bpy.ops.object.modifier_apply(modifier=m.name)
    _strip_caps(hull.data)                               # open the cylinder: drop top & bottom faces

    base.hide_set(True)
    cutter.hide_set(True)
    return hull


def _strip_caps(me):
    """Delete the cylinder's flat top & bottom faces so only the wall (with tunnel openings) + the bored
    passage walls remain — the finished open camera shell. Caps are the horizontal faces sitting at the
    mesh's min/max height (Blender Z, which is game Y); every other face is near-vertical wall."""
    bm = bmesh.new()
    bm.from_mesh(me)
    zs = [v.co.z for v in bm.verts]
    zmax, zmin = max(zs), min(zs)
    tol = 0.5
    kill = [f for f in bm.faces
            if abs(f.normal.z) > 0.9
            and (abs(f.calc_center_median().z - zmax) < tol or abs(f.calc_center_median().z - zmin) < tol)]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bm.to_mesh(me)
    bm.free()
    me.update()


def build():
    col = _ensure_collection('iwa01_ref')
    _clear(col)
    made = {name: _make(col, name, d['verts'], d['faces'], d['colour']) for name, d in OBJECTS.items()}
    hull = _cut_hull(col, made['cylinder_solid'], made['tunnel_solid'])
    # shade viewport by object colour so the per-object colours show without material mode
    for area in bpy.context.screen.areas if bpy.context.screen else []:
        if area.type == 'VIEW_3D':
            area.spaces[0].shading.color_type = 'OBJECT'
    print('iwa01_ref built: iwa01_hull (cut, caps stripped -> open camera shell) + cylinder_solid, '
          f'tunnel_solid (hidden refs); hull faces={len(hull.data.polygons)}')


build()

# ── EXPORT-BACK NOTE ─────────────────────────────────────────────────────────────────────────
# When the hull is how you want it (edit "iwa01_hull", or whatever object you finish with), run this
# in the Text Editor to print game-space triangles ready to paste into brownboo_camera_collision.py.
# It triangulates n-gons and maps Blender (x,y,z) back to game (x, z, y):
#
#   import bpy, bmesh
#   ob = bpy.data.objects['iwa01_hull']
#   bm = bmesh.new(); bm.from_mesh(ob.data)
#   bmesh.ops.triangulate(bm, faces=bm.faces)
#   for f in bm.faces:
#       t = [(round(v.co.x,2), round(v.co.z,2), round(v.co.y,2)) for v in f.verts]
#       print('  ' + ' '.join(f'{x} {y} {z}' for (x,y,z) in t))
#   bm.free()
'''


if __name__ == '__main__':
    main()
