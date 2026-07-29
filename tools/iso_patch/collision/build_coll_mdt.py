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

attrs: optional per-triangle 16-byte colour-block entries (raw bytes, one per tri). Used to preserve the
event-trigger tags baked into a town's `_a` collision: GetEventPoly reads the loading-zone destination from a
hit poly's colour entry (shorts at CCPoly+0x40/+0x42/+0x44, copied from colour[colourIdx] by CreateCollisionMDT).
Distinct 16-byte entries are deduped into the colour block; each record's colourIdx points at its entry.
"""
import struct


def build_coll_mdt(tris, colour=False, attrs=None):
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

    # per-triangle colour-block entries: dedupe distinct 16-byte entries, map each tri -> its entry index
    entries, tri_cidx = [], []
    if attrs is not None:
        if len(attrs) != tc:
            raise ValueError(f'attrs len {len(attrs)} != tri count {tc}')
        seen = {}
        for e in attrs:
            e = bytes(e)
            if len(e) != 0x10:
                raise ValueError('each attr must be a 16-byte colour entry')
            j = seen.get(e)
            if j is None:
                j = len(entries); seen[e] = j; entries.append(e)
            tri_cidx.append(j)

    DL = POS + len(pos_bytes)
    DL = (DL + 0xf) & ~0xf
    # DL header (0x18): [tag, 0x10, tc, 0x20202020, 3, tc] then records
    dl = bytearray(struct.pack('<6I', 0x003400b8, 0x10, tc, 0x20202020, 3, tc))
    for ti, (a, b, c) in enumerate(idx):
        cidx = tri_cidx[ti] if attrs is not None else 0
        dl += struct.pack('<5i', a, b, c, cidx, 0)

    body_end = DL + len(dl)
    colour_off = 0
    if attrs is not None:
        colour_off = (body_end + 0xf) & ~0xf
        body_end = colour_off + 0x10 * len(entries)
    elif colour:
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
    if attrs is not None:
        blk = b''.join(entries)
        mdt[colour_off:colour_off + len(blk)] = blk
    elif colour:
        struct.pack_into('<4B', mdt, colour_off, 0x80, 0x80, 0x80, 0x80)
    return bytes(mdt)


if __name__ == '__main__':
    # round-trip self-test against the real collision parser
    import sys, os
    sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # tools/
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
