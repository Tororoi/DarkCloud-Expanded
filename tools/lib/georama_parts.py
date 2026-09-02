#!/usr/bin/env python3
"""Extract georama PART models (a whole sub-file = one placeable part) at local origin, for the georama
editor. Buildings e03h*, roads e03r* (Queens); generalizes to any town's h*/r* sub-files.

part_models(scene_rel, name_re) -> {part_name: {'tris':[[[x,y,z]*3]...], 'bbox':[minx..maxz]}}
  tris are LOCAL (sub-file origin), parent-chain accumulated + strict mdt_codec decode.

lod_models(scene_rel, name_re) -> {part_name: {'0': tris, '1': tris, '2': tris}} for parts that ship
  LOD variant meshes (<sub>_0=full, _1=medium, _2=low; Queens buildings have all three, trees _0/_2).
  The sub-file's PTS directory lists `<sub>_X.mds` names in the same order as its MDS blocks
  (verified against the inline off/size record of `_a` across all Queens subs), so variant k = k-th
  MDS block. Parts with fewer than two LOD levels are omitted.
"""
import re
import struct
import mdt_codec
from extract_scene_mesh import load_scene, xform
import scene_placed


def _mds_tris(scn, mds):
    nodes, wm = scene_placed._accum(scn, mds)
    tris = []
    for i, (nn, mo, par, mat) in enumerate(nodes):
        if mo == 0:
            continue
        fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c+3] == b'MDT'), None)
        if not fo:
            continue
        try:
            m = mdt_codec.parse_mdt(scn, fo)
        except Exception:
            continue
        M = wm(i)
        wv = [xform(M, (p[0], p[1], p[2])) for p in m.pos]
        for a, b, c in scene_placed._flatten(m):
            tris.append([list(wv[a]), list(wv[b]), list(wv[c])])
    return tris


def lod_models(scene_rel, name_re):
    scn = load_scene(scene_rel)
    DIR = scene_placed.scn_directory_map(scn)
    rx = re.compile(name_re)
    out = {}
    for sub in sorted(DIR):
        if not rx.match(sub):
            continue
        off, size = DIR[sub]
        head = scn[off:off + 0x200]
        names = [m.group(1).decode() for m in
                 re.finditer(re.escape(sub).encode() + rb'_([0-9a-z])\.mds\x00', head)]
        blocks = [m.start() for m in re.finditer(rb'MDS\x00', scn[off:off + size])]
        if len(names) != len(blocks):        # directory shape not understood -> don't guess
            continue
        lods = {}
        for suf, bo in zip(names, blocks):
            if suf in ('0', '1', '2'):
                t = _mds_tris(scn, off + bo)
                if t:
                    lods[suf] = t
        if len(lods) >= 2:                   # a lone _0 is just the normal mesh, not an LOD chain
            out[sub] = lods
    return out


def lod_layers(scene_rel, name_re, instances=None, group='LOD compare (full/medium/low)',
               showroom=(-500.0, -1000.0, -150.0)):
    """Viewer layer dicts (scene_viewer_html schema) for every asset with LOD variant meshes:
    one toggle per level (full/medium/low), world-placed at the given default-layout instances
    ({sub: [{'x','y','z','rot'}]}). Assets without an instance line up in a showroom row at
    (x0, z, step-x). Returns [] for towns whose data ships no LOD chains (all s-scenes; only the
    five e-towns carry _1/_2) — callers can wire this unconditionally."""
    import math
    META = {'0': ('full', [200, 170, 120], '#db8'), '1': ('medium', [110, 200, 160], '#7da'),
            '2': ('low', [230, 130, 120], '#e87')}
    instances = instances or {}
    sx, sz, step = showroom
    out = []
    lods = lod_models(scene_rel, name_re)
    for sub in sorted(lods):
        full = lods[sub].get('0') or next(iter(lods[sub].values()))
        xs = [p[0] for t in full for p in t]; ys = [p[1] for t in full for p in t]
        zs = [p[2] for t in full for p in t]
        cxm = (min(xs) + max(xs)) / 2; czm = (min(zs) + max(zs)) / 2; my = min(ys)
        insts, show = instances.get(sub), False
        if not insts:
            insts = [{'x': sx, 'y': 0.0, 'z': sz, 'rot': 0}]; sx += step; show = True
        for lv in sorted(lods[sub]):
            nm, col, bd = META[lv]
            wt = []
            for o in insts:
                a = o['rot'] * math.pi / 2; ca = math.cos(a); sa = math.sin(a)
                for t in lods[sub][lv]:
                    wt.append([[(p[0]-cxm)*ca - (p[2]-czm)*sa + o['x'],
                                p[1]-my + o['y'],
                                (p[0]-cxm)*sa + (p[2]-czm)*ca + o['z']] for p in t])
            out.append({'key': f'lod_{sub}_{lv}',
                        'label': f'{sub} {nm} (_{lv})' + (' [showroom]' if show else ''),
                        'tris': wt, 'color': col, 'alpha': 1.0, 'border': bd, 'on': False,
                        'group': group})
    return out


def part_models(scene_rel, name_re):
    scn = load_scene(scene_rel)
    DIR = scene_placed.scn_directory_map(scn)
    rx = re.compile(name_re)
    out = {}
    for sub in sorted(DIR):
        if not rx.match(sub):
            continue
        off, size = DIR[sub]
        mrel = scn[off:off+size].find(b'MDS\x00')
        if mrel < 0:
            continue
        mds = off + mrel
        nodes, wm = scene_placed._accum(scn, mds)
        tris = []
        for i, (nn, mo, par, mat) in enumerate(nodes):
            if mo == 0:
                continue
            fo = next((c for c in (mo, mds + mo) if 0 < c < len(scn) and scn[c:c+3] == b'MDT'), None)
            if not fo:
                continue
            try:
                m = mdt_codec.parse_mdt(scn, fo)
            except Exception:
                continue
            M = wm(i)
            wv = [xform(M, (p[0], p[1], p[2])) for p in m.pos]
            for a, b, c in scene_placed._flatten(m):
                tris.append([list(wv[a]), list(wv[b]), list(wv[c])])
        if not tris:
            continue
        xs = [p[0] for t in tris for p in t]; ys = [p[1] for t in tris for p in t]; zs = [p[2] for t in tris for p in t]
        out[sub] = {'tris': tris,
                    'bbox': [round(min(xs),1), round(min(ys),1), round(min(zs),1),
                             round(max(xs),1), round(max(ys),1), round(max(zs),1)]}
    return out

# ── DEFAULT town layout (former georama_default.py) ─────────────────────────────────────────────
# gdata0.edt = 16-byte records (x,y,z float, code int32), code==-1 ends. code low-16 = part index
# (0..11 = buildings e03h01..e03h12, 12 = tree, 13 = road), high-16 (signed) = rotation (mod 4).
def default_layout(gdata_rel):
    edt = load_scene(gdata_rel)
    out = {'buildings': [], 'trees': [], 'roads': []}
    o = 0xc
    while o + 16 <= len(edt):
        x, y, z = struct.unpack_from('<3f', edt, o)
        code = struct.unpack_from('<i', edt, o + 12)[0]
        o += 16
        if code == -1:
            break
        part = code & 0xffff
        rot = (code >> 16) & 0xffff
        if rot >= 0x8000:
            rot -= 0x10000
        rot &= 3
        x, y, z = round(x), round(y), round(z)          # snap off the 0.1 bias
        if part <= 11:
            out['buildings'].append({'part': part, 'name': 'e03h%02d' % (part + 1),
                                     'x': x, 'y': y, 'z': z, 'rot': rot})
        elif part == 12:
            out['trees'].append({'x': x, 'y': y, 'z': z, 'rot': rot})
        elif part == 13:
            out['roads'].append({'x': x, 'y': y, 'z': z, 'rot': rot})
    return out


if __name__ == '__main__':
    import sys
    scene = sys.argv[1] if len(sys.argv) > 1 else 'gedit/e03/scene.scn'
    pat = sys.argv[2] if len(sys.argv) > 2 else r'e03[hr]\d'
    pm = part_models(scene, pat)
    for nm, d in pm.items():
        b = d['bbox']
        print(f"  {nm:8} tris={len(d['tris']):5}  W={b[3]-b[0]:.0f} D={b[5]-b[2]:.0f} H={b[4]-b[1]:.0f}")
    print(f"{len(pm)} parts, {sum(len(d['tris']) for d in pm.values())} tris")
