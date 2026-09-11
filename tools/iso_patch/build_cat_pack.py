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
VERSION_MARK  = "//catpack v8 +floatup"
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


def build_cfg(text, nl):
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
    while out and out[-1].strip() == "":
        out.pop()
    out += [""] + block
    return nl.join(out)


def assemble(base_bytes, cat_bytes, float_bytes):
    base, cat, flt = mc.Pack.parse(base_bytes), mc.Pack.parse(cat_bytes), mc.Pack.parse(float_bytes)
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
    return out, rep


def verify(base_bytes, new_bytes, cat_bytes):
    old, new, cat = mc.Pack.parse(base_bytes), mc.Pack.parse(new_bytes), mc.Pack.parse(cat_bytes)
    assert mc.Pack.parse(new.rebuild()).rebuild() == new_bytes, "pack round-trip"
    on, nn = _mds_nodes(old.find(HOST_MDS).payload), _mds_nodes(new.find(HOST_MDS).payload)
    cn = _mds_nodes(cat.find("c04cat.mds").payload)
    nb, K = len(on), len(cn)
    npl = new.find(HOST_MDS).payload
    assert len(nn) == nb + K and struct.unpack_from("<I", npl, 8)[0] == nb + K, "node count"
    assert [n[0] for n in nn[:nb]] == [n[0] for n in on], "host nodes renamed"
    assert nn[nb][0] == CAT_ROOT_NAME and nn[nb][2] == CAT_PARENT, "cat root parent"
    for k in range(1, K):
        assert nn[nb + k][0] == NODE_PREFIX + cn[k][0] and nn[nb + k][2] == nb + cn[k][2], f"cat node {k}"
    meshes = 0
    for nm, mo, _ in nn:
        if mo:
            assert npl[mo:mo + 4] == b"MDT\x00", f"{nm} mesh magic"
            meshes += 1
    assert meshes == sum(1 for n in on if n[1]) + sum(1 for n in cn if n[1]), "mesh count"
    opl = old.find(HOST_MDS).payload
    for i, (nm, mo, par) in enumerate(on):
        o = opl[0x18 + i * 0x70:0x18 + i * 0x70 + 0x68]
        n = bytearray(npl[0x18 + i * 0x70:0x18 + i * 0x70 + 0x68])
        if mo:
            struct.pack_into("<I", n, 0x20, mo)
        assert bytes(n) == o, f"host node {i} changed"
    assert len(new.find(HOST_BBP).payload) == (nb + K) * 64, "bbp rows"
    assert len(new.find("cat.bbp").payload) == K * 64, "cat.bbp rows"
    for name in ("cat.mot", "cat.wgt"):
        m = mc.Mot.from_record(new.find(name))
        assert all(0 <= t.w0 < K for t in m.tracks), f"{name} track ids must be cat-relative"
    text = new.find(HOST_CFG).payload.decode("shift_jis", "replace")
    assert f'ALLOC_DBUFF "{CAT_SKIN_NAME}"' in text and 'MOTION 1, "cat.mot"' in text and f"KEY_START {KEY_START}" in text, "cfg"
    assert text.count("MOTION_END") == 2, "cfg blocks"
    last_foot = max((text.rfind(k) for k in ("FOOT", "EVENT")), default=-1)
    assert last_foot < text.index('MOTION 1, "cat.mot"'), "MOTION 1 must follow every FOOT/EVENT line (they bind to the current channel)"
    for other in ("hand_up.cfg",):                      # untouched siblings
        if old.find(other) is not None:
            assert new.find(other).payload == old.find(other).payload, f"{other} changed"
    bank = Bank(new.find(HOST_IMG).payload)
    names = {n for n, _ in bank.entries}
    assert {"c04cat01", "c04cat02", "c04cat03", "c04cat04", "c04cat05"} <= names, "cat textures"
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
        new_chr, rep = assemble(base, read_src(CAT_CHR), read_src(FLOAT_CHR))
        log(f"Divine Beast Title cat assembled into c04b.chr — {rep['nodes'][1]} cat nodes, {len(rep['textures'])} textures, "
            f"cat.mot {rep['mot_bytes']:,} B ({rep['mot_keys']} keys), {rep['size'][0]:,}->{rep['size'][1]:,} B")
        redirect(HOST_CHR, new_chr)
        log("DONE (Divine Beast Title cat pack)")


def _from_dc_dir(dc_dir):
    _, base = mc.load_pack(HOST_CHR, dc_dir)
    _, cat = mc.load_pack(CAT_CHR, dc_dir)
    _, flt = mc.load_pack(FLOAT_CHR, dc_dir)
    return assemble(base.rebuild(), cat.rebuild(), flt.rebuild())


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
