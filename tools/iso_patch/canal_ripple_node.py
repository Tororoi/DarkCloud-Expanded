#!/usr/bin/env python3
"""Queens (e03) wading RIPPLE node — a persistent, texture-ANIMATED ripple decal for the low-tide canal.

The look to match is the plant/stilt rings (Brownboo's "foam interior" `s04w01__za01`, Norune's
`hamon__A01z`): a PERSISTENT MESH whose ring shape is geometry and whose foam churns via the town
TEX_ANIME system (texanime.cfg: 64x512 strip -> target texture, one 64x64 frame blit every `rate`
ticks). Queens' own texanime already animates exactly one such texture: `e01b23` -> `e01b22` rate 5
(波紋 "ripple ring", banks e01b02/e01b03.img listed in e03's WATER_IMG).

Those water-bank textures live in the WATER texture group, resident around the water pass — a mesh
sampling them must DRAW with the water part (a static part binding e01b22 rendered garbage in-game).
So this tool appends the ripple node INTO the water part's own sub-file (`e03c08`, the PTS that holds
`mizu__a01`): it draws in the water pass, group-correct, TEX_ANIME animating it natively, and the
runtime moves it with the PROVEN mizu frame lever (CanalTide: CFrame translation +0x200/204/208 +
world-dirty +0x240).

Geometry: Norune's `hamon__A01z` ripple splat (56 verts, flat ±86 @ Y0.2, material e01b22 — already
the right texture), verts scaled x0.45 -> ±39 around the ~75-unit-tall player. Node renamed
`wripl__za01` (z = no Z-write, a01 = alpha-test; parsed from the name by SetFrameAttr).

Surgery follows the VANILLA multi-node MDS shape exactly (v6.1 — the v6 append-only variant relocated
the node table and cloned mizu's record verbatim, producing THREE things no vanilla MDS has: tbl != 0x10,
a duplicate head-index 0, and a second root — black screen on load. 259 vanilla multi-node MDSes checked:
tbl is always 0x10, head[0] is always the node INDEX (head[1]=0x70 the record size), and there is always
exactly ONE root, children carrying parent=0):
  * the new node record is INSERTED at table position 1 (right after mizu): head index 1, parent 0
    (child of mizu — the runtime driver compensates mizu's tide translation), park translation
    (0,-3000,0) in its matrix. Everything after the table shifts +0x70; mizu's meshOff 0x80 -> 0xF0
    (still 16-aligned).
  * the ripple MDT is appended at the sub END (16-aligned); the new node's meshOff points there.
  * PTS header: the 8 SIZE_FIELDS (total-size markers) and the MDS size (+0x58) grow; SCN directory:
    this sub's size grows, later subs' offsets shift.

Run standalone for an offline structural check against the extracted disc:
    DC1_DATA_DIR=~/ROMs/dc_extracted python3 tools/iso_patch/canal_ripple_node.py
"""
import os, struct, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))            # tools/ (mdt_codec, extract_scene_mesh)
sys.path.insert(0, os.path.join(HERE, "collision"))     # bake_player_camera_collision (_dir)

SUB_NAME   = "e03c08"                # the Queens WATER part (hosts mizu__a01)
DONOR_NODE = b"hamon__A01z\x00"      # Norune's waterwheel ripple splat (e01 scene)
NODE_NAME  = b"wripl__za01"          # our node: z + a01 suffix flags, unique name for the runtime scan
SCALE      = 0.45                    # +-86 donor -> +-39 player ring
PARK_Y     = -3000.0                 # baked into the node matrix: loads hidden until CanalTide moves it


def _dir(scn):
    from bake_player_camera_collision import _dir as d
    return d(scn)


# ══ RING RETEXTURE — put the BOBBER's ripple sprite into Queens' animated ripple texture ═══════════
# The node's e01b22/e01b23 pair ships shore-FOAM art. The look to match is the cast bobber / hook
# ripple, which EffectHamon draws with the sprite `hamon` — a clean single circular ripple ring (e03
# img.pak → effect.img bank, 64x64 8bpp+CLUT, GS-swizzled). (The dungeon spring atlas `d00e01` was
# tried first but its rings are busy concentric arcs with radial spokes — read as inferior in-game.)
# Both e01b22 (64x64) and e01b23 (64x512 strip) are RGBA32 — we overwrite ONLY their pixel bytes in
# e03's img.pak (byte-size-identical, zero structural change) with an 8-frame expanding-ring loop
# synthesized from `hamon`; the vanilla texanime entry (e01b23 → e01b22 rate 5) then animates it.
# NOTE this also retextures the one other e01b22 user in Queens (fountain-water detail node
# `e02w05__a01z`) — the bobber ripple shows there too; revisit if it reads wrong in-game.

def _find_tim2(container: bytes, want: bytes):
    for magic in (b"IMG\x00", b"IM2\x00"):
        i = 0
        while True:
            i = container.find(magic, i)
            if i == -1:
                break
            count = struct.unpack_from("<I", container, i + 4)[0]
            if 0 < count < 64:
                for e in range(count):
                    ent = i + 0x10 + e * 0x30
                    if ent + 0x30 > len(container):
                        break
                    if container[ent:ent + 0x20].split(b"\0")[0] == want:
                        return i + struct.unpack_from("<I", container, ent + 0x20)[0]
            i += 4
    raise KeyError(f"{want!r} not found in container")


def _pak_bank(pak: bytes, entry_name: bytes) -> int:
    """Offset of the IM2 bank inside a named img.pak entry (entry table: name@+0, data/size/stride
    u32s @+0x40). The bobber sprite lives inside the `effect.img` entry's bank, not at pak top level."""
    p = 0
    while p < len(pak) and pak[p] != 0:
        name = pak[p:p + 0x20].split(b"\0")[0]
        data_off, _sz, stride = struct.unpack_from("<III", pak, p + 0x40)
        if name == entry_name:
            return p + data_off
        p += stride
    raise KeyError(f"{entry_name!r} entry not in img.pak")


def _unswizzle8(data, w, h):
    out = bytearray(w * h)
    for y in range(h):
        for x in range(w):
            block_loc = (y & ~0xF) * w + (x & ~0xF) * 2
            swap_sel = (((y + 2) >> 2) & 0x1) * 4
            pos_y = (((y & ~3) >> 1) + (y & 1)) & 0x7
            col_loc = pos_y * w * 2 + ((x + swap_sel) & 0x7) * 4
            bn = ((y >> 1) & 1) + ((x >> 2) & 2)
            src = block_loc + col_loc + bn
            if src < len(data):
                out[y * w + x] = data[src]
    return out


def _hamon_ring_rgba(pak: bytes):
    """The bobber/hook ripple sprite `hamon` (e03 effect.img, 64x64 8bpp+CLUT, GS-swizzled) as
    [(r,g,b,a)] with PS2 alpha (0x80 = opaque) — the EXACT texture EffectHamon draws for the cast
    bobber. Luminance-keyed + gained so the clean single ring stands out over the water (the sprite
    is authored dim for the effect system's additive blend; our node uses a plain alpha blend)."""
    bank = _pak_bank(pak, b"effect.img")
    count = struct.unpack_from("<I", pak, bank + 4)[0]
    t = None
    for e in range(count):
        ent = bank + 0x10 + e * 0x30
        if pak[ent:ent + 0x20].split(b"\0")[0] == b"hamon":
            t = bank + struct.unpack_from("<I", pak, ent + 0x20)[0]
            break
    if t is None:
        raise KeyError("hamon not in effect.img bank")
    clut_sz, img_sz = struct.unpack_from("<II", pak, t + 0x14)
    hdr_sz = struct.unpack_from("<H", pak, t + 0x1C)[0]
    w, h = struct.unpack_from("<HH", pak, t + 0x24)
    if (w, h) != (64, 64) or pak[t + 0x23] != 5:
        raise ValueError(f"hamon unexpected shape {w}x{h} type {pak[t+0x23]}")
    img = pak[t + 0x10 + hdr_sz: t + 0x10 + hdr_sz + img_sz]
    clut = pak[t + 0x10 + hdr_sz + img_sz: t + 0x10 + hdr_sz + img_sz + clut_sz]
    pal = []
    for i in range(256):                                  # CSM1: swap entry groups 8-15 <-> 16-23 per 32
        blk, idx = i // 32, i % 32
        if 8 <= idx < 16:
            idx += 8
        elif 16 <= idx < 24:
            idx -= 8
        j = (blk * 32 + idx) * 4
        pal.append(tuple(clut[j:j + 4]))
    de = _unswizzle8(img, w, h)
    # ⚠ ALL the ring art is in the TOP-LEFT 32x32 quadrant (RE'd: the other 3 quadrants are empty;
    # EffectHamon draws exactly this via a 32x32 source rect at origin) — a CENTERED double concentric
    # ring. Mapping the full 64x64 put a tiny ring in one corner (off-centre + small + the 2nd ring too
    # small to read). Sample only that quadrant, 2x nearest-neighbour upscaled to fill 64x64, so the
    # double ring maps centred and full-size onto the quad — the actual cast-bobber look.
    # RING_GAIN lifts BOTH colour (brightness) and alpha (toward opaque) past plain normalization so
    # the ring reads clearly over the water instead of a subtle wash. Tune here — 1.0 = plain normalize.
    RING_GAIN = 2.2
    quad = [pal[de[(y // 2) * 64 + (x // 2)]] for y in range(64) for x in range(64)]   # TL 32x32, 2x up
    peak = max(max(r, g, b) for r, g, b, _a in quad) or 1
    out = []
    for r, g, b, _a in quad:
        lum = max(r, g, b)
        out.append((min(255, int(r * 255 * RING_GAIN // peak)), min(255, int(g * 255 * RING_GAIN // peak)),
                    min(255, int(b * 255 * RING_GAIN // peak)), min(0x80, int(lum * 0x80 * RING_GAIN // peak))))
    return out


def _ring_frames(pak: bytes):
    """8 frames, 64x64 RGBA each: the `hamon` ring zooming outward with a fade-out tail — the cast
    bobber's expanding ripple as a seamless-enough 8-frame loop (rate 5 -> ~0.7 s cycle)."""
    src = _hamon_ring_rgba(pak)
    frames = []
    for f in range(8):
        s = 0.70 + 0.45 * f / 7.0                          # zoom 0.70 -> 1.15
        env = (0.55, 1.0, 1.0, 1.0, 1.0, 0.9, 0.7, 0.4)[f] # alpha envelope: pop in, dissolve out
        out = []
        for y in range(64):
            for x in range(64):
                sx = int(32 + (x - 32) / s + 0.5)
                sy = int(32 + (y - 32) / s + 0.5)
                if 0 <= sx < 64 and 0 <= sy < 64:
                    r, g, b, a = src[sy * 64 + sx]
                else:
                    r, g, b, a = 0, 0, 0, 0
                out.append((r, g, b, int(a * env)))
        frames.append(out)
    frames.reverse()   # TEX_ANIME plays the strip in the opposite order to how it built here —
                       # in-game the un-reversed sequence CONTRACTED; reversed = expanding outward.
    return frames


def retexture_ripple_bank(pak: bytes) -> bytes:
    """e03/img.pak with e01b22 + e01b23's PIXELS replaced by the bobber-ripple animation (in place).
    Source `hamon` lives in this same pak, so no external container is needed."""
    out = bytearray(pak)
    frames = _ring_frames(pak)
    for name, fw, fh, blocks in ((b"e01b22", 64, 64, [frames[0]]), (b"e01b23", 64, 512, frames)):
        t = _find_tim2(out, name)
        clut_sz, img_sz = struct.unpack_from("<II", out, t + 0x14)
        hdr_sz = struct.unpack_from("<H", out, t + 0x1C)[0]
        w, h = struct.unpack_from("<HH", out, t + 0x24)
        if (w, h, out[t + 0x23], clut_sz) != (fw, fh, 3, 0) or img_sz != fw * fh * 4:
            raise ValueError(f"{name!r}: unexpected TIM2 shape {w}x{h} t{out[t+0x23]} img {img_sz}")
        base = t + 0x10 + hdr_sz
        for bi, fr in enumerate(blocks):
            for i, (r, g, b, a) in enumerate(fr):
                o = base + (bi * fw * fw + i) * 4
                out[o] = r; out[o + 1] = g; out[o + 2] = b; out[o + 3] = a
    if len(out) != len(pak):
        raise ValueError("retexture changed the pak size?!")
    print("ripple retexture: e01b22 + e01b23 pixels -> hamon bobber-ripple 8-frame loop (pak size unchanged)")
    return bytes(out)


def carve_donor_mdt(e01scn: bytes) -> bytearray:
    """The raw hamon__A01z MDT from Norune's scene, verts scaled in place (sizes unchanged, so the
    block is byte-layout-identical to the vanilla one — internal offsets are MDT-relative)."""
    at = e01scn.find(DONOR_NODE)
    if at < 0:
        raise KeyError("hamon__A01z not found in e01 scene")
    rec = at - 8
    mds = e01scn.rfind(b"MDS\x00", 0, at)
    mesh_off = struct.unpack_from("<i", e01scn, rec + 0x28)[0]
    mdt_off = mds + mesh_off
    if e01scn[mdt_off:mdt_off + 3] != b"MDT":
        raise ValueError("hamon__A01z meshOff does not resolve to an MDT")
    total = struct.unpack_from("<I", e01scn, mdt_off + 8)[0]
    mdt = bytearray(e01scn[mdt_off:mdt_off + total])
    n_pos = struct.unpack_from("<I", mdt, 0xC)[0]
    pos = struct.unpack_from("<I", mdt, 0x10)[0]
    for v in range(n_pos):
        for k in range(3):
            o = pos + v * 16 + k * 4
            struct.pack_into("<f", mdt, o, struct.unpack_from("<f", mdt, o)[0] * SCALE)
    # material sanity — the whole design keys on this name
    mat = struct.unpack_from("<I", mdt, 0x38)[0]
    mname = bytes(mdt[mat + 0x34:mat + 0x54]).split(b"\0")[0]
    if mname != b"e01b22":
        raise ValueError(f"donor material is {mname!r}, expected e01b22")
    while len(mdt) % 16:
        mdt.append(0)
    return mdt


def add_wading_ripple(scn: bytes, e01scn: bytes):
    """Return (new_scene_bytes, delta)."""
    entry = next((e for e in _dir(scn) if e[0] == SUB_NAME), None)
    if entry is None:
        raise KeyError(f"{SUB_NAME} not in scene directory")
    _, sub_off, sub_size, _ = entry
    if sub_off % 16 or sub_size % 16:
        raise ValueError(f"{SUB_NAME} not 16-aligned ({sub_off:#x}/{sub_size:#x})")
    sub = bytearray(scn[sub_off:sub_off + sub_size])

    mds = sub.find(b"MDS\x00")
    ver, cnt, tbl = struct.unpack_from("<III", sub, mds + 4)
    if cnt != 1 or tbl != 0x10:
        raise ValueError(f"{SUB_NAME} MDS unexpected (count={cnt}, tbl={tbl:#x})")
    n0 = mds + tbl
    if bytes(sub[n0 + 8:n0 + 0x28]).split(b"\0")[0] != b"mizu__a01":
        raise ValueError("node 0 is not mizu__a01")
    h0, h1 = struct.unpack_from("<II", sub, n0)
    if (h0, h1) != (0, 0x70):
        raise ValueError(f"mizu node head unexpected {(h0, h1)}")
    mizu_mesh_off = struct.unpack_from("<i", sub, n0 + 0x28)[0]

    mdt = carve_donor_mdt(e01scn)

    insert_at = n0 + 0x70                                # right after mizu's record (= old table end)
    tail_shift = 0x70
    ripple_mdt_off_abs = sub_size + tail_shift           # appended at (shifted) sub end — 16-aligned
    node1 = bytearray(sub[n0:n0 + 0x70])                 # start from mizu's record for the misc fields
    struct.pack_into("<II", node1, 0, 1, 0x70)           # head: node INDEX 1, record size 0x70
    node1[8:0x28] = NODE_NAME + b"\0" * (0x20 - len(NODE_NAME))
    struct.pack_into("<i", node1, 0x28, ripple_mdt_off_abs - mds)   # meshOff (MDS-relative)
    struct.pack_into("<I", node1, 0x2C, 0)               # parent = node 0 (mizu) — the ONE root, like vanilla
    for r in range(3):                                   # identity 3x3
        for c in range(3):
            struct.pack_into("<f", node1, 0x30 + (r * 4 + c) * 4, 1.0 if r == c else 0.0)
    struct.pack_into("<3f", node1, 0x60, 0.0, PARK_Y, 0.0)          # parked until CanalTide moves it

    new_sub = sub[:insert_at] + bytes(node1) + sub[insert_at:] + mdt
    struct.pack_into("<I", new_sub, mds + 8, 2)                     # count=2 (tbl stays 0x10)
    struct.pack_into("<i", new_sub, n0 + 0x28, mizu_mesh_off + tail_shift)   # mizu MDT shifted +0x70
    if (mizu_mesh_off + tail_shift) % 16 or ripple_mdt_off_abs % 16:
        raise ValueError("MDT misaligned after insert")
    delta = len(new_sub) - sub_size
    if delta % 16:
        raise ValueError("growth not 16-aligned")

    # PTS header fixups (field semantics per IsoPatcher's part builder: SIZE_FIELDS 0x4C/50/54/78/90/
    # A8/C0/D8 are end-of-part markers for absent sub-blocks — all hold the total size; +0x48 = MDS
    # offset (unchanged); +0x58 = MDS byte SIZE, which must grow to cover the appended table + MDT
    # (tbl/meshOff are MDS-relative — a loader honoring +0x58 would otherwise truncate them away).
    fixed = 0
    for i in range(8, 0xE0, 4):
        if struct.unpack_from("<I", new_sub, i)[0] == sub_size:
            struct.pack_into("<I", new_sub, i, len(new_sub)); fixed += 1
    if struct.unpack_from("<I", new_sub, 0x58)[0] != sub_size - mds:
        raise ValueError("PTS +0x58 is not the MDS size — header layout drifted")
    struct.pack_into("<I", new_sub, 0x58, len(new_sub) - mds)

    out = bytearray(scn[:sub_off]) + new_sub + bytearray(scn[sub_off + sub_size:])
    for name, off, size, eoff in _dir(scn):
        if off == sub_off:
            struct.pack_into("<I", out, eoff + 0x14, size + delta)
        elif off > sub_off:
            struct.pack_into("<I", out, eoff + 0x10, off + delta)

    print(f"wading ripple: node '{NODE_NAME.decode()}' + {len(mdt):#x}-byte MDT appended to {SUB_NAME} "
          f"(sub {sub_size:#x} -> {len(new_sub):#x}, delta {delta:+#x}, {fixed} size-fields + MDS size fixed); "
          f"scene {len(scn):#x} -> {len(out):#x}")
    return bytes(out), delta


if __name__ == "__main__":
    from extract_scene_mesh import load_scene
    sys.path.insert(0, os.path.join(HERE, ".."))
    import mdt_codec

    scn0 = load_scene("gedit/e03/scene.scn")
    e01 = load_scene("gedit/e01/scene.scn")
    scn1, delta = add_wading_ripple(scn0, e01)

    # 1) the e03c08 sub still parses: both nodes resolve to valid MDTs, ripple extents are player-scale
    entry = next(e for e in _dir(scn1) if e[0] == SUB_NAME)
    _, off, size, _ = entry
    sub = scn1[off:off + size]
    mds = sub.find(b"MDS\x00")
    ver, cnt, tbl = struct.unpack_from("<III", sub, mds + 4)
    assert cnt == 2, cnt
    names = []
    for i in range(cnt):
        no = mds + tbl + i * 0x70
        nm = sub[no + 8:no + 0x28].split(b"\0")[0].decode()
        mo = struct.unpack_from("<i", sub, no + 0x28)[0]
        m = mdt_codec.parse_mdt(sub, mds + mo)
        mdt_codec.build_mdt(m)
        xs = [p[0] for p in m.pos]; ys = [p[1] for p in m.pos]; zs = [p[2] for p in m.pos]
        names.append(nm)
        print(f"  node[{i}] '{nm}' meshOff={mo:#x} verts={len(m.pos)} "
              f"X[{min(xs):.1f},{max(xs):.1f}] Y[{min(ys):.1f},{max(ys):.1f}] Z[{min(zs):.1f},{max(zs):.1f}]")
        h0, h1 = struct.unpack_from("<II", sub, no)
        par = struct.unpack_from("<I", sub, no + 0x2C)[0]
        assert (h0, h1) == (i, 0x70), (i, h0, h1)                    # vanilla head shape: index + record size
        assert par == (0xFFFFFFFF if i == 0 else 0), (i, par)        # ONE root, child parents to it
        if nm == NODE_NAME.decode():
            assert max(xs) < 45 and min(xs) > -45, "ripple not scaled"
            trans = struct.unpack_from("<3f", sub, no + 0x60)
            assert abs(trans[1] - PARK_Y) < 0.5, trans
    assert names == ["mizu__a01", NODE_NAME.decode()], names
    assert tbl == 0x10, tbl

    # 2) mizu's MDT bytes are UNTOUCHED, just shifted +0x70 (insert-style surgery)
    e0 = next(e for e in _dir(scn0) if e[0] == SUB_NAME)
    old = scn0[e0[1]:e0[1] + e0[2]]
    assert sub[0x1E0 + 0x70:e0[2] + 0x70] == old[0x1E0:]             # mizu MDT byte-identical at +0x70

    # 3) every OTHER sub-file's bytes are byte-identical at its (shifted) offset
    d0 = {n: (o, s) for n, o, s, _ in _dir(scn0)}
    d1 = {n: (o, s) for n, o, s, _ in _dir(scn1)}
    assert set(d0) == set(d1)
    moved = 0
    for n in d0:
        if n == SUB_NAME:
            continue
        o0, s0 = d0[n]; o1, s1 = d1[n]
        assert s0 == s1, n
        assert scn0[o0:o0 + s0] == scn1[o1:o1 + s1], f"{n} bytes changed"
        if o0 != o1: moved += 1
    print(f"verify: e03c08 2-node parse OK, mizu bytes identical, {moved} later subs shifted intact")
    print("OK")
