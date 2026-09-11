#!/usr/bin/env python3
"""Assemble every EE cave stub (.s) straight into Resources/isoPatch/ (the embedded copies the
C# build consumes). Single source of truth for each stub's assemble base VA — these MUST match
the STUB_VA / write-site constants in IsoPatcher.cs (the per-stub patch methods note their VA).

    python3 tools/stubs/build_ee_stubs.py            # assemble + write all
    python3 tools/stubs/build_ee_stubs.py --check    # assemble only; fail if any output differs from
                                            # the committed Resources copy (CI / pre-commit)

Run after editing any tools/stubs/*.s, then rebuild the app so the EmbeddedResource updates.
"""
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'lib'))
import toolpath  # noqa: F401 — puts every tools/ subfolder on sys.path
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from mips_asm import assemble

DEST = os.path.join(HERE, '..', '..', 'Dark Cloud Improved Version', 'Resources', 'isoPatch')

# (source .s in tools/, dest .bin in Resources/isoPatch/, assemble base VA)
# The 0x1FB0xxx stubs live in the mod's own ELF PT_LOAD segment (the hijacked phdr3 —
# CodeCaveAddresses.ElfCave is the registry; ElfPatches.HijackPhdr3CaveSegment creates it). Their
# bases MUST match the ElfCave constants, and the segment start must stay 16KB-aligned: its host
# pages may hold NO runtime-written data (PINE writes to a compiled page SIGBUS PCSX2 — see the
# ElfCave doc's page-isolation rule). ⚠ The old home 0x228BB0..0x22A210 is LIVE dungeon code
# (CharaChangeLoop/Key/Draw, called from the dun.bin overlay) — never assemble anything there again.
STUBS = [
    ('canal_evict_fade_hook.s',  'canalEvictFadeHook.bin',   0x1FB0000),
    ('queens_spray_cave.s',      'queensSprayCave.bin',      0x1FB0050),
    ('spray_bias_shim.s',        'sprayBiasShim.bin',        0x1FB0150),
    ('cape_early_draw.s',        'capeEarlyDraw.bin',        0x1FB0190),
    ('fishline_split_caves.s',   'fishlineSplitCaves.bin',   0x1FB0210),
    ('fishline_uncast_gate.s',   'fishlineUncastGate.bin',   0x1FB0270),
    ('camera_norm_side.s',       'cameraNormSide.bin',       0x1FB0350),
    ('stilts_heal.s',            'stiltsHeal.bin',           0x1FB0BD0),
    ('water_order_gate.s',       'waterOrderGate.bin',       0x1FB0C50),
    ('cat_pellet_follow.s',      'catPelletFollow.bin',      0x1FB0D90),
    ('town_camera_collision.s',  'townCameraCollision.bin',  0x14B838),
    ('camera_height.s',          'cameraHeight.bin',         0x27D090),
]


def main():
    check = '--check' in sys.argv[1:]
    stale = []
    for src, dest, base in STUBS:
        blob = assemble(open(os.path.join(HERE, src)).read(), base)
        path = os.path.join(DEST, dest)
        cur = open(path, 'rb').read() if os.path.exists(path) else None
        if blob == cur:
            print(f'  ok      {dest} ({len(blob)} B @0x{base:06X})')
        elif check:
            stale.append(dest)
            print(f'  STALE   {dest} (committed {0 if cur is None else len(cur)} B != assembled {len(blob)} B or bytes differ)')
        else:
            open(path, 'wb').write(blob)
            print(f'  WROTE   {dest} ({len(blob)} B @0x{base:06X})')
    if stale:
        raise SystemExit(f'--check failed: {", ".join(stale)} out of date — run tools/stubs/build_ee_stubs.py')


if __name__ == '__main__':
    main()
