#!/usr/bin/env python3
"""Bake a self-contained WebGL page of the Divine Beast Title cat EXACTLY as the disc bake grafts it into Xiao's dungeon
pack: tools/iso_patch/build_cat_pack.py's assemble() is run IN MEMORY on the user's ISO sources (nothing is written), and
the cat subtree (catroot + cat_* nodes, its two meshes, the trimmed cat.mot with the float-up graft, and the MOTION 1 KEY
windows the game plays) is exported through the model viewer's codec (extract_model.py) into viewer_template.html.

A second entry shows the untouched s86 source cat (gedit\\s86\\chara\\c04cat.chr) with every clip its own cfg names, for
comparison. Output: cat_viewer.html next to this script (or --out).

    python3 tools/model_viewer/cat_viewer.py [--iso PATH] [--out PATH]
"""
import io
import json
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


def subtree_nodes(mds, first, count):
    """read_skeleton on the whole .mds, then the cat's records re-indexed 0..count-1 (parents likewise, root → −1)
    with bind worlds recomputed from the cat root — so build_mesh's auto-skin only ever binds to CAT joints."""
    allnodes = em.read_skeleton(mds)
    out = []
    for k in range(count):
        n = dict(allnodes[first + k])
        n['i'] = k
        n['parent'] = -1 if n['parent'] < 0 else n['parent'] - first
        out.append(n)
    for n in out:
        L = em.mat_from_rt(n['R'], n['T'])
        n['world'] = L if n['parent'] < 0 else em.mat_mul(L, out[n['parent']]['world'])
        n['invworld'] = em.rigid_inv(n['world'])
        n['worldpos'] = (n['world'][3][0], n['world'][3][1], n['world'][3][2])
    return out


def model_from(label, code, folder, nodes, mds, pack, mot_name, motions, mds_name):
    em._MESH_DIM_CACHE.clear()
    meshes = []
    for n in nodes:
        if n['meshoff']:
            mm = em.build_mesh(mds, n, nodes)
            if mm:
                meshes.append(mm)
    tracks = em.build_tracks(pack, mot_name, len(nodes))
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
        jmeshes.append({'node': m['node'], 'skin': 1 if m['skin'] else 0, 'nv': m['nv'], 'nt': len(m['tris']),
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
    base, cat, flt, glow = (arc.read(n) for n in (bcp.HOST_CHR, bcp.CAT_CHR, bcp.FLOAT_CHR, bcp.GLOW_SRC))
    packed, rep = bcp.assemble(base, cat, flt, glow)
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
    game = model_from('Divine Beast cat — as baked into c04b.chr', 'c04b+cat', 'dun/mainchara/c04b.chr (patched)',
                      nodes, mds, pack, 'cat.mot', motions, bcp.HOST_MDS)
    print(f"in-game cat: {K} nodes appended after her {nb}, {game['_stats']['meshes']} meshes, {game['_stats']['tris']} tris, "
          f"{game['_stats']['tracks']} tracks, {len(motions)} clips; pack {rep['size'][0]:,} → {rep['size'][1]:,} B; textures {rep['textures']}")

    # ── the untouched s86 source cat, every clip its cfg names ──
    src = mc.Pack.parse(cat)
    cfg = em.find_cfg(src)
    mds_name, mot_name, smotions = em.parse_cfg(cfg.payload)
    smds = src.find(mds_name or 'c04cat.mds').payload
    snodes = em.read_skeleton(smds)
    source = model_from('s86 source cat — every clip', 'c04cat', bcp.CAT_CHR.replace('\\', '/'),
                        snodes, smds, src, mot_name or 'c04cat.mot', smotions, mds_name or 'c04cat.mds')
    print(f"source cat: {len(snodes)} nodes, {source['_stats']['tris']} tris, {len(smotions)} clips, max frame {source['maxFrame']}")

    tpl = io.open(os.path.join(HERE, 'viewer_template.html'), encoding='utf-8').read()
    html = tpl.replace('<title>Dark Cloud Model Viewer</title>', '<title>Divine Beast Cat Rig</title>')
    html = html.replace('/*__MODEL_DATA__*/', 'const MODELS = ' + json.dumps([game, source], separators=(',', ':'), ensure_ascii=False) + ';')
    io.open(out, 'w', encoding='utf-8').write(html)
    print(f"wrote {out}: {len(html.encode('utf-8')) / 1e6:.2f} MB")


if __name__ == '__main__':
    main()
