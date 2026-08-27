#!/usr/bin/env python3
"""Bake the structure/perimeter/short-wall camera collision (+ player-only canal walls) into a town's ground
`_a`, as a POST-STEP on the mod's already-patched ISO.

Run this AFTER the mod's "Patch ISO" (IsoPatcher) has produced its disc: this reads that ISO's current
e03/scene.scn (already carrying the fishing-sign kanban etc.), bakes our collision into it, and redirects
the scene into the ISO's free DATA.DAT tail — so the resulting disc has EVERY mod ISO patch plus this
collision. It operates IN PLACE and re-uses the free tail IsoPatcher already opened (no second dummy-absorb).

  DC1_DATA_DIR=... python3 tools/iso_patch/collision/bake_structure_collision_iso.py [e03 ...]
      [--iso "/path/Dark Cloud - Expanded.iso"]           # default: ~/ROMs/Patched ISOs/Dark Cloud - Expanded.iso
  --standalone : instead make a fresh copy of $DC1_ISO + absorb dummy first (isolated collision-only test)

Pair with the mod's CameraWallCollision.TerrainOnly = true.
"""
import os, sys, struct, shutil
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)                              # collision/ (bake_player_camera_collision)
sys.path.insert(0, os.path.join(HERE, ".."))         # iso_patch/ (ps2iso)
sys.path.insert(0, os.path.join(HERE, "..", ".."))   # tools/ (shared infra, via bake_player's imports)
# This bake reads scene/mapinfo from the ISO (via bake_structures_from_bytes' monkeypatched load_scene) and its
# canal/perimeter geometry is authored constants — it never touches an extracted disc. DC1_DATA_DIR is only
# extract_scene_mesh's import-time guard, so default it (to the tools dir; never dereferenced) so callers that
# have only the ISO — e.g. IsoPatcher's post-step — can run us without an extracted-disc dir.
os.environ.setdefault("DC1_DATA_DIR", os.path.join(HERE, "..", ".."))
import ps2iso
from bake_player_camera_collision import bake_structures_from_bytes

SEC = ps2iso.SECTOR
def align(x, a=SEC): return (x + a - 1) & ~(a - 1)

TOWNS = {"e03": ("gedit/e03/scene.scn", "gedit/e03/mapinfo.cfg")}
# s04 (Brownboo) is handled separately below: its bake REBUILDS the s04g01_v camera variant (authored
# obj56 + s04h01 hull, tools/brownboo_camera_collision.py) instead of the e03-style ground-`_a` pipeline.
DEFAULT_ISO = os.path.expanduser("~/ROMs/Patched ISOs/Dark Cloud - Expanded.iso")


def _hd2_slot(hd2_r, i): return hd2_r["ext"] * SEC + 16 + i * 32


def _free_tail(f, dat_size, hd2_r, hed):
    """Highest used byte across all DATA.HD2 entries (aligned) = the free tail after IsoPatcher's redirects."""
    n = len(hed) // 80
    mx = 0
    for i in range(n):
        f.seek(_hd2_slot(hd2_r, i)); off, size = struct.unpack("<II", f.read(8))
        if 0 < off + size <= dat_size:
            mx = max(mx, off + size)
    return align(mx)


def main():
    args = sys.argv[1:]
    standalone = "--standalone" in args
    args = [a for a in args if a != "--standalone"]
    iso = DEFAULT_ISO
    if "--iso" in args:
        k = args.index("--iso"); iso = args[k + 1]; del args[k:k + 2]
    codes = args or ["e03", "s04"]

    if standalone:
        src = os.environ.get("DC1_ISO")
        if not src:
            raise SystemExit("Set $DC1_ISO for --standalone")
        iso = os.path.join(os.path.expanduser("~/ROMs"), "Patched ISOs", "Dark Cloud - StructureCollision.iso")
        os.makedirs(os.path.dirname(iso), exist_ok=True)
        print(f"copying -> {iso} ...")
        shutil.copyfile(src, iso)
    if not os.path.exists(iso):
        raise SystemExit(f"ISO not found: {iso}\n(run the mod's Patch ISO first, or pass --iso / --standalone)")

    with open(iso, "r+b") as f:
        recs = ps2iso.parse_root(f)
        hed_r, hd2_r, dat_r = recs["DATA.HED"], recs["DATA.HD2"], recs["DATA.DAT"]
        dat_iso = dat_r["ext"] * SEC
        dat_size = dat_r["size"]
        if standalone:
            free_off, free_bytes = ps2iso.absorb_dummy(f, recs)
            recs = ps2iso.parse_root(f); dat_r = recs["DATA.DAT"]; dat_size = dat_r["size"]
            print(f"absorbed dummy: +{free_bytes/1e6:.1f} MB free")
        hed = ps2iso.read_file(f, hed_r)
        tail = _free_tail(f, dat_size, hd2_r, hed)
        print(f"target ISO: {iso}")
        print(f"free tail @ DATA.DAT {tail:#x}  ({(dat_size - tail)/1e6:.1f} MB free)")

        def redirect_file(name, new_data, verify_head=None):
            nonlocal tail
            i = ps2iso.archive_find(hed, name)
            if i is None:
                raise SystemExit(f"{name} not in archive")
            slot = _hd2_slot(hd2_r, i)
            f.seek(slot); o0, s0 = struct.unpack("<II", f.read(8))
            if tail + len(new_data) > dat_size:
                raise SystemExit("ran out of DATA.DAT tail space")
            f.seek(dat_iso + tail); f.write(new_data)
            sec, cnt = tail >> 11, (len(new_data) + SEC - 1) // SEC
            f.seek(slot); f.write(struct.pack("<IIII", tail, len(new_data), sec, cnt))
            f.seek(dat_iso + sec * SEC); back = f.read(len(new_data))
            if verify_head is not None:
                assert back[:len(verify_head)] == verify_head, f"{name} readback bad"
            print(f"redirected {name}: {s0:,} -> {len(new_data):,} B  @sector {sec:#x} count {cnt}")
            tail = align(tail + len(new_data))

        def read_archive(name):
            i = ps2iso.archive_find(hed, name)
            f.seek(_hd2_slot(hd2_r, i)); off, size = struct.unpack("<II", f.read(8))
            f.seek(dat_iso + off); return f.read(size)

        for code in codes:
            if code == "s04":
                # Brownboo s04g01_v camera-collision rebuild (bake_brownboo_camera_iso.baked_named):
                # custom obj56 = central-cylinder simplification + the iwa01 rock replaced by a CSG hull
                # (cylinder shell minus flared tunnel cutter, tools/export_iwa01_blender.py, frozen to
                # iwa01_hull_data.py). RE-ENABLED 2026-08 for the CSG-hull approach after the earlier
                # per-leg-node attempt clipped; CullBuildings (see-through houses) still runs alongside.
                from bake_player_camera_collision import build_flat_mds, _replace_a_block
                from bake_brownboo_camera_iso import baked_named
                s04_scene = read_archive("gedit/s04/scene.scn")
                try:
                    named = baked_named()                      # extraction (authoring env)
                except FileNotFoundError:
                    named = baked_named(s04_scene)             # app env: fresh IsoPatcher scene = vanilla _v
                s04_new, s04_delta = _replace_a_block(s04_scene, 's04g01', build_flat_mds(named), suffix='_v')
                print(f"s04: s04g01_v rebuilt: {len(named)} nodes, {sum(len(t) for _, t in named)} tris, "
                      f"scene {len(s04_scene):,} -> {len(s04_new):,} B")
                redirect_file("gedit/s04/scene.scn", s04_new, b"SCN\x00")
                continue
            if code not in TOWNS:
                print(f"skip {code}"); continue
            scene_rel, mapinfo_rel = TOWNS[code]
            scene0 = read_archive(scene_rel)               # current scene (post-IsoPatcher: has the kanban etc.)
            mapinfo0 = read_archive(mapinfo_rel)
            baked, stats, manifest = bake_structures_from_bytes(scene_rel, scene0, mapinfo0)
            cam_nodes = sum(s[2] for s in stats); inv_nodes = sum(s[3] for s in stats)
            cam_tris = sum(s[4] for s in stats); inv_tris = sum(s[5] for s in stats)
            print(f"{code}: {cam_nodes} camera nodes ({cam_tris} tris) + {inv_nodes} player-only ({inv_tris} tris), "
                  f"scene {len(scene0):,} -> {len(baked):,} B")
            if code == "e03":
                # VISUAL-only canal west-end cap (2 tris @ y=50 closing the look-up gap from the low-tide
                # canal floor). Applied AFTER the collision bake, deliberately: the cap must never enter
                # the camera/player collision (it would be an invisible ceiling over the canal).
                from canal_visual_cap import add_canal_cap
                baked, _cap = add_canal_cap(baked)
                # Wading ripple TEXTURE (v7: the decal itself is a static part injected by IsoPatcher;
                # CanalTide flips its layer to 0x15 so it draws in the water pass): swap the animated
                # ripple texture's PIXELS (e01b22 + e01b23 strip in e03's own img.pak, byte-size-identical)
                # for the cast-bobber ripple sprite (`hamon`, already in this same pak's effect.img).
                from canal_ripple_node import retexture_ripple_bank
                redirect_file("gedit/e03/img.pak",
                              retexture_ripple_bank(read_archive("gedit/e03/img.pak")))
            redirect_file(scene_rel, baked, b"SCN\x00")

        print(f"\nDONE (in place) -> {iso}\nBoot with the mod (CameraWallCollision.TerrainOnly=true).")


if __name__ == "__main__":
    main()
