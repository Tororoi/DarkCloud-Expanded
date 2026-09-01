#!/usr/bin/env python3
"""Dark Cloud MDT mesh codec — parse an MDT block to a semantic structure and rebuild it byte-for-byte,
so we can ADD / REMOVE / MOVE triangles and re-inject the edited mesh into scene.scn.

MDT is a clean INDEXED mesh (the game converts it to VU1 data at load via CVisualVu1::CreateVUdataFromMDT
@0x135aa0). Header = 16 u32; the meaningful fields are block POINTERS at byte offsets:
    +0x08 hw[2]  total MDT size (bytes)
    +0x0C hw[3]  vertex (position) count
    +0x10 hw[4]  POSITIONS offset   (16B/entry: X,Y,Z,W floats)
    +0x18 hw[6]  UVs offset         (16B/entry: S,T,?,? floats)
    +0x20 hw[8]  COLOURS offset     (0xffffffff / <1 = none; presence => 4-int records vs 3-int)
    +0x24 hw[9]  DISPLAY-LIST size (bytes)
    +0x28 hw[10] DISPLAY-LIST offset
    +0x30 hw[12] NORMALS offset     (16B/entry; <1 => falls back to positions)
    +0x38 hw[14] MATERIALS offset   (0x60B/entry; texture name at +0x34)
The other header words (hw[1]=0x40, hw[5],hw[7],hw[11],hw[13], hw[15]=uninit) are preserved verbatim.

Display list: 0x10 preamble (only preamble[2] = submesh count is meaningful) then, per submesh, a 3-int
header [primType, vertCount, matIdx] followed by vertCount records. A record is [posIdx, uvIdx, normIdx]
(3 ints) or [posIdx, uvIdx, normIdx, colIdx] (4 ints when a colour block is present). primType 3 = tri
LIST (3 records/tri), 4 = tri STRIP.

Block LAYOUT ORDER varies per mesh (water is POS, DL, UV, NORM, MAT); we record each block's source order
and rebuild in the same order with the same 0x10 alignment, so an unmodified parse->build is byte-identical.
"""
import struct


class Mdt:
    __slots__ = ('hdr', 'pos', 'uv', 'norm', 'col', 'submeshes', 'materials',
                 'preamble', 'order', 'pads', 'has_col')

    def __repr__(self):
        return (f"<Mdt pos={len(self.pos)} uv={len(self.uv)} norm={len(self.norm)} "
                f"col={len(self.col) if self.col is not None else None} "
                f"sub={len(self.submeshes)} mat={len(self.materials)} "
                f"tris={sum(_tri_count(p, len(r)) for p, m, r in self.submeshes)}>")


def _tri_count(prim, nrec):
    if prim == 3:
        return nrec // 3
    if prim == 4:
        return max(0, nrec - 2)
    return 0


def _read_vec16(scn, base, n):
    return [struct.unpack_from('<4f', scn, base + i * 16) for i in range(n)]


def parse_mdt(scn, fo):
    """Parse the MDT at absolute offset fo into an Mdt. All offsets in the returned struct are semantic."""
    assert scn[fo:fo + 3] == b'MDT', "not an MDT"
    hw = list(struct.unpack_from('<16I', scn, fo))
    total = hw[2]
    POS, UV, COL, DLSZ, DL, NORM, MAT = hw[4], hw[6], hw[8], hw[9], hw[10], hw[12], hw[14]
    has_col = 0 < COL < 0x80000000
    stride = 4 if has_col else 3
    # sanity: reject implausible headers (heuristic node-finders sometimes hand us a non-MDT offset)
    if not (0x40 <= total <= len(scn) - fo) or fo + total > len(scn):
        raise ValueError(f"implausible total size 0x{total:x}")
    for nm, off in (('POS', POS), ('UV', UV), ('DL', DL), ('NORM', NORM), ('MAT', MAT)):
        if off and not (0x40 <= off < total):
            raise ValueError(f"{nm} offset 0x{off:x} outside [0x40,0x{total:x})")

    # --- display list: preamble + submeshes ---
    preamble = list(struct.unpack_from('<4i', scn, fo + DL))
    numsub = preamble[2]
    if not (0 < numsub <= hw[3] + 1):
        raise ValueError(f"implausible submesh count {numsub}")
    o = DL + 0x10
    dl_end = DL + DLSZ if 0 < DLSZ <= total else total
    submeshes = []
    for _ in range(numsub):
        if fo + o + 0xC > fo + total:
            raise ValueError("submesh header past MDT end")
        prim, vcnt, midx = struct.unpack_from('<3i', scn, fo + o)
        o += 0xC
        if vcnt < 0 or o + vcnt * stride * 4 > dl_end + 0x10:
            raise ValueError(f"submesh vcnt {vcnt} overruns display list")
        recs = [tuple(struct.unpack_from('<%di' % stride, scn, fo + o + r * stride * 4)) for r in range(vcnt)]
        o += vcnt * stride * 4
        submeshes.append([prim, midx, recs])

    # --- max indices actually referenced => block entry counts (the game never stores counts) ---
    npos = hw[3]
    nuv = max((r[1] for _, _, rs in submeshes for r in rs), default=-1) + 1
    nnorm = max((r[2] for _, _, rs in submeshes for r in rs), default=-1) + 1
    ncol = (max((r[3] for _, _, rs in submeshes for r in rs), default=-1) + 1) if has_col else 0

    m = Mdt()
    m.hdr = hw
    m.has_col = has_col
    m.pos = _read_vec16(scn, fo + POS, npos)
    m.uv = _read_vec16(scn, fo + UV, nuv)
    m.norm = _read_vec16(scn, fo + NORM, nnorm) if NORM > 0 else []
    m.col = _read_vec16(scn, fo + COL, ncol) if has_col else None
    m.submeshes = submeshes
    m.preamble = preamble
    # materials: 0x60 bytes each, verbatim; count = (next thing after MAT .. total)
    nmat = (total - MAT) // 0x60
    m.materials = [bytes(scn[fo + MAT + i * 0x60: fo + MAT + (i + 1) * 0x60]) for i in range(nmat)]
    # record source block order + the exact inter-block padding bytes so an unmodified rebuild is byte-exact
    named = [('POS', POS), ('DL', DL), ('UV', UV), ('NORM', NORM if NORM > 0 else None), ('MAT', MAT)]
    if has_col:
        named.append(('COL', COL))
    named = [(nm, off) for nm, off in named if off is not None]
    named.sort(key=lambda x: x[1])
    m.order = [nm for nm, _ in named]
    # capture padding after each block (bytes between the block's natural end and the next block start)
    sizes = {'POS': npos * 16,
             'DL': DLSZ,
             'UV': nuv * 16,
             'NORM': len(m.norm) * 16,
             'COL': (ncol * 16) if has_col else 0,
             'MAT': nmat * 0x60}
    offs = {'POS': POS, 'DL': DL, 'UV': UV, 'NORM': NORM, 'MAT': MAT}
    if has_col:
        offs['COL'] = COL
    m.pads = {}
    seq = [(nm, offs[nm]) for nm in m.order]
    for i, (nm, off) in enumerate(seq):
        nxt = seq[i + 1][1] if i + 1 < len(seq) else total
        m.pads[nm] = bytes(scn[fo + off + sizes[nm]: fo + nxt])
    return m


def build_mdt(m):
    """Serialize an Mdt back to bytes, laying blocks out in the recorded source order (0x10-aligned via the
    captured padding) and recomputing every header pointer/size. Unmodified in -> byte-identical out."""
    stride = 4 if m.has_col else 3

    def dl_bytes():
        b = bytearray()
        b += struct.pack('<4i', *m.preamble[:3], m.preamble[3])
        # keep the stored submesh count in the preamble authoritative
        struct.pack_into('<i', b, 8, len(m.submeshes))
        for prim, midx, recs in m.submeshes:
            b += struct.pack('<3i', prim, len(recs), midx)
            for r in recs:
                b += struct.pack('<%di' % stride, *r)
        return bytes(b)

    def vec_bytes(vecs):
        b = bytearray()
        for v in vecs:
            b += struct.pack('<4f', *v)
        return bytes(b)

    block = {
        'POS': vec_bytes(m.pos),
        'DL': dl_bytes(),
        'UV': vec_bytes(m.uv),
        'NORM': vec_bytes(m.norm),
        'COL': vec_bytes(m.col) if m.has_col else b'',
        'MAT': b''.join(m.materials),
    }

    out = bytearray(0x40)          # header, filled at the end
    offs = {}
    for nm in m.order:
        offs[nm] = len(out)
        out += block[nm]
        out += m.pads.get(nm, b'')

    hw = list(m.hdr)
    hw[2] = len(out)                                  # total size
    hw[3] = len(m.pos)                                # vertex count
    hw[4] = offs['POS']
    hw[6] = offs['UV']
    hw[8] = offs['COL'] if m.has_col else m.hdr[8]   # preserve source "no colour" sentinel (0 or 0xffffffff)
    hw[9] = len(block['DL'])                          # display-list size
    hw[10] = offs['DL']
    hw[12] = offs['NORM'] if m.norm else 0
    hw[14] = offs['MAT']
    struct.pack_into('<16I', out, 0, *[w & 0xffffffff for w in hw])
    return bytes(out)


# ---- self-test: byte-exact round-trip over every mesh in a scene ----
if __name__ == '__main__':
    import sys, os, re
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from extract_scene_mesh import load_scene, parse_mds
    rel = sys.argv[1] if len(sys.argv) > 1 else 'gedit/s13/scene.scn'
    scn = load_scene(rel)
    total = ok = colvar = fail = 0
    for mm in re.finditer(rb'MDS\x00', scn):
        for name, mo, mat in parse_mds(scn, mm.start()):
            if mo == 0:
                continue
            fo = next((c for c in (mo, mm.start() + mo) if 0 < c < len(scn) and scn[c:c+3] == b'MDT'), None)
            if not fo:
                continue
            total += 1
            try:
                m = parse_mdt(scn, fo)
                rebuilt = build_mdt(m)
                orig = bytes(scn[fo:fo + m.hdr[2]])
                if rebuilt == orig:
                    ok += 1
                    if m.has_col:
                        colvar += 1
                else:
                    fail += 1
                    # locate first diff
                    d = next((i for i in range(min(len(orig), len(rebuilt))) if orig[i] != rebuilt[i]), None)
                    print(f"  MISMATCH {name:20} len {len(orig)}->{len(rebuilt)} firstdiff@0x{d:x}" if d is not None
                          else f"  LENDIFF  {name:20} {len(orig)}->{len(rebuilt)}")
            except Exception as e:
                fail += 1
                print(f"  ERROR    {name:20} {e}")
    print(f"{rel}: {ok}/{total} byte-exact round-trips ({colvar} with colour block), {fail} failed")
