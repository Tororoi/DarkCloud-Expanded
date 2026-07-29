#!/usr/bin/env python3
"""Bake CAMERA + PLAYER collision into a town's ground `_a`, with the invisible walls separated as PLAYER-ONLY.

Sources (all origin-placed grounds e03g04/e03g05, so world tris == sub-file-local):
  * CAMERA+PLAYER: the specified VISIBLE structure meshes (grid3*, obj40/44 bridges, obj1/9 pipes,
    obj6/33/34/43/45) PLUS the obj42 short-wall COLLISION (from the vanilla `_a`), minus any tris that are in
    the invisible-wall list.
  * PLAYER-ONLY: the invisible walls (tools/invisible_walls.py) — collision-only canal-containment tris.

Structure of the rebuilt `_a` MDS (per ground):
    aroot (empty, parent -1)
      camcol (empty, parent 0)          <- CAMERA frame points here (mod)
        <structure/shortwall nodes>     parent 1  (camera + player)
      <invisible nodes>                 parent 0  (player only; siblings of camcol)
Player gathers from aroot (root, +0x16010) -> everything. Camera gathers from camcol (+0x1601c) -> only its
children, excluding the invisible siblings. PickUpNearPoly__6CFrame recurses children, each CCollisionMDT
self-culls on its bbox. Every node <=100 polys, unique name within the sub-file.

  bake_structures(scene_rel, town='e03', max_tris=100) -> (new_scn, stats, manifest)
"""
import os, sys, struct, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract_scene_mesh import load_scene, xform
import scene_placed
from scene_placed import placed_meshes
from build_coll_mdt import build_coll_mdt
from bake_terrain_camera_collision import kd_split, _replace_a_block
from invisible_walls import invisible_tris
from perimeter_wall import perimeter_wall_tris
import georama_collision as gc


def _num(prefix, n):
    m = re.match(prefix + r'(\d+)', n)
    return int(m.group(1)) if m else None


def is_cam_node(nm):
    # obj42 here = the VISIBLE short-wall mesh (y70-76), NOT the 626-tri collision node named obj42 inside the
    # ground `_a` (that's the town-wide player collision with the tall invisible walls — unrelated, dropped).
    # No 'kanban' — the injected fishing sign isn't a terrain structure and has no ground-style `_a`.
    return nm.startswith('grid3') or _num('obj', nm) in (40, 44, 1, 9, 6, 33, 34, 43, 45, 42)


def _key(t):
    return tuple(sorted(tuple(round(c, 1) for c in p) for p in t))


def _obj42_coll(scn):
    """obj42 short-wall COLLISION tris (world = local), per ground sub-file that contains it."""
    DIR = scene_placed._scndir(scn)
    out = {}
    for g in [n for n in DIR if re.match(r'e03g\d\d$', n)]:
        off, size = DIR[g]; sub = scn[off:off + size]; vo = gc._variant_a(sub, g)
        if vo is None:
            continue
        mds = off + vo; nodes, wm = scene_placed._accum(scn, mds)
        tris = []
        for i, (nn, mo, par, mat) in enumerate(nodes):
            if nn != 'obj42':
                continue
            fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c + 3] == b'MDT'), None)
            if not fo:
                continue
            M = wm(i)
            for a, b, c in gc.parse_coll_mdt(scn, fo):
                tris.append([list(xform(M, a)), list(xform(M, b)), list(xform(M, c))])
        if tris:
            out[g] = tris
    return out


def _unique_names(names):
    cnt = {}
    for n in names:
        cnt[n] = cnt.get(n, 0) + 1
    occ, out = {}, []
    for n in names:
        if cnt[n] > 1:
            k = occ.get(n, 0); occ[n] = k + 1
            out.append(f'{n}_{k}')
        else:
            out.append(n)
    return out


def _fit(name, used, maxlen=15):
    cand = name[:maxlen]; k = 0
    while cand in used:
        k += 1; suf = '~' + str(k); cand = name[:maxlen - len(suf)] + suf
    used.add(cand)
    return cand


def build_flat_mds(named):
    """named: [(node_name, [tri,...]), ...]. Build a flat `_a` (node 0 root, rest its children). Camera and
    player both gather the whole thing — the 5-unit canal walls clear the camera by height, so no camera/
    player split is needed."""
    n = len(named)
    header = struct.pack('<4sIII', b'MDS\x00', 1, n, 0x10)
    table = bytearray(); blob = bytearray()
    cur = 0x10 + n * 0x70
    for i, (nm, t) in enumerate(named):
        node = bytearray(0x70)
        struct.pack_into('<II', node, 0, 0, 0x70)
        b = nm.encode('latin1', 'replace')[:15]
        node[8:8 + len(b)] = b
        mdt = build_coll_mdt(t)
        struct.pack_into('<i', node, 0x28, cur)
        blob += mdt; cur += len(mdt)
        struct.pack_into('<i', node, 0x2c, -1 if i == 0 else 0)
        struct.pack_into('<16f', node, 0x30, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)
        table += node
    return header + bytes(table) + bytes(blob)


def bake_structures(scene_rel, town='e03', max_tris=100):
    scn = load_scene(scene_rel)
    P = placed_meshes(scene_rel, scene_rel.replace('scene.scn', 'mapinfo.cfg'))
    from collections import defaultdict
    bysub = defaultdict(list)
    for pm in P:
        if not is_cam_node(pm['name']):
            continue
        v = pm['verts']
        tris = [[list(v[a]), list(v[b]), list(v[c])] for a, b, c in pm['tris']]
        if tris:
            bysub[pm['sub']].append((pm['name'], tris))

    inv = invisible_tris(town)

    stats, manifest = [], []
    dirnames = set(scene_placed._scndir(scn).keys())
    subs = sorted(bysub)
    anchored = False
    for sub in subs:
        if sub not in dirnames:            # skip anything that isn't a real SCN sub-file (e.g. injected parts)
            continue
        srcs = list(bysub.get(sub, []))                            # structures + short walls from VISIBLE meshes
        if not anchored:                                           # town-wide walls go on the first ground
            # The vanilla ground `_a` (the 626-tri obj42 collision node etc.) is DROPPED — it carries the tall
            # canal-containment invisible walls. We replace it with our own detailed collision.
            srcs.append(('perimeter', perimeter_wall_tris(town)))  # perimeter wall keeps the camera in town
            if inv:
                srcs.append(('canal', inv))                        # 5-unit canal walls (clear the camera by height)
            anchored = True
        unames = _unique_names([nm for nm, _ in srcs])
        named, used = [], set()
        for (nm, tris), uname in zip(srcs, unames):
            for bi, bk in enumerate(kd_split(tris, max_tris)):
                mn = _fit(f'{uname}#{bi}', used, 15)
                named.append((mn, bk)); manifest.append((sub, nm, mn, len(bk), 'shared'))
        mds = build_flat_mds(named)
        scn, delta = _replace_a_block(scn, sub, mds)
        tris_ct = sum(len(b) for _, b in named)
        stats.append((sub, len(srcs), len(named), 0, tris_ct, 0, delta))
    return scn, stats, manifest


def bake_structures_from_bytes(scene_rel, scene_bytes, mapinfo_bytes=None, town='e03', max_tris=100):
    """bake_structures with the scene (and optionally mapinfo) supplied as bytes rather than read from disk —
    for baking straight out of an ISO."""
    import extract_scene_mesh as esm
    mapinfo_rel = scene_rel.replace('scene.scn', 'mapinfo.cfg')

    def patched(rel, _o=esm.load_scene):
        if rel == scene_rel:
            return scene_bytes
        if mapinfo_bytes is not None and rel == mapinfo_rel:
            return mapinfo_bytes
        return _o(rel)

    saved = (esm.load_scene, scene_placed.load_scene, gc.load_scene)
    esm.load_scene = scene_placed.load_scene = gc.load_scene = patched
    try:
        return bake_structures(scene_rel, town, max_tris)
    finally:
        esm.load_scene, scene_placed.load_scene, gc.load_scene = saved


if __name__ == '__main__':
    scene = sys.argv[1] if len(sys.argv) > 1 else 'gedit/e03/scene.scn'
    new_scn, stats, manifest = bake_structures(scene)
    grew = sum(s[-1] for s in stats)
    print(f"shared-collision bake: {len(stats)} sub-files, scene grew {grew:+} bytes")
    for sub, ns, cn, iv, ct, it, d in stats:
        print(f"  {sub}: {cn} collision nodes ({ct} tris) from {ns} sources  (Δ{d:+})")
    # validate: within-sub-file name uniqueness (flat _a, no camcol/aroot)
    DIR = scene_placed._scndir(new_scn)
    for g in ('e03g04', 'e03g05'):
        off, size = DIR[g]; sub = new_scn[off:off + size]; vo = gc._variant_a(sub, g); mds = off + vo
        cnt, tbl = struct.unpack_from('<II', new_scn, mds + 8)
        names = [new_scn[mds + tbl + i * 0x70 + 8:mds + tbl + i * 0x70 + 8 + 16].split(b'\x00')[0].decode('latin1') for i in range(cnt)]
        print(f"  {g}_a: {cnt} nodes, names-unique={len(set(names)) == cnt}")
