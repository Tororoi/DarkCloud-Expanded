#!/usr/bin/env python3
"""Extract georama PART models (a whole sub-file = one placeable part) at local origin, for the georama
editor. Buildings e03h*, roads e03r* (Queens); generalizes to any town's h*/r* sub-files.

part_models(scene_rel, name_re) -> {part_name: {'tris':[[[x,y,z]*3]...], 'bbox':[minx..maxz]}}
  tris are LOCAL (sub-file origin), parent-chain accumulated + strict mdt_codec decode.
"""
import re, struct
import mdt_codec
from extract_scene_mesh import load_scene, xform
import scene_placed


def part_models(scene_rel, name_re):
    scn = load_scene(scene_rel)
    DIR = scene_placed._scndir(scn)
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


if __name__ == '__main__':
    import sys
    scene = sys.argv[1] if len(sys.argv) > 1 else 'gedit/e03/scene.scn'
    pat = sys.argv[2] if len(sys.argv) > 2 else r'e03[hr]\d'
    pm = part_models(scene, pat)
    for nm, d in pm.items():
        b = d['bbox']
        print(f"  {nm:8} tris={len(d['tris']):5}  W={b[3]-b[0]:.0f} D={b[5]-b[2]:.0f} H={b[4]-b[1]:.0f}")
    print(f"{len(pm)} parts, {sum(len(d['tris']) for d in pm.values())} tris")
