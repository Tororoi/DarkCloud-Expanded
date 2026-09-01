#!/usr/bin/env python3
"""Simplified vertical camera-collision hulls for the Yellow Drops torii-gate pillars (baked),
plus the doumu_c factory-wall hug shared with the ISO bake.

Targets: the 2 INNER extru12 torii gates (s1303 / s1304; the s1313/s1314 ones are far outside the
play area). Per leg: collect visual verts in the leg's ground-footprint band, take the xz convex
hull, simplify to <= HULL_N points (angle-binned, keeping the most distant point per bin so
extents survive), pad outward by PILLAR_PAD, Chaikin-round, and emit vertical wall quads from
Y_LO to Y_HI (the miti_c camera-wall convention). (The 4 iriguti factory-entrance arches were
dropped 2026-08-31 per user review — not beneficial in-game; code deleted, recoverable via git.)

pillar_hulls() -> {label: {'tris': [...], 'foot': [(x,z)...]}} — one entry per gate instance.
"""
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from scene_placed import placed_meshes

PILLAR_PAD = 8.0          # outward padding of the hull beyond the visual footprint
HULL_N = 8                # max hull points per leg
Y_LO, Y_HI = -10.0, 130.0 # camera wall span (matches the vanilla miti_c walls)
# vert band used for the footprint: the extru legs are straight, so the true ground FOOTPRINT is
# the right base.
BANDS = {'extru': (20.0, 45.0)}

_TARGETS = [   # (label, mesh-name prefix, sub, inst)
    ('extru_inner_S', 'extru', 's1303', 0),
    ('extru_inner_N', 'extru', 's1304', 0),
]


def _cluster2(pts):
    """Split the leg verts into the two end legs: project onto the cloud's principal axis and
    split at the widest gap in the sorted projections (legs sit at the ends, air between)."""
    n = len(pts)
    cx = sum(p[0] for p in pts) / n
    cz = sum(p[1] for p in pts) / n
    sxx = sum((p[0]-cx)**2 for p in pts); szz = sum((p[1]-cz)**2 for p in pts)
    sxz = sum((p[0]-cx)*(p[1]-cz) for p in pts)
    ang = 0.5 * math.atan2(2*sxz, sxx - szz)
    ax, az = math.cos(ang), math.sin(ang)
    proj = sorted((p[0]*ax + p[1]*az, p) for p in pts)
    gap_i, gap = 0, -1.0
    for i in range(n // 4, 3 * n // 4):            # the split gap lives in the middle half
        d = proj[i+1][0] - proj[i][0]
        if d > gap:
            gap, gap_i = d, i
    return [[p for _, p in proj[:gap_i+1]], [p for _, p in proj[gap_i+1:]]]


def _hull(pts):
    pts = sorted(set(pts))
    if len(pts) < 3:
        return list(pts)

    def half(seq):
        h = []
        for p in seq:
            while len(h) >= 2 and (h[-1][0]-h[-2][0])*(p[1]-h[-2][1]) - (h[-1][1]-h[-2][1])*(p[0]-h[-2][0]) <= 0:
                h.pop()
            h.append(p)
        return h
    lo, hi = half(pts), half(pts[::-1])
    return lo[:-1] + hi[:-1]


def _simplify(hull, n):
    if len(hull) <= n:
        return hull
    cx = sum(p[0] for p in hull) / len(hull)
    cz = sum(p[1] for p in hull) / len(hull)
    bins = {}
    for p in hull:
        b = int((math.atan2(p[1]-cz, p[0]-cx) + math.pi) / (2*math.pi) * n) % n
        r = math.hypot(p[0]-cx, p[1]-cz)
        if b not in bins or r > bins[b][0]:
            bins[b] = (r, p)
    return [bins[b][1] for b in sorted(bins)]


def _walls(feet):
    tris = []
    for foot in feet:
        # collision planes are ONE-SIDED (PlaneNormal from vertex order); vanilla camera walls
        # face OUTWARD (toward the camera side). Orient each foot loop so the walls do too.
        n = len(foot)
        area2 = sum(foot[i][0]*foot[(i+1) % n][1] - foot[(i+1) % n][0]*foot[i][1] for i in range(n))
        loop = foot if area2 < 0 else foot[::-1]
        for a, b in zip(loop, loop[1:] + loop[:1]):
            tris.append([[a[0], Y_LO, a[1]], [b[0], Y_LO, b[1]], [b[0], Y_HI, b[1]]])
            tris.append([[a[0], Y_LO, a[1]], [b[0], Y_HI, b[1]], [a[0], Y_HI, a[1]]])
    return tris


def _chaikin(poly):
    """One corner-cutting pass (Chaikin): doubles the points, rounds the corners."""
    out = []
    n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        out.append((0.75 * a[0] + 0.25 * b[0], 0.75 * a[1] + 0.25 * b[1]))
        out.append((0.25 * a[0] + 0.75 * b[0], 0.25 * a[1] + 0.75 * b[1]))
    return out


def pillar_hulls():
    placed = placed_meshes('gedit/s13/scene.scn', 'gedit/s13/mapinfo.cfg')
    out = {}
    for label, pref, subn, inst in _TARGETS:
        verts = []
        for pm in placed:
            if pm['sub'] == subn and pm['inst'] == inst and pm['name'].startswith(pref):
                lo, hi = BANDS[pref]
                verts += [(round(v[0], 2), round(v[2], 2)) for v in pm['verts'] if lo <= v[1] <= hi]
        legs = _cluster2(verts)
        tris, feet = [], []
        for leg in legs:
            hull = _simplify(_hull(leg), HULL_N)
            cx = sum(p[0] for p in hull) / len(hull)
            cz = sum(p[1] for p in hull) / len(hull)
            padded = []
            for p in hull:
                d = math.hypot(p[0]-cx, p[1]-cz) or 1.0
                padded.append((p[0] + (p[0]-cx)/d*PILLAR_PAD, p[1] + (p[1]-cz)/d*PILLAR_PAD))
            feet.append(_chaikin(padded))                 # one round of corner-cutting -> smoother
        out[label] = {'tris': _walls(feet), 'foot': feet}
    return out


# ---- doumu_c hug: pull the factory camera wall radially in by DOUMU_PULL, but never closer than
#      DOUMU_CLEAR to the factory's own surface radius in that 10-degree sector (from sphere27
#      verts in the wall's height band). Shared by the viewer proposal AND the ISO bake.
DOUMU_PULL, DOUMU_CLEAR = 20.0, 4.0
_DOUMU_C = (-2.0, -6.0)
_dsec = None


def _doumu_sectors():
    global _dsec
    if _dsec is None:
        _dsec = [0.0] * 36
        for pm in placed_meshes('gedit/s13/scene.scn', 'gedit/s13/mapinfo.cfg'):
            if pm['name'] != 'sphere27':
                continue
            for v in pm['verts']:
                if not (20.0 <= v[1] <= 140.0):
                    continue
                r = math.hypot(v[0] - _DOUMU_C[0], v[2] - _DOUMU_C[1])
                i = int(math.degrees(math.atan2(v[2] - _DOUMU_C[1], v[0] - _DOUMU_C[0])) // 10) % 36
                for j in (i - 1, i, i + 1):
                    _dsec[j % 36] = max(_dsec[j % 36], r)
    return _dsec


def doumu_hug_xz(x, z):
    sec = _doumu_sectors()
    dx, dz = x - _DOUMU_C[0], z - _DOUMU_C[1]
    r = math.hypot(dx, dz)
    if r <= 1.0:
        return x, z
    floor_r = sec[int(math.degrees(math.atan2(dz, dx)) // 10) % 36] + DOUMU_CLEAR
    r2 = max(r - DOUMU_PULL, min(floor_r, r))
    return _DOUMU_C[0] + dx * r2 / r, _DOUMU_C[1] + dz * r2 / r


if __name__ == '__main__':
    for label, d in pillar_hulls().items():
        print(label, len(d['tris']), 'tris;', [len(f) for f in d['foot']], 'hull pts per leg')
