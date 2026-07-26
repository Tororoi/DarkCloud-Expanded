#!/usr/bin/env python3
"""MDT <-> Wavefront OBJ bridge, so a town mesh can be remodelled in Blender and re-encoded.

export_obj(mdt, path): writes <path>.obj (positions=v, UVs=vt, normals=vn; triangles grouped by material
via `usemtl matN`, strips flattened to lists) + a <path>.mdtjson sidecar holding everything OBJ can't carry
(raw 0x60 material blocks, the preserved header flag words, block order + padding, colour info).

import_obj(path): reads the (possibly Blender-edited) OBJ + sidecar and rebuilds an mdt_codec.Mdt — one
prim-3 (LIST) submesh per material group, records taken straight from the OBJ f p/t/n indices. New geometry
the user added in Blender just appears as new v/vt/vn/f and flows through. Materials are reused by slot
(matN -> existing material block), so added faces should keep an existing `usemtl` (reuse a town texture).

Round-trip is geometry-exact (same set of world triangles), not byte-exact — re-encoding relists strips and
regroups submeshes, which the engine renders identically. Colour-block meshes aren't supported by the OBJ
path yet (cliffs/terrain/water have none); export raises if one is passed.
"""
import os, json, struct
import mdt_codec


def _flatten(prim, recs):
    """A submesh's records -> list of (recA, recB, recC) triangles (records are index tuples)."""
    out = []
    if prim == 3:
        for k in range(0, len(recs) - 2, 3):
            out.append((recs[k], recs[k + 1], recs[k + 2]))
    elif prim == 4:
        for i in range(len(recs) - 2):
            a, b, c = (recs[i], recs[i + 1], recs[i + 2]) if i % 2 == 0 else (recs[i + 1], recs[i], recs[i + 2])
            out.append((a, b, c))
    return out


def export_obj(m, path, name="mesh"):
    if m.has_col:
        raise NotImplementedError("colour-block meshes not supported by the OBJ bridge yet")
    obj = os.path.splitext(path)[0] + ".obj"
    with open(obj, "w") as f:
        f.write(f"# MDT export: {name}  ({len(m.pos)} pos, {len(m.uv)} uv, {len(m.norm)} norm, "
                f"{len(m.materials)} materials)\n")
        f.write(f"mtllib {os.path.basename(os.path.splitext(path)[0])}.mtl\n")
        f.write(f"o {name}\n")
        for x, y, z, _w in m.pos:
            f.write(f"v {x:.6f} {y:.6f} {z:.6f}\n")
        for s, t, _a, _b in m.uv:
            f.write(f"vt {s:.6f} {t:.6f}\n")
        for x, y, z, _w in m.norm:
            f.write(f"vn {x:.6f} {y:.6f} {z:.6f}\n")
        # group triangles by material slot
        bymat = {}
        for prim, midx, recs in m.submeshes:
            bymat.setdefault(midx, []).extend(_flatten(prim, recs))
        for midx in sorted(bymat):
            f.write(f"usemtl mat{midx}\n")
            for tri in bymat[midx]:
                # OBJ is 1-based; record = (posIdx, uvIdx, normIdx)
                f.write("f " + " ".join(f"{p+1}/{u+1}/{n+1}" for (p, u, n) in tri) + "\n")
    # a minimal .mtl so Blender shows distinct material slots (names must match usemtl)
    mtl = os.path.splitext(path)[0] + ".mtl"
    with open(mtl, "w") as f:
        for i, mb in enumerate(m.materials):
            tex = mb[0x34:0x34 + 16].split(b"\x00")[0].decode("latin1", "replace")
            f.write(f"newmtl mat{i}\nKd 0.8 0.8 0.8\n# texture {tex}\n")
    # sidecar: everything OBJ can't carry
    side = {
        "hdr": m.hdr, "preamble": m.preamble, "order": m.order,
        "pads": {k: v.hex() for k, v in m.pads.items()},
        "has_col": m.has_col,
        "materials": [mb.hex() for mb in m.materials],
    }
    with open(os.path.splitext(path)[0] + ".mdtjson", "w") as f:
        json.dump(side, f)
    return obj


def import_obj(path):
    base = os.path.splitext(path)[0]
    side = json.load(open(base + ".mdtjson"))
    pos, uv, norm = [], [], []
    groups = {}          # matIdx -> list of records (posIdx,uvIdx,normIdx), 0-based
    cur = 0
    with open(base + ".obj") as f:
        for ln in f:
            t = ln.split()
            if not t:
                continue
            if t[0] == "v":
                pos.append((float(t[1]), float(t[2]), float(t[3]), 1.0))
            elif t[0] == "vt":
                uv.append((float(t[1]), float(t[2]), 0.0, 1.0))
            elif t[0] == "vn":
                norm.append((float(t[1]), float(t[2]), float(t[3]), 0.0))
            elif t[0] == "usemtl":
                cur = int(t[1][3:]) if t[1].startswith("mat") else 0
            elif t[0] == "f":
                verts = []
                for tok in t[1:]:
                    a = (tok.split("/") + ["", ""])[:3]
                    pi = int(a[0]) - 1
                    ui = (int(a[1]) - 1) if a[1] else 0
                    ni = (int(a[2]) - 1) if a[2] else 0
                    verts.append((pi, ui, ni))
                # triangulate an n-gon as a fan (Blender may emit quads/ngons)
                for k in range(1, len(verts) - 1):
                    groups.setdefault(cur, []).extend([verts[0], verts[k], verts[k + 1]])

    m = mdt_codec.Mdt()
    m.hdr = list(side["hdr"])
    m.preamble = list(side["preamble"])
    m.order = side["order"]
    m.pads = {k: bytes.fromhex(v) for k, v in side["pads"].items()}
    m.has_col = side["has_col"]
    m.materials = [bytes.fromhex(x) for x in side["materials"]]
    m.pos = pos if pos else [(0.0, 0.0, 0.0, 1.0)]
    m.uv = uv if uv else [(0.0, 1.0, 0.0, 1.0)]
    m.norm = norm if norm else [(0.0, 1.0, 0.0, 0.0)]
    m.col = None
    # one prim-3 (LIST) submesh per material group
    m.submeshes = [[3, midx, groups[midx]] for midx in sorted(groups)]
    return m


# ---- self-test: MDT -> OBJ -> MDT with no edits, verify the triangle SET is preserved ----
if __name__ == "__main__":
    import sys, re, tempfile
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from extract_scene_mesh import load_scene, parse_mds

    rel = sys.argv[1] if len(sys.argv) > 1 else "gedit/s13/scene.scn"
    want = sys.argv[2] if len(sys.argv) > 2 else "obj2__n"
    scn = load_scene(rel)

    def tri_set(m):
        s = set()
        for prim, midx, recs in m.submeshes:
            for a, b, c in _flatten(prim, recs):
                # key on the POSITION each record points at (order-independent per tri)
                tri = tuple(sorted((tuple(round(x, 3) for x in m.pos[a[0]]),
                                    tuple(round(x, 3) for x in m.pos[b[0]]),
                                    tuple(round(x, 3) for x in m.pos[c[0]]))))
                s.add(tri)
        return s

    found = False
    for mm in re.finditer(rb"MDS\x00", scn):
        for name, mo, mat in parse_mds(scn, mm.start()):
            if name != want or mo == 0:
                continue
            fo = next((c for c in (mo, mm.start() + mo) if 0 < c < len(scn) and scn[c:c+3] == b"MDT"), None)
            if not fo:
                continue
            found = True
            m = mdt_codec.parse_mdt(scn, fo)
            d = tempfile.mkdtemp()
            p = os.path.join(d, "m")
            export_obj(m, p, name)
            m2 = import_obj(p)
            built = mdt_codec.build_mdt(m2)
            m3 = mdt_codec.parse_mdt(built, 0)
            a, b = tri_set(m), tri_set(m3)
            print(f"{name}: orig tris={len(a)}  roundtrip tris={len(b)}  "
                  f"identical={a == b}  (rebuilt MDT {len(built)} bytes, re-parsed OK)")
            if a != b:
                print("  missing:", list(a - b)[:2], "\n  extra:", list(b - a)[:2])
            break
        if found:
            break
    if not found:
        print(f"node {want} not found in {rel}")
