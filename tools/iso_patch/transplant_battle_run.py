#!/usr/bin/env python3
"""Transplant a character's polished battle run animation into the lighter town model the ally-swap
loads, as a POST-STEP on the mod's already-patched ISO (exactly like the collision baker).

WHY: some town-swap models have a bad/absent run. The town model is chosen for size + shadow + cloth,
not animation quality (the good run lives in the big dungeon battle model, too large to swap in whole).
So we graft ONLY the run motion's keyframes (frames 60-80) from the battle model's `.mot` onto the town
model's `.mot`, per-joint by NAME (the two rigs share the core body nodes 0-37 but the town/event model
inserts extra joints higher up, so a positional copy would garble the arms/head — see mot_codec
`splice_motion_by_joint` + game_data/docs/mot-format.md §5). Nothing is bundled: both models are read
from the user's own ISO at patch time.

The grafted `.mot` grows a few KB, so the town `.chr` is redirected into the free DATA.DAT tail the mod
already opened — the same mechanism ApplySignPatch / the collision bake use. Run AFTER those so the tail
high-water is found past their redirects.

  DC-less: reads everything from the ISO; no DC1_DATA_DIR needed.
  python3 tools/iso_patch/transplant_battle_run.py [--iso "/path/Dark Cloud - Expanded.iso"]
"""
import os, sys, struct
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)                                   # iso_patch/ (ps2iso)
sys.path.insert(0, os.path.join(HERE, "..", "lib"))        # tools/lib (mot_codec)
import ps2iso
import mot_codec as mc

SEC = ps2iso.SECTOR
def align(x, a=SEC): return (x + a - 1) & ~(a - 1)
DEFAULT_ISO = os.path.expanduser("~/ROMs/Patched ISOs/Dark Cloud - Expanded.iso")

# Each transplant grafts one source model's run onto one dest (town-swap) `.chr`, which is redirected back
# into the ISO. A model has TWO animated meshes that must both play the run — the body and its SHADOW — each
# with its own `.mot`+`.mds` sub-files, but they SHARE the cfg KEY table (so index 1 = run drives both). So a
# transplant carries a LIST of `grafts` (body, then shadow), each: src/dst `.mot`+`.mds` sub-file names and a
# (src_lo,src_hi,dst_lo,dst_hi) frame window (the run is 60-80 in every model here). All grafts hit the SAME
# dst `.chr` in one pass, then one redirect.
#   insert_run_key (optional): the town character plays a FIXED motion INDEX — id0=idle, id1=run, id2=walk
#     (EdMoveChara 0x16a160 / Step__CCharacter 0x138530; see mot-format.md §8). If the dest has NO run at
#     index 1 (Ruby's e223c05a is idle0/walk1/poses...), a run KEY must be INSERTED as the 2nd KEY in `cfg`
#     (idle stays 0, run becomes 1, walk shifts to 2). Omit when the dest already has run at index 1 (Ungaga).
TRANSPLANTS = [
    dict(name="Ungaga run",
         src_chr="dun/mainchara/c10b.chr", dst_chr="gedit/e04/chara/e323_2c10a.chr",
         grafts=[
             dict(src_mot="c10a.mot", src_mds="c10a.mds", dst_mot="e323_2c10a.mot", dst_mds="e323_2c10a.mds", window=(60, 80, 60, 80)),
             dict(src_mot="c10s.mot", src_mds="c10s.mds", dst_mot="e323_2c10s.mot", dst_mds="e323_2c10s.mds", window=(60, 80, 60, 80)),  # shadow
         ]),
    dict(name="Ruby run",
         src_chr="dun/mainchara/c05a.chr", dst_chr="gedit/e03/chara/e223c05a.chr",
         grafts=[
             dict(src_mot="c05a.mot", src_mds="c05a.mds", dst_mot="e223c05a.mot", dst_mds="e223c05a.mds", window=(60, 80, 60, 80)),
             dict(src_mot="c05s.mot", src_mds="c05s.mds", dst_mot="e223c05s.mot", dst_mds="e223c05s.mds", window=(60, 80, 60, 80)),  # shadow
         ],
         cfg="info.cfg", insert_run_key=b'KEY\t60,\t80,\t0.55,\t//run\r\n'),
]


def _seal_window(dst_mot, dlo, dhi):
    """Pin every animated joint's keyframe at EXACTLY the run-window boundaries (dlo, dhi) so playback of
    [dlo,dhi] can't interpolate ACROSS a boundary into the dest's neighbouring motions (walk before / event
    poses after). Without this, a joint the battle run keyframes only sparsely (a run's shoulders sit nearly
    still → 1 keyframe) drifts from that lone key toward the adjacent pose over the window — e223's next
    motion is an arms-up stretch, so the run's shoulders crept upward = "arms raise weirdly". Duplicating the
    nearest in-window key to each open boundary holds the joint at its run value across the whole window.
    Dense joints (arms, 21 keys already spanning dlo..dhi) are untouched."""
    for t in dst_mot.tracks:
        win = t.frames_in(dlo, dhi)
        if not win:
            continue
        add = []
        if win[0].frame > dlo:
            k = win[0].copy(); k.frame = dlo; add.append(k)
        if win[-1].frame < dhi:
            k = win[-1].copy(); k.frame = dhi; add.append(k)
        if add:
            t.keyframes = sorted(t.keyframes + add, key=lambda k: k.frame)


def _insert_run_key(cfg_bytes, run_line):
    """Insert `run_line` as the 2nd KEY (ordinal 1) in a `.chr` cfg: after KEY_START + the idle KEY.
    Idle stays index 0, the inserted run becomes index 1, and every following KEY shifts down one."""
    i = cfg_bytes.find(b'KEY_START')
    if i < 0:
        raise SystemExit("cfg has no KEY_START — cannot place the run KEY")
    i = cfg_bytes.find(b'\r\n', i) + 2      # -> start of the idle KEY line (index 0)
    i = cfg_bytes.find(b'\r\n', i) + 2      # -> start of the index-1 line (insertion point)
    return cfg_bytes[:i] + run_line + cfg_bytes[i:]


def _hd2_slot(hd2_r, i): return hd2_r["ext"] * SEC + 16 + i * 32


def _free_tail(f, dat_size, hd2_r, hed):
    """Highest used byte across all DATA.HD2 entries (aligned) = the free tail after prior redirects."""
    mx = 0
    for i in range(len(hed) // 80):
        f.seek(_hd2_slot(hd2_r, i)); off, size = struct.unpack("<II", f.read(8))
        if 0 < off + size <= dat_size:
            mx = max(mx, off + size)
    return align(mx)


def _splice_chr(src_chr_bytes, dst_chr_bytes, t):
    """Apply every graft (body + shadow) from src into dst's `.chr`, per-joint by name, then optionally
    insert a run KEY. Returns (new_dst_chr, reports[]) — one report per graft."""
    src_pack = mc.Pack.parse(src_chr_bytes)
    dst_pack = mc.Pack.parse(dst_chr_bytes)
    reports = []
    for g in t["grafts"]:
        src = mc.Mot.from_record(src_pack.find(g["src_mot"]))
        dst = mc.Mot.from_record(dst_pack.find(g["dst_mot"]))
        src_frames = mc.read_mds_frames(src_pack.find(g["src_mds"]).payload)
        dst_frames = mc.read_mds_frames(dst_pack.find(g["dst_mds"]).payload)
        slo, shi, dlo, dhi = g["window"]
        rep = mc.splice_motion_by_joint(dst, src, src_frames, dst_frames, slo, shi, dlo, dhi)
        _seal_window(dst, dlo, dhi)                            # isolate the run from neighbouring poses (arm-raise fix)
        new_mot = dst.rebuild()                               # full record (header + payload)
        dst_pack.replace_payload(g["dst_mot"], new_mot[dst.data_off:])
        reports.append((g["dst_mot"], rep))
    # optional: give the dest a run at KEY ordinal 1 (models whose motion table has no run there)
    if t.get("insert_run_key"):
        cfg_rec = dst_pack.find(t["cfg"])
        if cfg_rec is None:
            raise SystemExit(f"{t['cfg']} not found in {t['dst_chr']}")
        dst_pack.replace_payload(t["cfg"], _insert_run_key(cfg_rec.payload, t["insert_run_key"]))
    new_chr = dst_pack.rebuild()
    # re-parse guard: the rebuilt .chr must round-trip and every spliced .mot must re-parse
    check = mc.Pack.parse(new_chr)
    for g in t["grafts"]:
        mc.Mot.from_record(check.find(g["dst_mot"]))
    return new_chr, reports


def run(iso, transplants=TRANSPLANTS, log=print):
    if not os.path.exists(iso):
        raise SystemExit(f"ISO not found: {iso}\n(run the mod's Patch ISO first, or pass --iso)")
    with open(iso, "r+b") as f:
        recs = ps2iso.parse_root(f)
        hed_r, hd2_r, dat_r = recs["DATA.HED"], recs["DATA.HD2"], recs["DATA.DAT"]
        dat_iso = dat_r["ext"] * SEC
        dat_size = dat_r["size"]
        hed = ps2iso.read_file(f, hed_r)
        tail = _free_tail(f, dat_size, hd2_r, hed)

        def read_archive(name):
            i = ps2iso.archive_find(hed, name)
            if i is None:
                raise SystemExit(f"{name} not in archive")
            f.seek(_hd2_slot(hd2_r, i)); off, size = struct.unpack("<II", f.read(8))
            f.seek(dat_iso + off); return f.read(size)

        def redirect_file(name, new_data):
            nonlocal tail
            i = ps2iso.archive_find(hed, name)
            if i is None:
                raise SystemExit(f"{name} not in archive")
            if tail + len(new_data) > dat_size:
                raise SystemExit("ran out of DATA.DAT tail space")
            slot = _hd2_slot(hd2_r, i)
            f.seek(slot); _o0, s0 = struct.unpack("<II", f.read(8))
            f.seek(dat_iso + tail); f.write(new_data)
            sec, cnt = tail >> 11, (len(new_data) + SEC - 1) // SEC
            f.seek(slot); f.write(struct.pack("<IIII", tail, len(new_data), sec, cnt))
            f.seek(dat_iso + sec * SEC); back = f.read(len(new_data))
            assert back == new_data, f"{name} readback mismatch"
            log(f"redirected {name}: {s0:,} -> {len(new_data):,} B  @sector {sec:#x} count {cnt}")
            tail = align(tail + len(new_data))

        for t in transplants:
            src_b = read_archive(t["src_chr"])
            dst_b = read_archive(t["dst_chr"])
            new_chr, reports = _splice_chr(src_b, dst_b, t)
            for mot, rep in reports:
                log(f"{t['name']} [{mot}]: grafted {len(rep['written'])} tracks "
                    f"({len(rep['remapped'])} remapped, "
                    f"{len(rep['dropped_no_joint'])+len(rep['dropped_no_track'])} src-only dropped)")
            log(f"{t['name']}: {t['dst_chr']} {len(dst_b):,} -> {len(new_chr):,} B")
            redirect_file(t["dst_chr"], new_chr)
        log("DONE (battle-run transplant)")


def main():
    args = sys.argv[1:]
    iso = DEFAULT_ISO
    if "--iso" in args:
        k = args.index("--iso"); iso = args[k + 1]
    run(iso)


if __name__ == "__main__":
    main()
