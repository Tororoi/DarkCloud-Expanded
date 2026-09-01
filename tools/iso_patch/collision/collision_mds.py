#!/usr/bin/env python3
"""Engine-level MDS splice + flat collision-MDS serialiser (town-agnostic). Split out of
bake_player_camera_collision.py (2026-09); that module re-exports these names for its importers.

  _variant_off / _replace_a_block  — locate / replace a sub-file's `<name>_a` (or `_c`) MDS block in scene.scn
  build_flat_mds                   — [(node, tris[, colour_entries])] -> flat `_a` MDS (root + children)
  _pool_split / _fit               — kd_split a pooled soup into <=max_tris nodes with unique 15-char names
"""
import re, struct
import os, sys
_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)                                     # this dir (sibling collision modules)
sys.path.insert(0, os.path.dirname(os.path.dirname(_HERE)))   # tools/ (scene_placed, georama_collision, tri_util…)
import scene_placed
from georama_collision import build_coll_mdt
from tri_util import kd_split


_dir = scene_placed.scn_dir   # canonical directory parser (list form)


def _variant_off(sub, name, suffix='_a'):
    """Offset (within `sub`) of the `<name><suffix>.mds` MDS variant block, or None. suffix='_a' = player
    collision, '_c' = camera collision, etc."""
    m = next(re.finditer((re.escape(name) + suffix + r'\.mds\x00').encode(), sub), None)
    if not m:
        return None
    off = struct.unpack_from('<I', sub, m.end() + 3)[0]
    return off if 0 < off < len(sub) and sub[off:off + 3] == b'MDS' else None


def _replace_a_block(scn, sub_name, new_mds, suffix='_a'):
    """Replace `sub_name`'s entire `<name><suffix>` MDS block with new_mds; fix trailing variant offsets and the
    SCN directory. suffix='_a' (player) by default; '_c' rewrites the camera-collision variant. Returns
    (new_scn, delta)."""
    scn = bytearray(scn)
    entry = next((e for e in _dir(scn) if e[0] == sub_name), None)
    if entry is None:
        raise KeyError(sub_name)
    _, sub_off, sub_size, _ = entry
    sub = bytes(scn[sub_off:sub_off + sub_size])
    vo = _variant_off(sub, sub_name, suffix)
    if vo is None:
        raise KeyError(f"{sub_name}: no {suffix}")

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
    """named: [(node_name, [tri,...]) | (node_name, [tri,...], [colour_entry_16b,...]), ...]. Build a flat `_a`
    (node 0 root, rest its children). Camera and player both gather the whole thing — the 5-unit canal walls
    clear the camera by height, so no camera/player split is needed. A 3-tuple carries per-triangle colour-block
    attributes (the event-trigger tags) through build_coll_mdt so loading zones keep their destination."""
    n = len(named)
    header = struct.pack('<4sIII', b'MDS\x00', 1, n, 0x10)
    table = bytearray(); blob = bytearray()
    cur = 0x10 + n * 0x70
    for i, entry in enumerate(named):
        nm, t = entry[0], entry[1]
        attrs = entry[2] if len(entry) > 2 else None
        node = bytearray(0x70)
        struct.pack_into('<II', node, 0, 0, 0x70)
        b = nm.encode('latin1', 'replace')[:15]
        node[8:8 + len(b)] = b
        mdt = build_coll_mdt(t, attrs=attrs)
        struct.pack_into('<i', node, 0x28, cur)
        blob += mdt; cur += len(mdt)
        struct.pack_into('<i', node, 0x2c, -1 if i == 0 else 0)
        struct.pack_into('<16f', node, 0x30, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)
        table += node
    return header + bytes(table) + bytes(blob)


def _pool_split(pool, prefix, used, max_tris=100):
    """kd_split a POOLED triangle soup into <=max_tris spatially-compact nodes with unique names. Pooling across
    source meshes (then splitting) packs NEARBY polys into the same node regardless of which mesh they came from,
    so every node has a tight bounding box — which is exactly what the runtime frame-gather self-cull keys off."""
    return [(_fit(f'{prefix}{bi}', used, 15), bk) for bi, bk in enumerate(kd_split(pool, max_tris))]


def append_variant_nodes(scn, sub_name, add_nodes, suffix='_a', template_node=1):
    """Append collision nodes to a sub-file's existing `<name><suffix>` MDS WITHOUT touching its vanilla
    nodes: the header/table are re-laid with extra entries cloned from `template_node` (name/index/
    mesh-offset overwritten; parent + matrix inherited — identity, parent 0 for the ground subs), every
    vanilla MDT block is copied byte-for-byte (so per-tri colour/attribute entries such as the
    loading-zone trigger tags survive untouched), then the new MDTs follow. Splices the rebuilt block back
    via _replace_a_block. add_nodes = [(node_name, mdt_bytes), ...]. Returns (new_scn, delta)."""
    entry = next((e for e in _dir(scn) if e[0] == sub_name), None)
    if entry is None:
        raise KeyError(sub_name)
    _, sub_off, sub_size, _ = entry
    sub = bytes(scn[sub_off:sub_off + sub_size])
    vo = _variant_off(sub, sub_name, suffix)
    if vo is None:
        raise KeyError(f"{sub_name}: no {suffix}")
    after = []
    for m in re.finditer(rb'[\w]+\.mds\x00', sub):
        fpos = m.end() + 3
        if fpos + 4 > len(sub):
            continue
        toff = struct.unpack_from('<I', sub, fpos)[0]
        if vo < toff < sub_size and sub[toff:toff + 3] == b'MDS':
            after.append(toff)
    mds_size = (min(after) if after else sub_size) - vo
    cnt, tbl = struct.unpack_from('<II', sub, vo + 8)
    nodes = []
    for i in range(cnt):
        b = vo + tbl + i * 0x70
        nm = sub[b + 8:b + 24].split(b'\x00')[0].decode('latin1')
        mo = struct.unpack_from('<i', sub, b + 0x28)[0]
        nodes.append((i, nm, mo))
    order = sorted([n for n in nodes if n[2]], key=lambda n: n[2])
    blocks = []
    for k, (i, nm, mo) in enumerate(order):
        end = order[k + 1][2] if k + 1 < len(order) else mds_size
        blocks.append((i, sub[vo + mo: vo + end]))
    out = bytearray(sub[vo: vo + tbl + cnt * 0x70])
    for k, (nm, mdt) in enumerate(add_nodes):
        ent = bytearray(sub[vo + tbl + template_node * 0x70: vo + tbl + (template_node + 1) * 0x70])
        struct.pack_into('<I', ent, 0, cnt + k)
        nmb = nm.encode('latin1')[:15]
        ent[8:24] = nmb + b'\x00' * (16 - len(nmb))
        out += ent
    struct.pack_into('<I', out, 8, cnt + len(add_nodes))
    for i, raw in blocks:
        new_mo = len(out)
        out += raw
        while len(out) % 16:
            out += b'\x00'
        struct.pack_into('<i', out, tbl + i * 0x70 + 0x28, new_mo)
    for k, (nm, mdt) in enumerate(add_nodes):
        new_mo = len(out)
        out += mdt
        while len(out) % 16:
            out += b'\x00'
        struct.pack_into('<i', out, tbl + (cnt + k) * 0x70 + 0x28, new_mo)
    return _replace_a_block(scn, sub_name, bytes(out), suffix)
