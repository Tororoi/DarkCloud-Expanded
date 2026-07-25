#!/usr/bin/env python3
"""Inject the fishing-sign kanban into a town as a NATIVE georama part — no code cave, no runtime draw.

A town draws its geometry from `gedit/<t>/scene.scn` (a SCN container of `PTS` parts) and places each part
via a `gedit/<t>/mapinfo.cfg` GROUND/WATER entry (position + Y-rotation). We add ONE part + ONE entry:

  scene.scn : append a `PTS` part cloned from the simplest 1-mesh part (s04a01) — its header is identical
              across parts except the name and nine size fields (part size @+0x4C/50/54/78/90/A8/C0/D8,
              MDS size @+0x58). The embedded MDS is our kanban.mds with an identity node matrix (verts are
              local, facing south); a 26th part-table entry (name, dataOffset, dataSize) + partCount++.
              Table slack (table ends 0x4C0, first blob 0xC10) means no existing blob moves.
  mapinfo   : a GROUND "kanban" entry that places it at the world position (rotation 0 = south).

The kanban's texture (e01b24) is registered globally at boot by build_sign_iso.py, so the part resolves it.
"""
import struct, re

SIZE_FIELDS = [0x4C, 0x50, 0x54, 0x78, 0x90, 0xA8, 0xC0, 0xD8]   # PTS header fields that hold the PART size
MDSSIZE_FIELD = 0x58                                              # PTS header field that holds the MDS size
NODE = 0x10                                                       # kanban.mds node 0 offset
MAT = NODE + 0x30                                                 # node matrix
TRANS = MAT + 12 * 4                                              # translation row (matrix[3][0..2])


def _find_part(scn, name: bytes):
    """dataOffset of the first part-table entry named `name`, or None."""
    n = struct.unpack_from('<I', scn, 4)[0]
    for i in range(n):
        e = 0x10 + i * 0x30
        if scn[e:e + 0x10].split(b'\x00')[0] == name:
            return struct.unpack_from('<I', scn, e + 0x10)[0]
    return None


def build_injected_scene(scene: bytes, kanban_mds: bytes, template_name=b"s04a01", part_name=b"kanban") -> bytes:
    """Return scene.scn with a `kanban` PTS part appended. The 0x160-byte PTS header is cloned from a simple
    1-mesh part (s04a01 by default). The kanban node matrix is forced to identity (local verts, faces south);
    world placement is done by the mapinfo entry, matching how instanced parts work."""
    scn = bytearray(scene)
    template_part_off = _find_part(scn, template_name)
    if template_part_off is None:
        raise ValueError(f"template part {template_name!r} not found in scene.scn")
    kb = bytearray(kanban_mds)
    for r in range(3):                                            # identity 3x3
        for c in range(3):
            struct.pack_into('<f', kb, MAT + (r * 4 + c) * 4, 1.0 if r == c else 0.0)
    struct.pack_into('<3f', kb, TRANS, 0.0, 0.0, 0.0)             # origin — mapinfo positions it

    hdr = bytearray(scn[template_part_off:template_part_off + 0x160])
    hdr[0x08:0x18] = part_name + b"_0.mds" + b"\x00" * (0x10 - len(part_name) - 6)
    part = hdr + kb
    psize = len(part)
    for o in SIZE_FIELDS:
        struct.pack_into('<I', part, o, psize)
    struct.pack_into('<I', part, MDSSIZE_FIELD, len(kb))

    n = struct.unpack_from('<I', scn, 4)[0]
    blob = (len(scn) + 0xF) & ~0xF
    scn += b"\x00" * (blob - len(scn)) + part
    ent = 0x10 + n * 0x30
    scn[ent:ent + 0x10] = part_name + b"\x00" * (0x10 - len(part_name))
    struct.pack_into('<II', scn, ent + 0x10, blob, psize)
    struct.pack_into('<I', scn, 4, n + 1)
    return bytes(scn)


# Brownboo's upper crater walls render double-sided and hide the town from an overhead edit-mode camera.
# At MDS load the engine's SetFrameAttr turns each letter of a node name's "__" suffix into a render flag:
# 's' enables backface culling (single-sided), 'n' leaves it off (two-sided). The artist tagged the lower
# rings (world Y 0..300) __s but the upper rings (Y 300..1200) __n. Flipping the 12 upper nodes to __s makes
# them cull exactly like the lower rings (attribute-identical) — one byte per node, geometry unchanged.
UPPER_WALL_NODES = [f"s04g01{i:02d}__n" for i in range(5, 17)]


def cull_upper_crater_walls(scene: bytes) -> bytes:
    """Enable backface culling on Brownboo's upper crater walls by flipping their `__n` suffix to `__s`."""
    scn = bytearray(scene)
    for node in UPPER_WALL_NODES:
        key = node.encode() + b"\x00"                            # the null-terminated node-name field
        at = scn.find(key)
        if at < 0:
            raise ValueError(f"crater-wall node {node!r} not found in scene.scn")
        scn[at + len(node) - 1] = ord('s')                      # trailing 'n' -> 's'
    return bytes(scn)


# Stray horizontal triangles a top-down edit camera sees (both up-facing, outside the town, still visible
# after the walls cull). Two sets, keyed by a per-node Y cutoff (remove up-facing tris with centroid Y < yMax):
#   * each square crater ring has 4 corner-fill tris at its (+-500,+-500) corners, and a ring's ONLY up-facing
#     tris are those 4 -> yMax = +inf.
#   * the crater FLOORs s04g0117__s and s04g0117__s1 (the pond bottom) are real horizontal surfaces
#     (Y 0..76 / mostly up-facing) but each also has 2 sunken corner strays at Y=-100 -> yMax = -50 removes
#     just those, leaving the floor intact. Together the two nodes hold all 4 crater-floor corners.
# Each lives in a primType-3 triangle LIST, so collapsing its two trailing index-records onto the first makes a
# zero-area triangle the GS discards, without disturbing any strip or the display-list layout. Record stride is
# 3 or 4 ints depending on whether the mesh carries a per-vertex colour block (see read_tris) — s04g0117__s1 is
# a 4-int-record "variant" mesh, handled by the stride computed below.
CORNER_TRI_NODES = ([(f"s040101__s", 1e9)] + [(f"s04g010{i}__s", 1e9) for i in (2, 3, 4)]
                    + [(f"s04g01{i:02d}__n", 1e9) for i in range(5, 17)]
                    + [("s04g0117__s", -50.0), ("s04g0117__s1", -50.0)])


def _mdt_of(scn: bytes, node: str) -> int:
    """File offset of a node's MDT block. The node name sits at node+8; meshOff at node+0x28 is measured
    from the containing MDS block base (or absolute for some blocks) — try both, like the extractor."""
    at = scn.find(node.encode() + b"\x00")
    if at < 0:
        raise ValueError(f"ring node {node!r} not found in scene.scn")
    mesh_off = struct.unpack_from("<i", scn, (at - 8) + 0x28)[0]
    mds = scn.rfind(b"MDS\x00", 0, at)
    for cand in (mesh_off, mds + mesh_off):
        if 0 < cand < len(scn) - 3 and scn[cand:cand + 3] == b"MDT":
            return cand
    raise ValueError(f"MDT for {node!r} not resolved")


def remove_ring_corner_tris(scene: bytes):
    """Delete the stray up-facing corner triangles from the crater rings + floor (see CORNER_TRI_NODES).
    Returns (scene, removed)."""
    import extract_scene_mesh as _E
    scn = bytearray(scene)
    removed = 0
    for node, ymax in CORNER_TRI_NODES:
        fo = _mdt_of(scn, node)
        hw = struct.unpack_from("<16I", scn, fo)
        verts = _E.read_verts(bytes(scn), fo)                          # LOCAL verts (these nodes' rot is identity)
        dl, vcount = hw[10], hw[3]
        rb = (4 if (hw[8] != 0xffffffff and hw[8] > 0) else 3) * 4     # record size in bytes (colour block -> 4 ints)
        numsub = struct.unpack_from("<I", scn, fo + dl + 8)[0]
        o = dl + 0x10
        for _ in range(numsub):
            prim, vcnt = struct.unpack_from("<ii", scn, fo + o)
            o += 0xC
            recbase = fo + o
            o += vcnt * rb
            if prim != 3:
                continue
            for k in range(0, vcnt - 2, 3):
                i0, i1, i2 = (struct.unpack_from("<i", scn, recbase + (k + j) * rb)[0] for j in range(3))
                if not (0 <= i0 < vcount and 0 <= i1 < vcount and 0 <= i2 < vcount) or len({i0, i1, i2}) < 3:
                    continue
                a, b, c = verts[i0], verts[i1], verts[i2]
                if (a[1] + b[1] + c[1]) / 3.0 >= ymax:                 # only strays below the per-node cutoff
                    continue
                nx = (b[1]-a[1])*(c[2]-a[2]) - (b[2]-a[2])*(c[1]-a[1])
                ny = (b[2]-a[2])*(c[0]-a[0]) - (b[0]-a[0])*(c[2]-a[2])
                nz = (b[0]-a[0])*(c[1]-a[1]) - (b[1]-a[1])*(c[0]-a[0])
                L = (nx*nx + ny*ny + nz*nz) ** 0.5 or 1.0
                if ny / L > 0.9:                                       # up-facing -> collapse to degenerate
                    struct.pack_into("<i", scn, recbase + (k + 1) * rb, i0)
                    struct.pack_into("<i", scn, recbase + (k + 2) * rb, i0)
                    removed += 1
    return bytes(scn), removed


def build_injected_mapinfo(cfg: bytes, x: float, y: float, z: float, ry=0, part_name="kanban") -> bytes:
    """Return mapinfo.cfg with a GROUND "<part_name>" placement at (x,y,z), Y-rotation ry (0 = south).
    Inserted after the last GROUND "s04a01" block so it sits with the other georama placements."""
    t = cfg.decode('latin1')
    blk = (f'\r\n\tGROUND\t"{part_name}",\t\t//fishing sign\r\n'
           + ''.join(f'\t\t"",\t\t\t//level{i}\r\n' for i in (1, 2, 3))
           + '\t\t"",\t\t\t//\r\n\t\t"",\t\t\t//\r\n\t\t"",\t\t\t//\r\n\t\t"",\t\t\t//?\r\n'
           + f'\t\t{int(x)}\t,{int(y)}\t,{int(z)},\t//position\r\n'
           + f'\t\t0\t,{int(ry)}\t,0\t//rotation\r\n')
    m = None
    for m in re.finditer(r'\tGROUND\t"s04a01",.*?\r\n\t\t-?\d[^\r\n]*\r\n\t\t\d[^\r\n]*,[^\r\n]*\r\n', t, re.S):
        pass
    if m is None:
        raise ValueError("no GROUND s04a01 block found to anchor the insert")
    return (t[:m.end()] + blk + t[m.end():]).encode('latin1')


if __name__ == "__main__":   # offline self-test against the extracted disc
    import os, sys
    sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
    sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
    from extract_scene_mesh import load_scene
    kb = open("game_data/fishsign/kanban.mds", "rb").read()
    scn0 = bytes(load_scene('gedit/s04/scene.scn'))
    cfg0 = bytes(load_scene('gedit/s04/mapinfo.cfg'))
    scn1, removed = remove_ring_corner_tris(build_injected_scene(scn0, kb))
    scn1 = cull_upper_crater_walls(scn1)                          # cull rename AFTER removal (it renames __n->__s)
    cfg1 = build_injected_mapinfo(cfg0, 212, 9, -61)
    print(f"scene.scn {len(scn0)} -> {len(scn1)}  parts {struct.unpack_from('<I',scn0,4)[0]} -> {struct.unpack_from('<I',scn1,4)[0]}")
    culled = sum(scn1.count(f"s04g01{i:02d}__s\x00".encode()) for i in range(5, 17))
    print(f"upper crater walls culled: {culled}/12 nodes now __s (the 3 remaining '__n' nodes are buildings)")
    print(f"corner strays removed: {removed} (expect 68 = 4x16 ring corners + 2 s04g0117__s + 2 s04g0117__s1)")
    # re-extract the patched scene and confirm no stray up-facing corner tris remain
    from extract_scene_mesh import extract_mesh
    em = extract_mesh(scn1)
    leftover = 0
    # after the cull rename all 16 rings are __s; check each for any up-facing tri, and s04g0117 below Y=-50
    check = ([(f"s040101__s", 1e9)] + [(f"s04g01{i:02d}__s", 1e9) for i in range(2, 17)]
             + [("s04g0117__s", -50.0), ("s04g0117__s1", -50.0)])
    for node, ymax in check:
        V, T = em.get(node, ([], []))
        for t in T:
            a, b, c = (V[i] for i in t)
            if (a[1] + b[1] + c[1]) / 3.0 >= ymax:
                continue
            nx = (b[1]-a[1])*(c[2]-a[2]) - (b[2]-a[2])*(c[1]-a[1])
            ny = (b[2]-a[2])*(c[0]-a[0]) - (b[0]-a[0])*(c[2]-a[2])
            nz = (b[0]-a[0])*(c[1]-a[1]) - (b[1]-a[1])*(c[0]-a[0])
            if ny / ((nx*nx+ny*ny+nz*nz) ** 0.5 or 1) > 0.9:
                leftover += 1
    print(f"stray up-facing corner tris remaining after patch: {leftover} (expect 0)")
    refs = re.findall(r'(?:GROUND|WATER)\s+"([^"]+)"', cfg1.decode('latin1'))
    print(f"mapinfo.cfg {len(cfg0)} -> {len(cfg1)}  GROUND/WATER {len(refs)}  kanban present={'kanban' in refs}")
    i = cfg1.decode('latin1').find('"kanban"')
    print("kanban block:", repr(cfg1.decode('latin1')[cfg1.decode('latin1').rfind('GROUND',0,i):i+150]))
