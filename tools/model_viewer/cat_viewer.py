#!/usr/bin/env python3
"""Bake a self-contained WebGL page of the Divine Beast Title cat EXACTLY as the disc bake grafts it into Xiao's dungeon
pack: tools/iso_patch/build_cat_pack.py's assemble() is run IN MEMORY on the user's ISO sources (nothing is written), and
the cat subtree (catroot + cat_* nodes, its two meshes, the trimmed cat.mot with the float-up graft, and the MOTION 1 KEY
windows the game plays) is exported through the model viewer's codec (extract_model.py) into viewer_template.html.

A second entry shows the untouched s86 source cat (gedit\\s86\\chara\\c04cat.chr) with every clip its own cfg names, for
comparison, and a third shows Dran (dun\\monstor\\c12a.chr) with every clip — the wing donor for the Angel Shooter / Angel
Gear cat, so the wing poses to port can be judged (user 2026-09-12). Output: cat_viewer.html next to this script (or --out).

    python3 tools/model_viewer/cat_viewer.py [--iso PATH] [--out PATH]
"""
import io
import json
import re
import math
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'iso_patch'))
sys.path.insert(0, os.path.join(HERE, '..', 'lib'))
sys.path.insert(0, HERE)
import ps2iso                                  # noqa: E402
import mot_codec as mc                         # noqa: E402
import build_cat_pack as bcp                   # noqa: E402
import extract_model as em                     # noqa: E402
from mdt_codec import parse_mdt                # noqa: E402,F401
import cat_wings as cw                         # noqa: E402 — the wing graft itself lives in tools/lib (the bake needs it, this page only shows it)
from cat_wings import subtree_nodes, DRAN_CHR, _sample, _quat_to_mat, _slerp   # noqa: E402,F401 (re-exported for the probes)


class Archive:
    def __init__(self, iso):
        self.f = open(iso, 'rb')
        recs = ps2iso.parse_root(self.f)
        self.hed = ps2iso.read_file(self.f, recs['DATA.HED'])
        self.hd2 = recs['DATA.HD2']
        self.dat_iso = recs['DATA.DAT']['ext'] * ps2iso.SECTOR

    def read(self, name):
        i = ps2iso.archive_find(self.hed, name)
        if i is None:
            raise SystemExit(f"{name} not in the archive")
        self.f.seek(self.hd2['ext'] * ps2iso.SECTOR + 16 + i * 32)
        off, size = struct.unpack('<II', self.f.read(8))
        self.f.seek(self.dat_iso + off)
        return self.f.read(size)


def model_from(label, code, folder, nodes, mds, pack, mot_name, motions, mds_name, wgt_name=None, node_base=0, extra_meshes=()):
    """`wgt_name`: the pack's skin-weight record → exact skinning; `node_base`: the .wgt's node indices are offset by this
    (the cat's cat.wgt keeps the rig's own 0..K-1 numbering, so 0 for every case here)."""
    em._MESH_DIM_CACHE.clear()
    weights = em.load_weights(pack, wgt_name)
    meshes = []
    for n in nodes:
        if n['meshoff']:
            per = weights.get(n['i'] + node_base) if weights else None
            mm = em.build_mesh_weighted(mds, n, nodes, per) if per else em.build_mesh(mds, n, nodes)
            if mm:
                meshes.append(mm)
    meshes += list(extra_meshes(meshes) if callable(extra_meshes) else extra_meshes)   # a callable sees the built meshes (the cape hangs off the skin's)
    tracks = em.build_tracks(pack, mot_name, len(nodes))
    return serialize(label, code, folder, nodes, meshes, tracks, motions, mds_name, mot_name)


def serialize(label, code, folder, nodes, meshes, tracks, motions, mds_name, mot_name):
    maxabs = 1.0
    for m in meshes:
        for arr in (m['p0'], m['p1']):
            for p in arr:
                maxabs = max(maxabs, abs(p[0]), abs(p[1]), abs(p[2]))
    for n in nodes:
        maxabs = max(maxabs, abs(n['T'][0]), abs(n['T'][1]), abs(n['T'][2]))
    for t in tracks:
        if t['chan'] == 2:
            for v in t['vals']:
                maxabs = max(maxabs, abs(v[0]), abs(v[1]), abs(v[2]))
    pos_scale = 32000.0 / maxabs
    jnodes = [{'n': n['name'], 'p': n['parent'], 't': [round(c, 4) for c in n['T']], 'q': [round(x, 6) for x in n['quat']]} for n in nodes]
    jmeshes = []
    for m in meshes:
        jmeshes.append({'node': m['node'], 'skin': 1 if m['skin'] else 0, 'nv': m['nv'], 'nt': len(m['tris']), 'tag': m.get('tag', ''),
                        'b0': em.b64_u16(m['b0']), 'b1': em.b64_u16(m['b1']), 'w0': em.b64_u8([w * 255 for w in m['w0']]),
                        'p0': em.b64_i16([c for p in m['p0'] for c in p], pos_scale), 'p1': em.b64_i16([c for p in m['p1'] for c in p], pos_scale),
                        'tri': em.b64_u16([i for t in m['tris'] for i in t])})
    jtracks = []
    for t in tracks:
        scale = 32767.0 if t['chan'] == 0 else pos_scale
        jtracks.append({'node': t['node'], 'chan': t['chan'], 'nk': len(t['frames']), 'f': em.b64_u16(t['frames']),
                        'v': em.b64_i16([c for v in t['vals'] for c in v], scale)})
    maxframe = max((t['frames'][-1] for t in tracks), default=1)
    return {'label': label, 'group': 'Xiao', 'code': code, 'folder': folder, 'name': folder, 'mds': mds_name, 'mot': mot_name,
            'posScale': pos_scale, 'quatScale': 32767.0, 'maxFrame': maxframe, 'skelOnly': 0,
            'nodes': jnodes, 'meshes': jmeshes, 'motions': motions, 'tracks': jtracks,
            '_stats': {'nodes': len(nodes), 'meshes': len(meshes), 'verts': sum(m['nv'] for m in meshes),
                       'tris': sum(len(m['tris']) for m in meshes), 'tracks': len(tracks), 'motions': len(motions), 'rank': 0}}


def main():
    a = sys.argv[1:]
    iso = a[a.index('--iso') + 1] if '--iso' in a else os.path.expanduser('~/ROMs/Dark Cloud (USA).iso')
    out = a[a.index('--out') + 1] if '--out' in a else os.path.join(HERE, 'cat_viewer.html')
    arc = Archive(iso)

    # ── the in-game cat: the disc bake, assembled in memory ──
    base, cat, flt, glow, dran_bytes = (arc.read(n) for n in (bcp.HOST_CHR, bcp.CAT_CHR, bcp.FLOAT_CHR, bcp.GLOW_SRC, DRAN_CHR))
    packed, rep = bcp.assemble(base, cat, flt, glow, dran_bytes, wings=True)      # the real bake, wings and all
    packed0, rep0 = bcp.assemble(base, cat, flt, glow, wings=False)               # the wingless cat the viewer experiment builds on
    pack = mc.Pack.parse(packed)
    nb, K = rep['nodes']
    mds = bytearray(pack.find(bcp.HOST_MDS).payload)
    root = 0x18 + nb * 0x70
    assert mds[root:root + 0x20].split(b'\0')[0] == bcp.CAT_ROOT_NAME.encode(), 'cat root record'
    m = list(struct.unpack_from('<16f', mds, root + 0x28))              # the bake hides the cat on Xiao by scaling the
    for r in range(3):                                                    # root's bind 3x3 by HIDE_SCALE; the runtime copy
        for c in range(3):                                                # restores 1.0 — do the same for the view
            m[r * 4 + c] /= bcp.HIDE_SCALE
    struct.pack_into('<16f', mds, root + 0x28, *m)
    mds = bytes(mds)
    nodes = subtree_nodes(mds, nb, K)
    motions = [{'name': cm, 'gloss': '', 'start': s, 'end': e, 'speed': sp, 'id': bcp.KEY_START + i, 'empty': 0}
               for i, (s, e, sp, cm) in enumerate(bcp.CAT_KEYS)]
    lo, hi = min(k[0] for k in bcp.CAT_KEYS), max(k[1] for k in bcp.CAT_KEYS)
    motions.append({'name': 'every clip (timeline)', 'gloss': '', 'start': lo, 'end': hi, 'speed': 0.5, 'id': -1, 'empty': 0})
    # seam previews: the game's key-change cross-fade is STATIC — the outgoing clip freezes on its frame, the new key sits on its
    # first frame, and the engine slerps between the two for `steps` game frames; only then does the new clip play (motion-key-blend).
    # Shown as a pre-roll of steps·speed clip frames before the clip. The vertical pounce (DivineBeastCat.cs): ready 95-105 → 10-step
    # fade → float-up 285-294 at 0.75 (feet off at 293, then HELD at 294 for the whole flight) → 16-step fade → leap 205 → hard cut
    # → land 215-227 at 0.36.
    for i_from, i_to, label, steps, speed in ((1, 7, 'ready → float-up', 10, 0.75), (7, 4, 'float-up → leap', 16, None),
                                              (6, 8, 'walk → sit', 10, None), (8, 6, 'sit → walk', 10, None)):
        s0, e0, _, _ = bcp.CAT_KEYS[i_from]; s1, e1, sp1, _ = bcp.CAT_KEYS[i_to]; sp = speed or sp1
        motions.append({'name': f'seam: {label} ({steps}-step fade, then the clip at {sp})', 'gloss': '', 'start': round(s1 - steps * sp, 3),
                        'end': e1, 'speed': sp, 'id': -2, 'empty': 0, 'seam': {'from': e0, 'blend': steps, 'clipStart': s1}})
    game = model_from('Divine Beast cat — as baked into c04b.chr', 'c04b+cat', 'dun/mainchara/c04b.chr (patched)',
                      nodes, mds, pack, 'cat.mot', motions, bcp.HOST_MDS, wgt_name='cat.wgt')
    print(f"in-game cat: {K} nodes appended after her {nb}, {game['_stats']['meshes']} meshes, {game['_stats']['tris']} tris, "
          f"{game['_stats']['tracks']} tracks, {len(motions)} clips; pack {rep['size'][0]:,} → {rep['size'][1]:,} B; textures {rep['textures']}")
    # the experiment builds on the WINGLESS pack (its own graft adds the wings in viewer space)
    pack = mc.Pack.parse(packed0); nb, K = rep0['nodes']
    mds = bytearray(pack.find(bcp.HOST_MDS).payload); root = 0x18 + nb * 0x70
    m = list(struct.unpack_from('<16f', mds, root + 0x28))
    for r in range(3):
        for c in range(3): m[r * 4 + c] /= bcp.HIDE_SCALE
    struct.pack_into('<16f', mds, root + 0x28, *m); mds = bytes(mds)
    nodes = subtree_nodes(mds, nb, K)

    # ── the untouched s86 source cat, every clip its cfg names ──
    src = mc.Pack.parse(cat)
    cfg = em.find_cfg(src)
    mds_name, mot_name, smotions = em.parse_cfg(cfg.payload)
    smds = src.find(mds_name or 'c04cat.mds').payload
    snodes = em.read_skeleton(smds)
    source = model_from('s86 source cat — every clip', 'c04cat', bcp.CAT_CHR.replace('\\', '/'),
                        snodes, smds, src, mot_name or 'c04cat.mot', smotions, mds_name or 'c04cat.mds', wgt_name='c04cat.wgt')
    print(f"source cat: {len(snodes)} nodes, {source['_stats']['tris']} tris, {len(smotions)} clips, max frame {source['maxFrame']}")

    # ── Dran, the wing donor: its rig and every clip (wings = r_wing1..4 / l_wing1..4 bones under the root) ──
    dran_name = r'dun\monstor\c12a.chr'
    dran = mc.Pack.parse(arc.read(dran_name))
    dcfg = em.find_cfg(dran)
    dmds_name, dmot_name, dmotions = em.parse_cfg(dcfg.payload)
    DRAN_GLOSS = {'飛行': 'flying (hover)', '突進（離陸      ）': 'charge: take-off', '突進ループ': 'charge loop', '突進（74': 'charge (end)',
                  'ダメージ': 'damage', '死亡': 'death', '離陸': 'take-off', '火': 'fire breath', '毛繕い(入り）': 'preening (enter)',
                  '毛繕いループ': 'preening loop', '毛繕い（戻り）': 'preening (return)', 'じたじた': 'flailing'}
    for m in dmotions:
        m['gloss'] = DRAN_GLOSS.get(m['name'], m['gloss'] or ('(unnamed)' if not m['name'] else ''))
    dmds = dran.find(dmds_name).payload
    dnodes = em.read_skeleton(dmds)
    donor = model_from('Dran — wing donor, every clip', 'c12a', dran_name.replace('\\', '/'), dnodes, dmds, dran, dmot_name, dmotions, dmds_name, wgt_name='c12a.wgt')
    donor['group'] = 'Dran'
    wings = [n['name'] for n in dnodes if 'wing' in n['name'].lower()]
    print(f"Dran: {len(dnodes)} nodes ({len(wings)} wing bones: {', '.join(wings)}), {donor['_stats']['tris']} tris, {donor['_stats']['tracks']} tracks, {len(dmotions)} clips, max frame {donor['maxFrame']}")

    # Super Steve + an Angel sphere: the blue (wingless) cat with the yellow cape's cloth REST lattice (cat_wings.CAPE_*)
    caped = model_from('Super Steve cat + red cape (cloth rest shape)', 'c04b+cat+cape', 'wingless bake + CAPE_* rest lattice',
                       nodes, mds, pack, 'cat.mot', motions, bcp.HOST_MDS, wgt_name='cat.wgt',
                       extra_meshes=lambda ms: [cw.cape_wind_pose(cw.build_cape_mesh(nodes, next(m for m in ms if nodes[m['node']]['name'] == 'cat_skin')),
                                                                  cw.CAPE_ROWS, cw.CAPE_COLS)]
                       + cw.build_bound_meshes(nodes))
    caped['group'] = 'Xiao'
    print(f"caped cat: cape {cw.CAPE_COLS} wide × {cw.CAPE_ROWS} down, pinned edge resampled from collar verts {cw.CAPE_COLLAR}, hem width {cw.CAPE_WIDTH:g}, length {cw.CAPE_LENGTH:g}, lift {cw.CAPE_LIFT:g}")
    wg = cw.build_winged_cat(nodes, mds, pack, motions, dnodes, dmds, dran, dmot_name)
    winged = serialize("Divine Beast cat + Dran's wings — leap = charge loop, land = flare + fold (experiment)", 'c04b+cat+wings',
                       'viewer experiment on the wingless pack', wg['nodes'], wg['meshes'], wg['tracks'], motions,
                       'c04b.mds + c12a obj1 wings', 'cat.mot + c12a.mot (wings)')
    winged['group'] = 'Xiao'

    tpl = io.open(os.path.join(HERE, 'viewer_template.html'), encoding='utf-8').read()
    html = tpl.replace('<title>Dark Cloud Model Viewer</title>', '<title>Divine Beast Cat Rig</title>')
    # the cape panel opens on the values the game actually ships, read from the two files that hold them — the flat texture the
    # bake writes and the ambient the runtime gives that one cloth — so the viewer can never present stale numbers to tune from
    here = os.path.dirname(os.path.abspath(__file__))
    def _grab(path, pattern, default):
        try: m = re.search(pattern, open(path, encoding='utf-8').read())
        except OSError: m = None
        return [int(float(m.group(i))) for i in (1, 2, 3)] if m else default
    tex = _grab(os.path.join(here, '..', 'iso_patch', 'build_cat_pack.py'),
                r'CAPE_RGBA\s*=\s*\((\d+),\s*(\d+),\s*(\d+)', [128, 28, 0])
    tint = _grab(os.path.join(here, '..', '..', 'Dark Cloud Improved Version', 'Weapons', 'Xiao', 'DivineBeastCat.cs'),
                 r'CapeTint\s*=\s*\{\s*([\d.]+)f?,\s*([\d.]+)f?,\s*([\d.]+)f?', [80, 20, 10])
    html = html.replace('/*__CAPE_DEFAULTS__*/', json.dumps({'tex': tex, 'tint': tint}) + ' || ')
    print(f"cape panel seeded from source: texture {tuple(tex)}, tint {tuple(tint)}")
    html = html.replace('/*__MODEL_DATA__*/', 'const MODELS = ' + json.dumps([winged, game, caped, source, donor], separators=(',', ':'), ensure_ascii=False) + ';')
    io.open(out, 'w', encoding='utf-8').write(html)
    print(f"wrote {out}: {len(html.encode('utf-8')) / 1e6:.2f} MB")


if __name__ == '__main__':
    main()
