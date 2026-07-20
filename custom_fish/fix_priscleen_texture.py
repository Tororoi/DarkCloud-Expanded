#!/usr/bin/env python3
"""Fix the Priscleen garbled texture: DC1 textures are IM2, DC2's are IM3 — a different OUTER container, but
BOTH wrap a standard TIM2 texture inside. DC1's CTextureManager::EnterIMGFile parses IM2 and reads every
pixel/CLUT/dimension field from the embedded TIM2; it only reads the per-texture entry's NAME and its +0x20
(the TIM2 offset). So converting IM3 -> IM2 is a header swap: keep the DC2 TIM2 verbatim, wrap it in an IM2
header + one 0x30-byte entry (name = the mesh's texture name, TIM2 at 0x40).

Rebuilds Resources/Fish/f19a.chr with the .img converted. Run: python3 custom_fish/fix_priscleen_texture.py
"""
import os, sys, struct
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools"))
from extract_scene_mesh import load_scene

SRC = "/Users/thomascantwell/ROMs/dc2_extracted/priscleen/f19a.chr"
OUT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..",
                       "Dark Cloud Improved Version", "Resources", "Fish", "f19a.chr"))

def parse_chr(data):
    subs = []; off = 0
    while off + 0x50 <= len(data):
        name = data[off:off+0x40].split(b"\0")[0]
        do = int.from_bytes(data[off+0x40:off+0x44], "little")
        sz = int.from_bytes(data[off+0x44:off+0x48], "little")
        st = int.from_bytes(data[off+0x48:off+0x4c], "little")
        if not name and sz == 0: break
        subs.append([name, data[off+do:off+do+sz]])
        if st == 0: break
        off += st
    return subs

def write_chr(subs):
    out = bytearray()
    for name, data in subs:
        hdr = bytearray(0x50)
        hdr[0:len(name)] = name
        pad = (len(data) + 15) // 16 * 16
        struct.pack_into("<III", hdr, 0x40, 0x50, len(data), 0x50 + pad)
        out += hdr + data + b"\0" * (pad - len(data))
    out += bytearray(0x50)                                   # zero terminator
    return bytes(out)

def im3_to_im2(img, tex_name):
    """Strip the IM3 wrapper, keep the TIM2, re-wrap as IM2 (header 0x10 + entry 0x30 + TIM2 @0x40).
    Entry layout is copied from a real DC1 .img (f01a) so any incidental fields are valid; only the name
    and the TIM2 offset (already 0x40) matter to DC1's parser."""
    t = img.find(b"TIM2")
    if t < 0: raise SystemExit("no TIM2 found in DC2 .img")
    tim2 = img[t:]
    f01_img = next(d for n, d in parse_chr(load_scene("chara/f01a.chr")) if n.endswith(b".img"))
    head = bytearray(f01_img[:0x40])                         # IM2 header (0x10) + entry (0x30), TIM2 offset already 0x40
    nb = tex_name.encode()
    head[0x10:0x18] = (nb + b"\0" * 8)[:8]                   # overwrite the entry name (8 bytes)
    return bytes(head) + tim2

def main():
    subs = parse_chr(open(SRC, "rb").read())
    for e in subs:
        name = e[0].decode("latin1")
        if name.endswith(".img"):
            tex = name[:-4]                                  # "f19a01.img" -> "f19a01" (matches the mds token)
            old = len(e[1])
            e[1] = im3_to_im2(e[1], tex)
            print(f"converted {name}: IM3 {old}B -> IM2 {len(e[1])}B (texture '{tex}')")
    open(OUT, "wb").write(write_chr(subs))
    print(f"wrote {OUT} ({os.path.getsize(OUT)}B)")

if __name__ == "__main__":
    main()
