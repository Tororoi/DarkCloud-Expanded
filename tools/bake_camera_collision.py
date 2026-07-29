#!/usr/bin/env python3
"""Bake per-building CAMERA collision into a town scene.scn: replace each building sub-file's `_a` root-node
collision MDT with the building's FULL visible geometry (unsimplified), so the town camera (which reads the
building's `_a` via the +0xdc=+0xd0 alias) collides with the true building silhouette instead of the
ground-height footprint. Per-building = each e03h* sub-file, so it moves with the georama placement.

The tris come from the user's OWN ISO (part_models -> visible mesh) and are baked into the user's OWN ISO
copy at patch time; nothing game-derived is committed. This is a TEST bake (full geometry) to measure the
camera-gather cost; if it's too heavy we simplify per building later.

Pipeline: part_models (visible tris, sub-file local) -> build_coll_mdt -> variant-aware splice into the `_a`
root node (identity transform, verified) -> new scene.scn bytes.

  bake_scene(scene_rel, name_re='e03h\\d\\d$') -> (new_scn_bytes, stats)
"""
import os, sys, struct, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract_scene_mesh import load_scene
import scene_placed, scene_splice as ss, georama_collision as gc
from georama_parts import part_models
from build_coll_mdt import build_coll_mdt


def _dir(scn):
    """[(name, off, size, entry_file_offset)] — directory is a fixed 0x10, 0x30-byte entries (word@4 is the
    sub-file COUNT, not the dir offset; ss._dir mis-reads this for these scenes)."""
    out, o = [], 0x10
    while o + 0x30 <= len(scn):
        nm = scn[o:o + 16].split(b'\x00')[0].decode('latin1', 'replace')
        if not nm or not nm[0].isalnum():
            break
        off, size = struct.unpack_from('<II', scn, o + 0x10)
        out.append((nm, off, size, o))
        o += 0x30
    return out


def _splice_a_root(scn, sub_name, new_mdt):
    """Replace the `_a` block's root-node MDT in `sub_name` with new_mdt; fix sibling meshOffs, the SCN
    directory, and every trailing variant-entry offset (_s/_k/_c/_w) that shifts. Returns (new_scn, delta)."""
    scn = bytearray(scn)
    entry = next((e for e in _dir(scn) if e[0] == sub_name), None)
    if entry is None:
        raise KeyError(sub_name)
    _, sub_off, sub_size, _ = entry
    sub = bytes(scn[sub_off:sub_off + sub_size])
    vo = gc._variant_a(sub, sub_name)
    if vo is None:
        raise KeyError(f"{sub_name}: no _a variant")

    mds_abs = sub_off + vo
    cnt, tbl = struct.unpack_from('<II', scn, mds_abs + 8)
    table = mds_abs + tbl
    mo = struct.unpack_from('<i', scn, table + 0x28)[0]          # root node (idx 0) meshOff
    mdt_abs = mds_abs + mo
    assert scn[mdt_abs:mdt_abs + 3] == b'MDT', "root node has no MDT"
    old_size = struct.unpack_from('<I', scn, mdt_abs + 8)[0]

    new_mdt = bytearray(new_mdt)
    while len(new_mdt) % 0x10:
        new_mdt.append(0)
    delta = len(new_mdt) - old_size
    growth_subrel = mdt_abs - sub_off                            # sub-rel byte where the grow happens

    # identify trailing variant-entry offsets to bump (real entries: valid offset -> 'MDS', past the grow)
    bumps = []   # (abs_field_pos, new_value)
    for m in re.finditer(rb'[\w]+\.mds\x00', sub):
        fpos = m.end() + 3
        if fpos + 4 > len(sub):
            continue
        toff = struct.unpack_from('<I', sub, fpos)[0]
        if 0 < toff < sub_size and sub[toff:toff + 3] == b'MDS' and toff > growth_subrel:
            bumps.append((sub_off + fpos, toff + delta))

    out = bytearray(scn[:mdt_abs]) + new_mdt + bytearray(scn[mdt_abs + old_size:])

    # sibling meshOffs in the _a MDS laid out after the root MDT
    for i in range(cnt):
        b = table + i * 0x70
        smo = struct.unpack_from('<i', out, b + 0x28)[0]
        if smo != 0 and smo > mo:
            struct.pack_into('<i', out, b + 0x28, smo + delta)

    # trailing variant offsets (positions are before the grow, so unmoved in `out`)
    for pos, val in bumps:
        struct.pack_into('<I', out, pos, val)

    # SCN directory: this sub-file's size += delta; later sub-files' offsets += delta
    for name, off, size, eoff in _dir(scn):
        if off == sub_off:
            struct.pack_into('<I', out, eoff + 0x14, size + delta)
        elif off > sub_off:
            struct.pack_into('<I', out, eoff + 0x10, off + delta)
    return bytes(out), delta


def bake_scene(scene_rel, name_re=r'e03h\d\d$'):
    scn = load_scene(scene_rel)
    pm = part_models(scene_rel, name_re)          # {name: {'tris':[...local...]}}
    stats = []
    for name in sorted(pm):
        tris = pm[name]['tris']
        if not tris:
            continue
        mdt = build_coll_mdt(tris)
        scn, delta = _splice_a_root(scn, name, mdt)
        stats.append((name, len(tris), len(mdt), delta))
    return scn, stats


def bake_scene_from_bytes(scene_rel, scene_bytes, name_re=r'e03h\d\d$'):
    """Same as bake_scene but the scene comes from bytes (e.g. read straight out of the ISO), not disk.
    Returns (new_scn_bytes, stats)."""
    import extract_scene_mesh as esm
    import georama_parts as gp
    patched = (lambda rel, _o=esm.load_scene, _b=scene_bytes, _r=scene_rel: _b if rel == _r else _o(rel))
    saved = (esm.load_scene, scene_placed.load_scene, gp.load_scene, gc.load_scene)
    esm.load_scene = scene_placed.load_scene = gp.load_scene = gc.load_scene = patched
    try:
        return bake_scene(scene_rel, name_re)
    finally:
        esm.load_scene, scene_placed.load_scene, gp.load_scene, gc.load_scene = saved


if __name__ == '__main__':
    scene = sys.argv[1] if len(sys.argv) > 1 else 'gedit/e03/scene.scn'
    pat = sys.argv[2] if len(sys.argv) > 2 else r'e03h\d\d$'
    new_scn, stats = bake_scene(scene, pat)
    grew = sum(d for *_, d in stats)
    print(f"baked {len(stats)} buildings, scene grew {grew} bytes ({grew/1024:.0f} KB)")
    for n, tc, ms, d in stats:
        print(f"  {n}: {tc:5} tris -> {ms:6} B MDT (Δ{d:+})")

    # ---- offline validation: re-parse the spliced scene and confirm the full geometry reads back ----
    out = "/tmp/scene_baked.scn"
    open(out, "wb").write(new_scn)
    cl = gc.collision_local  # collision_local loads from disk; monkey-parse the bytes instead
    # re-run parse on the in-memory bytes via a temp override of load_scene
    import extract_scene_mesh as esm
    orig = esm.load_scene
    esm.load_scene = lambda rel: new_scn if rel == scene else orig(rel)
    scene_placed.load_scene = esm.load_scene
    gc.load_scene = esm.load_scene
    got = gc.collision_local(scene, pat)
    esm.load_scene = orig
    print("re-parsed spliced scene — per-building collision tris now:")
    tot = 0
    for n in sorted(got):
        tot += len(got[n]); print(f"  {n}_a: {len(got[n])} tris")
    print(f"TOTAL collision tris after bake: {tot}")
