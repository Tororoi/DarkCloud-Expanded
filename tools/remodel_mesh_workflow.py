#!/usr/bin/env python3
"""Town-mesh remodel workflow: export a scene.scn mesh to OBJ for Blender, then apply the edited OBJ back
into a new scene.scn (offline), validating that the whole scene still decodes. The ISO bake (data.dat/hd2
growth + IsoPatcher) is a separate, later step — this tool proves the edit end-to-end without touching the ISO.

  # 1. export a node to OBJ (+ .mtl + .mdtjson sidecar) for Blender:
  python3 tools/remodel_mesh_workflow.py export gedit/s13/scene.scn s1308 obj2__n game_data/yellowdrops/edit/obj2

  # 2. edit game_data/yellowdrops/edit/obj2.obj in Blender (add/remove/move tris; keep an existing usemtl
  #    for new faces; DON'T rename the object), export back over the same .obj (triangulate on export).

  # 3. apply the edited OBJ -> a new scene.scn, with full-scene validation:
  python3 tools/remodel_mesh_workflow.py apply gedit/s13/scene.scn s1308 obj2__n game_data/yellowdrops/edit/obj2 \
          game_data/yellowdrops/edit/scene_s13_edited.scn
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract_scene_mesh import load_scene, extract_mesh
import mdt_codec, mdt_obj
import scene_placed


# scene.scn layout (see tools/mdt_codec.py for the MDT itself): 0x30-byte directory entries from 0x10
# (name @0, sub-file offset @0x10, size @0x14); sub-file -> MDS blocks (node table @+0xC, 0x70/node,
# meshOff @+0x28 RELATIVE to the MDS). MDTs pack sequentially, 0x10-aligned. Growing node k's MDT by
# delta shifts every later meshOff in its MDS, the sub-file size, and every later sub-file's offset.
# (Former scene_splice.py, now on scene_placed.scn_directory_list — the fixed-0x10 gedit directory.)
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
    entry = next((e for e in scene_placed.scn_directory_list(scn) if e[0] == sub_name), None)
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
    for name, off, size, eoff in scene_placed.scn_directory_list(scn):     # iterate original dir (offsets pre-shift)
        if off == sub_off:
            struct.pack_into('<I', out, eoff + 0x14, size + delta)
        elif off > sub_off:
            struct.pack_into('<I', out, eoff + 0x10, off + delta)
    return bytes(out), delta


def _node_mdt(scn, sub, node):
    entry = next((e for e in scene_placed.scn_directory_list(scn) if e[0] == sub), None)
    if entry is None:
        raise SystemExit(f"sub-file {sub!r} not found; available: {[e[0] for e in scene_placed.scn_directory_list(scn)]}")
    _, so, ss, _ = entry
    _, _, _, _, _, mdt_abs, _ = _find_node(scn, so, ss, node)
    return mdt_abs


def cmd_export(scene_rel, sub, node, out_base):
    scn = load_scene(scene_rel)
    m = mdt_codec.parse_mdt(scn, _node_mdt(scn, sub, node))
    os.makedirs(os.path.dirname(os.path.abspath(out_base)), exist_ok=True)
    obj = mdt_obj.export_obj(m, out_base, node)
    tris = sum(mdt_codec._tri_count(p, len(r)) for p, mi, r in m.submeshes)
    print(f"exported {node}: {len(m.pos)} verts, {tris} tris, {len(m.materials)} materials -> {obj}")
    print(f"  sidecar: {out_base}.mdtjson (materials + header) — keep it next to the .obj")


def cmd_apply(scene_rel, sub, node, obj_base, out_scn):
    scn = load_scene(scene_rel)
    old = mdt_codec.parse_mdt(scn, _node_mdt(scn, sub, node))
    old_tris = sum(mdt_codec._tri_count(p, len(r)) for p, mi, r in old.submeshes)

    m = mdt_obj.import_obj(obj_base)
    new_mdt = mdt_codec.build_mdt(m)
    new_tris = sum(mdt_codec._tri_count(p, len(r)) for p, mi, r in m.submeshes)
    mdt_codec.parse_mdt(new_mdt, 0)   # sanity: the rebuilt MDT must re-parse
    print(f"edited {node}: {len(old.pos)}v/{old_tris}t -> {len(m.pos)}v/{new_tris}t "
          f"(MDT {old.hdr[2]} -> {len(new_mdt)} bytes)")

    spliced, delta = splice_mdt(scn, sub, node, new_mdt)

    # full-scene validation: every OTHER node must decode unchanged; the edited node decodes with its new tris
    a, b = extract_mesh(scn), extract_mesh(spliced)
    bad = []
    for name in a:
        if name not in b:
            bad.append((name, 'MISSING after splice')); continue
        if name == node:
            continue
        if len(a[name][0]) != len(b[name][0]) or len(a[name][1]) != len(b[name][1]):
            bad.append((name, f'CHANGED v{len(a[name][0])}->{len(b[name][0])} t{len(a[name][1])}->{len(b[name][1])}'))
    if bad:
        print("VALIDATION FAILED:")
        for n, why in bad[:20]:
            print(f"  {n}: {why}")
        raise SystemExit(1)
    if node not in b:
        raise SystemExit(f"edited node {node} vanished after splice")

    os.makedirs(os.path.dirname(os.path.abspath(out_scn)), exist_ok=True)
    open(out_scn, 'wb').write(spliced)
    print(f"VALIDATION OK: all {len(a)} nodes decode; {node} now {len(b[node][1])} tris")
    print(f"scene.scn {len(scn)} -> {len(spliced)} bytes (delta {delta}) -> {out_scn}")
    print("  NEXT: ISO bake (grow scene.scn in data.dat, update data.hd2 index, IsoPatcher) — not yet wired.")


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print(__doc__); raise SystemExit(1)
    cmd = sys.argv[1]
    if cmd == 'export' and len(sys.argv) == 6:
        cmd_export(*sys.argv[2:6])
    elif cmd == 'apply' and len(sys.argv) == 7:
        cmd_apply(*sys.argv[2:7])
    else:
        print(__doc__); raise SystemExit(1)
