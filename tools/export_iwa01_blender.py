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


def _cylinder_tris(cx, cz, R, ybot, ytop, n=48):
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


_FLARE_WIDEN = 2.0   # cross-section scale at the wall (>1 widens the opening so it funnels in gradually).
                     # Free to exceed the shell's y[-15,92]: the Boolean clamps the hole to the cylinder,
                     # so an over-tall mouth just yields a full-height opening. The wall ring stays past R
                     # regardless (the along-axis extension reaches the wall before widening spreads it).
_FLARE_LEAD = 18.0   # straight, UNWIDENED lead-in (game units): each entrance poly is extended along its
                     # own angle by this much as ONE band (no subdivision — the angle isn't changing), so
                     # the widening starts out past the rock instead of bulging into it. Tune to taste.
_FLARE_WIDEN_RINGS = 2  # how many rings the widening funnel (neck -> wall) is split into. Only this part
                        # is subdivided; the straight lead-in stays a single band.


def _tunnel_solid_tris(cx, cz, R, margin=8.0):
    """Bore + both collars closed into a watertight CUTTER. Weld the passage surface, find its real open
    boundary loops (the two mouths), and flare each mouth out through the cylinder wall (radius R+margin)
    along ITS OWN entrance poly's angle, WIDENING the cross-section as it goes so the wall opening is
    bigger than the bore and funnels in gradually. Each extension is subdivided into _FLARE_SEGS rings
    for a smooth funnel, then capped. A Boolean Difference of the cylinder minus this cutter drills the
    tunnel: the cavity walls ARE this bore/collar/flare surface, the exit holes the widened mouths."""
    surf = ([list(map(list, t)) for t in _IWA01_INTERIOR]
            + [list(map(list, t)) for t in _IWA01_WEST]
            + [list(map(list, t)) for t in _IWA01_EAST])
    verts, faces = _weld(surf)
    tris = [[verts[i] for i in f] for f in faces]        # passage surface (welded, re-exploded to tris)

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
        neck, endpt = [], []                             # per-vert straight-extension point and wall landing
        for v in rim:
            d = dirs.get(tuple(round(c, 2) for c in v), axis)
            if math.hypot(d[0], d[2]) < 0.2:             # near-vertical poly -> can't reach radius; use axis
                d = axis
            e = _extend_along(v, d, cx, cz, R + margin)
            lead = min(_FLARE_LEAD, 0.9 * math.dist(v, e))           # keep the neck short of the wall
            neck.append([v[i] + d[i] * lead for i in range(3)])
            endpt.append(e)

        def _widen(p):                                   # scale p's offset from the mouth centreline by w
            def f(w):
                along = sum((p[i] - rc[i]) * axis[i] for i in range(3))
                cpt = [rc[i] + along * axis[i] for i in range(3)]
                return [cpt[i] + w * (p[i] - cpt[i]) for i in range(3)]
            return f

        # rings: bore rim -> neck (ONE straight band, angle unchanged) -> _FLARE_WIDEN_RINGS widening rings
        rings = [list(rim), list(neck)]                  # first two rings are straight (w = 1)
        for k in range(1, _FLARE_WIDEN_RINGS + 1):
            frac = k / _FLARE_WIDEN_RINGS
            w = 1.0 + (_FLARE_WIDEN - 1.0) * frac
            ring = []
            for nk, e in zip(neck, endpt):
                pos = [nk[i] + (e[i] - nk[i]) * frac for i in range(3)]  # slide neck -> wall
                ring.append(_widen(pos)(w))                             # widen only here
            rings.append(ring)
        n = len(rim)
        for s in range(len(rings) - 1):                  # loft consecutive rings
            A, B = rings[s], rings[s + 1]
            for i in range(n):
                a, b = A[i], A[(i + 1) % n]
                c, dd = B[i], B[(i + 1) % n]
                tris += [[a, b, dd], [a, dd, c]]
        last = rings[-1]                                 # cap the widened wall ring -> closed cutter
        ec = [sum(p[i] for p in last) / len(last) for i in range(3)]
        for i in range(n):
            tris.append([ec, last[i], last[(i + 1) % n]])
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
