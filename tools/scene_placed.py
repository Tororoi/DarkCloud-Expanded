#!/usr/bin/env python3
"""Correctly world-place a town's scene.scn geometry: apply the mapinfo.cfg GROUND instance transforms
(position + Y-rotation) AND the per-node parent-chain accumulation, and decode triangles with the strict
mdt_codec (which rejects the spurious offsets the parse_mds heuristic sometimes returns — those were the
"distorted polys"). Supersedes a bare extract_mesh() for towns whose sub-files are instanced templates
placed by mapinfo (e.g. Yellow Drops s13: buildings/entrances/decoration each placed at 1+ world spots).

placed_meshes(scene_rel, mapinfo_rel) -> list of {name, inst, tris:[[[x,y,z]*3]...], verts:[[x,y,z]...]}
  one entry per placed mesh NODE instance (a GROUND entry places a whole sub-file; each mesh node in it
  becomes one entry; the same sub-file placed N times yields N sets).
"""
import struct, re, math
import mdt_codec
from extract_scene_mesh import load_scene, xform


def scn_dir(scn):
    """The scene.scn sub-file directory (0x30-stride entries from 0x10) as an ordered list of
    (name, off, size, entry_off) — entry_off is where the entry itself sits, for repointing."""
    out, o = [], 0x10
    while o + 0x30 <= len(scn):
        nm = scn[o:o + 16].split(b'\x00')[0].decode('latin1', 'replace')
        if not nm or not nm[0].isalnum():
            break
        off, size = struct.unpack_from('<II', scn, o + 0x10)
        out.append((nm, off, size, o))
        o += 0x30
    return out


def _scndir(scn):
    """{name: (off, size)} — first entry wins (the sub-file's data is shared across instances)."""
    d = {}
    for nm, off, size, _ in scn_dir(scn):
        d.setdefault(nm, (off, size))
    return d


def _ground_placements(cfg):
    """[(subfile_name, pos[x,y,z], rot[x,y,z])] for each GROUND or WATER entry (position row then rotation
    row, after the LOD-name string lines). WATER (canal/river meshes, e.g. Queens e03c*) uses the same
    name+pos+rot layout as GROUND; towns without WATER entries are unaffected."""
    lines = cfg.splitlines()
    out, i = [], 0

    def numrow(s):
        s = s.split('//')[0].strip()
        if not re.match(r'^-?\d', s):
            return None
        parts = [p for p in re.split(r'[,\t ]+', s) if p]
        try:
            return [float(p) for p in parts]
        except ValueError:
            return None

    while i < len(lines):
        m = re.match(r'\s*(?:GROUND|WATER)\s+"([^"]+)"', lines[i])
        if m:
            name, nums, j = m.group(1), [], i + 1
            while j < len(lines) and not re.match(r'\s*(?:GROUND|WATER)\s+"', lines[j]) and len(nums) < 2:
                r = numrow(lines[j])
                if r and len(r) >= 3:
                    nums.append(r[:3])
                j += 1
            if len(nums) >= 2:
                out.append((name, nums[0], nums[1]))
            i = j
        else:
            i += 1
    return out


def _compose(a, b):
    r = [0.0] * 16
    for c in range(4):
        for row in range(4):
            r[c*4+row] = sum(a[k*4+row] * b[c*4+k] for k in range(4))
    return r


def _place_y(v, pos, ry):
    th = math.radians(ry); c, s = math.cos(th), math.sin(th)
    return [v[0]*c + v[2]*s + pos[0], v[1] + pos[1], -v[0]*s + v[2]*c + pos[2]]


def _accum(scn, mds):
    cnt, tbl = struct.unpack_from('<II', scn, mds + 8)
    nodes = []
    for i in range(cnt):
        b = mds + tbl + i * 0x70
        nm = scn[b+8:b+8+16].split(b'\x00')[0].decode('latin1', 'replace')
        mo, par = struct.unpack_from('<ii', scn, b + 0x28)
        mat = list(struct.unpack_from('<16f', scn, b + 0x30))
        nodes.append((nm, mo, par, mat))
    world = [None] * cnt

    def wm(i):
        if world[i] is None:
            nm, mo, par, mat = nodes[i]
            world[i] = mat if (par < 0 or par >= cnt) else _compose(wm(par), mat)
        return world[i]
    return nodes, wm


def _flatten(m):
    out = []
    for prim, midx, recs in m.submeshes:
        if prim == 3:
            for k in range(0, len(recs) - 2, 3):
                out.append((recs[k][0], recs[k+1][0], recs[k+2][0]))
        elif prim == 4:
            for i in range(len(recs) - 2):
                a, b, c = (recs[i], recs[i+1], recs[i+2]) if i % 2 == 0 else (recs[i+1], recs[i], recs[i+2])
                out.append((a[0], b[0], c[0]))
    return out


def placed_meshes(scene_rel, mapinfo_rel):
    scn = load_scene(scene_rel)
    cfg = load_scene(mapinfo_rel).decode('latin1', 'replace')
    DIR = _scndir(scn)
    placements = _ground_placements(cfg)
    inst_counter = {}
    out = []
    for name, pos, rot in placements:
        if name not in DIR:
            continue
        off, size = DIR[name]
        mrel = scn[off:off+size].find(b'MDS\x00')
        if mrel < 0:
            continue
        mds = off + mrel
        nodes, wm = _accum(scn, mds)
        inst = inst_counter.get(name, 0)
        inst_counter[name] = inst + 1
        for i, (nn, mo, par, mat) in enumerate(nodes):
            if mo == 0:
                continue
            fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c+3] == b'MDT'), None)
            if not fo:
                continue
            try:
                m = mdt_codec.parse_mdt(scn, fo)      # strict: skips spurious/garbage matches
            except Exception:
                continue
            M = wm(i)
            wv = [_place_y(xform(M, (p[0], p[1], p[2])), pos, rot[1]) for p in m.pos]
            tris = _flatten(m)
            if not tris:
                continue
            out.append({'name': nn, 'inst': inst, 'verts': wv, 'tris': tris, 'sub': name})
    return out


if __name__ == '__main__':
    import sys
    scene = sys.argv[1] if len(sys.argv) > 1 else 'gedit/s13/scene.scn'
    mapinfo = sys.argv[2] if len(sys.argv) > 2 else 'gedit/s13/mapinfo.cfg'
    ms = placed_meshes(scene, mapinfo)
    print(f"{len(ms)} placed mesh instances, {sum(len(x['tris']) for x in ms)} tris")
    from collections import Counter
    for nm, n in Counter((x['name'], ) for x in ms).most_common(15):
        pass
    seen = {}
    for x in ms:
        seen.setdefault(x['name'], 0)
        seen[x['name']] += 1
    for nm in sorted(seen):
        insts = [x for x in ms if x['name'] == nm]
        xs = [p[0] for x in insts for p in x['verts']]; zs = [p[2] for x in insts for p in x['verts']]
        print(f"  {nm:16} x{seen[nm]:<2} tris={sum(len(x['tris']) for x in insts):<5} "
              f"X[{min(xs):.0f},{max(xs):.0f}] Z[{min(zs):.0f},{max(zs):.0f}]")
