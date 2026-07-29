#!/usr/bin/env python3
"""Build an invisible PERIMETER WALL (player-only collision) from a reference outline. The reference tris
form an enclosed strip tracing the town edge; we take its OUTER boundary loop (in XZ, at the tris' y) and
extrude a vertical wall along it so the player can't leave the town. These walls don't exist in the game —
they're generated here. Used by tools/bake_structure_camera_collision.py (player-only, like invisible_walls).

perimeter_wall_tris(town, below, above) -> [ [[x,y,z]*3], ... ]  vertical wall quads (2 tris/edge)
"""
import math

# Reference outline (enclosed strip along the town perimeter); NOT used as collision directly.
_REF_E03 = """
-500,270,-1100, -400,270,-1000, -400,270,-1100
-400,270,-1100, -300,276,-1000, -300,277,-1100
-300,277,-1100, -200,271,-1000, -200,276,-1100
-200,276,-1100, -100,261.63,-1000, -100,261.63,-1100
-100,261.63,-1100, 0,267,-1000, 0,270,-1100
0,270,-1100, 100,270,-1000, 100,264,-1100
100,264,-1100, 200,270,-1000, 200,270,-1100
200,270,-1100, 300,270,-1000, 300,263,-1100
300,263,-1100, 400,264,-1000, 400,264,-1100
400,264,-1100, 500,270,-1000, 500,270,-1100
500,270,-1100, 600,266,-1000, 600,270,-1100
600,270,-1100, 700,274,-1000, 700,275,-1100
700,275,-1100, 800,270,-1000, 800,274,-1100
800,274,-1100, 900,270,-1000, 900,270,-1100
900,270,-1100, 1000,270,-1000, 1000,270,-1100
1000,269,-900, 1000,270,-1000, 900,270,-1000
900,266,-900, 1000,266,-800, 1000,269,-900
900,270,-800, 1000,275,-700, 1000,266,-800
900,271.24,-700, 1000,274,-600, 1000,275,-700
900,275,-600, 1000,275.28,-500, 1000,274,-600
900,275.28,-500, 1000,270,-400, 1000,275.28,-500
900,265,-400, 1000,265,-300, 1000,270,-400
900,263,-300, 1000,270,-200, 1000,265,-300
1000,270,-150, 1000,270,-200, 900,270,-200
-500,270,-1100, -500,270,-1000, -400,270,-1000
-400,274,-900, -500,270,-1000, -500,273,-900
-500,273,-900, -500,275.2,-800, -400,272,-800
-500,275.2,-800, -500,270,-700, -400,269,-700
-500,270,-700, -500,270,-600, -400,270,-600
-500,270,-600, -500,273,-500, -400,273,-500
-500,273,-500, -500,270,-400, -400,274,-400
-500,270,-400, -500,270,-300, -400,270,-300
-500,270,-300, -500,273,-200, -400,272,-200
-500,273,-200, -500,270,-100, -400,270,-100
-500,270,50, -500,270,200, -400,270,200
-500,270,200, -500,270,300, -400,270,300
-500,270,300, -500,270,400, -400,270,400
-500,270,400, -500,270,500, -400,270,500
-500,270,600, -500,270,700, -400,270,700
-500,270,700, -500,270,800, -400,270,800
-400,270,900, -500,270,800, -500,270,900
-400,270,1000, -500,270,900, -500,270,1000
-400,270,1100, -500,270,1000, -500,270,1100
-400,270,1200, -500,270,1100, -500,270,1200
-500,270,1200, -500,270,1300, -400,270,1300
-500,270,1300, -500,270,1400, -400,270,1400
-400,270,1300, -400,270,1400, -300,270,1400
-300,270,1300, -300,270,1400, -200,270,1400
-200,270,1300, -200,270,1400, -100,270,1400
-100,270,1300, -100,270,1400, 0,270,1400
0,270,1300, 0,270,1400, 100,270,1400
200,295,1300, 100,270,1400, 200,295,1400
200,295,1400, 300,320,1400, 300,320,1300
300,320,1400, 400,345,1400, 400,345,1300
500,357.5,1300, 400,345,1400, 500,357.5,1400
500,357.5,1400, 600,370,1400, 600,370,1300
650,370,1300, 600,370,1400, 650,370,1400
650,370,1300, 650,370,1400, 700,370,1400
700,370,1400, 800,370,1400, 800,370,1300
800,370,1300, 800,370,1400, 900,370,1400
900,370,1300, 900,370,1400, 1000,370,1400
1000,370,1300, 1000,370,1400, 1100,370,1400
1100,370,1300, 1100,370,1400, 1200,370,1400
1300,370,1300, 1200,370,1400, 1300,370,1400
1400,370,1300, 1300,370,1400, 1400,370,1400
1400,370,1400, 1500,370,1400, 1500,370,1300
1500,370,1300, 1500,370,1400, 1600,370,1400
1500,370,1300, 1600,370,1400, 1600,370,1300
1500,370,1200, 1600,370,1300, 1600,370,1200
1600,370,1200, 1600,370,1100, 1500,370,1100
1600,370,1100, 1600,370,1000, 1500,370,1000
1600,370,1000, 1600,370,900, 1500,370,900
1600,370,900, 1600,370,800, 1500,370,800
1500,370,700, 1600,370,800, 1600,370,700
1500,370,600, 1600,370,700, 1600,370,600
1500,370,500, 1600,370,600, 1600,370,500
1500,370,400, 1600,370,500, 1600,370,400
1500,370,300, 1600,370,400, 1600,370,300
1600,370,300, 1600,370,250, 1500,370,250
1600,370,250, 1600,370,200, 1500,370,200
1500,370,100, 1600,370,200, 1600,370,100
1600,370,-50, 1600,370,-100, 1500,370,-100
1500,370,-100, 1600,370,0, 1600,370,-50
1600,370,-100, 1500,270,-150, 1500,270,-100
1500,270,-150, 1400,270,-150, 1500,270,-100
1400,270,-150, 1300,277,-150, 1300,277,-100
1300,277,-150, 1200,267,-150, 1200,270,-100
1200,267,-150, 1100,270,-150, 1100,270,-100
1100,270,-150, 1000,270,-150, 1000,274,-100
-386.87,325,0, -513.13,270,-104.01, -513.13,325,0
-513.13,325,0, -513.13,270,104.01, -386.87,270,104.01
-386.87,325,552, -513.13,270,447.99, -513.13,325,552
-513.13,325,552, -513.13,270,653.01, -386.87,270,653.01
1622.13,447,50, 1622.13,376,-54.01, 1495.87,376,-54.01
1495.87,447,50, 1622.13,376,154.01, 1622.13,447,50
"""

_DATA = {'e03': _REF_E03}

# Wall segments the centroid heuristic mis-classified as outer (cross/inner edges). Given as wall tris; we
# extract each one's footprint edge and exclude it.
_BLACKLIST_E03 = """
-400,324,-900, -500,320,-1000, -500,70,-1000
-400,324,-900, -500,70,-1000, -400,74,-900
-400,322,-800, -500,323,-900, -500,73,-900
-400,322,-800, -500,73,-900, -400,72,-800
-500,320,800, -400,320,800, -400,70,800
-500,320,800, -400,70,800, -500,70,800
-500,320,1000, -400,320,1000, -400,70,1000
-500,320,1000, -400,70,1000, -500,70,1000
-500,320,1200, -400,320,1200, -400,70,1200
-500,320,1200, -400,70,1200, -500,70,1200
-500,320,1100, -400,70,1100, -500,70,1100
-500,320,1100, -400,320,1100, -400,70,1100
-500,320,900, -400,70,900, -500,70,900
-500,320,900, -400,320,900, -400,70,900
-500,320,1300, -400,320,1300, -400,70,1300
-500,320,1300, -400,70,1300, -500,70,1300
-400,320,1300, -400,70,1400, -400,70,1300
-400,320,1300, -400,320,1400, -400,70,1400
-300,320,1300, -300,70,1400, -300,70,1300
-300,320,1300, -300,320,1400, -300,70,1400
-200,320,1300, -200,320,1400, -200,70,1400
-200,320,1300, -200,70,1400, -200,70,1300
1300,420,1400, 1300,420,1300, 1300,170,1300
1300,420,1400, 1300,170,1300, 1300,170,1400
1400,420,1400, 1400,170,1300, 1400,170,1400
1400,420,1400, 1400,420,1300, 1400,170,1300
1000,320,-1000, 900,70,-1000, 1000,70,-1000
1000,320,-1000, 900,320,-1000, 900,70,-1000
1000,319,-900, 900,316,-900, 900,66,-900
1000,319,-900, 900,66,-900, 1000,69,-900
1000,316,-800, 900,320,-800, 900,70,-800
1000,316,-800, 900,70,-800, 1000,66,-800
1000,325,-700, 900,321.24,-700, 900,71.24,-700
1000,325,-700, 900,71.24,-700, 1000,75,-700
1000,324,-600, 900,75,-600, 1000,74,-600
1000,324,-600, 900,325,-600, 900,75,-600
1000,325.28,-500, 900,325.28,-500, 900,75.28,-500
1000,325.28,-500, 900,75.28,-500, 1000,75.28,-500
1000,320,-400, 900,65,-400, 1000,70,-400
1000,320,-400, 900,315,-400, 900,65,-400
1000,315,-300, 900,313,-300, 900,63,-300
1000,315,-300, 900,63,-300, 1000,65,-300
1000,324,-100, 1100,320,-150, 1100,70,-150
1000,324,-100, 1100,70,-150, 1000,74,-100
1100,320,-100, 1200,317,-150, 1200,67,-150
1100,320,-100, 1200,67,-150, 1100,70,-100
1300,327,-100, 1400,320,-150, 1400,70,-150
1200,320,-100, 1300,77,-150, 1200,70,-100
1200,320,-100, 1300,327,-150, 1300,77,-150
1300,327,-100, 1400,70,-150, 1300,77,-100
1622.1,497,50, 1622.1,426,-54, 1622.1,176,-54
1622.1,497,50, 1622.1,176,154, 1622.1,247,50
1622.1,497,50, 1622.1,176,-54, 1622.1,247,50
1622.1,497,50, 1622.1,426,154, 1622.1,176,154
-513.1,375,0, -513.1,320,-104, -513.1,70,-104
-513.1,375,0, -513.1,70,-104, -513.1,125,0
-513.1,375,0, -513.1,70,104, -513.1,125,0
-513.1,375,0, -513.1,320,104, -513.1,70,104
-513.1,320,448, -513.1,375,552, -513.1,125,552
-513.1,320,653, -513.1,375,552, -513.1,125,552
-513.1,320,448, -513.1,125,552, -513.1,70,448
-513.1,320,653, -513.1,125,552, -513.1,70,653
"""

_BLACK = {'e03': _BLACKLIST_E03}


def _parse(txt):
    tris = []
    for line in txt.strip().splitlines():
        line = line.strip()
        if not line:
            continue
        n = [float(x) for x in line.split(',')]
        tris.append([(n[0], n[1], n[2]), (n[3], n[4], n[5]), (n[6], n[7], n[8])])
    return tris


def _boundary_loops(tris):
    """Return boundary-edge loops (lists of vertex keys). Verts keyed by rounded XZ (y averaged per key)."""
    def k(p):
        return (round(p[0], 1), round(p[2], 1))
    ys = {}
    for t in tris:
        for p in t:
            ys.setdefault(k(p), []).append(p[1])
    yof = {kk: sum(v) / len(v) for kk, v in ys.items()}

    edgecount = {}
    for t in tris:
        ks = [k(p) for p in t]
        for a, b in ((ks[0], ks[1]), (ks[1], ks[2]), (ks[2], ks[0])):
            e = tuple(sorted((a, b)))
            edgecount[e] = edgecount.get(e, 0) + 1
    bedges = [e for e, c in edgecount.items() if c == 1]

    adj = {}
    for a, b in bedges:
        adj.setdefault(a, []).append(b)
        adj.setdefault(b, []).append(a)

    loops = []
    used = set()
    for start in adj:
        if start in used:
            continue
        loop = [start]; used.add(start); cur = start; prev = None
        while True:
            nxts = [n for n in adj.get(cur, []) if n != prev and (n not in used or n == start)]
            if not nxts:
                break
            nxt = nxts[0]
            if nxt == start:
                break
            loop.append(nxt); used.add(nxt); prev, cur = cur, nxt
        if len(loop) >= 3:
            loops.append(loop)
    return loops, yof


def _kk(p):
    return (round(p[0], 1), round(p[2], 1))


def _blacklist_edges(town):
    out = set()
    for t in _parse(_BLACK.get(town, '')):
        ks = {_kk(p) for p in t}
        if len(ks) == 2:                                   # a wall tri collapses to its footprint edge
            out.add(frozenset(ks))
    return out


def _outer_edges(town):
    """(outer footprint edges, ALL boundary edges, key->y map). Outer = midpoint farther from centroid than
    the triangle's third vertex."""
    tris = _parse(_DATA.get(town, ''))
    allv = [p for t in tris for p in t]
    cx = sum(p[0] for p in allv) / len(allv)
    cz = sum(p[2] for p in allv) / len(allv)

    def d2(p):
        return (p[0] - cx) ** 2 + (p[2] - cz) ** 2

    ys = {}
    for t in tris:
        for p in t:
            ys.setdefault(_kk(p), []).append(p[1])
    yof = {k: sum(v) / len(v) for k, v in ys.items()}

    ec = {}
    for t in tris:
        ks = [_kk(p) for p in t]
        for a, b in ((ks[0], ks[1]), (ks[1], ks[2]), (ks[2], ks[0])):
            ec[frozenset((a, b))] = ec.get(frozenset((a, b)), 0) + 1
    boundary = {e for e, c in ec.items() if c == 1 and len(e) == 2}

    edges = set()
    for t in tris:
        ks = [_kk(p) for p in t]
        for a, b in ((0, 1), (1, 2), (2, 0)):
            e = frozenset((ks[a], ks[b]))
            if e not in boundary:
                continue
            mid = ((t[a][0] + t[b][0]) / 2, 0, (t[a][2] + t[b][2]) / 2)
            if d2(mid) <= d2(t[({0, 1, 2} - {a, b}).pop()]):
                continue                                   # inner boundary edge
            edges.add(e)
    return edges, boundary, yof


def _dangling(edges):
    deg = {}
    for e in edges:
        for v in e:
            deg[v] = deg.get(v, 0) + 1
    return [v for v, d in deg.items() if d == 1]


def _path(boundary, src, dst):
    """Shortest edge path src->dst through the boundary graph (BFS by edge count). Returns edge list or None."""
    adj = {}
    for e in boundary:
        a, b = tuple(e)
        adj.setdefault(a, []).append(b); adj.setdefault(b, []).append(a)
    from collections import deque
    prev = {src: None}; q = deque([src])
    while q:
        cur = q.popleft()
        if cur == dst:
            out = []
            while prev[cur] is not None:
                out.append(frozenset((cur, prev[cur]))); cur = prev[cur]
            return out
        for n in adj.get(cur, []):
            if n not in prev:
                prev[n] = cur; q.append(n)
    return None


def _bridge_dangling(edges, boundary, yof, maxbridge=260.0):
    """Close gaps: first bridge dangling endpoints within maxbridge directly; then connect any remaining
    dangling pairs along the SHORTEST boundary path (recovers mis-classified outer runs like the east side)."""
    edges = set(edges)
    used = set()
    for v in _dangling(edges):
        if v in used:
            continue
        cands = [u for u in _dangling(edges) if u != v and u not in used and frozenset((v, u)) not in edges]
        if not cands:
            continue
        u = min(cands, key=lambda u: (v[0] - u[0]) ** 2 + (v[1] - u[1]) ** 2)
        if ((v[0] - u[0]) ** 2 + (v[1] - u[1]) ** 2) ** 0.5 <= maxbridge:
            edges.add(frozenset((v, u))); used.update((v, u))
    # path-bridge remaining dangling pairs
    rem = [v for v in _dangling(edges) if v not in used]
    done = set()
    for v in rem:
        if v in done:
            continue
        others = [u for u in rem if u != v and u not in done]
        if not others:
            continue
        u = min(others, key=lambda u: (v[0] - u[0]) ** 2 + (v[1] - u[1]) ** 2)
        p = _path(boundary, v, u)
        if p:
            edges.update(p); done.update((v, u))
    return edges


def _perimeter_loop(town):
    """Ordered XZ vertex loop of the closed outer perimeter (after blacklist + gap-bridging)."""
    edges, boundary, yof = _outer_edges(town)
    bl = _blacklist_edges(town)
    edges -= bl
    boundary -= bl
    edges = _bridge_dangling(edges, boundary, yof)
    adj = {}
    for e in edges:
        a, c = tuple(e)
        adj.setdefault(a, []).append(c); adj.setdefault(c, []).append(a)
    start = min(adj); loop = [start]; prev = None; cur = start
    while True:
        nxts = [n for n in adj[cur] if n != prev]
        if not nxts:
            break
        nxt = nxts[0]
        if nxt == start:
            break
        loop.append(nxt); prev, cur = cur, nxt
    return loop


def _dp(pts, eps):
    """Douglas-Peucker simplification of an XZ polyline."""
    import math
    if len(pts) < 3:
        return pts
    a, b = pts[0], pts[-1]

    def dist(p):
        dx, dz = b[0] - a[0], b[1] - a[1]; L = math.hypot(dx, dz)
        if L == 0:
            return math.hypot(p[0] - a[0], p[1] - a[1])
        return abs((p[0] - a[0]) * dz - (p[1] - a[1]) * dx) / L

    idx = max(range(1, len(pts) - 1), key=lambda i: dist(pts[i]))
    if dist(pts[idx]) > eps:
        return _dp(pts[:idx + 1], eps)[:-1] + _dp(pts[idx:], eps)
    return [a, b]


def _corners(town, n_walls):
    """Simplify the perimeter loop to exactly n_walls corners (binary-search the DP tolerance)."""
    loop = _perimeter_loop(town)
    closed = loop + [loop[0]]
    lo, hi, best = 1.0, 4000.0, None
    for _ in range(40):
        mid = (lo + hi) / 2
        c = _dp(closed, mid)[:-1]
        best = c
        if len(c) == n_walls:
            return c
        if len(c) > n_walls:
            lo = mid
        else:
            hi = mid
    return best


def perimeter_wall_tris(town='e03', y_bottom=170.0, y_top=500.0, n_walls=7):
    """Simplified perimeter: n_walls straight flat wall quads (2 tris each) between the dominant corners of
    the outer boundary, spanning [y_bottom, y_top]."""
    corners = _corners(town, n_walls)
    walls = []
    n = len(corners)
    for i in range(n):
        a = corners[i]; b = corners[(i + 1) % n]
        At = (a[0], y_top, a[1]); Bt = (b[0], y_top, b[1])
        Ab = (a[0], y_bottom, a[1]); Bb = (b[0], y_bottom, b[1])
        walls.append([list(At), list(Bt), list(Bb)])
        walls.append([list(At), list(Bb), list(Ab)])
    return walls


if __name__ == '__main__':
    tris = _parse(_REF_E03)
    loops, yof = _boundary_loops(tris)
    print(f'reference tris: {len(tris)}, boundary loops: {len(loops)}')
    for L in sorted(loops, key=len, reverse=True):
        xs = [p[0] for p in L]; zs = [p[1] for p in L]
        print(f'  loop: {len(L)} verts  x[{min(xs):.0f},{max(xs):.0f}] z[{min(zs):.0f},{max(zs):.0f}]')
    w = perimeter_wall_tris('e03')
    vs = [p for t in w for p in t]
    print(f'perimeter wall: {len(w)} tris  y[{min(v[1] for v in vs):.0f},{max(v[1] for v in vs):.0f}]')
