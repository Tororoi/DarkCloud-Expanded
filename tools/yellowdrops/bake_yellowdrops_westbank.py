#!/usr/bin/env python3
"""Bake the SMOOTHED (2x-density) west-bank bulge into a rebuilt s1301 subfile.

The smoothed waterline inserts WB_SUBDIV stations per segment (yellowdrops_westbank_data), which GROWS the
meshes — so instead of the in-place float patches of the old 3-station bulge, this tool rebuilds
the three nested blocks of gedit/s13/scene.scn's s1301 subfile:

  visual MDS (0xb20): grid10 + grid11 MDTs rebuilt via mdt_codec — every triangle edge that spans
      two adjacent bank stations is split at the (attribute-lerped) midpoint, then all station
      columns (old + new) shift west by the sine profile. UVs/normals/colours interpolate, so the
      texture stays continuous.
  s1301_a MDS: the crown collision wall + walkable floor, same edge-split + shift (floor triangles
      contain the station edges, so they follow the curve — no chordal gaps).
  s1301_c MDS: the miti_c camera wall + floor, same treatment on the camera's own station line.

Mechanics: each edited MDT is rebuilt; each nested MDS is re-laid (contiguous blocks, mds-relative
`mo` in the node table); the sub header's block (offset,size) words are repointed; the sub is
emitted whole as game_data/yellowdrops/yellowdrops_westbank_ground.bin for IsoPatcher.ReplaceS13Ground (which
appends it to scene.scn and repoints the directory entry — the old sub bytes become dead space).

Run: python3 tools/yellowdrops/bake_yellowdrops_westbank.py       (runs the self-tests, then writes the bin)
"""
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'lib'))
import toolpath  # noqa: F401 — puts every tools/ subfolder on sys.path
import os, sys, struct
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from extract_scene_mesh import load_scene
import scene_placed
import mdt_codec
from yellowdrops_westbank_data import WB_ROWS, wb_profile, WB_SUBDIV
from yellowdrops_camera_pillars import pillar_hulls, doumu_hug_xz
from georama_collision import build_coll_mdt

Y_LOCAL = 210.0            # s1301 node frames sit at world y + 210 (translation-only matrices)
TOL = 0.35

# station chains (vanilla local coords) per mesh, split into the pieces each mesh owns
def _chain(row, lo_z, hi_z):
    return [(x, y + Y_LOCAL, z) for x, y, z in WB_ROWS[row] if lo_z <= z <= hi_z]

CHAINS = {
    'grid10': [_chain('edge_top', -150, 0), _chain('edge_bot', -150, 0), _chain('crown', -150, 0)],
    'grid11': [_chain('edge_top', -30, 280), _chain('edge_bot', -30, 280), _chain('crown', -30, 280)],
    '_a':     [[(x, 240.0, z) for x, y, z in WB_ROWS['crown']],          # wall base = floor edge
               [(x, 336.0, z) for x, y, z in WB_ROWS['crown']]],         # wall top
    '_c':     [[(x, 240.0, z) for x, y, z in WB_ROWS['cam']],            # camera wall top/floor edge
               [(x, 200.0, z) for x, y, z in WB_ROWS['cam']]],           # camera wall bottom (y-10)
}


def _segments(chains):
    """[(A,B,mids)] for every station pair, with the inserted midpoint(s) (vanilla positions)."""
    segs = []
    for ch in chains:
        for a, b in zip(ch, ch[1:]):
            mids = [tuple(a[j] + (b[j] - a[j]) * k / (WB_SUBDIV + 1.0) for j in range(3))
                    for k in range(1, WB_SUBDIV + 1)]
            segs.append((a, b, mids))
    return segs


def _colmatch(p, s):
    return abs(p[0] - s[0]) <= TOL and abs(p[1] - s[1]) <= 1.0 and abs(p[2] - s[2]) <= TOL


def _all_columns(chains):
    cols = []
    for ch in chains:
        cols += list(ch)
        for a, b in zip(ch, ch[1:]):
            for k in range(1, WB_SUBDIV + 1):
                cols.append(tuple(a[j] + (b[j] - a[j]) * k / (WB_SUBDIV + 1.0) for j in range(3)))
    return cols


# ---------------- visual MDT edit (mdt_codec) ----------------------------------------------------
def edit_visual_mdt(scn_bytes, fo, chains):
    m = mdt_codec.parse_mdt(scn_bytes, fo)
    m.pos = [list(p) for p in m.pos]
    m.uv = [list(p) for p in m.uv]
    m.norm = [list(p) for p in m.norm]
    if m.has_col:
        m.col = [list(p) for p in m.col]
    # flatten to triangles (records keep their attribute indices)
    tris = []
    for prim, midx, recs in m.submeshes:
        if prim == 3:
            for k in range(0, len(recs) - 2, 3):
                tris.append((midx, [recs[k], recs[k + 1], recs[k + 2]]))
        elif prim == 4:
            for i in range(len(recs) - 2):
                a, b, c = (recs[i], recs[i + 1], recs[i + 2]) if i % 2 == 0 else (recs[i + 1], recs[i], recs[i + 2])
                if a[0] == b[0] or b[0] == c[0] or a[0] == c[0]:
                    continue                      # strip degenerates
                tris.append((midx, [a, b, c]))
        else:
            raise ValueError(f'prim {prim}')
    segs = _segments(chains)
    midpos = {}                                   # (segA,segB,k) -> pos index of created midpoint
    midattr = {}                                  # (iA_attr, iB_attr, block) -> new attr index

    def pos_of(i):
        return m.pos[i]

    def seg_of_edge(ia, ib):
        pa, pb = pos_of(ia), pos_of(ib)
        for a, b, mids in segs:
            if (_colmatch(pa, a) and _colmatch(pb, b)):
                return a, b, mids, False
            if (_colmatch(pa, b) and _colmatch(pb, a)):
                return a, b, mids, True
        return None

    def lerp_entry(block, ia, ib, t):
        key = (min(ia, ib), max(ia, ib), round(t, 4), block)
        if key in midattr:
            return midattr[key]
        va, vb = getattr(m, block)[ia], getattr(m, block)[ib]
        if ia > ib:
            t2 = 1.0 - t
        else:
            t2 = t
        nv = [va[j] + (vb[j] - va[j]) * t2 for j in range(len(va))]
        getattr(m, block).append(nv)
        midattr[key] = len(getattr(m, block)) - 1
        return midattr[key]

    def mid_rec(ra, rb, t):
        """New record at parameter t between records ra->rb (t measured from ra)."""
        # position: shared per segment (welded); attributes: lerped per attribute-pair
        key = (min(ra[0], rb[0]), max(ra[0], rb[0]), round(t if ra[0] < rb[0] else 1 - t, 4))
        if key not in midpos:
            pa, pb = pos_of(ra[0]), pos_of(rb[0])
            m.pos.append([pa[j] + (pb[j] - pa[j]) * t for j in range(len(pa))])
            midpos[key] = len(m.pos) - 1
        rec = [midpos[key], lerp_entry('uv', ra[1], rb[1], t)]
        rec.append(lerp_entry('norm', ra[2], rb[2], t) if m.norm else 0)
        if m.has_col:
            rec.append(lerp_entry('col', ra[3], rb[3], t))
        return tuple(rec)

    changed = True
    guard = 0
    while changed and guard < 30:
        changed = False
        guard += 1
        out = []
        for midx, recs in tris:
            done = False
            for e in range(3):
                ra, rb = recs[e], recs[(e + 1) % 3]
                rc = recs[(e + 2) % 3]
                hit = seg_of_edge(ra[0], rb[0])
                if hit:
                    mrec = mid_rec(ra, rb, 0.5)   # WB_SUBDIV==1: single midpoint
                    out.append((midx, [ra, mrec, rc]))
                    out.append((midx, [mrec, rb, rc]))
                    changed = True
                    done = True
                    break
            if not done:
                out.append((midx, recs))
        tris = out
    # NOTE: seg_of_edge matches FULL station->station edges only; the two split halves no longer
    # match (their endpoints include the midpoint), so one pass per edge suffices — `guard` is
    # belt-and-braces.

    # shift all station columns (old + new) by the profile
    cols = _all_columns(chains)
    nmoved = 0
    for p in m.pos:
        for c in cols:
            if _colmatch(p, c):
                p[0] += wb_profile(p[2])
                nmoved += 1
                break
    # write back as pure triangle-list submeshes grouped by material
    bymat = {}
    for midx, recs in tris:
        bymat.setdefault(midx, []).extend(recs)
    m.submeshes = [[3, midx, recs] for midx, recs in bymat.items()]
    # sync the block ENTRY COUNT header words the codec preserves verbatim
    # (hw[3]=pos via build_mdt, hw[5]=uv, hw[7]=col, hw[11]=norm)
    m.hdr[5] = len(m.uv)
    m.hdr[11] = len(m.norm)
    if m.has_col:
        m.hdr[7] = len(m.col)
    # REPAD: build_mdt reuses the pads captured from the source, but our DL grew by a
    # non-multiple of 16 — every later block (UV/NORM/MAT) would land misaligned, and the
    # VU upload needs qword-aligned block pointers (this was the in-game texture garbage).
    stride = 4 if m.has_col else 3
    dl_len = 16 + sum(12 + len(recs) * stride * 4 for _, _, recs in m.submeshes)
    blocklens = {'POS': 16 * len(m.pos), 'UV': 16 * len(m.uv), 'NORM': 16 * len(m.norm),
                 'COL': 16 * len(m.col) if m.has_col else 0, 'DL': dl_len,
                 'MAT': sum(len(x) for x in m.materials)}
    for nm2 in m.order:
        m.pads[nm2] = b'\x00' * ((-blocklens[nm2]) % 16)
    out = mdt_codec.build_mdt(m)
    hw2 = struct.unpack_from('<16I', out, 0)
    for w in (hw2[4], hw2[6], hw2[10], hw2[12], hw2[14]):
        assert w % 16 == 0, f'block offset 0x{w:x} misaligned'
    return out, nmoved


# ---------------- collision MDT edit -------------------------------------------------------------
def edit_coll_mdt(scn_bytes, fo, chains):
    w = list(struct.unpack_from('<16I', scn_bytes, fo))
    assert w[0] == 0x54444d
    POS, DL = w[4], w[10]
    total = w[2]
    tc = struct.unpack_from('<I', scn_bytes, fo + DL + 0x14)[0]
    recs = [list(struct.unpack_from('<3i2I', scn_bytes, fo + DL + 0x18 + t * 0x14)) for t in range(tc)]
    nv = max(r[k] for r in recs for k in range(3)) + 1
    pos = [list(struct.unpack_from('<4f', scn_bytes, fo + POS + i * 0x10)) for i in range(nv)]
    segs = _segments(chains)
    midcache = {}

    def seg_hit(ia, ib):
        pa, pb = pos[ia], pos[ib]
        for a, b, mids in segs:
            if (_colmatch(pa, a) and _colmatch(pb, b)) or (_colmatch(pa, b) and _colmatch(pb, a)):
                return True
        return False

    def midpoint(ia, ib):
        key = (min(ia, ib), max(ia, ib))
        if key not in midcache:
            pos.append([(pos[ia][j] + pos[ib][j]) / 2 for j in range(4)])
            midcache[key] = len(pos) - 1
        return midcache[key]

    changed = True
    guard = 0
    while changed and guard < 30:
        changed = False
        guard += 1
        out = []
        for r in recs:
            done = False
            for e in range(3):
                ia, ib = r[e], r[(e + 1) % 3]
                if seg_hit(ia, ib):
                    mi = midpoint(ia, ib)
                    r1, r2 = list(r), list(r)
                    r1[(e + 1) % 3] = mi
                    r2[e] = mi
                    out += [r1, r2]
                    changed = True
                    done = True
                    break
            if not done:
                out.append(r)
        recs = out
    cols = _all_columns(chains)
    nmoved = 0
    for p in pos:
        for c in cols:
            if _colmatch(p, c):
                p[0] += wb_profile(p[2])
                nmoved += 1
                break
    # APPEND-AND-REPOINT: the collision MDT holds blocks we haven't mapped beyond POS/DL, so the
    # ORIGINAL bytes are kept verbatim (dead POS/DL regions included) and the grown POS + DL are
    # appended at 16-aligned offsets with w[4]/w[10] repointed. Nothing else moves.
    out = bytearray(scn_bytes[fo: fo + total])
    while len(out) % 16:
        out += b'\x00'
    new_pos = len(out)
    for p in pos:
        out += struct.pack('<4f', *p)
    new_dl = len(out)
    out += scn_bytes[fo + DL: fo + DL + 0x18]            # DL preamble verbatim
    out += b''.join(struct.pack('<3i2I', *r) for r in recs)
    while len(out) % 16:
        out += b'\x00'
    struct.pack_into('<I', out, 0x08, len(out))          # w[2] total
    struct.pack_into('<I', out, 0x0c, len(pos))          # w[3] vertex count
    struct.pack_into('<I', out, 0x10, new_pos)           # w[4] POS offset
    struct.pack_into('<I', out, 0x28, new_dl)            # w[10] DL offset
    struct.pack_into('<I', out, new_dl + 0x14, len(recs))
    return bytes(out), nmoved


# ---------------- MDS container re-lay -----------------------------------------------------------
def relay_mds(sub, mds_off, mds_size, edits, add_nodes=None, template_node=1):
    """Rebuild the nested MDS at mds_off with per-node-name replacement MDT bytes, optionally
    APPENDING new nodes (cloned table entries from `template_node`, own name/index/mo)."""
    cnt, tbl = struct.unpack_from('<II', sub, mds_off + 8)
    nodes = []
    for i in range(cnt):
        b = mds_off + tbl + i * 0x70
        nm = sub[b + 8:b + 24].split(b'\x00')[0].decode('latin1')
        mo = struct.unpack_from('<i', sub, b + 0x28)[0]
        nodes.append((i, nm, mo))
    order = sorted([n for n in nodes if n[2]], key=lambda n: n[2])
    blocks = []
    for k, (i, nm, mo) in enumerate(order):
        end = order[k + 1][2] if k + 1 < len(order) else mds_size
        blocks.append((i, nm, mo, sub[mds_off + mo: mds_off + end]))
    add_nodes = add_nodes or []
    out = bytearray(sub[mds_off: mds_off + tbl + cnt * 0x70])
    for k, (nm, mdt) in enumerate(add_nodes):                 # cloned table entries
        ent = bytearray(sub[mds_off + tbl + template_node * 0x70: mds_off + tbl + (template_node + 1) * 0x70])
        struct.pack_into('<I', ent, 0, cnt + k)               # head word 0 = node index
        nmb = nm.encode('latin1')
        ent[8:24] = nmb + b'\x00' * (16 - len(nmb))
        out += ent
    struct.pack_into('<I', out, 8, cnt + len(add_nodes))
    for i, nm, mo, raw in blocks:
        new_mo = len(out)
        out += edits.get(nm, raw)
        while len(out) % 16:
            out += b'\x00'
        struct.pack_into('<i', out, tbl + i * 0x70 + 0x28, new_mo)
    for k, (nm, mdt) in enumerate(add_nodes):
        new_mo = len(out)
        out += mdt
        while len(out) % 16:
            out += b'\x00'
        struct.pack_into('<i', out, tbl + (cnt + k) * 0x70 + 0x28, new_mo)
    return bytes(out)


def main():
    scn = load_scene('gedit/s13/scene.scn')
    soff, ssize = scene_placed.scn_directory_map(scn)['s1301']
    sub = scn[soff:soff + ssize]
    VIS_OFF, VIS_SIZE = 0xb20, 0x39af0
    A_OFF, A_SIZE = 0x3a610, 0x9bd0
    C_OFF, C_SIZE = 0x441e0, 0x8870

    # stage 1: container round-trips (no edits) must be byte-exact
    assert relay_mds(sub, VIS_OFF, VIS_SIZE, {}) == sub[VIS_OFF:VIS_OFF + VIS_SIZE], 'visual relay drift'
    assert relay_mds(sub, A_OFF, A_SIZE, {}) == sub[A_OFF:A_OFF + A_SIZE], '_a relay drift'
    assert relay_mds(sub, C_OFF, C_SIZE, {}) == sub[C_OFF:C_OFF + C_SIZE], '_c relay drift'
    print('stage 1 OK: container round-trips byte-exact')

    # stage 2: visual grid10/grid11
    edits_vis = {}
    cnt, tbl = struct.unpack_from('<II', sub, VIS_OFF + 8)
    for i in range(cnt):
        b = VIS_OFF + tbl + i * 0x70
        nm = sub[b + 8:b + 24].split(b'\x00')[0].decode('latin1')
        if nm in ('grid10', 'grid11'):
            mo = struct.unpack_from('<i', sub, b + 0x28)[0]
            new, nmoved = edit_visual_mdt(sub, VIS_OFF + mo, CHAINS[nm])
            edits_vis[nm] = new
            print(f'stage 2: {nm} rebuilt ({len(new)} bytes, {nmoved} column verts shifted)')

    # stage 3: collision walls (find the nodes holding the station columns)
    def coll_edit(mds_off, mds_size, chains, tag):
        cnt2, tbl2 = struct.unpack_from('<II', sub, mds_off + 8)
        edits = {}
        for i in range(cnt2):
            b = mds_off + tbl2 + i * 0x70
            nm = sub[b + 8:b + 24].split(b'\x00')[0].decode('latin1')
            mo = struct.unpack_from('<i', sub, b + 0x28)[0]
            if not mo:
                continue
            fo = mds_off + mo
            if sub[fo:fo + 3] != b'MDT':
                continue
            # does this node contain any of the chain columns?
            w = struct.unpack_from('<16I', sub, fo)
            POS, DL = w[4], w[10]
            tc = struct.unpack_from('<I', sub, fo + DL + 0x14)[0]
            nv = max(struct.unpack_from('<3i', sub, fo + DL + 0x18 + t * 0x14)[k]
                     for t in range(tc) for k in range(3)) + 1
            stations = [c for ch in chains for c in ch]
            hitn = 0
            for vi in range(nv):
                p = struct.unpack_from('<3f', sub, fo + POS + vi * 0x10)
                if any(_colmatch(p, s) for s in stations):
                    hitn += 1
            if hitn:
                new, nmoved = edit_coll_mdt(sub, fo, chains)
                edits[nm] = new
                print(f'stage 3: {tag}/{nm} rebuilt ({hitn} station verts found, {nmoved} shifted)')
        return edits

    edits_a = coll_edit(A_OFF, A_SIZE, CHAINS['_a'], 's1301_a')
    edits_c = coll_edit(C_OFF, C_SIZE, CHAINS['_c'], 's1301_c')
    assert edits_a and edits_c, 'collision wall nodes not found'

    # stage 3a2: doumu_c hug — pull the factory camera ring in (sector-clamped, size unchanged).
    # The node's accumulated frame carries a SCALE, so verts go local -> world (node matrix),
    # hug in world space, then back through the inverted 3x3.
    from extract_scene_mesh import xform as _xf
    nodes3, wm3 = scene_placed._accum(sub, C_OFF)
    for i, (nn3, mo3, par3, mat3) in enumerate(nodes3):
        if nn3 != 'doumu_c' or mo3 == 0:
            continue
        M = wm3(i)
        a, b2, c2 = M[0], M[4], M[8]
        d2, e2, f2 = M[1], M[5], M[9]
        g2, h2, i2 = M[2], M[6], M[10]
        det = a*(e2*i2 - f2*h2) - b2*(d2*i2 - f2*g2) + c2*(d2*h2 - e2*g2)
        inv = [(e2*i2 - f2*h2)/det, (c2*h2 - b2*i2)/det, (b2*f2 - c2*e2)/det,
               (f2*g2 - d2*i2)/det, (a*i2 - c2*g2)/det, (c2*d2 - a*f2)/det,
               (d2*h2 - e2*g2)/det, (b2*g2 - a*h2)/det, (a*e2 - b2*d2)/det]
        fo = C_OFF + mo3
        w = struct.unpack_from('<16I', sub, fo)
        blk = bytearray(sub[fo:fo + w[2]])
        nmv = 0
        for vi in range(w[3]):
            vx, vy, vz, vw = struct.unpack_from('<4f', blk, w[4] + vi * 0x10)
            wx, wy, wz = _xf(M, (vx, vy, vz))
            nx, nz = doumu_hug_xz(wx, wz)
            if abs(nx - wx) + abs(nz - wz) > 1e-6:
                rx, ry2, rz = nx - M[12], wy - M[13], nz - M[14]
                lx = inv[0]*rx + inv[1]*ry2 + inv[2]*rz
                lz = inv[6]*rx + inv[7]*ry2 + inv[8]*rz
                struct.pack_into('<4f', blk, w[4] + vi * 0x10, lx, vy, lz, vw)
                nmv += 1
        edits_c['doumu_c'] = bytes(blk)
        print(f'stage 3a2: doumu_c hugged ({nmv}/{w[3]} verts pulled in)')

    # stage 3b: pillar camera hulls as NEW _c nodes (own tight bboxes -> gather-culled per arch).
    # iriguti hulls dropped 2026-08-31 per user review (not beneficial in-game); extru gates only.
    hulls = pillar_hulls()
    namemap = {'extru_inner_S': 'pcam_xs', 'extru_inner_N': 'pcam_xn'}
    add_c = [(namemap[k], build_coll_mdt(d['tris'], y_shift=210.0)) for k, d in hulls.items() if k in namemap]
    print(f'stage 3b: {len(add_c)} pillar-hull camera nodes '
          f'({sum(len(hulls[k]["tris"]) for k in namemap)} tris total)')

    # stage 4: assemble the new sub
    vis = relay_mds(sub, VIS_OFF, VIS_SIZE, edits_vis)
    amds = relay_mds(sub, A_OFF, A_SIZE, edits_a)
    cmds = relay_mds(sub, C_OFF, C_SIZE, edits_c, add_nodes=add_c)
    new = bytearray(sub[:VIS_OFF]) + vis + amds + cmds
    a_off, c_off = VIS_OFF + len(vis), VIS_OFF + len(vis) + len(amds)
    for o in (0x4c, 0x50, 0x54, 0x78):
        struct.pack_into('<I', new, o, a_off)
    struct.pack_into('<I', new, 0x58, len(vis))
    struct.pack_into('<I', new, 0x7c, len(amds))
    for o in (0x90, 0xa8, 0xc0):
        struct.pack_into('<I', new, o, c_off)
    struct.pack_into('<I', new, 0xc4, len(cmds))
    OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'game_data', 'yellowdrops', 'yellowdrops_westbank_ground.bin'))
    with open(OUT, 'wb') as f:
        f.write(new)
    print(f'stage 4: new s1301 = {len(new)} bytes (was {ssize}, +{len(new) - ssize}) -> {OUT}')
    return bytes(new)


if __name__ == '__main__':
    main()
