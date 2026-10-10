#!/usr/bin/env python3
"""Generate MonsterSoundBake.SpeciesUnits: the sound units each species' sounds come from.

    python3 tools/analysis/gen_species_sound_units.py "<Dark Cloud (USA).iso>"

A monster sound id (601-2596) resolves through the ELF's static se_info table (6 B rows @0x25DFB0: program,
note, -, port, vol) to a (program, note) of the monster bank on MIDI port 10. A species plays its ids from three
places: its script (`_SET_SND_NOW` / `_SET_SND_FRM` / `_SET_LOOP_SND`, read here through stbdis, whose listing folds
`2501 + 6` style expressions), its model's `EVENT frame, 1, id` cfg lines, and the `sound[4]` of its two shot
configs (BT_SHOT_EFFECT +0x60). Script calls whose id is a function argument resolve at their callers within the
same program, so the programs of the resolved ids cover them. A program carries up to three species' sounds, each on its own
30-note window (notes 20-49, 50-79, 80-109; a species' ids are a run of one window), so a species' unit is (program, window),
printed as program × 4 + window. One C# row per species-table index.
"""
import os
import re
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'iso_patch'))
sys.path.insert(0, os.path.join(HERE, '..', 'lib'))
import ps2iso                                   # noqa: E402
import mot_codec as mc                          # noqa: E402
import stbdis                                   # noqa: E402

ELF_BASE = 0xFFF00                              # file offset = address - this (the ELF's one load segment)
SE_INFO, SE_ROWS = 0x25DFB0, 2800
SPECIES, STRIDE, VANILLA_ROWS = 0x27FB00, 0x9C, 167
SHOT_TABLE, SHOT_CONFIGS = 0x27FA70, 34
MONSTER_PORT = 10
SOUND_CALLS = ('_SET_SND_NOW', '_SET_SND_FRM', '_SET_LOOP_SND')
NOTE_FIRST, NOTE_WINDOW = 20, 30


class Archive:
    def __init__(self, iso):
        self.f = open(iso, 'rb')
        recs = ps2iso.parse_root(self.f)
        self.elf = ps2iso.read_file(self.f, recs['SCUS_971.11'])
        self.hed = ps2iso.read_file(self.f, recs['DATA.HED'])
        self.hd2 = recs['DATA.HD2']['ext'] * ps2iso.SECTOR
        self.dat = recs['DATA.DAT']['ext'] * ps2iso.SECTOR

    def read(self, name):
        i = ps2iso.archive_find(self.hed, name)
        if i is None:
            return None
        self.f.seek(self.hd2 + 16 + i * 32)
        off, size = struct.unpack('<II', self.f.read(8))
        self.f.seek(self.dat + off)
        return self.f.read(size)


def script_ids(stb):
    d = stbdis.Dis(stb)
    funcs = d.functions()
    text = '\n'.join(line for hdr in sorted(funcs) for line in d.listing(hdr, funcs[hdr]))
    ids = set()
    for m in re.finditer(r'(%s)\((.*)\)\s*$' % '|'.join(SOUND_CALLS), text, re.M):
        depth, cur, parts = 0, '', []
        for ch in m.group(2):
            depth += (ch == '(') - (ch == ')')
            if ch == ',' and depth == 0:
                parts.append(cur.strip()); cur = ''
            else:
                cur += ch
        parts.append(cur.strip())
        for a in parts:
            if re.fullmatch(r'[\d\s+\-*()]+', a):
                v = eval(a)
                if 0 <= v < SE_ROWS:
                    ids.add(v)
    return ids


def model_ids(chr_bytes):
    ids = set()
    for r in mc.Pack.parse(chr_bytes).records:
        if r.name.lower().endswith('.cfg'):
            for m in re.finditer(rb'EVENT\s+[\d.\-]+\s*,\s*1\s*,\s*(\d+)', r.payload):
                ids.add(int(m.group(1)))
    return ids


def main(iso):
    arc = Archive(iso)
    elf = arc.elf
    se = [struct.unpack_from('<bbbbh', elf, SE_INFO - ELF_BASE + i * 6) for i in range(SE_ROWS)]

    def shot_ids(i):
        if not 0 <= i < SHOT_CONFIGS:
            return set()
        cfg = struct.unpack_from('<I', elf, SHOT_TABLE - ELF_BASE + i * 4)[0]
        return {v for v in struct.unpack_from('<4i', elf, cfg - ELF_BASE + 0x60) if v >= 0} if cfg else set()

    for t in range(VANILLA_ROWS):
        row = SPECIES - ELF_BASE + t * STRIDE
        model = elf[row:row + 16].split(b'\0')[0].decode('latin1')
        script = elf[row + 0x40:row + 0x50].split(b'\0')[0].decode('latin1')
        s0, s1 = struct.unpack_from('<hh', elf, row + 0x68)
        ids = shot_ids(s0) | shot_ids(s1)
        stb = arc.read(f'dun\\monstor\\{script}.stb')
        if stb:
            ids |= script_ids(stb)
        chr_bytes = arc.read(f'dun\\monstor\\{model}.chr')
        if chr_bytes:
            ids |= model_ids(chr_bytes)
        units = sorted({(se[i][0], (se[i][1] - NOTE_FIRST) // NOTE_WINDOW) for i in ids if se[i][3] == MONSTER_PORT})
        if len(units) > 2:
            raise SystemExit(f'{t} {model}: {units} — more than the table holds')
        cells = ', '.join([f'{p} * 4 + {w}' for p, w in units] + ['N'] * (2 - len(units)))
        print(f'            {{ {cells} }},   // {t:3} {model}')


if __name__ == '__main__':
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    main(sys.argv[1])
