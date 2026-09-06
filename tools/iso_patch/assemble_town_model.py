#!/usr/bin/env python3
"""Assemble a swapped-in town ally's model: fill the 10 town motion slots (see
docs/town-swap-animation-map.md) by transplanting each chosen clip (same-rig, by joint name) into the
base model's body + shadow `.mot`, then rewrite the base cfg's KEY table to the slots, and redirect the
rebuilt `.chr` into the ISO's free DATA.DAT tail (same mechanism as transplant_battle_run.py).

Town slots played by the engine (`chara\\c01d.chr`): 0 idle · 1 run · 2 walk · 3 push-door · 4 pull-door ·
5 item-get · 6 item-get-loop · 7 damage(skip) · 8 fall · 9 land.

A slot is either KEPT (points at frames the base already has) or GRAFTED (a clip from another model spliced
into a free frame window of the base, body+shadow together). Nothing is bundled — every model is read from
the user's own ISO at patch time. Run AFTER the collision bake so the tail high-water is found past it.

  python3 tools/iso_patch/assemble_town_model.py [--iso "/path/Dark Cloud - Expanded.iso"] [--test]
"""
import os, sys, struct, re
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "lib"))
import ps2iso
import mot_codec as mc

SEC = ps2iso.SECTOR
def align(x, a=SEC): return (x + a - 1) & ~(a - 1)
DEFAULT_ISO = os.path.expanduser("~/ROMs/Patched ISOs/Dark Cloud - Expanded.iso")

# Each character: base .chr + 10 slots. A slot = dict(frames=(start,end), speed, name, src=None|<src_chr>,
# win=(lo,hi)). src=None keeps the base's own frames [start,end]. src set = graft that source .chr's clip
# frames `win` into the base at [start,end] (same length), body+shadow. Multiple slots can share one graft
# (set src only on the first; later slots with same frames just re-KEY).
CHARS = {
    "Xiao": dict(
        base="gedit/e01/chara/c04pcat.chr",
        slots=[
            dict(idx=0, frames=(10, 20),  speed=0.10, name="idle",  src=None),
            dict(idx=1, frames=(120, 136), speed=0.50, name="run",   src="gedit/s86/chara/c04cat.chr",   win=(120, 136)),
            dict(idx=2, frames=(60, 80),  speed=0.30, name="walk",  src=None),
            # door = e04c04cat #5's vertical opening (frames 160-169), pulled back off the door: the cat is
            # longer than Toan, so the door-open teleport (Toan's collision) overshoots the head into the door.
            # root_offset shifts the root node -z = BACK (out of the door). TUNABLE — nudge the z magnitude.
            dict(idx=3, frames=(150, 159), speed=0.20, name="door",  src="gedit/e01/chara/e04c04cat.chr", win=(160, 169), root_offset=(0.0, 0.0, -2.0)),
            dict(idx=4, frames=(150, 159), speed=0.20, name="door2", src=None),   # reuse the grafted door frames
            # sit = s86/c04cat motion 1 (frames 30-40), a complete looping sit — grafted into the base at 30-40
            # and shared by both item slots. The town engine loops the armed KEY window, so 30-40 cycles cleanly.
            dict(idx=5, frames=(30, 40),  speed=0.10, name="sit",      src="gedit/s86/chara/c04cat.chr", win=(30, 40)),
            dict(idx=6, frames=(30, 40),  speed=0.10, name="sit-loop", src=None),   # reuse the grafted sit, full 30-40
            dict(idx=7, frames=(10, 20),  speed=0.10, name="damage(skip)", src=None),
            dict(idx=8, frames=(205, 214), speed=0.50, name="fall(leap)", src="gedit/s86/chara/c04cat.chr", win=(205, 214)),
            dict(idx=9, frames=(215, 227), speed=0.36, name="land",  src="gedit/s86/chara/c04cat.chr",   win=(215, 227)),
        ],
    ),
}


def _cfg_motions(pack):
    """Return (cfg_record, body_mot, body_mds, shadow_mot, shadow_mds) from a .chr's main cfg (the one whose
    payload holds MODEL/MOTION/KEY)."""
    cand = [r for r in pack.records if r.name.lower().endswith('.cfg')]
    cfg = None
    for r in cand:
        if b'KEY_START' in r.payload and b'MOTION' in r.payload:
            cfg = r; break
    if cfg is None and cand:
        cfg = cand[0]
    t = cfg.payload
    def grab(tag):
        m = re.search(tag + rb'[ \t]+"([^"]+)"', t)
        return m.group(1).decode('latin1') if m else None
    body_mot = grab(rb'MOTION[ \t]+0,')
    if not body_mot:  # MOTION 0, "x.mot", ...
        m = re.search(rb'MOTION[ \t]+0,[ \t]*"([^"]+)"', t); body_mot = m.group(1).decode('latin1') if m else None
    body_mds = grab(rb'MODEL')
    sh_mot = None
    m = re.search(rb'SHADOW_MOTION[ \t]+"([^"]+)"', t); sh_mot = m.group(1).decode('latin1') if m else None
    sh_mds = grab(rb'SHADOW_MODEL')
    return cfg, body_mot, body_mds, sh_mot, sh_mds


def _graft(dst_pack, dst_mot, dst_mds, src_pack, src_mot, src_mds, slo, shi, dlo, dhi):
    dst = mc.Mot.from_record(dst_pack.find(dst_mot))
    src = mc.Mot.from_record(src_pack.find(src_mot))
    dframes = mc.read_mds_frames(dst_pack.find(dst_mds).payload)
    sframes = mc.read_mds_frames(src_pack.find(src_mds).payload)
    mc.splice_motion_by_joint(dst, src, sframes, dframes, slo, shi, dlo, dhi)
    dst_pack.replace_payload(dst_mot, dst.rebuild()[dst.data_off:])


def _apply_root_offset(pack, mot_name, dlo, dhi, off):
    """Add (dx,dy,dz) to the root-motion node's translation keyframes in [dlo,dhi] — shifts the whole pose in
    world space (used to pull the door reach back off the door). Root node = the first chan-2 track."""
    m = mc.Mot.from_record(pack.find(mot_name))
    rt = next((t for t in m.tracks if t.w2 == 2), None)
    if rt is None:
        return
    for kf in rt.keyframes:
        if dlo <= kf.frame <= dhi:
            v = list(kf.value)
            v[0] += off[0]; v[1] += off[1]; v[2] += off[2]
            struct.pack_into('<4f', kf.raw, 0x10, *v)
    pack.replace_payload(mot_name, m.rebuild()[m.data_off:])


def _rewrite_keys(cfg_payload, slots):
    """Replace the KEY_START..MOTION_END block's KEY lines with one KEY per slot (slot order = index)."""
    lines = b"".join(
        b"KEY\t%d,\t%d,\t%.2f, //%s\r\n" % (s["frames"][0], s["frames"][1], s["speed"], (s["name"][:12]).encode('latin1'))
        for s in sorted(slots, key=lambda s: s["idx"])
    )
    ks = cfg_payload.find(b'KEY_START')
    nl = cfg_payload.find(b'\r\n', ks) + 2           # after "KEY_START 0"
    me = cfg_payload.find(b'MOTION_END', nl)
    return cfg_payload[:nl] + lines + cfg_payload[me:]


def assemble(base_bytes, read_src, char):
    """read_src(name)->bytes reads any .chr from the ISO. Returns (new_base_chr, report)."""
    base = mc.Pack.parse(base_bytes)
    cfg, bmot, bmds, smot, smds = _cfg_motions(base)
    src_cache = {}
    def src(name):
        if name not in src_cache:
            src_cache[name] = mc.Pack.parse(read_src(name))
        return src_cache[name]
    grafts = 0
    for s in char["slots"]:
        if not s.get("src"):
            continue
        sp = src(s["src"])
        scfg, sbmot, sbmds, ssmot, ssmds = _cfg_motions(sp)
        wlo, whi = s["win"]; dlo, dhi = s["frames"]
        _graft(base, bmot, bmds, sp, sbmot, sbmds, wlo, whi, dlo, dhi)               # body
        if smot and ssmot:                                                            # shadow (keep body+shadow in sync)
            _graft(base, smot, smds, sp, ssmot, ssmds, wlo, whi, dlo, dhi)
        if s.get("root_offset"):                                                      # pull the reach back (door)
            _apply_root_offset(base, bmot, dlo, dhi, s["root_offset"])
            if smot: _apply_root_offset(base, smot, dlo, dhi, s["root_offset"])
        grafts += 1
    base.replace_payload(cfg.name, _rewrite_keys(cfg.payload, char["slots"]))
    new_chr = base.rebuild()
    # guard: re-parse + confirm 10 KEYs
    chk = mc.Pack.parse(new_chr)
    _, keys = _cfg_motions(chk)[0], re.findall(rb'KEY[ \t]+\d+,', _cfg_motions(chk)[0].payload)
    return new_chr, dict(grafts=grafts, keys=len(keys), size=len(new_chr))


# ---------------------------------------------------------------- ISO install (tail redirect)
def _hd2_slot(hd2_r, i): return hd2_r["ext"] * SEC + 16 + i * 32
def _free_tail(f, dat_size, hd2_r, hed):
    mx = 0
    for i in range(len(hed) // 80):
        f.seek(_hd2_slot(hd2_r, i)); off, size = struct.unpack("<II", f.read(8))
        if 0 < off + size <= dat_size: mx = max(mx, off + size)
    return align(mx)


def run(iso, chars=CHARS, log=print):
    if not os.path.exists(iso):
        raise SystemExit(f"ISO not found: {iso}")
    with open(iso, "r+b") as f:
        recs = ps2iso.parse_root(f)
        hd2_r, dat_r = recs["DATA.HD2"], recs["DATA.DAT"]
        dat_iso = dat_r["ext"] * SEC; dat_size = dat_r["size"]
        hed = ps2iso.read_file(f, recs["DATA.HED"])
        tail = _free_tail(f, dat_size, hd2_r, hed)
        def read_src(name):
            i = ps2iso.archive_find(hed, name)
            if i is None: raise SystemExit(f"{name} not in archive")
            f.seek(_hd2_slot(hd2_r, i)); off, size = struct.unpack("<II", f.read(8)); f.seek(dat_iso + off); return f.read(size)
        def redirect(name, data):
            nonlocal tail
            i = ps2iso.archive_find(hed, name)
            slot = _hd2_slot(hd2_r, i)
            if tail + len(data) > dat_size: raise SystemExit("out of DATA.DAT tail")
            f.seek(dat_iso + tail); f.write(data)
            sec, cnt = tail >> 11, (len(data) + SEC - 1) // SEC
            f.seek(slot); f.write(struct.pack("<IIII", tail, len(data), sec, cnt))
            f.seek(dat_iso + sec * SEC); assert f.read(len(data)) == data, f"{name} readback"
            log(f"redirected {name}: -> {len(data):,} B @sector {sec:#x}")
            tail = align(tail + len(data))
        for who, ch in chars.items():
            base = read_src(ch["base"])
            new_chr, rep = assemble(base, read_src, ch)
            log(f"{who}: {ch['base']} assembled — {rep['grafts']} grafts, {rep['keys']} KEYs, {len(base):,}->{rep['size']:,} B")
            redirect(ch["base"], new_chr)
        log("DONE (town-model assembly)")


def _test():
    """Assemble from the extracted disc (mot_codec.load_pack) and verify KEY table + grafted frames."""
    for who, ch in CHARS.items():
        base_bytes = mc.read_subfile(ch["base"].replace('/', '\\'))[2]
        def read_src(name): return mc.read_subfile(name.replace('/', '\\'))[2]
        new_chr, rep = assemble(base_bytes, read_src, ch)
        print(f"{who}: {rep}")
        chk = mc.Pack.parse(new_chr)
        cfg = _cfg_motions(chk)[0]
        keys = re.findall(rb'KEY[ \t]+(\d+),[ \t]*(\d+),[ \t]*([\d.]+),?[ \t]*//([^\r\n]*)', cfg.payload)
        for i, (a, b, s, nm) in enumerate(keys):
            print(f"   #{i} {a.decode()}-{b.decode()} sp={s.decode()} {nm.decode('shift_jis','replace')[:14]}")
        # verify a grafted body frame exists (run @120, land @215)
        m = mc.Mot.from_record(chk.find(_cfg_motions(chk)[1]))
        fr = set(kf.frame for t in m.tracks for kf in t.keyframes)
        print(f"   body frames now include 120(run)={120 in fr} 150(door)={150 in fr} 205(fall)={205 in fr} 215(land)={215 in fr}")


def main():
    if "--test" in sys.argv: _test(); return
    iso = DEFAULT_ISO
    if "--iso" in sys.argv: iso = sys.argv[sys.argv.index("--iso") + 1]
    run(iso)


if __name__ == "__main__":
    main()
