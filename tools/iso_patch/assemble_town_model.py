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
            # slot 7 (damage — never triggered in town) repurposed as the ladder REFUSAL: e613c04cat's head-shake
            # sequence #6 no [115-135] / #7 hold [140-150] / #8 return [155-165], grafted whole into fresh frames
            # past the land clip. TownLadder plays it via the idle-motion cave when a blocked mount raises
            # RefusalRequested (one pass, then back to idle).
            dict(idx=7, frames=(228, 278), speed=0.30, name="refuse(shake)", src="dun/d01/event/e613c04cat.chr", win=(115, 165)),
            dict(idx=8, frames=(205, 214), speed=0.50, name="fall(leap)", src="gedit/s86/chara/c04cat.chr", win=(205, 214)),
            dict(idx=9, frames=(215, 227), speed=0.36, name="land",  src="gedit/s86/chara/c04cat.chr",   win=(215, 227)),
            # idx 10 = DOUBLE-DOOR (両開き): Toan's c01d has an 11th KEY the door system plays at double doors —
            # every ally build must fill it or double-doors play nothing (or worse, a choreography clip).
            dict(idx=10, frames=(150, 159), speed=0.20, name="door3(dbl)", src=None),   # reuse the grafted door clip
            # CHOREOGRAPHY-ONLY slots (the engine drives 0-10; these are played by TownLadder's jump script via
            # _SET_NPC_MOTION, which accepts any KEY index — the docs/town-swap-animation-map.md up-ladder plan):
            # 11 = s86 #3 "ready" crouch; 12 = e04c04cat #5 float/hop-up, fast, NO root offset (the door slot's
            # copy of this clip is z-shifted for the door teleport — unusable mid-flight).
            dict(idx=11, frames=(95, 105),  speed=0.40, name="jump-ready", src="gedit/s86/chara/c04cat.chr", win=(95, 105)),
            dict(idx=12, frames=(285, 294), speed=0.60, name="float-up",   src="gedit/e01/chara/e04c04cat.chr", win=(160, 169)),
            # 13 = the base's own walk (60-80) copied to fresh frames and REVERSED — the backwards steps of the
            # ladder-jump alignment (the engine can't play a clip backwards; speed overrides are gated positive).
            dict(idx=13, frames=(300, 320), speed=0.30, name="walk-back",  src="gedit/e01/chara/c04pcat.chr", win=(60, 80), reverse=True),
        ],
    ),
    # Osmond (docs/town-swap-animation-map.md "Osmond — cataloged 2026-09-05"): base c18p (4 KEYs; body frames
    # 1..80). CUTSCENE CONSTRAINT: the replayable s13/s2201 talk-to-Osmond scene plays idx 0 and 3 — their
    # KEY values (frames+speed) are reproduced EXACTLY. Doors reuse the talk clip (the user's resolved call);
    # run = the same #1 clip at a bumped KEY speed (no better run exists); item-get = talk held at frame 69
    # (hand-out pose, sampled hold); fall/land = e403c18a #10/#11 (jump-down seq's fall-loop + land).
    "Osmond": dict(
        base="gedit/e05/chara/c18p.chr",
        slots=[
            dict(idx=0, frames=(10, 20),   speed=0.10, name="idle",        src=None),   # EXACT original (cutscene)
            # run in its OWN window (base's 30-50 copied to 130-150): the engine's run↔walk foot-phase blend
            # (ids 1/2 hard-case) breaks when both ids share one window at different speeds — the "walk plays
            # faster than run" inversion. Toan's own c01d runs/walks from separate windows.
            dict(idx=1, frames=(130, 150), speed=0.75, name="run(sped)",   src="gedit/e05/chara/c18p.chr", win=(30, 50)),
            dict(idx=2, frames=(30, 50),   speed=0.20, name="walk",        src=None),   # eased from the 0.30 original (read fast in-game)
            dict(idx=3, frames=(60, 80),   speed=0.20, name="door(talk)",  src=None),   # EXACT original (cutscene)
            dict(idx=4, frames=(60, 80),   speed=0.30, name="door2(talk)", src=None),   # pull-door: Toan's door cadence
            dict(idx=5, frames=(90, 92),   speed=0.10, name="item(hold69)", hold=69),   # talk hand-out pose, held
            dict(idx=6, frames=(90, 92),   speed=0.10, name="item-loop",   src=None),   # same held pose
            dict(idx=7, frames=(10, 20),   speed=0.10, name="damage(skip)", src=None),
            dict(idx=8, frames=(100, 108), speed=0.30, name="fall",        src="gedit/e05/chara/e403c18a.chr", win=(210, 218)),
            dict(idx=9, frames=(110, 125), speed=0.30, name="land",        src="gedit/e05/chara/e403c18a.chr", win=(220, 235)),
            dict(idx=10, frames=(60, 80),  speed=0.30, name="door3(dbl)",  src=None),   # double-door (両開き) = talk clip
            # CHOREOGRAPHY (TownLadder's Osmond sequences, played via _SET_NPC_MOTION):
            # down-ladder launch = e403 #9 jump-down dive; up-ladder = the helicopter backpack set from
            # e402c18a·s13 (#16 propeller-out / #17 start-fly / #18 fly-loop) + the REVERSED pair for the
            # top-of-ladder landing (descend + stow), per the catalog.
            dict(idx=11, frames=(155, 175), speed=0.30, name="jump-down",    src="gedit/e05/chara/e403c18a.chr", win=(185, 205)),
            dict(idx=12, frames=(180, 235), speed=0.25, name="propeller",    src="gedit/s13/chara/e402c18a.chr", win=(280, 335)),
            dict(idx=13, frames=(240, 255), speed=0.25, name="start-fly",    src="gedit/s13/chara/e402c18a.chr", win=(345, 360)),
            dict(idx=14, frames=(260, 270), speed=0.20, name="fly-loop",     src="gedit/s13/chara/e402c18a.chr", win=(360, 370)),
            dict(idx=15, frames=(275, 290), speed=0.25, name="rev-startfly", src="gedit/s13/chara/e402c18a.chr", win=(345, 360), reverse=True),
            dict(idx=16, frames=(295, 350), speed=0.30, name="rev-propeller(stow)", src="gedit/s13/chara/e402c18a.chr", win=(280, 335), reverse=True),
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


def _sample_track(t, frame):
    """The track's interpolated value at `frame`: lerp for translations, sign-corrected nlerp for rotation
    quaternions (visually equivalent to the engine's blend at keyframe-neighbor angular gaps)."""
    import math
    prev = max((k for k in t.keyframes if k.frame <= frame), key=lambda k: k.frame, default=None)
    nxt = min((k for k in t.keyframes if k.frame >= frame), key=lambda k: k.frame, default=None)
    if prev is None and nxt is None: return None
    if prev is None: return nxt.value
    if nxt is None or nxt.frame == prev.frame: return prev.value
    u = (frame - prev.frame) / (nxt.frame - prev.frame)
    a, b = prev.value, nxt.value
    if t.w2 == 0:                                     # quaternion
        if sum(x * y for x, y in zip(a, b)) < 0: b = tuple(-x for x in b)
        v = [ax + (bx - ax) * u for ax, bx in zip(a, b)]
        n = math.sqrt(sum(x * x for x in v)) or 1.0
        return tuple(x / n for x in v)
    return tuple(ax + (bx - ax) * u for ax, bx in zip(a, b))


def _bake_hold(pack, mot_name, src_frame, dlo, dhi):
    """Bake a STATIC held pose: sample every track at `src_frame` and write sealed keyframes at dlo AND dhi
    with that value (only ~1/5 of tracks keyframe any given source frame — the rest interpolate, so a plain
    window graft of a single frame would leave most joints drifting toward neighboring clips)."""
    m = mc.Mot.from_record(pack.find(mot_name))
    for t in m.tracks:
        v = _sample_track(t, src_frame)
        if v is None or not t.keyframes:
            continue
        t.keyframes[:] = [k for k in t.keyframes if not (dlo <= k.frame <= dhi)]
        for f in (dlo, dhi):
            kf = t.keyframes[0].copy()
            kf.frame = f
            struct.pack_into('<4f', kf.raw, 0x10, *v)
            t.keyframes.append(kf)
        t.keyframes.sort(key=lambda k: k.frame)
    pack.replace_payload(mot_name, m.rebuild()[m.data_off:])


def _seal_graft(dst_pack, dst_mot, dst_mds, src_pack, src_mot, src_mds, slo, shi, dlo, dhi):
    """Guarantee every grafted track has explicit keyframes AT the window edges (dlo/dhi), valued by sampling
    the SOURCE at slo/shi. Sparse tracks (no kf exactly on an edge) otherwise interpolate across into the
    NEIGHBORING clips — a pop at loop wraps (Osmond's fall-loop) and pose bleed at clip starts (the old
    Ruby arm-raise). Runs after every graft, before reverse/root_offset."""
    dframes = mc.read_mds_frames(dst_pack.find(dst_mds).payload)
    sframes = mc.read_mds_frames(src_pack.find(src_mds).payload)
    remap = {i: dframes.index(n) for i, n in enumerate(sframes) if n in dframes}
    dm = mc.Mot.from_record(dst_pack.find(dst_mot))
    sm = mc.Mot.from_record(src_pack.find(src_mot))
    dtracks = {(t.w0, t.w2): t for t in dm.tracks}
    changed = False
    for st in sm.tracks:
        if st.w0 not in remap:
            continue
        dt = dtracks.get((remap[st.w0], st.w2))
        if dt is None or not dt.keyframes:
            continue
        for sf, df in ((slo, dlo), (shi, dhi)):
            if any(k.frame == df for k in dt.keyframes):
                continue
            v = _sample_track(st, sf)
            if v is None:
                continue
            kf = dt.keyframes[0].copy()
            kf.frame = df
            struct.pack_into('<4f', kf.raw, 0x10, *v)
            dt.keyframes.append(kf)
            dt.keyframes.sort(key=lambda k: k.frame)
            changed = True
    if changed:
        dst_pack.replace_payload(dst_mot, dm.rebuild()[dm.data_off:])


def _reverse_window(pack, mot_name, dlo, dhi):
    """Mirror the keyframes inside [dlo,dhi] (frame -> dlo+dhi-frame, re-sorted ascending) — bakes a clip that
    plays BACKWARDS (the engine cannot: both motion-speed override paths are gated positive)."""
    m = mc.Mot.from_record(pack.find(mot_name))
    for t in m.tracks:
        win = [kf for kf in t.keyframes if dlo <= kf.frame <= dhi]
        if not win:
            continue
        for kf in win:
            kf.frame = dlo + dhi - kf.frame
        t.keyframes.sort(key=lambda k: k.frame)
    pack.replace_payload(mot_name, m.rebuild()[m.data_off:])


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
        if s.get("hold") is not None:                # static held pose sampled from the BASE's own motion
            dlo, dhi = s["frames"]
            _bake_hold(base, bmot, s["hold"], dlo, dhi)
            if smot: _bake_hold(base, smot, s["hold"], dlo, dhi)
            grafts += 1
            continue
        if not s.get("src"):
            continue
        sp = src(s["src"])
        scfg, sbmot, sbmds, ssmot, ssmds = _cfg_motions(sp)
        wlo, whi = s["win"]; dlo, dhi = s["frames"]
        _graft(base, bmot, bmds, sp, sbmot, sbmds, wlo, whi, dlo, dhi)               # body
        _seal_graft(base, bmot, bmds, sp, sbmot, sbmds, wlo, whi, dlo, dhi)          # edge keyframes (loop-clean)
        if smot and ssmot:                                                            # shadow (keep body+shadow in sync)
            _graft(base, smot, smds, sp, ssmot, ssmds, wlo, whi, dlo, dhi)
            _seal_graft(base, smot, smds, sp, ssmot, ssmds, wlo, whi, dlo, dhi)
        if s.get("root_offset"):                                                      # pull the reach back (door)
            _apply_root_offset(base, bmot, dlo, dhi, s["root_offset"])
            if smot: _apply_root_offset(base, smot, dlo, dhi, s["root_offset"])
        if s.get("reverse"):                                                          # bake a backwards-playing clip
            _reverse_window(base, bmot, dlo, dhi)
            if smot: _reverse_window(base, smot, dlo, dhi)
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
