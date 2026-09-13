#!/usr/bin/env python3
"""Bake a self-contained WebGL page that shows, for every enemy species, its bind-pose mesh (solid or wireframe),
its hurt spheres (`_SET_BODY_COL`, decoded from dun/monstor/<code>.stb with codeBase-relative frame names and
resolved to the named frame's bind position), its root position (the model origin — what the game moves the
enemy by) and its lock-on point.

Reads everything from the user's ISO (read-only). Output: enemy_hitboxes.html next to this script (or --out).

    python3 tools/analysis/enemy_hitbox_viewer.py [--iso PATH] [--out PATH] [--patched]

--patched applies tools/iso_patch/patch_monster_spheres.py's script rewrites in memory (the ISO is never written) so the
atlas shows the spheres the patched disc declares (Blizzard, Sam, Billy, Minotaur Joe's cat-hittable face).
"""
import base64
import io
import json
import math
import os
import re
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'iso_patch'))
sys.path.insert(0, os.path.join(HERE, '..', 'lib'))
sys.path.insert(0, os.path.join(HERE, '..', 'model_viewer'))
import ps2iso                       # noqa: E402
import mot_codec as mc              # noqa: E402
import extract_model as em          # noqa: E402
import patch_monster_spheres as pm  # noqa: E402  (the disc bake's sphere rewrites, applied in memory with --patched)

SRC = os.path.join(HERE, '..', '..', 'Dark Cloud Improved Version', 'GameData', 'EnemyData.cs')
FN_SET_BODY_COL = 130
FN_SET_DMG_COL = 131                # attack spheres (_SET_DMG_COL) — shown in a second colour
FN_SET_BODY_COL_PARA = 134          # (index, %) on the sphere set by the LAST _SET_BODY_COL: index 10+char = damage % by attacker (Toan..Osmond)
CHARS = ['Toan', 'Xiao', 'Goro', 'Ruby', 'Ungaga', 'Osmond']


# ---------------------------------------------------------------- archive
class Archive:
    def __init__(self, iso):
        self.f = open(iso, 'rb')
        recs = ps2iso.parse_root(self.f)
        self.hed = ps2iso.read_file(self.f, recs['DATA.HED'])
        self.hd2 = recs['DATA.HD2']
        self.dat_iso = recs['DATA.DAT']['ext'] * ps2iso.SECTOR
        self.names = [self.hed[i * 80:i * 80 + 80].split(b'\0')[0].decode('latin1') for i in range(len(self.hed) // 80)]

    def read(self, name):
        i = ps2iso.archive_find(self.hed, name)
        if i is None:
            return None
        slot = self.hd2['ext'] * ps2iso.SECTOR + 16 + i * 32
        self.f.seek(slot)
        off, size = struct.unpack('<II', self.f.read(8))
        self.f.seek(self.dat_iso + off)
        return self.f.read(size)

    def find(self, prefix, ext):
        p = prefix.lower()
        exact = [n for n in self.names if n.lower() == p + ext]
        if exact:
            return exact[0]
        cands = sorted(n for n in self.names if n.lower().startswith(p) and n.lower().endswith(ext) and not n.endswith('_'))
        return cands[0] if cands else None


# ---------------------------------------------------------------- spheres
def script_calls(stb, fns):
    """Every EXT call of one of `fns` in a monster script, in script order: (fn, [args]) with args typed
    ('str', name) / ('f', float) / ('i', int). Strings are codeBase-relative (extract_bodycol.py reads them
    absolute and mislabels most bones as root). A CALL_FUNC (op 19) is followed into its function — walked
    instruction by instruction up to its RET — so calls made from an appended function (the disc bake in
    tools/iso_patch/patch_monster_spheres.py) land at the call site, in order, and are not counted again when the
    linear scan reaches the function's own bytes."""
    W = lambda o: struct.unpack_from('<I', stb, o)[0] if o + 4 <= len(stb) else 0
    if W(0) != 0x425453:
        return []
    cb = W(8)
    n = len(stb)

    def call_at(i):
        """(fn, args) if the EXT at `i` ends a call to one of `fns`."""
        if not (W(i) == 21 and W(i + 8) == 0):
            return None
        argc = W(i + 4)
        if not 2 <= argc <= 10:
            return None
        fpos = i - argc * 12
        if fpos < cb or W(fpos) != 3 or W(fpos + 4) != 1 or W(fpos + 8) not in fns:
            return None
        args = []
        for k in range(1, argc):
            ap = fpos + k * 12
            o, a, bb = W(ap), W(ap + 4), W(ap + 8)
            if o == 3 and a == 3:
                e = stb.find(b'\0', cb + bb)
                args.append(('str', stb[cb + bb:e].decode('latin1', 'replace')))
            elif o == 3 and a == 2:
                args.append(('f', struct.unpack('<f', struct.pack('<I', bb))[0]))
            elif o == 3 and a == 1:
                args.append(('i', struct.unpack('<i', struct.pack('<I', bb))[0]))
        return (W(fpos + 8), args)

    out, consumed = [], set()
    i = cb
    while i + 12 <= n:
        if i in consumed:
            i += 4
            continue
        if W(i) == 19 and W(i + 4) == 0 and cb + W(i + 8) + 56 <= n:          # CALL_FUNC → funcdata → its code
            pc = cb + W(cb + W(i + 8))
            steps = 0
            while cb <= pc and pc + 12 <= n and W(pc) != 15 and steps < 4096:
                c = call_at(pc)
                if c:
                    out.append(c)
                    consumed.add(pc)
                pc += 12
                steps += 1
        c = call_at(i)
        if c:
            out.append(c)
        i += 4
    return out


def decode_spheres(stb, fn):
    """(frameName, radius, winStart, winEnd, pct[6], spare[5]) for every `fn` call; pct = the damage % per attacker
    character (default 100) set by the _SET_BODY_COL_PARA calls that follow a hurt sphere's declaration; spare = the
    sphere's spare 5-int table (PARA index 0..4, default 100; the mod's ELF patch reads [1] = kick type admitted for
    Xiao at [0] % — the Divine Beast cat on Minotaur Joe's face)."""
    out = []
    for f, args in script_calls(stb, (fn, FN_SET_BODY_COL_PARA)):
        if f == fn:
            name = next((a[1] for a in args if a[0] == 'str'), None)
            nums = [a[1] for a in args if a[0] in ('f', 'i')]
            if name is not None and nums:
                out.append([name, nums[0], nums[1] if len(nums) > 1 else 0.0, nums[2] if len(nums) > 2 else 0.0, [100] * 6, [100] * 5])
        elif f == FN_SET_BODY_COL_PARA and out and fn == FN_SET_BODY_COL and len(args) >= 2 and args[0][0] == 'i' and args[1][0] == 'i':
            idx, val = args[0][1], args[1][1]
            if 10 <= idx < 16:
                out[-1][4][idx - 10] = val
            elif 0 <= idx < 5:
                out[-1][5][idx] = val
    return [tuple(o) for o in out]


# ---------------------------------------------------------------- species
def species_rows():
    src = io.open(SRC, encoding='utf-8').read()
    rows = []
    for m in re.finditer(r'EnemyDefaults (\w+) = new EnemyDefaults \{(.*?)\};', src, re.S):
        body = ' '.join(m.group(2).split())
        name = re.search(r'Name="([^"]*)"', body)
        model = re.search(r'ModelCode="([^"]*)"', body)
        ti = re.search(r'TableIndex=(\d+)', body)
        idv = re.search(r'\bId=(\d+)', body)
        rows.append(dict(field=m.group(1), name=name.group(1) if name else m.group(1),
                         model=model.group(1) if model else None,
                         ti=int(ti.group(1)) if ti else None, id=int(idv.group(1)) if idv else None))
    rows.sort(key=lambda r: (r['ti'] is None, r['ti'] or 0))
    return rows


def b64_i16(vals, scale):
    return base64.b64encode(struct.pack('<%dh' % len(vals), *[max(-32768, min(32767, int(round(v * scale)))) for v in vals])).decode('ascii')


def b64_u16(vals):
    return base64.b64encode(struct.pack('<%dH' % len(vals), *vals)).decode('ascii')


def build_species(arc, row, patched_spec=None):
    code = row['model']
    chr_name = arc.find('dun\\monstor\\' + code, '.chr')
    if not chr_name:
        return None, 'no .chr'
    pack = mc.Pack.parse(arc.read(chr_name))
    # the BODY model is the cfg's MODEL line — the first .mds in a pack is often the shadow rig (e43s before e43a)
    mds_rec = None
    for rec in pack.records:
        if rec.name.lower().endswith('.cfg'):
            mm = re.search(r'\bMODEL\s+"([^"]+)"', rec.payload.decode('shift_jis', 'replace'))
            if mm and pack.find(mm.group(1)) is not None:
                mds_rec = pack.find(mm.group(1))
                break
    if mds_rec is None:
        cands = [r for r in pack.records if r.name.lower().endswith('.mds')]
        mds_rec = max(cands, key=lambda r: struct.unpack_from('<I', r.payload, 8)[0], default=None)
    if mds_rec is None:
        return None, 'no .mds'
    mds = mds_rec.payload
    nodes = em.read_skeleton(mds)
    # bind-pose meshes in MODEL space (each node's MDT verts are in that node's local space)
    verts, tris = [], []
    for nd in nodes:
        if not nd['meshoff']:
            continue
        try:
            m = em.parse_mdt(mds, nd['meshoff'])
        except Exception:
            continue
        t = em.mdt_triangles(m)
        if not t:
            continue
        base = len(verts)
        for v in m.pos:
            verts.append(em.xform_pt(nd['world'], v[:3]))
        tris.extend((base + a, base + b, base + c) for a, b, c in t)
    byname = {nd['name']: nd for nd in nodes}
    stb_name = arc.find('dun\\monstor\\' + code, '.stb')
    stb = arc.read(stb_name) if stb_name else None
    patched = False
    if stb and patched_spec is not None:                     # --patched: show the disc bake's spheres, not the vanilla script's
        spec = patched_spec.get(stb_name.lower())
        if spec:
            new, _ = pm.patch_script(stb, spec)
            if new:
                stb, patched = new, True
    spheres, attack = [], []
    if stb:
        for kind, fn, dest in (('hurt', FN_SET_BODY_COL, spheres), ('attack', FN_SET_DMG_COL, attack)):
            for name, r, w0, w1, pct, spare in decode_spheres(stb, fn):
                nd = byname.get(name)
                pos = nd['worldpos'] if nd else (0.0, 0.0, 0.0)
                d = {'frame': name, 'found': nd is not None, 'r': round(r, 3), 'w0': round(w0, 1), 'w1': round(w1, 1),
                     'p': [round(c, 3) for c in pos], 'pct': pct}
                if kind == 'hurt' and spare[1] == pm.CAT_KICK:
                    d['catpct'] = spare[0]                   # the cat's own % on this sphere (ELF PatchCatSpherePercent)
                dest.append(d)
    # the Divine Beast cat's pick: the biggest sphere that can take its damage (cat % if armed, else Xiao's %); ties → furthest forward (+z)
    live = [s for s in spheres if s.get('catpct', s['pct'][1]) > 0] or spheres
    if live:
        rmax = max(s['r'] for s in live)
        pick = max((s for s in live if s['r'] >= rmax - 0.01), key=lambda s: s['p'][2])
        pick['cat'] = True
    lock = byname.get('lockon')
    maxabs = 1.0
    for v in verts:
        maxabs = max(maxabs, abs(v[0]), abs(v[1]), abs(v[2]))
    scale = 32000.0 / maxabs
    bones = [{'n': nd['name'], 'p': nd['parent'], 'w': [round(c, 3) for c in nd['worldpos']]} for nd in nodes]
    return {
        'name': row['name'], 'id': row['id'], 'ti': row['ti'], 'code': code, 'chr': chr_name.replace('\\', '/'), 'patched': patched,
        'stb': (stb_name or '').replace('\\', '/'), 'nv': len(verts), 'nt': len(tris), 'scale': scale,
        'v': b64_i16([c for v in verts for c in v], scale), 't': b64_u16([i for t in tris for i in t]),
        'bones': bones, 'spheres': spheres, 'attack': attack,
        'lockon': [round(c, 3) for c in lock['worldpos']] if lock else None,
    }, None


# ---------------------------------------------------------------- page
def bake(models, out_path):
    tpl = io.open(os.path.join(HERE, 'enemy_hitbox_viewer_template.html'), encoding='utf-8').read()
    data = json.dumps(models, separators=(',', ':'), ensure_ascii=False)
    html = tpl.replace('/*__MODELS__*/[]', data)
    io.open(out_path, 'w', encoding='utf-8').write(html)
    return len(html.encode('utf-8'))


def main():
    a = sys.argv[1:]
    iso = a[a.index('--iso') + 1] if '--iso' in a else os.path.expanduser('~/ROMs/Dark Cloud (USA).iso')
    out = a[a.index('--out') + 1] if '--out' in a else os.path.join(HERE, 'enemy_hitboxes.html')
    arc = Archive(iso)
    patched_spec = {k.lower(): v for k, v in pm.PATCHES.items()} if '--patched' in a else None
    models, seen = [], {}
    for row in species_rows():
        if not row['model']:
            continue
        if row['model'] in seen:                       # same model as an earlier row (Enhanced tiers): share it
            models[seen[row['model']]]['also'].append(row['name'])
            continue
        m, why = build_species(arc, row, patched_spec)
        if m is None:
            sys.stderr.write(f"  skip {row['name']} ({row['model']}): {why}\n")
            continue
        m['also'] = []
        seen[row['model']] = len(models)
        models.append(m)
    size = bake(models, out)
    total_tris = sum(m['nt'] for m in models)
    print(f"wrote {out}: {len(models)} models, {total_tris:,} tris, {size / 1e6:.1f} MB")


if __name__ == '__main__':
    main()
