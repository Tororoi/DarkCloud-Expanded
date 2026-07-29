#!/usr/bin/env python3
"""EXPERIMENT: rebuild a town's TERRAIN (ground e03g*) collision as many small, self-culling nodes so the
camera can gather it cheaply, and split each section to <=100 polys.

Why nodes (not parts): PickUpNearPoly__6CFrame recurses child nodes, and each node's CCollisionMDT gather
self-culls on its own bbox (object-bbox check returns 0 on no overlap). So a ground `_a` split into N
<=100-poly nodes is gathered with per-node culling for free — only nodes near the camera box iterate their
polys. The vanilla ground `_a` is 1-2 giant nodes (obj42=626, obj41=300) with one town-wide bbox each, i.e.
no internal culling.

Source = the terrain's own `_a` collision tris (right kind of geometry, small, player-safe), rebuilt into our
controlled split MDS that REPLACES the vanilla `_a`. Buildings are left untouched; the mod gathers only the
ground parts, so the camera sees only this custom split terrain. Data comes from the user's ISO -> nothing
committed.

  bake_terrain(scene_rel, ground_re=r'e03g0[45]$', max_tris=100) -> (new_scn_bytes, stats)
"""
import os, sys, struct, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract_scene_mesh import load_scene
import scene_placed, georama_collision as gc
from build_coll_mdt import build_coll_mdt


def _dir(scn):
    out, o = [], 0x10
    while o + 0x30 <= len(scn):
        nm = scn[o:o + 16].split(b'\x00')[0].decode('latin1', 'replace')
        if not nm or not nm[0].isalnum():
            break
        off, size = struct.unpack_from('<II', scn, o + 0x10)
        out.append((nm, off, size, o))
        o += 0x30
    return out


def kd_split(tris, max_tris=100):
    """Recursively median-split the triangle soup along its longest centroid axis until each leaf <= max_tris.
    Gives compact, balanced buckets (tight bboxes -> effective per-node culling)."""
    def cen(t):
        return ((t[0][0] + t[1][0] + t[2][0]) / 3, (t[0][1] + t[1][1] + t[2][1]) / 3, (t[0][2] + t[1][2] + t[2][2]) / 3)

    def rec(ts):
        if len(ts) <= max_tris:
            return [ts]
        cs = [cen(t) for t in ts]
        axis = max(range(3), key=lambda a: max(c[a] for c in cs) - min(c[a] for c in cs))
        order = sorted(range(len(ts)), key=lambda i: cs[i][axis])
        mid = len(ts) // 2
        return rec([ts[i] for i in order[:mid]]) + rec([ts[i] for i in order[mid:]])

    return rec(list(tris))


def build_a_mds(buckets, prefix='cam'):
    """Build a collision MDS block: header + N-node table + N collision MDTs (one per bucket). Node 0 is the
    root (parent -1); the rest are its children (parent 0) so the frame gather recurses all of them."""
    n = len(buckets)
    header = struct.pack('<4sIII', b'MDS\x00', 1, n, 0x10)
    mdts = [build_coll_mdt(b) for b in buckets]

    table = bytearray()
    blob = bytearray()
    cur = 0x10 + n * 0x70                      # first MDT offset (rel to MDS); 0x10-aligned already
    for i, mdt in enumerate(mdts):
        node = bytearray(0x70)
        struct.pack_into('<II', node, 0, 0, 0x70)
        nm = f'{prefix}{i}'.encode()[:15]
        node[8:8 + len(nm)] = nm
        struct.pack_into('<i', node, 0x28, cur)                     # meshOff
        struct.pack_into('<i', node, 0x2c, -1 if i == 0 else 0)     # parent
        struct.pack_into('<16f', node, 0x30, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)
        table += node
        blob += mdt                            # build_coll_mdt already 0x10-padded
        cur += len(mdt)
    return header + bytes(table) + bytes(blob)


def _replace_a_block(scn, sub_name, new_mds):
    """Replace `sub_name`'s entire `_a` MDS block with new_mds; fix trailing variant offsets and the SCN
    directory. Returns (new_scn, delta)."""
    scn = bytearray(scn)
    entry = next((e for e in _dir(scn) if e[0] == sub_name), None)
    if entry is None:
        raise KeyError(sub_name)
    _, sub_off, sub_size, _ = entry
    sub = bytes(scn[sub_off:sub_off + sub_size])
    vo = gc._variant_a(sub, sub_name)
    if vo is None:
        raise KeyError(f"{sub_name}: no _a")

    # variant entries (real ones: offset -> 'MDS'); the _a block ends at the next variant after it
    variants = []
    for m in re.finditer(rb'[\w]+\.mds\x00', sub):
        fpos = m.end() + 3
        if fpos + 4 > len(sub):
            continue
        toff = struct.unpack_from('<I', sub, fpos)[0]
        if 0 < toff < sub_size and sub[toff:toff + 3] == b'MDS':
            variants.append((sub_off + fpos, toff))
    after = [t for _, t in variants if t > vo]
    old_size = (min(after) if after else sub_size) - vo

    new_mds = bytearray(new_mds)
    while len(new_mds) % 0x10:
        new_mds.append(0)
    delta = len(new_mds) - old_size

    out = bytearray(scn[:sub_off + vo]) + new_mds + bytearray(scn[sub_off + vo + old_size:])
    # trailing variant offsets shift by delta (positions are before the block, unmoved)
    for pos, toff in variants:
        if toff > vo:
            struct.pack_into('<I', out, pos, toff + delta)
    # SCN directory
    for name, off, size, eoff in _dir(scn):
        if off == sub_off:
            struct.pack_into('<I', out, eoff + 0x14, size + delta)
        elif off > sub_off:
            struct.pack_into('<I', out, eoff + 0x10, off + delta)
    return bytes(out), delta


def bake_terrain(scene_rel, ground_re=r'e03g0[45]$', max_tris=100):
    scn = load_scene(scene_rel)
    cl = gc.collision_local(scene_rel, ground_re)      # {ground: local collision tris}
    stats = []
    for name in sorted(cl):
        tris = cl[name]
        if not tris:
            continue
        buckets = kd_split(tris, max_tris)
        mds = build_a_mds(buckets, prefix=name[-2:] + 'c')
        scn, delta = _replace_a_block(scn, name, mds)
        stats.append((name, len(tris), len(buckets), max(len(b) for b in buckets), len(mds), delta))
    return scn, stats


def bake_terrain_from_bytes(scene_rel, scene_bytes, ground_re=r'e03g0[45]$', max_tris=100):
    """bake_terrain but the scene comes from bytes (read from the ISO)."""
    import extract_scene_mesh as esm
    patched = (lambda rel, _o=esm.load_scene, _b=scene_bytes, _r=scene_rel: _b if rel == _r else _o(rel))
    saved = (esm.load_scene, scene_placed.load_scene, gc.load_scene)
    esm.load_scene = scene_placed.load_scene = gc.load_scene = patched
    try:
        return bake_terrain(scene_rel, ground_re, max_tris)
    finally:
        esm.load_scene, scene_placed.load_scene, gc.load_scene = saved


if __name__ == '__main__':
    scene = sys.argv[1] if len(sys.argv) > 1 else 'gedit/e03/scene.scn'
    gre = sys.argv[2] if len(sys.argv) > 2 else r'e03g0[45]$'
    new_scn, stats = bake_terrain(scene, gre)
    grew = sum(d for *_, d in stats)
    print(f"terrain bake: {len(stats)} ground parts, scene grew {grew:+} bytes")
    for n, tc, nb, mx, ms, d in stats:
        print(f"  {n}: {tc} tris -> {nb} nodes (max {mx}/node), {ms} B MDS (Δ{d:+})")

    # offline validation: re-parse and confirm the split collision reads back with the same tris
    import extract_scene_mesh as esm
    orig = esm.load_scene
    esm.load_scene = scene_placed.load_scene = gc.load_scene = (lambda rel: new_scn if rel == scene else orig(rel))
    got = gc.collision_local(scene, gre)
    esm.load_scene = scene_placed.load_scene = gc.load_scene = orig
    print("re-parsed split terrain:")
    for n in sorted(got):
        print(f"  {n}_a: {len(got[n])} tris")
