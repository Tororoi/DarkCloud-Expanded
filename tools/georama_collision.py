#!/usr/bin/env python3
"""Extract a town's COLLISION geometry (the `_a` = 当たり/atari variant meshes) offline from scene.scn.

Each scene sub-file (e03h01 building, e03g04 ground, e03r/e03t road/tree) carries a nested variant list:
after the visual mesh come `<name>_a.mds` (collision), `_k`, `_c` blocks. Each variant entry is
`name\0` + 3-byte fixup-ptr tail + u32 offset + u32 size (offset relative to the sub-file), pointing to a
standard MDS node block whose MDTs are COLLISION MDTs. A collision MDT differs from a visual one: the game
builds CCPoly triangles in CreateCollisionMDT (main 0x127250) by reading POS block @MDT word[4], the display
list @word[10], the triangle count at DL+0x14, and records at DL+0x18 with a stride of 5 int32 (the first 3
are POS-block vertex indices; [3] a colour index; [4] pad). No display-list primitive/attribute decoding —
just indexed triangles — so mdt_codec's visual parser does not apply.

collision_local(scene_rel, name_re) -> {part_name: [ [[x,y,z]*3], ... ]}  (LOCAL sub-file space, parent-accum)
place_base(scene_rel, mapinfo_rel, names) -> world-placed collision tris for base GROUND entries (identity/xf)
"""
import re, struct
from extract_scene_mesh import load_scene, xform
import scene_placed


def parse_coll_mdt(scn, mdt):
    """Triangles of a COLLISION MDT (see CreateCollisionMDT). Returns [(v0,v1,v2), ...] in POS-block space."""
    w = struct.unpack_from('<16I', scn, mdt)
    if w[0] != 0x54444d:                     # 'MDT\0'
        return []
    POS, DL = w[4], w[10]
    tc = struct.unpack_from('<I', scn, mdt + DL + 0x14)[0]
    rb = mdt + DL + 0x18

    def V(i):
        return struct.unpack_from('<3f', scn, mdt + POS + i * 0x10)

    tris = []
    for t in range(tc):
        i0, i1, i2 = struct.unpack_from('<3i', scn, rb + t * 0x14)
        tris.append((V(i0), V(i1), V(i2)))
    return tris


def _variant_a(sub, name):
    """Offset (within `sub`) of the `<name>_a` collision MDS block, or None."""
    m = next(re.finditer((re.escape(name) + r'_a\.mds\x00').encode(), sub), None)
    if not m:
        return None
    off = struct.unpack_from('<I', sub, m.end() + 3)[0]   # +3 skips the baked fixup-ptr tail
    return off if 0 < off < len(sub) and sub[off:off + 3] == b'MDS' else None


def _mesh_local(scn, mds):
    """Parent-accumulated collision triangles of the MDS node block at absolute offset `mds`."""
    nodes, wm = scene_placed._accum(scn, mds)
    tris = []
    for i, (nn, mo, par, mat) in enumerate(nodes):
        if mo == 0:
            continue
        fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c + 3] == b'MDT'), None)
        if not fo:
            continue
        M = wm(i)
        for a, b, c in parse_coll_mdt(scn, fo):
            tris.append([list(xform(M, a)), list(xform(M, b)), list(xform(M, c))])
    return tris


def collision_local(scene_rel, name_re):
    """{part_name: local collision tris} for every sub-file matching name_re that has an `_a` variant."""
    scn = load_scene(scene_rel)
    DIR = scene_placed._scndir(scn)
    rx = re.compile(name_re)
    out = {}
    for name in sorted(DIR):
        if re.search(r'_[a-z]$', name) or not rx.match(name):
            continue
        off, size = DIR[name]
        sub = scn[off:off + size]
        vo = _variant_a(sub, name)
        if vo is None:
            continue
        tris = _mesh_local(scn, off + vo)
        if tris:
            out[name] = tris
    return out


def place_base(scene_rel, mapinfo_rel, name_re):
    """World-placed collision tris for base GROUND/WATER sub-files (apply mapinfo pos + Y-rot)."""
    cfg = load_scene(mapinfo_rel).decode('latin1', 'replace')
    placements = {n: (pos, rot) for n, pos, rot in scene_placed._ground_placements(cfg)}
    local = collision_local(scene_rel, name_re)
    out = {}
    for name, tris in local.items():
        pos, rot = placements.get(name, ([0, 0, 0], [0, 0, 0]))
        out[name] = [[scene_placed._place_y(p, pos, rot[1]) for p in t] for t in tris]
    return out


if __name__ == '__main__':
    import sys
    scene = sys.argv[1] if len(sys.argv) > 1 else 'gedit/e03/scene.scn'
    pat = sys.argv[2] if len(sys.argv) > 2 else r'e03[hgrt]\d\d$'
    cl = collision_local(scene, pat)
    tot = 0
    for nm in sorted(cl):
        t = cl[nm]
        xs = [p[0] for tr in t for p in tr]; ys = [p[1] for tr in t for p in tr]; zs = [p[2] for tr in t for p in tr]
        print(f"  {nm}_a  tris={len(t):4}  W={max(xs)-min(xs):.0f} D={max(zs)-min(zs):.0f} H={max(ys)-min(ys):.0f}")
        tot += len(t)
    print(f"{len(cl)} collision parts, {tot} tris")

# ---------------- collision MDT ENCODER (mirror of parse_coll_mdt) -------------------------------
# Moved here from tools/iso_patch/collision/build_coll_mdt.py (2026-09) so the encoder lives next to
# its decoder; the westbank/queens copy (slightly different filler words, same consumed fields) was
# deleted at the same time. CreateCollisionMDT reads: total @w2, verts @w4 (stride 0x10 XYZW),
# DL @w10 (count @DL+0x14, records @DL+0x18 stride 5 int32), colour block @w14 (0 = skip -> zeroed
# per-poly tag). attrs: optional per-tri 16-byte colour entries (event-trigger tags).
def build_coll_mdt(tris, colour=False, attrs=None, y_shift=0.0):
    # dedupe verts -> indexed POS block (quantise to avoid float dupes). y_shift: node frames that
    # sit at T=(0,-y_shift,0) need local y = world y + y_shift (e.g. the s1301 pcam hulls at 210).
    index = {}
    verts = []
    idx = []
    for t in tris:
        tri = []
        for p in t:
            key = (round(p[0], 3), round(p[1] + y_shift, 3), round(p[2], 3))
            i = index.get(key)
            if i is None:
                i = len(verts)
                index[key] = i
                verts.append((float(p[0]), float(p[1]) + y_shift, float(p[2])))
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
