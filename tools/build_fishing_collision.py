#!/usr/bin/env python3
"""Build the custom FISHING collision bins (the DCFC files FishingCollision.AppendCustomCollision appends to
the fishing cpoly at session start) from one manifest, one writer:

  map  2  Queens        game_data/queens/queens_2.bin                <- queens_fishing_collision (bridges + pipes
                                                                      from the disc + authored containment walls)
  map 23  Yellow Drops  game_data/yellowdrops/yellowdrops_23.bin     <- yellowdrops_westbank_data.westbank_fish_walls

Both embed ISO-derived geometry, so they stay UNTRACKED and the csproj Links them into
Resources/FishingCollision/ at build (guarded by Exists()). They are appended at RUNTIME (not ISO-baked)
because baking them into the ground `_a` would make them PLAYER collision too. Since 2026-09 the native
walls are no longer stripped from the fishing cpoly, so each bin should only carry walls the native
geometry lacks. Yellow Drops' bank chain IS such a wall (native `_a` stops at the bank top, y>=30; the fish
sit at y~-4) and stays. Queens' 440 bridge + 216 pipe tris duplicate the baked e03 ground `_a` and can
probably be trimmed to its 57 containment walls once the in-game check confirms it.
The Brownboo rocks are NOT a bin: they are authored Python data (iso_patch/collision/brownboo_rock_data.py)
baked into the ISO's s04g01_a by brownboo_collision_builder.py.

Format: 'DCFC', u32 version=1, u32 mapNo, u32 triCount, then triCount x 9 floats (3 verts; the mod
computes the plane normal itself).

    python3 tools/build_fishing_collision.py            # build all
    python3 tools/build_fishing_collision.py --check    # rebuild in memory; fail if any file differs
    python3 tools/build_fishing_collision.py 2 23       # only these maps
"""
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), 'lib'))
import toolpath  # noqa: F401 — puts every tools/ subfolder on sys.path
import os, struct, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
GAME_DATA = os.path.normpath(os.path.join(HERE, '..', 'game_data'))


def dcfc_bytes(map_no, tris):
    out = bytearray(b'DCFC')
    out += struct.pack('<III', 1, map_no, len(tris))
    for t in tris:
        for v in t:
            out += struct.pack('<fff', float(v[0]), float(v[1]), float(v[2]))
    return bytes(out)


def queens_tris():
    from queens_fishing_collision import fishing_collision_tris
    g = fishing_collision_tris()
    print(f"    bridges {len(g['bridges'])} + pipes {len(g['pipes'])} + contain {len(g['contain'])}")
    return g['all']


def yellowdrops_tris():
    from yellowdrops_westbank_data import westbank_fish_walls
    return westbank_fish_walls()


# (mapNo, town, output path, triangle source)
TOWNS = [
    (2,  'queens',      os.path.join(GAME_DATA, 'queens', 'queens_2.bin'),           queens_tris),
    (23, 'yellowdrops', os.path.join(GAME_DATA, 'yellowdrops', 'yellowdrops_23.bin'), yellowdrops_tris),
]


def main():
    args = sys.argv[1:]
    check = '--check' in args
    only = {int(a) for a in args if a.isdigit()}
    stale = []
    for map_no, town, path, source in TOWNS:
        if only and map_no not in only:
            continue
        print(f'{town} (map {map_no}):')
        blob = dcfc_bytes(map_no, source())
        cur = open(path, 'rb').read() if os.path.exists(path) else None
        rel = os.path.relpath(path, os.path.join(HERE, '..'))
        if blob == cur:
            print(f'    ok      {rel} ({len(blob)} B, {(len(blob) - 16) // 36} tris)')
        elif check:
            stale.append(rel)
            print(f'    STALE   {rel}')
        else:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            open(path, 'wb').write(blob)
            print(f'    WROTE   {rel} ({len(blob)} B, {(len(blob) - 16) // 36} tris)')
    if stale:
        raise SystemExit(f'--check failed: {", ".join(stale)} out of date — run tools/build_fishing_collision.py')


if __name__ == '__main__':
    main()
