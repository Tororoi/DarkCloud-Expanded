#!/usr/bin/env python3
"""Export rebuilt Queens part subfiles to the bin IsoPatcher.ApplyQueensPartSwaps consumes.
Currently: e03h06 (doubled `_c` hull height + full-visual `_a` split into nodes — tools/queens_snake_statue_collision.py).

Format: u32 count; per part: name[8] + u32 origSubSize (guard) + u32 newSubSize + bytes (16-aligned).
Run: python3 tools/export_queens_parts.py -> game_data/queens/queens_parts.bin
"""
import os, struct, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from extract_scene_mesh import load_scene
import scene_placed
from queens_snake_statue_collision import rebuild_h06

scn = load_scene('gedit/e03/scene.scn')
DIR = scene_placed.scn_directory_map(scn)
OUT = os.path.normpath(os.path.join(HERE, '..', 'game_data', 'queens', 'queens_parts.bin'))
blob = bytearray()
entries = [('e03h06',) + rebuild_h06(scn, DIR)[:2]]
blob += struct.pack('<I', len(entries))
for name, new, orig in entries:
    blob += name.encode('latin1').ljust(8, b'\x00')
    blob += struct.pack('<II', orig, len(new))
    blob += new
    blob += b'\x00' * ((-len(new)) % 16)
    print(f'{name}: sub {orig} -> {len(new)}')
open(OUT, 'wb').write(blob)
print(f'wrote {len(blob)} bytes -> {OUT}')
