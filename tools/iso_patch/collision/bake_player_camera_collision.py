#!/usr/bin/env python3
"""Bake CAMERA + PLAYER collision for a town into its scene.scn ground meshes. Single home for the whole Queens
(e03) collision bake: the hand-authored geometry (BOTH-frame walls / pipe drums / flat quads, player-only
railings + canal containment, the generated perimeter wall — formerly the both_walls / player_walls /
invisible_walls / perimeter_wall modules) AND the orchestration that groups it and splices it into the scene.

Two collision variants per ground sub-file (both origin-placed, so world tris == sub-file-local):
  * PLAYER `_a` (part+0x14 -> frame +0xd0): simplified structure meshes + perimeter + BOTH-frame walls + canal
    invisible walls + railings + the loading-zone trigger quads (attribute-tagged). Split PER ground sub.
  * CAMERA `_c` (part+0x20 -> frame +0xdc): simplified structure meshes + perimeter + BOTH-frame walls ONLY
    (no canal/railings/triggers = player-only). Consolidated onto the one sub that ships a `_c` variant.
Buildings keep their vanilla `_a`/`_c` (buildings=False). grouped_collision() pools each frame's tris and
kd_splits them into <=100-poly, spatially-compact nodes (tight bbox = free runtime gather culling); it is shared
with queens_viewer.py so both write / show the identical grouping.

Shared primitives kept in their own modules (used by other tools too): scene_placed (placement), mdt_codec /
georama_collision (mesh decode), build_coll_mdt (collision-MDT serialiser). The MDS-splice + kd-split helpers
(kd_split / _replace_a_block / _variant_off) live here now. ISO wrapper: iso_patch/bake_structure_collision_iso.py.

  bake_structures(scene_rel, town='e03', max_tris=100) -> (new_scn, stats, manifest)
"""
import os, sys, struct, re, math
_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)                                     # this dir (build_coll_mdt)
sys.path.insert(0, os.path.dirname(os.path.dirname(_HERE)))   # tools/ (scene_placed, mdt_codec, georama_collision…)
from extract_scene_mesh import load_scene, xform
import scene_placed
from scene_placed import placed_meshes
import mdt_codec
from georama_collision import build_coll_mdt
from tri_util import kd_split
import georama_collision as gc


# ==========================================================================
# MDS splice + kd-split primitives (former bake_terrain_camera_collision.py).
# ==========================================================================
_dir = scene_placed.scn_dir   # canonical directory parser (list form)


def _variant_off(sub, name, suffix='_a'):
    """Offset (within `sub`) of the `<name><suffix>.mds` MDS variant block, or None. suffix='_a' = player
    collision, '_c' = camera collision, etc."""
    m = next(re.finditer((re.escape(name) + suffix + r'\.mds\x00').encode(), sub), None)
    if not m:
        return None
    off = struct.unpack_from('<I', sub, m.end() + 3)[0]
    return off if 0 < off < len(sub) and sub[off:off + 3] == b'MDS' else None


def _replace_a_block(scn, sub_name, new_mds, suffix='_a'):
    """Replace `sub_name`'s entire `<name><suffix>` MDS block with new_mds; fix trailing variant offsets and the
    SCN directory. suffix='_a' (player) by default; '_c' rewrites the camera-collision variant. Returns
    (new_scn, delta)."""
    scn = bytearray(scn)
    entry = next((e for e in _dir(scn) if e[0] == sub_name), None)
    if entry is None:
        raise KeyError(sub_name)
    _, sub_off, sub_size, _ = entry
    sub = bytes(scn[sub_off:sub_off + sub_size])
    vo = _variant_off(sub, sub_name, suffix)
    if vo is None:
        raise KeyError(f"{sub_name}: no {suffix}")

    # variant entries (real ones: offset -> 'MDS'); the _a block ends at the next variant after it
    variants = []
    for m in re.finditer(rb'[\w]+\.mds\x00', sub):
        fpos = m.end() + 3
        if fpos + 4 > len(sub):
            continue
        toff = struct.unpack_from('<I', sub, fpos)[0]
        if 0 < toff < sub_size and sub[toff:toff + 3] == b'MDS':
            variants.append((sub_off + fpos, toff))
    after = [t for _, t in variants if t > vo]
    old_size = (min(after) if after else sub_size) - vo

    new_mds = bytearray(new_mds)
    while len(new_mds) % 0x10:
        new_mds.append(0)
    delta = len(new_mds) - old_size

    out = bytearray(scn[:sub_off + vo]) + new_mds + bytearray(scn[sub_off + vo + old_size:])
    # trailing variant offsets shift by delta (positions are before the block, unmoved)
    for pos, toff in variants:
        if toff > vo:
            struct.pack_into('<I', out, pos, toff + delta)
    # SCN directory
    for name, off, size, eoff in _dir(scn):
        if off == sub_off:
            struct.pack_into('<I', out, eoff + 0x14, size + delta)
        elif off > sub_off:
            struct.pack_into('<I', out, eoff + 0x10, off + delta)
    return bytes(out), delta


# ==========================================================================
# Hand-authored Queens (e03) collision geometry (was both_walls.py etc.)
# ==========================================================================
# BOTH-frame walls + flat-ground quads + pipe drums (former both_walls.py):

# town -> [ ((x0,y0,z0), (x1,y1,z1), height), ... ]
_BOTH_WALLS = {
    'e03': [
        # North play-area boundary U. Replaces the deleted raised-border strip + its vertical walls (the ~180
        # terrain tris in simplify_terrain._REMOVE) AND the removed north perimeter (perimeter_wall._REMOVE). Inner
        # edge x=-400 west / z=-1000 north / x=900 east; from the y70 ground up +430 -> y500 (blocks player at the
        # ground and the camera up to the old perimeter height). Open to the south (z>-150) where the player enters.
        ((-400.0, 70.0, -150.0), (-400.0, 70.0, -1000.0), 430.0),   # west   (x=-400)
        ((-400.0, 70.0, -1000.0), (900.0, 70.0, -1000.0), 430.0),   # north  (z=-1000)
        ((900.0, 70.0, -1000.0), (900.0, 70.0, -150.0), 430.0),     # east   (x=900)
        # south wall plane z=1300 (x -400..600) simplified from 60 terrain tris to one quad; y0 up +270 -> y270
        ((-400.0, 0.0, 1300.0), (600.0, 0.0, 1300.0), 270.0),
        # canal side walls (x -200..900), simplified to one quad each; y0 up +70 -> y70:
        # (an 87-degree lean was TRIED here (2026-08) so the bobber's vertical probe would see them, and
        #  REVERTED: the probe's ground-lift semantics just deposited the bobber on the rim. The bobber-vs-wall
        #  fix is native now — IsoPatcher.PatchFishingUncastGate makes the game auto-uncast an out-of-water
        #  bobber quickly instead. These stay plain 90-degree walls.)
        ((-200.0, 0.0, -50.0), (900.0, 0.0, -50.0), 70.0),   # south canal wall z=-50
        ((-200.0, 0.0, 50.0), (900.0, 0.0, 50.0), 70.0),     # north canal wall z=50
        # east wall plane x=600 (z 200..1300) simplified from 66 terrain tris to one quad; y0 up +270 -> y270
        ((600.0, 0.0, 200.0), (600.0, 0.0, 1300.0), 270.0),
        # y370 platform's inner wall (y170->370), L-shape simplified from 80 tris to 2 quads:
        ((650.0, 170.0, 1300.0), (1500.0, 170.0, 1300.0), 200.0),   # south leg z=1300, x 650..1500
        ((1500.0, 170.0, 250.0), (1500.0, 170.0, 1300.0), 200.0),   # east leg  x=1500, z 250..1300
    ],
}


# Explicit hand-adjusted tris (both frames), for cases an edge-extrude can't express. e03: the wall segment ABOVE
# the x=600 quad (y270->~370), with its bottom edge SMOOTHED from y262-271 to y270 to line up with the flat quad
# top (the originals are deleted in simplify_terrain._REMOVE).
_MANUAL_TRIS = {
    'e03': [
        [[600, 270.0, 600], [600, 367.0, 600], [600, 370.0, 500]],
        [[600, 270.0, 600], [600, 370.0, 500], [600, 270.0, 500]],
        [[600, 270.0, 400], [600, 370.0, 400], [600, 366.0, 300]],
        [[600, 270.0, 400], [600, 366.0, 300], [600, 270.0, 300]],
        [[600, 270.0, 300], [600, 366.0, 300], [600, 370.0, 200]],
        [[600, 270.0, 300], [600, 370.0, 200], [600, 270.0, 200]],
        [[600, 270.0, 700], [600, 370.0, 700], [600, 367.0, 600]],
        [[600, 270.0, 700], [600, 367.0, 600], [600, 270.0, 600]],
        [[600, 270.0, 500], [600, 370.0, 500], [600, 370.0, 400]],
        [[600, 270.0, 500], [600, 370.0, 400], [600, 270.0, 400]],
        [[600, 270.0, 800], [600, 370.0, 800], [600, 370.0, 700]],
        [[600, 270.0, 800], [600, 370.0, 700], [600, 270.0, 700]],
        [[600, 270.0, 1000], [600, 376.0, 1000], [600, 376.0, 900]],
        [[600, 270.0, 1000], [600, 376.0, 900], [600, 270.0, 900]],
        [[600, 270.0, 1200], [600, 366.0, 1200], [600, 370.0, 1100]],
        [[600, 270.0, 1200], [600, 370.0, 1100], [600, 270.0, 1100]],
        [[600, 270.0, 900], [600, 376.0, 900], [600, 370.0, 800]],
        [[600, 270.0, 900], [600, 370.0, 800], [600, 270.0, 800]],
        [[600, 270.0, 1100], [600, 370.0, 1100], [600, 376.0, 1000]],
        [[600, 270.0, 1100], [600, 376.0, 1000], [600, 270.0, 1000]],
        [[600, 270.0, 1300], [600, 366.0, 1200], [600, 270.0, 1200]],
        # y370 platform floor (horizontal), L-shape simplified from 48 tris to 2 quads (4 tris):
        [[600, 370.0, 1300], [1600, 370.0, 1300], [1600, 370.0, 1400]],   # south leg x[600,1600] z[1300,1400]
        [[600, 370.0, 1300], [1600, 370.0, 1400], [600, 370.0, 1400]],
        [[1500, 370.0, 100], [1600, 370.0, 100], [1600, 370.0, 1300]],    # east leg x[1500,1600] z[100,1300]
        [[1500, 370.0, 100], [1600, 370.0, 1300], [1500, 370.0, 1300]],
        # east canal-end wall face (x=1400, spans canal z[-50,50]): 3 vertical tris (y89-170) simplified to 2 and
        # extended DOWN to y64 (the floor tri's edge) to close the gap. Originals removed in simplify_terrain._REMOVE.
        [[1400, 64.0, -50], [1400, 64.0, 50], [1400, 170.0, 50]],
        [[1400, 64.0, -50], [1400, 170.0, 50], [1400, 170.0, -50]],
    ],
}


# CAMERA-only authored tris (`_c` frame only; never enter the player `_a`). Wind so (b-a)x(c-a) faces the
# play area — the one-sided camera raycast passes through backfaces. e03: the x=-200 face of the canal
# west-end pocket (y 0..50, z +-50, under the visual cap) — keeps the camera from backing west into the
# capped pocket void when the player wades near the canal's west end. Normal faces +X (the canal).
_CAMERA_TRIS = {
    'e03': [
        [[-200, 0.0, -50], [-200, 50.0, 50], [-200, 0.0, 50]],
        [[-200, 0.0, -50], [-200, 50.0, -50], [-200, 50.0, 50]],
        # CANAL SIDE WALLS, INWARD-FACING (camera-only) — the INSIDE face of the canal walls. The BOTH-frame
        # canal walls (y0..70, above) are wound to face the BANKS (the walking camera's play area), so a fishing
        # camera following a bobber INSIDE the canal sits on their backface and the one-sided ray sails through
        # (confirmed: eye orbits past z=+-50 at dist 80, never pulling in). These duplicate the walls wound the
        # OTHER way so the camera is blocked from inside the channel too.
        # Height = y70 (the canal rim, matching the real wall) — the real fix for the fishing camera is reducing
        # the camera-gather poly count so the ~409-poly buffer stops saturating and the native swept-slide can
        # actually hold these (they ARE gathered, ~24-40 of them, but the full buffer drops the one at the eye's
        # crossing). Camera-only, so no gameplay effect.
        # south wall z=-50, normal +Z (into canal):
        [[-200, 0.0, -50], [900, 0.0, -50], [900, 70.0, -50]],
        [[-200, 0.0, -50], [900, 70.0, -50], [-200, 70.0, -50]],
        # north wall z=+50, normal -Z (into canal):
        [[-200, 0.0, 50], [900, 70.0, 50], [900, 0.0, 50]],
        [[-200, 0.0, 50], [-200, 70.0, 50], [900, 70.0, 50]],
    ],
}


def camera_tris(town='e03'):
    return [[list(p) for p in t] for t in _CAMERA_TRIS.get(town, [])]


# Flat GROUND rectangles (both frames): each (x0,x1,z0,z1,y) REPLACES every flat terrain tri whose footprint
# falls inside it at that y (a corner-set + the ground connecting the corners) with ONE quad. The matching
# removal lives in simplify_terrain (below, via _in_flat_region reading _FLAT_REGIONS). Non-flat tris
# (walls/slopes) inside the footprint are untouched.
_FLAT_REGIONS = {
    'e03': [
        (-400.0, 900.0, -1000.0, -150.0, 70.0),    # y70 ground, x[-400,900] z[-1000,-150]
        (650.0, 1500.0, 250.0, 1300.0, 170.0),     # y170 ground, x[650,1500] z[250,1300]
        (-300.0, 600.0, 300.0, 1300.0, 0.0),       # y0 ground, x[-300,600] z[300,1300]
        # canal-area ground:
        (-400.0, 900.0, 50.0, 200.0, 70.0),        # y70 north bank (top edge steps 150<->200, quad slightly over-covers)
        (-400.0, 900.0, -100.0, -50.0, 70.0),      # y70 south bank
        (-400.0, -200.0, -50.0, 50.0, 70.0),       # y70 west end (canal doesn't reach here)
        (-200.0, 1300.0, -50.0, 50.0, 0.0),        # y0 canal floor
        # west y270 platform + a y0 patch:
        (-500.0, -400.0, 50.0, 500.0, 270.0),      # y270 west platform (z 50..500)
        (-500.0, -400.0, 600.0, 1300.0, 270.0),    # y270 west platform (z 600..1300)
        (-500.0, 100.0, 1300.0, 1400.0, 270.0),    # y270 north strip (z 1300..1400)
        (-400.0, -300.0, 1000.0, 1300.0, 0.0),     # y0 patch (z 1000..1300)
    ],
}


def flat_ground_tris(town='e03'):
    out = []
    for x0, x1, z0, z1, y in _FLAT_REGIONS.get(town, []):
        out.append([[x0, y, z0], [x1, y, z0], [x1, y, z1]])
        out.append([[x0, y, z0], [x1, y, z1], [x0, y, z1]])
    return out


# SOLID octagonal drums (both frames) replacing the hollow obj1/obj9 pipe tubes (which had an extruded inner hole).
# Each tube = an octagon in the X-Y plane (cardinal radius R, diagonal d) extruded along z; the drum is that outer
# octagon, side-walled and end-capped (no hole). obj1/obj9 are dropped from is_cam_node so only the drum remains.
_PIPE_DRUMS = {
    'e03': [   # (center_x, center_y, z0, z1, R, d) — each obj instance is TWO short stub-pipes (NOT a crossing
               # tube): a south stub z[-50,-40] and a north stub z[40,50] at each bank. 3 instances => 6 pipes.
               # Keep the ORIGINAL 10-unit extent (don't span the canal); just the solid octagon, hole removed.
        (198.0, 50.0, -50.0, -40.0, 12.0, 9.0), (198.0, 50.0, 40.0, 50.0, 12.0, 9.0),
        (601.0, 50.0, -50.0, -40.0, 12.0, 9.0), (601.0, 50.0, 40.0, 50.0, 12.0, 9.0),
        (1100.0, 50.0, -50.0, -40.0, 12.0, 9.0), (1100.0, 50.0, 40.0, 50.0, 12.0, 9.0),
    ],
}


def pipe_drum_tris(town='e03'):
    """Each canal pipe stub simplified to its bounding BOX (formerly an 8-sided octagonal drum): the octagon's
    bbox is x[cx-R,cx+R], y[cy-R,cy+R] extruded over z[z0,z1]. Emits 4 side faces + the free-end cap (facing the
    canal); the embedded end (|z|=50, flush in the canal wall) is left open exactly like the drum was. 10 tris."""
    out = []
    for cx, cy, z0, z1, R, d in _PIPE_DRUMS.get(town, []):
        x0, x1, y0, y1 = cx - R, cx + R, cy - R, cy + R
        emb = abs(z0) > abs(z1)                                   # embedded (in-wall) end is the |z|=50 one
        zf = z1 if emb else z0                                    # free (canal-facing) end gets the cap
        out += _dir_quad([x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [x1, y0, z1], [1, 0, 0])    # +x side
        out += _dir_quad([x0, y0, z0], [x0, y1, z0], [x0, y1, z1], [x0, y0, z1], [-1, 0, 0])   # -x side
        out += _dir_quad([x0, y1, z0], [x1, y1, z0], [x1, y1, z1], [x0, y1, z1], [0, 1, 0])    # +y side
        out += _dir_quad([x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1], [0, -1, 0])   # -y side
        out += _dir_quad([x0, y0, zf], [x1, y0, zf], [x1, y1, zf], [x0, y1, zf], [0, 0, 1 if emb else -1])  # free cap
    return out


def both_wall_tris(town='e03'):
    out = []
    for a, b, h in _BOTH_WALLS.get(town, []):
        a0, b0 = list(a), list(b)
        a1 = [a[0], a[1] + h, a[2]]
        b1 = [b[0], b[1] + h, b[2]]
        out.append([a0, b0, b1])
        out.append([a0, b1, a1])
    out += [[list(p) for p in t] for t in _MANUAL_TRIS.get(town, [])]
    out += flat_ground_tris(town)
    out += pipe_drum_tris(town)
    return out

# ==========================================================================
# Player-only railings/containment (former player_walls.py)
# ==========================================================================

# town -> [ ((x0,y0,z0), (x1,y1,z1), height), ... ]   base edge + upward extrude height
# NOTE: the two platform south-edge railings moved to perimeter_wall._EXTRA (both frames, per user); the canal
# containment below is the only genuine player-only invisible wall left here.
_PLAYER_WALLS = {
    'e03': [
        # canal containment: fill the y0->50 gap at x=-200 (z -50..50) between the canal floor edge (y0) and the
        # wall segment above it (y50-70), so a player in the canal can't slip out to the west
        ((-200.0, 0.0, -50.0), (-200.0, 0.0, 50.0), 50.0),
    ],
}


def player_wall_tris(town='e03'):
    out = []
    for a, b, h in _PLAYER_WALLS.get(town, []):
        a0, b0 = list(a), list(b)
        a1 = [a[0], a[1] + h, a[2]]
        b1 = [b[0], b[1] + h, b[2]]
        out.append([a0, b0, b1])
        out.append([a0, b1, a1])
    return out


# ==========================================================================
# Player-only invisible walls: canal containment (former invisible_walls.py)
# ==========================================================================

_E03 = """
0,70,50, 700,70,50, 700,168.25,50
0,70,50, 700,168.25,50, 0,168.25,50
-100,168.25,50, -200,70,50, -100,70,50
-100,168.25,50, -200,168.25,50, -200,70,50
-68,169.98,50, -100,168.25,50, -100,70,50
-68,169.98,50, -100,70,50, -68,70,50
-68.21,76.68,25.02, -68.21,78.9,0, -68.21,178.88,0
-68.21,176.66,-25.02, -68.21,78.9,0, -68.21,76.68,-25.02
-68.21,76.68,25.02, -68.21,178.88,0, -68.21,176.66,25.02
-68.21,176.66,-25.02, -68.21,178.88,0, -68.21,78.9,0
-68.21,176.66,25.02, -68,70,50, -68.21,76.68,25.02
-68.21,176.66,25.02, -68,169.98,50, -68,70,50
-68.21,76.68,-25.02, -68,169.98,-50, -68.21,176.66,-25.02
-68.21,76.68,-25.02, -68,70,-50, -68,169.98,-50
-68,169.98,-50, -68,70,-50, -200,70,-50
-68,169.98,-50, -200,70,-50, -200,170,-50
-200,170,-50, -200,70,-50, -200,70,50
-200,170,-50, -200,70,50, -200,168.25,50
-28,70,50, 0,70,50, 0,168.25,50
-28,70,50, 0,168.25,50, -28,169.98,50
-27.79,176.66,25.02, -27.79,78.9,0, -27.79,76.68,25.02
-27.79,76.68,25.02, -28,169.98,50, -27.79,176.66,25.02
-27.79,76.68,25.02, -28,70,50, -28,169.98,50
-27.79,76.68,-25.02, -27.79,78.9,0, -27.79,178.88,0
-27.79,176.66,25.02, -27.79,178.88,0, -27.79,78.9,0
-27.79,76.68,-25.02, -27.79,178.88,0, -27.79,176.66,-25.02
-27.79,176.66,-25.02, -28,70,-50, -27.79,76.68,-25.02
-27.79,176.66,-25.02, -28,169.98,-50, -28,70,-50
-28,169.98,-50, 200,70,-50, -28,70,-50
-28,169.98,-50, 200,172.16,-50, 200,70,-50
300,70,-50, 200,70,-50, 200,172.16,-50
300,70,-50, 200,172.16,-50, 300,172.16,-50
780,70,-50, 300,70,-50, 300,172.16,-50
780,70,-50, 300,172.16,-50, 780,172,-50
779.79,76.68,-25.02, 780,70,-50, 780,172,-50
779.79,178.68,-25.02, 779.79,78.9,0, 779.79,76.68,-25.02
779.79,76.68,-25.02, 780,172,-50, 779.79,178.68,-25.02
779.79,76.68,25.02, 779.79,180.9,0, 779.79,178.68,25.02
779.79,178.68,-25.02, 779.79,180.9,0, 779.79,78.9,0
779.79,76.68,25.02, 779.79,78.9,0, 779.79,180.9,0
779.79,178.68,25.02, 780,70,50, 779.79,76.68,25.02
779.79,178.68,25.02, 780,172,50, 780,70,50
780,172,50, 700,168.25,50, 700,70,50
780,172,50, 700,70,50, 780,70,50
820.21,178.68,25.02, 820.21,78.9,0, 820.21,76.68,25.02
820.21,76.68,-25.02, 820.21,78.9,0, 820.21,180.9,0
820.21,76.68,-25.02, 820.21,180.9,0, 820.21,178.68,-25.02
820.21,178.68,-25.02, 820,70,-50, 820.21,76.68,-25.02
820.21,178.68,-25.02, 820,172,-50, 820,70,-50
820.21,178.68,25.02, 820.21,180.9,0, 820.21,78.9,0
820.21,76.68,25.02, 820,172,50, 820.21,178.68,25.02
820.21,76.68,25.02, 820,70,50, 820,172,50
820,70,50, 900,168.25,50, 820,172,50
820,70,50, 900,70,50, 900,168.25,50
900,70,50, 1000,168.25,50, 900,168.25,50
900,70,50, 1000,98,50, 1000,168.25,50
1000,98,50, 1100,98,50, 1100,177.19,50
1000,98,50, 1100,177.19,50, 1000,168.25,50
1100,98,50, 1200,210.73,50, 1100,177.19,50
1100,98,50, 1200,128,50, 1200,210.73,50
1300,232.25,50, 1200,128,50, 1300,128,50
1300,232.25,50, 1200,210.73,50, 1200,128,50
1300,232.25,50, 1300,128,50, 1400,170,50
1300,232.25,50, 1400,170,50, 1400,244,50
820,172,-50, 900,70,-50, 820,70,-50
820,172,-50, 900,172.16,-50, 900,70,-50
1000,98,-50, 900,70,-50, 900,172.16,-50
1000,98,-50, 900,172.16,-50, 1000,253.7,-50
1100,98,-50, 1000,98,-50, 1000,253.7,-50
1100,98,-50, 1000,253.7,-50, 1100,253.7,-50
1200,128,-50, 1100,98,-50, 1100,253.7,-50
1200,128,-50, 1100,253.7,-50, 1200,253.7,-50
1200,128,-50, 1300,255.2,-50, 1300,128,-50
1200,128,-50, 1200,253.7,-50, 1300,255.2,-50
1300,128,-50, 1300,255.2,-50, 1400,244,-50
1400,170,50, 1400,244,-50, 1400,244,50
1300,128,-50, 1400,244,-50, 1400,170,-50
1400,170,50, 1400,170,-50, 1400,244,-50
"""

_INVIS_DATA = {'e03': _E03}


def invisible_tris(town='e03', max_height=5.0):
    """Canal-containment collision. max_height caps each wall's vertical extent (top pulled down to base +
    max_height) so the FEET-level player check still hits it but the higher camera clears it — this is why we
    can leave these in the shared collision instead of excluding them from the camera."""
    txt = _INVIS_DATA.get(town, '')
    tris = []
    for line in txt.strip().splitlines():
        line = line.strip()
        if not line:
            continue
        n = [float(x) for x in line.split(',')]
        tris.append([[n[0], n[1], n[2]], [n[3], n[4], n[5]], [n[6], n[7], n[8]]])
    if max_height is not None:
        # The walls' BOTTOM edge already follows the ground contour. Build the ground height per XZ (min y
        # seen at that column = the bottom), then pull every vertex down to ground(XZ) + max_height, so the
        # new top edge follows the same contour 5 units up instead of being flattened.
        def _k(p):
            return (round(p[0], 1), round(p[2], 1))
        ground = {}
        for t in tris:
            for p in t:
                kk = _k(p)
                ground[kk] = min(ground.get(kk, p[1]), p[1])
        tris = [[[p[0], min(p[1], ground[_k(p)] + max_height), p[2]] for p in t] for t in tris]
    return tris

# ==========================================================================
# Perimeter wall — the outer boundary that keeps the camera in town.
# ==========================================================================
# The 7 corner points (XZ) of the simplified outer boundary, finalized in the viewer. Formerly derived at build
# time from a traced reference outline via Douglas-Peucker; frozen to that result, so NO game-mesh vertices are
# retained here — just the authored corners.
_PERIM_CORNERS = {
    'e03': [(-500.0, -1100.0), (-500.0, 1400.0), (1600.0, 1400.0), (1600.0, -100.0),
            (1500.0, -150.0), (1000.0, -150.0), (1000.0, -1100.0)],
}

# Corner-loop walls made REDUNDANT by an inner wall (the north-U in _BOTH_WALLS bounds the camera there now),
# dropped by rounded-int key.
_PREMOVE_E03 = """
1000,500,-150, 1000,170,-1100, 1000,170,-150
1000,500,-150, 1000,500,-1100, 1000,170,-1100
1000,500,-1100, -500,170,-1100, 1000,170,-1100
1000,500,-1100, -500,500,-1100, -500,170,-1100
"""

# Extra hand-authored perimeter segments (BOTH frames) the corner loop doesn't cover: the two platform
# south-edge railings at z=-150 (base EDGE extruded UP; the camera should hug these, so perimeter not canal).
_EXTRA = {
    'e03': [
        ((900.0, 270.0, -150.0), (1000.0, 270.0, -150.0), 130.0),    # east platform south edge (y270 -> 400)
        ((-400.0, 273.0, -150.0), (-500.0, 273.0, -150.0), 130.0),   # west platform south edge (y273 -> 403)
    ],
}


def _pkey(t):
    return tuple(sorted(tuple(round(c) for c in p) for p in t))


_PREMOVE = {'e03': set(_pkey([[float(x) for x in l.split(',')][i:i + 3] for i in (0, 3, 6)])
                       for l in _PREMOVE_E03.strip().split('\n'))}


def perimeter_wall_tris(town='e03', y_bottom=170.0, y_top=500.0):
    """Straight flat wall quads (2 tris each) between the frozen corner points, spanning [y_bottom, y_top],
    plus the hand-authored _EXTRA railings. Both frames. Walls in _PREMOVE (redundant vs an inner wall) dropped."""
    corners = _PERIM_CORNERS.get(town, [])
    walls = []
    n = len(corners)
    for i in range(n):
        a = corners[i]; b = corners[(i + 1) % n]
        At = (a[0], y_top, a[1]); Bt = (b[0], y_top, b[1])
        Ab = (a[0], y_bottom, a[1]); Bb = (b[0], y_bottom, b[1])
        walls.append([list(At), list(Bt), list(Bb)])
        walls.append([list(At), list(Bb), list(Ab)])
    rem = _PREMOVE.get(town, set())
    out = [w for w in walls if _pkey(w) not in rem]
    for a, b, h in _EXTRA.get(town, []):                              # extra railings: base edge extruded up
        a0, b0 = list(a), list(b)
        a1 = [a[0], a[1] + h, a[2]]; b1 = [b[0], b[1] + h, b[2]]
        out.append([a0, b0, b1]); out.append([a0, b1, a1])
    return out


def _num(prefix, n):
    m = re.match(prefix + r'(\d+)', n)
    return int(m.group(1)) if m else None


def is_cam_node(nm):
    # obj42 here = the VISIBLE short-wall mesh (y70-76), NOT the 626-tri collision node named obj42 inside the
    # ground `_a` (that's the town-wide player collision with the tall invisible walls — unrelated, dropped).
    # No 'kanban' — the injected fishing sign isn't a terrain structure and has no ground-style `_a`.
    # obj1/obj9 (the canal pipes) are EXCLUDED here — their hollow tube collision is replaced by solid octagonal
    # drums (both_walls.pipe_drum_tris).
    return nm.startswith('grid3') or _num('obj', nm) in (40, 44, 6, 33, 34, 43, 45, 42)


def _key(t):
    return tuple(sorted(tuple(round(c, 1) for c in p) for p in t))


def _obj42_coll(scn):
    """obj42 short-wall COLLISION tris (world = local), per ground sub-file that contains it."""
    DIR = scene_placed._scndir(scn)
    out = {}
    for g in [n for n in DIR if re.match(r'e03g\d\d$', n)]:
        off, size = DIR[g]; sub = scn[off:off + size]; vo = gc._variant_a(sub, g)
        if vo is None:
            continue
        mds = off + vo; nodes, wm = scene_placed._accum(scn, mds)
        tris = []
        for i, (nn, mo, par, mat) in enumerate(nodes):
            if nn != 'obj42':
                continue
            fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c + 3] == b'MDT'), None)
            if not fo:
                continue
            M = wm(i)
            for a, b, c in gc.parse_coll_mdt(scn, fo):
                tris.append([list(xform(M, a)), list(xform(M, b)), list(xform(M, c))])
        if tris:
            out[g] = tris
    return out


def trigger_nodes(scn):
    """Event-trigger collision quads baked into each ground `_a` (the loading zones): tris whose colour-block
    entry has a non-zero destination tag (the +0x40 short GetEventPoly reads). Returns
    {sub: [(node_name, [tri,...], [colour_entry_16b,...]), ...]}. These MUST survive into the rebuilt `_a`,
    else EdEventPointCpPoly gathers no tagged poly at the event point and the town exit stops working.
    (Queens e03: e03g04 nodes 'map' dest=1 / 'minato' dest=3; e03g05 node 'obj41_2' dest=2.)"""
    DIR = scene_placed._scndir(scn)
    out = {}
    for g in [n for n in DIR if re.match(r'e03g\d\d$', n)]:
        off, size = DIR[g]; sub = scn[off:off + size]; vo = gc._variant_a(sub, g)
        if vo is None:
            continue
        mds = off + vo; nodes, wm = scene_placed._accum(scn, mds)
        found = []
        for ni, (nn, mo, par, mat) in enumerate(nodes):
            if mo == 0:
                continue
            fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c + 3] == b'MDT'), None)
            if not fo:
                continue
            w = struct.unpack_from('<16I', scn, fo)
            POS, DL, COL = w[4], w[10], w[14]
            tc = struct.unpack_from('<I', scn, fo + DL + 0x14)[0]; rb = fo + DL + 0x18
            M = wm(ni)
            tris, ents = [], []
            for t in range(tc):
                i0, i1, i2, ci, _pad = struct.unpack_from('<5i', scn, rb + t * 0x14)
                if not (COL and ci >= 0):
                    continue
                ent = scn[fo + COL + ci * 0x10: fo + COL + ci * 0x10 + 0x10]
                if len(ent) < 0x10 or (struct.unpack_from('<H', ent, 0)[0] == 0):   # +0x40 short 0 = plain surface
                    continue
                def V(i):
                    p = struct.unpack_from('<3f', scn, fo + POS + i * 0x10)
                    return list(xform(M, p))
                tris.append([V(i0), V(i1), V(i2)]); ents.append(ent)
            if tris:
                found.append((nn, tris, ents))
        if found:
            out[g] = found
    return out


def _face_ny(t):
    """|unit face-normal.y| of a triangle. ~1 = horizontal (roof/floor), ~0 = vertical (wall)."""
    (x1, y1, z1), (x2, y2, z2), (x3, y3, z3) = t
    ux, uy, uz = x2 - x1, y2 - y1, z2 - z1
    vx, vy, vz = x3 - x1, y3 - y1, z3 - z1
    ny = uz * vx - ux * vz
    L = math.sqrt((uy * vz - uz * vy) ** 2 + ny * ny + (ux * vy - uy * vx) ** 2)
    return abs(ny) / L if L else 1.0


def _building_lods(scn, off, sub):
    """Absolute offsets of a building sub-file's VISIBLE-mesh LODs, in decreasing detail. The sub-file leads
    with the LOD chain (MDS#0 full .. MDS#k coarsest) before the collision/shadow blocks; take the leading run
    of MDS blocks that actually decode to triangles."""
    lods = []
    for m in re.finditer(b'MDS\x00', sub):
        mds = off + m.start()
        nodes, wm = scene_placed._accum(scn, mds)
        tc = 0
        for i, (nn, mo, par, mat) in enumerate(nodes):
            if mo == 0:
                continue
            fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c + 3] == b'MDT'), None)
            if fo:
                try:
                    tc += len(scene_placed._flatten(mdt_codec.parse_mdt(scn, fo)))
                except Exception:
                    pass
        if tc == 0:
            break                                    # end of the LOD chain (shadow/collision blocks follow)
        lods.append(mds)
    return lods


def building_collision_nodes(scn, max_tris=100, wall_max_ny=None, lod=2):
    """Per e03h* building sub-file: a coarse LOD of its VISIBLE mesh decoded in BUILDING-LOCAL space (node
    matrices only, NO mapinfo placement — buildings are georama PARTS, transformed at runtime), kd-split into
    <=max_tris nodes. Returns {sub: [(node_name, local_tris), ...]}. The georama part placement moves these at
    runtime exactly like the native multi-node building `_a` (e03h01 ships 6: obj7/car2/car1/car3/grid43/lt1).
    lod picks the LOD (0=full detail .. clamped to the coarsest available); the town-load memory pool
    (CDataAlloc2 @0x1d3a050, holds meshes+collision, hangs on overflow) is TIGHT, and the full mesh (LOD0, ~17.5k
    tris → ~2.6MB of pool) overflows it — a coarse LOD stays COMPLETE (same bbox, roofs walkable) at ~1/3 the
    tris. The whole (coarse) mesh is kept — several Queens buildings have WALKABLE roofs, so dropping horizontal
    faces left holes. Optional wall_max_ny (0..1) additionally keeps only |face-normal.y| <= it — off by default."""
    DIR = scene_placed._scndir(scn)
    out = {}
    for g in sorted(n for n in DIR if re.match(r'e03h\d\d$', n)):
        off, size = DIR[g]; sub = scn[off:off + size]
        if gc._variant_a(sub, g) is None:            # only buildings that ship an `_a` (placed + collidable)
            continue
        lods = _building_lods(scn, off, sub)
        if not lods:
            continue
        mds = lods[min(lod, len(lods) - 1)]          # coarsest available at/below the requested LOD
        nodes, wm = scene_placed._accum(scn, mds)
        tris = []
        for i, (nn, mo, par, mat) in enumerate(nodes):
            if mo == 0:
                continue
            fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c + 3] == b'MDT'), None)
            if not fo:
                continue
            try:
                m = mdt_codec.parse_mdt(scn, fo)     # strict visible-mesh decode (same as placed_meshes)
            except Exception:
                continue
            M = wm(i)
            lv = [list(xform(M, (p[0], p[1], p[2]))) for p in m.pos]   # BUILDING-LOCAL (node matrix only)
            for a, b, c in scene_placed._flatten(m):
                tris.append([lv[a], lv[b], lv[c]])
        if wall_max_ny is not None:
            tris = [t for t in tris if _face_ny(t) <= wall_max_ny]
        if not tris:
            continue
        stem = g[3:]                                  # 'e03h01' -> 'h01' (short, unique node-name base)
        named = [(f'{stem}w{bi}', bk) for bi, bk in enumerate(kd_split(tris, max_tris))]
        out[g] = named
    return out


def _unique_names(names):
    cnt = {}
    for n in names:
        cnt[n] = cnt.get(n, 0) + 1
    occ, out = {}, []
    for n in names:
        if cnt[n] > 1:
            k = occ.get(n, 0); occ[n] = k + 1
            out.append(f'{n}_{k}')
        else:
            out.append(n)
    return out


def _fit(name, used, maxlen=15):
    cand = name[:maxlen]; k = 0
    while cand in used:
        k += 1; suf = '~' + str(k); cand = name[:maxlen - len(suf)] + suf
    used.add(cand)
    return cand


def build_flat_mds(named):
    """named: [(node_name, [tri,...]) | (node_name, [tri,...], [colour_entry_16b,...]), ...]. Build a flat `_a`
    (node 0 root, rest its children). Camera and player both gather the whole thing — the 5-unit canal walls
    clear the camera by height, so no camera/player split is needed. A 3-tuple carries per-triangle colour-block
    attributes (the event-trigger tags) through build_coll_mdt so loading zones keep their destination."""
    n = len(named)
    header = struct.pack('<4sIII', b'MDS\x00', 1, n, 0x10)
    table = bytearray(); blob = bytearray()
    cur = 0x10 + n * 0x70
    for i, entry in enumerate(named):
        nm, t = entry[0], entry[1]
        attrs = entry[2] if len(entry) > 2 else None
        node = bytearray(0x70)
        struct.pack_into('<II', node, 0, 0, 0x70)
        b = nm.encode('latin1', 'replace')[:15]
        node[8:8 + len(b)] = b
        mdt = build_coll_mdt(t, attrs=attrs)
        struct.pack_into('<i', node, 0x28, cur)
        blob += mdt; cur += len(mdt)
        struct.pack_into('<i', node, 0x2c, -1 if i == 0 else 0)
        struct.pack_into('<16f', node, 0x30, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)
        table += node
    return header + bytes(table) + bytes(blob)


# ==========================================================================
# Terrain simplification — drop the game's structure-collision tris that a
# hand-authored wall/quad above now stands in for (so they're never emitted).
# ==========================================================================
# Removal REGIONS (authored bounds), replacing a former 528-entry list of exact game vertices. Each region drops
# the collision of a structure a hand-authored piece replaces. Where a box would also cover KEPT geometry (a floor
# sharing the footprint), tris are matched by their own PLANE (all verts coplanar) or restricted to horizontal
# faces, so a broad box never deletes a neighbour. Verified to remove EXACTLY the old set — only 6 irregular
# corner/scatter tris need the exact residue below.
def _box(t, x0, x1, y0, y1, z0, z1, e=0.5):
    return all(x0 - e <= p[0] <= x1 + e and y0 - e <= p[1] <= y1 + e and z0 - e <= p[2] <= z1 + e for p in t)


def _plane_x(t, xv, z0, z1, y0, y1, e=0.5):        # tri coplanar with x=xv, within a z/y window
    return all(abs(p[0] - xv) < e and z0 - e <= p[2] <= z1 + e and y0 - e <= p[1] <= y1 + e for p in t)


def _plane_z(t, zv, x0, x1, y0, y1, e=0.5):        # tri coplanar with z=zv, within an x/y window
    return all(abs(p[2] - zv) < e and x0 - e <= p[0] <= x1 + e and y0 - e <= p[1] <= y1 + e for p in t)


def _horiz(t):                                     # near-horizontal face (a floor/top; |normal.y| > 0.7)
    e1 = [t[1][i] - t[0][i] for i in range(3)]; e2 = [t[2][i] - t[0][i] for i in range(3)]
    n = [e1[1]*e2[2] - e1[2]*e2[1], e1[2]*e2[0] - e1[0]*e2[2], e1[0]*e2[1] - e1[1]*e2[0]]
    return abs(n[1]) > 0.7 * (math.hypot(*n) or 1.0)


def _in_remove_region(t):
    """True if t is structure collision the bake drops because a hand-authored wall/quad replaces it (e03)."""
    return (
        _plane_z(t, 1300, -400, 600, 0, 270)         # south wall plane      -> _BOTH_WALLS quad
        or _plane_x(t, 600, 200, 1300, 0, 272)       # east wall plane        -> _BOTH_WALLS quad
        or _plane_x(t, 600, 200, 1250, 262, 376)     # wall segment above it  -> smoothed _MANUAL_TRIS
        or _box(t, -500, -400, 70, 170, -900, -200)  # west boundary strip, now behind the north-U
        or _plane_x(t, -400, -1000, -150, 70, 276)   # north-U border wall, west leg  -> _BOTH_WALLS U
        or _plane_x(t, 900, -1000, -150, 70, 276)    # north-U border wall, east leg
        or _plane_z(t, -1000, -400, 900, 70, 276)    # north-U border wall, north leg
        or (_box(t, -500, 1000, 260, 278, -1100, -1000) and _horiz(t))  # north-U raised top, north leg
        or (_box(t, -500, -350, 260, 278, -1000, -150) and _horiz(t))   # north-U raised top, west leg
        or (_box(t, 850, 1000, 260, 278, -1000, -150) and _horiz(t))    # north-U raised top, east leg
        or _plane_x(t, 1500, 250, 1300, 170, 370)    # y170-370 platform inner wall, east leg -> _BOTH_WALLS
        or _plane_z(t, 1300, 650, 1500, 170, 370)    # y170-370 platform inner wall, south leg
        or _box(t, 650, 1600, 370, 370, 100, 1400)   # y370 platform floor    -> _MANUAL_TRIS quads
        or _box(t, 1400, 1600, 64, 89, -50, 50)      # east canal wall (part is also east of x=1600)
        or _plane_x(t, 1400, -50, 50, 89, 170)       # east canal-end wall face -> _BOTH_WALLS
    )


def _rmkey(t):
    return tuple(sorted(tuple(round(c) for c in p) for p in t))


# The handful of irregular corner/scatter tris no clean region captures without also deleting a kept neighbour.
_REMOVE_RESIDUE_E03 = """
600,270,1300, 600,366,1200, 600,262,1200
650,370,1300, 600,370,1300, 600,370,1400
650,370,1300, 600,370,1400, 650,370,1400
-400,270,150, -400,267,50, -500,270,50
-200,50,50, -600,0,50, -600,50,50
-200,50,50, -200,0,50, -600,0,50
"""

_RESIDUE = set(_rmkey([[float(x) for x in ln.split(',')][i:i + 3] for i in (0, 3, 6)])
               for ln in _REMOVE_RESIDUE_E03.strip().split('\n') if ln.strip())


# ── One-sided CAMERA collision: these baked camera-wall tris are wound so their normal faces AWAY from the play
#    area. Under the backface-culled player→camera raycast (the mod's camera reads `_c` one-sided now) that lets the
#    ray pass straight through them instead of being blocked. Flip their winding so the normal faces the play area.
#    Applied to the CAMERA path + viewer sets only; player `_a` collision is two-sided (CheckHit) so it's unchanged.
_BACKFACE_E03 = """
-400,70,-100, 900,70,-50, -400,70,-50
-400,70,-100, 900,70,-100, 900,70,-50
-400,70,-50, -200,70,-50, -200,70,50
-400,70,-50, -200,70,50, -400,70,50
-400,70,50, 900,70,200, -400,70,200
-400,70,50, 900,70,50, 900,70,200
-200,0,-50, 1300,0,50, -200,0,50
-200,0,-50, 1300,0,-50, 1300,0,50
-200,0,50, 900,70,50, -200,70,50
-200,0,50, 900,0,50, 900,70,50
-500,270,50, -400,270,50, -400,270,500
-500,270,50, -400,270,500, -500,270,500
-500,270,600, -400,270,600, -400,270,1300
-500,270,600, -400,270,1300, -500,270,1300
-500,270,1300, 100,270,1400, -500,270,1400
-500,270,1300, 100,270,1300, 100,270,1400
600,370,1300, 1600,370,1400, 600,370,1400
600,370,1300, 1600,370,1300, 1600,370,1400
1500,370,100, 1600,370,1300, 1500,370,1300
1500,370,100, 1600,370,100, 1600,370,1300
-400,0,1300, 600,270,1300, -400,270,1300
-400,0,1300, 600,0,1300, 600,270,1300
650,170,1300, 1500,370,1300, 650,370,1300
650,170,1300, 1500,170,1300, 1500,370,1300
-300,0,300, 600,0,300, 600,0,1300
-300,0,300, 600,0,1300, -300,0,1300
650,170,250, 1500,170,1300, 650,170,1300
650,170,250, 1500,170,250, 1500,170,1300
-400,70,-1000, 900,70,-1000, 900,70,-150
-400,70,-1000, 900,70,-150, -400,70,-150
-400,0,1000, -300,0,1300, -400,0,1300
-400,0,1000, -300,0,1000, -300,0,1300
"""


def _wkey(t):
    """Winding-PRESERVING key: min cyclic rotation of the 3 rounded vertices. Reversing a tri's winding yields a
    DIFFERENT key, so a flip touches only tris matching the known-backwards winding — never their correct twin."""
    v = [tuple(round(c) for c in p) for p in t]
    return min((v[0], v[1], v[2]), (v[1], v[2], v[0]), (v[2], v[0], v[1]))


_BACKFACE_KEYS = {_wkey([[float(x) for x in ln.split(',')][i:i + 3] for i in (0, 3, 6)])
                  for ln in _BACKFACE_E03.strip().split('\n') if ln.strip()}


def fix_camera_winding(tris):
    """Reverse the winding of any tri whose winding matches a known-backwards camera wall (normal faces the play
    area afterward). Returns a new list; non-matching tris pass through unchanged."""
    return [[t[0], t[2], t[1]] if _wkey(t) in _BACKFACE_KEYS else t for t in tris]


def _tnormal(t):
    a, b, c = t
    u = [b[i] - a[i] for i in range(3)]
    v = [c[i] - a[i] for i in range(3)]
    return [u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]]


def _dot3(a, b):
    return a[0]*b[0] + a[1]*b[1] + a[2]*b[2]


def _cross3(a, b):
    return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]


def _unit3(a):
    L = math.sqrt(_dot3(a, a)) or 1.0
    return [c / L for c in a]


def _pt_in_tri2(px, py, tt):
    (x0, y0), (x1, y1), (x2, y2) = tt
    d1 = (px-x1)*(y0-y1) - (x0-x1)*(py-y1)
    d2 = (px-x2)*(y1-y2) - (x1-x2)*(py-y2)
    d3 = (px-x0)*(y2-y0) - (x2-x0)*(py-y0)
    return not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0))


def _greedy_rects(cov):
    """Cover the True cells of a boolean grid with a small set of maximal rectangles (extend right, then down).
    Preserves holes: an uncovered cell (e.g. a doorway) is never spanned. Not provably minimal but near it."""
    nx, ny = len(cov), len(cov[0])
    used = [[False]*ny for _ in range(nx)]
    out = []
    for i in range(nx):
        for j in range(ny):
            if not cov[i][j] or used[i][j]:
                continue
            i1 = i
            while i1+1 < nx and cov[i1+1][j] and not used[i1+1][j]:
                i1 += 1
            j1 = j
            while j1+1 < ny and all(cov[ii][j1+1] and not used[ii][j1+1] for ii in range(i, i1+1)):
                j1 += 1
            for ii in range(i, i1+1):
                for jj in range(j, j1+1):
                    used[ii][jj] = True
            out.append((i, j, i1, j1))
    return out


def _plane_quad(u, v, nn, d, a0, b0, a1, b1):
    """Reconstruct a rectangle [a0,a1]x[b0,b1] of the plane {p : p.nn == d} back to 3D via the orthonormal
    in-plane basis (u,v): p = a*u + b*v + d*nn. Two tris, wound so the face normal matches nn."""
    def P(a, b):
        return [a*u[i] + b*v[i] + d*nn[i] for i in range(3)]
    A, B, C, D = P(a0, b0), P(a1, b0), P(a1, b1), P(a0, b1)
    t1, t2 = [A, B, C], [A, C, D]
    if _dot3(_tnormal(t1), nn) < 0:                 # keep the group's one-sided facing
        t1, t2 = [A, C, B], [A, D, C]
    return [t1, t2]


def simplify_coplanar(tris, snap=5.0, outward=0.0, top=None, keep_windows=False):
    """Merge coplanar tris (of ANY plane orientation — vertical facades, flat tops, sloped roofs/banks) into
    minimal rectangles, preserving holes (doorways etc.). Camera-only decimation: a tessellated planar face made
    of many small tris collapses to a few big quads, which is what keeps the runtime camera-gather buffer (~409
    poly cap) from saturating over the canal. Each plane is worked in its own orthonormal in-plane basis (u,v);
    2D coords snap to a `snap`-unit grid so faint tessellation slants fuse — harmless for camera collision.

    For a VERTICAL facade (in-plane up-axis v ~ world-up) each occupied column is extended UP to the group's
    tallest point, so a jagged per-structure roofline becomes one flat top — the "extend the walls to full length"
    step. Only fills UPWARD from each column's lowest wall cell, so ground-level doorway gaps survive; columns with
    no wall at all (true gaps between separate structures) stay open. Returns the new tri list."""
    def sn(x):
        return round(x / snap) * snap
    groups, out = {}, []
    for t in tris:
        n = _tnormal(t)
        if math.sqrt(_dot3(n, n)) < 1e-9:
            out.append(t); continue                 # degenerate — leave it
        nn = _unit3(n)
        d = _dot3(nn, t[0])
        # Coarse plane key (normal to 0.1, offset to the snap grid): the source facades are slightly non-planar
        # (verts wobble ~0.5u, so per-tri normals wobble a few degrees). A tight key splits one wall into many
        # singleton "planes" that never merge; 0.1 fuses the wobble into one plane. Reconstruction rides the
        # group's representative plane, flattening the <1u wobble — invisible to camera collision.
        key = (tuple(round(c, 1) + 0.0 for c in nn), round(d / snap) * snap)
        groups.setdefault(key, (nn, d, []))[2].append(t)
    for (nn, d, g) in groups.values():
        if len(g) < 2:
            out.extend(g); continue
        # Author the merged plane OUTWARD (behind the visual mesh): nn faces the play area, so the vertex furthest
        # opposite nn is the outermost source point. Place the plane there (minus `outward` margin) so the merged
        # camera wall never protrudes in FRONT of the rendered wall — the camera can ride flush to the visual mesh
        # without the collision clipping it early. Costs a hair of depth behind the wall, invisible to the camera.
        d = min(_dot3(nn, p) for t in g for p in t) - outward
        # In-plane basis derived from world-up: v = steepest-ascent direction (world-up projected into the plane),
        # u = horizontal in-plane. So v is the up-axis for EVERY vertical facade (flatten works regardless of which
        # way the wall faces), and the basis lines up with the natural horizontal-rows/up-columns tessellation of
        # facades and sloped roofs alike. Degenerate only for a (near-)horizontal plane, where there's no up-in-
        # plane — fall back to a world-x basis and skip flatten (floors/ceilings have no roofline).
        vp = [-_dot3([0.0, 1.0, 0.0], nn) * nn[i] for i in range(3)]
        vp[1] += 1.0                                  # world-up minus its out-of-plane component
        if _dot3(vp, vp) < 1e-6:
            u = _unit3([1.0 - nn[0]*nn[0], -nn[0]*nn[1], -nn[0]*nn[2]])
            v = _cross3(nn, u)
        else:
            v = _unit3(vp)
            u = _unit3(_cross3(v, nn))
        tri2d = [[(sn(_dot3(p, u)), sn(_dot3(p, v))) for p in t] for t in g]
        us = sorted({p[0] for tt in tri2d for p in tt})
        vs = sorted({p[1] for tt in tri2d for p in tt})
        if len(us) < 2 or len(vs) < 2:
            out.extend(g); continue
        cov = [[any(_pt_in_tri2((us[i]+us[i+1])/2, (vs[j]+vs[j+1])/2, tt) for tt in tri2d)
                for j in range(len(vs)-1)] for i in range(len(us)-1)]
        if v[1] > 0.9:                               # VERTICAL facade — flatten the roofline to the group max
            for col in cov:
                occ = [j for j, c in enumerate(col) if c]
                if occ:
                    # keep_windows: fill only from the HIGHEST wall cell up — flattens crenellations but leaves
                    # every mid-wall opening (arcade arches / windows) intact. Default fills from the lowest cell
                    # up (only ground doorways survive), which is right for solid facades.
                    for j in range(max(occ) if keep_windows else min(occ), len(col)):
                        col[j] = True
        new = []
        for (i0, j0, i1, j1) in _greedy_rects(cov):
            b1 = vs[j1 + 1]
            if top is not None and v[1] > 0.9 and abs(b1 - vs[-1]) < 1e-6:
                b1 = top                              # force a VERTICAL facade's flattened top to a shared world-y
            new += _plane_quad(u, v, nn, d, us[i0], vs[j0], us[i1 + 1], b1)
        out.extend(new if len(new) < len(g) else g)
    return out


# ── DIRECTED camera-mesh simplification ────────────────────────────────────────────────────────────────────
# Each job selects a subset of camera `_c` tris (by axis-aligned world box + optional plane-facing test) and runs
# simplify_coplanar over ONLY that subset, authored OUTWARD so the merged wall sits behind the visual mesh. Jobs
# are added one at a time as they're reviewed — nothing merges until it's listed here. `sel` is a predicate on a
# tri; `outward`/`snap` tune the merge for that group.
def _plane_region(axis, off, x=None, y=None, z=None):
    """Selector: tris lying on the plane {coord[axis] == off} (all verts within 2u) whose centroid falls in the
    optional x/y/z ranges. Used to target one specific wall for a directed merge."""
    def sel(t):
        if not all(abs(p[axis] - off) < 2 for p in t):
            return False
        c = [(t[0][i] + t[1][i] + t[2][i]) / 3.0 for i in range(3)]
        for i, rng in ((0, x), (1, y), (2, z)):
            if rng and not (rng[0] <= c[i] <= rng[1]):
                return False
        return True
    return sel


# The 72 gatehouse tris to simplify (pylon facades z=-86.76/-165.24 + flanking posts, both sides of the passage).
# Matched by exact key so we DON'T touch the passage lintel / bore walls / floor (kept, so the doorway hole stays).
_E03_GATE_RAW = """194.21,102.21,-80.55,185.79,69.87,-80.55,194.21,69.87,-80.55
194.21,102.21,-87.01,194.21,69.87,-80.55,194.21,69.87,-87.01
194.21,102.21,-87.01,194.21,102.21,-80.55,194.21,69.87,-80.55
194.21,102.21,-80.55,185.79,102.21,-80.55,185.79,69.87,-80.55
185.79,102.21,-80.55,185.79,69.87,-87.01,185.79,69.87,-80.55
185.79,102.21,-80.55,185.79,102.21,-87.01,185.79,69.87,-87.01
185.79,102.21,-87.01,185.79,102.21,-80.55,182.83,109.13,-74.49
185.79,102.21,-87.01,182.83,109.13,-74.49,182.83,109.13,-87.01
185.79,102.21,-80.55,194.21,102.21,-80.55,197.17,109.13,-74.49
185.79,102.21,-80.55,197.17,109.13,-74.49,182.83,109.13,-74.49
194.21,102.21,-80.55,194.21,102.21,-87.01,197.17,109.13,-87.01
194.21,102.21,-80.55,197.17,109.13,-87.01,197.17,109.13,-74.49
314.21,102.21,-87.01,314.21,69.87,-80.55,314.21,69.87,-87.01
314.21,102.21,-87.01,314.21,102.21,-80.55,314.21,69.87,-80.55
314.21,102.21,-80.55,305.79,102.21,-80.55,305.79,69.87,-80.55
305.79,102.21,-80.55,305.79,69.87,-87.01,305.79,69.87,-80.55
314.21,102.21,-80.55,305.79,69.87,-80.55,314.21,69.87,-80.55
305.79,102.21,-80.55,305.79,102.21,-87.01,305.79,69.87,-87.01
314.21,102.21,-80.55,314.21,102.21,-87.01,317.17,109.13,-87.01
314.21,102.21,-80.55,317.17,109.13,-87.01,317.17,109.13,-74.49
305.79,102.21,-87.01,302.83,109.13,-74.49,302.83,109.13,-87.01
305.79,102.21,-87.01,305.79,102.21,-80.55,302.83,109.13,-74.49
305.79,102.21,-80.55,317.17,109.13,-74.49,302.83,109.13,-74.49
305.79,102.21,-80.55,314.21,102.21,-80.55,317.17,109.13,-74.49
350.01,170,-86.76,298,170,-86.76,298,60,-86.76
350.01,170,-86.76,298,60,-86.76,350.01,60,-86.76
350.01,170,-86.76,350.01,270,-86.76,298,170,-86.76
298,170,-86.76,350.01,270,-86.76,300,270,-86.76
201.99,170,-86.76,149.99,170,-86.76,149.99,60,-86.76
201.99,170,-86.76,149.99,60,-86.76,201.99,60,-86.76
199.99,270,-86.76,149.99,170,-86.76,201.99,170,-86.76
199.99,270,-86.76,149.99,270,-86.76,149.99,170,-86.76
350.01,170,-165.24,350.01,170,-86.76,350.01,60,-86.76
350.01,170,-86.76,350.01,270,-165.24,350.01,270,-86.76
350.01,170,-86.76,350.01,170,-165.24,350.01,270,-165.24
350.01,170,-165.24,350.01,60,-86.76,350.01,60,-165.24
298,170,-165.24,350.01,60,-165.24,298,60,-165.24
298,170,-165.24,350.01,170,-165.24,350.01,60,-165.24
300,270,-165.24,350.01,170,-165.24,298,170,-165.24
300,270,-165.24,350.01,270,-165.24,350.01,170,-165.24
149.99,170,-165.24,201.99,170,-165.24,201.99,60,-165.24
149.99,170,-165.24,149.99,270,-165.24,201.99,170,-165.24
201.99,170,-165.24,149.99,270,-165.24,199.99,270,-165.24
149.99,170,-86.76,149.99,170,-165.24,149.99,60,-165.24
149.99,170,-165.24,149.99,170,-86.76,149.99,270,-86.76
149.99,170,-165.24,149.99,270,-86.76,149.99,270,-165.24
149.99,170,-86.76,149.99,60,-165.24,149.99,60,-86.76
305.79,102.21,-171.37,302.83,109.13,-164.91,302.83,109.13,-177.43
305.79,102.21,-171.37,305.79,102.21,-164.91,302.83,109.13,-164.91
314.21,102.21,-164.91,317.17,109.13,-177.43,317.17,109.13,-164.91
314.21,102.21,-164.91,314.21,102.21,-171.37,317.17,109.13,-177.43
305.79,102.21,-164.91,305.79,102.21,-171.37,305.79,69.87,-171.37
305.79,102.21,-171.37,314.21,102.21,-171.37,314.21,69.87,-171.37
314.21,102.21,-171.37,314.21,102.21,-164.91,314.21,69.87,-164.91
314.21,102.21,-171.37,314.21,69.87,-164.91,314.21,69.87,-171.37
305.79,102.21,-171.37,314.21,69.87,-171.37,305.79,69.87,-171.37
314.21,102.21,-171.37,305.79,102.21,-171.37,302.83,109.13,-177.43
314.21,102.21,-171.37,302.83,109.13,-177.43,317.17,109.13,-177.43
194.21,102.21,-171.37,194.21,69.87,-164.91,194.21,69.87,-171.37
194.21,102.21,-171.37,194.21,102.21,-164.91,194.21,69.87,-164.91
185.79,102.21,-171.37,182.83,109.13,-164.91,182.83,109.13,-177.43
185.79,102.21,-171.37,185.79,102.21,-164.91,182.83,109.13,-164.91
194.21,102.21,-171.37,185.79,102.21,-171.37,182.83,109.13,-177.43
194.21,102.21,-171.37,182.83,109.13,-177.43,197.17,109.13,-177.43
185.79,102.21,-171.37,194.21,69.87,-171.37,185.79,69.87,-171.37
185.79,102.21,-171.37,194.21,102.21,-171.37,194.21,69.87,-171.37
194.21,102.21,-164.91,197.17,109.13,-177.43,197.17,109.13,-164.91
194.21,102.21,-164.91,194.21,102.21,-171.37,197.17,109.13,-177.43
185.79,102.21,-164.91,185.79,102.21,-171.37,185.79,69.87,-171.37
185.79,102.21,-164.91,185.79,69.87,-171.37,185.79,69.87,-164.91
149.99,170,-165.24,201.99,60,-165.24,149.99,60,-165.24
305.79,102.21,-164.91,305.79,69.87,-171.37,305.79,69.87,-164.91"""
_E03_GATE_KEYS = set(_rmkey([[float(x) for x in ln.split(',')][i:i+3] for i in (0, 3, 6)])
                     for ln in _E03_GATE_RAW.strip().split('\n'))


def _e03_gate_sel(t):
    return _rmkey(t) in _E03_GATE_KEYS


def _dir_quad(a, b, c, d, want):
    """Quad (a,b,c,d) as two tris, wound so its face normal points the same way as `want`."""
    tt = [[a, b, c], [a, c, d]]
    return tt if _dot3(_tnormal(tt[0]), want) >= 0 else [[a, c, b], [a, d, c]]


def _e03_gate_tris():
    """Replacement for the gatehouse pylons: two CLOSED bumps (left x[150,201.99], right x[298,350]) flanking the
    passage. Each covers its front/back protrusion (out to z=-73 / -179, in front of / behind every facade+post+cap)
    with a LONG outer ramp (foot 70u along the wall at the z=-100/-150 plane -> gentle slope) up to a flat face, so
    there are no sharp perpendicular sides = no concave corner for the camera to catch. Closed into a solid by a
    passage-side jamb wall (coplanar with the kept bore walls x=201.99/298) and top strips at y=280 that meet the
    cap's front/back edges. Passage (x[202,298]) itself is untouched, so the doorway hole stays."""
    Y0, Y1, ZF, ZB, ZWF, ZWB, RAMP = 70.0, 280.0, -73.0, -179.0, -100.0, -150.0, 70.0

    def vquad(x0, z0, x1, z1, want):                              # vertical quad (x0,z0)->(x1,z1), y[Y0,Y1]
        return _dir_quad([x0, Y0, z0], [x1, Y0, z1], [x1, Y1, z1], [x0, Y1, z0], want)

    def hfan(pts, want):                                         # horizontal face at y=Y1, fan-triangulated
        out = []
        for i in range(1, len(pts) - 1):
            tt = [pts[0], pts[i], pts[i + 1]]
            out.append(tt if _dot3(_tnormal(tt), want) >= 0 else [pts[0], pts[i + 1], pts[i]])
        return out

    out = []
    for x_outer, x_pass in ((150.0, 201.99), (350.0, 298.0)):
        xr = x_outer + (RAMP if x_outer > x_pass else -RAMP)       # ramp foot, out along the wall past the pylon
        px = [1, 0, 0] if x_outer < x_pass else [-1, 0, 0]         # passage-side outward normal
        out += vquad(xr, ZWF, x_outer, ZF, [0, 0, 1])             # FRONT outer ramp (wall -> bump)
        out += vquad(x_outer, ZF, x_pass, ZF, [0, 0, 1])          # FRONT flat face
        out += vquad(xr, ZWB, x_outer, ZB, [0, 0, -1])            # BACK outer ramp
        out += vquad(x_outer, ZB, x_pass, ZB, [0, 0, -1])         # BACK flat face
        out += vquad(x_pass, ZF, x_pass, ZB, px)                  # PASSAGE-side jamb (meets kept bore wall)
        out += hfan([[xr, Y1, ZWF], [x_outer, Y1, ZF], [x_pass, Y1, ZF], [x_pass, Y1, ZWF]], [0, 1, 0])  # front top
        out += hfan([[xr, Y1, ZWB], [x_outer, Y1, ZB], [x_pass, Y1, ZB], [x_pass, Y1, ZWB]], [0, 1, 0])  # back top
    return out


# The doorway pediment/gable + the now-redundant bore-wall jambs (the pylon replacement already carries jambs).
# Matched by key; replaced by a clean gable prism moved onto the pylons' faces (front z=-73, back z=-179).
_E03_ARCH_RAW = """201.99,170,-165.24,201.99,60,-86.76,201.99,60,-165.24
201.99,170,-165.24,201.99,170,-86.76,201.99,60,-86.76
298,170,-86.76,298,60,-165.24,298,60,-86.76
298,170,-86.76,298,170,-165.24,298,60,-165.24
201.99,170,-86.76,250,270,-86.76,199.99,270,-86.76
250,170,-86.76,250,270,-86.76,201.99,170,-86.76
250,170,-86.76,300,270,-86.76,250,270,-86.76
298,170,-86.76,300,270,-86.76,250,170,-86.76
298,170,-165.24,250,270,-165.25,300,270,-165.24
250,170,-165.24,250,270,-165.25,298,170,-165.24
201.99,170,-165.24,199.99,270,-165.24,250,170,-165.24
250,170,-165.24,199.99,270,-165.24,250,270,-165.25
149.99,270,-165.24,250,325,-165.24,250,270,-165.25
250,270,-165.25,250,325,-165.24,350.01,270,-165.24
250,270,-86.76,250,325,-86.76,149.99,270,-86.76
350.01,270,-86.76,250,325,-86.76,250,270,-86.76
250,325,-165.24,149.99,270,-86.76,250,325,-86.76
250,325,-165.24,149.99,270,-165.24,149.99,270,-86.76
250,325,-86.76,350.01,270,-86.76,350.01,270,-165.24
250,325,-86.76,350.01,270,-165.24,250,325,-165.24
250,170,-165.24,298,170,-165.24,298,170,-86.76
250,170,-165.24,298,170,-86.76,250,170,-86.76
250,170,-86.76,201.99,170,-165.24,250,170,-165.24
250,170,-86.76,201.99,170,-86.76,201.99,170,-165.24"""
_E03_ARCH_KEYS = set(_rmkey([[float(x) for x in ln.split(',')][i:i+3] for i in (0, 3, 6)])
                     for ln in _E03_ARCH_RAW.strip().split('\n'))


def _e03_arch_sel(t):
    return _rmkey(t) in _E03_ARCH_KEYS


def _e03_arch_tris():
    """Doorway gable moved onto the pylon faces (front z=-73, back z=-179) and simplified to a pentagon-prism:
    front + back gable faces, the doorway-head soffit, and two roof slopes to the peak. Its front/back are coplanar
    with the pylon flats, so the whole gatehouse front (pylons + gable) reads as one flush surface."""
    ZF, ZB = -73.0, -179.0
    A, B, C, D, E = (201.99, 170.0), (298.0, 170.0), (350.01, 270.0), (250.0, 325.0), (149.99, 270.0)

    def v(p, z):
        return [p[0], p[1], z]

    def wtri(a, b, c, want):
        t = [a, b, c]
        return [t] if _dot3(_tnormal(t), want) >= 0 else [[a, c, b]]

    pent, out = [A, B, C, D, E], []
    for i in range(1, 4):                                     # fan the pentagon: front (+z) and back (-z)
        out += wtri(v(pent[0], ZF), v(pent[i], ZF), v(pent[i + 1], ZF), [0, 0, 1])
        out += wtri(v(pent[0], ZB), v(pent[i], ZB), v(pent[i + 1], ZB), [0, 0, -1])
    out += _dir_quad(v(A, ZF), v(B, ZF), v(B, ZB), v(A, ZB), [0, -1, 0])   # doorway-head soffit (faces down)
    out += _dir_quad(v(E, ZF), v(D, ZF), v(D, ZB), v(E, ZB), [0, 1, 0])    # left roof slope (faces up)
    out += _dir_quad(v(C, ZF), v(D, ZF), v(D, ZB), v(C, ZB), [0, 1, 0])    # right roof slope
    return out


# Remaining major town walls (350 tris) -> 24: each vertical face flattened in Y on its own plane, plus the
# three walkway tops flattened flat. Matched by exact key. (Gate dip at x[500,600] flattened solid; camera-only.)
_E03_TOWNWALL_RAW = """-400,170,200,-400,70,200,-300,70,200
-400,170,200,-300,70,200,-300,170,200
-300,170,200,-400,270,200,-400,170,200
-300,170,200,-300,268,200,-400,270,200
-200,170,200,-300,268,200,-300,170,200
-300,70,200,-200,170,200,-300,170,200
-300,70,200,-200,70,200,-200,170,200
-200,70,200,-100,170,200,-200,170,200
-200,70,200,-100,70,200,-100,170,200
-100,170,200,-200,270,200,-200,170,200
-200,170,200,-200,270,200,-300,268,200
-100,170,200,-100,263,200,-200,270,200
100,176,200,100,274,200,0,270,200
200,173,200,100,274,200,100,176,200
200,173,200,200,276,200,100,274,200
100,176,200,0,270,200,0,170,200
0,170,200,100,70,200,100,176,200
0,170,200,0,70,200,100,70,200
100,176,200,100,70,200,200,70,200
100,176,200,200,70,200,200,173,200
200,70,200,100,54,200,200,40,200
200,70,200,100,70,200,100,54,200
100,54,200,100,70,200,0,70,200
200,173,200,200,70,200,300,70,200
300,70,200,200,40,200,300,26,200
300,70,200,200,70,200,200,40,200
300,170,200,200,276,200,200,173,200
200,173,200,300,70,200,300,170,200
300,170,200,300,270,200,200,276,200
400,170,200,400,266,200,300,270,200
400,170,200,300,270,200,300,170,200
300,170,200,400,70,200,400,170,200
300,170,200,300,70,200,400,70,200
400,70,200,300,70,200,300,26,200
400,70,200,300,26,200,400,14,200
500,65.97,200,400,70,200,400,14,200
500,65.97,200,400,14,200,500,0,200
400,170,200,400,70,200,500,65.97,200
400,170,200,500,65.97,200,500,172,200
500,172,200,400,266,200,400,170,200
500,172,200,500,272,200,400,266,200
500,0,200,600,0,200,600,70,200
500,0,200,600,70,200,500,65.97,200
500,172,200,500,65.97,200,600,70,200
500,172,200,600,70,200,600,170,200
600,170,200,500,272,200,500,172,200
600,170,200,600,270,200,500,272,200
600,270,300,600,370,200,600,270,200
600,270,300,600,366,300,600,370,200
600,270,400,600,366,300,600,270,300
600,270,400,600,370,400,600,366,300
600,270,500,600,370,400,600,270,400
600,270,500,600,370,500,600,370,400
600,270,600,600,367,600,600,370,500
600,270,600,600,370,500,600,270,500
600,270,700,600,367,600,600,270,600
600,270,700,600,370,700,600,367,600
600,270,800,600,370,700,600,270,700
600,270,800,600,370,800,600,370,700
600,270,900,600,370,800,600,270,800
600,270,900,600,376,900,600,370,800
600,270,1000,600,376,1000,600,376,900
600,270,1000,600,376,900,600,270,900
600,270,1100,600,376,1000,600,270,1000
600,270,1100,600,370,1100,600,376,1000
600,270,1200,600,370,1100,600,270,1100
600,270,1300,600,366,1200,600,270,1200
600,270,1200,600,366,1200,600,370,1100
600,270,1300,600,370,1300,600,366,1200
-400,270,200,-300,268,200,-300,270,150
-400,270,200,-300,270,150,-400,270,150
-300,270,150,-300,268,200,-200,270,200
-300,270,150,-200,270,200,-200,269,150
-200,269,150,-200,270,200,-100,263,200
-200,269,150,-100,263,200,-100,266,150
0,270,150,100,274,200,100,273,150
0,270,150,0,270,200,100,274,200
200,273,150,100,273,150,100,274,200
200,273,150,100,274,200,200,276,200
300,267,150,200,273,150,200,276,200
300,267,150,200,276,200,300,270,200
400,270,150,300,267,150,300,270,200
400,270,150,300,270,200,400,266,200
500,272,150,400,270,150,400,266,200
500,272,150,400,266,200,500,272,200
500,272,200,600,266,150,500,272,150
500,272,200,600,270,200,600,266,150
695,270,200,600,266,150,600,270,200
695,270,200,695,270,150,600,266,150
600,170,150,695,270,150,695,170,150
600,170,150,600,266,150,695,270,150
695,170,150,695,70,150,600,70,150
695,170,150,600,70,150,600,170,150
-300,170,150,-300,70,150,-400,70,150
-300,170,150,-400,70,150,-400,170,150
-400,170,150,-300,270,150,-300,170,150
-400,170,150,-400,270,150,-300,270,150
-200,170,150,-200,269,150,-100,266,150
-300,170,150,-200,269,150,-200,170,150
-200,170,150,-300,70,150,-300,170,150
-300,170,150,-300,270,150,-200,269,150
-200,170,150,-200,70,150,-300,70,150
-100,170,150,-200,70,150,-200,170,150
-100,170,150,-100,70,150,-200,70,150
-200,170,150,-100,266,150,-100,170,150
100,176,150,100,70,150,0,70,150
100,176,150,0,70,150,0,170,150
0,170,150,100,273,150,100,176,150
0,170,150,0,270,150,100,273,150
100,176,150,100,273,150,200,273,150
200,172,150,100,70,150,100,176,150
100,176,150,200,273,150,200,172,150
200,172,150,200,70,150,100,70,150
300,170,150,200,70,150,200,172,150
300,170,150,300,70,150,200,70,150
200,172,150,300,267,150,300,170,150
200,172,150,200,273,150,300,267,150
300,170,150,300,267,150,400,270,150
400,170,150,300,70,150,300,170,150
300,170,150,400,270,150,400,170,150
400,170,150,400,70,150,300,70,150
500,172,150,400,70,150,400,170,150
500,172,150,500,70,150,400,70,150
400,170,150,500,272,150,500,172,150
400,170,150,400,270,150,500,272,150
500,172,150,500,272,150,600,266,150
600,170,150,500,70,150,500,172,150
500,172,150,600,266,150,600,170,150
600,170,150,600,70,150,500,70,150
695,170,150,695,270,150,695,270,200
695,170,150,695,270,200,695,170,200
695,170,200,695,70,150,695,170,150
695,170,200,695,70,200,695,70,150
600,270,200,650,370,200,695,270,200
600,270,200,600,370,200,650,370,200
695,270,200,650,370,200,700,366,200
700,270,200,700,366,200,800,364,200
700,270,200,800,364,200,800,267,200
800,267,200,700,170,200,700,270,200
695,270,200,700,366,200,700,270,200
800,267,200,800,170,200,700,170,200
700,70,200,800,170,200,800,70,200
700,70,200,700,170,200,800,170,200
800,267,200,800,364,200,899,374,200
900,270,200,800,170,200,800,267,200
800,267,200,899,374,200,900,270,200
900,70,200,800,170,200,900,170,200
900,70,200,800,70,200,800,170,200
900,270,200,900,170,200,800,170,200
900,270,200,899,374,200,1000,370,200
900,270,200,1000,370,200,1000,270,200
1000,270,200,1000,170,200,900,170,200
1000,270,200,900,170,200,900,270,200
1000,98,200,900,70,200,900,170,200
1000,98,200,900,170,200,1000,170,200
1100,130,200,1000,98,200,1000,170,200
1100,130,200,1000,170,200,1100,170,200
1100,271,200,1100,170,200,1000,170,200
1100,271,200,1000,170,200,1000,270,200
1000,270,200,1100,377.69,200,1100,271,200
1000,270,200,1000,370,200,1100,377.69,200
1100,271,200,1100,377.69,200,1200,374,200
1100,271,200,1200,374,200,1200,270,200
1200,270,200,1100,170,200,1100,271,200
1200,270,200,1200,170,200,1100,170,200
1100,130,200,1100,170,200,1200,170,200
1300,270,200,1200,170,200,1200,270,200
1200,270,200,1300,370,200,1300,270,200
1300,270,200,1300,170,200,1200,170,200
1200,270,200,1200,374,200,1300,370,200
1400,270,200,1500,370,200,1500,270,200
1400,270,200,1400,364,200,1500,370,200
1500,270,200,1500,170,200,1400,170,200
1500,270,200,1400,170,200,1400,270,200
1500,270,200,1500,370,200,1500,370,100
1500,270,200,1500,370,100,1500,270,100
1500,270,100,1500,170,200,1500,270,200
1500,270,100,1500,170,100,1500,170,200
1500,270,0,1500,370,-100,1500,270,-100
1500,270,0,1500,370,0,1500,370,-100
1500,270,0,1500,270,-100,1500,170,-100
1500,270,0,1500,170,-100,1500,170,-50
1400,270,250,1500,170,250,1500,270,250
1400,270,250,1400,170,250,1500,170,250
1500,270,250,1400,364,250,1400,270,250
1500,270,250,1500,370,250,1400,364,250
1400,364,250,1500,370,250,1500,370,200
1400,364,250,1500,370,200,1400,364,200
1300,370,200,1200,368.02,250,1300,370,250
1300,370,200,1200,374,200,1200,368.02,250
1200,368.02,250,1200,374,200,1100,377.69,200
1200,368.02,250,1100,377.69,200,1100,372,250
1100,372,250,1100,377.69,200,1000,370,200
1100,372,250,1000,370,200,1000,370,250
1000,370,250,1000,370,200,899,374,200
1000,370,250,899,374,200,900,370,250
900,370,250,899,374,200,800,364,200
900,370,250,800,364,200,800,363,250
800,363,250,800,364,200,700,366,200
800,363,250,700,366,200,700,366,250
700,366,250,650,370,200,650,370,250
700,366,250,700,366,200,650,370,200
650,370,250,650,370,200,600,370,200
600,370,200,600,366,300,650,370,300
600,370,200,650,370,300,650,370,250
650,370,300,600,366,300,600,370,400
650,370,300,600,370,400,650,367,400
650,367,400,600,370,400,600,370,500
650,367,400,600,370,500,650,370,500
650,370,500,600,370,500,600,367,600
650,370,500,600,367,600,650,370,600
650,370,600,600,367,600,600,370,700
650,370,600,600,370,700,650,367,700
650,367,700,600,370,700,600,370,800
650,367,700,600,370,800,650,370,800
650,376,900,650,370,800,600,370,800
650,376,900,600,370,800,600,376,900
650,376,1000,650,376,900,600,376,900
650,376,1000,600,376,900,600,376,1000
650,370,1100,650,376,1000,600,376,1000
650,370,1100,600,376,1000,600,370,1100
650,366,1200,650,370,1100,600,370,1100
650,366,1200,600,370,1100,600,366,1200
600,366,1200,650,370,1300,650,366,1200
600,366,1200,600,370,1300,650,370,1300
650,266,1200,650,370,1300,650,270,1300
650,266,1200,650,366,1200,650,370,1300
650,270,1300,650,170,1300,650,170,1200
650,270,1300,650,170,1200,650,266,1200
650,270,1100,650,366,1200,650,266,1200
650,270,1100,650,370,1100,650,366,1200
650,266,1200,650,170,1200,650,170,1100
650,266,1200,650,170,1100,650,270,1100
650,270,1100,650,170,1100,650,170,1000
650,275,1000,650,370,1100,650,270,1100
650,270,1100,650,170,1000,650,275,1000
650,275,1000,650,376,1000,650,370,1100
650,275,900,650,376,1000,650,275,1000
650,275,900,650,376,900,650,376,1000
650,275,1000,650,170,1000,650,170,900
650,275,1000,650,170,900,650,275,900
650,270,800,650,376,900,650,275,900
650,270,800,650,370,800,650,376,900
650,275,900,650,170,800,650,270,800
650,275,900,650,170,900,650,170,800
650,270,800,650,170,800,650,170,700
650,270,800,650,170,700,650,266,700
650,266,700,650,370,800,650,270,800
650,266,700,650,367,700,650,370,800
650,274,600,650,367,700,650,266,700
650,266,700,650,170,600,650,274,600
650,266,700,650,170,700,650,170,600
650,274,600,650,370,600,650,367,700
650,274,600,650,170,500,650,270,500
650,270,500,650,370,600,650,274,600
650,270,500,650,370,500,650,370,600
650,274,600,650,170,600,650,170,500
650,270,500,650,170,500,650,170,400
650,270,500,650,170,400,650,270,400
650,270,400,650,370,500,650,270,500
650,270,400,650,367,400,650,370,500
650,270,300,650,367,400,650,270,400
650,270,400,650,170,300,650,270,300
650,270,300,650,370,300,650,367,400
650,270,400,650,170,400,650,170,300
650,270,300,650,170,300,650,170,250
650,270,300,650,170,250,650,270,250
650,270,250,650,370,300,650,270,300
650,270,250,650,370,250,650,370,300
650,270,250,700,170,250,700,270,250
650,270,250,650,170,250,700,170,250
700,270,250,650,370,250,650,270,250
700,270,250,700,366,250,650,370,250
700,270,250,700,170,250,800,170,250
800,267,250,700,366,250,700,270,250
800,267,250,800,363,250,700,366,250
700,270,250,800,170,250,800,267,250
800,267,250,800,170,250,900,170,250
800,267,250,900,170,250,900,270,250
900,270,250,800,363,250,800,267,250
900,270,250,900,370,250,800,363,250
900,270,250,900,170,250,1000,170,250
900,270,250,1000,170,250,1000,270,250
1000,270,250,1000,170,250,1100,170,250
1000,270,250,1100,170,250,1100,271,250
1000,270,250,1000,370,250,900,370,250
1000,270,250,900,370,250,900,270,250
1100,271,250,1000,370,250,1000,270,250
1100,271,250,1100,372,250,1000,370,250
1200,270,250,1100,372,250,1100,271,250
1100,271,250,1200,170,250,1200,270,250
1100,271,250,1100,170,250,1200,170,250
1200,270,250,1200,368.02,250,1100,372,250
1200,270,250,1200,170,250,1300,170,250
1200,270,250,1300,170,250,1300,270,250
1300,270,250,1200,368.02,250,1200,270,250
1300,270,250,1300,370,250,1200,368.02,250
-400,100,300,-400,70,300,-400,70,200
-400,70,200,-400,200,300,-400,100,300
-400,70,200,-400,170,200,-400,200,300
-400,170,200,-400,270,200,-400,270,300
-400,170,200,-400,270,300,-400,200,300
-400,200,300,-400,270,300,-400,270,400
-400,200,300,-400,270,400,-400,200,400
-400,200,400,-400,270,500,-400,200,500
-400,200,400,-400,270,400,-400,270,500
-400,200,500,-400,100,400,-400,200,400
-400,100,500,-400,70,400,-400,100,400
-400,200,500,-400,100,500,-400,100,400
-400,100,400,-400,70,400,-400,70,300
-400,100,400,-400,70,300,-400,100,300
-400,100,500,-400,70,500,-400,70,400
-400,100,600,-400,200,700,-400,100,700
-400,100,700,-400,68,700,-400,70,600
-400,100,700,-400,70,600,-400,100,600
-400,100,600,-400,200,600,-400,200,700
-400,200,600,-400,270,700,-400,200,700
-400,200,600,-400,270,600,-400,270,700
-400,200,700,-400,270,700,-400,270,800
-400,200,700,-400,270,800,-400,200,800
-400,200,800,-400,270,800,-400,270,900
-400,200,800,-400,270,900,-400,200,900
-400,200,900,-400,270,1000,-400,200,1000
-400,200,900,-400,270,900,-400,270,1000
-400,200,1000,-400,270,1000,-400,270,1100
-400,200,1000,-400,270,1100,-400,200,1100
-400,200,1200,-400,200,1100,-400,270,1100
-400,200,1200,-400,270,1100,-400,270,1200
-400,270,1200,-400,170,1300,-400,200,1200
-400,270,1200,-400,270,1300,-400,170,1300
-400,170,1300,-400,100,1200,-400,200,1200
-400,170,1300,-400,90,1300,-400,100,1200
-400,100,1200,-400,90,1300,-400,0,1300
-400,100,1200,-400,0,1300,-400,0,1200
-400,0,1200,-400,100,1100,-400,100,1200
-400,0,1200,-400,0,1100,-400,100,1100
-400,100,1100,-400,0,1100,-400,0,1000
-400,100,1100,-400,0,1000,-400,100,1000
-400,100,1000,-400,200,1100,-400,100,1100
-400,100,1000,-400,200,1000,-400,200,1100
-400,100,1000,-400,0,1000,-400,27,900
-400,100,1000,-400,27,900,-400,100,900
-400,100,900,-400,27,900,-400,50,800
-400,100,900,-400,50,800,-400,100,800
-400,100,800,-400,200,900,-400,100,900
-400,100,800,-400,200,800,-400,200,900
-400,100,800,-400,50,800,-400,68,700
-400,100,800,-400,68,700,-400,100,700
600,0,200,600,270,1300,600,270,200
600,0,200,600,0,1300,600,270,1300"""
_E03_TOWNWALL_KEYS = set(_rmkey([[float(x) for x in ln.split(',')][i:i+3] for i in (0, 3, 6)])
                         for ln in _E03_TOWNWALL_RAW.strip().split('\n'))


# Arcade back-side geometry (x[-500,-400]) now hidden behind the solid x=-400 wall -> removed outright.
_E03_ARCBACK_RAW = """-400,170,150,-400,70,150,-400,70,50
-400,170,50,-400,270,150,-400,170,150
-400,170,50,-400,267,50,-400,270,150
-400,170,150,-400,70,50,-400,170,50
-400,200,1200,-400,100,1200,-500,100,1200
-400,200,1200,-500,100,1200,-500,200,1200
-400,100,1200,-400,100,1100,-500,100,1100
-400,100,1200,-500,100,1100,-500,100,1200
-400,200,1100,-500,100,1100,-400,100,1100
-400,200,1100,-500,200,1100,-500,100,1100
-400,100,1000,-500,100,1000,-500,200,1000
-400,100,1000,-500,200,1000,-400,200,1000
-500,100,1000,-400,100,1000,-400,100,900
-500,100,1000,-400,100,900,-500,100,900
-400,200,900,-500,100,900,-400,100,900
-400,200,900,-500,200,900,-500,100,900
-400,100,800,-500,100,800,-500,200,800
-400,100,800,-500,200,800,-400,200,800
-500,100,700,-400,100,800,-400,100,700
-500,100,700,-500,100,800,-400,100,800
-500,100,700,-400,100,700,-400,200,700
-500,100,700,-400,200,700,-500,200,700
-500,100,300,-400,200,300,-500,200,300
-500,100,300,-400,100,300,-400,200,300
-500,100,400,-400,100,300,-500,100,300
-500,100,400,-400,100,400,-400,100,300
-400,100,400,-500,100,400,-500,200,400
-400,100,400,-500,200,400,-400,200,400
-400,200,400,-500,200,300,-400,200,300
-400,200,400,-500,200,400,-500,200,300
-500,200,800,-500,200,700,-400,200,700
-500,200,800,-400,200,700,-400,200,800
-400,200,1000,-500,200,900,-400,200,900
-400,200,1000,-500,200,1000,-500,200,900
-500,200,1200,-400,200,1100,-400,200,1200
-500,200,1200,-500,200,1100,-400,200,1100"""
_E03_ARCBACK_KEYS = set(_rmkey([[float(x) for x in ln.split(',')][i:i+3] for i in (0, 3, 6)])
                        for ln in _E03_ARCBACK_RAW.strip().split('\n'))


def _e03_arcback_sel(t):
    return _rmkey(t) in _E03_ARCBACK_KEYS


def _e03_townwall_sel(t):
    return _rmkey(t) in _E03_TOWNWALL_KEYS


def _e03_townwall_tris():
    """Vertical faces run through simplify_coplanar (each kept on-plane, flattened in Y, but the archway/gate HOLES
    preserved — the flatten fills only from each column's lowest wall cell up, so base openings stay open) + the 3
    flat walkway tops. Holes: e.g. a 5-arch arcade in the x=-400 wall, so this is a bit above 36."""
    raw = [[float(x) for x in ln.split(',')] for ln in _E03_TOWNWALL_RAW.strip().split('\n')]
    allt = [[r[0:3], r[3:6], r[6:9]] for r in raw]
    vert = [t for t in allt if abs(_tnormal(t)[1]) / (math.sqrt(_dot3(_tnormal(t), _tnormal(t))) or 1.0) < 0.5]
    # The x=-400 arcade windows are decorative (not walkable) EXCEPT the z[500,600] arch (obj45's passage). Fill
    # each non-walkable z-section solid (one quad); everything else goes through the hole-preserving merge.
    x400_solid = [(200, 500, 280.0), (600, 1300, 270.0)]   # (z0, z1, top-y); z[200,500] raised to support y280 walkway

    def _in_solid(t):
        if not all(abs(p[0] + 400) < 1 for p in t):
            return False
        cz = sum(p[2] for p in t) / 3
        return any(z0 - 5 <= cz <= z1 + 5 for z0, z1, _ in x400_solid)

    # Close top/wall gaps by making each rampart meet at ONE height = its tallest wall (raise everything up, never
    # lower a top into the visual). SW rampart tops out at z=200's 280, SE & spine at 380. Short front/cap walls
    # (z=150, x=695 -> 280; z=250 -> 380) get forced up to match; the back walls already reach it. `top=` forces it.
    forced = [(280.0, lambda t: all(abs(p[2] - 150) < 1 for p in t) or all(abs(p[0] - 695) < 1 for p in t)),
              (380.0, lambda t: all(abs(p[2] - 250) < 1 for p in t))]
    out, done = [], set()
    for h, pred in forced:
        grp = [t for t in vert if pred(t) and not _in_solid(t)]
        done |= set(map(id, grp))
        out += simplify_coplanar(grp, snap=10.0, keep_windows=True, top=h)
    rest = [t for t in vert if id(t) not in done and not _in_solid(t)]
    out += simplify_coplanar(rest, snap=10.0, keep_windows=True)
    for z0, z1, ty in x400_solid:
        out += _dir_quad([-400, 0, z0], [-400, 0, z1], [-400, ty, z1], [-400, ty, z0], [1, 0, 0])
    out += _dir_quad([-400, 280, 150], [700, 280, 150], [700, 280, 200], [-400, 280, 200], [0, 1, 0])   # SW top (-> x=700)
    out += _dir_quad([600, 380, 200], [1350, 380, 200], [1350, 380, 250], [600, 380, 250], [0, 1, 0])   # SE top (stops at arch center x=1350)
    out += _dir_quad([600, 380, 200], [650, 380, 200], [650, 380, 1300], [600, 380, 1300], [0, 1, 0])   # spine top
    # flatten the z=200 (+z) wall's staircase base at x[0,600] to one flat quad y[0,280]
    out = [t for t in out if not (all(abs(p[2] - 200) < 1 and -1 <= p[0] <= 601 and -1 <= p[1] <= 281 for p in t)
                                  and _tnormal(t)[2] > 0)]
    out += _dir_quad([0, 0, 200], [600, 0, 200], [600, 280, 200], [0, 280, 200], [0, 0, 1])
    # flatten the z=200 (-z) wall's staircase at x[600,1300] to one flat quad y[70,380]
    out = [t for t in out if not (all(abs(p[2] - 200) < 1 and 599 <= p[0] <= 1301 and 69 <= p[1] <= 381 for p in t)
                                  and _tnormal(t)[2] < 0)]
    out += _dir_quad([600, 70, 200], [1300, 70, 200], [1300, 380, 200], [600, 380, 200], [0, 0, -1])
    # (REVERTED 2026-08 with the obj33 customs: the x=695 return stays, the obj33-bridge/cap quads are
    #  gone, and the z=200/z=250 x[1400,1500] + x=1500 z[-100,200] wall sections are KEPT — they were
    #  only droppable while obj33's custom extensions covered them.)
    # USER-DIRECTED (2026-08): the restored x[1400,1500] wall sections came out of the forced-380
    # simplify 10 units ABOVE the obj33 roof ledge (horizontal y=370, x[1500,1600]) — cap them at 370
    # and close the top with a y=370 lid over the rampart section so the ledge line runs flush through.
    def _x1400sec(t, zplane, nsign):
        n = _tnormal(t)
        return (all(1399 <= p[0] <= 1501 and 169 <= p[1] <= 381 and abs(p[2] - zplane) < 1 for p in t)
                and n[2] * nsign > 0)
    out = [t for t in out if not (_x1400sec(t, 250, +1) or _x1400sec(t, 200, -1))]
    out += _dir_quad([1400, 170, 250], [1500, 170, 250], [1500, 370, 250], [1400, 370, 250], [0, 0, 1])
    out += _dir_quad([1400, 170, 200], [1500, 170, 200], [1500, 370, 200], [1400, 370, 200], [0, 0, -1])
    out += _dir_quad([1400, 370, 200], [1500, 370, 200], [1500, 370, 250], [1400, 370, 250], [0, 1, 0])
    # USER-DIRECTED (2026-08): the vanilla x=695 return sits 5 units shy of its neighbours (the SW top
    # cap and the z=150 wall both reach x=700) -> move the return face to x=700 so the corner is flush.
    out = [t for t in out if not all(abs(p[0] - 695) < 1 and 149 <= p[2] <= 201 and 69 <= p[1] <= 281 for p in t)]
    out += _dir_quad([700, 70, 150], [700, 70, 200], [700, 280, 200], [700, 280, 150], [1, 0, 0])
    # USER-DIRECTED (2026-08): with obj43 back to vanilla, the corner at the z=150 wall's west end was
    # open (obj43's retired custom extension used to cover it; a diagonal seal made a new gap). The
    # VISUAL mesh has a flat x=-400 return face there (grid3_4_1_1_1_1_: z 50->150, y 70->270, +x) —
    # add exactly that; the z[45,104] portion overlaps obj43's interior harmlessly.
    out += _dir_quad([-400, 70, 50], [-400, 70, 150], [-400, 270, 150], [-400, 270, 50], [1, 0, 0])
    return out


# obj43 arch (single-facade gatehouse, x=-387 facing +x, passage tunnel z[-47,46], gable roof) -> simplified.
_E03_OBJ43_RAW = """-387.00,102.21,-71.21,-380.54,102.21,-71.21,-380.54,69.87,-71.21
-387.00,102.21,-71.21,-380.54,69.87,-71.21,-387.00,69.87,-71.21
-380.54,102.21,-62.79,-387.00,102.21,-62.79,-387.00,69.87,-62.79
-380.54,102.21,-62.79,-387.00,69.87,-62.79,-380.54,69.87,-62.79
-387.00,102.21,-62.79,-380.54,102.21,-62.79,-374.47,109.13,-59.83
-387.00,102.21,-62.79,-374.47,109.13,-59.83,-387.00,109.13,-59.83
-380.54,102.21,-62.79,-380.54,102.21,-71.21,-374.47,109.13,-74.17
-380.54,102.21,-62.79,-374.47,109.13,-74.17,-374.47,109.13,-59.83
-380.54,102.21,-71.21,-380.54,102.21,-62.79,-380.54,69.87,-62.79
-380.54,102.21,-71.21,-380.54,69.87,-62.79,-380.54,69.87,-71.21
-380.54,102.21,-71.21,-387.00,102.21,-71.21,-387.00,109.13,-74.17
-380.54,102.21,-71.21,-387.00,109.13,-74.17,-374.47,109.13,-74.17
-387.00,102.21,48.79,-380.54,102.21,48.79,-380.54,69.87,48.79
-387.00,102.21,48.79,-380.54,69.87,48.79,-387.00,69.87,48.79
-380.54,102.21,57.21,-387.00,102.21,57.21,-387.00,69.87,57.21
-380.54,102.21,57.21,-387.00,69.87,57.21,-380.54,69.87,57.21
-387.00,102.21,57.21,-380.54,102.21,57.21,-374.47,109.13,60.17
-387.00,102.21,57.21,-374.47,109.13,60.17,-387.00,109.13,60.17
-380.54,102.21,57.21,-380.54,102.21,48.79,-374.47,109.13,45.83
-380.54,102.21,57.21,-374.47,109.13,45.83,-374.47,109.13,60.17
-380.54,102.21,48.79,-380.54,102.21,57.21,-380.54,69.87,57.21
-380.54,102.21,48.79,-380.54,69.87,57.21,-380.54,69.87,48.79
-380.54,102.21,48.79,-387.00,102.21,48.79,-387.00,109.13,45.83
-380.54,102.21,48.79,-387.00,109.13,45.83,-374.47,109.13,45.83
-387.00,270.00,45.00,-386.87,270.00,104.01,-386.87,170.00,45.01
-513.13,170.00,45.01,-386.87,170.00,45.01,-386.87,60.00,45.01
-513.13,170.00,45.01,-386.87,60.00,45.01,-513.13,60.00,45.01
-386.87,170.00,45.01,-386.87,170.00,104.01,-386.87,60.00,104.01
-386.87,170.00,45.01,-386.87,60.00,104.01,-386.87,60.00,45.01
-386.87,170.00,104.01,-513.13,170.00,104.01,-513.13,60.00,104.01
-386.87,170.00,104.01,-513.13,60.00,104.01,-386.87,60.00,104.01
-386.87,170.00,-47.01,-513.13,170.00,-47.01,-513.13,60.00,-47.01
-386.87,170.00,-47.01,-513.13,60.00,-47.01,-386.87,60.00,-47.01
-386.87,170.00,-104.01,-386.87,170.00,-47.01,-386.87,60.00,-47.01
-386.87,170.00,-104.01,-386.87,60.00,-47.01,-386.87,60.00,-104.01
-386.87,270.00,104.01,-386.87,170.00,104.01,-386.87,170.00,45.01
-386.87,325.00,0.00,-386.87,270.00,-104.01,-513.13,270.00,-104.01
-386.87,325.00,0.00,-513.13,270.00,-104.01,-513.13,325.00,0.00
-513.13,170.00,0.00,-513.13,170.00,-47.01,-386.87,170.00,-47.01
-513.13,170.00,0.00,-386.87,170.00,-47.01,-386.87,170.00,0.00
-386.87,270.00,-104.01,-386.87,325.00,0.00,-386.86,270.00,0.00
-386.87,270.00,-104.01,-386.86,270.00,0.00,-387.00,270.00,-47.00
-513.13,170.00,-104.01,-386.87,170.00,-104.01,-386.87,60.00,-104.01
-513.13,170.00,-104.01,-386.87,60.00,-104.01,-513.13,60.00,-104.01
-513.13,325.00,0.00,-513.13,270.00,104.01,-386.87,270.00,104.01
-513.13,325.00,0.00,-386.87,270.00,104.01,-386.87,325.00,0.00
-386.86,270.00,0.00,-386.87,325.00,0.00,-386.87,270.00,104.01
-386.86,270.00,0.00,-386.87,270.00,104.01,-387.00,270.00,45.00
-386.87,170.00,-104.01,-513.13,170.00,-104.01,-513.13,270.00,-104.01
-386.87,170.00,-104.01,-513.13,270.00,-104.01,-386.87,270.00,-104.01
-513.13,170.00,104.01,-386.87,170.00,104.01,-386.87,270.00,104.01
-513.13,170.00,104.01,-386.87,270.00,104.01,-513.13,270.00,104.01
-386.87,170.00,0.00,-386.87,170.00,45.01,-513.13,170.00,45.01
-386.87,170.00,0.00,-513.13,170.00,45.01,-513.13,170.00,0.00
-386.87,170.00,-104.01,-386.87,270.00,-104.01,-386.87,170.00,-47.01
-386.87,170.00,-47.01,-386.87,270.00,-104.01,-387.00,270.00,-47.00
-386.87,170.00,-47.01,-387.00,270.00,-47.00,-386.87,170.00,0.00
-386.87,170.00,0.00,-387.00,270.00,-47.00,-386.86,270.00,0.00
-386.87,170.00,0.00,-386.86,270.00,0.00,-386.87,170.00,45.01
-386.87,170.00,45.01,-386.86,270.00,0.00,-387.00,270.00,45.00"""
_E03_OBJ43_KEYS = set(_rmkey([[float(x) for x in ln.split(',')][i:i+3] for i in (0, 3, 6)])
                      for ln in _E03_OBJ43_RAW.strip().split('\n'))


# West walkway surface raised y270 -> y280 so obj43's right-extension top meets it cleanly.
_E03_WALK_KEYS = set(_rmkey([[float(x) for x in ln.split(',')][i:i+3] for i in (0, 3, 6)])
                     for ln in "-500,270,50,-500,270,500,-400,270,500\n-500,270,50,-400,270,500,-400,270,50".split('\n'))


def _e03_walk_sel(t):
    return _rmkey(t) in _E03_WALK_KEYS


def _e03_walk_tris():
    return (_dir_quad([-500, 280, 50], [-500, 280, 500], [-400, 280, 500], [-400, 280, 50], [0, 1, 0]))


# obj45: two arches -> simplified. Arch A (obj43-style, x=-387 facade +x, passage z[504,597], 2 torches).
# Arch B (obj34-style, facades z=135/213, passage x[-98,0], 4 torches, two-sided +-z).
_E03_OBJ45_RAW = """5.79,102.21,128.63,14.21,102.21,128.63,14.21,67.25,128.63
5.79,102.21,128.63,14.21,67.25,128.63,5.79,67.25,128.63
14.21,102.21,128.63,5.79,102.21,128.63,2.83,109.13,122.57
14.21,102.21,128.63,2.83,109.13,122.57,17.17,109.13,122.57
14.21,102.21,135.09,14.21,102.21,128.63,17.17,109.13,122.57
14.21,102.21,135.09,17.17,109.13,122.57,17.17,109.13,135.09
14.21,102.21,128.63,14.21,102.21,135.09,14.21,67.25,135.09
14.21,102.21,128.63,14.21,67.25,135.09,14.21,67.25,128.63
5.79,102.21,135.09,5.79,102.21,128.63,5.79,67.25,128.63
5.79,102.21,135.09,5.79,67.25,128.63,5.79,67.25,135.09
-114.21,102.21,128.63,-114.21,102.21,135.09,-117.17,109.13,135.09
-114.21,102.21,128.63,-117.17,109.13,135.09,-117.17,109.13,122.57
-114.21,102.21,128.63,-105.79,102.21,128.63,-105.79,69.87,128.63
-114.21,102.21,128.63,-105.79,69.87,128.63,-114.21,69.87,128.63
-105.79,102.21,128.63,-114.21,102.21,128.63,-117.17,109.13,122.57
-105.79,102.21,128.63,-117.17,109.13,122.57,-102.83,109.13,122.57
-105.79,102.21,135.09,-105.79,102.21,128.63,-102.83,109.13,122.57
-105.79,102.21,135.09,-102.83,109.13,122.57,-102.83,109.13,135.09
-105.79,102.21,128.63,-105.79,102.21,135.09,-105.79,69.87,135.09
-105.79,102.21,128.63,-105.79,69.87,135.09,-105.79,69.87,128.63
-114.21,102.21,135.09,-114.21,102.21,128.63,-114.21,69.87,128.63
-114.21,102.21,135.09,-114.21,69.87,128.63,-114.21,69.87,135.09
5.79,102.21,128.63,5.79,102.21,135.09,2.83,109.13,135.09
5.79,102.21,128.63,2.83,109.13,135.09,2.83,109.13,122.57
-387.00,102.21,488.79,-380.54,102.21,488.79,-380.54,69.87,488.79
-387.00,102.21,488.79,-380.54,69.87,488.79,-387.00,69.87,488.79
-105.79,102.21,219.45,-105.79,102.21,212.99,-102.83,109.13,212.99
-105.79,102.21,219.45,-102.83,109.13,212.99,-102.83,109.13,225.51
14.21,102.21,212.99,14.21,102.21,219.45,14.21,67.25,219.45
14.21,102.21,212.99,14.21,67.25,219.45,14.21,67.25,212.99
-105.79,102.21,219.45,-114.21,102.21,219.45,-114.21,69.87,219.45
-105.79,102.21,219.45,-114.21,69.87,219.45,-105.79,69.87,219.45
-114.21,102.21,219.45,-105.79,102.21,219.45,-102.83,109.13,225.51
-114.21,102.21,219.45,-102.83,109.13,225.51,-117.17,109.13,225.51
-380.54,102.21,497.21,-387.00,102.21,497.21,-387.00,69.87,497.21
-380.54,102.21,497.21,-387.00,69.87,497.21,-380.54,69.87,497.21
5.79,102.21,219.45,5.79,102.21,212.99,5.79,67.25,212.99
5.79,102.21,219.45,5.79,67.25,212.99,5.79,67.25,219.45
-114.21,102.21,212.99,-114.21,102.21,219.45,-117.17,109.13,225.51
-114.21,102.21,212.99,-117.17,109.13,225.51,-117.17,109.13,212.99
-114.21,102.21,219.45,-114.21,102.21,212.99,-114.21,69.87,212.99
-114.21,102.21,219.45,-114.21,69.87,212.99,-114.21,69.87,219.45
5.79,102.21,212.99,5.79,102.21,219.45,2.83,109.13,225.51
5.79,102.21,212.99,2.83,109.13,225.51,2.83,109.13,212.99
5.79,102.21,219.45,14.21,102.21,219.45,17.17,109.13,225.51
5.79,102.21,219.45,17.17,109.13,225.51,2.83,109.13,225.51
14.21,102.21,219.45,5.79,102.21,219.45,5.79,67.25,219.45
14.21,102.21,219.45,5.79,67.25,219.45,14.21,67.25,219.45
14.21,102.21,219.45,14.21,102.21,212.99,17.17,109.13,212.99
14.21,102.21,219.45,17.17,109.13,212.99,17.17,109.13,225.51
-105.79,102.21,212.99,-105.79,102.21,219.45,-105.79,69.87,219.45
-105.79,102.21,212.99,-105.79,69.87,219.45,-105.79,69.87,212.99
-387.00,102.21,497.21,-380.54,102.21,497.21,-374.47,109.13,500.17
-387.00,102.21,497.21,-374.47,109.13,500.17,-387.00,109.13,500.17
-380.54,102.21,497.21,-380.54,102.21,488.79,-374.47,109.13,485.83
-380.54,102.21,497.21,-374.47,109.13,485.83,-374.47,109.13,500.17
-380.54,102.21,488.79,-380.54,102.21,497.21,-380.54,69.87,497.21
-380.54,102.21,488.79,-380.54,69.87,497.21,-380.54,69.87,488.79
-380.54,102.21,488.79,-387.00,102.21,488.79,-387.00,109.13,485.83
-380.54,102.21,488.79,-387.00,109.13,485.83,-374.47,109.13,485.83
-387.00,102.21,608.79,-380.54,102.21,608.79,-380.54,69.87,608.79
-387.00,102.21,608.79,-380.54,69.87,608.79,-387.00,69.87,608.79
-380.54,102.21,617.21,-387.00,102.21,617.21,-387.00,69.87,617.21
-380.54,102.21,617.21,-387.00,69.87,617.21,-380.54,69.87,617.21
-387.00,102.21,617.21,-380.54,102.21,617.21,-374.47,109.13,620.17
-387.00,102.21,617.21,-374.47,109.13,620.17,-387.00,109.13,620.17
-380.54,102.21,617.21,-380.54,102.21,608.79,-374.47,109.13,605.83
-380.54,102.21,617.21,-374.47,109.13,605.83,-374.47,109.13,620.17
-380.54,102.21,608.79,-380.54,102.21,617.21,-380.54,69.87,617.21
-380.54,102.21,608.79,-380.54,69.87,617.21,-380.54,69.87,608.79
-380.54,102.21,608.79,-387.00,102.21,608.79,-387.00,109.13,605.83
-380.54,102.21,608.79,-387.00,109.13,605.83,-374.47,109.13,605.83
-386.87,170.00,552.00,-386.87,170.00,596.01,-513.13,170.00,596.01
-386.87,170.00,552.00,-513.13,170.00,596.01,-513.13,170.00,552.00
-386.87,170.00,502.99,-386.87,170.00,447.99,-386.87,270.00,447.99
-386.87,170.00,502.99,-386.87,270.00,447.99,-387.00,270.00,503.00
-386.86,270.00,552.00,-386.87,325.00,552.00,-386.87,270.00,653.01
-386.86,270.00,552.00,-386.87,270.00,653.01,-387.00,270.00,596.00
-386.87,270.00,447.99,-386.87,325.00,552.00,-386.86,270.00,552.00
-386.87,270.00,447.99,-386.86,270.00,552.00,-387.00,270.00,503.00
-386.87,170.00,447.99,-386.87,170.00,502.99,-386.87,60.00,502.99
-386.87,170.00,447.99,-386.87,60.00,502.99,-386.87,60.00,447.99
-386.87,170.00,502.99,-513.13,170.00,503.00,-513.13,60.00,503.00
-386.87,170.00,502.99,-513.13,60.00,503.00,-386.87,60.00,502.99
-386.87,170.00,653.01,-513.13,170.00,653.01,-513.13,60.00,653.01
-386.87,170.00,653.01,-513.13,60.00,653.01,-386.87,60.00,653.01
-386.87,170.00,596.01,-386.87,170.00,653.01,-386.87,60.00,653.01
-386.87,170.00,596.01,-386.87,60.00,653.01,-386.87,60.00,596.01
-513.13,170.00,596.01,-386.87,170.00,596.01,-386.87,60.00,596.01
-513.13,170.00,596.01,-386.87,60.00,596.01,-513.13,60.00,596.01
-386.87,325.00,552.00,-386.87,270.00,447.99,-513.13,270.00,447.99
-386.87,325.00,552.00,-513.13,270.00,447.99,-513.13,325.00,552.00
-513.13,170.00,552.00,-513.13,170.00,503.00,-386.87,170.00,502.99
-513.13,170.00,552.00,-386.87,170.00,502.99,-386.87,170.00,552.00
-513.13,170.00,447.99,-386.87,170.00,447.99,-386.87,60.00,447.99
-513.13,170.00,447.99,-386.87,60.00,447.99,-513.13,60.00,447.99
-513.13,325.00,552.00,-513.13,270.00,653.01,-386.87,270.00,653.01
-513.13,325.00,552.00,-386.87,270.00,653.01,-386.87,325.00,552.00
-386.87,170.00,447.99,-513.13,170.00,447.99,-513.13,270.00,447.99
-386.87,170.00,447.99,-513.13,270.00,447.99,-386.87,270.00,447.99
-513.13,170.00,653.01,-386.87,170.00,653.01,-386.87,270.00,653.01
-513.13,170.00,653.01,-386.87,270.00,653.01,-513.13,270.00,653.01
-50.00,170.00,134.76,-2.00,170.00,134.76,-2.00,170.00,213.24
-50.00,170.00,134.76,-2.00,170.00,213.24,-50.00,170.00,213.24
-98.01,170.00,134.76,-150.01,170.00,134.76,-150.01,270.00,134.76
-98.01,170.00,134.76,-150.01,270.00,134.76,-100.01,270.00,134.76
-50.00,270.00,134.75,-50.00,325.00,134.76,50.01,270.00,134.76
-50.00,270.00,134.75,50.01,270.00,134.76,0.00,270.00,134.76
-2.00,170.00,213.24,50.01,170.00,213.24,50.01,270.00,213.24
-2.00,170.00,213.24,50.01,270.00,213.24,0.00,270.00,213.24
-50.00,270.00,213.24,-50.00,325.00,213.24,-150.01,270.00,213.24
-50.00,270.00,213.24,-150.01,270.00,213.24,-100.01,270.00,213.24
-150.01,270.00,134.76,-50.00,325.00,134.76,-50.00,270.00,134.75
-150.01,270.00,134.76,-50.00,270.00,134.75,-100.01,270.00,134.76
50.01,270.00,213.24,-50.00,325.00,213.24,-50.00,270.00,213.24
50.01,270.00,213.24,-50.00,270.00,213.24,0.00,270.00,213.24
-150.01,170.00,134.76,-98.01,170.00,134.76,-98.01,60.00,134.76
-150.01,170.00,134.76,-98.01,60.00,134.76,-150.01,60.00,134.76
-98.01,170.00,134.76,-98.01,170.00,213.24,-98.01,60.00,213.24
-98.01,170.00,134.76,-98.01,60.00,213.24,-98.01,60.00,134.76
50.01,170.00,134.76,50.01,170.00,213.24,50.01,60.00,213.24
50.01,170.00,134.76,50.01,60.00,213.24,50.01,60.00,134.76
-2.00,170.00,134.76,50.01,170.00,134.76,50.01,60.00,134.76
-2.00,170.00,134.76,50.01,60.00,134.76,-2.00,60.00,134.76
50.01,170.00,213.24,-2.00,170.00,213.24,-2.00,60.00,213.24
50.01,170.00,213.24,-2.00,60.00,213.24,50.01,60.00,213.24
-2.00,170.00,213.24,-2.00,170.00,134.76,-2.00,60.00,134.76
-2.00,170.00,213.24,-2.00,60.00,134.76,-2.00,60.00,213.24
-50.00,325.00,134.76,-150.01,270.00,134.76,-150.01,270.00,213.24
-50.00,325.00,134.76,-150.01,270.00,213.24,-50.00,325.00,213.24
-50.00,170.00,213.24,-98.01,170.00,213.24,-98.01,170.00,134.76
-50.00,170.00,213.24,-98.01,170.00,134.76,-50.00,170.00,134.76
-150.01,170.00,213.24,-150.01,170.00,134.76,-150.01,60.00,134.76
-150.01,170.00,213.24,-150.01,60.00,134.76,-150.01,60.00,213.24
-50.00,325.00,213.24,50.01,270.00,213.24,50.01,270.00,134.76
-50.00,325.00,213.24,50.01,270.00,134.76,-50.00,325.00,134.76
-98.01,170.00,213.24,-150.01,170.00,213.24,-150.01,60.00,213.24
-98.01,170.00,213.24,-150.01,60.00,213.24,-98.01,60.00,213.24
50.01,170.00,213.24,50.01,170.00,134.76,50.01,270.00,134.76
50.01,170.00,213.24,50.01,270.00,134.76,50.01,270.00,213.24
-150.01,170.00,134.76,-150.01,170.00,213.24,-150.01,270.00,213.24
-150.01,170.00,134.76,-150.01,270.00,213.24,-150.01,270.00,134.76
-386.87,170.00,502.99,-387.00,270.00,503.00,-386.87,170.00,552.00
-386.87,170.00,552.00,-387.00,270.00,503.00,-386.86,270.00,552.00
-386.87,170.00,552.00,-386.86,270.00,552.00,-386.87,170.00,596.01
-386.87,170.00,596.01,-386.86,270.00,552.00,-387.00,270.00,596.00
-386.87,170.00,596.01,-387.00,270.00,596.00,-386.87,170.00,653.01
-386.87,170.00,653.01,-387.00,270.00,596.00,-386.87,270.00,653.01
-2.00,170.00,213.24,0.00,270.00,213.24,-50.00,170.00,213.24
-50.00,170.00,213.24,0.00,270.00,213.24,-50.00,270.00,213.24
-50.00,170.00,213.24,-50.00,270.00,213.24,-98.01,170.00,213.24
-98.01,170.00,213.24,-50.00,270.00,213.24,-100.01,270.00,213.24
-98.01,170.00,213.24,-100.01,270.00,213.24,-150.01,170.00,213.24
-150.01,170.00,213.24,-100.01,270.00,213.24,-150.01,270.00,213.24
50.01,270.00,134.76,50.01,170.00,134.76,0.00,270.00,134.76
0.00,270.00,134.76,50.01,170.00,134.76,-2.00,170.00,134.76
0.00,270.00,134.76,-2.00,170.00,134.76,-50.00,270.00,134.75
-50.00,270.00,134.75,-2.00,170.00,134.76,-50.00,170.00,134.76
-50.00,270.00,134.75,-50.00,170.00,134.76,-100.01,270.00,134.76
-100.01,270.00,134.76,-50.00,170.00,134.76,-98.01,170.00,134.76"""
_E03_OBJ45_KEYS = set(_rmkey([[float(x) for x in ln.split(',')][i:i+3] for i in (0, 3, 6)])
                      for ln in _E03_OBJ45_RAW.strip().split('\n'))



_E03_OBJ33A_KEYS = set(_rmkey([[float(x) for x in ln.split(',')][i:i+3] for i in (0, 3, 6)])
                       for ln in """1496.2,202.21,-2.79,1489.74,169.87,-2.79,1496.2,169.87,-2.79
1496.2,202.21,-2.79,1489.74,202.21,-2.79,1489.74,169.87,-2.79
1489.74,202.21,-2.79,1489.74,169.87,-11.21,1489.74,169.87,-2.79
1489.74,202.21,-2.79,1489.74,202.21,-11.21,1489.74,169.87,-11.21
1489.74,202.21,-11.21,1496.2,202.21,-11.21,1496.2,169.87,-11.21
1489.74,202.21,-11.21,1496.2,169.87,-11.21,1489.74,169.87,-11.21
1496.2,202.21,-11.21,1483.68,209.13,-14.17,1496.2,209.13,-14.17
1496.2,202.21,-11.21,1489.74,202.21,-11.21,1483.68,209.13,-14.17
1489.74,202.21,-11.21,1483.68,209.13,0.17,1483.68,209.13,-14.17
1489.74,202.21,-11.21,1489.74,202.21,-2.79,1483.68,209.13,0.17
1489.74,202.21,-2.79,1496.2,209.13,0.17,1483.68,209.13,0.17
1489.74,202.21,-2.79,1496.2,202.21,-2.79,1496.2,209.13,0.17
1489.74,202.21,117.21,1496.2,209.13,120.17,1483.68,209.13,120.17
1489.74,202.21,117.21,1496.2,202.21,117.21,1496.2,209.13,120.17
1496.2,202.21,117.21,1489.74,202.21,117.21,1489.74,169.87,117.21
1496.2,202.21,117.21,1489.74,169.87,117.21,1496.2,169.87,117.21
1489.74,202.21,108.79,1483.68,209.13,120.17,1483.68,209.13,105.83
1489.74,202.21,108.79,1489.74,202.21,117.21,1483.68,209.13,120.17
1489.74,202.21,108.79,1496.2,169.87,108.79,1489.74,169.87,108.79
1489.74,202.21,108.79,1496.2,202.21,108.79,1496.2,169.87,108.79
1489.74,202.21,117.21,1489.74,169.87,108.79,1489.74,169.87,117.21
1489.74,202.21,117.21,1489.74,202.21,108.79,1489.74,169.87,108.79
1496.2,202.21,108.79,1489.74,202.21,108.79,1483.68,209.13,105.83
1496.2,202.21,108.79,1483.68,209.13,105.83,1496.2,209.13,105.83
1495.87,270,154.01,1495.87,270,95.01,1495.87,144,95.01
1495.87,270,154.01,1495.87,144,95.01,1495.87,144,154.01
1495.87,376,154.01,1495.87,270,95.01,1495.87,270,154.01
1495.87,376,102.01,1495.87,270,50,1495.87,270,95.01
1495.87,376,102.01,1495.87,270,95.01,1495.87,376,154.01
1495.87,376,50,1495.87,270,50,1495.87,376,102.01
1495.87,376,50,1495.87,270,1.99,1495.87,270,50
1495.87,376,-2.01,1495.87,270,-54.01,1495.87,270,1.99
1495.87,376,-2.01,1495.87,270,1.99,1495.87,376,50
1495.87,376,-2.01,1495.87,376,-54.01,1495.87,270,-54.01
1495.87,270,1.99,1495.87,270,-54.01,1495.87,144,-54.01
1495.87,270,1.99,1495.87,144,-54.01,1495.87,144,1.99
1622.13,270,50,1495.87,270,95.01,1495.87,270,50
1622.13,270,50,1622.13,270,95.01,1495.87,270,95.01
1495.87,270,50,1495.87,270,1.99,1622.13,270,1.99
1495.87,270,50,1622.13,270,1.99,1622.13,270,50
1622.13,270,1.99,1495.87,144,1.99,1622.13,144,1.99
1622.13,270,1.99,1495.87,270,1.99,1495.87,144,1.99
1495.87,270,95.01,1622.13,144,95.01,1495.87,144,95.01
1495.87,270,95.01,1622.13,270,95.01,1622.13,144,95.01
1500,370,-100,1500,370,0,1600,370,0
1500,370,-100,1600,370,0,1600,370,-50
1600,370,-50,1600,370,-100,1500,370,-100""".strip().split("\n"))



_E03_Z200NOTCH_KEYS = set(_rmkey([[float(x) for x in ln.split(',')][i:i+3] for i in (0, 3, 6)])
                          for ln in """695,70,200,695,170,200,700,70,200
695,170,200,700,70,200,700,170,200
695,170,200,695,270,200,700,270,200
695,170,200,700,170,200,700,270,200""".strip().split("\n"))


def _e03_z200notch_sel(t):
    return _rmkey(t) in _E03_Z200NOTCH_KEYS



_E03_SEWALLTORCH_KEYS = set(_rmkey([[float(x) for x in ln.split(',')][i:i+3] for i in (0, 3, 6)])
                            for ln in """778,170,201,778,70,201,769.21,70,179.79
981,170,201,981,70,201,972.21,70,179.79
726.79,170,179.79,726.79,70,179.79,718,70,201
929.79,170,179.79,929.79,70,179.79,921,70,201
769.21,170,179.79,769.21,70,179.79,748,70,171
748,170,171,748,70,171,726.79,70,179.79
972.21,170,179.79,972.21,70,179.79,951,70,171
951,170,171,951,70,171,929.79,70,179.79
726.79,170,179.79,718,70,201,718,170,201
929.79,170,179.79,921,70,201,921,170,201
778,170,201,769.21,70,179.79,769.21,170,179.79
981,170,201,972.21,70,179.79,972.21,170,179.79
748,170,171,726.79,70,179.79,726.79,170,179.79
769.21,170,179.79,748,70,171,748,170,171
951,170,171,929.79,70,179.79,929.79,170,179.79
972.21,170,179.79,951,70,171,951,170,171
769.21,170,179.79,778,249,201,778,170,201
718,170,201,726.79,237,179.79,726.79,170,179.79
726.79,170,179.79,748,214,171,748,170,171
748,170,171,769.21,223,179.79,769.21,170,179.79
921,170,201,929.79,237,179.79,929.79,170,179.79
972.21,170,179.79,981,266,201,981,170,201
929.79,170,179.79,951,241,171,951,170,171
951,170,171,972.21,250,179.79,972.21,170,179.79
748,170,171,748,214,171,769.21,223,179.79
726.79,170,179.79,726.79,237,179.79,748,214,171
769.21,170,179.79,769.21,223,179.79,778,249,201
718,170,201,718,257,201,726.79,237,179.79
921,170,201,921,257,201,929.79,237,179.79
929.79,170,179.79,929.79,237,179.79,951,241,171
972.21,170,179.79,972.21,250,179.79,981,266,201
951,170,171,951,241,171,972.21,250,179.79
769.21,223,179.79,748,214,171,748,263,201
748,214,171,726.79,237,179.79,748,263,201
778,249,201,769.21,223,179.79,748,263,201
929.79,237,179.79,921,257,201,951,251,201
726.79,237,179.79,718,257,201,748,263,201
951,241,171,929.79,237,179.79,951,251,201
972.21,250,179.79,951,241,171,951,251,201
981,266,201,972.21,250,179.79,951,251,201""".strip().split("\n"))


def _e03_sewalltorch_sel(t):
    return _rmkey(t) in _E03_SEWALLTORCH_KEYS


def _e03_obj33a_sel(t):
    return _rmkey(t) in _E03_OBJ33A_KEYS


def _e03_obj33a_tris():
    """obj33 arch 1 (single -x facade, faces the town). obj43-style: front brought to the torch line x=1483.68
    (covers both torches), extended flat to the flanking walls z[-100,200], passage z[2,95] y[144,270] kept.
    Returns close the depth back to the wall plane x=1500."""
    out = []

    def q(a, b, c, d, w):
        out.extend(_dir_quad(a, b, c, d, w))

    FRX, BX, TBX, EX = 1483.68, 1500.0, 1622.13, 1600.0  # front; wall plane; tunnel back; walkway east edge
    ZL, ZR, PZ0, PZ1 = -100.0, 200.0, 2.0, 95.0
    Y0, LY, TY = 144.0, 270.0, 370.0             # front top lowered to the walkway height (y370) -> flat cap
    N = [-1, 0, 0]
    q([FRX, Y0, ZL], [FRX, Y0, PZ0], [FRX, LY, PZ0], [FRX, LY, ZL], N)        # L pier front (over torch -> wall z=-100)
    q([FRX, Y0, PZ1], [FRX, Y0, ZR], [FRX, LY, ZR], [FRX, LY, PZ1], N)        # R pier front (over torch -> wall z=200)
    q([FRX, LY, ZL], [FRX, LY, ZR], [FRX, TY, ZR], [FRX, TY, ZL], N)          # upper panel
    q([FRX, TY, ZL], [EX, TY, ZL], [EX, TY, ZR], [FRX, TY, ZR], [0, 1, 0])    # flat top cap, merged out over the walkway
    q([FRX, Y0, ZL], [FRX, TY, ZL], [BX, TY, ZL], [BX, Y0, ZL], [0, 0, -1])   # L return -> wall plane
    # (R return z=200 dropped: arch2's east extensions now cover the arch1<->x1500 junction)
    q([FRX, Y0, PZ0], [FRX, LY, PZ0], [TBX, LY, PZ0], [TBX, Y0, PZ0], [0, 0, 1]) # passage jamb z=2 (full tunnel)
    q([FRX, Y0, PZ1], [FRX, LY, PZ1], [TBX, LY, PZ1], [TBX, Y0, PZ1], [0, 0, -1])# passage jamb z=95 (full tunnel)
    q([FRX, LY, PZ0], [FRX, LY, PZ1], [TBX, LY, PZ1], [TBX, LY, PZ0], [0, -1, 0]) # passage ceiling (full tunnel)
    return out



def _e03_obj33b_sel(t):
    # obj33 arch 2 (z-passage, faces +-z) region; exclude the grid3 ground quad (horizontal, y~170)
    if not all(1249 <= p[0] <= 1451 and 170 <= p[2] <= 277 and 155 <= p[1] <= 435 for p in t):
        return False
    n = _tnormal(t)
    if abs(n[1]) > abs(n[0]) and abs(n[1]) > abs(n[2]) and sum(p[1] for p in t) / 3 < 200:
        return False
    return True


def _e03_obj33b_tris():
    """obj33 arch 2 = obj34/Arch-B style: two facades facing +-z with pylons brought to the torch lines z=177/271
    (cover all 4 torches), passage x[1302,1398] through in z, gable ridge along z at x=1350."""
    out = []

    def q(a, b, c, d, w):
        out.extend(_dir_quad(a, b, c, d, w))

    def tri(a, b, c, w):
        tt = [a, b, c]
        out.append(tt if _dot3(_tnormal(tt), w) >= 0 else [a, c, b])

    FZ, BZ = 177.0, 271.0
    FWZ, BWZ, WXL, WXR = 200.0, 250.0, 1180.0, 1500.0   # town-wall faces + ramp feet into the W/E wall sections
    A1X = 1483.68                                        # arch1's front plane (east front ramp rounds into it)
    WY = 370.0                                           # walkway/arch1-top height the east side connects onto
    X0, X1, PX0, PX1, RX = 1250.0, 1450.0, 1302.0, 1398.0, 1350.0
    Y0, LY, Y1, RY = 161.0, 278.0, 378.0, 433.0
    q([X0, Y0, FZ], [PX0, Y0, FZ], [PX0, Y1, FZ], [X0, Y1, FZ], [0, 0, -1])   # front L pylon (over torch)
    q([PX1, Y0, FZ], [X1, Y0, FZ], [X1, Y1, FZ], [PX1, Y1, FZ], [0, 0, -1])   # front R pylon (over torch)
    q([PX0, LY, FZ], [PX1, LY, FZ], [PX1, Y1, FZ], [PX0, Y1, FZ], [0, 0, -1]) # front lintel
    q([X0, Y0, BZ], [PX0, Y0, BZ], [PX0, Y1, BZ], [X0, Y1, BZ], [0, 0, 1])    # back L pylon (over torch)
    q([PX1, Y0, BZ], [X1, Y0, BZ], [X1, Y1, BZ], [PX1, Y1, BZ], [0, 0, 1])    # back R pylon (over torch)
    q([PX0, LY, BZ], [PX1, LY, BZ], [PX1, Y1, BZ], [PX0, Y1, BZ], [0, 0, 1])  # back lintel
    q([PX0, Y0, FZ], [PX0, Y0, BZ], [PX0, LY, BZ], [PX0, LY, FZ], [1, 0, 0])  # jamb x=1302
    q([PX1, Y0, FZ], [PX1, Y0, BZ], [PX1, LY, BZ], [PX1, LY, FZ], [-1, 0, 0]) # jamb x=1398
    q([PX0, LY, FZ], [PX1, LY, FZ], [PX1, LY, BZ], [PX0, LY, BZ], [0, -1, 0]) # tunnel ceiling
    q([X0, Y0, FZ], [X0, Y0, BZ], [X0, Y1, BZ], [X0, Y1, FZ], [-1, 0, 0])       # west end cap (terminate at pylon plane x=1250)
    q([X0, Y0, BZ], [WXL, Y0, BWZ], [WXL, Y1, BWZ], [X0, Y1, BZ], [0, 0, 1])    # west back ramp: back pylon z=271 -> z=250 wall (x=1180)
    q([WXL, Y1, BWZ], [X0, Y1, BZ], [X0, 380.0, BWZ], [WXL, 380.0, BWZ], [0, 1, 0])  # west back ramp top cap -> SE cap (y380, z250)
    # east side: run the front/back straight at the z-running walls (arch1 front x=1483.68, x=1500 wall) so they
    # meet head-on (90 deg) instead of the old acute (56/23 deg) diagonals; a top cap closes the gap between them.
    q([X1, Y0, FZ], [A1X, Y0, FZ], [A1X, WY, FZ], [X1, Y1, FZ], [0, 0, -1])    # east front -> arch1 front (top -> y370)
    q([X1, Y0, BZ], [WXR, Y0, BZ], [WXR, WY, BZ], [X1, Y1, BZ], [0, 0, 1])     # east back -> x=1500 wall (top -> y370)
    q([X1, Y1, FZ], [A1X, WY, FZ], [WXR, WY, BZ], [X1, Y1, BZ], [0, 1, 0])     # east top cap -> walkway y370 (no gap)
    tri([A1X, WY, FWZ], [WXR, WY, FWZ], [WXR, WY, BZ], [0, 1, 0])              # fill corner gap (arch1 top z200 <-> walkway x1500)
    q([X0, Y1, FZ], [X0, Y1, BZ], [RX, RY, BZ], [RX, RY, FZ], [0, 1, 0])      # roof x<ridge
    q([X1, Y1, FZ], [X1, Y1, BZ], [RX, RY, BZ], [RX, RY, FZ], [0, 1, 0])      # roof x>ridge
    tri([X0, Y1, FZ], [X1, Y1, FZ], [RX, RY, FZ], [0, 0, -1])                 # gable front (z=177)
    tri([X0, Y1, BZ], [X1, Y1, BZ], [RX, RY, BZ], [0, 0, 1])                  # gable back (z=271)
    return out


def _e03_obj45_sel(t):
    return _rmkey(t) in _E03_OBJ45_KEYS


def _e03_obj45_tris():
    Y0, Y1, LY, RY = 70.0, 280.0, 170.0, 325.0
    out = []

    def q(a, b, c, d, w):
        out.extend(_dir_quad(a, b, c, d, w))

    def tri(a, b, c, w):
        tt = [a, b, c]
        out.append(tt if _dot3(_tnormal(tt), w) >= 0 else [a, c, b])

    # ---- Arch A (matches obj34/obj43: full-height fronts at the torch line x=-373 cover the torches directly and
    # ramp out to the flanking x=-400 walls; the roof sits behind at x=-387..-513. A single flat cap seats the
    # front onto the gable. Right side sits on the y270 wall, left on y280. ----
    FACX, FRX, BX, WALLX = -387.0, -373.0, -513.0, -400.0
    PZ0, PZ1, OZ0, OZ1, RZ = 504.0, 597.0, 448.0, 653.0, 552.0
    LWZ, RWZ, RWY = 378.0, 723.0, 270.0      # ramp feet into the L/R walls; right eave/wall y270
    q([FRX, Y0, OZ0], [FRX, Y0, PZ0], [FRX, LY, PZ0], [FRX, LY, OZ0], [1, 0, 0])        # lower L pier (over L torch)
    q([FRX, Y0, PZ1], [FRX, Y0, OZ1], [FRX, LY, OZ1], [FRX, LY, PZ1], [1, 0, 0])        # lower R pier (over R torch)
    q([FRX, LY, OZ0], [FRX, LY, OZ1], [FRX, RWY, OZ1], [FRX, Y1, OZ0], [1, 0, 0])       # upper panel (top y280->y270)
    q([FACX, Y1, OZ0], [FRX, Y1, OZ0], [FRX, RWY, OZ1], [FACX, RWY, OZ1], [0, 1, 0])    # flat cap (front -> gable base)
    q([WALLX, Y0, LWZ], [FRX, Y0, OZ0], [FRX, Y1, OZ0], [WALLX, Y1, LWZ], [1, 0, 0])    # L ramp front -> x=-400 wall
    q([FRX, Y0, OZ1], [WALLX, Y0, RWZ], [WALLX, RWY, RWZ], [FRX, RWY, OZ1], [1, 0, 0])  # R ramp front -> x=-400 wall
    tri([FRX, Y1, OZ0], [WALLX, Y1, LWZ], [WALLX, Y1, OZ0], [0, 1, 0])                  # L ramp top cap -> eave/walkway
    tri([FRX, RWY, OZ1], [WALLX, RWY, OZ1], [WALLX, RWY, RWZ], [0, 1, 0])               # R ramp top cap -> eave/walkway
    q([BX, Y0, OZ0], [FACX, Y0, OZ0], [FACX, Y1, OZ0], [BX, Y1, OZ0], [0, 0, -1])       # side z=448
    q([BX, Y0, OZ1], [FACX, Y0, OZ1], [FACX, RWY, OZ1], [BX, RWY, OZ1], [0, 0, 1])      # side z=653 (top y270)
    q([BX, Y0, PZ0], [FRX, Y0, PZ0], [FRX, LY, PZ0], [BX, LY, PZ0], [0, 0, 1])          # jamb z=504
    q([BX, Y0, PZ1], [FRX, Y0, PZ1], [FRX, LY, PZ1], [BX, LY, PZ1], [0, 0, -1])         # jamb z=597
    q([BX, LY, PZ0], [FRX, LY, PZ0], [FRX, LY, PZ1], [BX, LY, PZ1], [0, -1, 0])         # tunnel ceiling
    q([BX, Y1, OZ0], [FACX, Y1, OZ0], [FACX, RY, RZ], [BX, RY, RZ], [0, 1, 0])          # roof z<ridge (eave y280)
    q([BX, RWY, OZ1], [FACX, RWY, OZ1], [FACX, RY, RZ], [BX, RY, RZ], [0, 1, 0])        # roof z>ridge (eave y270)
    tri([FACX, Y1, OZ0], [FACX, RWY, OZ1], [FACX, RY, RZ], [1, 0, 0])                   # gable front
    tri([BX, Y1, OZ0], [BX, RWY, OZ1], [BX, RY, RZ], [-1, 0, 0])                        # gable back

    # ---- Arch B (obj34-style, faces +-z) ----
    FZ, BZ = 127.0, 221.0            # front/back bump depths (cover torches z~122.5 / 225.5)
    X0, X1, PX0, PX1, RX = -150.0, 50.0, -98.0, 0.0, -50.0   # outer x, passage x, ridge x (center)
    # front pylons (faces -z): cover torches, passage open
    # front pylons (faces -z), each flat over its torch then ramping out to the z=150 wall (obj34-style, no corner)
    q([X0, Y0, FZ], [PX0, Y0, FZ], [PX0, Y1, FZ], [X0, Y1, FZ], [0, 0, -1])            # B front pylon L (flat)
    q([X0, Y0, FZ], [-220, Y0, 150], [-220, Y1, 150], [X0, Y1, FZ], [0, 0, -1])        # B front L ramp -> z=150
    q([PX1, Y0, FZ], [X1, Y0, FZ], [X1, Y1, FZ], [PX1, Y1, FZ], [0, 0, -1])            # B front pylon R (flat)
    q([X1, Y0, FZ], [120, Y0, 150], [120, Y1, 150], [X1, Y1, FZ], [0, 0, -1])          # B front R ramp -> z=150
    q([PX0, LY, FZ], [PX1, LY, FZ], [PX1, Y1, FZ], [PX0, Y1, FZ], [0, 0, -1])          # B front lintel
    # back pylons (faces +z), flat over torch then ramping out to the z=200 wall
    q([X0, Y0, BZ], [PX0, Y0, BZ], [PX0, Y1, BZ], [X0, Y1, BZ], [0, 0, 1])             # B back pylon L (flat)
    q([X0, Y0, BZ], [-220, Y0, 200], [-220, Y1, 200], [X0, Y1, BZ], [0, 0, 1])         # B back L ramp -> z=200
    q([PX1, 0.0, BZ], [X1, 0.0, BZ], [X1, Y1, BZ], [PX1, Y1, BZ], [0, 0, 1])           # B back pylon R (down to y0 floor)
    q([X1, 0.0, BZ], [120, 0.0, 200], [120, Y1, 200], [X1, Y1, BZ], [0, 0, 1])         # B back R ramp -> z=200 (to y0)
    q([PX0, LY, BZ], [PX1, LY, BZ], [PX1, Y1, BZ], [PX0, Y1, BZ], [0, 0, 1])           # B back lintel
    # passage jambs (x=PX0, PX1) through the tunnel
    q([PX0, Y0, FZ], [PX0, Y0, BZ], [PX0, LY, BZ], [PX0, LY, FZ], [1, 0, 0])           # B jamb x=-98
    q([PX1, Y0, FZ], [PX1, Y0, BZ], [PX1, LY, BZ], [PX1, LY, FZ], [-1, 0, 0])          # B jamb x=0
    q([PX0, LY, FZ], [PX1, LY, FZ], [PX1, LY, BZ], [PX0, LY, BZ], [0, -1, 0])          # B tunnel ceiling
    # gable roof (ridge x=RX y=RY, eaves x=X0,X1 y=Y1) over the tunnel span z[FZ,BZ]
    q([X0, Y1, FZ], [X0, Y1, BZ], [RX, RY, BZ], [RX, RY, FZ], [0, 1, 0])               # B roof x<ridge
    q([X1, Y1, FZ], [X1, Y1, BZ], [RX, RY, BZ], [RX, RY, FZ], [0, 1, 0])               # B roof x>ridge
    tri([X0, Y1, FZ], [X1, Y1, FZ], [RX, RY, FZ], [0, 0, -1])                          # B gable front
    tri([X0, Y1, BZ], [X1, Y1, BZ], [RX, RY, BZ], [0, 0, 1])                           # B gable back
    # ramp top caps (close the y280 sliver between each ramp's top edge and its wall top)
    tri([X0, Y1, FZ], [-220, Y1, 150], [X0, Y1, 150], [0, 1, 0])                       # B front L ramp top
    tri([X1, Y1, FZ], [120, Y1, 150], [X1, Y1, 150], [0, 1, 0])                        # B front R ramp top
    tri([X0, Y1, BZ], [-220, Y1, 200], [X0, Y1, 200], [0, 1, 0])                       # B back L ramp top
    tri([X1, Y1, BZ], [120, Y1, 200], [X1, Y1, 200], [0, 1, 0])                        # B back R ramp top
    return out


def _e03_obj43_sel(t):
    return _rmkey(t) in _E03_OBJ43_KEYS


def _e03_obj43_tris():
    """obj43 simplified: torch-covering ramped fronts (facade x=-387 ramps out to x=-373 over each torch, no
    concave corner), flat side walls, gable roof (2 slopes + 2 gable ends), passage tunnel z[-47,46] kept."""
    FACX, FRX, BX = -387.0, -373.0, -513.0
    Y0, Y1, RY, LY = 70.0, 280.0, 325.0, 170.0
    PZ0, PZ1, OZ0, OZ1 = -47.0, 46.0, -104.0, 104.0
    out = []

    def q(a, b, c, d, w):
        out.extend(_dir_quad(a, b, c, d, w))

    def tri(a, b, c, w):
        tt = [a, b, c]
        out.append(tt if _dot3(_tnormal(tt), w) >= 0 else [a, c, b])

    # LEFT front: this side butts the big z=-100 canal wall, so extend the flat straight out to z=-100 (meets the
    # wall) instead of ramping into obj43's own z=-104 corner -> no concave pocket. (z[-104,-100] is behind the wall.)
    q([FRX, Y0, -100], [FRX, Y0, PZ0], [FRX, LY, PZ0], [FRX, LY, -100], [1, 0, 0])   # L lower pylon (-> big wall z=-100)
    # RIGHT front: butts the z=150 canal wall, so extend the flat straight out to z=150 (meets the wall) instead
    # of ramping into obj43's z=104 corner -> no concave pocket, same as the left.
    q([FRX, Y0, PZ1], [FRX, Y0, 150], [FRX, LY, 150], [FRX, LY, PZ1], [1, 0, 0])     # R lower pylon (-> z=150 wall)
    # Lintel brought OUT to x=-373 (flush with the fronts) -> no notch: the whole facade above the doorway is one
    # panel z[-100,150], with the two lower pylons beside the doorway. A single top cap seats it onto the gable.
    q([FRX, LY, -100], [FRX, LY, 150], [FRX, Y1, 150], [FRX, Y1, -100], [1, 0, 0])   # upper facade panel
    q([-400, Y1, -100], [FRX, Y1, -100], [FRX, Y1, 150], [-400, Y1, 150], [0, 1, 0]) # front top cap -> meets y280 walkway at x=-400
    q([BX, Y0, OZ0], [FACX, Y0, OZ0], [FACX, Y1, OZ0], [BX, Y1, OZ0], [0, 0, -1])    # side z=-104
    q([BX, Y0, OZ1], [FACX, Y0, OZ1], [FACX, Y1, OZ1], [BX, Y1, OZ1], [0, 0, 1])     # side z=104
    q([BX, Y0, PZ0], [FRX, Y0, PZ0], [FRX, LY, PZ0], [BX, LY, PZ0], [0, 0, 1])       # jamb z=-47
    q([BX, Y0, PZ1], [FRX, Y0, PZ1], [FRX, LY, PZ1], [BX, LY, PZ1], [0, 0, -1])      # jamb z=46
    q([BX, LY, PZ0], [FRX, LY, PZ0], [FRX, LY, PZ1], [BX, LY, PZ1], [0, -1, 0])      # tunnel ceiling
    q([BX, Y1, OZ0], [FACX, Y1, OZ0], [FACX, RY, 0], [BX, RY, 0], [0, 1, 0])         # roof slope z<0
    q([BX, Y1, OZ1], [FACX, Y1, OZ1], [FACX, RY, 0], [BX, RY, 0], [0, 1, 0])         # roof slope z>0
    tri([FACX, Y1, OZ0], [FACX, Y1, OZ1], [FACX, RY, 0], [1, 0, 0])                  # front gable end
    tri([BX, Y1, OZ0], [BX, Y1, OZ1], [BX, RY, 0], [-1, 0, 0])                       # back gable end
    return out


def _e03_north_face(t):
    """The whole z=-100 canal front: the x[-400,900] wall PLUS the x[900,1500] end towers, all as up-to-290
    vertical faces. Replaced by two quads (span A x[-400,200] + span B x[300,1500], y[70,WALL_TOP]) so the towers
    read as one flat front lined up with the cap. NB: the towers' rising base (y98..170) gets filled down to y70."""
    return (all(abs(p[2] + 100) < 2 for p in t) and min(p[1] for p in t) >= 55
            and max(p[1] for p in t) <= 290 and -450 <= (t[0][0] + t[1][0] + t[2][0]) / 3.0 <= 1550)


def _e03_wall_cap(t):
    """The bumpy top cap of the canal wall structure: up-facing tris that SPAN the z=-100..-150 gap (each has a
    vert on each face), y>=258, x[-400,1500]. Merged (as a replace job) to one flat quad that lines up with the
    raised wall tops so the structure is closed."""
    zs = [p[2] for p in t]
    return (max(zs) - min(zs) > 40 and all(-152 <= p[2] <= -98 for p in t)
            and all(-401 <= p[0] <= 1501 for p in t) and min(p[1] for p in t) >= 258
            and _tnormal(t)[1] > 0)


def _quad(a, b, c, d):
    """Two tris (a,b,c,d wound in order) for an explicit authored quad."""
    return [[a, b, c], [a, c, d]]


# WALL_TOP: shared top height for the whole z=-100/-150 canal wall structure. = the tallest merged wall top
# (the z=-100 front, 280) so closing only RAISES the back wall (275->280) and the cap (max 277.61 -> 280) — never
# lowers a wall or reduces the cap's peak, per the directive.
_E03_WALL_TOP = 280.0

_CAM_MERGE_JOBS = {
    'e03': [
        # Directed, one group at a time; empty list = full-detail camera `_c`. 'merge' runs simplify_coplanar on
        # the selected tris (authored OUTWARD, optional shared `top`); 'replace' swaps them for authored `tris`.
        # South-back canal wall z=-150, x[-400,900], flattened to the shared WALL_TOP: 48 tris -> 4 (x[200,300] gap).
        {'kind': 'merge', 'sel': _plane_region(2, -150, x=(-400, 900), y=(55, 290)),
         'snap': 5.0, 'outward': 0.0, 'top': _E03_WALL_TOP},
        # North-front canal face z=-100: wall x[-400,900] + end towers x[900,1500] -> span A (x[-400,200]) +
        # span B (x[300,1500]), both y[70,WALL_TOP]. 69 tris -> 4; x[200,300] passage gap kept, span B lined up
        # with the cap's front edge. (Tower rising base y98..170 is filled down to y70 to make span B one quad.)
        {'kind': 'replace', 'sel': _e03_north_face,
         'tris': _quad([-400, 70, -100], [200, 70, -100], [200, _E03_WALL_TOP, -100], [-400, _E03_WALL_TOP, -100])
               + _quad([300, 70, -100], [1500, 70, -100], [1500, _E03_WALL_TOP, -100], [300, _E03_WALL_TOP, -100])},
        # Top cap connecting the two faces (36 bumpy tris) -> one flat up-facing quad at WALL_TOP spanning
        # z[-150,-100], x[-400,1500], so it closes flush with both raised wall tops.
        {'kind': 'replace', 'sel': _e03_wall_cap,
         'tris': _quad([-400, _E03_WALL_TOP, -100], [1500, _E03_WALL_TOP, -100],
                       [1500, _E03_WALL_TOP, -150], [-400, _E03_WALL_TOP, -150])},
        # REVERTED 2026-08 (user): the ramped-arch approach didn't give the smooth camera glide hoped for;
        # obj34 (gatehouse pylons + gable), obj43, obj45 and obj33 are back to their VANILLA `_c` (which
        # matches the visual meshes) as the baseline for a corner-ROUNDING approach instead.
        # {'kind': 'replace', 'sel': _e03_gate_sel, 'tris': _e03_gate_tris()},      # obj34 pylons
        # {'kind': 'replace', 'sel': _e03_arch_sel, 'tris': _e03_arch_tris()},      # obj34 gable
        # Remaining major town walls (350 tris) -> 24 (9 vertical faces on-plane + 3 walkway tops).
        {'kind': 'replace', 'sel': _e03_townwall_sel, 'tris': _e03_townwall_tris()},
        # Arcade back-side (x[-500,-400]) hidden behind the solid wall -> removed.
        {'kind': 'replace', 'sel': _e03_arcback_sel, 'tris': []},
        # {'kind': 'replace', 'sel': _e03_obj43_sel, 'tris': _e03_obj43_tris()},   # REVERTED (see above)
        # {'kind': 'replace', 'sel': _e03_obj45_sel, 'tris': _e03_obj45_tris()},   # REVERTED (see above)
        # obj33 arch 1 (single -x facade + torches simplified, passage kept).
        # grid3 canal-wall notch at x[695,700] z=200 -> covered by the flattened z=200 wall.
        {'kind': 'replace', 'sel': _e03_z200notch_sel, 'tris': []},
        # obj6_4 "torches" on the SE town wall — DISABLED 2026-08: the 40 selected tris are actually the
        # TWO PILLARS on the z=200 wall face (x 718-778 and 921-981); user wants them kept (vanilla).
        # {'kind': 'replace', 'sel': _e03_sewalltorch_sel, 'tris': []},
        # {'kind': 'replace', 'sel': _e03_obj33a_sel, 'tris': _e03_obj33a_tris()}, # REVERTED (see above)
        # {'kind': 'replace', 'sel': _e03_obj33b_sel, 'tris': _e03_obj33b_tris()}, # REVERTED (see above)
        # {'kind': 'replace', 'sel': _e03_walk_sel, 'tris': _e03_walk_tris()},     # REVERTED: the y280 raise
        #     existed solely to meet obj43's custom extension; vanilla walkway (y270) is back with it.
    ],
}


def cam_merge_selected(tris, town):
    """Apply each listed directed job to its selected tris (merge -> simplify_coplanar authored outward; replace ->
    authored quads); pass everything else through at full detail. Only groups explicitly vetted against the visual
    mesh get touched."""
    jobs = _CAM_MERGE_JOBS.get(town, [])
    if not jobs:
        return tris
    remaining, out = list(tris), []
    for job in jobs:
        sel = job['sel']
        picked = [t for t in remaining if sel(t)]
        remaining = [t for t in remaining if not sel(t)]
        if not picked:
            continue
        if job['kind'] == 'replace':
            out += job['tris']
        else:
            out += simplify_coplanar(picked, snap=job['snap'], outward=job.get('outward', 0.0), top=job.get('top'))
    return out + remaining


def _in_flat_region(t):
    """True if every vertex of t sits inside a both_walls._FLAT_REGIONS rectangle at that region's y (a flat
    ground patch that's been collapsed to one quad). Walls/slopes in the footprint span other y and are kept."""
    for x0, x1, z0, z1, y in _FLAT_REGIONS.get('e03', []):
        if all(x0 - 1 <= p[0] <= x1 + 1 and z0 - 1 <= p[2] <= z1 + 1 and abs(p[1] - y) <= 2 for p in t):
            return True
    return False


def _canal_wall(t):
    """South (z=-50) / north (z=50) canal side-wall tris within x[-200,900], y[0,70] — collapsed to 2 quads
    (both_walls). The full north wall runs wider (x<-200); only the x[-200,900] span is simplified here."""
    return ((all(abs(p[2] + 50) < 1 for p in t) or all(abs(p[2] - 50) < 1 for p in t))
            and all(-1 <= p[1] <= 71 and -201 <= p[0] <= 901 for p in t))


def simplify_terrain(tris):
    """SHARED terrain simplifications, applied before the terrain feeds BOTH the `_a` (player) and `_c` (camera)
    collision (they diverge only in what's ADDED: canal is `_a`-only, triggers are `_a`-only, perimeter is both).
      (1) Drop everything at/west of the x=-500 perimeter boundary — the outer terrain + the west edge-slope.
      (1b) Drop everything fully EAST of the x=1600 perimeter boundary (the outer east terrain).
      (2) Drop the removal REGIONS (structures a hand-authored wall/quad replaces) + the 6-tri exact residue.
      (3) Delete flat ground inside a _FLAT_REGIONS rectangle — replaced by one quad (flat_ground_tris).
      (4) Delete the x[-200,900] canal side walls — replaced by 2 quads (_BOTH_WALLS)."""
    return [t for t in tris if max(p[0] for p in t) > -499.0 and min(p[0] for p in t) < 1600.0
            and not _in_remove_region(t) and _rmkey(t) not in _RESIDUE
            and not _in_flat_region(t) and not _canal_wall(t)]


def _pool_split(pool, prefix, used, max_tris=100):
    """kd_split a POOLED triangle soup into <=max_tris spatially-compact nodes with unique names. Pooling across
    source meshes (then splitting) packs NEARBY polys into the same node regardless of which mesh they came from,
    so every node has a tight bounding box — which is exactly what the runtime frame-gather self-cull keys off."""
    return [(_fit(f'{prefix}{bi}', used, 15), bk) for bi, bk in enumerate(kd_split(pool, max_tris))]


_E03_GATE_XMIN_REF = -73.93                                    # obj44's min x; obj40 is the same mesh at +848 x
_E03_GATE_RAIL = [                                             # inner side-railings (mid wall + corner gussets) to drop
    [[-68.21, 76.68, 25.02], [-68.21, 84.68, 25.02], [-68.0, 70.0, 50.0]],
    [[-68.0, 70.0, 50.0], [-68.21, 84.68, 25.02], [-68.0, 78.0, 50.0]],
    [[-68.21, 76.68, 25.02], [-68.21, 86.9, 0.0], [-68.21, 84.68, 25.02]],
    [[-68.21, 78.9, 0.0], [-68.21, 86.9, 0.0], [-68.21, 76.68, 25.02]],
    [[-68.21, 78.9, 0.0], [-68.21, 84.68, -25.02], [-68.21, 86.9, 0.0]],
    [[-68.21, 76.68, -25.02], [-68.21, 84.68, -25.02], [-68.21, 78.9, 0.0]],
    [[-68.0, 70.0, -50.0], [-68.0, 78.0, -50.0], [-68.21, 76.68, -25.02]],
    [[-68.21, 76.68, -25.02], [-68.0, 78.0, -50.0], [-68.21, 84.68, -25.02]],
    [[-27.79, 76.68, -25.02], [-27.79, 84.68, -25.02], [-28.0, 70.0, -50.0]],
    [[-28.0, 70.0, -50.0], [-27.79, 84.68, -25.02], [-28.0, 78.0, -50.0]],
    [[-27.79, 76.68, -25.02], [-27.79, 86.9, 0.0], [-27.79, 84.68, -25.02]],
    [[-27.79, 78.9, 0.0], [-27.79, 86.9, 0.0], [-27.79, 76.68, -25.02]],
    [[-27.79, 78.9, 0.0], [-27.79, 84.68, 25.02], [-27.79, 86.9, 0.0]],
    [[-27.79, 76.68, 25.02], [-27.79, 84.68, 25.02], [-27.79, 78.9, 0.0]],
    [[-27.79, 76.68, 25.02], [-28.0, 78.0, 50.0], [-27.79, 84.68, 25.02]],
    [[-28.0, 70.0, 50.0], [-28.0, 78.0, 50.0], [-27.79, 76.68, 25.02]],
]


_E03_GATE_RAISE_DY = 8.0
_E03_GATE_RAISE = [                                            # walkway-top surface: raise +8 to meet the parapet tops
    [[-68.21, 76.68, 25.02], [-68.0, 70.0, 50.0], [-28.0, 70.0, 50.0]],
    [[-68.21, 76.68, 25.02], [-28.0, 70.0, 50.0], [-27.79, 76.68, 25.02]],
    [[-27.79, 76.68, 25.02], [-68.21, 78.9, 0.0], [-68.21, 76.68, 25.02]],
    [[-68.21, 76.68, -25.02], [-68.21, 78.9, 0.0], [-27.79, 78.9, 0.0]],
    [[-27.79, 76.68, 25.02], [-27.79, 78.9, 0.0], [-68.21, 78.9, 0.0]],
    [[-68.21, 76.68, -25.02], [-27.79, 78.9, 0.0], [-27.79, 76.68, -25.02]],
    [[-27.79, 76.68, -25.02], [-68.0, 70.0, -50.0], [-68.21, 76.68, -25.02]],
    [[-27.79, 76.68, -25.02], [-28.0, 70.0, -50.0], [-68.0, 70.0, -50.0]],
]
_E03_GATE_EXTEND = [                                           # outer side-face: raise its top edge (verts y>=65) +8
    [[-73.0, 70.0, -50.0], [-73.07, 47.0, -50.0], [-73.07, 47.0, -36.0]],
    [[-73.07, 47.0, -36.0], [-73.0, 76.68, -25.0], [-73.0, 70.0, -50.0]],
    [[-73.07, 47.0, -36.0], [-73.07, 53.0, -26.0], [-73.0, 76.68, -25.0]],
    [[-73.0, 78.9, 0.0], [-73.0, 76.68, -25.0], [-73.07, 53.0, -26.0]],
    [[-73.07, 58.0, -13.0], [-73.07, 60.0, 0.0], [-73.0, 78.9, 0.0]],
    [[-73.0, 78.9, 0.0], [-73.07, 53.0, -26.0], [-73.07, 58.0, -13.0]],
    [[-73.0, 78.9, 0.0], [-73.07, 60.0, 0.0], [-73.07, 58.0, 13.0]],
    [[-73.0, 78.9, 0.0], [-73.07, 58.0, 13.0], [-73.0, 76.68, 25.0]],
    [[-73.0, 76.68, 25.0], [-73.07, 58.0, 13.0], [-73.07, 53.0, 26.0]],
    [[-73.0, 76.68, 25.0], [-73.07, 53.0, 26.0], [-73.0, 70.0, 50.0]],
    [[-73.0, 70.0, 50.0], [-73.07, 53.0, 26.0], [-73.07, 47.0, 35.0]],
    [[-73.0, 70.0, 50.0], [-73.07, 47.0, 35.0], [-73.07, 47.0, 50.0]],
    [[-22.93, 47.0, 35.0], [-23.0, 76.68, 25.0], [-23.0, 70.0, 50.0]],
    [[-22.93, 47.0, 35.0], [-22.93, 53.0, 26.0], [-23.0, 76.68, 25.0]],
    [[-23.0, 70.0, 50.0], [-22.93, 47.0, 50.0], [-22.93, 47.0, 35.0]],
    [[-23.0, 78.9, 0.0], [-23.0, 76.68, 25.0], [-22.93, 53.0, 26.0]],
    [[-23.0, 78.9, 0.0], [-22.93, 53.0, 26.0], [-22.93, 58.0, 13.0]],
    [[-22.93, 58.0, 13.0], [-22.93, 60.0, 0.0], [-23.0, 78.9, 0.0]],
    [[-23.0, 78.9, 0.0], [-22.93, 60.0, 0.0], [-22.93, 58.0, -13.0]],
    [[-23.0, 78.9, 0.0], [-22.93, 58.0, -13.0], [-23.0, 76.68, -25.0]],
    [[-23.0, 76.68, -25.0], [-22.93, 58.0, -13.0], [-22.93, 53.0, -26.0]],
    [[-23.0, 76.68, -25.0], [-22.93, 53.0, -26.0], [-23.0, 70.0, -50.0]],
    [[-23.0, 70.0, -50.0], [-22.93, 53.0, -26.0], [-22.93, 47.0, -36.0]],
    [[-23.0, 70.0, -50.0], [-22.93, 47.0, -36.0], [-23.0, 47.0, -50.0]],
]
_E03_GATE_OUTER = [                                            # vertical filler walls (x=-73/-23, y70..86.9) now obsolete
    [[-23.0, 78.0, -50.0], [-23.0, 76.68, -25.0], [-23.0, 70.0, -50.0]],
    [[-23.0, 84.68, -25.02], [-23.0, 76.68, -25.0], [-23.0, 78.0, -50.0]],
    [[-23.0, 84.68, -25.02], [-23.0, 78.9, 0.0], [-23.0, 76.68, -25.0]],
    [[-23.0, 86.9, 0.0], [-23.0, 78.9, 0.0], [-23.0, 84.68, -25.02]],
    [[-23.0, 86.9, 0.0], [-23.0, 76.68, 25.0], [-23.0, 78.9, 0.0]],
    [[-23.0, 84.68, 25.02], [-23.0, 76.68, 25.0], [-23.0, 86.9, 0.0]],
    [[-23.0, 84.68, 25.02], [-23.0, 70.0, 50.0], [-23.0, 76.68, 25.0]],
    [[-23.0, 78.0, 50.0], [-23.0, 70.0, 50.0], [-23.0, 84.68, 25.02]],
    [[-73.0, 84.68, -25.02], [-73.0, 70.0, -50.0], [-73.0, 76.68, -25.0]],
    [[-73.0, 78.0, -50.0], [-73.0, 70.0, -50.0], [-73.0, 84.68, -25.02]],
    [[-73.0, 84.68, -25.02], [-73.0, 76.68, -25.0], [-73.0, 86.9, 0.0]],
    [[-73.0, 86.9, 0.0], [-73.0, 76.68, -25.0], [-73.0, 78.9, 0.0]],
    [[-73.0, 86.9, 0.0], [-73.0, 78.9, 0.0], [-73.0, 84.68, 25.02]],
    [[-73.0, 84.68, 25.02], [-73.0, 78.9, 0.0], [-73.0, 76.68, 25.0]],
    [[-73.0, 84.68, 25.02], [-73.0, 76.68, 25.0], [-73.0, 78.0, 50.0]],
    [[-73.0, 78.0, 50.0], [-73.0, 76.68, 25.0], [-73.0, 70.0, 50.0]],
]
_E03_GATE_ROOF24 = [                                           # the 24-tri domed roof (barrel vault) -> merge to 8
    [[-27.79, 84.68, -25.02], [-28.0, 78.0, -50.0], [-68.0, 78.0, -50.0]],
    [[-27.79, 84.68, -25.02], [-68.0, 78.0, -50.0], [-68.21, 84.68, -25.02]],
    [[-68.21, 84.68, -25.02], [-68.0, 78.0, -50.0], [-73.0, 78.0, -50.0]],
    [[-68.21, 84.68, -25.02], [-73.0, 78.0, -50.0], [-73.0, 84.68, -25.02]],
    [[-73.0, 84.68, -25.02], [-68.21, 86.9, 0.0], [-68.21, 84.68, -25.02]],
    [[-73.0, 84.68, -25.02], [-73.0, 86.9, 0.0], [-68.21, 86.9, 0.0]],
    [[-23.0, 84.68, -25.02], [-28.0, 78.0, -50.0], [-27.79, 84.68, -25.02]],
    [[-23.0, 84.68, -25.02], [-23.0, 78.0, -50.0], [-28.0, 78.0, -50.0]],
    [[-27.79, 84.68, -25.02], [-23.0, 86.9, 0.0], [-23.0, 84.68, -25.02]],
    [[-27.79, 84.68, -25.02], [-27.79, 86.9, 0.0], [-23.0, 86.9, 0.0]],
    [[-68.21, 84.68, 25.02], [-68.21, 86.9, 0.0], [-73.0, 86.9, 0.0]],
    [[-68.21, 84.68, 25.02], [-73.0, 86.9, 0.0], [-73.0, 84.68, 25.02]],
    [[-73.0, 84.68, 25.02], [-68.0, 78.0, 50.0], [-68.21, 84.68, 25.02]],
    [[-73.0, 84.68, 25.02], [-73.0, 78.0, 50.0], [-68.0, 78.0, 50.0]],
    [[-27.79, 84.68, 25.02], [-23.0, 78.0, 50.0], [-23.0, 84.68, 25.02]],
    [[-27.79, 84.68, 25.02], [-28.0, 78.0, 50.0], [-23.0, 78.0, 50.0]],
    [[-23.0, 84.68, 25.02], [-27.79, 86.9, 0.0], [-27.79, 84.68, 25.02]],
    [[-23.0, 84.68, 25.02], [-23.0, 86.9, 0.0], [-27.79, 86.9, 0.0]],
    [[-68.21, 84.68, -25.02], [-27.79, 86.9, 0.0], [-27.79, 84.68, -25.02]],
    [[-68.21, 84.68, -25.02], [-68.21, 86.9, 0.0], [-27.79, 86.9, 0.0]],
    [[-27.79, 84.68, 25.02], [-27.79, 86.9, 0.0], [-68.21, 86.9, 0.0]],
    [[-27.79, 84.68, 25.02], [-68.21, 86.9, 0.0], [-68.21, 84.68, 25.02]],
    [[-68.21, 84.68, 25.02], [-28.0, 78.0, 50.0], [-27.79, 84.68, 25.02]],
    [[-68.21, 84.68, 25.02], [-68.0, 78.0, 50.0], [-28.0, 78.0, 50.0]],
]
_E03_GATE_ROOF8 = [                                            # 4 full-width quads x[-73,-23] over the z-arch profile
    [[-73.0, 78.0, -50.0], [-23.0, 78.0, -50.0], [-23.0, 84.68, -25.0], [-73.0, 84.68, -25.0]],
    [[-73.0, 84.68, -25.0], [-23.0, 84.68, -25.0], [-23.0, 86.9, 0.0], [-73.0, 86.9, 0.0]],
    [[-73.0, 86.9, 0.0], [-23.0, 86.9, 0.0], [-23.0, 84.68, 25.0], [-73.0, 84.68, 25.0]],
    [[-73.0, 84.68, 25.0], [-23.0, 84.68, 25.0], [-23.0, 78.0, 50.0], [-73.0, 78.0, 50.0]],
]


def _gate_rk(t, dx=0.0):
    return tuple(sorted((round(p[0] + dx, 1), round(p[1], 1), round(p[2], 1)) for p in t))


def _e03_gate_torch_simplify(tris):
    """obj40/obj44 (identical bridge-gate meshes): replace each of the 4 corner torch-posts (y>=77) with a full-
    height corner COLUMN — bounding cube in x/z, extended DOWN to y=0 (the deck is open below the torches: no
    solid body at mid-height, nothing inward of the centre-facing sides, so nothing gets buried). Emits top (+y)
    + all 4 vertical sides; the bottom (-y) is dropped (it would sit in the ground at y=0). Also drops the inner
    side-railings (_E03_GATE_RAIL). Corners/offset are read off the mesh's own bbox, so this works for obj44 and
    its +848-x twin obj40 alike. ~104 torch tris + 16 rail tris removed -> 40 cube tris."""
    xs = [p[0] for t in tris for p in t]
    zs = [p[2] for t in tris for p in t]
    xmin, xmax, zmin, zmax = min(xs), max(xs), min(zs), max(zs)
    dx = round(xmin - _E03_GATE_XMIN_REF)
    dropkeys = set(_gate_rk(rt, dx) for rt in _E03_GATE_RAIL + _E03_GATE_OUTER)
    raisekeys = set(_gate_rk(rt, dx) for rt in _E03_GATE_RAISE)
    extendkeys = set(_gate_rk(rt, dx) for rt in _E03_GATE_EXTEND)
    Y0, XW, ZW = 77.25, 7.0, 8.0                       # y>=77 gate geometry is torches only; XW/ZW capture one corner
    regions = []
    for sx in (0, 1):
        for sz in (0, 1):
            rx = (xmin - 0.5, xmin + XW) if sx == 0 else (xmax - XW, xmax + 0.5)
            rz = (zmin - 0.5, zmin + ZW) if sz == 0 else (zmax - ZW, zmax + 0.5)
            regions.append((rx[0], rx[1], rz[0], rz[1], sx, sz))
    keep, cap = [], [[] for _ in regions]
    for t in tris:
        k = _gate_rk(t)
        if k in dropkeys:                              # inner side-railings + obsolete outer filler walls
            continue
        if k in raisekeys:                             # raise the walkway top +8 to merge with the parapet tops
            keep.append([[p[0], p[1] + _E03_GATE_RAISE_DY, p[2]] for p in t])
            continue
        if k in extendkeys:                            # extend outer side-face up: raise its top edge (y>=65) +8
            keep.append([[p[0], p[1] + (_E03_GATE_RAISE_DY if p[1] >= 65 else 0.0), p[2]] for p in t])
            continue
        c = [sum(p[i] for p in t) / 3 for i in range(3)]
        r = None
        if c[1] >= Y0:
            for ri, (x0, x1, z0, z1, sx, sz) in enumerate(regions):
                if x0 <= c[0] <= x1 and z0 <= c[2] <= z1:
                    r = ri
                    break
        (keep if r is None else cap[r]).append(t)
    out = list(keep)
    for (x0, x1, z0, z1, sx, sz), g in zip(regions, cap):
        if not g:
            continue
        cx = [p[0] for t in g for p in t]; cy = [p[1] for t in g for p in t]; cz = [p[2] for t in g for p in t]
        X0, X1, YT, Z0, Z1, YB = min(cx), max(cx), max(cy), min(cz), max(cz), 0.0   # YT=torch top, YB=ground
        out += _dir_quad([X0, YT, Z0], [X1, YT, Z0], [X1, YT, Z1], [X0, YT, Z1], [0, 1, 0])   # top (+y)
        out += _dir_quad([X0, YB, Z0], [X0, YB, Z1], [X0, YT, Z1], [X0, YT, Z0], [-1, 0, 0])  # -x side -> y0
        out += _dir_quad([X1, YB, Z0], [X1, YB, Z1], [X1, YT, Z1], [X1, YT, Z0], [1, 0, 0])   # +x side -> y0
        out += _dir_quad([X0, YB, Z0], [X1, YB, Z0], [X1, YT, Z0], [X0, YT, Z0], [0, 0, -1])  # -z side -> y0
        out += _dir_quad([X0, YB, Z1], [X1, YB, Z1], [X1, YT, Z1], [X0, YT, Z1], [0, 0, 1])   # +z side -> y0
    # merge the 24-tri domed roof (barrel vault, flat in x) into 8: 4 full-width quads over the z-arch profile
    roofkeys = set(_gate_rk(rt, dx) for rt in _E03_GATE_ROOF24)
    out = [t for t in out if _gate_rk(t) not in roofkeys]
    for q in _E03_GATE_ROOF8:
        p = [[v[0] + dx, v[1], v[2]] for v in q]
        out += _dir_quad(p[0], p[1], p[2], p[3], [0, 1, 0])
    # close each z-end face's middle gap (between the corner gussets): ground y70 -> roof low edge y78
    for zc, wn in ((-50.0, [0, 0, -1]), (50.0, [0, 0, 1])):
        out += _dir_quad([-68.0 + dx, 70.0, zc], [-28.0 + dx, 70.0, zc],
                         [-28.0 + dx, 78.0, zc], [-68.0 + dx, 78.0, zc], wn)
    return out


def grouped_collision(placed, scn, town='e03', max_tris=100):
    """Spatially REGROUP the whole custom collision, shared by the ISO bake (bake_structures) and the viewer so
    both write / show the identical node grouping.

    All non-trigger tris of a frame are pooled and kd_split into <=max_tris nodes (nearby polys share a node).
    Player `_a` stays split per ground sub-file (each sub keeps its own collision; the town-wide hand-authored
    walls anchor on the first ground). Camera `_c` is consolidated on the single sub that ships a `_c` variant.
    Returns {'subs':[...], 'player':{sub:(named, trigs)}, 'camera':[named], 'sets':{...}} where
      named = [(name, tris)], trigs = [(name, tris, colour_entries)], and 'sets' holds the raw component tri
      lists ('structure','bwalls','perimeter','invisible') for the viewer's isolate-this-piece toggles."""
    from collections import defaultdict
    bysub = defaultdict(list)
    cam_bysub = defaultdict(list)                                  # camera structure pool, MINUS the own-node meshes
    cam_own = defaultdict(list)                                    # obj40/obj44: each ships as its OWN camera node
    own_names = {'obj40', 'obj44'} if town == 'e03' else set()    # identical ~216-tri gate meshes
    for pm in placed:
        if not is_cam_node(pm['name']):
            continue
        v = pm['verts']
        t = simplify_terrain([[list(v[a]), list(v[b]), list(v[c])] for a, b, c in pm['tris']])
        if not t:
            continue
        bysub[pm['sub']].extend(t)                                 # player pool: everything (names not needed)
        if pm['name'] in own_names:
            cam_own[pm['name']].extend(t)                          # camera: this mesh gets its own node
        else:
            cam_bysub[pm['sub']].extend(t)                         # camera: pooled structure
    subs = sorted(bysub)
    perim, bw = perimeter_wall_tris(town), both_wall_tris(town)
    inv, pw = invisible_tris(town), player_wall_tris(town)
    triggers = trigger_nodes(scn)

    # ---- PLAYER `_a`: per sub, pool everything (structure + town-wide walls on sub 0), split; triggers stay tagged
    player = {}
    for i, sub in enumerate(subs):
        used = set()
        pool = list(bysub[sub])
        if i == 0:                                                 # first ground anchors the town-wide walls
            pool += perim + bw + inv + pw
        named = _pool_split(pool, 'pcol', used, max_tris)
        trigs = [(_fit(tn, used, 15), tt, te) for tn, tt, te in triggers.get(sub, [])]
        player[sub] = (named, trigs)

    # ---- CAMERA `_c`: consolidated structure + both-walls + perimeter (NO canal/railings/triggers = player-only).
    #      One-sided: flip known-backwards windings so every camera wall's normal faces the play area (fix_camera_
    #      winding). Player `_a` above keeps the raw (two-sided) winding.
    # Camera `_c`: full-detail structure by default. Simplification is DIRECTED, not blanket — specific groups are
    # merged by cam_merge_selected (authored OUTWARD, behind the visual mesh, so the collision never clips in front
    # of the rendered wall). This keeps the runtime gather-buffer reduction targeted and reviewable one group at a
    # time, instead of one sweeping pass that's hard to vet against the visual meshes.
    cstruct = fix_camera_winding([t for sub in subs for t in cam_bysub[sub]])
    cbw, cperim = fix_camera_winding(bw), fix_camera_winding(perim)
    cext = camera_tris(town)                                       # authored camera-only (already play-area wound)
    # Directed simplification runs over the WHOLE camera pool (structure + perimeter + both-walls + authored), so a
    # job can target a wall no matter which set it happens to live in (e.g. the x=600 spine is in `perimeter`).
    cam_pool = cam_merge_selected(cstruct + cperim + cbw + cext, 'e03')
    used = set()
    # <=max_tris (100) spatially-compact ring nodes. DELIBERATELY kept small: camera `_c` is mostly the town
    # PERIMETER RING, so every kd-split bucket has a map-spanning bbox and the runtime frame-gather cull is weak.
    # The camera CCPoly gather buffer is a hard 400-poly/frame cap (no bounds check) summed across every part near
    # the camera, and NW-Queens already SATURATES it — bigger nodes coarsen the cull and blow the cap. Keep at 100.
    camera = _pool_split(cam_pool, 'ccol', used, max_tris)
    if town == 'e03':                                              # obj40/obj44: 4 corner torches -> outer cube faces
        cam_own = {nm: _e03_gate_torch_simplify(tt) for nm, tt in cam_own.items()}
    own_named = [(f'c{nm}', fix_camera_winding(tt)) for nm, tt in sorted(cam_own.items())]
    camera += [(_fit(nm, used, 15), tt) for nm, tt in own_named]   # obj40/obj44: own node each, unsplit

    sets = {'structure': cstruct + [t for _, tt in own_named for t in tt],
            'bwalls': cbw + cext, 'perimeter': cperim, 'invisible': inv + pw}
    return {'subs': subs, 'player': player, 'camera': camera, 'sets': sets}


def drop_obj48_zwrite(scn):
    """Turn the waterfall render frames' Z-WRITE back ON so they occlude the player's BODY. The obj48 falls
    (`obj48__a01z[N]`) and the taki fall's back layer (`taki2__a01z`) carry the `z` per-frame flag (SetFrameAttr
    → CFrame+0x104=0 = ZBUF.ZMSK, no depth write), so a player behind a fall draws on top of it. Replace that
    `z` with `x` (an unhandled = no-op flag letter) in each — keeps the alpha-test (`a01`) and any instance
    digit, just drops the Z-write-off. (taki1__a01a already writes Z.) NOTE: this makes the BODY occlude but
    depth-clips Toan's CLOTH cape, which draws with a different depth state — a known smaller artifact; the
    correct full fix is drawing the falls after the character, blocked by the DrawWater/refraction entanglement.
    Returns (scn_bytes, count)."""
    if not isinstance(scn, (bytes, bytearray)):
        return scn, 0
    b = bytearray(scn)
    n = 0
    for pat in (b'obj48__a01z', b'taki2__a01z'):
        zpos = len(pat) - 1
        i = 0
        while True:
            j = b.find(pat, i)
            if j < 0:
                break
            if b[j + zpos] == 0x7a:      # 'z' -> 'x' (0x78): unknown letter, SetFrameAttr skips it, Z-write stays ON
                b[j + zpos] = 0x78
                n += 1
            i = j + 1
    return bytes(b), n


def bake_structures(scene_rel, town='e03', max_tris=100, buildings=False, wall_max_ny=None, building_lod=2):
    scn = load_scene(scene_rel)
    P = placed_meshes(scene_rel, scene_rel.replace('scene.scn', 'mapinfo.cfg'))
    G = grouped_collision(P, scn, town, max_tris)

    stats, manifest = [], []
    dirnames = set(scene_placed._scndir(scn).keys())

    # ---- PLAYER `_a` per ground sub-file ----
    for sub in G['subs']:
        if sub not in dirnames:            # skip anything that isn't a real SCN sub-file (e.g. injected parts)
            continue
        named, trigs = G['player'][sub]
        for mn, bk in named:
            manifest.append((sub, 'player', mn, len(bk), 'shared'))
        for mn, tt, _te in trigs:
            manifest.append((sub, mn, mn, len(tt), 'trigger'))
        allnodes = list(named) + [(mn, tt, te) for mn, tt, te in trigs]
        mds = build_flat_mds(allnodes)
        scn, delta = _replace_a_block(scn, sub, mds)
        tris_ct = sum(len(bk) for _, bk in named) + sum(len(tt) for _, tt, _ in trigs)
        stats.append((sub, len(allnodes), len(allnodes), 0, tris_ct, 0, delta))

    # ---- CAMERA `_c`: consolidated on the first ground that ships a `_c` variant (origin-placed, world==local;
    #      e03g05 has no `_c`). The town camera reads this via the native camera frame (+0xdc), so the mod must
    #      NOT alias camera=player. Buildings keep their vanilla `_a`/`_c`.
    cam_named = G['camera']
    DIRmap = scene_placed._scndir(scn)
    cam_host = next((s for s in G['subs'] if s in DIRmap
                     and _variant_off(scn[DIRmap[s][0]:DIRmap[s][0] + DIRmap[s][1]], s, '_c') is not None), None)
    if cam_host and cam_named:
        cam_mds = build_flat_mds(cam_named)
        scn, cdelta = _replace_a_block(scn, cam_host, cam_mds, suffix='_c')
        for _mn, _bk in cam_named:
            manifest.append((cam_host, 'camera', _mn, len(_bk), 'camera'))
        stats.append((cam_host + '_c', len(cam_named), len(cam_named), 0, sum(len(e[1]) for e in cam_named), 0, cdelta))

    # ---- BUILDINGS (default OFF): replace each e03h* `_a` with its wall silhouette split into <=max_tris nodes, in
    #      BUILDING-LOCAL space so the georama part placement moves it at runtime (see building_collision_nodes).
    if buildings:
        for sub, named in building_collision_nodes(scn, max_tris, wall_max_ny, building_lod).items():
            if sub not in dirnames:
                continue
            for mn, bk in named:
                manifest.append((sub, 'building', mn, len(bk), 'building'))
            mds = build_flat_mds(named)
            scn, delta = _replace_a_block(scn, sub, mds)
            tris_ct = sum(len(bk) for _, bk in named)
            stats.append((sub, 1, len(named), 0, tris_ct, 0, delta))

    # Waterfalls Z-write ON so the player's BODY occludes behind them. This depth-clips Toan's CLOTH cape (a
    # known smaller artifact); the full fix (draw falls after the character) is blocked by the DrawWater/
    # refraction entanglement — see drop_obj48_zwrite. To revert: comment the two lines below.
    if town == 'e03':
        scn, _z = drop_obj48_zwrite(scn)
    return scn, stats, manifest


def bake_structures_from_bytes(scene_rel, scene_bytes, mapinfo_bytes=None, town='e03', max_tris=100):
    """bake_structures with the scene (and optionally mapinfo) supplied as bytes rather than read from disk —
    for baking straight out of an ISO."""
    import extract_scene_mesh as esm
    mapinfo_rel = scene_rel.replace('scene.scn', 'mapinfo.cfg')

    def patched(rel, _o=esm.load_scene):
        if rel == scene_rel:
            return scene_bytes
        if mapinfo_bytes is not None and rel == mapinfo_rel:
            return mapinfo_bytes
        return _o(rel)

    # Patch load_scene on every module that resolves it: esm (source), scene_placed / gc (which imported it),
    # AND THIS module's own binding — bake_structures calls the bare `load_scene(scene_rel)` it imported locally,
    # so without patching our own global the direct call would still read the disc (and require DC1_DATA_DIR).
    g = globals()
    saved = (esm.load_scene, scene_placed.load_scene, gc.load_scene, g['load_scene'])
    esm.load_scene = scene_placed.load_scene = gc.load_scene = g['load_scene'] = patched
    try:
        return bake_structures(scene_rel, town, max_tris)
    finally:
        esm.load_scene, scene_placed.load_scene, gc.load_scene, g['load_scene'] = saved


if __name__ == '__main__':
    scene = sys.argv[1] if len(sys.argv) > 1 else 'gedit/e03/scene.scn'
    new_scn, stats, manifest = bake_structures(scene)
    grew = sum(s[-1] for s in stats)
    print(f"shared-collision bake: {len(stats)} sub-files, scene grew {grew:+} bytes")
    for sub, ns, cn, iv, ct, it, d in stats:
        print(f"  {sub}: {cn} collision nodes ({ct} tris) from {ns} sources  (Δ{d:+})")
    # validate: within-sub-file name uniqueness (flat _a, no camcol/aroot)
    DIR = scene_placed._scndir(new_scn)
    for g in ('e03g04', 'e03g05'):
        off, size = DIR[g]; sub = new_scn[off:off + size]; vo = gc._variant_a(sub, g); mds = off + vo
        cnt, tbl = struct.unpack_from('<II', new_scn, mds + 8)
        names = [new_scn[mds + tbl + i * 0x70 + 8:mds + tbl + i * 0x70 + 8 + 16].split(b'\x00')[0].decode('latin1') for i in range(cnt)]
        print(f"  {g}_a: {cnt} nodes, names-unique={len(set(names)) == cnt}")
