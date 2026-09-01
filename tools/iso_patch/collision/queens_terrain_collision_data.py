#!/usr/bin/env python3
"""All hand-authored / directed Queens (e03) collision geometry. Split out of
queens_collision_builder.py (2026-09). Sections, in dependency order (several tables are built at
import time from the raw dumps, so keep the order):
  1. BOTH-frame walls, flat-ground quads, pipe drums; player-only railings; canal invisible walls;
     the perimeter wall (formerly the both_walls / player_walls / invisible_walls / perimeter_wall modules)
  2. terrain removal regions + exact residue; camera backface-winding fixes; simplify_terrain
  3. directed camera `_c` simplification: the LIVE jobs (canal wall front/back/cap, town walls, arcade back,
     z=200 notch) + cam_merge_selected. The 2026-08 gate/arch/obj43/obj45/obj33/walkway/SE-torch customs were
     REVERTED to vanilla by user decision and their builders + raw dumps deleted 2026-09 (recoverable via git).
  4. obj40/obj44 gate-torch simplification tables + gate_torch_simplify
"""
import math
import os, sys
_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)                                     # this dir (sibling collision modules)
sys.path.insert(0, os.path.dirname(os.path.dirname(_HERE)))   # tools/ (scene_placed, georama_collision, tri_util…)
from collision_geom import (_box, _plane_x, _plane_z, _horiz, tri_key_int, tri_key_winding, triangle_normal, _dot3,
                            simplify_coplanar, _plane_region, _dir_quad, _quad)


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
# octagon, side-walled and end-capped (no hole). obj1/obj9 are dropped from is_camera_structure_node so only the drum remains.
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

CANAL_CONTAINMENT_RAW = """
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

_INVIS_DATA = {'e03': CANAL_CONTAINMENT_RAW}


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
CANAL_PRE_REMOVE_RAW = """
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


_PREMOVE = {'e03': set(tri_key_int([[float(x) for x in l.split(',')][i:i + 3] for i in (0, 3, 6)])
                       for l in CANAL_PRE_REMOVE_RAW.strip().split('\n'))}


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
    out = [w for w in walls if tri_key_int(w) not in rem]
    for a, b, h in _EXTRA.get(town, []):                              # extra railings: base edge extruded up
        a0, b0 = list(a), list(b)
        a1 = [a[0], a[1] + h, a[2]]; b1 = [b[0], b[1] + h, b[2]]
        out.append([a0, b0, b1]); out.append([a0, b1, a1])
    return out


# ==========================================================================
# Terrain simplification — drop the game's structure-collision tris that a
# hand-authored wall/quad above now stands in for (so they're never emitted).
# ==========================================================================
# Removal REGIONS (authored bounds), replacing a former 528-entry list of exact game vertices. Each region drops
# the collision of a structure a hand-authored piece replaces. Where a box would also cover KEPT geometry (a floor
# sharing the footprint), tris are matched by their own PLANE (all verts coplanar) or restricted to horizontal
# faces, so a broad box never deletes a neighbour. Verified to remove EXACTLY the old set — only 6 irregular
# corner/scatter tris need the exact residue below.
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


# The handful of irregular corner/scatter tris no clean region captures without also deleting a kept neighbour.
CANAL_REMOVE_RESIDUE_RAW = """
600,270,1300, 600,366,1200, 600,262,1200
650,370,1300, 600,370,1300, 600,370,1400
650,370,1300, 600,370,1400, 650,370,1400
-400,270,150, -400,267,50, -500,270,50
-200,50,50, -600,0,50, -600,50,50
-200,50,50, -200,0,50, -600,0,50
"""

_RESIDUE = set(tri_key_int([[float(x) for x in ln.split(',')][i:i + 3] for i in (0, 3, 6)])
               for ln in CANAL_REMOVE_RESIDUE_RAW.strip().split('\n') if ln.strip())


# ── One-sided CAMERA collision: these baked camera-wall tris are wound so their normal faces AWAY from the play
#    area. Under the backface-culled player→camera raycast (the mod's camera reads `_c` one-sided now) that lets the
#    ray pass straight through them instead of being blocked. Flip their winding so the normal faces the play area.
#    Applied to the CAMERA path + viewer sets only; player `_a` collision is two-sided (CheckHit) so it's unchanged.
CANAL_BACKFACE_RAW = """
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


_BACKFACE_KEYS = {tri_key_winding([[float(x) for x in ln.split(',')][i:i + 3] for i in (0, 3, 6)])
                  for ln in CANAL_BACKFACE_RAW.strip().split('\n') if ln.strip()}


def fix_camera_winding(tris):
    """Reverse the winding of any tri whose winding matches a known-backwards camera wall (normal faces the play
    area afterward). Returns a new list; non-matching tris pass through unchanged."""
    return [[t[0], t[2], t[1]] if tri_key_winding(t) in _BACKFACE_KEYS else t for t in tris]


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
            and not _in_remove_region(t) and tri_key_int(t) not in _RESIDUE
            and not _in_flat_region(t) and not _canal_wall(t)]


# ── DIRECTED camera-mesh simplification ────────────────────────────────────────────────────────────────────
# Each job selects a subset of camera `_c` tris (by axis-aligned world box + optional plane-facing test) and runs
# simplify_coplanar over ONLY that subset, authored OUTWARD so the merged wall sits behind the visual mesh. Jobs
# are added one at a time as they're reviewed — nothing merges until it's listed here. `sel` is a predicate on a
# tri; `outward`/`snap` tune the merge for that group.


TOWN_WALL_RAW = """-400,170,200,-400,70,200,-300,70,200
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
TOWN_WALL_KEYS = set(tri_key_int([[float(x) for x in ln.split(',')][i:i+3] for i in (0, 3, 6)])
                         for ln in TOWN_WALL_RAW.strip().split('\n'))


# Arcade back-side geometry (x[-500,-400]) now hidden behind the solid x=-400 wall -> removed outright.
ARCADE_BACK_RAW = """-400,170,150,-400,70,150,-400,70,50
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
ARCADE_BACK_KEYS = set(tri_key_int([[float(x) for x in ln.split(',')][i:i+3] for i in (0, 3, 6)])
                        for ln in ARCADE_BACK_RAW.strip().split('\n'))


def _e03_arcback_sel(t):
    return tri_key_int(t) in ARCADE_BACK_KEYS


def _e03_townwall_sel(t):
    return tri_key_int(t) in TOWN_WALL_KEYS


def _e03_townwall_tris():
    """Vertical faces run through simplify_coplanar (each kept on-plane, flattened in Y, but the archway/gate HOLES
    preserved — the flatten fills only from each column's lowest wall cell up, so base openings stay open) + the 3
    flat walkway tops. Holes: e.g. a 5-arch arcade in the x=-400 wall, so this is a bit above 36."""
    raw = [[float(x) for x in ln.split(',')] for ln in TOWN_WALL_RAW.strip().split('\n')]
    allt = [[r[0:3], r[3:6], r[6:9]] for r in raw]
    vert = [t for t in allt if abs(triangle_normal(t)[1]) / (math.sqrt(_dot3(triangle_normal(t), triangle_normal(t))) or 1.0) < 0.5]
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
                                  and triangle_normal(t)[2] > 0)]
    out += _dir_quad([0, 0, 200], [600, 0, 200], [600, 280, 200], [0, 280, 200], [0, 0, 1])
    # flatten the z=200 (-z) wall's staircase at x[600,1300] to one flat quad y[70,380]
    out = [t for t in out if not (all(abs(p[2] - 200) < 1 and 599 <= p[0] <= 1301 and 69 <= p[1] <= 381 for p in t)
                                  and triangle_normal(t)[2] < 0)]
    out += _dir_quad([600, 70, 200], [1300, 70, 200], [1300, 380, 200], [600, 380, 200], [0, 0, -1])
    # (REVERTED 2026-08 with the obj33 customs: the x=695 return stays, the obj33-bridge/cap quads are
    #  gone, and the z=200/z=250 x[1400,1500] + x=1500 z[-100,200] wall sections are KEPT — they were
    #  only droppable while obj33's custom extensions covered them.)
    # USER-DIRECTED (2026-08): the restored x[1400,1500] wall sections came out of the forced-380
    # simplify 10 units ABOVE the obj33 roof ledge (horizontal y=370, x[1500,1600]) — cap them at 370
    # and close the top with a y=370 lid over the rampart section so the ledge line runs flush through.
    def _x1400sec(t, zplane, nsign):
        n = triangle_normal(t)
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


Z200_NOTCH_KEYS = set(tri_key_int([[float(x) for x in ln.split(',')][i:i+3] for i in (0, 3, 6)])
                          for ln in """695,70,200,695,170,200,700,70,200
695,170,200,700,70,200,700,170,200
695,170,200,695,270,200,700,270,200
695,170,200,700,170,200,700,270,200""".strip().split("\n"))


def _e03_z200notch_sel(t):
    return tri_key_int(t) in Z200_NOTCH_KEYS


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
            and triangle_normal(t)[1] > 0)


# WALL_TOP: shared top height for the whole z=-100/-150 canal wall structure. = the tallest merged wall top
# (the z=-100 front, 280) so closing only RAISES the back wall (275->280) and the cap (max 277.61 -> 280) — never
# lowers a wall or reduces the cap's peak, per the directive.
CANAL_WALL_TOP_Y = 280.0

_CAM_MERGE_JOBS = {
    'e03': [
        # Directed, one group at a time; empty list = full-detail camera `_c`. 'merge' runs simplify_coplanar on
        # the selected tris (authored OUTWARD, optional shared `top`); 'replace' swaps them for authored `tris`.
        # South-back canal wall z=-150, x[-400,900], flattened to the shared WALL_TOP: 48 tris -> 4 (x[200,300] gap).
        {'kind': 'merge', 'sel': _plane_region(2, -150, x=(-400, 900), y=(55, 290)),
         'snap': 5.0, 'outward': 0.0, 'top': CANAL_WALL_TOP_Y},
        # North-front canal face z=-100: wall x[-400,900] + end towers x[900,1500] -> span A (x[-400,200]) +
        # span B (x[300,1500]), both y[70,WALL_TOP]. 69 tris -> 4; x[200,300] passage gap kept, span B lined up
        # with the cap's front edge. (Tower rising base y98..170 is filled down to y70 to make span B one quad.)
        {'kind': 'replace', 'sel': _e03_north_face,
         'tris': _quad([-400, 70, -100], [200, 70, -100], [200, CANAL_WALL_TOP_Y, -100], [-400, CANAL_WALL_TOP_Y, -100])
               + _quad([300, 70, -100], [1500, 70, -100], [1500, CANAL_WALL_TOP_Y, -100], [300, CANAL_WALL_TOP_Y, -100])},
        # Top cap connecting the two faces (36 bumpy tris) -> one flat up-facing quad at WALL_TOP spanning
        # z[-150,-100], x[-400,1500], so it closes flush with both raised wall tops.
        {'kind': 'replace', 'sel': _e03_wall_cap,
         'tris': _quad([-400, CANAL_WALL_TOP_Y, -100], [1500, CANAL_WALL_TOP_Y, -100],
                       [1500, CANAL_WALL_TOP_Y, -150], [-400, CANAL_WALL_TOP_Y, -150])},
        # (obj34 pylons/gable, obj43, obj45, obj33 and the walkway raise: REVERTED 2026-08 to vanilla `_c`;
        #  their custom builders were deleted 2026-09 — see the module docstring.)
        # Remaining major town walls (350 tris) -> 24 (9 vertical faces on-plane + 3 walkway tops).
        {'kind': 'replace', 'sel': _e03_townwall_sel, 'tris': _e03_townwall_tris()},
        # Arcade back-side (x[-500,-400]) hidden behind the solid wall -> removed.
        {'kind': 'replace', 'sel': _e03_arcback_sel, 'tris': []},
        # obj33 arch 1 (single -x facade + torches simplified, passage kept).
        # grid3 canal-wall notch at x[695,700] z=200 -> covered by the flattened z=200 wall.
        {'kind': 'replace', 'sel': _e03_z200notch_sel, 'tris': []},
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


GATE_X_MIN_REF = -73.93                                    # obj44's min x; obj40 is the same mesh at +848 x
GATE_RAILING_TRIS = [                                             # inner side-railings (mid wall + corner gussets) to drop
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


GATE_WALKWAY_RAISE_DY = 8.0
GATE_WALKWAY_RAISE_TRIS = [                                            # walkway-top surface: raise +8 to meet the parapet tops
    [[-68.21, 76.68, 25.02], [-68.0, 70.0, 50.0], [-28.0, 70.0, 50.0]],
    [[-68.21, 76.68, 25.02], [-28.0, 70.0, 50.0], [-27.79, 76.68, 25.02]],
    [[-27.79, 76.68, 25.02], [-68.21, 78.9, 0.0], [-68.21, 76.68, 25.02]],
    [[-68.21, 76.68, -25.02], [-68.21, 78.9, 0.0], [-27.79, 78.9, 0.0]],
    [[-27.79, 76.68, 25.02], [-27.79, 78.9, 0.0], [-68.21, 78.9, 0.0]],
    [[-68.21, 76.68, -25.02], [-27.79, 78.9, 0.0], [-27.79, 76.68, -25.02]],
    [[-27.79, 76.68, -25.02], [-68.0, 70.0, -50.0], [-68.21, 76.68, -25.02]],
    [[-27.79, 76.68, -25.02], [-28.0, 70.0, -50.0], [-68.0, 70.0, -50.0]],
]
GATE_OUTER_FACE_EXTEND_TRIS = [                                           # outer side-face: raise its top edge (verts y>=65) +8
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
GATE_OUTER_FILLER_TRIS = [                                            # vertical filler walls (x=-73/-23, y70..86.9) now obsolete
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
GATE_ROOF_24_TRIS = [                                           # the 24-tri domed roof (barrel vault) -> merge to 8
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
GATE_ROOF_8_TRIS = [                                            # 4 full-width quads x[-73,-23] over the z-arch profile
    [[-73.0, 78.0, -50.0], [-23.0, 78.0, -50.0], [-23.0, 84.68, -25.0], [-73.0, 84.68, -25.0]],
    [[-73.0, 84.68, -25.0], [-23.0, 84.68, -25.0], [-23.0, 86.9, 0.0], [-73.0, 86.9, 0.0]],
    [[-73.0, 86.9, 0.0], [-23.0, 86.9, 0.0], [-23.0, 84.68, 25.0], [-73.0, 84.68, 25.0]],
    [[-73.0, 84.68, 25.0], [-23.0, 84.68, 25.0], [-23.0, 78.0, 50.0], [-73.0, 78.0, 50.0]],
]


def bridge_gate_tri_key(t, dx=0.0):
    return tuple(sorted((round(p[0] + dx, 1), round(p[1], 1), round(p[2], 1)) for p in t))


def gate_torch_simplify(tris):
    """obj40/obj44 (identical bridge-gate meshes): replace each of the 4 corner torch-posts (y>=77) with a full-
    height corner COLUMN — bounding cube in x/z, extended DOWN to y=0 (the deck is open below the torches: no
    solid body at mid-height, nothing inward of the centre-facing sides, so nothing gets buried). Emits top (+y)
    + all 4 vertical sides; the bottom (-y) is dropped (it would sit in the ground at y=0). Also drops the inner
    side-railings (GATE_RAILING_TRIS). Corners/offset are read off the mesh's own bbox, so this works for obj44 and
    its +848-x twin obj40 alike. ~104 torch tris + 16 rail tris removed -> 40 cube tris."""
    xs = [p[0] for t in tris for p in t]
    zs = [p[2] for t in tris for p in t]
    xmin, xmax, zmin, zmax = min(xs), max(xs), min(zs), max(zs)
    dx = round(xmin - GATE_X_MIN_REF)
    dropkeys = set(bridge_gate_tri_key(rt, dx) for rt in GATE_RAILING_TRIS + GATE_OUTER_FILLER_TRIS)
    raisekeys = set(bridge_gate_tri_key(rt, dx) for rt in GATE_WALKWAY_RAISE_TRIS)
    extendkeys = set(bridge_gate_tri_key(rt, dx) for rt in GATE_OUTER_FACE_EXTEND_TRIS)
    Y0, XW, ZW = 77.25, 7.0, 8.0                       # y>=77 gate geometry is torches only; XW/ZW capture one corner
    regions = []
    for sx in (0, 1):
        for sz in (0, 1):
            rx = (xmin - 0.5, xmin + XW) if sx == 0 else (xmax - XW, xmax + 0.5)
            rz = (zmin - 0.5, zmin + ZW) if sz == 0 else (zmax - ZW, zmax + 0.5)
            regions.append((rx[0], rx[1], rz[0], rz[1], sx, sz))
    keep, cap = [], [[] for _ in regions]
    for t in tris:
        k = bridge_gate_tri_key(t)
        if k in dropkeys:                              # inner side-railings + obsolete outer filler walls
            continue
        if k in raisekeys:                             # raise the walkway top +8 to merge with the parapet tops
            keep.append([[p[0], p[1] + GATE_WALKWAY_RAISE_DY, p[2]] for p in t])
            continue
        if k in extendkeys:                            # extend outer side-face up: raise its top edge (y>=65) +8
            keep.append([[p[0], p[1] + (GATE_WALKWAY_RAISE_DY if p[1] >= 65 else 0.0), p[2]] for p in t])
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
    roofkeys = set(bridge_gate_tri_key(rt, dx) for rt in GATE_ROOF_24_TRIS)
    out = [t for t in out if bridge_gate_tri_key(t) not in roofkeys]
    for q in GATE_ROOF_8_TRIS:
        p = [[v[0] + dx, v[1], v[2]] for v in q]
        out += _dir_quad(p[0], p[1], p[2], p[3], [0, 1, 0])
    # close each z-end face's middle gap (between the corner gussets): ground y70 -> roof low edge y78
    for zc, wn in ((-50.0, [0, 0, -1]), (50.0, [0, 0, 1])):
        out += _dir_quad([-68.0 + dx, 70.0, zc], [-28.0 + dx, 70.0, zc],
                         [-28.0 + dx, 78.0, zc], [-68.0 + dx, 78.0, zc], wn)
    return out
