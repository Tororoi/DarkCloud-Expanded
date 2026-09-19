#!/usr/bin/env python3
"""Bake monster-script changes into the disc (dun/monstor/<code>.stb → DATA.DAT tail redirect).

Scripts cannot grow in place (later labels and every jump target would shift), so a patch REPLACES an existing call
block (PUSH cmd, PUSH args…, EXT) with `CALL_FUNC → appended function` + NOPs for the rest of the block, and appends the
function (strings, a 56-byte funcdata, its code) at the end of the file — every pointer in the format is codeBase-
relative, so appended data is addressable and nothing else moves. (docs/stb-script-format.md: op 3 = PUSHCONST (1 int,
2 float, 3 string), 19 = CALL_FUNC (a2 = funcdata offset from codeBase), 21 = EXT (a1 counts the id), 15 = RET, 22 = NOP.)

Patches (user 2026-09-12):
  Hurt spheres — Blizzard (e65a) → Titan's four (bcol0 5, bcol1 10.6, bcol2 6, bcol3 6); Sam (e86a), Billy (e69a) →
      Mr. Blare's two (bcol0 8.5, bcol1 6.5); Minotaur Joe (c16a) → EVERY hurt sphere armed for the Divine Beast cat:
      each block → its own function = the original _SET_BODY_COL (verbatim) + _SET_BODY_COL_PARA(0, 100) +
      _SET_BODY_COL_PARA(1, 2), then RET — so the script's own follow-up PARA calls still land on the right sphere
      (bak_ColNo). The spare per-sphere table (+0x55490 + j*0x14; every _SET_BODY_COL resets it to 100, no vanilla reader
      or writer): ElfPatches.PatchCatSpherePercent makes CheckDmg use entry [0] for a Xiao-owned hit whose kick type
      equals entry [1] (2 = the cat; pellets carry 0). Xiao's pellets keep the script's own percentages (0 on Joe).
  Mimic wake = a GUARD, not invincibility — all 14 chest-mimics (7 Mimic, 7 King Mimic): the wake label's
      `_STATUS_SET_MUTEKI(100)` (cmd 101: 100 invincibility frames, CheckDmg skips every hit) is NOP'd, and the init
      label gains `_SET_GUARD_FRAME(10, 28)` (King: 10, 27) = the "appear" clip's frames, so plain attacks clink off
      the opening chest while Guard Crush (Dark Cloud, the cat's flight) lands. The 9-frame post-hit and the dying
      invincibility stay.

    python3 tools/iso_patch/patch_monster_scripts.py --iso PATH
"""
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ps2iso                      # noqa: E402

SEC = ps2iso.SECTOR
FN_SET_BODY_COL = 130
FN_SET_BODY_COL_PARA = 134
OP_PUSHCONST, OP_RET, OP_CALL_FUNC, OP_EXT, OP_NOP = 3, 15, 19, 21, 22
FN_STATUS_SET_MUTEKI = 101
FN_SET_GUARD_FRAME = 244
PROBE = b'DCE-SPHERES-v1\0'        # appended marker: the tool's idempotency check
CAT_KICK = 2                       # kick type the Divine Beast cat stamps on its hits (+0x98); pellets are 0
MIMICS = ('e35a', 'e37a', 'e39a', 'e79a', 'e81a', 'e83a', 'e109a')          # appear clip frames 10..28
KING_MIMICS = ('e34a', 'e36a', 'e38a', 'e78a', 'e80a', 'e82a', 'e110a')     # appear clip frames 10..27

# mode 'replace': the FIRST _SET_BODY_COL block calls a function declaring `spheres` [(frame, radius)], every other block is
#                 NOP'd — a whole new hurt-sphere set.
# mode 'arm':     EVERY block calls its own function = that block verbatim + `para` [(cmd, int, int)] — keeps each sphere and
#                 the script's own follow-up PARA calls, adds ours.
PATCHES = {
    r'dun\monstor\e65a.stb': dict(who='Blizzard → Titan', mode='replace', spheres=[('bcol0', 5.0), ('bcol1', 10.6), ('bcol2', 6.0), ('bcol3', 6.0)]),
    r'dun\monstor\e86a.stb': dict(who='Sam → Mr. Blare',   mode='replace', spheres=[('bcol0', 8.5), ('bcol1', 6.5)]),
    r'dun\monstor\e69a.stb': dict(who='Billy → Mr. Blare', mode='replace', spheres=[('bcol0', 8.5), ('bcol1', 6.5)]),
    r'dun\monstor\c16a.stb': dict(who="Minotaur Joe: every hurt sphere cat-hittable", mode='arm',
                                  para=[(FN_SET_BODY_COL_PARA, 0, 100), (FN_SET_BODY_COL_PARA, 1, CAT_KICK)]),
}
# mode 'guard_wake': NOP every `_STATUS_SET_MUTEKI(100)` (the wake's invincibility) and append `_SET_GUARD_FRAME(a, b)` to
#                    the init label — through the existing `_SET_GUARD_FRAME` block if there is one, else the first `_SET_BODY_COL`.
for _code in MIMICS:
    PATCHES[f'dun\\monstor\\{_code}.stb'] = dict(who=f'Mimic {_code}: wake guards instead of 100 invincibility frames', mode='guard_wake', guard=(10.0, 28.0))
for _code in KING_MIMICS:
    PATCHES[f'dun\\monstor\\{_code}.stb'] = dict(who=f'King Mimic {_code}: wake guards instead of 100 invincibility frames', mode='guard_wake', guard=(10.0, 27.0))


def u32(b, o): return struct.unpack_from('<I', b, o)[0]


def call_blocks(stb, cmd):
    """Every `cmd(...)` block as (file offset, instruction count, args): the PUSHes (cmd id + args) AND the EXT that
    consumes them — `argc + 1` instructions, e.g. 4 (48 B) for `_SET_BODY_COL("bcol0", 12.0)`. args are decoded ints /
    floats / strings (None for a non-constant push)."""
    cb = u32(stb, 8)
    out = []
    i = cb
    while i + 12 <= len(stb):
        if u32(stb, i) == OP_EXT and u32(stb, i + 8) == 0:
            argc = u32(stb, i + 4)
            if 2 <= argc <= 10:
                fpos = i - argc * 12
                if fpos >= cb and u32(stb, fpos) == OP_PUSHCONST and u32(stb, fpos + 4) == 1 and u32(stb, fpos + 8) == cmd:
                    args = []
                    for k in range(1, argc):
                        ap = fpos + k * 12
                        op, typ, val = u32(stb, ap), u32(stb, ap + 4), u32(stb, ap + 8)
                        if op == OP_PUSHCONST and typ == 3:
                            so = cb + val
                            args.append(stb[so:stb.find(b'\0', so)].decode('latin1', 'replace'))
                        elif op == OP_PUSHCONST and typ == 2:
                            args.append(struct.unpack('<f', struct.pack('<I', val))[0])
                        elif op == OP_PUSHCONST and typ == 1:
                            args.append(struct.unpack('<i', struct.pack('<I', val))[0])
                        else:
                            args.append(None)
                    out.append((fpos, argc + 1, args))
        i += 4
    return out


def sphere_blocks(stb):
    """`_SET_BODY_COL` blocks as (file offset, instruction count, frame name)."""
    return [(fpos, n, next((a for a in args if isinstance(a, str)), None)) for fpos, n, args in call_blocks(stb, FN_SET_BODY_COL)]


def ins(op, a1=0, a2=0): return struct.pack('<III', op, a1, a2)


def _funcdata(code_off_from_cb):
    return struct.pack('<IIII', code_off_from_cb, 0, 0, 0) + b'\0' * 40      # fd[0] entry (codeBase-relative), no locals, no args


def _push(v):
    if isinstance(v, float):
        return ins(OP_PUSHCONST, 2, struct.unpack('<I', struct.pack('<f', v))[0])
    return ins(OP_PUSHCONST, 1, v & 0xFFFFFFFF)


def _para_code(para):
    return b''.join(ins(OP_PUSHCONST, 1, cmd) + _push(a) + _push(b) + ins(OP_EXT, 3, 0) for cmd, a, b in para)


def patch_script(stb, spec):
    if PROBE in stb:
        return None, 'already patched'
    cb = u32(stb, 8)
    if spec['mode'] == 'guard_wake':
        return _patch_guard_wake(stb, spec, cb)
    blocks = sphere_blocks(stb)
    if not blocks:
        return None, 'no _SET_BODY_COL block found'
    out = bytearray(stb)
    while len(out) % 4:
        out.append(0)
    if spec['mode'] == 'replace':
        stroff = {}
        for name, _ in spec['spheres']:
            if name not in stroff:
                stroff[name] = len(out) - cb
                out += name.encode('ascii') + b'\0'
        out += PROBE
        while len(out) % 4:
            out.append(0)
        fd_off = len(out)
        out += _funcdata(fd_off + 56 - cb)
        for name, r in spec['spheres']:
            out += ins(OP_PUSHCONST, 1, FN_SET_BODY_COL) + ins(OP_PUSHCONST, 3, stroff[name]) \
                 + ins(OP_PUSHCONST, 2, struct.unpack('<I', struct.pack('<f', r))[0]) + ins(OP_EXT, 3, 0)
        out += ins(OP_RET)
        fpos, n, _ = blocks[0]
        out[fpos:fpos + n * 12] = ins(OP_CALL_FUNC, 0, fd_off - cb) + ins(OP_NOP) * (n - 1)
        for fpos2, n2, _ in blocks[1:]:
            out[fpos2:fpos2 + n2 * 12] = ins(OP_NOP) * n2
        return bytes(out), f"{spec['who']}: {len(blocks)} block(s) → one function @+0x{fd_off - cb:X} ({len(spec['spheres'])} spheres), {len(stb):,}→{len(out):,} B"
    # mode 'arm': one function per block — the block verbatim (its string offsets stay valid), our params, RET
    out += PROBE
    while len(out) % 4:
        out.append(0)
    funcs = []
    for fpos, n, name in blocks:
        fd_off = len(out)
        out += _funcdata(fd_off + 56 - cb) + stb[fpos:fpos + n * 12] + _para_code(spec['para']) + ins(OP_RET)
        out[fpos:fpos + n * 12] = ins(OP_CALL_FUNC, 0, fd_off - cb) + ins(OP_NOP) * (n - 1)
        funcs.append(f"{name}@+0x{fd_off - cb:X}")
    return bytes(out), f"{spec['who']}: {len(blocks)} block(s) → {', '.join(funcs)} ({len(spec['para'])} params each), {len(stb):,}→{len(out):,} B"


def _patch_guard_wake(stb, spec, cb):
    muteki = [(f, n) for f, n, args in call_blocks(stb, FN_STATUS_SET_MUTEKI) if args == [100]]
    if not muteki:
        return None, 'no _STATUS_SET_MUTEKI(100) block found'
    anchors = call_blocks(stb, FN_SET_GUARD_FRAME) or call_blocks(stb, FN_SET_BODY_COL)
    if not anchors:
        return None, 'no init-label block to hang the guard frame on'
    fpos, n, _ = anchors[0]
    out = bytearray(stb)
    while len(out) % 4:
        out.append(0)
    out += PROBE
    while len(out) % 4:
        out.append(0)
    fd_off = len(out)
    a, b = spec['guard']
    out += _funcdata(fd_off + 56 - cb) + stb[fpos:fpos + n * 12] + _para_code([(FN_SET_GUARD_FRAME, float(a), float(b))]) + ins(OP_RET)
    out[fpos:fpos + n * 12] = ins(OP_CALL_FUNC, 0, fd_off - cb) + ins(OP_NOP) * (n - 1)
    for mpos, mn in muteki:
        out[mpos:mpos + mn * 12] = ins(OP_NOP) * mn
    return bytes(out), f"{spec['who']}: {len(muteki)} MUTEKI(100) block(s) NOP'd, guard frames {a:g}..{b:g} added via the block @0x{fpos:X} → function @+0x{fd_off - cb:X}, {len(stb):,}→{len(out):,} B"


# ---------------------------------------------------------------- ISO install (the cat pack's recipe)
def _hd2_slot(hd2_r, i): return hd2_r['ext'] * SEC + 16 + i * 32


def _free_tail(f, dat_size, hd2_r, hed):
    n = len(hed) // 80
    end = 0
    for i in range(n):
        f.seek(_hd2_slot(hd2_r, i))
        off, size = struct.unpack('<II', f.read(8))
        end = max(end, off + size)
    return (end + SEC - 1) & ~(SEC - 1)


def run(iso, log=print):
    if not os.path.exists(iso):
        raise SystemExit(f"ISO not found: {iso}")
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
                raise SystemExit(f"{name} not in archive")
            return _hd2_slot(hd2_r, i)

        def read_src(name):
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
            log(f"redirected {name}: -> {len(data):,} B @sector {sec:#x}")
            tail = (tail + len(data) + SEC - 1) & ~(SEC - 1)

        done = 0
        for name, spec in PATCHES.items():
            src = read_src(name)
            new, why = patch_script(src, spec)
            if new is None:
                log(f"{name}: {why} — skipped")
                continue
            log(f"patched {why}")
            redirect(name, new)
            done += 1
        log(f"DONE (monster scripts: {done} redirected)")


def main():
    a = sys.argv[1:]
    if '--iso' not in a:
        raise SystemExit('usage: patch_monster_scripts.py --iso PATH')
    run(a[a.index('--iso') + 1])


if __name__ == '__main__':
    main()
