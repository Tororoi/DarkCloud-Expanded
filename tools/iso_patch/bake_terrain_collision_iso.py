#!/usr/bin/env python3
"""EXPERIMENT bake: split TERRAIN camera collision into a patched ISO COPY.

Rebuilds each town's ground `_a` (e03g*) as many <=100-poly self-culling nodes (see
tools/bake_terrain_camera_collision.py) and redirects scene.scn's DATA.HD2 entry. Pair with the mod's
CameraWallCollision.TerrainOnly = true so the camera gathers ONLY this custom split terrain (no buildings).

  DC1_ISO=/path/to/'Dark Cloud (USA).iso' python3 tools/iso_patch/bake_terrain_collision_iso.py [e03 ...]
  -> ~/ROMs/Patched ISOs/Dark Cloud - TerrainCollision.iso
"""
import os, sys, struct, shutil
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, ".."))
import ps2iso
from bake_terrain_camera_collision import bake_terrain_from_bytes

SEC = ps2iso.SECTOR
def align(x, a=SEC): return (x + a - 1) & ~(a - 1)

TOWNS = {"e03": ("gedit/e03/scene.scn", r"e03g0[45]$")}


def main():
    codes = [a for a in sys.argv[1:] if not a.startswith("-")] or ["e03"]
    src = os.environ.get("DC1_ISO")
    if not src:
        raise SystemExit("Set $DC1_ISO")
    outdir = os.path.join(os.path.expanduser("~/ROMs"), "Patched ISOs")
    os.makedirs(outdir, exist_ok=True)
    out = os.path.join(outdir, "Dark Cloud - TerrainCollision.iso")
    print(f"copying -> {out} ...")
    shutil.copyfile(src, out)

    with open(out, "r+b") as f:
        recs = ps2iso.parse_root(f)
        hed_r, hd2_r, dat_r = recs["DATA.HED"], recs["DATA.HD2"], recs["DATA.DAT"]
        dat_iso = dat_r["ext"] * SEC
        free_off, free_bytes = ps2iso.absorb_dummy(f, recs)
        print(f"absorbed dummy: +{free_bytes/1e6:.1f} MB free")
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

        for code in codes:
            if code not in TOWNS:
                print(f"skip {code}"); continue
            scene_rel, gre = TOWNS[code]
            i = ps2iso.archive_find(hed, scene_rel)
            if i is None:
                print(f"skip {code}: {scene_rel} not in archive"); continue
            f.seek(hd2_r["ext"] * SEC + 16 + i * 32); so, ss = struct.unpack("<II", f.read(8))
            f.seek(dat_iso + so); scene0 = f.read(ss)
            baked, stats = bake_terrain_from_bytes(scene_rel, scene0, gre)
            nodes = sum(nb for _, _, nb, _, _, _ in stats)
            print(f"{code}: {len(stats)} grounds -> {nodes} split nodes, scene {len(scene0):,} -> {len(baked):,} B")
            redirect_file(scene_rel, baked, b"SCN\x00")

        print(f"\nDONE -> {out}\nBoot with the mod (CameraWallCollision.TerrainOnly=true).")


if __name__ == "__main__":
    main()
