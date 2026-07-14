#!/usr/bin/env python3
r"""Dark Cloud .img texture-bank surgery — list, extract and build IM2 banks.

Motivation: the fishing sign carved out by tools/mds_surgery.py samples the texture `e01b24`, which
lives inside the bank `e04b01.img` in Muska Lacka's img.pak. `e01b24` is present in the img.pak of
e01/e02/e04/s09 — exactly the four fishing towns — and absent from e03/e05/s04, which is both a nice
confirmation that it IS the fishing-sign texture and the reason the harvested sign renders untextured
in the target towns. To fix that we need to move one texture entry into another town's bank.

FORMAT (IM2 — a flat archive of TIM2 images, addressed by NAME):

  header (0x10):
    +0x00  char[4]  "IM2\0"
    +0x04  u32      entryCount
    +0x08  u32      0
    +0x0C  u32      0

  entry (0x30 each, table starts at 0x10):
    +0x00  char[32] name          <- NUL-terminated. The PADDING IS UNINITIALIZED GARBAGE
                                     (stale heap from the packing tool — stray "Y:\Project\..."
                                     paths and 00..FF runs). Read to the first NUL and ignore
                                     the rest; do NOT assume zero padding.
    +0x20  u32      offset        <- ABSOLUTE file offset of this entry's TIM2 block
    +0x24  ...                    <- more garbage

  then the TIM2 blocks, in table order.

The name in the table is what a model's `HB<name>` reference resolves against — NOT the .img
filename. So a bank can be called anything; only its entry names matter to the meshes.

TIM2 blocks are self-describing (totalSize in the picture header), so a bank is trivially
re-composable: copy blocks, rewrite offsets.
"""
import struct, sys, argparse

HDR = 0x10
ENT = 0x30


def tim2_info(data, off):
    """Decode the TIM2 picture header. Layout is the stock Sony TIM2:
       0x10-byte file header, then a picture header at +0x10."""
    if data[off:off + 4] != b'TIM2':
        raise ValueError(f'0x{off:X} is not a TIM2 block')
    pic = off + 0x10
    total, clut_sz, img_sz = struct.unpack_from('<3I', data, pic)
    hdr_sz, clut_colors = struct.unpack_from('<2H', data, pic + 0x0C)
    pfmt, mips, ctype, itype = struct.unpack_from('<4B', data, pic + 0x10)
    w, h = struct.unpack_from('<2H', data, pic + 0x14)
    return dict(total=total, clut=clut_sz, image=img_sz, hdr=hdr_sz,
                colors=clut_colors, bpp=itype, w=w, h=h, mips=mips)


class Img:
    def __init__(self, data):
        if data[:4] != b'IM2\x00':
            raise ValueError('not an IM2 bank')
        self.data = data
        self.count = struct.unpack_from('<I', data, 4)[0]
        self.entries = []
        for i in range(self.count):
            e = HDR + i * ENT
            name = data[e:e + 0x20].split(b'\x00')[0].decode('latin1', 'replace')
            off = struct.unpack_from('<I', data, e + 0x20)[0]
            self.entries.append((name, off))

    def block(self, name):
        """The TIM2 bytes for `name`. Size comes from the NEXT entry's offset (or EOF) — the
        entries are laid out in table order, which is more robust than trusting the TIM2 header."""
        offs = sorted(o for _, o in self.entries)
        for n, o in self.entries:
            if n != name:
                continue
            nxt = next((x for x in offs if x > o), len(self.data))
            return self.data[o:nxt]
        raise KeyError(name)


def build(items):
    """items = [(name, tim2_bytes)] -> a fresh IM2 bank."""
    out = bytearray(struct.pack('<4sIII', b'IM2\x00', len(items), 0, 0))
    out += bytes(len(items) * ENT)                       # table, filled in below
    for i, (name, blob) in enumerate(items):
        e = HDR + i * ENT
        nb = name.encode('latin1')[:0x1F]
        out[e:e + len(nb)] = nb                          # rest already zero (cleaner than vanilla)
        struct.pack_into('<I', out, e + 0x20, len(out))
        out += blob
    return bytes(out)


def cmd_list(img, args):
    print(f"IM2 bank, {img.count} entries")
    print(f"{'#':>3}  {'name':<12} {'offset':>10} {'size':>9}   image")
    for i, (n, o) in enumerate(img.entries):
        blob = img.block(n)
        try:
            info = tim2_info(img.data, o)
            desc = f"{info['w']}x{info['h']}  {info['bpp']}bpp  clut={info['colors']}"
        except Exception as ex:
            desc = f"(unreadable: {ex})"
        print(f"{i:>3}  {n:<12} 0x{o:08X} {len(blob):>9}   {desc}")


def cmd_extract(img, args):
    blob = img.block(args.name)
    open(args.out, 'wb').write(blob)
    info = tim2_info(img.data, dict(img.entries)[args.name])
    print(f"{args.name}: {len(blob)} B, {info['w']}x{info['h']}, {info['bpp']}bpp, "
          f"clut={info['colors']} -> {args.out}")


def cmd_make(img, args):
    """Build a minimal bank containing only the named entries."""
    items = [(n, img.block(n)) for n in args.names]
    out = build(items)
    open(args.out, 'wb').write(out)
    print(f"built {args.out}: {len(out)} B, {len(items)} entr{'y' if len(items)==1 else 'ies'}")
    for n, b in items:
        print(f"   {n:<12} {len(b):>9} B")
    chk = Img(open(args.out, 'rb').read())
    assert [n for n, _ in chk.entries] == args.names
    for n in args.names:
        assert chk.block(n) == dict(items)[n], f'{n} round-trip mismatch'
    print("verified: re-parses, entry names and TIM2 payloads round-trip byte-identical")


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('bank', help='an .img (IM2) file')
    sub = ap.add_subparsers(dest='cmd', required=True)
    p = sub.add_parser('list'); p.set_defaults(fn=cmd_list)
    p = sub.add_parser('extract'); p.add_argument('name'); p.add_argument('-o', '--out', required=True)
    p.set_defaults(fn=cmd_extract)
    p = sub.add_parser('make', help='build a minimal bank holding only these entries')
    p.add_argument('names', nargs='+'); p.add_argument('-o', '--out', required=True)
    p.set_defaults(fn=cmd_make)
    args = ap.parse_args()
    args.fn(Img(open(args.bank, 'rb').read()), args)


if __name__ == '__main__':
    main()
