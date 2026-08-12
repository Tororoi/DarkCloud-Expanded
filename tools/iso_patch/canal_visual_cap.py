#!/usr/bin/env python3
"""Queens (e03) canal WEST-END visual cap — close the see-through gap when looking up from the canal.

At low tide the player stands on the canal floor (y~0) at the west end (x -600..-200, z -50..50) and can
look UP through a hole in the visible town mesh: the x=-200 wall (y 50..70), the z=+/-50 slant walls and
the street above never got an underside, so the sky/void shows through. This adds the missing horizontal
cap at y=50 — two triangles spanning x[-600,-200] x z[-50,50] — to the node that already owns the z=-50
slant wall (`grid1__n`, sub-file e03g05, main `_m` MDS): its `__n` suffix renders two-sided (visible from
below regardless of winding) and its texture/UV mapping runs along X, so the cap reads as the wall folding
over. The two cap corners the node already has (-600,50,-50 / -200,50,-50) are reused verbatim (position,
UV, normal, colour indices); the two z=+50 corners are appended copying the attributes of their matching
x twin, so the texture is uniform across Z.

The MDT grows, so the splice fixes every reference layer (all verified against the real container):
  * e03g05's main-MDS node table: meshOff (+0x28 of each 0x70 record) > the spliced MDT shifts.
  * the PTS TOC (header 0xE0): every u32 offset past the splice shifts — the far-LOD MDS (x3 entries),
    the 0x1F3F0 block, and the `_a` collision MDS. (Offsets before the splice, e.g. +0x64's 0x15290,
    are untouched; TOC values are only treated as offsets when they land inside the sub-file.)
  * the SCN directory: e03g05's size, and the offset of every later sub-file.

Run standalone for an offline structural check against the extracted disc:
    DC1_DATA_DIR=~/ROMs/dc_extracted python3 tools/iso_patch/canal_visual_cap.py
"""
import os, re, struct, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))            # tools/ (mdt_codec, extract_scene_mesh)
sys.path.insert(0, os.path.join(HERE, "collision"))     # bake_player_camera_collision (_dir)
import mdt_codec

SUB_NAME  = "e03g05"
HOST_NODE = b"grid1__n\x00"
TOC_SIZE  = 0xE0            # PTS header word[1] — the directory region holding sub-block offsets

# cap corners (world == node-local: grid nodes carry identity matrices)
C_NW = (-600.0, 50.0, -50.0)   # exists in grid1__n (slant-wall top, west)
C_NE = (-200.0, 50.0, -50.0)   # exists (slant-wall top, east)
C_SE = (-200.0, 50.0,  50.0)   # appended, attributes copied from C_NE
C_SW = (-600.0, 50.0,  50.0)   # appended, attributes copied from C_NW
CAP_TRIS = [(C_NW, C_NE, C_SE), (C_NW, C_SE, C_SW)]


def _dir(scn):
    from bake_player_camera_collision import _dir as d
    return d(scn)


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
    entry = next((e for e in _dir(scn) if e[0] == SUB_NAME), None)
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

    # corner records: reuse the existing west/east slant-wall tops; append the z=+50 twins
    si_nw, r_nw = _find_rec(m, C_NW)
    si_ne, r_ne = _find_rec(m, C_NE)
    if r_nw is None or r_ne is None:
        raise ValueError("expected slant-wall corner verts not found in grid1__n")
    si = si_ne                                          # host submesh = the wall's own (same material)

    def _appended(base_rec, pos):
        r = list(base_rec)
        m.pos.append((pos[0], pos[1], pos[2], m.pos[base_rec[0]][3]))
        r[0] = len(m.pos) - 1
        # UV/normal/colour indices stay = the base record's -> identical mapping across Z
        return r

    r_se = _appended(r_ne, C_SE)
    r_sw = _appended(r_nw, C_SW)
    by_corner = {C_NW: r_nw, C_NE: r_ne, C_SE: r_se, C_SW: r_sw}

    # The cap gets its OWN submesh (same material as the canal wall's), NOT an append to the wall's
    # record stream: extending an existing submesh changes how the engine batches ITS records for VU1
    # and was observed to garble that submesh's original faces in-game — exactly the "adjacent polys"
    # next to the cap. A fresh submesh leaves every original record stream byte-identical.
    prim, midx, _ = m.submeshes[si]
    assert prim == 3
    cap_recs = [list(by_corner[c]) for tri in CAP_TRIS for c in tri]
    m.submeshes.append((3, midx, cap_recs))

    new_mdt = bytearray(mdt_codec.build_mdt(m))
    while len(new_mdt) % 0x10:
        new_mdt.append(0)
    # keep the on-disc footprint 0x10-aligned like the original (old blocks are back-to-back)
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
    for name, off, size, eoff in _dir(scn):
        if off == sub_off:
            struct.pack_into("<I", out, eoff + 0x14, size + delta)
        elif off > sub_off:
            struct.pack_into("<I", out, eoff + 0x10, off + delta)

    print(f"canal cap: +2 tris in grid1__n (MDT {old_size:#x} -> {len(new_mdt):#x}, delta {delta:+#x}); "
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
    assert not introduced and base_cap == 0 and new_cap == 2
    print("OK")
