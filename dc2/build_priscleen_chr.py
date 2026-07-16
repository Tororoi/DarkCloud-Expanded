#!/usr/bin/env python3
r"""Convert DC2's Priscleen fish model (sg/fish/f19a.chr) into a DC1-loadable .chr.

DC1 and DC2 use the IDENTICAL .chr pack container (see dc2/README.md): a chain of sub-file entries
{name @+0, u32 dataOff@+0x40 (=0x50), u32 size@+0x44, u32 nextStride@+0x48 (=0x50+align16(size)), data
@+0x50}, ending in a 0x50 zero terminator; lookup is by name so order is irrelevant. The mesh/motion/
weights/texture binaries are byte-identical formats. The ONLY incompatibility is the info.cfg *text*
grammar. So this converter copies every DC2 sub-file verbatim and only regenerates info.cfg in DC1's
grammar (mimicking chara/f01a.chr), keeping DC2's own values and the f19a naming throughout.

Priscleen becomes DC1 species id 8, but the model keeps the f19a name (f09a is reserved for a future
added fish, not conflated with this). Output: ROMs/dc2_extracted/priscleen/f19a.chr.
"""
import sys, os, re, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dc2_archive import read_file

OUT = "/Users/thomascantwell/ROMs/dc2_extracted/priscleen/f19a.chr"
ALIGN = 0x10
def align(n, a=ALIGN): return (n + a - 1) & ~(a - 1)

def parse_pack(d):
    """Return [(name, header_bytes(0x50), data_bytes)] in file order, using the DC1/DC2 pack format."""
    out = []; p = 0
    while p < len(d) and d[p] != 0:
        name = d[p:p+0x40].split(b'\x00')[0].decode('latin1')
        dataOff, size, stride = struct.unpack_from("<III", d, p+0x40)
        out.append((name, d[p:p+0x50], d[p+dataOff:p+dataOff+size]))
        p += stride
    return out

def dc1_info_cfg(dc2_cfg):
    """Rewrite a DC2 info.cfg (bytes) into DC1 grammar (bytes), preserving f19a names + values."""
    t = dc2_cfg.decode('latin1')
    img   = re.search(r'IMG\s+0,\s*"([^"]+)"', t).group(1)
    model = re.search(r'MODEL\s+"([^"]+)"', t).group(1)
    body  = re.search(r'BODY_SIZE\s+([\d.]+),\s*([\d.]+),\s*([\d.]+)', t)
    bx, by, bz = (str(int(float(g))) for g in body.groups())
    mot   = re.search(r'MOTION\s+0,\s*"([^"]+)",\s*"([^"]+)",\s*"([^"]+)"', t)
    m_mot, m_bbp, m_wgt = mot.groups()
    keys  = re.findall(r'KEY\s+"[^"]*",\s*(\d+),\s*(\d+),\s*([\d.]+)', t)  # (start,end,weight)
    lines = [
        f'IMG 0,"{img}"', 'IMG_END', 'MATERIAL_ANIME 0', 'VERTEX_ANIME 1', '',
        'ALLOC_DBUFF "skin"', f'MODEL "{model}"', f'BODY_SIZE {bx},{by},{bz}', '',
        f'MOTION 0, "{m_mot}", "{m_bbp}", "{m_wgt}"', 'SHADOW_MOTION "", "", ""', 'KEY_START 0',
    ]
    for i, (s, e, w) in enumerate(keys):
        lines.append(f'KEY\t{s},\t{e},\t{w},\t//{i}')
    lines += ['MOTION_END', '']
    return ('\r\n'.join(lines)).encode('latin1')

def entry(name, data):
    """Build a 0x50-header pack entry + aligned data."""
    hdr = bytearray(0x50)
    nb = name.encode('latin1')
    hdr[0:len(nb)] = nb                                   # name (rest zero; GetPackFile ignores 0x08..0x3F)
    struct.pack_into("<III", hdr, 0x40, 0x50, len(data), 0x50 + align(len(data)))
    return bytes(hdr) + data + b'\x00' * (align(len(data)) - len(data))

def main():
    src = read_file("sg/fish/f19a.chr")
    subs = parse_pack(src)
    print("DC2 sub-files:", [(n, len(data)) for n, _, data in subs])
    out = bytearray()
    for name, hdr, data in subs:
        if name.lower() == "info.cfg":
            data = dc1_info_cfg(data)
            print("\n=== regenerated DC1 info.cfg ===\n" + data.decode('latin1'))
        out += entry(name, data)
    out += b'\x00' * 0x50                                 # terminator
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    open(OUT, "wb").write(out)
    print(f"\nwrote {len(out)} bytes -> {OUT}")

if __name__ == "__main__":
    main()
