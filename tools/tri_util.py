#!/usr/bin/env python3
"""Shared triangle-soup utilities (consolidated 2026-09 from three per-tool copies).

kd_split unifies the three former implementations exactly — callers keep their proven node
layouts by picking the same knobs they always used:
  bake_player_camera_collision.kd_split   -> kd_split(tris, 100)                       (3-axis median)
  brownboo_camera_collision._kd_split     -> kd_split(tris, 100, proportional=True)
  queens_hcam.split_tris                  -> kd_split(tris, 200, axes=(0, 2))          (xz median)
"""
import math


def kd_split(tris, max_tris, axes=(0, 1, 2), proportional=False):
    """Recursively split the triangle soup along its longest centroid axis (of `axes`) until each
    leaf <= max_tris. Median split (default) gives balanced power-of-two leaves (can land at
    ~max/2); proportional hands each side its ceil(n/max) leaf share, so leaves sit just under
    max_tris."""
    def cen(t):
        return ((t[0][0] + t[1][0] + t[2][0]) / 3, (t[0][1] + t[1][1] + t[2][1]) / 3,
                (t[0][2] + t[1][2] + t[2][2]) / 3)

    def rec(ts):
        if proportional:
            k = math.ceil(len(ts) / max_tris)
            if k <= 1:
                return [ts]
        elif len(ts) <= max_tris:
            return [ts]
        cs = [cen(t) for t in ts]
        axis = max(axes, key=lambda a: max(c[a] for c in cs) - min(c[a] for c in cs))
        order = sorted(range(len(ts)), key=lambda i: cs[i][axis])
        mid = round(len(ts) * (k // 2) / k) if proportional else len(ts) // 2
        return rec([ts[i] for i in order[:mid]]) + rec([ts[i] for i in order[mid:]])

    return rec(list(tris))


def chaikin(poly):
    """One corner-cutting pass (Chaikin) on a closed xz polygon: doubles the points, rounds the
    corners."""
    out = []
    n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        out.append((0.75 * a[0] + 0.25 * b[0], 0.75 * a[1] + 0.25 * b[1]))
        out.append((0.25 * a[0] + 0.75 * b[0], 0.25 * a[1] + 0.75 * b[1]))
    return out
