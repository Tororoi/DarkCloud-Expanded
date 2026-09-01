#!/usr/bin/env python3
"""Custom camera-collision (`_c`) meshes for Queens buildings h04/h05/h08/h09.

Candidate geometry (user-directed): the part's LOD2 visual mesh as the base, plus FULL-mesh
features LOD2 flattens — the curved roofs (upper ROOF_ZONE of each structural body node), the
canopies (hiyoke* incl. the nuki fringe) and the round roof chimney-pillar (entotu; h04/05/08).

Bake: each part subfile's `_c` block is LAST in the file, so the rebuild is truncate-and-append:
a new `_c` MDS (root + N child nodes, each a <=MAX_NODE_TRIS collision MDT via kd splitting for
per-node gather culling), with only the header's _c SIZE word (+0xc4) and the scene directory
entry changing. Node frames are identity — MDT verts are written in raw part-local coordinates.

Shared by the viewer overlay (tools/queens_viewer.py) and the exporter
(tools/export_queens_hcam.py) so what you review is what bakes.
"""
import os, re, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract_scene_mesh import load_scene, xform
import scene_placed
import mdt_codec
from georama_parts import lod_models
from westbank_smooth_bake import build_coll_mdt

CAND_PARTS = ('e03h04', 'e03h05', 'e03h08', 'e03h09')
ROOF_ZONE = 0.45             # per-body-node: tris with lowest vert above this height fraction = roof
MAX_NODE_TRIS = 200          # user-directed split target per _c node
_H_BODIES = {'e03h04': ('e03h04__s', 'betu__s'), 'e03h05': ('e03h05__s',),
             'e03h08': ('e03h08__s', '2f__s'), 'e03h09': ('eo3h09__s',)}


def _node_tris(scn, DIR, sub_name):
    """{node_name: [local tris]} from the part's FULL (_0) mds."""
    off, size = DIR[sub_name]
    head = scn[off:off + 0x200]
    names = [m.group(1).decode() for m in re.finditer(re.escape(sub_name).encode() + rb'_([0-9a-z])\.mds\x00', head)]
    blocks = [m.start() for m in re.finditer(rb'MDS\x00', scn[off:off + size])]
    out = {}
    for suf, bo in zip(names, blocks):
        if suf != '0':
            continue
        mds = off + bo
        nodes, wm = scene_placed._accum(scn, mds)
        for i, (nn, mo, par, mat) in enumerate(nodes):
            if mo == 0:
                continue
            fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c + 3] == b'MDT'), None)
            if not fo:
                continue
            try:
                m = mdt_codec.parse_mdt(scn, fo)
            except Exception:
                continue
            M = wm(i)
            wv = [xform(M, (p[0], p[1], p[2])) for p in m.pos]
            ts = [[list(wv[a]), list(wv[b]), list(wv[c])] for a, b, c in scene_placed._flatten(m)]
            if ts:
                out.setdefault(nn, []).extend(ts)
    return out


def candidate_tris():
    """{part: [tris]} in RAW part-local coordinates (uncentered — bake frame)."""
    scn = load_scene('gedit/e03/scene.scn')
    DIR = scene_placed._scndir(scn)
    lods = lod_models('gedit/e03/scene.scn', r'e03h(04|05|08|09)$')
    out = {}
    for part in CAND_PARTS:
        cand = [[list(q) for q in t] for t in lods[part]['2']]
        added = {'roof': 0, 'canopy': 0, 'pillar': 0}
        nodes = _node_tris(scn, DIR, part)
        for bk in _H_BODIES[part]:
            if bk not in nodes:
                raise KeyError(f'body node {bk} missing in {part}')
            bys = [p[1] for t in nodes[bk] for p in t]
            cut = min(bys) + ROOF_ZONE * (max(bys) - min(bys))
            for t in nodes[bk]:
                if min(p[1] for p in t) >= cut:
                    cand.append([list(q) for q in t])
                    added['roof'] += 1
        for k, ts in nodes.items():
            feat = 'canopy' if k.startswith('hiyoke') else 'pillar' if k.startswith('entotu') else None
            if feat:
                for t in ts:
                    cand.append([list(q) for q in t])
                    added[feat] += 1
        out[part] = cand
    return out


def split_tris(tris, max_tris=MAX_NODE_TRIS):
    """Recursive median kd-split on the longest xz axis until every chunk <= max_tris."""
    if len(tris) <= max_tris:
        return [tris]
    cens = [((t[0][0] + t[1][0] + t[2][0]) / 3, (t[0][2] + t[1][2] + t[2][2]) / 3) for t in tris]
    xs = [c[0] for c in cens]; zs = [c[1] for c in cens]
    ax = 0 if (max(xs) - min(xs)) >= (max(zs) - min(zs)) else 1
    order = sorted(range(len(tris)), key=lambda i: cens[i][ax])
    mid = len(order) // 2
    a = [tris[i] for i in order[:mid]]
    b = [tris[i] for i in order[mid:]]
    return split_tris(a, max_tris) + split_tris(b, max_tris)


def build_coll_mds(old_mds, chunks, name_prefix='hc'):
    """A fresh collision MDS: header cloned from old_mds, root null node + one identity-frame
    node per chunk (entries cloned from old_mds's first mesh node)."""
    cnt0, tbl0 = struct.unpack_from('<II', old_mds, 8)
    template = None
    for i in range(cnt0):
        b = tbl0 + i * 0x70
        if struct.unpack_from('<i', old_mds, b + 0x28)[0]:
            template = old_mds[b:b + 0x70]
            break
    assert template is not None

    def entry(idx, nm, mo, par):
        e = bytearray(template)
        struct.pack_into('<i', e, 0, idx)
        nmb = nm.encode('latin1')
        e[8:24] = nmb + b'\x00' * (16 - len(nmb))
        struct.pack_into('<ii', e, 0x28, mo, par)
        ident = [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0]
        struct.pack_into('<16f', e, 0x30, *ident)
        return bytes(e)

    n = len(chunks)
    new_c = bytearray(old_mds[:0x10])
    struct.pack_into('<I', new_c, 8, n + 1)
    new_c += entry(0, 'null1', 0, -1)
    mdts = [build_coll_mdt(ch, y_shift=0.0) for ch in chunks]
    pos = 0x10 + (n + 1) * 0x70
    offs = []
    for m in mdts:
        offs.append(pos)
        pos += len(m) + ((-len(m)) % 16)
    for k in range(n):
        new_c += entry(k + 1, f'{name_prefix}{k:02d}', offs[k], 0)
    for m in mdts:
        new_c += m
        new_c += b'\x00' * ((-len(m)) % 16)
    return bytes(new_c)


def rebuild_part_sub(scn, DIR, name, chunks):
    """The part's subfile bytes with its `_c` block (last in the file) replaced by a new MDS:
    root null node + one identity-frame node per chunk."""
    off, size = DIR[name]
    sub = bytearray(scn[off:off + size])
    c_off = struct.unpack_from('<I', sub, 0xc0)[0]
    c_size = struct.unpack_from('<I', sub, 0xc4)[0]
    assert c_off + c_size == size, f'{name}: _c is not the last block'
    old_c = bytes(sub[c_off:c_off + c_size])
    new_c = build_coll_mds(old_c, chunks)
    out = bytearray(sub[:c_off]) + new_c
    struct.pack_into('<I', out, 0xc4, len(new_c))
    return bytes(out), size


if __name__ == '__main__':
    scn = load_scene('gedit/e03/scene.scn')
    DIR = scene_placed._scndir(scn)
    for part, tris in candidate_tris().items():
        chunks = split_tris(tris)
        new, old_size = rebuild_part_sub(scn, DIR, part, chunks)
        print(f'{part}: {len(tris)} tris -> {len(chunks)} nodes ({[len(c) for c in chunks]}), '
              f'sub {old_size} -> {len(new)} bytes')
