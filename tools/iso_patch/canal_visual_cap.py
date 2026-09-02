#!/usr/bin/env python3
"""Queens (e03) canal WEST-END visual cap — close the see-through gap when looking up from the canal.

At low tide the player stands on the canal floor (y~0) at the west end (x -600..-200, z -50..50) and can
look UP through a hole in the visible town mesh: the x=-200 wall (y 50..70), the z=+/-50 slant walls and
the street above never got an underside, so the sky/void shows through. This adds the missing horizontal
cap at y=50 — two triangles spanning x[-600,-200] x z[-50,50] — hosted in `grid1__n` (sub-file e03g05,
main `_m` MDS; `__n` = two-sided, visible from below regardless of winding) as its OWN submesh cloning
the LEDGE's render data: the y=50 walkway-top quad at x[-700,-600] is the known-good daylight surface
next to the gap, and the cap continues it eastward with the ledge's material, uv sample and up-facing
normals. (History: appending records to an existing submesh garbled that submesh's other faces in-game —
the engine batches per-submesh record streams for VU1; and cloning the z=-50 WALL's records rendered
blue — that wall face's in-game look was never verified. Own submesh + ledge donors is the safe combo.)

The MDT grows, so the splice fixes every reference layer (all verified against the real container):
  * e03g05's main-MDS node table: meshOff (+0x28 of each 0x70 record) > the spliced MDT shifts.
  * the PTS TOC (header 0xE0): every u32 offset past the splice shifts — the far-LOD MDS (x3 entries),
    the 0x1F3F0 block, and the `_a` collision MDS. (Offsets before the splice, e.g. +0x64's 0x15290,
    are untouched; TOC values are only treated as offsets when they land inside the sub-file.)
  * the SCN directory: e03g05's size, and the offset of every later sub-file.

Run standalone for an offline structural check against the extracted disc:
    DC1_DATA_DIR=~/ROMs/dc_extracted python3 tools/iso_patch/canal_visual_cap.py
"""
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'lib'))
import toolpath  # noqa: F401 — puts every tools/ subfolder on sys.path
import os, re, struct, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))            # tools/ (mdt_codec, extract_scene_mesh)
sys.path.insert(0, os.path.join(HERE, "collision"))     # scene_placed (scn_directory_list)
import mdt_codec

SUB_NAME  = "e03g05"
HOST_NODE = b"grid1__n\x00"
TOC_SIZE  = 0xE0            # PTS header word[1] — the directory region holding sub-block offsets

# The cap = FOUR 100-unit quads (x -600..-200 in steps, z ±50) at y=50, not one 400-unit quad:
# every texcoord in this node sits inside [0,1] — the texture is GS-CLAMPED, never tiled, so a
# v beyond [0,1] smears the edge row into stripes (the observed artifact for both the single-texcoord
# and the v:0→−2 tiled attempts). Four quads with PING-PONG mirrored v (0→.5, .5→0, 0→.5, .5→0) keep
# every coordinate in range, share identical v at the seams (seamless), match the ledge's exact
# density (0.5 uv per 100 units), and need ZERO new texcoord entries — (0.5,0)/(0,0)/(0.5,0.5)/(0,0.5)
# all already exist in the node.
CAP_XS = [-600.0, -500.0, -400.0, -300.0, -200.0]
CAP_V  = [0.0, 0.5, 0.0, 0.5, 0.0]          # ping-pong v per column (x); u stays 0.5 @ z-50, 0 @ z+50
CAP_Y, CAP_ZN, CAP_ZS = 50.0, -50.0, 50.0
CAP_TRIS = []                                # world tris (viewer + verification); records built in add_canal_cap
for _i in range(4):
    x0, x1 = CAP_XS[_i], CAP_XS[_i + 1]
    a, b = (x0, CAP_Y, CAP_ZN), (x1, CAP_Y, CAP_ZN)
    c, d = (x1, CAP_Y, CAP_ZS), (x0, CAP_Y, CAP_ZS)
    CAP_TRIS += [(a, b, c), (a, c, d)]


from scene_placed import scn_directory_list


def _find_rec(m, pos, tol=0.5):
    """First display-list record (any submesh, prim 3) whose position matches `pos`."""
    for si, (prim, midx, recs) in enumerate(m.submeshes):
        if prim != 3:
            continue
        for r in recs:
            p = m.pos[r[0]]
            if all(abs(p[i] - pos[i]) <= tol for i in range(3)):
                return si, list(r)
    return None, None


def add_canal_cap(scn: bytes):
    """Return (new_scene_bytes, delta). Raises if the container doesn't look as expected."""
    entry = next((e for e in scn_directory_list(scn) if e[0] == SUB_NAME), None)
    if entry is None:
        raise KeyError(f"{SUB_NAME} not in scene directory")
    _, sub_off, sub_size, _ = entry
    sub = bytearray(scn[sub_off:sub_off + sub_size])

    # host node record -> its MDT (meshOff is relative to the containing MDS)
    at = sub.find(HOST_NODE)
    if at < 0:
        raise KeyError("grid1__n not found in e03g05")
    rec = at - 8
    mesh_off = struct.unpack_from("<i", sub, rec + 0x28)[0]
    mds_base = bytes(sub).rfind(b"MDS\x00", 0, at)
    mdt_off = mds_base + mesh_off                      # sub-relative
    if bytes(sub[mdt_off:mdt_off + 3]) != b"MDT":
        raise ValueError("grid1__n meshOff does not resolve to an MDT")

    m = mdt_codec.parse_mdt(bytes(sub), mdt_off)
    old_size = m.hdr[2]

    # ── The cap clones the LEDGE's appearance, not the wall's. The known-good daylight surface next to
    # the gap is the y=50 walkway-top quad at x[-700,-600] (its records: uv (0,1), up-facing normals,
    # its own material) — the cap continues that surface eastward, so it inherits ALL of the ledge's
    # render data. (The first attempt copied the z=-50 WALL's records — uv (0,0), wall normals, wall
    # material — and rendered wrong/blue in-game; that wall face's look was never actually verified.)
    LEDGE_W = (-700.0, 50.0, -50.0)
    ledge = {}     # rounded (x,z) of the ledge quad corner -> its record
    ledge_si = None
    for si_, (prim_, midx_, recs_) in enumerate(m.submeshes):
        if prim_ != 3:
            continue
        for k in range(0, len(recs_) - 2, 3):
            tri = [recs_[k], recs_[k + 1], recs_[k + 2]]
            pts = [m.pos[r[0]][:3] for r in tri]
            if all(abs(pt[1] - 50.0) < 0.5 for pt in pts) and all(-701 <= pt[0] <= -599 for pt in pts) and all(-51 <= pt[2] <= 51 for pt in pts):
                # ^ the z bound is LOAD-BEARING: the ledge continues in 100-unit segments to z=-500,
                #   and segment corners SHARE coordinates with different texcoords (each segment maps
                #   its own rect) — without it, last-writer-wins pollutes the donor map and the cap
                #   inherits a skewed, still-columnar mapping.
                for r, pt in zip(tri, pts):
                    ledge[(round(pt[0]), round(pt[2]))] = list(r)
                ledge_si = si_
    need = [(-600, -50), (-700, -50), (-700, 50), (-600, 50)]
    if ledge_si is None or any(kk not in ledge for kk in need):
        raise ValueError(f"ledge quad corners not resolved (have {sorted(ledge)})")

    # ── record semantics for these scene meshes (DECODED from the floor quad, which maps a perfect
    # texture rect): rec[1] indexes the hw[6] block = the per-vertex NORMAL (the codec calls it "uv"),
    # rec[2] indexes the hw[12] block = the TEXTURE COORDINATES (the codec calls it "norm") — the
    # labels are SWAPPED vs reality. And this node's texture is GS-CLAMPED (every vanilla texcoord in
    # [0,1]) — see the CAP_XS/CAP_V note at the top for why the cap is a 4-quad ping-pong strip.
    up_normal_idx = ledge[(-600, -50)][1]              # the ledge's rec[1] = the up normal (hw[6] idx 4)
    # (u,v) -> existing texcoord entry index, harvested from the ledge quad's own corners:
    tcs = {}
    for key in need:
        r = ledge[key]
        tc = m.norm[r[2]]
        tcs[(round(tc[0], 2), round(tc[1], 2))] = r[2]
    for want in ((0.5, 0.0), (0.0, 0.0), (0.5, 0.5), (0.0, 0.5)):
        if want not in tcs:
            raise ValueError(f"expected texcoord {want} not present on the ledge quad ({sorted(tcs)})")

    ledge_e = {(-600, -50): ledge[(-600, -50)][0], (-600, 50): ledge[(-600, 50)][0]}
    pos_w = m.pos[ledge_e[(-600, -50)]][3]             # W component copied from a real vert

    def _pos_idx(x, z):
        if (round(x), round(z)) in ledge_e:
            return ledge_e[(round(x), round(z))]       # reuse the ledge's own east-edge verts
        m.pos.append((x, CAP_Y, z, pos_w))
        return len(m.pos) - 1

    def _rec(x, z, v):
        u = 0.5 if z < 0 else 0.0
        return [_pos_idx(x, z), up_normal_idx, tcs[(u, round(v, 2))]]

    cap_recs = []
    for i in range(4):
        x0, x1 = CAP_XS[i], CAP_XS[i + 1]
        v0, v1 = CAP_V[i], CAP_V[i + 1]
        a, b = _rec(x0, CAP_ZN, v0), _rec(x1, CAP_ZN, v1)
        c, d = _rec(x1, CAP_ZS, v1), _rec(x0, CAP_ZS, v0)
        cap_recs += [a, b, c, a, c, d]

    # The cap gets its OWN submesh (the LEDGE's material), NOT an append to an existing record stream:
    # extending a submesh changes how the engine batches ITS records for VU1 and was observed to garble
    # that submesh's original faces in-game. A fresh submesh leaves every original stream byte-identical.
    ledge_midx = m.submeshes[ledge_si][1]
    m.submeshes.append((3, ledge_midx, cap_recs))

    # ── RE-ALIGN the inter-block pads for the NEW block sizes. build_mdt replays the pads CAPTURED
    # from the source layout — correct for a byte-identical round-trip, WRONG after a resize: vanilla's
    # display list needed 4 pad bytes to 16-align the UV block, but the grown display list ends aligned
    # already, so replaying the captured pad MISALIGNS every following block (UV/NORM/MAT) by 4. The
    # engine reads the 16-byte UV/normal entries with quadword loads, and EE lq IGNORES the low 4
    # address bits — every entry silently reads 4 bytes early = garbage UVs/normals over the whole
    # node. (THIS, not record data, was the in-game "corrupted texture" across all three cap attempts.)
    stride = 4 if m.has_col else 3
    sizes = {
        'POS':  len(m.pos) * 16,
        'DL':   16 + sum(12 + len(r) * stride * 4 for _p, _m2, r in m.submeshes),
        'UV':   len(m.uv) * 16,
        'NORM': len(m.norm) * 16,
        'COL':  (len(m.col) * 16) if m.has_col else 0,
        'MAT':  sum(len(x) for x in m.materials),
    }
    for nm in m.order:
        m.pads[nm] = b'\x00' * ((16 - sizes[nm] % 16) % 16)

    new_mdt = bytearray(mdt_codec.build_mdt(m))
    while len(new_mdt) % 0x10:
        new_mdt.append(0)
    # hard proof of alignment: every block pointer in the rebuilt header must be 16-aligned
    hw2 = struct.unpack_from('<16I', new_mdt, 0)
    for idx, what in ((4, 'POS'), (6, 'UV'), (10, 'DL'), (12, 'NORM'), (14, 'MAT')):
        if hw2[idx] % 16:
            raise ValueError(f"{what} block misaligned at {hw2[idx]:#x}")
    delta = len(new_mdt) - old_size
    if delta < 0:
        raise ValueError("cap edit shrank the MDT?!")

    new_sub = bytearray(sub[:mdt_off]) + new_mdt + sub[mdt_off + old_size:]

    # 1. main-MDS node table: shift meshOffs past the spliced MDT
    cnt, tbl = struct.unpack_from("<II", new_sub, mds_base + 8)
    fixed = 0
    for i in range(cnt):
        ro = mds_base + tbl + i * 0x70
        mo = struct.unpack_from("<i", new_sub, ro + 0x28)[0]
        if mo > mesh_off:
            struct.pack_into("<i", new_sub, ro + 0x28, mo + delta)
            fixed += 1

    # 2. PTS TOC: shift every u32 that lands inside the sub-file past the splice
    toc_fixed = []
    for i in range(8, TOC_SIZE, 4):
        v = struct.unpack_from("<I", new_sub, i)[0]
        if mdt_off + old_size <= v < sub_size:
            struct.pack_into("<I", new_sub, i, v + delta)
            toc_fixed.append((i, v))

    # 3. SCN directory: this sub grows; later subs shift
    out = bytearray(scn[:sub_off]) + new_sub + bytearray(scn[sub_off + sub_size:])
    for name, off, size, eoff in scn_directory_list(scn):
        if off == sub_off:
            struct.pack_into("<I", out, eoff + 0x14, size + delta)
        elif off > sub_off:
            struct.pack_into("<I", out, eoff + 0x10, off + delta)

    print(f"canal cap: +{len(CAP_TRIS)} tris in grid1__n (MDT {old_size:#x} -> {len(new_mdt):#x}, delta {delta:+#x}); "
          f"{fixed} meshOffs, {len(toc_fixed)} TOC offsets, scene {len(scn):#x} -> {len(out):#x}")
    return bytes(out), delta


if __name__ == "__main__":
    sys.path.insert(0, os.path.join(HERE, ".."))
    from extract_scene_mesh import load_scene, parse_mds

    def survey(scn):
        """(parse-failure set, cap-tri count) over every mesh the extractor can see. The vanilla scene has
        ~68 heuristic parse failures (parse_mds hands the strict codec non-mesh offsets) — the edit is
        judged by DIFF against that baseline, not by expecting 100%."""
        fails, cap = set(), 0
        want = {tuple(sorted(tuple(round(c) for c in p) for p in t)) for t in CAP_TRIS}
        for mm in re.finditer(rb"MDS\x00", scn):
            for name, mo, mat in parse_mds(scn, mm.start()):
                if mo == 0:
                    continue
                fo = next((c for c in (mo, mm.start() + mo) if 0 < c < len(scn) and scn[c:c + 3] == b"MDT"), None)
                if not fo:
                    continue
                try:
                    m = mdt_codec.parse_mdt(scn, fo)
                    mdt_codec.build_mdt(m)
                    if name == "grid1__n":
                        for prim, midx, recs in m.submeshes:
                            if prim != 3:
                                continue
                            for k in range(0, len(recs) - 2, 3):
                                t = tuple(sorted(tuple(round(c) for c in m.pos[recs[k + j][0]][:3]) for j in range(3)))
                                if t in want:
                                    cap += 1
                    if name == "grid1__n":
                        pass
                except Exception as e:
                    fails.add((name, str(e)[:60]))
        return fails, cap

    scn0 = load_scene("gedit/e03/scene.scn")
    base_fails, base_cap = survey(scn0)
    scn1, delta = add_canal_cap(scn0)
    new_fails, new_cap = survey(scn1)

    introduced = new_fails - base_fails
    print(f"verify: baseline fails {len(base_fails)}, patched fails {len(new_fails)}, "
          f"introduced {sorted(introduced) or 'NONE'}; cap tris {base_cap}->{new_cap}")
    assert not introduced and base_cap == 0 and new_cap == len(CAP_TRIS)
    print("OK")
