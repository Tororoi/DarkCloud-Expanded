#!/usr/bin/env python3
"""
borrow_shot_effects.py — make effect containers loadable through the dungeon's shot-effect pack under the
`dun\\effect` names no species uses (redirected into the DATA.DAT tail).

The pack's loader (Entry__12CSHOT_EFFECT, main ELF 0x1ACC70) takes ONE name from a BT_SHOT_EFFECT config and
uses it twice: `dun/effect/<name>.chr` is the archive file, `<name>.cfg` the record it then asks that container
for. A container whose cfg record is named differently loads nothing (`_b_boll.chr` holds `b_boll.cfg`;
`es_zone.chr` holds `info.cfg`; a copy of another character's effect holds its own name). So for every entry
below the SOURCE container is copied onto the dead name with one record appended — `<name>.cfg`, the source's
cfg payload — and nothing else changed: the cfg text still names the source's own .mds/.img/.mot records,
which are in the copy untouched. The sources themselves are never modified.

The mod fires these with BorrowedShots.CustomConfig(template, name, …) (Weapons/BorrowedShots.cs).

    python3 tools/iso_patch/borrow_shot_effects.py --iso PATH
"""
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', 'lib'))
import ps2iso                      # noqa: E402
import mot_codec as mc             # noqa: E402

SEC = ps2iso.SECTOR

# dead `dun\effect` name → (source container in the archive, the record to expose as <name>.cfg)
BORROWED = {
    '_b_boll':   (r'dun\effect\_b_boll.chr',   'b_boll.cfg'),
    '_f_boll':   (r'dun\effect\_f_boll.chr',   'f_boll.cfg'),
    '_f_boll_2': (r'dun\effect\_f_boll_2.chr', 'f_boll_2.cfg'),
    '_f_boll_3': (r'dun\effect\_f_boll_3.chr', 'f_boll_3.cfg'),
    '_i_boll':   (r'dun\effect\_i_boll.chr',   'i_boll.cfg'),
    'es_zone':   (r'dun\effect\es_zone.chr',   'info.cfg'),
}


def _hd2_slot(hd2_r, i):
    return hd2_r['ext'] * SEC + 16 + i * 32


def _free_tail(f, dat_size, hd2_r, hed):
    end = 0
    for i in range(len(hed) // 80):
        f.seek(_hd2_slot(hd2_r, i))
        off, size = struct.unpack('<II', f.read(8))
        end = max(end, off + size)
    return (end + SEC - 1) & ~(SEC - 1)


def expose(container, name, cfg_record):
    """The container with `<name>.cfg` appended (the payload of `cfg_record`), or None when it is there already."""
    pack = mc.Pack.parse(container)
    if pack.find(name + '.cfg') is not None:
        return None
    src = pack.find(cfg_record)
    if src is None:
        raise SystemExit(f'{name}: no record {cfg_record} in the source container')
    pack.records.append(mc.new_record(name + '.cfg', src.payload))
    out = pack.rebuild()
    check = mc.Pack.parse(out)
    assert check.find(name + '.cfg').payload == src.payload and len(check.records) == len(pack.records)
    return out


def run(iso, log=print):
    if not os.path.exists(iso):
        raise SystemExit(f'ISO not found: {iso}')
    with open(iso, 'r+b') as f:
        recs = ps2iso.parse_root(f)
        hd2_r, dat_r = recs['DATA.HD2'], recs['DATA.DAT']
        dat_iso = dat_r['ext'] * SEC
        dat_size = dat_r['size']
        hed = ps2iso.read_file(f, recs['DATA.HED'])
        tail = _free_tail(f, dat_size, hd2_r, hed)

        def slot_of(name):
            i = ps2iso.archive_find(hed, name)
            if i is None:
                raise SystemExit(f'{name} not in archive')
            return _hd2_slot(hd2_r, i)

        def read_archive(name):
            f.seek(slot_of(name)); off, size = struct.unpack('<II', f.read(8)); f.seek(dat_iso + off); return f.read(size)

        def redirect(name, data):
            nonlocal tail
            slot = slot_of(name)
            if tail + len(data) > dat_size:
                raise SystemExit('out of DATA.DAT tail')
            f.seek(dat_iso + tail); f.write(data)
            sec, cnt = tail >> 11, (len(data) + SEC - 1) // SEC
            f.seek(slot); f.write(struct.pack('<IIII', tail, len(data), sec, cnt))
            f.seek(dat_iso + sec * SEC); assert f.read(len(data)) == data, f'{name} readback'
            log(f'redirected {name}: -> {len(data):,} B @sector {sec:#x}')
            tail = (tail + len(data) + SEC - 1) & ~(SEC - 1)

        done = 0
        for name, (source, cfg_record) in BORROWED.items():
            target = rf'dun\effect\{name}.chr'
            new = expose(read_archive(source), name, cfg_record)
            if new is None:
                log(f'{target}: already exposes {name}.cfg — skipped')
                continue
            redirect(target, new)
            done += 1
        log(f'DONE (borrowed shot effects: {done} redirected)')


def main():
    a = sys.argv[1:]
    if '--iso' not in a:
        raise SystemExit('usage: borrow_shot_effects.py --iso PATH')
    run(a[a.index('--iso') + 1])


if __name__ == '__main__':
    main()
