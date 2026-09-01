#!/usr/bin/env python3
"""Bake the authored Brownboo CAMERA collision into the mod's already-patched ISO, in place.

Rebuilds gedit/s04/scene.scn's `s04g01_v` (the town camera-collision variant — the slot every other town
names `_c`, see LoadMapObject @0x19B790) as a flat multi-node MDS:
  • `v`         — the vanilla terrain camera hull, byte-identical tris (404) — NOT ours to change;
  • `c56_*`     — obj56 with ONLY the iwa01 rock replaced by the CSG hull (horn funnels + tunnel),
                  kd-split into <=100-tri nodes. Building cylinders stay VANILLA (the tightened
                  cylinders of 2026-08 clipped visibly — camera geometry needs padding; tight_obj56
                  remains available but unwired). The player `_a` is untouched. Same proven pipeline as the
Queens e03g04_c bake (build_flat_mds + _replace_a_block), just targeting suffix `_v`.

  DC1_DATA_DIR=... python3 tools/iso_patch/collision/bake_brownboo_camera_iso.py \
      [--iso "/path/Dark Cloud - Expanded.iso"]
"""
import os, sys, struct
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, ".."))
sys.path.insert(0, os.path.join(HERE, "..", ".."))
os.environ.setdefault("DC1_DATA_DIR", os.path.join(HERE, "..", ".."))
import ps2iso
from bake_player_camera_collision import build_flat_mds, _replace_a_block
from brownboo_camera_collision import (vanilla_v_nodes, iwa01_ring_obj56, _kd_split)

SEC = ps2iso.SECTOR
def align(x, a=SEC): return (x + a - 1) & ~(a - 1)

DEFAULT_ISO = os.path.expanduser("~/ROMs/Patched ISOs/Dark Cloud - Expanded.iso")
SCENE = "gedit/s04/scene.scn"
H01_POS_Y = -10.0                                   # s04h01's single mapinfo placement (0,-10,0) rot 0


def baked_named(scn=None):
    """The [(node_name, world tris)] list for the rebuilt s04g01_v.

    scn: s04 scene.scn BYTES to decode the vanilla inputs from (the app's patch flow passes the fresh
    IsoPatcher output, whose `_v` is still vanilla — no DC1_DATA_DIR extraction needed there); None = the
    local extraction (authoring env). ⚠ Never pass an ALREADY-BAKED scene: its `_v` no longer has the
    vanilla obj56 node, and the removal drift guard will trip."""
    van_v = next((tris for nn, tris in vanilla_v_nodes(scn) if nn == 'v'), None)
    if not van_v:
        raise SystemExit("vanilla s04g01_v node 'v' not found (already-baked scene?)")
    named = [('v', van_v)]
    named += [(f'c56_{i:02d}', bk) for i, bk in enumerate(_kd_split(iwa01_ring_obj56(scn), 100))]
    return named


def _hd2_slot(hd2_r, i): return hd2_r["ext"] * SEC + 16 + i * 32


def main():
    args = sys.argv[1:]
    iso = DEFAULT_ISO
    if "--iso" in args:
        k = args.index("--iso"); iso = args[k + 1]; del args[k:k + 2]
    if not os.path.exists(iso):
        raise SystemExit(f"ISO not found: {iso} (run the mod's Patch ISO first, or pass --iso)")

    named = baked_named()
    tot = sum(len(t) for _, t in named)
    print(f"s04g01_v rebuild: {len(named)} nodes, {tot} tris "
          f"({', '.join(f'{n}:{len(t)}' for n, t in named[:4])}, ...)")

    with open(iso, "r+b") as f:
        recs = ps2iso.parse_root(f)
        hed_r, hd2_r, dat_r = recs["DATA.HED"], recs["DATA.HD2"], recs["DATA.DAT"]
        dat_iso = dat_r["ext"] * SEC
        dat_size = dat_r["size"]
        hed = ps2iso.read_file(f, hed_r)
        # free tail = past the highest used byte across all HD2 entries (IsoPatcher's convention)
        n = len(hed) // 80
        tail = 0
        for i in range(n):
            f.seek(_hd2_slot(hd2_r, i)); off, size = struct.unpack("<II", f.read(8))
            if 0 < off + size <= dat_size:
                tail = max(tail, off + size)
        tail = align(tail)
        print(f"free tail @ DATA.DAT {tail:#x}  ({(dat_size - tail)/1e6:.1f} MB free)")

        i = ps2iso.archive_find(hed, SCENE)
        if i is None:
            raise SystemExit(f"{SCENE} not in archive")
        slot = _hd2_slot(hd2_r, i)
        f.seek(slot); o0, s0 = struct.unpack("<II", f.read(8))
        f.seek(dat_iso + o0); scn0 = f.read(s0)

        new_scn, delta = _replace_a_block(scn0, 's04g01', build_flat_mds(named), suffix='_v')
        print(f"scene {len(scn0):,} -> {len(new_scn):,} B (delta {delta:+,})")

        if tail + len(new_scn) > dat_size:
            raise SystemExit("ran out of DATA.DAT tail space")
        f.seek(dat_iso + tail); f.write(new_scn)
        sec, cnt = tail >> 11, (len(new_scn) + SEC - 1) // SEC
        f.seek(slot); f.write(struct.pack("<IIII", tail, len(new_scn), sec, cnt))
        f.seek(dat_iso + sec * SEC); back = f.read(len(new_scn))
        assert back[:4] == b"SCN\x00", "scene readback bad"
        print(f"redirected {SCENE}: {s0:,} -> {len(new_scn):,} B  @sector {sec:#x} count {cnt}")

    # verify: decode the baked block back out of the new scene bytes
    import re
    import scene_placed
    off, size = scene_placed._scndir(new_scn)['s04g01']
    sub = new_scn[off:off + size]
    m = next(re.finditer(rb's04g01_v\.mds\x00', sub))
    vo = struct.unpack_from('<I', sub, m.end() + 3)[0]
    assert sub[vo:vo + 3] == b'MDS', "baked _v block not MDS"
    cnt2 = struct.unpack_from('<I', sub, vo + 8)[0]
    print(f"verify: baked s04g01_v has {cnt2} nodes (want {len(named)})")
    assert cnt2 == len(named)
    print(f"\nDONE (in place) -> {iso}")


if __name__ == "__main__":
    main()
