#!/usr/bin/env python3
"""Parse a PCSX2 GS dump (.gs / .gs.zst / .gs.xz): decode the GIF command stream and report, in order,
every GS-local TEXTURE UPLOAD (BITBLTBUF/TRXPOS/TRXREG/TRXDIR + IMAGE data) and every TEX0 texture BIND,
with frame (vsync) boundaries. Built 2026-09 for the Brownboo stilts investigation (memory
fishing-stilts-texture-block): shows exactly which transfer overwrites which GS blocks and where each
draw samples from — the ground truth static RE could not reach.

  python3 tools/analysis/parse_gs_dump.py dump.gs.zst [--window lo hi] [--all]

Addresses printed in GS BLOCK units (1 block = 256 bytes = 64 words), the same units as TEX0.TBP0 /
BITBLTBUF.DBP and CTextureManager's per-block GS pointers (e.g. the shared scratch 0x1A40).
"""
import struct, sys, os

def load(path):
    raw = open(path, 'rb').read()
    if path.endswith('.zst'):
        import zstandard
        raw = zstandard.ZstdDecompressor().decompress(raw, max_output_size=1 << 31)
    elif path.endswith('.xz') or raw[:6] == b'\xfd7zXZ\x00':
        import lzma
        raw = lzma.decompress(raw)
    return raw

def parse_header(b):
    crc = struct.unpack_from('<I', b, 0)[0]
    if crc != 0xFFFFFFFF:                                    # old-style: crc, state_size, state
        state_size = struct.unpack_from('<I', b, 4)[0]
        pos = 8 + state_size
    else:                                                    # new-style header (v2.7 layout, RE'd 2026-09-01):
        # +0x04 state offset (relative to byte 8), +0x08 state version, +0x0C state size,
        # +0x10 serial offset (rel 8), +0x14 serial size, +0x18 crc, +0x1C/+0x20 screenshot w/h,
        # +0x24 screenshot offset (rel 8), +0x28 screenshot size. Serial + screenshot precede the state.
        (state_rel, state_version, state_size, serial_off, serial_size, crc,
         shot_w, shot_h, shot_off, shot_size) = struct.unpack_from('<10I', b, 4)
        pos = 8 + max(state_rel, serial_off + serial_size, shot_off + shot_size) + state_size
    pos += 8192                                              # GS priv regs snapshot
    return pos

PSM_BPP = {0x00: 32, 0x01: 32, 0x02: 16, 0x0A: 16, 0x13: 8, 0x14: 4, 0x1B: 32, 0x24: 32, 0x2C: 32,
           0x30: 32, 0x31: 32, 0x32: 16, 0x3A: 16}           # bits/px (approx; H-variants counted as 32)
PSM_NAME = {0x00: 'CT32', 0x01: 'CT24', 0x02: 'CT16', 0x0A: 'CT16S', 0x13: 'T8', 0x14: 'T4',
            0x1B: 'T8H', 0x24: 'T4HL', 0x2C: 'T4HH', 0x30: 'Z32', 0x31: 'Z24', 0x32: 'Z16', 0x3A: 'Z16S'}

class Ctx:
    def __init__(self):
        self.frame = 0
        self.seq = 0
        self.events = []            # ('up', frame, seq, path, dbp, dpsm, dbw, dsax, dsay, rrw, rrh, qwc)
                                    # ('tex', frame, seq, path, ctxid, tbp, tbw, psm, w, h, cbp)
                                    # ('vsync', frame)
        self.bitblt = 0
        self.trxpos = 0
        self.trxreg = 0
        self.cur_up = None
        self.image_left = 0         # qwords of IMAGE data still expected (from NLOOP; may span tags)

    def ad_write(self, addr, val, path):
        addr &= 0x7F
        if addr == 0x50: self.bitblt = val
        elif addr == 0x51: self.trxpos = val
        elif addr == 0x52: self.trxreg = val
        elif addr == 0x53:
            if val & 3 == 0:                                 # host -> local: an upload begins
                dbp = (self.bitblt >> 32) & 0x3FFF
                dbw = (self.bitblt >> 48) & 0x3F
                dpsm = (self.bitblt >> 56) & 0x3F
                dsax = (self.trxpos >> 32) & 0x7FF
                dsay = (self.trxpos >> 43) & 0x7FF
                rrw = self.trxreg & 0xFFF
                rrh = (self.trxreg >> 32) & 0xFFF
                self.cur_up = ['up', self.frame, self.seq, path, dbp, dpsm, dbw, dsax, dsay, rrw, rrh, 0]
                self.events.append(self.cur_up)
                self.seq += 1
        elif addr in (0x16, 0x17):                           # TEX2_1/TEX2_2: CLUT swap (cbp/psm only)
            psm = (val >> 20) & 0x3F
            cbp = (val >> 37) & 0x3FFF
            self.events.append(('tex2', self.frame, self.seq, path, addr - 0x16, cbp, psm))
            self.seq += 1
        elif addr in (0x06, 0x07):                           # TEX0_1 / TEX0_2
            tbp = val & 0x3FFF
            tbw = (val >> 14) & 0x3F
            psm = (val >> 20) & 0x3F
            w = 1 << ((val >> 26) & 0xF)
            h = 1 << ((val >> 30) & 0xF)
            cbp = (val >> 37) & 0x3FFF
            self.events.append(('tex', self.frame, self.seq, path, addr - 0x06, tbp, tbw, psm, w, h, cbp))
            self.seq += 1

def parse_gif(ctx, data, path):
    pos, n = 0, len(data)
    while pos + 16 <= n:
        if ctx.image_left:                                   # continuation of IMAGE data
            take = min(ctx.image_left, (n - pos) // 16)
            if ctx.cur_up is not None: ctx.cur_up[11] += take
            ctx.image_left -= take
            pos += take * 16
            continue
        lo, hi = struct.unpack_from('<QQ', data, pos)
        pos += 16
        nloop = lo & 0x7FFF
        flg = (lo >> 58) & 3
        nreg = (lo >> 60) & 0xF or 16
        if nloop == 0:
            continue
        if flg == 0:                                         # PACKED
            for _ in range(nloop):
                for r in range(nreg):
                    if pos + 16 > n: return
                    desc = (hi >> (4 * r)) & 0xF
                    if desc == 0x0E:                         # A+D
                        val = struct.unpack_from('<Q', data, pos)[0]
                        addr = data[pos + 8]
                        ctx.ad_write(addr, val, path)
                    elif desc in (0x6, 0x7):                 # TEX0_1/TEX0_2 as a direct packed reg (VU1 path)
                        val = struct.unpack_from('<Q', data, pos)[0]
                        ctx.ad_write(desc, val, path)
                    pos += 16
        elif flg == 1:                                       # REGLIST: 2 regs per qword
            total = nloop * nreg
            for k in range(total):
                if pos + 8 > n: return
                desc = (hi >> (4 * (k % nreg))) & 0xF
                if desc in (0x6, 0x7):
                    ctx.ad_write(desc, struct.unpack_from('<Q', data, pos)[0], path)
                pos += 8
            if total & 1: pos += 8                            # qword padding
        else:                                                # IMAGE
            ctx.image_left = nloop
    return

def parse(path, window=(0x1900, 0x2400), show_all=False):
    b = load(path)
    pos = parse_header(b)
    ctx = Ctx()
    n = len(b)
    while pos < n:
        pid = b[pos]; pos += 1
        if pid == 0:                                         # transfer
            tpath = b[pos]; pos += 1
            size = struct.unpack_from('<I', b, pos)[0]; pos += 4
            parse_gif(ctx, b[pos:pos + size], tpath)
            pos += size
        elif pid == 1:                                       # vsync
            pos += 1
            ctx.events.append(('vsync', ctx.frame))
            ctx.frame += 1
            ctx.image_left = 0
        elif pid == 2:                                       # readfifo
            pos += 4
        elif pid == 3:                                       # registers
            pos += 8192
        else:
            print(f'!! unknown packet id {pid} @ {pos - 1:#x} — stopping'); break

    lo, hi = window
    def up_span(e):
        _, _, _, _, dbp, dpsm, _, _, dsay, rrw, rrh, _ = e
        bpp = PSM_BPP.get(dpsm, 32)
        blocks = max(1, (rrw * rrh * bpp + 2047) // 2048)
        # dsay offsets rows into the buffer: add its block contribution (approx, full-width rows)
        row_blocks = (dsay * rrw * bpp + 2047) // 2048
        return dbp + row_blocks, dbp + row_blocks + blocks

    print(f'== {os.path.basename(path)}: {ctx.frame + 1} frame(s), {len(ctx.events)} events')
    upl = [e for e in ctx.events if e[0] == 'up']
    tex = [e for e in ctx.events if e[0] == 'tex']
    print(f'   uploads: {len(upl)}   TEX0 binds: {len(tex)}   distinct bind TBPs: {len(set(t[5] for t in tex))}')
    print(f'   --- chronological events touching block window [{lo:#x},{hi:#x}) (seq order) ---')
    for e in ctx.events:
        if e[0] == 'vsync':
            print(f'   ---- vsync (end frame {e[1]}) ----')
        elif e[0] == 'up':
            s, t = up_span(e)
            if show_all or (t > lo and s < hi):
                _, fr, seq, pth, dbp, dpsm, dbw, dsax, dsay, rrw, rrh, qwc = e
                print(f'   [{seq:4d}] UPLOAD  dbp={dbp:#06x}..{t:#06x} psm={PSM_NAME.get(dpsm, hex(dpsm))}'
                      f' {rrw}x{rrh}@({dsax},{dsay}) bw={dbw} qwc={qwc} path{pth}')
        else:
            _, fr, seq, pth, cid, tbp, tbw, psm, w, h, cbp = e
            if show_all or lo <= tbp < hi or lo <= cbp < hi:
                print(f'   [{seq:4d}] TEX0.{cid + 1}  tbp={tbp:#06x} psm={PSM_NAME.get(psm, hex(psm))}'
                      f' {w}x{h} bw={tbw} cbp={cbp:#06x} path{pth}')
    # summary: all upload dest ranges + top bind TBPs
    print('   --- all upload destinations (merged) ---')
    spans = sorted(up_span(e) + (e[2],) for e in upl)
    for s, t, seq in spans[:40]:
        print(f'     {s:#06x}..{t:#06x}  (first seq {seq})')
    if len(spans) > 40: print(f'     ... {len(spans) - 40} more')
    from collections import Counter
    cnt = Counter(t[5] for t in tex)
    print('   --- most-bound TEX0 TBPs ---')
    for tbp, c in cnt.most_common(25):
        print(f'     tbp={tbp:#06x} x{c}')

if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    show_all = '--all' in sys.argv
    win = (0x1900, 0x2400)
    if '--window' in sys.argv:
        i = sys.argv.index('--window')
        win = (int(sys.argv[i + 1], 0), int(sys.argv[i + 2], 0))
    for p in args:
        parse(p, win, show_all)
        print()
