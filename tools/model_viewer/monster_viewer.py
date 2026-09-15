#!/usr/bin/env python3
"""Bake a self-contained WebGL page of one or more MONSTER rigs (dun\\monstor\\<code>.chr) with every clip their cfg names,
through the model viewer's codec (extract_model.py + viewer_template.html). Read-only on the ISO.

    python3 tools/model_viewer/monster_viewer.py e34a e35a [--iso PATH] [--out PATH]
"""
import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'iso_patch'))
sys.path.insert(0, os.path.join(HERE, '..', 'lib'))
sys.path.insert(0, os.path.join(HERE, '..', 'analysis'))
sys.path.insert(0, HERE)
import mot_codec as mc                         # noqa: E402
import extract_model as em                     # noqa: E402
import cat_viewer as cv                        # noqa: E402  (Archive + model_from)
from enemy_hitbox_viewer import species_rows   # noqa: E402


def main():
    a = sys.argv[1:]
    iso = a[a.index('--iso') + 1] if '--iso' in a else os.path.expanduser('~/ROMs/Dark Cloud (USA).iso')
    out = a[a.index('--out') + 1] if '--out' in a else os.path.join(HERE, 'monster_viewer.html')
    codes = [x for x in a if not x.startswith('--') and x not in (iso, out)]
    if not codes:
        raise SystemExit('give at least one model code, e.g. e34a')
    names = {r['model']: r['name'] for r in species_rows() if r['model']}
    arc = cv.Archive(iso)
    models = []
    for code in codes:
        chr_name = next((n for n in (f'dun\\monstor\\{code}.chr',) if True), None)
        pack = mc.Pack.parse(arc.read(chr_name))
        cfg = em.find_cfg(pack)
        mds_name, mot_name, motions = em.parse_cfg(cfg.payload)
        mds = pack.find(mds_name).payload
        nodes = em.read_skeleton(mds)
        label = f"{names.get(code, code)} ({code})"
        wm = re.search(rb'MOTION\s+\d+\s*,\s*"[^"]+"\s*,\s*"[^"]*"\s*,\s*"([^"]+\.wgt)"', cfg.payload)
        m = cv.model_from(label, code, chr_name.replace('\\', '/'), nodes, mds, pack, mot_name, motions, mds_name,
                          wgt_name=wm.group(1).decode('latin1') if wm else None)
        m['group'] = names.get(code, code)
        models.append(m)
        print(f"{label}: {len(nodes)} nodes, {m['_stats']['tris']} tris, {m['_stats']['tracks']} tracks, {len(motions)} clips, max frame {m['maxFrame']}")
    tpl = io.open(os.path.join(HERE, 'viewer_template.html'), encoding='utf-8').read()
    title = names.get(codes[0], codes[0]) + ' Rig'
    html = tpl.replace('<title>Dark Cloud Model Viewer</title>', f'<title>{title}</title>')
    html = html.replace('/*__MODEL_DATA__*/', 'const MODELS = ' + json.dumps(models, separators=(',', ':'), ensure_ascii=False) + ';')
    io.open(out, 'w', encoding='utf-8').write(html)
    print(f"wrote {out}: {len(html.encode('utf-8')) / 1e6:.2f} MB")


if __name__ == '__main__':
    main()
