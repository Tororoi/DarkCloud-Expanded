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
    scn1 = build_injected_scene(scn0, kb)
    cfg1 = build_injected_mapinfo(cfg0, 212, 9, -61)
    print(f"scene.scn {len(scn0)} -> {len(scn1)}  parts {struct.unpack_from('<I',scn0,4)[0]} -> {struct.unpack_from('<I',scn1,4)[0]}")
    refs = re.findall(r'(?:GROUND|WATER)\s+"([^"]+)"', cfg1.decode('latin1'))
    print(f"mapinfo.cfg {len(cfg0)} -> {len(cfg1)}  GROUND/WATER {len(refs)}  kanban present={'kanban' in refs}")
    i = cfg1.decode('latin1').find('"kanban"')
    print("kanban block:", repr(cfg1.decode('latin1')[cfg1.decode('latin1').rfind('GROUND',0,i):i+150]))
