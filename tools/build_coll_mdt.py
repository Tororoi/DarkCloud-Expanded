#!/usr/bin/env python3
"""Build a COLLISION-format MDT (the kind CreateCollisionMDT @ main 0x127250 consumes) from a raw triangle
soup. Mirror of tools/georama_collision.parse_coll_mdt.

A collision MDT differs from a visual one only in its display list: no VIF/GIF primitive decoding, just an
indexed triangle table. CreateCollisionMDT reads exactly these fields:
  memcpy size   = header w2 (total MDT byte size)
  POS block     = header w4 (verts, stride 0x10, XYZW)
  DL block      = header w10; triCount @ DL+0x14; records @ DL+0x18, stride 5 int32 (v0,v1,v2,colourIdx,pad)
  colour block  = header w14 (0 => skip colour; each record's [3] indexes it, stride 0x10)
So a valid minimal collision MDT is header + POS + DL, matching the real byte layout observed in e03h*_a.

build_coll_mdt(tris) -> bytes   # tris = [ [[x,y,z],[x,y,z],[x,y,z]], ... ]  (deduped into an indexed POS block)
"""
import struct


def build_coll_mdt(tris, colour=False):
    # dedupe verts -> indexed POS block (quantise to avoid float dupes)
    index = {}
    verts = []
    idx = []
    for t in tris:
        tri = []
        for p in t:
            key = (round(p[0], 3), round(p[1], 3), round(p[2], 3))
            i = index.get(key)
            if i is None:
                i = len(verts)
                index[key] = i
                verts.append((float(p[0]), float(p[1]), float(p[2])))
            tri.append(i)
        idx.append(tuple(tri))

    vc, tc = len(verts), len(idx)
    POS = 0x40
    pos_bytes = b''.join(struct.pack('<4f', v[0], v[1], v[2], 1.0) for v in verts)

    DL = POS + len(pos_bytes)
    DL = (DL + 0xf) & ~0xf
    # DL header (0x18): [tag, 0x10, tc, 0x20202020, 3, tc] then records
    dl = bytearray(struct.pack('<6I', 0x003400b8, 0x10, tc, 0x20202020, 3, tc))
    cidx = 0
    for (a, b, c) in idx:
        dl += struct.pack('<5i', a, b, c, cidx, 0)

    body_end = DL + len(dl)
    colour_off = 0
    if colour:
        colour_off = (body_end + 0xf) & ~0xf
        body_end = colour_off + 0x10

    total = (body_end + 0xf) & ~0xf

    hdr = [0] * 16
    hdr[0] = 0x0054444d   # 'MDT\0'
    hdr[1] = 0x40
    hdr[2] = total
    hdr[3] = vc
    hdr[4] = POS
    hdr[5] = 2 * tc
    hdr[8] = 0
    hdr[10] = DL
    hdr[13] = 1
    hdr[14] = colour_off

    mdt = bytearray(total)
    struct.pack_into('<16I', mdt, 0, *hdr)
    mdt[POS:POS + len(pos_bytes)] = pos_bytes
    mdt[DL:DL + len(dl)] = dl
    if colour:
        struct.pack_into('<4B', mdt, colour_off, 0x80, 0x80, 0x80, 0x80)
    return bytes(mdt)


if __name__ == '__main__':
    # round-trip self-test against the real collision parser
    import sys, os
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import georama_collision as gc

    tris = [
        [[0, 0, 0], [10, 0, 0], [10, 0, 10]],
        [[0, 0, 0], [10, 0, 10], [0, 0, 10]],
        [[0, 0, 0], [0, 50, 0], [10, 50, 0]],
    ]
    mdt = build_coll_mdt(tris)
    scn = mdt  # parse_coll_mdt indexes from the MDT start
    got = gc.parse_coll_mdt(scn, 0)
    print(f'built {len(mdt)} byte MDT, {len(tris)} tris in -> {len(got)} tris parsed back')
    ok = True
    for a, b in zip(tris, got):
        for pa, pb in zip(a, b):
            if any(abs(x - y) > 1e-3 for x, y in zip(pa, pb)):
                ok = False
    print('ROUND-TRIP', 'OK' if ok and len(got) == len(tris) else 'FAIL')
    for t in got[:3]:
        print('  ', [[round(x, 1) for x in p] for p in t])
