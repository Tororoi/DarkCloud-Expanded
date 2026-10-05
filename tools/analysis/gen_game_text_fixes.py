#!/usr/bin/env python3
r"""Write docs/game-text-fixes.md: every English message the ISO patch's text step changes, stock vs patched.

Usage:  python3 tools/analysis/gen_game_text_fixes.py <stock iso> <patched iso> [out.md]

Both ISOs are read through DATA.HED/HD2/DAT; every `*_1.mes` and English `.bin` message bank is decoded (meswin 16-bit
glyph text, see mes_decode.py; 0xFAFA..0xFAFF are the party's name tokens) and compared message by message. A message is shown in full when short, otherwise as a
window around each changed span. `/` marks a line break, `¶` a page break.
"""
import difflib, os, struct, sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'iso_patch'))
import ps2iso

GL = {0x55: "'", 0x57: '"', 0x58: '!', 0x59: '?', 0x5B: '&', 0x5D: '-', 0x5F: '/', 0x60: '%',
      0x61: '(', 0x62: ')', 0x6B: ':', 0x6C: ',', 0x6D: '.'}
UNUSED = ('meswin\\_systeme.bin', 'meswin\\_system14e.bin')     # JP-era copies the USA executable never names


def dec(w):
    g, hi = w & 0xFF, w >> 8
    if hi == 0xFF: return {0: ' / ', 1: '', 2: ' ', 3: ' ¶ '}.get(g, '')
    if hi == 0xFC: return ''                                        # text colour
    if hi == 0xFA and 0xFA <= g <= 0xFF: return '[' + ('Toan', 'Xiao', 'Goro', 'Ruby', 'Ungaga', 'Osmond')[g - 0xFA] + ']'
    if hi == 0xFD:
        if 0x21 <= g <= 0x3A: return chr(g + 0x20)
        if 0x3B <= g <= 0x54: return chr(g + 0x26)
        if 0x6F <= g <= 0x78: return chr(g - 0x6F + 0x30)
        return GL.get(g, '{%02x}' % g)
    return '<%04x>' % w


def open_iso(path):
    f = open(path, 'rb'); recs = ps2iso.parse_root(f)
    hed = ps2iso.read_file(f, recs['DATA.HED']); hd2 = ps2iso.read_file(f, recs['DATA.HD2'])
    dat = recs['DATA.DAT']['ext'] * 2048
    names = [hed[i * 80:i * 80 + 80].split(b'\0')[0].decode('latin1') for i in range(len(hed) // 80)]

    def read(i):
        off, size = struct.unpack_from('<II', hd2, 16 + i * 32); f.seek(dat + off); return f.read(size)
    return names, read


def messages(mes):
    """(index, id) -> glyph words through the 0xFF01 terminator."""
    cnt = struct.unpack_from('<H', mes, 0)[0]; out = {}
    for i in range(cnt):
        mid, woff = struct.unpack_from('<HH', mes, 4 + i * 4); tb = 2 * (cnt + woff + 1); ws = []
        while tb + 1 < len(mes):
            w = struct.unpack_from('<H', mes, tb)[0]; ws.append(w); tb += 2
            if w == 0xFF01: break
        out[(i, mid)] = ws
    return out


def is_bank(name):
    return name.endswith('_1.mes') or name in ('meswin\\systeme.bin', 'meswin\\system14e.bin', 'meswin\\system_ae.bin',
                                               'gedit\\system\\editsys.bin') or name in UNUSED


def excerpt(a, b, full=120, ctx=28):
    """The pair as shown: whole texts when short, else a window around each changed span."""
    if len(a) <= full and len(b) <= full: return a, b
    sm = difflib.SequenceMatcher(None, a, b, autojunk=False)
    ops = [o for o in sm.get_opcodes() if o[0] != 'equal']
    outa, outb = [], []
    for _, i1, i2, j1, j2 in ops:
        sa, ea = max(0, i1 - ctx), min(len(a), i2 + ctx); sb, eb = max(0, j1 - ctx), min(len(b), j2 + ctx)
        outa.append(('…' if sa else '') + a[sa:ea] + ('…' if ea < len(a) else ''))
        outb.append(('…' if sb else '') + b[sb:eb] + ('…' if eb < len(b) else ''))
    return ' · '.join(outa), ' · '.join(outb)


def main():
    stock, patched = sys.argv[1], sys.argv[2]
    out = sys.argv[3] if len(sys.argv) > 3 else os.path.join(os.path.dirname(__file__), '..', '..', 'docs', 'game-text-fixes.md')
    names, ra = open_iso(stock); names_b, rb = open_iso(patched)
    assert names == names_b, 'the two ISOs index different files'
    sections, total = [], 0
    for i, n in enumerate(names):
        if not is_bank(n): continue
        ma, mb = messages(ra(i)), messages(rb(i))
        rows = []
        for k in sorted(ma):
            if ma[k] == mb.get(k): continue
            if not any(w >> 8 == 0xFD for w in ma[k]): continue        # an unterminated junk entry whose padding a relocated text now occupies
            a, b = excerpt(dec_all(ma[k]), dec_all(mb[k]))
            rows.append(f'- **#{k[1]}** {a}  \n  → {b}')
        if rows: sections.append((n, rows)); total += len(rows)

    lines = ['# Game text fixes', '',
             'Every message in the game\'s English text that the ISO patch\'s `text-fixes` step changes, stock on the first line and',
             'patched on the second (`/` line break, `¶` page break). The step edits each bank in place (`IsoPatch/MesTextFixes.cs`:',
             'whole-word substitutions plus the two spacing rules); the mod\'s own dialogue is not part of this list.', '',
             f'{total} messages in {len(sections)} banks. Regenerate with `tools/analysis/gen_game_text_fixes.py <stock iso> <patched iso>`.', '']
    used = [s for s in sections if s[0] not in UNUSED]; unused = [s for s in sections if s[0] in UNUSED]
    for n, rows in used:
        lines += [f'## `{n}`', ''] + rows + ['']
    if unused:
        lines += ['## Unused copies', '', 'The USA executable never loads these JP-era banks; the step patches them for completeness.', '']
        for n, rows in unused:
            lines += [f'### `{n}`', ''] + rows + ['']
    with open(out, 'w') as f: f.write('\n'.join(lines))
    print(f'{total} messages in {len(sections)} banks -> {os.path.relpath(out)}')


def dec_all(ws): return ''.join(dec(w) for w in ws).strip()


if __name__ == '__main__':
    main()
