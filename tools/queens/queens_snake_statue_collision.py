#!/usr/bin/env python3
"""Queens h06 collision upgrades (user-directed 2026-08/09):
  `_c` (camera): replaced with a simple open cylinder (camera_hull_cylinder_tris) — CAMERA_HULL_SEGMENTS-gon at
      CAMERA_HULL_RADIUS_MUL x the vanilla mean ring radius, centered on the snake-head summit (CAMERA_HULL_AXIS_XZ),
      doubled height — built as a fresh MDS APPENDED at the sub's end, +0xc0/+0xc4 repointed.
  `_a` (player): replaced with the FULL detailed visual mesh (all _0 nodes) plus the collision
      surgery below (PLAYER_COLLISION_REMOVE_TRIS / PLAYER_COLLISION_ADD_TRIS), rebuilt as a fresh MDS APPENDED at the sub's
      end with the header's _a words (+0x78/+0x7c) repointed (old blocks become dead space).

rebuild_h06() -> (new_sub_bytes, orig_size, chunks). Run this file directly to export
game_data/queens/queens_parts.bin for IsoPatcher.ApplyQueensPartSwaps.
"""
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'lib'))
import toolpath  # noqa: F401 — puts every tools/ subfolder on sys.path
import math, os, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract_scene_mesh import load_scene, xform
import scene_placed
import mdt_codec
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'iso_patch', 'collision'))
from collision_geom import kd_split
from georama_collision import build_coll_mdt

MAX_PLAYER_COLLISION_TRIS = 1000     # single node = the whole model. Settled: the 400-poly cap does NOT bind
                      # georama-part collision, and fewer/larger nodes snag less than many small ones.
CAMERA_HULL_SCALE_Y = 2.0
CAMERA_HULL_RADIUS_MUL = 2.0    # cylinder radius = vanilla mean ring radius x this
CAMERA_HULL_SEGMENTS = 16       # ring segments: vanilla octagon subdivided once, snapped to a true circle
CAMERA_HULL_AXIS_XZ = (-13.80, 49.65)   # cylinder axis: centroid of the two snake-head summit tris
                              # (-18.79,114.81,56.87 / -5.77,118.98,50.67 / -17.82,116.89,49.15)
                              # and (-5.77,118.98,50.67 / -16.85,114.81,41.43 / -17.82,116.89,49.15)


from queens_snake_statue_surgery_data import PLAYER_COLLISION_REMOVE_TRIS, PLAYER_COLLISION_ADD_TRIS   # user-selected tris (viewer)

def _tri_matches(t, ref, tol=0.05):
    for rot in range(3):
        r = ref[rot:] + ref[:rot]
        if all(abs(t[i][j] - r[i][j]) <= tol for i in range(3) for j in range(3)):
            return True
    return False


def apply_surgery(tris):
    kept, removed = [], 0
    for t in tris:
        if any(_tri_matches(t, ref) for ref in PLAYER_COLLISION_REMOVE_TRIS):
            removed += 1
            continue
        kept.append(t)
    if PLAYER_COLLISION_REMOVE_TRIS and removed != len(PLAYER_COLLISION_REMOVE_TRIS):
        print(f'  WARNING: {removed} tris removed but {len(PLAYER_COLLISION_REMOVE_TRIS)} listed — check coords')
    kept += [[list(p) for p in t] for t in PLAYER_COLLISION_ADD_TRIS]
    return kept


def full_visual_tris(scn, DIR, name='e03h06'):
    off, size = DIR[name]
    sub = scn[off:off + size]
    mds0 = struct.unpack_from('<I', sub, 0x48)[0]
    nodes, wm = scene_placed._accum(sub, mds0)
    out = []
    for i, (nn, mo, par, mat) in enumerate(nodes):
        if mo == 0:
            continue
        fo = next((c for c in (mo, mds0 + mo) if 0 < c < len(sub) and sub[c:c + 3] == b'MDT'), None)
        if not fo:
            continue
        try:
            m = mdt_codec.parse_mdt(sub, fo)
        except Exception:
            continue
        M = wm(i)
        wv = [xform(M, (p[0], p[1], p[2])) for p in m.pos]
        out += [[list(wv[a]), list(wv[b]), list(wv[c])] for a, b, c in scene_placed._flatten(m)]
    return out


def camera_hull_cylinder_tris(sub):
    """The replacement `_c` hull as part-local tris: a simple open cylinder — CAMERA_HULL_SEGMENTS-gon,
    radius = vanilla mean ring radius x CAMERA_HULL_RADIUS_MUL, centered on the snake-head summit
    (CAMERA_HULL_AXIS_XZ) so the hull is equidistant around a player standing there, from y=0 to
    ymax*CAMERA_HULL_SCALE_Y. Side quads only (no caps), wound OUTWARD like the vanilla hull."""
    c_off = struct.unpack_from('<I', sub, 0xc0)[0]
    cnt, tbl = struct.unpack_from('<II', sub, c_off + 8)
    vs = []
    for i in range(cnt):
        mo = struct.unpack_from('<i', sub, c_off + tbl + i * 0x70 + 0x28)[0]
        if not mo:
            continue
        fo = c_off + mo
        w = struct.unpack_from('<16I', sub, fo)
        vs += [struct.unpack_from('<3f', sub, fo + w[4] + vi * 0x10) for vi in range(w[3])]
    ymax = max(v[1] for v in vs)
    base = [v for v in vs if v[1] < ymax * 0.5]
    cx = sum(v[0] for v in base) / len(base)
    cz = sum(v[2] for v in base) / len(base)
    rad = sum(math.hypot(v[0] - cx, v[2] - cz) for v in base) / len(base)
    hx, hz = CAMERA_HULL_AXIS_XZ
    top_y, r = ymax * CAMERA_HULL_SCALE_Y, rad * CAMERA_HULL_RADIUS_MUL
    ring_b = []; ring_t = []
    for i in range(CAMERA_HULL_SEGMENTS):
        a = 2 * math.pi * i / CAMERA_HULL_SEGMENTS
        ring_b.append([hx + r * math.cos(a), 0.0, hz + r * math.sin(a)])
        ring_t.append([hx + r * math.cos(a), top_y, hz + r * math.sin(a)])
    tris = []
    for i in range(CAMERA_HULL_SEGMENTS):
        j = (i + 1) % CAMERA_HULL_SEGMENTS
        for tr in ([ring_b[i], ring_b[j], ring_t[j]], [ring_b[i], ring_t[j], ring_t[i]]):
            ab = [tr[1][k] - tr[0][k] for k in range(3)]
            ac = [tr[2][k] - tr[0][k] for k in range(3)]
            n = [ab[1] * ac[2] - ab[2] * ac[1], ab[2] * ac[0] - ab[0] * ac[2],
                 ab[0] * ac[1] - ab[1] * ac[0]]
            gx = sum(q[0] for q in tr) / 3 - hx
            gz = sum(q[2] for q in tr) / 3 - hz
            if n[0] * gx + n[2] * gz < 0:
                tr = [tr[0], tr[2], tr[1]]
            tris.append([list(q) for q in tr])
    return tris


def build_coll_mds(old_mds, chunks, name_prefix='hc'):
    """A fresh collision MDS: header cloned from old_mds, root null node + one identity-frame
    node per chunk (entries cloned from old_mds's first mesh node)."""
    cnt0, tbl0 = struct.unpack_from('<II', old_mds, 8)
    template = None
    for i in range(cnt0):
        b = tbl0 + i * 0x70
        if struct.unpack_from('<i', old_mds, b + 0x28)[0]:
            template = old_mds[b:b + 0x70]
            break
    assert template is not None

    def entry(idx, nm, mo, par):
        e = bytearray(template)
        struct.pack_into('<i', e, 0, idx)
        nmb = nm.encode('latin1')
        e[8:24] = nmb + b'\x00' * (16 - len(nmb))
        struct.pack_into('<ii', e, 0x28, mo, par)
        ident = [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0]
        struct.pack_into('<16f', e, 0x30, *ident)
        return bytes(e)

    n = len(chunks)
    new_c = bytearray(old_mds[:0x10])
    struct.pack_into('<I', new_c, 8, n + 1)
    new_c += entry(0, 'null1', 0, -1)
    mdts = [build_coll_mdt(ch, y_shift=0.0) for ch in chunks]
    pos = 0x10 + (n + 1) * 0x70
    offs = []
    for m in mdts:
        offs.append(pos)
        pos += len(m) + ((-len(m)) % 16)
    for k in range(n):
        new_c += entry(k + 1, f'{name_prefix}{k:02d}', offs[k], 0)
    for m in mdts:
        new_c += m
        new_c += b'\x00' * ((-len(m)) % 16)
    return bytes(new_c)


def rebuild_h06(scn, DIR):
    off, size = DIR['e03h06']
    sub = bytearray(scn[off:off + size])
    vis = full_visual_tris(scn, DIR)
    # -- _c: open cylinder, rebuilt as a fresh MDS appended at the end (vert count grows) --
    c_off = struct.unpack_from('<I', sub, 0xc0)[0]
    c_size = struct.unpack_from('<I', sub, 0xc4)[0]
    old_c = bytes(sub[c_off:c_off + c_size])
    new_c = build_coll_mds(old_c, [camera_hull_cylinder_tris(bytes(sub))], name_prefix='hc')
    # -- _a: full visual mesh, split, appended, repointed --
    a_off = struct.unpack_from('<I', sub, 0x78)[0]
    a_size = struct.unpack_from('<I', sub, 0x7c)[0]
    old_a = bytes(sub[a_off:a_off + a_size])
    chunks = kd_split(apply_surgery(vis), MAX_PLAYER_COLLISION_TRIS, axes=(0, 2))
    new_a = build_coll_mds(old_a, chunks, name_prefix='ha')
    while len(sub) % 16:
        sub += b'\x00'
    new_a_off = len(sub)
    sub += new_a
    struct.pack_into('<II', sub, 0x78, new_a_off, len(new_a))
    while len(sub) % 16:
        sub += b'\x00'
    new_c_off = len(sub)
    sub += new_c
    struct.pack_into('<II', sub, 0xc0, new_c_off, len(new_c))
    return bytes(sub), size, chunks


if __name__ == '__main__':
    # Export the rebuilt part to the bin IsoPatcher.ApplyQueensPartSwaps consumes.
    # Format: u32 count; per part: name[8] + u32 origSubSize (guard) + u32 newSubSize + bytes (16-aligned).
    scn = load_scene('gedit/e03/scene.scn')
    DIR = scene_placed.scn_directory_map(scn)
    OUT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                        '..', '..', 'game_data', 'queens', 'queens_parts.bin'))
    blob = bytearray()
    entries = [('e03h06',) + rebuild_h06(scn, DIR)[:2]]
    blob += struct.pack('<I', len(entries))
    for name, new, orig in entries:
        blob += name.encode('latin1').ljust(8, b'\x00')
        blob += struct.pack('<II', orig, len(new))
        blob += new
        blob += b'\x00' * ((-len(new)) % 16)
        print(f'{name}: sub {orig} -> {len(new)}')
    open(OUT, 'wb').write(blob)
    print(f'wrote {len(blob)} bytes -> {OUT}')
