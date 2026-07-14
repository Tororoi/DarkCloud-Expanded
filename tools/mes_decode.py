#!/usr/bin/env python3
r"""Decode/encode Dark Cloud meswin 16-bit glyph text; dump allmenu.mes entries.

Usage:  python3 tools/mes_decode.py <allmenu.mes file> [filter-substring]

Text encoding (16-bit LE codes, high byte = plane):
  0xFDgg glyphs: 0x01-0x3A = '!'-'Z' (ascii-0x20); 0x3B-0x54 = 'a'-'z' (ascii-0x26);
                 0x55="'" 0x57='"' 0x5B='&' 0x5D='-' 0x5F='/' 0x61='(' 0x62=')'
                 0x6C=',' 0x6D='.' 0x6F-0x78 = '0'-'9'
  0xFF00 newline, 0xFF01 end-of-message, 0xFF02 space
  0xFBxx icon/placeholder (0xFBFF right after a weapon name = its level/icon slot)
  0xFCxx color (02/03 set, 00 reset), 0xF8xx button glyphs
Weapon description entry: "Name[FBFF]" \n line1 \n line2 [\n line3] [FF01]

Where the text lives on disc: data.dat -> every commenu\a_eng\*.pak menu pak embeds an
identical `allmenu.mes` sub-file (e.g. emenu.pak @+0x7B020). Pak sub-file header = 0x50
bytes: name@+0, u32 dataSize@+0x44, u32 nextSubfileOffset@+0x48 (from subfile start).
.mes layout: u16 count, u16 ?, count*(u16 id, u16 off); in practice entries are best
enumerated by scanning 0xFF01 terminators (the entry# below). Toan swords: Dagger[broken]
= entry#204 .. AtlamilliaSword = #223 (== itemId-53 for 257..276); 2nd-half weapons at
entry#453..#487. In-place replacement only: new text must fit the original byte span
(pad with 0xFF02 spaces or terminate early with 0xFF01) — offsets elsewhere must not move.
"""
import struct, sys

GL = {0x55: "'", 0x57: '"', 0x5B: '&', 0x5D: '-', 0x5F: '/',
      0x61: '(', 0x62: ')', 0x6C: ',', 0x6D: '.'}

def dec_code(c):
    g = c & 0xFF; hi = c >> 8
    if hi == 0xFF: return {0: '\n', 1: '[END]', 2: ' '}.get(g, '[ff%02x]' % g)
    if hi == 0xFB: return '[fb%02x]' % g
    if hi == 0xFD:
        if 0x01 <= g <= 0x3A: return chr(g + 0x20)
        if 0x3B <= g <= 0x54: return chr(g + 0x26)
        if 0x6F <= g <= 0x78: return chr(g - 0x6F + 0x30)
        return GL.get(g, '{%02x}' % g)
    return '<%04x>' % c

def enc_char(ch):
    o = ord(ch)
    if ch == '\n': return b'\x00\xff'
    if ch == ' ':  return b'\x02\xff'
    for k, v in GL.items():
        if v == ch: return bytes([k, 0xFD])
    if 0x30 <= o <= 0x39: return bytes([o - 0x30 + 0x6F, 0xFD])
    if 0x21 <= o <= 0x5A: return bytes([o - 0x20, 0xFD])
    if 0x61 <= o <= 0x7A: return bytes([o - 0x26, 0xFD])
    raise ValueError('unencodable: %r' % ch)

def encode(s):
    """ASCII string -> meswin glyph bytes (no terminator)."""
    return b''.join(enc_char(c) for c in s)

def dump_entries(mes):
    """Yield (entry#, byteoff, bytelen, text) by 0xFF01-terminator scan."""
    cnt, = struct.unpack_from('<H', mes, 0)
    base = 4 + cnt * 4
    starts = [base]; q = base
    while q + 1 < len(mes):
        c = struct.unpack_from('<H', mes, q)[0]; q += 2
        if c == 0xFF01: starts.append(q)
    for i in range(len(starts) - 1):
        s, e = starts[i], starts[i + 1]
        t = ''.join(dec_code(struct.unpack_from('<H', mes, q)[0]) for q in range(s, e - 1, 2))
        yield i, s, e - s, t.replace('[END]', '').strip()

if __name__ == '__main__':
    mes = open(sys.argv[1], 'rb').read()
    filt = sys.argv[2] if len(sys.argv) > 2 else None
    for i, off, ln, t in dump_entries(mes):
        if filt and filt not in t: continue
        print('entry#%-3d @0x%04X len=%-3d %s' % (i, off, ln, t.replace('\n', ' | ')))
