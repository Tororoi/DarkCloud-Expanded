#!/usr/bin/env python3
"""Extract world-space geometry from a Dark Cloud town scene (gedit/*/scene.scn).

Decodes the SCN container -> MDS node blocks -> MDT meshes. Vertex positions are
plain XYZW floats (stride 16, w=1.0) starting at MDT+0x40; the triangle topology is
a packed VU1 display-list (GIF tags interleaved, ~9 words/vertex) and is NOT decoded
here -- for collision we re-triangulate the point cloud ourselves.

Coordinate frame: the extracted verts are already in WORLD space (pond centre = 0,0,
water surface Y=0), matching the runtime WaterLevel. Validated against Brownboo (s04).

Node naming conventions (Brownboo, likely general):
  s04g01*  terrain / crater walls        iwa*     rocks (岩) - in-water obstacles
  s04g03b* shore banks (dock platforms)  s04g04*  boardwalk support posts (Y-15..0)
  st0*     shore fences                  h01/2/3* buildings (own MDS block, LOCAL
  obj*     boardwalk planks (instanced)           origin - placed by georama, external)

Buildings/planks sit at local origin: their world placement lives in the georama
instance table (not in scene.scn), so they need a separate step to place precisely.

Usage: extract_scene_mesh.py gedit/s04/scene.scn [name-prefix]
"""
import struct, re, sys, math

DAT_DIR = "/Users/thomascantwell/ROMs/dc_extracted"

def load_scene(rel):
    hed = open(f"{DAT_DIR}/data.hed", "rb").read()
    hd2 = open(f"{DAT_DIR}/data.hd2", "rb").read()
    dat = open(f"{DAT_DIR}/data.dat", "rb")
    for i in range(len(hed) // 80):
        n = hed[i*80:i*80+80].split(b"\x00")[0].decode("latin1", "replace").replace("\\", "/")
        if n == rel:
            off, size, _ = struct.unpack_from("<III", hd2, 16 + i*32)
            dat.seek(off); return dat.read(size)
    raise SystemExit(f"{rel} not found in data.hed index")

def _isname(scn, b):
    s = scn[b:b+4]; return len(s) >= 2 and all(32 <= c < 127 for c in s[:2])

def parse_mds(scn, mds):
    count = struct.unpack_from("<I", scn, mds+8)[0]
    tbl = None
    for x in range(mds+0x20, mds+0x140, 4):
        if all(_isname(scn, x + k*0x70 + 8) for k in range(min(3, count))): tbl = x; break
    if tbl is None: return []
    out = []
    for k in range(count):
        b = tbl + k*0x70
        name = scn[b+8:b+8+16].split(b"\x00")[0].decode("latin1", "replace")
        mo, par = struct.unpack_from("<ii", scn, b+0x28)
        mat = struct.unpack_from("<16f", scn, b+0x30)
        out.append((name, mo, mat))
    return out

def read_verts(scn, fo):
    if scn[fo:fo+3] != b"MDT": return []
    total = struct.unpack_from("<I", scn, fo+8)[0]
    vs = []; p = fo + 0x40
    while p + 16 <= fo + total:
        x, y, z, w = struct.unpack_from("<4f", scn, p)
        if abs(w - 1.0) > 1e-3 or not all(abs(v) < 8000 for v in (x, y, z)): break
        vs.append((x, y, z)); p += 16
    return vs

def xform(m, v):
    x, y, z = v
    return (m[0]*x + m[4]*y + m[8]*z + m[12],
            m[1]*x + m[5]*y + m[9]*z + m[13],
            m[2]*x + m[6]*y + m[10]*z + m[14])

def read_tris(scn, fo):
    """EXACT triangles from a visual MDT's display list — 100% clean, matches the engine's own decoder
    (CVisualVu1::CreateVUdataFromMDT @0x135aa0). Returns list of (i0,i1,i2) vertex indices.

    Format: 16-u32 MDT header; vertices XYZW at hw[4] (stride 0x10, count hw[3]). Display list at
    hw[10] = a 4-int preamble whose 3rd int (@hw[10]+8) is the SUBMESH COUNT, then that many submeshes.
    Each submesh = a 3-int header (primType, vertexCount, materialIdx) followed by vertexCount records
    of 3 ints each; the record's FIRST int is the position index (the other two are uv/normal indices).
    primType 3 = triangle LIST (every 3 records = 1 tri); primType 4 = triangle STRIP (each record
    after the first two = 1 tri, winding alternates). Submeshes mix list and strip within one mesh.
    """
    if scn[fo:fo+3] != b"MDT": return []
    hw = struct.unpack_from("<16I", scn, fo)
    dl, vcount = hw[10], hw[3]
    if dl == 0 or fo + dl + 0x10 > len(scn): return []
    numsub = struct.unpack_from("<I", scn, fo + dl + 8)[0]
    o = dl + 0x10
    tris = []
    for _ in range(numsub):
        if fo + o + 0xC > len(scn): break
        prim, vcnt = struct.unpack_from("<ii", scn, fo + o)
        o += 0xC
        if vcnt < 0 or fo + o + vcnt * 12 > len(scn): break
        pos = [struct.unpack_from("<i", scn, fo + o + r*12)[0] for r in range(vcnt)]
        o += vcnt * 12
        def ok(a, b, c): return 0 <= a < vcount and 0 <= b < vcount and 0 <= c < vcount and a != b and b != c and a != c
        if prim == 3:
            for k in range(0, vcnt - 2, 3):
                if ok(pos[k], pos[k+1], pos[k+2]): tris.append((pos[k], pos[k+1], pos[k+2]))
        elif prim == 4:
            for i in range(vcnt - 2):
                a, b, c = (pos[i], pos[i+1], pos[i+2]) if i % 2 == 0 else (pos[i+1], pos[i], pos[i+2])
                if ok(a, b, c): tris.append((a, b, c))
    return tris

def _filter_long(verts, tris, factor=4.0, floor=40.0):
    """Drop triangles whose longest edge exceeds factor * median edge (min `floor`). Multi-segment
    meshes decode with a minority of spurious long triangles connecting distant verts; this removes
    them without touching normal geometry (single-segment meshes have no long edges to drop)."""
    if not tris: return tris
    import math as _m
    def me(t):
        a, b, c = (verts[i] for i in t)
        return max(_m.dist(a, b), _m.dist(b, c), _m.dist(a, c))
    edges = sorted(me(t) for t in tris)
    med = edges[len(edges) // 2]
    thr = max(floor, med * factor)
    return [t for t in tris if me(t) <= thr]

def extract_mesh(scn, prefix=None, clean=False):
    """Return {node_name: (world_verts, tris)} — world-space verts + exact triangle indices.
    The decoder is now exact (see read_tris), so clean/filtering defaults OFF; leave clean=True only
    if you deliberately want long-edge triangles dropped (e.g. to exclude giant crater/sky spans)."""
    out = {}
    for m in re.finditer(rb"MDS\x00", scn):
        for name, mo, mat in parse_mds(scn, m.start()):
            if mo == 0 or (prefix and not name.startswith(prefix)): continue
            fo = next((c for c in (mo, m.start()+mo) if 0 < c < len(scn) and scn[c:c+3] == b"MDT"), None)
            if not fo: continue
            wv = [xform(mat, v) for v in read_verts(scn, fo)]
            tris = _filter_long(wv, read_tris(scn, fo)) if clean else read_tris(scn, fo)
            out.setdefault(name, [[], []])
            base = len(out[name][0])
            out[name][0].extend(wv)
            out[name][1].extend((a+base, b+base, c+base) for a, b, c in tris)
    return {k: (v[0], v[1]) for k, v in out.items()}

def extract(scn, prefix=None):
    """Return {node_name: [world (x,y,z), ...]} for every mesh-bearing node."""
    out = {}
    for m in re.finditer(rb"MDS\x00", scn):
        for name, mo, mat in parse_mds(scn, m.start()):
            if mo == 0 or (prefix and not name.startswith(prefix)): continue
            fo = next((c for c in (mo, m.start()+mo) if 0 < c < len(scn) and scn[c:c+3] == b"MDT"), None)
            if fo: out.setdefault(name, []).extend(xform(mat, v) for v in read_verts(scn, fo))
    return out

def convex_hull_xz(verts, ylo=-16.0, yhi=3.0, expand=3.0, maxpts=10):
    """XZ convex hull of the verts in the [ylo,yhi] band, expanded outward, decimated."""
    pts = sorted(set((round(x, 1), round(z, 1)) for x, y, z in verts if ylo <= y <= yhi))
    if len(pts) < 3: return pts
    cr = lambda o, a, b: (a[0]-o[0])*(b[1]-o[1]) - (a[1]-o[1])*(b[0]-o[0])
    lo = []
    for p in pts:
        while len(lo) >= 2 and cr(lo[-2], lo[-1], p) <= 0: lo.pop()
        lo.append(p)
    up = []
    for p in reversed(pts):
        while len(up) >= 2 and cr(up[-2], up[-1], p) <= 0: up.pop()
        up.append(p)
    h = lo[:-1] + up[:-1]
    if len(h) > maxpts:
        step = len(h) / maxpts; h = [h[int(i*step)] for i in range(maxpts)]
    cx = sum(p[0] for p in h) / len(h); cz = sum(p[1] for p in h) / len(h)
    out = []
    for x, z in h:
        dx, dz = x - cx, z - cz; d = math.hypot(dx, dz) or 1
        out.append((x + dx/d*expand, z + dz/d*expand))
    return out

if __name__ == "__main__":
    rel = sys.argv[1] if len(sys.argv) > 1 else "gedit/s04/scene.scn"
    prefix = sys.argv[2] if len(sys.argv) > 2 else None
    scn = load_scene(rel)
    for name, wv in sorted(extract(scn, prefix).items()):
        xs = [p[0] for p in wv]; ys = [p[1] for p in wv]; zs = [p[2] for p in wv]
        print(f"{name:16} {len(wv):4} verts  X {min(xs):.0f}..{max(xs):.0f}  "
              f"Y {min(ys):.0f}..{max(ys):.0f}  Z {min(zs):.0f}..{max(zs):.0f}")
