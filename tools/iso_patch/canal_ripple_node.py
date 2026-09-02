#!/usr/bin/env python3
"""Queens (e03) wading-ripple TEXTURE bake: overwrite e01b22 + e01b23's PIXELS in e03's img.pak with an
8-frame expanding-ring loop synthesized from the bobber-ripple sprite `hamon` (byte-size-identical, zero
structural change); the vanilla texanime entry (e01b23 -> e01b22 rate 5) then animates it. Consumed by
patch_iso_town_collision (retexture_ripple_bank).

The ripple NODE itself is a static part the C# ISO bake injects (IsoAssetCarver.CarveRippleDecal ->
IsoPatcher "wripple"/"wriplL"/"wriplR"; CanalWaterEffects drives it at runtime). The Python MDS node-surgery
prototype that used to live here (v6.1 insert-at-table-position-1) was superseded by that path and removed
2026-09 — recoverable via git.
"""
import os, struct, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))            # tools/ (mdt_codec, extract_scene_mesh)

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


def _bilerp(px, w, h, fx, fy):
    """Bilinearly sample a flat row-major RGBA list `px` (w*h) at fractional (fx, fy); returns int RGBA."""
    fx = 0.0 if fx < 0 else (w - 1.0 if fx > w - 1 else fx)
    fy = 0.0 if fy < 0 else (h - 1.0 if fy > h - 1 else fy)
    x0 = int(fx); y0 = int(fy); x1 = min(x0 + 1, w - 1); y1 = min(y0 + 1, h - 1)
    dx = fx - x0; dy = fy - y0
    c00 = px[y0 * w + x0]; c10 = px[y0 * w + x1]; c01 = px[y1 * w + x0]; c11 = px[y1 * w + x1]
    return tuple(int(c00[i] * (1 - dx) * (1 - dy) + c10[i] * dx * (1 - dy)
                     + c01[i] * (1 - dx) * dy + c11[i] * dx * dy + 0.5) for i in range(4))


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
    RING_GAIN = 2.5
    # 32x32 TL quadrant as RGBA, then BILINEARLY upscaled to 64x64 (was nearest-neighbour "// 2", which
    # left the ring visibly blocky). Bilinear can't add detail past the 32x32 source but smooths the edges
    # so the ring reads as a soft ring rather than stair-stepped pixels.
    tl = [pal[de[sy * 64 + sx]] for sy in range(32) for sx in range(32)]
    quad = [_bilerp(tl, 32, 32, x / 2.0, y / 2.0) for y in range(64) for x in range(64)]
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
                sx = 32 + (x - 32) / s
                sy = 32 + (y - 32) / s
                if 0 <= sx <= 63 and 0 <= sy <= 63:                # bilinear zoom (was nearest) = smooth edges
                    r, g, b, a = _bilerp(src, 64, 64, sx, sy)
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
