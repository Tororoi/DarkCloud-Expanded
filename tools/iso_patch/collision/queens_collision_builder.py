#!/usr/bin/env python3
"""Bake CAMERA + PLAYER collision for a town into its scene.scn ground meshes. Single home for the whole Queens
(e03) collision bake: the hand-authored geometry (BOTH-frame walls / pipe drums / flat quads, player-only
railings + canal containment, the generated perimeter wall — formerly the both_walls / player_walls /
invisible_walls / perimeter_wall modules) AND the orchestration that groups it and splices it into the scene.

Two collision variants per ground sub-file (both origin-placed, so world tris == sub-file-local):
  * PLAYER `_a` (part+0x14 -> frame +0xd0): simplified structure meshes + perimeter + BOTH-frame walls + canal
    invisible walls + railings + the loading-zone trigger quads (attribute-tagged). Split PER ground sub.
  * CAMERA `_c` (part+0x20 -> frame +0xdc): simplified structure meshes + perimeter + BOTH-frame walls ONLY
    (no canal/railings/triggers = player-only). Consolidated onto the one sub that ships a `_c` variant.
Buildings keep their vanilla `_a`/`_c` (buildings=False). grouped_collision() pools each frame's tris and
kd_splits them into <=100-poly, spatially-compact nodes (tight bbox = free runtime gather culling); it is shared
with queens_viewer.py so both write / show the identical grouping.

Split 2026-09 into: collision_mds_writer.py (MDS splice + flat-MDS serialiser), collision_geom.py (pure tri math +
coplanar merge), queens_terrain_collision_data.py (all authored Queens geometry, removal regions, directed camera jobs),
and this façade (scene introspection, grouped_collision, the bake entry points). The names other tools import
from here (build_flat_mds, _replace_a_block, grouped_collision, trigger_nodes, load_scene,
bake_structures_from_bytes) are re-exported below. ISO wrapper: iso_patch/patch_iso_town_collision.py.

  bake_structures(scene_rel, town='e03', max_tris=100) -> (new_scn, stats, manifest)
"""
import os, sys, struct, re, math
_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)                                     # this dir (collision_mds_writer / collision_geom / queens_terrain_collision_data)
sys.path.insert(0, os.path.dirname(os.path.dirname(_HERE)))   # tools/ (scene_placed, mdt_codec, georama_collision…)
from extract_scene_mesh import load_scene, xform
import scene_placed
from scene_placed import placed_meshes
import mdt_codec
from tri_util import kd_split
import georama_collision as gc
from collision_mds_writer import _variant_off, _replace_a_block, _unique_names, fit_node_name, build_flat_mds, _pool_split
from queens_terrain_collision_data import (camera_tris, flat_ground_tris, pipe_drum_tris, both_wall_tris, player_wall_tris,
                                invisible_tris, perimeter_wall_tris, simplify_terrain, fix_camera_winding,
                                cam_merge_selected, gate_torch_simplify)


def node_index(prefix, n):
    m = re.match(prefix + r'(\d+)', n)
    return int(m.group(1)) if m else None


def is_camera_structure_node(nm):
    # obj42 here = the VISIBLE short-wall mesh (y70-76), NOT the 626-tri collision node named obj42 inside the
    # ground `_a` (that's the town-wide player collision with the tall invisible walls — unrelated, dropped).
    # No 'kanban' — the injected fishing sign isn't a terrain structure and has no ground-style `_a`.
    # obj1/obj9 (the canal pipes) are EXCLUDED here — their hollow tube collision is replaced by solid octagonal
    # drums (both_walls.pipe_drum_tris).
    return nm.startswith('grid3') or node_index('obj', nm) in (40, 44, 6, 33, 34, 43, 45, 42)


def short_wall_collision(scn):
    """obj42 short-wall COLLISION tris (world = local), per ground sub-file that contains it."""
    DIR = scene_placed.scn_directory_map(scn)
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


def trigger_nodes(scn):
    """Event-trigger collision quads baked into each ground `_a` (the loading zones): tris whose colour-block
    entry has a non-zero destination tag (the +0x40 short GetEventPoly reads). Returns
    {sub: [(node_name, [tri,...], [colour_entry_16b,...]), ...]}. These MUST survive into the rebuilt `_a`,
    else EdEventPointCpPoly gathers no tagged poly at the event point and the town exit stops working.
    (Queens e03: e03g04 nodes 'map' dest=1 / 'minato' dest=3; e03g05 node 'obj41_2' dest=2.)"""
    DIR = scene_placed.scn_directory_map(scn)
    out = {}
    for g in [n for n in DIR if re.match(r'e03g\d\d$', n)]:
        off, size = DIR[g]; sub = scn[off:off + size]; vo = gc._variant_a(sub, g)
        if vo is None:
            continue
        mds = off + vo; nodes, wm = scene_placed._accum(scn, mds)
        found = []
        for ni, (nn, mo, par, mat) in enumerate(nodes):
            if mo == 0:
                continue
            fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c + 3] == b'MDT'), None)
            if not fo:
                continue
            w = struct.unpack_from('<16I', scn, fo)
            POS, DL, COL = w[4], w[10], w[14]
            tc = struct.unpack_from('<I', scn, fo + DL + 0x14)[0]; rb = fo + DL + 0x18
            M = wm(ni)
            tris, ents = [], []
            for t in range(tc):
                i0, i1, i2, ci, _pad = struct.unpack_from('<5i', scn, rb + t * 0x14)
                if not (COL and ci >= 0):
                    continue
                ent = scn[fo + COL + ci * 0x10: fo + COL + ci * 0x10 + 0x10]
                if len(ent) < 0x10 or (struct.unpack_from('<H', ent, 0)[0] == 0):   # +0x40 short 0 = plain surface
                    continue
                def V(i):
                    p = struct.unpack_from('<3f', scn, fo + POS + i * 0x10)
                    return list(xform(M, p))
                tris.append([V(i0), V(i1), V(i2)]); ents.append(ent)
            if tris:
                found.append((nn, tris, ents))
        if found:
            out[g] = found
    return out


def _face_ny(t):
    """|unit face-normal.y| of a triangle. ~1 = horizontal (roof/floor), ~0 = vertical (wall)."""
    (x1, y1, z1), (x2, y2, z2), (x3, y3, z3) = t
    ux, uy, uz = x2 - x1, y2 - y1, z2 - z1
    vx, vy, vz = x3 - x1, y3 - y1, z3 - z1
    ny = uz * vx - ux * vz
    L = math.sqrt((uy * vz - uz * vy) ** 2 + ny * ny + (ux * vy - uy * vx) ** 2)
    return abs(ny) / L if L else 1.0


def _building_lods(scn, off, sub):
    """Absolute offsets of a building sub-file's VISIBLE-mesh LODs, in decreasing detail. The sub-file leads
    with the LOD chain (MDS#0 full .. MDS#k coarsest) before the collision/shadow blocks; take the leading run
    of MDS blocks that actually decode to triangles."""
    lods = []
    for m in re.finditer(b'MDS\x00', sub):
        mds = off + m.start()
        nodes, wm = scene_placed._accum(scn, mds)
        tc = 0
        for i, (nn, mo, par, mat) in enumerate(nodes):
            if mo == 0:
                continue
            fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c + 3] == b'MDT'), None)
            if fo:
                try:
                    tc += len(scene_placed._flatten(mdt_codec.parse_mdt(scn, fo)))
                except Exception:
                    pass
        if tc == 0:
            break                                    # end of the LOD chain (shadow/collision blocks follow)
        lods.append(mds)
    return lods


def building_collision_nodes(scn, max_tris=100, wall_max_ny=None, lod=2):
    """Per e03h* building sub-file: a coarse LOD of its VISIBLE mesh decoded in BUILDING-LOCAL space (node
    matrices only, NO mapinfo placement — buildings are georama PARTS, transformed at runtime), kd-split into
    <=max_tris nodes. Returns {sub: [(node_name, local_tris), ...]}. The georama part placement moves these at
    runtime exactly like the native multi-node building `_a` (e03h01 ships 6: obj7/car2/car1/car3/grid43/lt1).
    lod picks the LOD (0=full detail .. clamped to the coarsest available); the town-load memory pool
    (CDataAlloc2 @0x1d3a050, holds meshes+collision, hangs on overflow) is TIGHT, and the full mesh (LOD0, ~17.5k
    tris → ~2.6MB of pool) overflows it — a coarse LOD stays COMPLETE (same bbox, roofs walkable) at ~1/3 the
    tris. The whole (coarse) mesh is kept — several Queens buildings have WALKABLE roofs, so dropping horizontal
    faces left holes. Optional wall_max_ny (0..1) additionally keeps only |face-normal.y| <= it — off by default."""
    DIR = scene_placed.scn_directory_map(scn)
    out = {}
    for g in sorted(n for n in DIR if re.match(r'e03h\d\d$', n)):
        off, size = DIR[g]; sub = scn[off:off + size]
        if gc._variant_a(sub, g) is None:            # only buildings that ship an `_a` (placed + collidable)
            continue
        lods = _building_lods(scn, off, sub)
        if not lods:
            continue
        mds = lods[min(lod, len(lods) - 1)]          # coarsest available at/below the requested LOD
        nodes, wm = scene_placed._accum(scn, mds)
        tris = []
        for i, (nn, mo, par, mat) in enumerate(nodes):
            if mo == 0:
                continue
            fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c + 3] == b'MDT'), None)
            if not fo:
                continue
            try:
                m = mdt_codec.parse_mdt(scn, fo)     # strict visible-mesh decode (same as placed_meshes)
            except Exception:
                continue
            M = wm(i)
            lv = [list(xform(M, (p[0], p[1], p[2]))) for p in m.pos]   # BUILDING-LOCAL (node matrix only)
            for a, b, c in scene_placed._flatten(m):
                tris.append([lv[a], lv[b], lv[c]])
        if wall_max_ny is not None:
            tris = [t for t in tris if _face_ny(t) <= wall_max_ny]
        if not tris:
            continue
        stem = g[3:]                                  # 'e03h01' -> 'h01' (short, unique node-name base)
        named = [(f'{stem}w{bi}', bk) for bi, bk in enumerate(kd_split(tris, max_tris))]
        out[g] = named
    return out


def grouped_collision(placed, scn, town='e03', max_tris=100):
    """Spatially REGROUP the whole custom collision, shared by the ISO bake (bake_structures) and the viewer so
    both write / show the identical node grouping.

    All non-trigger tris of a frame are pooled and kd_split into <=max_tris nodes (nearby polys share a node).
    Player `_a` stays split per ground sub-file (each sub keeps its own collision; the town-wide hand-authored
    walls anchor on the first ground). Camera `_c` is consolidated on the single sub that ships a `_c` variant.
    Returns {'subs':[...], 'player':{sub:(named, trigs)}, 'camera':[named], 'sets':{...}} where
      named = [(name, tris)], trigs = [(name, tris, colour_entries)], and 'sets' holds the raw component tri
      lists ('structure','bwalls','perimeter','invisible') for the viewer's isolate-this-piece toggles."""
    from collections import defaultdict
    bysub = defaultdict(list)
    cam_bysub = defaultdict(list)                                  # camera structure pool, MINUS the own-node meshes
    cam_own = defaultdict(list)                                    # obj40/obj44: each ships as its OWN camera node
    own_names = {'obj40', 'obj44'} if town == 'e03' else set()    # identical ~216-tri gate meshes
    for pm in placed:
        if not is_camera_structure_node(pm['name']):
            continue
        v = pm['verts']
        t = simplify_terrain([[list(v[a]), list(v[b]), list(v[c])] for a, b, c in pm['tris']])
        if not t:
            continue
        bysub[pm['sub']].extend(t)                                 # player pool: everything (names not needed)
        if pm['name'] in own_names:
            cam_own[pm['name']].extend(t)                          # camera: this mesh gets its own node
        else:
            cam_bysub[pm['sub']].extend(t)                         # camera: pooled structure
    subs = sorted(bysub)
    perim, bw = perimeter_wall_tris(town), both_wall_tris(town)
    inv, pw = invisible_tris(town), player_wall_tris(town)
    triggers = trigger_nodes(scn)

    # ---- PLAYER `_a`: per sub, pool everything (structure + town-wide walls on sub 0), split; triggers stay tagged
    player = {}
    for i, sub in enumerate(subs):
        used = set()
        pool = list(bysub[sub])
        if i == 0:                                                 # first ground anchors the town-wide walls
            pool += perim + bw + inv + pw
        named = _pool_split(pool, 'pcol', used, max_tris)
        trigs = [(fit_node_name(tn, used, 15), tt, te) for tn, tt, te in triggers.get(sub, [])]
        player[sub] = (named, trigs)

    # ---- CAMERA `_c`: consolidated structure + both-walls + perimeter (NO canal/railings/triggers = player-only).
    #      One-sided: flip known-backwards windings so every camera wall's normal faces the play area (fix_camera_
    #      winding). Player `_a` above keeps the raw (two-sided) winding.
    # Camera `_c`: full-detail structure by default. Simplification is DIRECTED, not blanket — specific groups are
    # merged by cam_merge_selected (authored OUTWARD, behind the visual mesh, so the collision never clips in front
    # of the rendered wall). This keeps the runtime gather-buffer reduction targeted and reviewable one group at a
    # time, instead of one sweeping pass that's hard to vet against the visual meshes.
    cstruct = fix_camera_winding([t for sub in subs for t in cam_bysub[sub]])
    cbw, cperim = fix_camera_winding(bw), fix_camera_winding(perim)
    cext = camera_tris(town)                                       # authored camera-only (already play-area wound)
    # Directed simplification runs over the WHOLE camera pool (structure + perimeter + both-walls + authored), so a
    # job can target a wall no matter which set it happens to live in (e.g. the x=600 spine is in `perimeter`).
    cam_pool = cam_merge_selected(cstruct + cperim + cbw + cext, 'e03')
    used = set()
    # <=max_tris (100) spatially-compact ring nodes. DELIBERATELY kept small: camera `_c` is mostly the town
    # PERIMETER RING, so every kd-split bucket has a map-spanning bbox and the runtime frame-gather cull is weak.
    # The camera CCPoly gather buffer is a hard 400-poly/frame cap (no bounds check) summed across every part near
    # the camera, and NW-Queens already SATURATES it — bigger nodes coarsen the cull and blow the cap. Keep at 100.
    camera = _pool_split(cam_pool, 'ccol', used, max_tris)
    if town == 'e03':                                              # obj40/obj44: 4 corner torches -> outer cube faces
        cam_own = {nm: gate_torch_simplify(tt) for nm, tt in cam_own.items()}
    own_named = [(f'c{nm}', fix_camera_winding(tt)) for nm, tt in sorted(cam_own.items())]
    camera += [(fit_node_name(nm, used, 15), tt) for nm, tt in own_named]   # obj40/obj44: own node each, unsplit

    sets = {'structure': cstruct + [t for _, tt in own_named for t in tt],
            'bwalls': cbw + cext, 'perimeter': cperim, 'invisible': inv + pw}
    return {'subs': subs, 'player': player, 'camera': camera, 'sets': sets}


def enable_waterfall_zwrite(scn):
    """Turn the waterfall render frames' Z-WRITE back ON so they occlude the player's BODY. The obj48 falls
    (`obj48__a01z[N]`) and the taki fall's back layer (`taki2__a01z`) carry the `z` per-frame flag (SetFrameAttr
    → CFrame+0x104=0 = ZBUF.ZMSK, no depth write), so a player behind a fall draws on top of it. Replace that
    `z` with `x` (an unhandled = no-op flag letter) in each — keeps the alpha-test (`a01`) and any instance
    digit, just drops the Z-write-off. (taki1__a01a already writes Z.) NOTE: this makes the BODY occlude but
    depth-clips Toan's CLOTH cape, which draws with a different depth state — a known smaller artifact; the
    correct full fix is drawing the falls after the character, blocked by the DrawWater/refraction entanglement.
    Returns (scn_bytes, count)."""
    if not isinstance(scn, (bytes, bytearray)):
        return scn, 0
    b = bytearray(scn)
    n = 0
    for pat in (b'obj48__a01z', b'taki2__a01z'):
        zpos = len(pat) - 1
        i = 0
        while True:
            j = b.find(pat, i)
            if j < 0:
                break
            if b[j + zpos] == 0x7a:      # 'z' -> 'x' (0x78): unknown letter, SetFrameAttr skips it, Z-write stays ON
                b[j + zpos] = 0x78
                n += 1
            i = j + 1
    return bytes(b), n


def bake_structures(scene_rel, town='e03', max_tris=100, buildings=False, wall_max_ny=None, building_lod=2):
    scn = load_scene(scene_rel)
    P = placed_meshes(scene_rel, scene_rel.replace('scene.scn', 'mapinfo.cfg'))
    G = grouped_collision(P, scn, town, max_tris)

    stats, manifest = [], []
    dirnames = set(scene_placed.scn_directory_map(scn).keys())

    # ---- PLAYER `_a` per ground sub-file ----
    for sub in G['subs']:
        if sub not in dirnames:            # skip anything that isn't a real SCN sub-file (e.g. injected parts)
            continue
        named, trigs = G['player'][sub]
        for mn, bk in named:
            manifest.append((sub, 'player', mn, len(bk), 'shared'))
        for mn, tt, _te in trigs:
            manifest.append((sub, mn, mn, len(tt), 'trigger'))
        allnodes = list(named) + [(mn, tt, te) for mn, tt, te in trigs]
        mds = build_flat_mds(allnodes)
        scn, delta = _replace_a_block(scn, sub, mds)
        tris_ct = sum(len(bk) for _, bk in named) + sum(len(tt) for _, tt, _ in trigs)
        stats.append((sub, len(allnodes), len(allnodes), 0, tris_ct, 0, delta))

    # ---- CAMERA `_c`: consolidated on the first ground that ships a `_c` variant (origin-placed, world==local;
    #      e03g05 has no `_c`). The town camera reads this via the native camera frame (+0xdc), so the mod must
    #      NOT alias camera=player. Buildings keep their vanilla `_a`/`_c`.
    cam_named = G['camera']
    DIRmap = scene_placed.scn_directory_map(scn)
    cam_host = next((s for s in G['subs'] if s in DIRmap
                     and _variant_off(scn[DIRmap[s][0]:DIRmap[s][0] + DIRmap[s][1]], s, '_c') is not None), None)
    if cam_host and cam_named:
        cam_mds = build_flat_mds(cam_named)
        scn, cdelta = _replace_a_block(scn, cam_host, cam_mds, suffix='_c')
        for _mn, _bk in cam_named:
            manifest.append((cam_host, 'camera', _mn, len(_bk), 'camera'))
        stats.append((cam_host + '_c', len(cam_named), len(cam_named), 0, sum(len(e[1]) for e in cam_named), 0, cdelta))

    # ---- BUILDINGS (default OFF): replace each e03h* `_a` with its wall silhouette split into <=max_tris nodes, in
    #      BUILDING-LOCAL space so the georama part placement moves it at runtime (see building_collision_nodes).
    if buildings:
        for sub, named in building_collision_nodes(scn, max_tris, wall_max_ny, building_lod).items():
            if sub not in dirnames:
                continue
            for mn, bk in named:
                manifest.append((sub, 'building', mn, len(bk), 'building'))
            mds = build_flat_mds(named)
            scn, delta = _replace_a_block(scn, sub, mds)
            tris_ct = sum(len(bk) for _, bk in named)
            stats.append((sub, 1, len(named), 0, tris_ct, 0, delta))

    # Waterfalls Z-write ON so the player's BODY occludes behind them. This depth-clips Toan's CLOTH cape (a
    # known smaller artifact); the full fix (draw falls after the character) is blocked by the DrawWater/
    # refraction entanglement — see enable_waterfall_zwrite. To revert: comment the two lines below.
    if town == 'e03':
        scn, _z = enable_waterfall_zwrite(scn)
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

    # Patch load_scene on every module that resolves it: esm (source), scene_placed / gc (which imported it),
    # AND THIS module's own binding — bake_structures calls the bare `load_scene(scene_rel)` it imported locally,
    # so without patching our own global the direct call would still read the disc (and require DC1_DATA_DIR).
    g = globals()
    saved = (esm.load_scene, scene_placed.load_scene, gc.load_scene, g['load_scene'])
    esm.load_scene = scene_placed.load_scene = gc.load_scene = g['load_scene'] = patched
    try:
        return bake_structures(scene_rel, town, max_tris)
    finally:
        esm.load_scene, scene_placed.load_scene, gc.load_scene, g['load_scene'] = saved


if __name__ == '__main__':
    scene = sys.argv[1] if len(sys.argv) > 1 else 'gedit/e03/scene.scn'
    new_scn, stats, manifest = bake_structures(scene)
    grew = sum(s[-1] for s in stats)
    print(f"shared-collision bake: {len(stats)} sub-files, scene grew {grew:+} bytes")
    for sub, ns, cn, iv, ct, it, d in stats:
        print(f"  {sub}: {cn} collision nodes ({ct} tris) from {ns} sources  (Δ{d:+})")
    # validate: within-sub-file name uniqueness (flat _a, no camcol/aroot)
    DIR = scene_placed.scn_directory_map(new_scn)
    for g in ('e03g04', 'e03g05'):
        off, size = DIR[g]; sub = new_scn[off:off + size]; vo = gc._variant_a(sub, g); mds = off + vo
        cnt, tbl = struct.unpack_from('<II', new_scn, mds + 8)
        names = [new_scn[mds + tbl + i * 0x70 + 8:mds + tbl + i * 0x70 + 8 + 16].split(b'\x00')[0].decode('latin1') for i in range(cnt)]
        print(f"  {g}_a: {cnt} nodes, names-unique={len(set(names)) == cnt}")
