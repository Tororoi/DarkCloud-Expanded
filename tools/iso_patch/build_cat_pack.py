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
the game (2026-09-10). The character pack is never rebuilt by the menu.

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

Usage:
  python3 tools/iso_patch/build_cat_pack.py --iso "<patched iso>"      # install (called by IsoPatcher)
  python3 tools/iso_patch/build_cat_pack.py --dc-dir "$DC1_DATA_DIR" --out /tmp/c04b_cat.chr   # dev build
  python3 tools/iso_patch/build_cat_pack.py --dc-dir "$DC1_DATA_DIR" --test
"""
import math
import os, sys, struct

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "lib"))
sys.path.insert(0, os.path.join(HERE, "..", "analysis"))
import ps2iso                      # noqa: E402
import mot_codec as mc             # noqa: E402
import img_surgery as im           # noqa: E402

SEC = ps2iso.SECTOR
def align(x, a=SEC): return (x + a - 1) & ~(a - 1)

HOST_CHR  = r"dun\mainchara\c04b.chr"
HOST_CFG, HOST_MDS, HOST_BBP, HOST_IMG = "base.cfg", "c04b.mds", "c04b.bbp", "c04b01.img"
CAT_CHR   = r"gedit\s86\chara\c04cat.chr"
FLOAT_CHR = r"gedit\e01\chara\e04c04cat.chr"   # the town cat's vertical float/hop-up (its #5 clip, frames 160..169)
FLOAT_SRC, FLOAT_DST = (160, 169), (285, 294)  # grafted into cat.mot where s86 has no keys (assemble_town_model.py slot 12)
GLOW_SRC  = r"dun\mpd_pack\d06main_a.mpd"     # the Gallery of Time map pack: its own fire.img holds the PURPLE torch glow disc
GLOW_NAME = "catglow"                           # the cat's glow: that 64x64 RGBA32 disc re-tinted (CFireOmni draws it additively)
GLOW_CORE  = (15, 219, 255)                     # radial gradient: this at the centre …
GLOW_OUTER = (60, 67, 255)                      # … to this at the disc's edge (user 2026-09-12)
GLOW_CROSS = 0.125                              # radius fraction where the mix is halfway (0.5 = linear); smaller = the blue reaches further in (user 2026-09-12)
# Per-weapon glows (user 2026-09-13): the Divine Beast Title keeps the blue disc; the Angel Shooter's cat glows WHITE, the
# Angel Gear's GOLD. Same disc, re-tinted; the runtime names the one to draw (mailbox CatGlowName → the glow cave).
GLOW_VARIANTS = {"catgloww": ((255, 255, 255), (215, 225, 255)),   # white, a cool edge
                 "catglowg": ((255, 238, 180), (255, 176, 40))}    # gold
CAPE_CLO_NAME = "catcape.clo"                   # the cape's cloth definition record (wing_bake.CAPE_CLO)
DRAN_CHR  = r"dun\monstor\c12a.chr"              # the wing donor (tools/lib/cat_wings.py grafts its wings, wing_bake.py bakes them; read from the ISO)
WING_RGBA = (255, 255, 255, 0x80)                # the wings' flat texture: solid white, GS alpha 0x80 = opaque (user 2026-09-13)
CAPE_RGBA = (255, 48, 48, 0x80)                  # the Super Steve cape's flat texture: solid bright red (user 2026-09-14)

NODE_PREFIX   = "cat_"             # every cat bone (her rig already carries `kao`, `skin`, …)
CAT_ROOT_NAME = "catroot"          # what the runtime looks for in her tree
CAT_SKIN_NAME = NODE_PREFIX + "skin"
HIDE_SCALE    = 0.001              # bind 3x3 of the cat root on HER (the copy restores 1.0) — belt and braces
CAT_PARENT    = -1                 # UNPARENTED: LoadMDSFile 0x1262B0 calls SetParent(frame, NULL) for parent < 0, so
                                   # the cat never joins her tree — no draw, no skinning, no DMA while hidden. (The
                                   # parented+hidden variant froze the weapon/party menus, whose draw buffers are
                                   # smaller than the dungeon's.) It still sits in her frame ARRAY for the runtime scan.
FLAT_TEXTURES = False              # real cat fur (user 2026-09-10: blue/glow will be flash effects, not a retexture)
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
REC_TAG = 0x00140E02               # word at record +0x4C on every vanilla record

# The earlier (2026-09-09) bake put the cat into the WEAPON packs, which froze the weapon menu. Their vanilla
# DATA.HD2 records (USA disc), so an ISO patched by that version is put back before the character bake.
HOST_VANILLA = (0x195D5800, 0x218220, 0x32BAB, 0x431)   # vanilla DATA.HD2 record of c04b.chr (USA disc)
WEAPON_REVERT = {
    r"commenu\weapon\c04w09.chr": (0x17275000, 0x90E0, 0x2E4EA, 0x13),
    r"commenu\c04wtes.chr":       (0x168A800, 0x8A950, 0x2D15, 0x116),
    r"dun\mainchara\c04w.pac":    (0x1985B800, 0x641A0, 0x330B7, 0xC9),
}


# ───────────────────────────────────────────── pack helpers ─────────────────────────────────────────────
def _new_record(name, payload, tag=REC_TAG):
    data_off = mc.DATA_OFF_STD
    stride = (data_off + len(payload) + 15) & ~15
    head = bytearray(data_off)
    nb = name.encode("latin1")[:0x3F]
    head[:len(nb)] = nb
    struct.pack_into("<III", head, 0x40, data_off, len(payload), stride)
    struct.pack_into("<I", head, 0x4C, tag)
    raw = bytes(head) + payload + b"\x00" * (stride - data_off - len(payload))
    return mc.Record(name, data_off, len(payload), stride, raw)


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
    # a shimmering cat, 2026-09-10).
    hdr[pic + 0x18:pic + 0x30] = bytes(0x18)
    return bytes(hdr) + bytes(img_sz) + bytes(rgba) * 256


# ───────────────────────────────────────────── the graft ────────────────────────────────────────────────
def glow_tim2(lightling, core=GLOW_CORE, outer=GLOW_OUTER, cross=GLOW_CROSS):
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
    return lightling[:0x10 + hs] + bytes(px) + lightling[0x10 + hs + isz:]


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
    items.append((GLOW_NAME, glow_tim2(Bank(glow_img.payload).block("lightling"))))
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
    for nm, (core, outer) in GLOW_VARIANTS.items():
        items.append((nm, glow_tim2(light, core=core, outer=outer)))
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
        assert all(f'ALLOC_DBUFF "{nm}"' in text for nm in wings["alloc_dbuff"]), "wing ALLOC_DBUFF"
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
        assert {wings["texture"], *GLOW_VARIANTS} <= names, "wing / glow textures"
    if C:
        assert wings["cape"]["texture"] in names, "cape texture"
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


# ───────────────────────────────────────────── ISO install ──────────────────────────────────────────────
def _hd2_slot(hd2_r, i): return hd2_r["ext"] * SEC + 16 + i * 32


def _free_tail(f, dat_size, hd2_r, hed):
    mx = 0
    for i in range(len(hed) // 80):
        f.seek(_hd2_slot(hd2_r, i)); off, size = struct.unpack("<II", f.read(8))
        if 0 < off + size <= dat_size: mx = max(mx, off + size)
    return align(mx)


def run(iso, log=print):
    if not os.path.exists(iso):
        raise SystemExit(f"ISO not found: {iso}")
    with open(iso, "r+b") as f:
        recs = ps2iso.parse_root(f)
        hd2_r, dat_r = recs["DATA.HD2"], recs["DATA.DAT"]
        dat_iso = dat_r["ext"] * SEC; dat_size = dat_r["size"]
        hed = ps2iso.read_file(f, recs["DATA.HED"])
        tail = _free_tail(f, dat_size, hd2_r, hed)

        def slot_of(name):
            i = ps2iso.archive_find(hed, name)
            if i is None: raise SystemExit(f"{name} not in archive")
            return _hd2_slot(hd2_r, i)

        def read_src(name):
            f.seek(slot_of(name)); off, size = struct.unpack("<II", f.read(8)); f.seek(dat_iso + off); return f.read(size)

        def redirect(name, data):
            nonlocal tail
            slot = slot_of(name)
            if tail + len(data) > dat_size: raise SystemExit("out of DATA.DAT tail")
            f.seek(dat_iso + tail); f.write(data)
            sec, cnt = tail >> 11, (len(data) + SEC - 1) // SEC
            f.seek(slot); f.write(struct.pack("<IIII", tail, len(data), sec, cnt))
            f.seek(dat_iso + sec * SEC); assert f.read(len(data)) == data, f"{name} readback"
            log(f"redirected {name}: -> {len(data):,} B @sector {sec:#x}")
            tail = align(tail + len(data))

        # Undo the weapon-pack bake of the earlier tool version, if this ISO carries it.
        for name, vanilla in WEAPON_REVERT.items():
            slot = slot_of(name)
            f.seek(slot); cur = struct.unpack("<IIII", f.read(16))
            if cur == vanilla: continue
            rec_name = "c04w09.chr" if name.endswith(".chr") and "wtes" not in name else None
            f.seek(dat_iso + vanilla[0]); van = f.read(vanilla[1])
            try:
                pk = mc.Pack.parse(van)
                probe = pk.find("c04w09.mds") if rec_name else mc.Pack.parse(pk.find("c04w09.chr").payload).find("c04w09.mds")
                ok = probe is not None and not has_cat(van if rec_name else pk.find("c04w09.chr").payload, "c04w09.mds")
            except Exception:
                ok = False
            if not ok:
                raise SystemExit(f"{name}: record differs from vanilla and the vanilla bytes are not where expected — refusing to revert")
            f.seek(slot); f.write(struct.pack("<IIII", *vanilla))
            log(f"reverted {name} to its vanilla record (earlier weapon-pack bake removed)")

        base = read_src(HOST_CHR)
        if is_current_bake(base):
            log("cat already in dun\\mainchara\\c04b.chr — skipped"); return
        if has_cat(base):                                    # an older bake of this tool: back to vanilla first
            f.seek(dat_iso + HOST_VANILLA[0]); van = f.read(HOST_VANILLA[1])
            if has_cat(van) or mc.Pack.parse(van).find(HOST_MDS) is None:
                raise SystemExit("c04b.chr carries an older cat bake and the vanilla bytes are not where expected — refusing")
            f.seek(slot_of(HOST_CHR)); f.write(struct.pack("<IIII", *HOST_VANILLA))
            log("reverted dun\\mainchara\\c04b.chr to its vanilla record (older cat bake removed)")
            base = van
        new_chr, rep = assemble(base, read_src(CAT_CHR), read_src(FLOAT_CHR), read_src(GLOW_SRC), read_src(DRAN_CHR), log=log)
        log(f"Divine Beast cat (wings) assembled into c04b.chr — {rep['nodes'][1]} cat nodes, {len(rep['textures'])} textures, "
            f"cat.mot {rep['mot_bytes']:,} B ({rep['mot_keys']} keys), {rep['size'][0]:,}->{rep['size'][1]:,} B")
        redirect(HOST_CHR, new_chr)
        log("DONE (Divine Beast Title cat pack)")


def _from_dc_dir(dc_dir):
    _, base = mc.load_pack(HOST_CHR, dc_dir)
    _, cat = mc.load_pack(CAT_CHR, dc_dir)
    _, flt = mc.load_pack(FLOAT_CHR, dc_dir)
    _, glow = mc.load_pack(GLOW_SRC, dc_dir)
    _, dran = mc.load_pack(DRAN_CHR, dc_dir)
    return assemble(base.rebuild(), cat.rebuild(), flt.rebuild(), glow.rebuild(), dran.rebuild())


def main():
    a = sys.argv[1:]
    if "--iso" in a:
        run(a[a.index("--iso") + 1]); return
    dc = a[a.index("--dc-dir") + 1] if "--dc-dir" in a else os.environ.get("DC1_DATA_DIR")
    if not dc:
        raise SystemExit("--iso <iso> | --dc-dir <extracted disc dir> [--out <file>] [--test]")
    out, rep = _from_dc_dir(dc)
    print(rep)
    if "--out" in a:
        open(a[a.index("--out") + 1], "wb").write(out); print("wrote", a[a.index("--out") + 1])
    if "--test" in a:
        print("self-check ok")


if __name__ == "__main__":
    main()
