#!/usr/bin/env python3
"""Splice an edited (possibly resized) MDT back into a town scene.scn, fixing every nested offset.

scene.scn layout (see tools/mdt_codec.py for the MDT itself):
  SCN header: "SCN\\0", dir offset @+4 (=0x10). Directory entries are 0x30 bytes: name(16) @0, sub-file
  file-offset @0x10, sub-file size @0x14.
  Sub-file -> one/more MDS blocks ("MDS\\0", nodeCount @+8, node-table offset @+0xC). Node = 0x70 bytes:
  name @+8, meshOff @+0x28 (RELATIVE to the MDS block), parent @+0x2c, 4x4 matrix @+0x30. MDTs are packed
  sequentially after the node table, in node order, so meshOffs are monotincreasing.

Growing node k's MDT by `delta` bytes:
  * every node in the SAME MDS whose meshOff > node k's meshOff shifts by delta,
  * the sub-file grows by delta -> its SCN-dir size += delta and every LATER sub-file's dir offset += delta,
  * the whole scene.scn grows by delta (the caller updates the data.hd2 index for the ISO bake; offline we
    just re-parse the spliced bytes).
MDT total sizes are 0x10-aligned; we pad the new MDT to 0x10 so following MDTs stay aligned.
"""
import struct, re
import mdt_codec


def _dir(scn):
    """[(name, off, size, entry_file_offset)] for every SCN sub-file directory entry."""
    assert scn[:4] == b'SCN\x00', "not an SCN container"
    diroff = struct.unpack_from('<I', scn, 4)[0]
    out, o = [], diroff
    while o + 0x30 <= len(scn):
        name = scn[o:o+16].split(b'\x00')[0].decode('latin1', 'replace')
        if not name or not name[0].isalnum():
            break
        off, size = struct.unpack_from('<II', scn, o + 0x10)
        out.append((name, off, size, o))
        o += 0x30
    return out


def _find_node(scn, sub_off, sub_size, node_name):
    """Return (mds_abs, node_index, node_count, table_abs, meshOff_rel, mdt_abs, mdt_size)."""
    for mm in re.finditer(rb'MDS\x00', scn[sub_off:sub_off + sub_size]):
        mds = sub_off + mm.start()
        cnt, tbl = struct.unpack_from('<II', scn, mds + 8)
        if not (0 < cnt < 1000):
            continue
        table = mds + tbl
        for i in range(cnt):
            b = table + i * 0x70
            if b + 0x70 > len(scn):
                break
            nm = scn[b+8:b+8+16].split(b'\x00')[0].decode('latin1', 'replace')
            mo = struct.unpack_from('<i', scn, b + 0x28)[0]
            if nm == node_name and mo != 0:
                mdt = mds + mo
                if scn[mdt:mdt+3] != b'MDT':
                    continue
                size = struct.unpack_from('<I', scn, mdt + 8)[0]
                return mds, i, cnt, table, mo, mdt, size
    raise KeyError(f"node {node_name!r} with a mesh not found in sub-file")


def splice_mdt(scn, sub_name, node_name, new_mdt):
    """Return new scene.scn bytes with `node_name`'s MDT replaced by `new_mdt` (any size)."""
    scn = bytearray(scn)
    entry = next((e for e in _dir(scn) if e[0] == sub_name), None)
    if entry is None:
        raise KeyError(f"sub-file {sub_name!r} not found")
    _, sub_off, sub_size, _ = entry
    mds, idx, cnt, table, mo, mdt_abs, old_size = _find_node(scn, sub_off, sub_size, node_name)

    # pad new MDT to 0x10 so following MDTs stay aligned; delta relative to the old on-disk footprint
    new_mdt = bytearray(new_mdt)
    while len(new_mdt) % 0x10:
        new_mdt.append(0)
    delta = len(new_mdt) - old_size

    # 1) replace the MDT bytes in place
    out = bytearray(scn[:mdt_abs]) + new_mdt + bytearray(scn[mdt_abs + old_size:])

    # 2) fix sibling meshOffs in this MDS (those laid out AFTER the edited one)
    for i in range(cnt):
        b = table + i * 0x70
        smo = struct.unpack_from('<i', out, b + 0x28)[0]
        if smo != 0 and smo > mo:
            struct.pack_into('<i', out, b + 0x28, smo + delta)

    # 3) fix the SCN directory: this sub-file's size += delta; every later sub-file's offset += delta
    for name, off, size, eoff in _dir(scn):     # iterate original dir (offsets pre-shift)
        if off == sub_off:
            struct.pack_into('<I', out, eoff + 0x14, size + delta)
        elif off > sub_off:
            struct.pack_into('<I', out, eoff + 0x10, off + delta)
    return bytes(out), delta


# ---- offline validator: edit a node, splice, then re-parse the WHOLE scene ----
if __name__ == '__main__':
    import sys, os
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from extract_scene_mesh import load_scene, extract_mesh, parse_mds

    rel = sys.argv[1] if len(sys.argv) > 1 else 'gedit/s13/scene.scn'
    sub = sys.argv[2] if len(sys.argv) > 2 else 's1308'
    node = sys.argv[3] if len(sys.argv) > 3 else 'obj2__n'
    scn = load_scene(rel)

    # locate the node's MDT, parse it, ADD a triangle (3 new verts) to submesh 0
    entry = next(e for e in _dir(scn) if e[0] == sub)
    _, so, ss, _ = entry
    mds, idx, cnt, table, mo, mdt_abs, old_size = _find_node(scn, so, ss, node)
    m = mdt_codec.parse_mdt(scn, mdt_abs)
    before_tris = sum(mdt_codec._tri_count(p, len(r)) for p, mi, r in m.submeshes)
    base = len(m.pos)
    m.pos += [(-30.0, 300.0, -30.0, 1.0), (30.0, 300.0, -30.0, 1.0), (0.0, 300.0, 30.0, 1.0)]
    m.submeshes[0][2].extend([(base, 0, 0), (base + 1, 0, 0), (base + 2, 0, 0)])
    new_mdt = mdt_codec.build_mdt(m)
    print(f"edit {node}: {old_size} -> {len(new_mdt)} bytes, tris {before_tris} -> {before_tris + 1}")

    spliced, delta = splice_mdt(scn, sub, node, new_mdt)
    print(f"spliced scene: {len(scn)} -> {len(spliced)} bytes (delta {delta})")

    # full re-parse: every node in the ORIGINAL must still decode in the spliced scene, byte-for-byte
    # geometry, EXCEPT the edited node which must gain its triangle.
    a = extract_mesh(scn)
    b = extract_mesh(spliced)
    print(f"nodes decoded: orig {len(a)}  spliced {len(b)}")
    bad = []
    for name in a:
        if name not in b:
            bad.append((name, 'MISSING'))
            continue
        va, ta = a[name]; vb, tb = b[name]
        if name == node:
            if len(tb) != len(ta) + 1:
                bad.append((name, f'tris {len(ta)}->{len(tb)} (want +1)'))
        elif len(va) != len(vb) or len(ta) != len(tb):
            bad.append((name, f'CHANGED v {len(va)}->{len(vb)} t {len(ta)}->{len(tb)}'))
    if bad:
        print("VALIDATION FAILURES:")
        for n, why in bad[:20]:
            print(f"  {n}: {why}")
    else:
        print(f"VALIDATION OK: all {len(a)} nodes decode unchanged, {node} gained its triangle")
