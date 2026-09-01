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
import mdt_codec, mdt_obj, scene_splice


def _node_mdt(scn, sub, node):
    entry = next((e for e in scene_splice.scn_directory_legacy(scn) if e[0] == sub), None)
    if entry is None:
        raise SystemExit(f"sub-file {sub!r} not found; available: {[e[0] for e in scene_splice.scn_directory_legacy(scn)]}")
    _, so, ss, _ = entry
    _, _, _, _, _, mdt_abs, _ = scene_splice._find_node(scn, so, ss, node)
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

    spliced, delta = scene_splice.splice_mdt(scn, sub, node, new_mdt)

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
