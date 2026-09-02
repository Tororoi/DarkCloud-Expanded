#!/usr/bin/env python3
"""Brownboo (s04) collision builder — pure functions over scene.scn BYTES, no ISO I/O. Consumed by the ISO
post-step patch_iso_town_collision.py (which reads the mod's already-patched disc, rebuilds the s04 blocks
through these helpers and redirects the scene into the DATA.DAT tail).

  • `s04g01_a` (PLAYER collision, also the fishing gather's source): bake_rocks() APPENDS the three
    hand-simplified rocks (brownboo_rock_data.ROCKS, authored data) as nodes rock_iwa01/02/03 — vanilla nodes
    byte-identical (append_variant_nodes). Every rock face is a slope, so the fishing floors-only compaction
    keeps them: the bobber can't cast onto/through the rocks and the fish can't swim through them, with no
    runtime append. Gather at the cast rect: 648 + 202 = 850 (<1024).

  • `s04g01_v` (the town CAMERA-collision variant — the slot every other town names `_c`, see LoadMapObject
    @0x19B790): baked_named() gives the flat multi-node MDS node list:
      `v`       — the vanilla terrain camera hull, byte-identical tris (404) — NOT ours to change;
      `c56_*`   — obj56 with ONLY the iwa01 rock replaced by the CSG hull (horn funnels + tunnel),
                  kd-split into <=100-tri nodes. Building cylinders stay VANILLA (the tightened cylinders of
                  2026-08 clipped visibly — camera geometry needs padding; the tight_obj56 code was removed 2026-09,
                  recoverable via git). Same proven pipeline as the Queens e03g04_c bake (build_flat_mds + _replace_a_block),
                  just targeting suffix `_v`.
"""
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', '..', 'lib'))
import toolpath  # noqa: F401 — puts every tools/ subfolder on sys.path
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, ".."))
sys.path.insert(0, os.path.join(HERE, "..", ".."))
os.environ.setdefault("DC1_DATA_DIR", os.path.join(HERE, "..", ".."))
from queens_collision_builder import build_flat_mds, _replace_a_block
from collision_mds_writer import append_variant_nodes
from georama_collision import build_coll_mdt
from brownboo_camera_collision import vanilla_v_nodes, iwa01_ring_obj56
from brownboo_rock_data import ROCKS
from collision_geom import kd_split

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
    named += [(f'c56_{i:02d}', bk) for i, bk in enumerate(kd_split(iwa01_ring_obj56(scn), 100, proportional=True))]
    return named


def rock_nodes():
    """[(node_name, collision MDT bytes)] for the three rocks — one node each (84/81/37 tris)."""
    return [(nm, build_coll_mdt(tris)) for nm, tris in ROCKS]


def bake_rocks(scn):
    """Append the rock nodes to s04g01_a. Refuses to double-bake (a rock node already present)."""
    if b'rock_iwa01' in scn:
        raise SystemExit("s04g01_a already carries the rock nodes (already-baked scene?)")
    return append_variant_nodes(scn, 's04g01', rock_nodes(), suffix='_a')
