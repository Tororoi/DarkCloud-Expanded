#!/usr/bin/env python3
r"""Divine Beast Title "cat shot" — bake Xiao's cat rig INTO her dungeon character pack.

Host = `dun\mainchara\c04b.chr` (base.cfg), the pack every dungeon loads whenever Xiao is the active character
(LoadChara2 → LoadPackData2 into the 3.36 MB character heap). The cat rides along as 37 extra nodes in her frame
ARRAY that are never linked into her tree (parent -1 → SetParent(frame, NULL): no draw, skinning or DMA while
hidden — a parented-but-hidden cat froze the menus, whose draw buffers are smaller than the dungeon's), plus a
SECOND motion channel (MOTION 1, KEY_START 64:
stand / ready / run / take-off / leap / land). Step__10CCharacter only advances the channel that owns the current
motion id (GetMotionParam), and her own ids stop at 45, so the cat channel never poses her — which is why its
tracks can use bone indices RELATIVE to the cat root (0..36): the runtime copies just the cat subtree and plays
the channel on the copy, where the cat root IS node 0.

Why not the weapon pack: the weapon MENU rebuilds every carried weapon into a 944 KB arena (MenuExCashBuffer,
0xEC00 units) and copies each weapon's whole texture bank into it; a +290 KB weapon pack overflowed it and froze
the game. The character pack is never rebuilt by the menu.

What goes in (from gedit\s86\chara\c04cat.chr — the cat rig with the leap clips):
  .mds  37 nodes appended after Xiao's 79, names prefixed `cat_` (her rig already has `kao` and `skin`), the
        cat root renamed `catroot`, UNPARENTED (-1), its bind 3x3 scaled by HIDE_SCALE as well. 2 MDT chunks.
  .bbp  c04b.bbp + the cat's 37 rows (one row per model node); cat.bbp = the cat's own 37 rows for channel 1.
  .img  c04b01.img + the 5 cat textures the skin references (137 KB; FLAT_TEXTURES swaps them for tiny flat
        pictures — kept as an option, the blue/glow will be flash effects instead).
  cat.mot / cat.wgt   the cat's tracks, bone ids unchanged (relative), keyframes trimmed to the clip windows.
  cfg   `ALLOC_DBUFF "cat_skin"` (software-skinned double buffer for the cat body) + the MOTION 1 block.

Runtime (Weapons/Xiao/DivineBeastCat.cs) finds `catroot` in her live tree, deep-copies the cat subtree into the
clone caves, un-hides it and drives it with keys 64..69.

The bake the app runs at patch time is IsoPatch/CatPackBakes.cs (with WingBake.cs and CatWings.cs), a byte-exact port of
this module and its two companions; this stays as the AUTHORING model — the cat viewer (a dev tool outside the repo) runs
assemble() in memory to show what the bake builds — and as the generator of the glow palette blob the build embeds.

Usage:
  python3 tools/iso_patch/build_cat_pack.py --dc-dir "$DC1_DATA_DIR" --out /tmp/c04b_cat.chr   # dev build
  python3 tools/iso_patch/build_cat_pack.py --dc-dir "$DC1_DATA_DIR" --test
  python3 tools/iso_patch/build_cat_pack.py --dc-dir "$DC1_DATA_DIR" --palettes <catGlowPalettes.bin>
"""
import math
import os, sys, struct

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "lib"))
sys.path.insert(0, os.path.join(HERE, "..", "analysis"))
import mot_codec as mc             # noqa: E402
import img_surgery as im           # noqa: E402

HOST_CHR  = r"dun\mainchara\c04b.chr"
HOST_CFG, HOST_MDS, HOST_BBP, HOST_IMG = "base.cfg", "c04b.mds", "c04b.bbp", "c04b01.img"
CAT_CHR   = r"gedit\s86\chara\c04cat.chr"
FLOAT_CHR = r"gedit\e01\chara\e04c04cat.chr"   # the town cat's vertical float/hop-up (its #5 clip, frames 160..169)
FLOAT_SRC, FLOAT_DST = (160, 169), (285, 294)  # grafted into cat.mot where s86 has no keys (assemble_town_model.py slot 12)
GLOW_SRC  = r"dun\mpd_pack\d06main_a.mpd"     # the Gallery of Time map pack: its own fire.img holds the PURPLE torch glow disc
GLOW_CORE  = (15, 219, 255)                     # radial gradient: this at the centre …
GLOW_OUTER = (60, 67, 255)                      # … to this at the disc's edge
GLOW_CROSS = 0.125                              # radius fraction where the mix is halfway (0.5 = linear); smaller = the blue reaches further in
# Per-weapon glows: the Divine Beast Title keeps the blue disc; the Angel Shooter's cat glows WHITE, the
# Angel Gear's GOLD. Same disc, re-tinted; the runtime names the one to draw (mailbox CatGlowName → the glow cave).
# ⚠ ALL glow discs must be 64x64. DrawFire__9CFireOmni (0x161AC0) builds its two sprite layers with a FIXED
# texel rect — (0,0,64,64) and (2,2,124,124) — so a smaller disc is sampled outside itself and simply does not
# draw: authored at 32x32 to save heap, Fire/Thunder/Wind/Holy showed no glow at all while the two 64x64 discs
# (Ice, None) were fine. glow_tim2 keeps its `size` knob, but nothing may use it here.
# The three per-WEAPON looks are palette ROWS of the same 8-bit disc now, not textures of their own: three 64x64 RGBA32
# discs cost 49,344 B of the character heap between them, and that heap's leftover IS the weapons/effects pools the cat
# is already squeezing. Rows 0-5 are the elements below; the mod picks the row through
# Mailbox.CatGlowPalRow (ONE-based, 0 = let the cave derive it from the equipped element).
GLOW_LOOKS = [(GLOW_CORE, GLOW_OUTER),                 # 6 Divine Beast Title — the authored blue
              ((215, 215, 215), (180, 190, 215)),      # 7 Angel Shooter — white, a cool edge
              ((255, 238, 180), (255, 176, 40))]       # 8 Angel Gear — gold
# ⚠ NO MORE 64x64 DISCS FIT. Four per-element discs (orange/yellow/green/purple) were added here and FROZE the game:
# each is 16 KB in Xiao's character pack, and the weapons/effects pools are whatever the heap has left after her
# character data — with them loaded the log read `chara 3,899,600/4,240,000 ... effects 70,144/115,168`, i.e. 45 KB
# free, and opening the menu overflowed the effects pool into CDataAlloc2's silent spin. Raising
# the heap is not the way out either: that is what moved the dungeon pools and painted message text over the glyph
# sheet (see memory dungeon-pool-addresses-shift). The way to per-element glows is ONE 8-bit disc plus a palette
# ramp, the way the cape works — 16 KB + 1 KB for every colour instead of 16 KB each. That is GLOW_T8_NAME below.
GLOW_T8_NAME = "catglowp"                       # the per-ELEMENT glow: ONE 8-bit disc, 5,184 B, whose CLUT the cave repaints
# The GS reads a PSMT8 palette in CSM1 order, which exchanges bits 3 and 4 of the index (EnterTexture 0x1313B0 memcpy's the
# file's 1024 B to CTexture+0x48 and ReloadTexture blits them to VRAM verbatim — nothing de-swizzles them on the way, and
# every one of the game's 8712 T8 pictures ships clutType 3 with that bit clear). Rather than permute the table, the disc
# only ever uses indices the permutation LEAVES ALONE — the 128 values whose bits 3 and 4 match — so the cave can copy its
# table straight down. That is the same immunity the flat cape palette gets from having all 256 entries identical, except
# it costs nothing: the disc needs 115 levels and 128 are available.
CLUT_FIXED = [i for i in range(256) if (i & 0x18) in (0x00, 0x18)]
# Per element, indexed by the element byte (00 Fire, 01 Ice, 02 Thunder, 03 Wind, 04 Holy, 05 None): the radial gradient's
# centre and edge, in the style of the discs above. Ice, Thunder and Holy are the user's calls; the rest are
# still derived starting values — ElementLooks.Rgb is one CAPE colour and a gradient needs two. Tune here, re-run
# --palettes, re-patch. "None" is the dimmed white the Angel Shooter wears. NOTE the OUTER colour is what mostly shows:
# GLOW_CROSS 0.125 puts the mix halfway at an eighth of the radius, so ~80% of the disc is outer-weighted.
GLOW_ELEMENTS = [((128, 118, 52), (200,   5,   0)),   # 0 Fire     orange
                 ((  0, 200, 215), (  0,  4, 183)),   # 1 Ice      blue
                 ((255, 248, 190), (156, 131,  43)),   # 2 Thunder  yellow, a touch lighter
                 ((128, 255, 113), (  0, 86,  126)),   # 3 Wind     green
                 ((211, 73, 236), ( 33,   0, 175)),   # 4 Holy     purple, richer
                 ((50, 50, 50), (160, 160, 160))]   # 5 None     the dimmed white
GLOW_ROWS = GLOW_ELEMENTS + GLOW_LOOKS          # exactly what the cave's table holds, in row order
CAPE_CLO_NAME = "catcape.clo"                   # the cape's cloth definition record (wing_bake.CAPE_CLO)
DRAN_CHR  = r"dun\monstor\c12a.chr"              # the wing donor (tools/lib/cat_wings.py grafts its wings, wing_bake.py bakes them; read from the ISO)
WING_RGBA = (255, 255, 255, 0x80)                # the wings' flat texture: solid white, GS alpha 0x80 = opaque
CAPE_RGBA = (128, 28, 0, 0x80)                   # the Super Steve cape's flat texture. The colour on screen comes from the
                                                 # ambient the runtime gives the cloth (DivineBeastCat.CapeTint); a LIGHT base
                                                 # serves that twice over — the tint reads as its own colour instead of
                                                 # compounding with the texture, and the scene shading that shows the ripples
                                                 # multiplies a bright base instead of a crushed dark one

NODE_PREFIX   = "cat_"             # every cat bone (her rig already carries `kao`, `skin`, …)
CAT_ROOT_NAME = "catroot"          # what the runtime looks for in her tree
CAT_SKIN_NAME = NODE_PREFIX + "skin"
HIDE_SCALE    = 0.001              # bind 3x3 of the cat root on HER (the copy restores 1.0) — belt and braces
CAT_PARENT    = -1                 # UNPARENTED: LoadMDSFile 0x1262B0 calls SetParent(frame, NULL) for parent < 0, so
                                   # the cat never joins her tree — no draw, no skinning, no DMA while hidden. (The
                                   # parented+hidden variant froze the weapon/party menus, whose draw buffers are
                                   # smaller than the dungeon's.) It still sits in her frame ARRAY for the runtime scan.
FLAT_TEXTURES = False              # real cat fur (blue/glow will be flash effects, not a retexture)
FLAT_RGBA     = (150, 190, 255, 0x80)   # pale blue — the "blue cat" — GS alpha 0x80 = opaque
VERSION_MARK  = "//catpack v18 wings+cape"
KEY_START     = 64                 # cat channel key ids 64.. (her own ids end at 45)
CAT_KEYS = [                       # (start, end, speed, comment) — s86 c04cat windows; ids = KEY_START + index
    (10,  20,  0.1,  "cat stand"),
    (95,  105, 0.4,  "cat ready"),
    (120, 136, 0.5,  "cat run"),
    (190, 204, 0.5,  "cat take-off"),
    (205, 214, 0.5,  "cat leap"),
    (215, 227, 0.36, "cat land"),
    (60,  80,  1.0,  "cat walk (s86 KEY 2, brisk)"),
    (285, 294, 0.6,  "cat float-up (e04c04cat #5 160..169)"),   # 71: the vertical leap, as the town ladder jump
    (30,  40,  0.1,  "cat sit (s86 KEY 1)"),                    # 72: in place when there is no enemy to go for
]
MOT_WINDOWS = [(k[0], k[1]) for k in CAT_KEYS]


# ───────────────────────────────────────────── pack helpers ─────────────────────────────────────────────
_new_record = mc.new_record          # a fresh pack record (header + payload, 16-byte stride)


def _mds_nodes(pl):
    cnt = struct.unpack_from("<I", pl, 8)[0]
    out = []
    for i in range(cnt):
        p = 0x18 + i * 0x70
        nm = pl[p:p + 0x20].split(b"\0")[0].decode("latin1")
        mo, par = struct.unpack_from("<Ii", pl, p + 0x20)
        out.append((nm, mo, par))
    return out


class Bank:
    """Texture bank with the IM2 table layout (img_surgery.Img) under EITHER magic (`IMG\0` here)."""
    MAGICS = (b"IMG\x00", b"IM2\x00")

    def __init__(self, data):
        if data[:4] not in self.MAGICS:
            raise ValueError("not an IMG/IM2 bank")
        self.magic, self.data = data[:4], data
        self.count = struct.unpack_from("<I", data, 4)[0]
        self.entries = []
        for i in range(self.count):
            e = im.HDR + i * im.ENT
            self.entries.append((data[e:e + 0x20].split(b"\x00")[0].decode("latin1", "replace"),
                                 struct.unpack_from("<I", data, e + 0x20)[0]))

    def block(self, name):
        offs = sorted(o for _, o in self.entries)
        for n, o in self.entries:
            if n == name:
                nxt = next((x for x in offs if x > o), len(self.data))
                return self.data[o:nxt]
        raise KeyError(name)

    @staticmethod
    def build(magic, items):
        out = bytearray(struct.pack("<4sIII", magic, len(items), 0, 0)) + bytes(len(items) * im.ENT)
        for i, (name, blob) in enumerate(items):
            e = im.HDR + i * im.ENT
            nb = name.encode("latin1")[:0x1F]
            out[e:e + len(nb)] = nb
            struct.pack_into("<I", out, e + 0x20, len(out))
            out += blob
            out += b"\x00" * (-len(out) % 16)
        return bytes(out)


def flat_tim2(template, rgba, size=32):
    """A size×size 8-bit TIM2 whose 256-colour CLUT is all `rgba`, built from a vanilla 8-bit picture's headers
    (file header + picture header copied; sizes, w/h and GsTex0's TBW/TW/TH patched)."""
    info = im.tim2_info(template, 0)
    if info["bpp"] != 5 or info["hdr"] != 0x30:
        raise SystemExit("flat_tim2: template is not an 8-bit TIM2 with a 0x30 picture header")
    pic = 0x10
    hdr = bytearray(template[:pic + 0x30])
    img_sz, clut_sz = size * size, 256 * 4
    struct.pack_into("<3I", hdr, pic, 0x30 + img_sz + clut_sz, clut_sz, img_sz)
    struct.pack_into("<2H", hdr, pic + 0x14, size, size)
    # GsTex0 / GsTex1 / GsRegs / GsTexClut stay ZERO like every vanilla picture: the engine derives the GS
    # register values itself at load (a hand-set buffer width here made the GS sample the wrong VRAM columns —
    # a shimmering cat).
    hdr[pic + 0x18:pic + 0x30] = bytes(0x18)
    return bytes(hdr) + bytes(img_sz) + bytes(rgba) * 256


# ───────────────────────────────────────────── the graft ────────────────────────────────────────────────
def glow_tim2(lightling, core=GLOW_CORE, outer=GLOW_OUTER, cross=GLOW_CROSS, size=None):
    """The torch glow disc (a 64x64 RGBA32 TIM2, no CLUT) re-coloured as a radial gradient: every pixel keeps its alpha
    and its share of the disc's peak luminance (the soft falloff), and its hue runs from `core` at the centre to `outer`
    at the disc's visible edge — the Gallery's purple becomes our cyan-cored blue."""
    ts, cs, isz, hs, cc, pf, mm, ct, it, w, h = struct.unpack_from("<IIIHHBBBBHH", lightling, 0x10)
    if it != 3 or cs != 0:
        raise SystemExit(f"glow source is not a 32-bit TIM2 (type {it}, clut {cs})")
    px = bytearray(lightling[0x10 + hs:0x10 + hs + isz])
    lum = lambda r, g, b: 0.30 * r + 0.59 * g + 0.11 * b
    lums = [lum(px[k], px[k + 1], px[k + 2]) for k in range(0, len(px), 4)]
    peak = max(lums) or 1.0
    cx, cy = (w - 1) / 2.0, (h - 1) / 2.0
    lit = [i for i, l in enumerate(lums) if l > peak * 0.02]                     # the disc's visible extent
    edge = max((((i % w) - cx) ** 2 + ((i // w) - cy) ** 2) ** 0.5 for i in lit) or 1.0
    for i in range(w * h):
        k = i * 4
        l = lums[i] / peak
        t = min(1.0, ((((i % w) - cx) ** 2 + ((i // w) - cy) ** 2) ** 0.5) / edge)   # 0 at the centre, 1 at the edge
        t = t ** (math.log(0.5) / math.log(cross))                                      # biased: halfway at radius `cross` (user: more blue, sooner)
        for c in range(3):
            px[k + c] = min(255, int((core[c] * (1.0 - t) + outer[c] * t) * l))
    if size and size < w:
        # Box-filter down (64x64 -> 32x32 = 4 KB instead of 16). The disc is a soft blob drawn small, so resolution is
        # not what sells it — but four more 64x64 discs cost 64 KB of the character heap and 256 VRAM blocks in Xiao's
        # group. These discs carry no CLUT, so the picture is header + pixels and nothing follows.
        n = w // size
        small = bytearray(size * size * 4)
        for y in range(size):
            for x in range(size):
                for c in range(4):
                    acc = sum(px[(((y * n + dy) * w) + (x * n + dx)) * 4 + c] for dy in range(n) for dx in range(n))
                    small[(y * size + x) * 4 + c] = acc // (n * n)
        hdr = bytearray(lightling[:0x10 + hs])
        struct.pack_into("<3I", hdr, 0x10, hs + len(small) + cs, cs, len(small))   # total, clut, image
        struct.pack_into("<2H", hdr, 0x10 + 0x14, size, size)
        return bytes(hdr) + bytes(small)
    return lightling[:0x10 + hs] + bytes(px) + lightling[0x10 + hs + isz:]


def _glow_levels(lightling):
    """The source disc's luminance per texel, and its sorted distinct levels. Luminance is what the index encodes: it
    falls monotonically from the disc's centre (measured 118 at the middle to 0 at the rim), so it stands in for radius
    and ONE baked index map serves every colour — the pixels carry position in the gradient, the palette carries hue."""
    hs = struct.unpack_from("<H", lightling, 0x1C)[0]
    isz = struct.unpack_from("<I", lightling, 0x18)[0]
    px = lightling[0x10 + hs:0x10 + hs + isz]
    key = [(px[i] * 299 + px[i + 1] * 587 + px[i + 2] * 114) // 1000 for i in range(0, len(px), 4)]
    return key, sorted(set(key))


def glow_indices(lightling):
    """One byte per texel: the pixel's luminance RANK, mapped onto a permutation-safe CLUT index (see CLUT_FIXED)."""
    key, levels = _glow_levels(lightling)
    if len(levels) > len(CLUT_FIXED):
        raise SystemExit(f"glow disc needs {len(levels)} palette entries; only {len(CLUT_FIXED)} are permutation-safe")
    slot = {v: CLUT_FIXED[i] for i, v in enumerate(levels)}
    return bytes(slot[k] for k in key), levels


def glow_palette(lightling, core, outer):
    """The 128 permutation-safe CLUT words for ONE colour, in ascending index order — exactly the 512 B the cave copies.
    Each word is the mean of what glow_tim2 would have produced for the texels at that luminance, so the disc matches the
    32-bit original to a measured mean of 0.28/255 (max 8) instead of being re-derived by some other formula."""
    key, levels = _glow_levels(lightling)
    hs = struct.unpack_from("<H", lightling, 0x1C)[0]
    isz = struct.unpack_from("<I", lightling, 0x18)[0]
    px = glow_tim2(lightling, core=core, outer=outer)[0x10 + hs:0x10 + hs + isz]
    acc = {}
    for i, k in enumerate(key):
        a = acc.setdefault(k, [0, 0, 0, 0, 0])
        for c in range(4):
            a[c] += px[i * 4 + c]
        a[4] += 1
    out = bytearray(len(CLUT_FIXED) * 4)
    for r, v in enumerate(levels):
        a = acc[v]
        for c in range(4):
            out[r * 4 + c] = a[c] // a[4]
    return bytes(out)


def glow_palettes(lightling):
    """All six element palettes back to back — the blob ElfPatches.PatchCatGlowPalettes writes into the data cave.
    ⚠ The cave decides "already painted" from ONE word, and the two offsets it reads are BAKED INTO cat_glow_palette.s.
    Word 0 cannot serve: it is the disc's transparent rim, identical in all six ramps. The stub reads the brightest level
    instead — table slot 114, CLUT index 226 — so both of the facts that make that valid are asserted here."""
    _, levels = _glow_levels(lightling)
    last = len(levels) - 1
    if last != 114 or CLUT_FIXED[last] != 226:
        raise SystemExit(f"the glow disc now has {len(levels)} levels (brightest at CLUT index {CLUT_FIXED[last]}) — update the "
                         f"state-check offsets in tools/stubs/cat_glow_palette.s to table 0x{last * 4:X} / CLUT 0x{CLUT_FIXED[last] * 4:X}")
    tabs = [glow_palette(lightling, core, outer) for core, outer in GLOW_ROWS]
    if len({t[last * 4:last * 4 + 4] for t in tabs}) != len(tabs):
        raise SystemExit("two glow ramps end on the same brightest colour — the cave tells 'already painted' from that one "
                         "word, so it could not distinguish those looks and would stop repainting between them; tune a core")
    return b"".join(tabs)


def glow_t8_tim2(template, lightling, core, outer):
    """The per-element glow disc as an 8-bit TIM2 (64x64 indices + a 256-entry CLUT) built off a vanilla 8-bit picture's
    headers, the way flat_tim2 builds the cape. 5,184 B holds EVERY colour, where a 32-bit disc costs 16,448 B each — four
    of those is what froze the game. The CLUT baked here is only the resting look; the cave repaints it per element."""
    info = im.tim2_info(template, 0)
    if info["bpp"] != 5 or info["hdr"] != 0x30:
        raise SystemExit("glow_t8_tim2: template is not an 8-bit TIM2 with a 0x30 picture header")
    w, h = struct.unpack_from("<2H", lightling, 0x24)
    idx, _ = glow_indices(lightling)
    if (w, h) != (64, 64) or len(idx) != w * h:
        raise SystemExit(f"glow_t8_tim2: source disc is {w}x{h} — DrawFire's texel rect is hardcoded to 64x64")
    pal = bytearray(256 * 4)
    tab = glow_palette(lightling, core, outer)
    for r, e in enumerate(CLUT_FIXED):
        pal[e * 4:e * 4 + 4] = tab[r * 4:r * 4 + 4]
    pic = 0x10
    hdr = bytearray(template[:pic + 0x30])
    struct.pack_into("<3I", hdr, pic, 0x30 + len(idx) + len(pal), len(pal), len(idx))
    struct.pack_into("<2H", hdr, pic + 0x14, w, h)
    hdr[pic + 0x18:pic + 0x30] = bytes(0x18)   # GS regs stay zero: the engine derives them (see flat_tim2)
    return bytes(hdr) + bytes(idx) + bytes(pal)


def cat_name(orig, index):
    return CAT_ROOT_NAME if index == 0 else NODE_PREFIX + orig


def graft_mds(bp, spl):
    """Append EVERY node of the cat rig (spl) to the host .mds (bp). Returns (payload, nb, K)."""
    bnodes, snodes = _mds_nodes(bp), _mds_nodes(spl)
    nb, K = len(bnodes), len(snodes)
    bnames = {b[0] for b in bnodes}
    if CAT_ROOT_NAME in bnames:
        raise SystemExit("cat rig already grafted into this pack (patched ISO used as the base?)")
    clash = [cat_name(nm, k) for k, (nm, _, _) in enumerate(snodes) if cat_name(nm, k) in bnames]
    if clash:
        raise SystemExit(f"node name clash between host and cat rig: {clash}")
    if struct.unpack_from("<I", bp, 0x14)[0] != 0x70 or struct.unpack_from("<I", spl, 0x14)[0] != 0x70:
        raise SystemExit("unexpected .mds record stride")
    mesh0 = 0x18 + nb * 0x70 - 8
    firsts = [mo for _, mo, _ in bnodes if mo]
    if min(firsts) != mesh0 or bp[mesh0:mesh0 + 4] != b"MDT\x00":
        raise SystemExit("host .mds table/mesh overlap layout not as expected")
    delta = K * 0x70
    table = bytearray(bp[:mesh0]) + struct.pack("<II", nb, 0x70)
    struct.pack_into("<I", table, 0x08, nb + K)
    for i in range(nb):
        off = struct.unpack_from("<I", table, 0x18 + i * 0x70 + 0x20)[0]
        if off:
            struct.pack_into("<I", table, 0x18 + i * 0x70 + 0x20, off + delta)
    mblock = bp[mesh0:]
    p_start = 0x18 + (nb + K) * 0x70 - 8 + len(mblock)
    recs, chunks = bytearray(), bytearray()
    for k, (nm, smo, spar) in enumerate(snodes):
        rec = bytearray(spl[0x18 + k * 0x70:0x18 + k * 0x70 + 0x68])
        rec[:0x20] = cat_name(nm, k).encode("latin1").ljust(0x20, b"\0")
        if spar < 0:
            if k != 0:
                raise SystemExit("cat rig has more than one root")
            struct.pack_into("<i", rec, 0x24, CAT_PARENT)        # -1 = unparented (see CAT_PARENT)
            m = list(struct.unpack_from("<16f", rec, 0x28))
            for r in range(3):
                for c in range(3):
                    m[r * 4 + c] *= HIDE_SCALE
            struct.pack_into("<16f", rec, 0x28, *m)
        else:
            struct.pack_into("<i", rec, 0x24, nb + spar)
        if smo:
            if spl[smo:smo + 4] != b"MDT\x00":
                raise SystemExit(f"cat node {nm} mesh @0x{smo:X} is not an MDT chunk")
            csz = struct.unpack_from("<I", spl, smo + 8)[0]
            struct.pack_into("<I", rec, 0x20, p_start + len(chunks))
            chunks += spl[smo:smo + csz]
            chunks += b"\x00" * (-len(chunks) % 16)
        recs += rec + struct.pack("<II", nb + k + 1, 0x70)
    recs = recs[:-8]
    return bytes(table) + bytes(recs) + mblock + bytes(chunks), nb, K


def graft_extra_nodes(payload, extra):
    """Append `extra` = [(name, parent index (absolute in the payload's table), local16, mdt bytes or None), …] to an .mds
    payload: the records go after the last node, every existing mesh offset shifts by the inserted records, and the new
    MDTs land after the existing mesh block (16-aligned). Same layout rules as graft_mds."""
    n_old = struct.unpack_from("<I", payload, 8)[0]
    stride = struct.unpack_from("<I", payload, 0x14)[0]
    if stride != 0x70:
        raise SystemExit("mds stride")
    mesh0 = 0x18 + n_old * 0x70 - 8
    delta = len(extra) * 0x70
    head = bytearray(payload[:mesh0]) + struct.pack("<II", n_old, 0x70)   # the prefix the first MDT header had absorbed
    struct.pack_into("<I", head, 8, n_old + len(extra))
    for i in range(n_old):
        mo = struct.unpack_from("<I", head, 0x18 + i * 0x70 + 0x20)[0]
        if mo:
            struct.pack_into("<I", head, 0x18 + i * 0x70 + 0x20, mo + delta)
    mblock = bytes(payload[mesh0:])
    p_start = 0x18 + (n_old + len(extra)) * 0x70 - 8 + len(mblock)
    chunks = b""
    recs = bytearray()
    for k, (name, par, local16, mdt) in enumerate(extra):
        nm = name.encode("ascii")
        if len(nm) > 0x1F:
            raise SystemExit(f"node name too long: {name}")
        rec = bytearray(0x68)
        rec[:len(nm)] = nm
        struct.pack_into("<I", rec, 0x20, p_start + len(chunks) if mdt else 0)
        struct.pack_into("<i", rec, 0x24, par)
        struct.pack_into("<16f", rec, 0x28, *local16)
        if mdt:
            chunks += mdt + b"\0" * ((-len(mdt)) % 16)
        recs += rec + struct.pack("<II", n_old + k + 1, 0x70)
    out = bytes(head) + bytes(recs)
    out = out[:-8] + mblock + chunks                                       # the last record's tail merges into the first MDT again
    return out


def trim_tracks(mot, windows):
    """Keep only the keyframes inside the clip windows (+ the bracketing key on each side). Bone ids are
    left RELATIVE to the cat root — the channel only ever plays on the copy, whose root is the cat root."""
    for t in mot.tracks:
        keys = sorted(t.keyframes, key=lambda k: k.frame)
        keep = set()
        for lo, hi in windows:
            before = [i for i, k in enumerate(keys) if k.frame < lo]
            after = [i for i, k in enumerate(keys) if k.frame > hi]
            if before: keep.add(before[-1])
            if after: keep.add(after[0])
            keep.update(i for i, k in enumerate(keys) if lo <= k.frame <= hi)
        t.keyframes = [keys[i] for i in sorted(keep)] or keys[:1]
    return mot


def build_cfg(text, nl, extra_dbuff=()):
    lines = text.split(nl)
    out, alloc_done, chan_done = [], False, False
    last_alloc = max((i for i, ln in enumerate(lines) if ln.strip().startswith("ALLOC_DBUFF")), default=-1)
    if last_alloc < 0:
        raise SystemExit("host cfg has no ALLOC_DBUFF line")
    if not any(ln.strip().startswith("MOTION_END") for ln in lines):
        raise SystemExit("host cfg has no MOTION_END")
    block = ['MOTION 1, "cat.mot", "cat.bbp", "cat.wgt"', 'SHADOW_MOTION "", "", ""', f"KEY_START {KEY_START}"]
    for i, (s, e, sp, cm) in enumerate(CAT_KEYS):
        block.append(f"\tKEY\t{s},\t{e},\t{sp},\t\t//{KEY_START + i} {cm}")
    block += ["MOTION_END", VERSION_MARK, ""]
    # The MOTION 1 block goes LAST: CommandFOOT / CommandEVENT (0x13AC50/0x13AC90) bind to `now_motion_data`,
    # the channel of the most recent MOTION line — the host's FOOT/EVENT lines must stay on channel 0.
    for i, ln in enumerate(lines):
        out.append(ln)
        if i == last_alloc:
            out.append(f'ALLOC_DBUFF "{CAT_SKIN_NAME}"')
            for nm in extra_dbuff:                                        # the wing meshes: skinned → double-buffered too
                out.append(f'ALLOC_DBUFF "{nm}"')
    while out and out[-1].strip() == "":
        out.pop()
    out += [""] + block
    return nl.join(out)


def assemble(base_bytes, cat_bytes, float_bytes, glow_bytes, dran_bytes=None, wings=True, log=print):
    """The cat bake. With `wings` (and Dran's pack) the viewer's wing graft is baked on top: 8 wing bones + 2 wing meshes,
    their .wgt/.mot/.bbp data, a flat white wing texture and the white/gold glow discs."""
    if wings and dran_bytes is None:
        raise SystemExit("wings need Dran's pack (DRAN_CHR)")
    base, cat, flt = mc.Pack.parse(base_bytes), mc.Pack.parse(cat_bytes), mc.Pack.parse(float_bytes)
    glow_pack = mc.Pack.parse(glow_bytes)
    glow_img = glow_pack.find("fire.img")
    if glow_img is None:
        raise SystemExit("glow source pack lacks fire.img")
    for n in ("e04c04cat.mds", "e04c04cat.mot"):
        if flt.find(n) is None:
            raise SystemExit(f"float-up pack lacks {n}")
    for n in (HOST_CFG, HOST_MDS, HOST_BBP, HOST_IMG):
        if base.find(n) is None:
            raise SystemExit(f"host pack lacks {n}")
    for n in ("c04cat.mds", "c04cat.bbp", "c04cat.img", "c04cat.mot", "c04cat.wgt"):
        if cat.find(n) is None:
            raise SystemExit(f"cat pack lacks {n}")
    rep = {}
    new_mds, nb, K = graft_mds(base.find(HOST_MDS).payload, cat.find("c04cat.mds").payload)
    base.replace_payload(HOST_MDS, new_mds)
    rep["nodes"] = (nb, K)
    bb, cb = base.find(HOST_BBP).payload, cat.find("c04cat.bbp").payload
    if len(bb) != nb * 64 or len(cb) != K * 64:
        raise SystemExit(".bbp is not count*64 bytes")
    base.replace_payload(HOST_BBP, bb + cb)
    himg, cimg = Bank(base.find(HOST_IMG).payload), Bank(cat.find("c04cat.img").payload)
    items = [(n, himg.block(n)) for n, _ in himg.entries]
    if FLAT_TEXTURES:
        template = cimg.block("c04cat01")
        items += [(n, flat_tim2(template, FLAT_RGBA)) for n, _ in cimg.entries]
    else:
        items += [(n, cimg.block(n)) for n, _ in cimg.entries]
    if len({n for n, _ in items}) != len(items):
        raise SystemExit("texture entry name clash")
    base.replace_payload(HOST_IMG, Bank.build(himg.magic, items))
    rep["textures"] = [n for n, _ in items]
    mot = trim_tracks(mc.Mot.from_pack(cat, "c04cat.mot"), MOT_WINDOWS)
    # The vertical leap: e04c04cat's float/hop-up window grafted by joint NAME (same rig, but never trust the order)
    # into frames FLOAT_DST — exactly what the town ladder jump plays between the ready crouch and the fall.
    fmot = mc.Mot.from_pack(flt, "e04c04cat.mot")
    frep = mc.splice_motion_by_joint(mot, fmot, mc.read_mds_frames(flt.find("e04c04cat.mds").payload),
                                     mc.read_mds_frames(cat.find("c04cat.mds").payload), *FLOAT_SRC, *FLOAT_DST)
    if not frep["written"]:
        raise SystemExit("float-up graft wrote no tracks")
    wgt = mc.Mot.from_pack(cat, "c04cat.wgt")
    mot_rec, wgt_rec = _new_record("cat.mot", mot.build_payload()), _new_record("cat.wgt", wgt.build_payload())
    bbp_rec = _new_record("cat.bbp", cb)
    rep["mot_bytes"], rep["wgt_bytes"] = mot_rec.size, wgt_rec.size
    rep["mot_keys"] = sum(len(t.keyframes) for t in mot.tracks)
    cfg = base.find(HOST_CFG)
    text = cfg.payload.decode("shift_jis", "replace")
    nl = "\r\n" if "\r\n" in text else "\n"
    base.replace_payload(HOST_CFG, build_cfg(text, nl).encode("shift_jis", "replace"))
    base.records += [mot_rec, wgt_rec, bbp_rec]
    out = base.rebuild()
    rep["size"] = (len(base_bytes), len(out))
    verify(base_bytes, out, cat_bytes)
    if not wings:
        return out, rep
    # ── the wings, baked from the viewer's graft on the wingless pack just built ──
    import wing_bake
    wd = wing_bake.build(lambda name: dran_bytes if name == DRAN_CHR else None, out, rep, cat_bytes, log=log)
    extra = [(nm, nb + par, local16, mdt) for nm, par, local16, mdt in wd["nodes"]]   # parents → absolute host indices
    base.replace_payload(HOST_MDS, graft_extra_nodes(base.find(HOST_MDS).payload, extra))
    base.replace_payload(HOST_BBP, base.find(HOST_BBP).payload + wd["bbp"])
    base.replace_payload("cat.bbp", base.find("cat.bbp").payload + wd["bbp"])
    bank = Bank(base.find(HOST_IMG).payload)
    items = [(n, bank.block(n)) for n, _ in bank.entries]
    items.append((wd["texture"], flat_tim2(cimg.block("c04cat01"), WING_RGBA)))
    light = Bank(glow_img.payload).block("lightling")
    # The element glow: one 8-bit disc for all six colours. Its resting CLUT is "None", so it looks right even if the
    # palette cave never runs. ⚠ add it to DivineBeastCat.CatTextureNames too, or it keeps pages in her block.
    items.append((GLOW_T8_NAME, glow_t8_tim2(cimg.block("c04cat01"), light, *GLOW_ELEMENTS[5])))
    if len({n for n, _ in items}) != len(items):
        raise SystemExit("texture entry name clash (wings)")
    base.replace_payload(HOST_IMG, Bank.build(bank.magic, items))
    rep["textures"] = [n for n, _ in items]
    wgt2 = mc.Mot.from_record(base.find("cat.wgt")); wgt2.tracks += wd["wgt_tracks"]
    mot2 = mc.Mot.from_record(base.find("cat.mot")); mot2.tracks += wd["mot_tracks"]
    base.replace_payload("cat.wgt", wgt2.build_payload()); base.replace_payload("cat.mot", mot2.build_payload())
    rep["mot_bytes"], rep["wgt_bytes"] = base.find("cat.mot").size, base.find("cat.wgt").size
    rep["mot_keys"] = sum(len(t.keyframes) for t in mot2.tracks)
    text2 = base.find(HOST_CFG).payload.decode("shift_jis", "replace")
    anchor = f'ALLOC_DBUFF "{CAT_SKIN_NAME}"'
    text2 = text2.replace(anchor, anchor + nl + nl.join(f'ALLOC_DBUFF "{nm}"' for nm in wd["alloc_dbuff"]), 1)
    # ── the Super Steve cape: the engine's own cloth. A FRAME node reachable from HER root (parent 0, its bind 3×3 at HIDE_SCALE
    #    so the cloth she builds from it collapses to a point), its MDT = the rest lattice in the cat's anchor bone's space, a
    #    CLOTH line + the .clo record, ALLOC_MDT for the node (every shipped cloth has it), a flat yellow texture. The runtime
    #    clones her CCloth onto the cat copy and re-anchors it (DivineBeastCat.SpawnCape). ──
    cape = wd["cape"]
    base.replace_payload(HOST_MDS, graft_extra_nodes(base.find(HOST_MDS).payload, [(cape["name"], cape["parent_abs"], cape["local16"], cape["mdt"])]))
    crow = struct.pack("<16f", *cape["local16"])
    base.replace_payload(HOST_BBP, base.find(HOST_BBP).payload + crow)
    base.replace_payload("cat.bbp", base.find("cat.bbp").payload + crow)
    bank2 = Bank(base.find(HOST_IMG).payload)
    items2 = [(n, bank2.block(n)) for n, _ in bank2.entries] + [(cape["texture"], flat_tim2(cimg.block("c04cat01"), CAPE_RGBA))]
    if len({n for n, _ in items2}) != len(items2):
        raise SystemExit("texture entry name clash (cape)")
    base.replace_payload(HOST_IMG, Bank.build(bank2.magic, items2))
    rep["textures"] = [n for n, _ in items2]
    base.records.append(_new_record(CAPE_CLO_NAME, cape["clo"]))
    lines2 = text2.split(nl)
    first_dbuff = next(i for i, ln in enumerate(lines2) if ln.strip().startswith("ALLOC_DBUFF"))
    lines2.insert(first_dbuff, f'ALLOC_MDT "{cape["name"]}"')
    shadow = next(i for i, ln in enumerate(lines2) if ln.strip().startswith("SHADOW_MODEL"))
    lines2.insert(shadow + 1, f'CLOTH "{CAPE_CLO_NAME}"')
    text2 = nl.join(lines2)
    base.replace_payload(HOST_CFG, text2.encode("shift_jis", "replace"))
    out = base.rebuild()
    rep["nodes"] = (nb, K + len(extra)); rep["size"] = (len(base_bytes), len(out)); rep["wings"] = wd["stats"]
    rep["cape_node"] = nb + K + len(extra)                                       # after the cat run: the runtime's node scan stops at it
    verify(base_bytes, out, cat_bytes, wings=wd)
    return out, rep


def verify(base_bytes, new_bytes, cat_bytes, wings=None):
    old, new, cat = mc.Pack.parse(base_bytes), mc.Pack.parse(new_bytes), mc.Pack.parse(cat_bytes)
    assert mc.Pack.parse(new.rebuild()).rebuild() == new_bytes, "pack round-trip"
    on, nn = _mds_nodes(old.find(HOST_MDS).payload), _mds_nodes(new.find(HOST_MDS).payload)
    cn = _mds_nodes(cat.find("c04cat.mds").payload)
    nb, K = len(on), len(cn)
    X = wings["nodes"] if wings else []
    C = 1 if wings and wings.get("cape") else 0                                      # the cape's FRAME node, last
    npl = new.find(HOST_MDS).payload
    assert len(nn) == nb + K + len(X) + C and struct.unpack_from("<I", npl, 8)[0] == nb + K + len(X) + C, "node count"
    if C:
        cp = wings["cape"]; ci = nb + K + len(X)
        assert nn[ci][0] == cp["name"] and nn[ci][2] == 0 and nn[ci][1], "cape node"
        cm = parse_mdt(npl, nn[ci][1]) if False else None
        assert struct.unpack_from("<I", npl, nn[ci][1] + 12)[0] == cp["rows"] * cp["cols"], "cape lattice vertex count"
        assert new.find(CAPE_CLO_NAME) is not None and new.find(CAPE_CLO_NAME).payload == cp["clo"], "cape .clo record"
        assert cp["clo"].count(b"BOUND") == len(cp["bounds"]), "cape BOUND count"        # DivineBeastCat.CapeBoundBones must match, in order
    for k, (nm, par, local16, mdt) in enumerate(X):                       # the wing bones and mesh nodes
        assert nn[nb + K + k][0] == nm and nn[nb + K + k][2] == nb + par and bool(nn[nb + K + k][1]) == bool(mdt), f"wing node {nm}"
        assert struct.unpack_from("<16f", npl, 0x18 + (nb + K + k) * 0x70 + 0x28) == tuple(struct.unpack("<16f", struct.pack("<16f", *local16))), f"wing node {nm} bind"
    assert [n[0] for n in nn[:nb]] == [n[0] for n in on], "host nodes renamed"
    assert nn[nb][0] == CAT_ROOT_NAME and nn[nb][2] == CAT_PARENT, "cat root parent"
    for k in range(1, K):
        assert nn[nb + k][0] == NODE_PREFIX + cn[k][0] and nn[nb + k][2] == nb + cn[k][2], f"cat node {k}"
    meshes = 0
    for nm, mo, _ in nn:
        if mo:
            assert npl[mo:mo + 4] == b"MDT\x00", f"{nm} mesh magic"
            meshes += 1
    assert meshes == sum(1 for n in on if n[1]) + sum(1 for n in cn if n[1]) + sum(1 for x in X if x[3]) + C, "mesh count"
    opl = old.find(HOST_MDS).payload
    for i, (nm, mo, par) in enumerate(on):
        o = opl[0x18 + i * 0x70:0x18 + i * 0x70 + 0x68]
        n = bytearray(npl[0x18 + i * 0x70:0x18 + i * 0x70 + 0x68])
        if mo:
            struct.pack_into("<I", n, 0x20, mo)
        assert bytes(n) == o, f"host node {i} changed"
    assert len(new.find(HOST_BBP).payload) == (nb + K + len(X) + C) * 64, "bbp rows"
    assert len(new.find("cat.bbp").payload) == (K + len(X) + C) * 64, "cat.bbp rows"
    for name in ("cat.mot", "cat.wgt"):
        m = mc.Mot.from_record(new.find(name))
        assert all(0 <= t.w0 < K + len(X) for t in m.tracks), f"{name} track ids must be cat-relative"
    if wings:
        wg = mc.Mot.from_record(new.find("cat.wgt"))
        for nm, par, local16, mdt in X:
            if not mdt: continue
            M = K + [x[0] for x in X].index(nm)
            run = [t for t in wg.tracks if t.w0 == M]
            assert run and run[0].w1 == par and not run[0].keyframes, f"{nm} wgt reset entry"
            bones = [t.w1 for t in run[1:]]
            assert bones == sorted(bones) and all(0 <= b < K + len(X) for b in bones), f"{nm} wgt bone order"
            cover = {}
            for t in run[1:]:
                for kf in t.keyframes: cover[kf.frame] = cover.get(kf.frame, 0) + kf.value[0]
            nv = struct.unpack_from("<I", npl, nn[nb + M][1] + 12)[0]
            assert set(cover) == set(range(nv)) and all(abs(v - 100) < 0.01 for v in cover.values()), f"{nm} wgt coverage ({len(cover)}/{nv})"
    text = new.find(HOST_CFG).payload.decode("shift_jis", "replace")
    assert f'ALLOC_DBUFF "{CAT_SKIN_NAME}"' in text and 'MOTION 1, "cat.mot"' in text and f"KEY_START {KEY_START}" in text, "cfg"
    if wings:
        assert all(f'ALLOC_DBUFF "{nm}"' in text for nm in wings["alloc_dbuff"]), "wing / mask ALLOC_DBUFF"
        assert f'ALLOC_DBUFF "{wings["mask"]["name"]}"' in text, "mask ALLOC_DBUFF"
    if C:
        assert f'ALLOC_MDT "{wings["cape"]["name"]}"' in text and f'CLOTH "{CAPE_CLO_NAME}"' in text, "cape cfg lines"
        assert text.index('CLOTH "') < text.index('MOTION 0'), "CLOTH must precede the motion blocks"
    assert text.count("MOTION_END") == 2, "cfg blocks"
    last_foot = max((text.rfind(k) for k in ("FOOT", "EVENT")), default=-1)
    assert last_foot < text.index('MOTION 1, "cat.mot"'), "MOTION 1 must follow every FOOT/EVENT line (they bind to the current channel)"
    for other in ("hand_up.cfg",):                      # untouched siblings
        if old.find(other) is not None:
            assert new.find(other).payload == old.find(other).payload, f"{other} changed"
    bank = Bank(new.find(HOST_IMG).payload)
    names = {n for n, _ in bank.entries}
    assert {"c04cat01", "c04cat02", "c04cat03", "c04cat04", "c04cat05"} <= names, "cat textures"
    if wings:
        assert {wings["texture"], GLOW_T8_NAME} <= names, "wing / glow textures"
    if C:
        assert wings["cape"]["texture"] in names, "cape texture"
        assert wings["mask"]["texture"] == wings["cape"]["texture"], "the mask shares the cape's texture — a private one does not resolve"
        inf = im.tim2_info(bank.block(wings["texture"]), 0)
        assert inf["w"] == 32 and inf["h"] == 32, "flat wing texture"
    assert {n for n, _ in Bank(old.find(HOST_IMG).payload).entries} <= names, "host textures kept"
    for n, _ in bank.entries:
        assert bank.block(n)[:4] == b"TIM2", f"texture {n} block"
        if FLAT_TEXTURES and n.startswith("c04cat"):
            inf = im.tim2_info(bank.block(n), 0)
            assert inf["w"] == 32 and inf["h"] == 32 and inf["image"] == 1024 and inf["clut"] == 1024, f"flat {n}"
    assert VERSION_MARK in text, "version mark"


def has_cat(chr_bytes, mds_name=HOST_MDS):
    try:
        return any(n[0] == CAT_ROOT_NAME for n in _mds_nodes(mc.Pack.parse(chr_bytes).find(mds_name).payload))
    except Exception:
        return False


def is_current_bake(chr_bytes):
    """This tool version's bake (older cat bakes of the host get reverted and redone)."""
    try:
        pk = mc.Pack.parse(chr_bytes)
        return has_cat(chr_bytes) and VERSION_MARK in pk.find(HOST_CFG).payload.decode("shift_jis", "replace")
    except Exception:
        return False


def _from_dc_dir(dc_dir):
    _, base = mc.load_pack(HOST_CHR, dc_dir)
    _, cat = mc.load_pack(CAT_CHR, dc_dir)
    _, flt = mc.load_pack(FLOAT_CHR, dc_dir)
    _, glow = mc.load_pack(GLOW_SRC, dc_dir)
    _, dran = mc.load_pack(DRAN_CHR, dc_dir)
    return assemble(base.rebuild(), cat.rebuild(), flt.rebuild(), glow.rebuild(), dran.rebuild())


def main():
    a = sys.argv[1:]
    dc = a[a.index("--dc-dir") + 1] if "--dc-dir" in a else os.environ.get("DC1_DATA_DIR")
    if not dc:
        raise SystemExit("--dc-dir <extracted disc dir> [--out <file>] [--test] [--palettes <file>]")
    if "--palettes" in a:
        # The ramps come from GLOW_ELEMENTS and the source disc ALONE, so this needs no pack bake — it is the fast path
        # the mod build calls on every compile (tools/build_resources.py).
        dest = a[a.index("--palettes") + 1]
        _, gl = mc.load_pack(GLOW_SRC, dc)
        blob = glow_palettes(Bank(gl.find("fire.img").payload).block("lightling"))
        open(dest, "wb").write(blob)
        print(f"wrote {dest} ({len(blob)} B = {len(GLOW_ELEMENTS)} x {len(CLUT_FIXED) * 4} B)")
        if "--out" not in a and "--test" not in a:
            return
    out, rep = _from_dc_dir(dc)
    print(rep)
    if "--out" in a:
        open(a[a.index("--out") + 1], "wb").write(out); print("wrote", a[a.index("--out") + 1])
    if "--test" in a:
        print("self-check ok")


if __name__ == "__main__":
    main()
