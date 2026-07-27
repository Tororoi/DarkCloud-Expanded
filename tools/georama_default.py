#!/usr/bin/env python3
"""Decode a town's DEFAULT georama layout from gedit/<code>/gdata0.edt.

The file is a list of 16-byte records (x,y,z float, code int32), terminated by code==-1. code low-16 =
part index (0..11 = BLD_PARTS buildings e03h01..e03h12, 12 = GRD_PARTS tree, 13 = ROAD), high-16 (signed)
= rotation (mod 4). y is the region height (0/70/170). Positions are authoritative world coords.

default_layout(scene_code) -> {'buildings':[{part,name,x,y,z,rot}], 'trees':[{x,y,z,rot}], 'roads':[{x,y,z,rot}]}
"""
import struct
from extract_scene_mesh import load_scene


def default_layout(gdata_rel):
    edt = load_scene(gdata_rel)
    out = {'buildings': [], 'trees': [], 'roads': []}
    o = 0xc
    while o + 16 <= len(edt):
        x, y, z = struct.unpack_from('<3f', edt, o)
        code = struct.unpack_from('<i', edt, o + 12)[0]
        o += 16
        if code == -1:
            break
        part = code & 0xffff
        rot = (code >> 16) & 0xffff
        if rot >= 0x8000:
            rot -= 0x10000
        rot &= 3
        x, y, z = round(x), round(y), round(z)          # snap off the 0.1 bias
        if part <= 11:
            out['buildings'].append({'part': part, 'name': 'e03h%02d' % (part + 1),
                                     'x': x, 'y': y, 'z': z, 'rot': rot})
        elif part == 12:
            out['trees'].append({'x': x, 'y': y, 'z': z, 'rot': rot})
        elif part == 13:
            out['roads'].append({'x': x, 'y': y, 'z': z, 'rot': rot})
    return out


if __name__ == '__main__':
    import sys
    d = default_layout(sys.argv[1] if len(sys.argv) > 1 else 'gedit/e03/gdata0.edt')
    print("buildings:", len(d['buildings']), "trees:", len(d['trees']), "roads:", len(d['roads']))
    for b in d['buildings']:
        print("  %-8s (%5d,%3d,%5d) rot=%d" % (b['name'], b['x'], b['y'], b['z'], b['rot']))
