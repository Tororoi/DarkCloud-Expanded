#!/usr/bin/env python3
"""Carve the Factory metal ladder (e05a01 node 'hasigo1') for the Queens canal low-tide feature.

Donor (local space): rails + rungs span y 0..90 with a rung every 5u (each rung spans ~[5k-1, 5k]);
at y=90 the rails bend over the platform edge and their ends come back DOWN to land on the platform
ground at y=90, ~6.4u behind the rail plane. The curve between is a pair of GRAB HANDLES arcing up
to y=99.4 (~9.4 above the ground they mount to). The whole mesh is yawed ~9.5 deg in local space
(measured dz/dx of the rail plane), with no lean.

Carve for the Queens canal wall (floor y=0 -> walkway y=70):
  1. de-yaw about Y so the rail plane is parallel to world X (normals rotated with the verts),
  2. CLIP the bottom off at y=22 (mid-gap between the rung at 19..20 and the rung at 24..25),
     interpolating pos/uv/norm at the cut so the rails stay watertight,
  3. snap the cut ring down to y=20,
  4. shift y by -20: rails now run 0..70, the handle feet land ON the walkway at y=70, and the
     handles stand to y=79.4 above it -- the donor's ground-mount look, preserved.

carved_mdt() returns a build_mdt()-able Mdt for the ISO bake; placed_ladder_tris() gives world
triangles for the viewer at the Queens placement: south canal wall (z=+50 face), centred at x=700,
rail backs against the wall face, feet on the walkway behind it.
"""
import copy, math, re
from extract_scene_mesh import load_scene
import scene_placed, mdt_codec

DONOR_PART, DONOR_NODE = "e05a01", "hasigo1"
CUT_Y = 22.0                              # clip plane: mid rung-gap (rungs at 19..20 and 24..25)
SNAP_Y = 20.0                             # cut ring pulled here so rails meet the floor post-shift
GROUND_Y, TARGET_GROUND = 90.0, 70.0      # donor platform level -> Queens walkway level
SHIFT = GROUND_Y - TARGET_GROUND          # 20

def donor_mdt():
    scn = load_scene("gedit/e05/scene.scn")
    off, size = scene_placed._scndir(scn)[DONOR_PART]
    m = re.search(rb'MDS', scn[off:off + size])
    nodes, _ = scene_placed._accum(scn, off + m.start())
    mo = next(mo for nn, mo, par, mat in nodes if nn == DONOR_NODE)
    fo = next(c for c in (mo, off + m.start() + mo) if 0 < c < len(scn) and scn[c:c + 3] == b'MDT')
    return mdt_codec.parse_mdt(scn, fo)

def _tris_of_sub(prim, recs):
    if prim == 3:
        return [tuple(recs[i:i + 3]) for i in range(0, len(recs) - 2, 3)]
    if prim == 4:  # strip
        out = []
        for i in range(len(recs) - 2):
            a, b, c = recs[i], recs[i + 1], recs[i + 2]
            out.append((a, c, b) if i & 1 else (a, b, c))
        return out
    return []

def _measure_yaw(m):
    B = [v for v in m.pos if v[1] < 85 and v[2] < -40]     # rail-plane verts (wall side)
    mx = sum(v[0] for v in B) / len(B); mz = sum(v[2] for v in B) / len(B)
    num = sum((v[0] - mx) * (v[2] - mz) for v in B)
    den = sum((v[0] - mx) ** 2 for v in B)
    return math.atan2(num, den)                            # ~ +9.5 deg

def carved_mdt():
    m = copy.deepcopy(donor_mdt())
    # 1) de-yaw so the rail plane runs parallel to X (rotate normals with the verts)
    th = _measure_yaw(m); c, s = math.cos(th), math.sin(th)
    roty = lambda v: (v[0] * c + v[2] * s, v[1], -v[0] * s + v[2] * c) + tuple(v[3:])
    m.pos = [roty(v) for v in m.pos]
    if m.norm:
        m.norm = [roty(v) for v in m.norm]
    # 2) clip everything below CUT_Y, interpolating new verts on the cut edges
    first_new = len(m.pos)
    cache = {}
    def lerp(a, b, t):
        return tuple(x + (y - x) * t for x, y in zip(a, b))
    def cut_vert(rA, rB):
        a, b = (rA, rB) if rA <= rB else (rB, rA)
        if (a, b) in cache:
            return cache[(a, b)]
        pa, pb = m.pos[a[0]], m.pos[b[0]]
        t = (CUT_Y - pa[1]) / (pb[1] - pa[1])
        m.pos.append(lerp(pa, pb, t))
        m.uv.append(lerp(m.uv[a[1]], m.uv[b[1]], t))
        rec = [len(m.pos) - 1, len(m.uv) - 1]
        if m.norm:
            m.norm.append(lerp(m.norm[a[2]], m.norm[b[2]], t)); rec.append(len(m.norm) - 1)
        else:
            rec.append(0)
        if m.has_col:
            m.col.append(lerp(m.col[a[3]], m.col[b[3]], t)); rec.append(len(m.col) - 1)
        rec = tuple(rec)
        cache[(a, b)] = rec
        return rec
    new_subs = []
    for prim, midx, recs in m.submeshes:
        out = []
        for tri in _tris_of_sub(prim, recs):
            res = []
            for i in range(3):
                A, B = tri[i], tri[(i + 1) % 3]
                ina = m.pos[A[0]][1] >= CUT_Y
                inb = m.pos[B[0]][1] >= CUT_Y
                if ina:
                    res.append(A)
                if ina != inb:
                    res.append(cut_vert(A, B))
            for k in range(1, len(res) - 1):               # fan the 3/4-gon back to tris
                out += [res[0], res[k], res[k + 1]]
        if out:
            new_subs.append([3, midx, out])
    m.submeshes = new_subs
    # 3) snap the cut ring down + 4) shift so the ground mount lands at the walkway level
    m.pos = [(v[0], (SNAP_Y if i >= first_new else v[1]) - SHIFT) + tuple(v[2:])
             for i, v in enumerate(m.pos)]
    _compact(m)                   # drop the clipped-away (now unreferenced) verts from all streams
    m.preamble = list(m.preamble); m.preamble[2] = len(new_subs)
    return m

def _compact(m):
    slots = {0: 'pos', 1: 'uv'}
    if m.norm:
        slots[2] = 'norm'
    if m.has_col:
        slots[3] = 'col'
    remap = {}
    for slot, attr in slots.items():
        old = getattr(m, attr)
        used = sorted({r[slot] for _, _, recs in m.submeshes for r in recs})
        remap[slot] = {o: n for n, o in enumerate(used)}
        setattr(m, attr, [old[o] for o in used])
    m.submeshes = [[prim, midx,
                    [tuple(remap[s][r[s]] if s in remap else r[s] for s in range(len(r)))
                     for r in recs]]
                   for prim, midx, recs in m.submeshes]

def local_tris(m):
    out = []
    for prim, midx, recs in m.submeshes:
        for t in _tris_of_sub(prim, recs):
            out.append(tuple(m.pos[r[0]][:3] for r in t))
    return out

# ---- Queens placement: south canal wall (z=+50 face), centred at x=706 (matches IsoPatcher LAD_X).
#      Anchor = the handle FEET landing on the walkway just past the wall edge; the rails then hang
#      ~1.5u off the wall face and the mount BRACKETS (the deep-z standoffs at y~32/47 post-shift)
#      embed into the solid wall, reading as bolted on. ----
LADDER_X = 706.0
WALL_Z = 50.0
FEET_LAND_Z = 52.0               # handle-feet tips land 2u onto the walkway
EAST_BRIDGE_X = 800.0            # the new fishing sign still sits under the eastern bridge

def place_offset(m):
    """(dx, dz) that moves the carved LOCAL mesh to its Queens world position."""
    ys = [v[1] for v in m.pos]; xs = [v[0] for v in m.pos]
    feet_tip = max(v[2] for v in m.pos if v[1] > 69)             # bend region only (not brackets)
    return LADDER_X - (min(xs) + max(xs)) / 2, FEET_LAND_Z - feet_tip

def place(tris):
    dx, dz = place_offset(carved_mdt())
    return [tuple((p[0] + dx, p[1], p[2] + dz) for p in t) for t in tris]

def placed_ladder_tris():
    return place(local_tris(carved_mdt()))

# ---- MDS packaging for ISO injection: the carved MDT, verts baked to their Queens WORLD position,
#      wrapped in the same 0x80-byte identity-node MDS header the fishing-sign kanban uses (node 0,
#      meshOff 0x80, identity matrix). The town then places it via a mapinfo GROUND entry at origin. ----
MDS_HEADER_SRC = "../game_data/fishsign/kanban.mds"   # donor for the 0x80-byte MDS wrapper

def world_mdt():
    """carved_mdt() with pos translated to the final Queens world placement (mapinfo pos = origin)."""
    m = carved_mdt()
    dx, dz = place_offset(m)
    m.pos = [(v[0] + dx, v[1], v[2] + dz) + tuple(v[3:]) for v in m.pos]
    return m

def ladder_mds(node_name=b"hasigo"):
    """MDS bytes: kanban's 0x80 wrapper (node renamed) + the world-placed carved ladder MDT."""
    import os
    src = os.path.join(os.path.dirname(os.path.abspath(__file__)), MDS_HEADER_SRC)
    wrap = bytearray(open(src, "rb").read()[:0x80])
    wrap[0x18:0x28] = node_name[:15].ljust(0x10, b"\x00")        # node-name field (kanban node@0x10 +8)
    return bytes(wrap) + mdt_codec.build_mdt(world_mdt())

# New fishing sign marker: canal floor under the eastern bridge, board facing WEST (-x)
def sign_marker_tris():
    x, y0, y1 = EAST_BRIDGE_X, 0.0, 13.0
    quads = [
        # post
        [(x, y0, -0.6), (x, y1, -0.6), (x, y1, 0.6), (x, y0, 0.6)],
        # board (z-y plane, normal -x), 12 wide x 8 tall
        [(x, 4.0, -6.0), (x, 12.0, -6.0), (x, 12.0, 6.0), (x, 4.0, 6.0)],
    ]
    tris = []
    for q in quads:
        tris.append((q[0], q[1], q[2])); tris.append((q[0], q[2], q[3]))
    return tris

if __name__ == "__main__":
    d, m = donor_mdt(), carved_mdt()
    dt, ct = local_tris(d), local_tris(m)
    ys = [p[1] for t in ct for p in t]
    print(f"donor tris={len(dt)}  carved tris={len(ct)}  y {min(ys):.2f}..{max(ys):.2f}")
    pl = placed_ladder_tris()
    xs = [p[0] for t in pl for p in t]; zs = [p[2] for t in pl for p in t]
    feet = [p for t in pl for p in t if p[2] > WALL_Z and 69 < p[1] < 72]
    rail = [p for t in pl for p in t if p[1] < 30 and p[2] < WALL_Z]
    brak = [p for t in pl for p in t if p[1] < 60 and p[2] > WALL_Z]
    print(f"placed x {min(xs):.2f}..{max(xs):.2f}  z {min(zs):.2f}..{max(zs):.2f}")
    print(f"rail-back z={max(p[2] for p in rail):.2f} (wall face {WALL_Z})  "
          f"feet-on-walkway verts={len(feet)}  bracket verts in wall={len(brak)}")
    rb = mdt_codec.build_mdt(m)
    print(f"carved rebuilds to {len(rb)} bytes (donor {d.hdr[2]})")
