#!/usr/bin/env python3
"""TEST bake: per-building CAMERA collision baked into a town scene.scn, into a patched ISO COPY.

Replaces each building sub-file's `_a` root-node collision with the building's FULL visible geometry
(unsimplified), so the town camera (reading `_a` via the +0xdc=+0xd0 alias) collides with the true building
silhouette. This is the performance stress-test the plan calls for — full geometry, no simplification — to
see whether the camera gather handles the poly volume.

  * The building tris are read from the ISO's OWN scene.scn and baked into the ISO COPY. Nothing
    game-derived is committed to git; run against your own disc.
  * Data-layer only (grows scene.scn, redirects its DATA.HD2 entry). The camera pull-in itself is the mod's
    runtime ELF patches — boot the resulting ISO with the mod running.

Usage:
  DC1_ISO=/path/to/'Dark Cloud (USA).iso'  python3 tools/iso_patch/bake_building_collision_iso.py [e03 ...]
  -> ~/ROMs/Patched ISOs/Dark Cloud - CamCollision.iso
"""
import os, sys, struct, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)                          # ps2iso
sys.path.insert(0, os.path.join(HERE, ".."))      # bake_camera_collision
import ps2iso
from bake_camera_collision import bake_scene_from_bytes

SEC = ps2iso.SECTOR
def align(x, a=SEC): return (x + a - 1) & ~(a - 1)

# town code -> (scene.scn archive path, building name regex)
TOWNS = {
    "e03": ("gedit/e03/scene.scn", r"e03h\d\d$"),   # Queens (fishing town, most detail)
}


def main():
    codes = [a for a in sys.argv[1:] if not a.startswith("-")] or ["e03"]
    src = os.environ.get("DC1_ISO")
    if not src:
        raise SystemExit("Set $DC1_ISO (see .env.sample)")
    outdir = os.path.join(os.path.expanduser("~/ROMs"), "Patched ISOs")
    os.makedirs(outdir, exist_ok=True)
    out = os.path.join(outdir, "Dark Cloud - CamCollision.iso")

    print(f"copying -> {out} ...")
    shutil.copyfile(src, out)

    with open(out, "r+b") as f:
        recs = ps2iso.parse_root(f)
        hed_r, hd2_r, dat_r = recs["DATA.HED"], recs["DATA.HD2"], recs["DATA.DAT"]
        dat_iso = dat_r["ext"] * SEC

        free_off, free_bytes = ps2iso.absorb_dummy(f, recs)
        print(f"absorbed dummy: +{free_bytes/1e6:.1f} MB free at DATA.DAT offset {free_off:#x}")
        hed = ps2iso.read_file(f, hed_r)
        tail = align(free_off)

        def redirect_file(name, new_data, verify_head=None):
            nonlocal tail
            i = ps2iso.archive_find(hed, name)
            if i is None:
                raise SystemExit(f"{name} not in archive")
            slot = hd2_r["ext"] * SEC + 16 + i * 32
            f.seek(slot); o0, s0 = struct.unpack("<II", f.read(8))
            if len(new_data) > free_off + free_bytes - tail:
                raise SystemExit("tail exhausted")
            f.seek(dat_iso + tail); f.write(new_data)
            sec, cnt = tail >> 11, (len(new_data) + SEC - 1) // SEC
            f.seek(slot); f.write(struct.pack("<IIII", tail, len(new_data), sec, cnt))
            f.seek(dat_iso + sec * SEC); back = f.read(len(new_data))
            if verify_head is not None:
                assert back[:len(verify_head)] == verify_head, f"{name} readback bad"
            print(f"redirected {name}: {s0:,} -> {len(new_data):,} B  @sector {sec:#x} count {cnt}")
            tail = align(tail + len(new_data))
            return o0, s0

        for code in codes:
            if code not in TOWNS:
                print(f"skip {code}: no mapping"); continue
            scene_rel, name_re = TOWNS[code]
            i = ps2iso.archive_find(hed, scene_rel)
            if i is None:
                print(f"skip {code}: {scene_rel} not in archive"); continue
            f.seek(hd2_r["ext"] * SEC + 16 + i * 32); so, ss = struct.unpack("<II", f.read(8))
            f.seek(dat_iso + so); scene0 = f.read(ss)
            baked, stats = bake_scene_from_bytes(scene_rel, scene0, name_re)
            tris = sum(t for _, t, _, _ in stats)
            print(f"{code}: baked {len(stats)} buildings, {tris} collision tris, "
                  f"scene {len(scene0):,} -> {len(baked):,} B")
            redirect_file(scene_rel, baked, b"SCN\x00")

        print(f"\nDONE -> {out}\nBoot with the mod running; camera should now hug the full building shapes.")


if __name__ == "__main__":
    main()
