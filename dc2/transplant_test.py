#!/usr/bin/env python3
"""Build Priscleen by transplanting its mesh + texture onto a PROVEN DC1 fish rig (skeleton / motion /
weights / bbp / 7 motions), then re-weighting Priscleen's vertices to that skeleton by spine position.

This works because the crash lived in Priscleen's own rig, not its mesh — a DC1 rig drives Priscleen's mesh
without crashing (verified). We keep the donor's entire rig and only swap the mesh block + regenerate the
weights.

DONOR must be a single-morphable-part fish whose mesh MDT is the LAST block in its mds (so we can transplant
with no offset shifting) and have <= Priscleen's 224 verts is NOT required (we regenerate weights). f07a is
a single-part, 14-spine-bone fish — a close body match to Priscleen.

Output is written as f19a.chr so the runtime injector (PriscleenFish.cs, species 8) picks it up.
"""
import os, sys, struct, re
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools"))
from extract_scene_mesh import load_scene, parse_mds

DONOR = "f12a"     # the DC1 fish whose rig we borrow (single-part, 273v, 20 spine bones — finest skeleton)
PRIS  = "/Users/thomascantwell/ROMs/dc2_extracted/priscleen/f19a.chr"   # Priscleen mesh source (has f19a.mds)
OUT   = os.path.normpath(os.path.join(os.path.dirname(__file__), "..",
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
        h = bytearray(0x50); h[0:len(name)] = name; pad = (len(data)+15)//16*16
        struct.pack_into("<III", h, 0x40, 0x50, len(data), 0x50 + pad); out += h + data + b"\0"*(pad-len(data))
    return bytes(out) + bytes(0x50)

def mdt_of(mds, want):
    for name, mo, mat in parse_mds(mds, 0):
        if name == want and mds[mo:mo+3] == b"MDT":
            return mo, struct.unpack_from("<I", mds, mo+8)[0]
    return None, None

def mdt_verts(mds, off):
    vc = struct.unpack_from("<I", mds, off+0xc)[0]; vo = off + struct.unpack_from("<I", mds, off+0x10)[0]
    return [struct.unpack_from("<3f", mds, vo+i*0x10) for i in range(vc)]

def reweight(donor_wgt, donor_verts, pris_verts):
    """Rebuild the donor's .wgt for Priscleen's vertices: keep the donor's exact node/bone structure (so the
    parent-setup + hierarchy order MotionProc2 needs are preserved), but reassign weights by SPINE (Z)
    position. DC1 skinning is SMOOTH (a vert may weight to 2-3 bones summing to 100), so we BLEND each
    Priscleen vertex between its two adjacent spine bones by Z — no rigid snapping, no tearing at bone
    boundaries."""
    nodes = []; o = 0
    while o + 0x20 <= len(donor_wgt):
        h = list(struct.unpack_from("<8I", donor_wgt, o)); cnt = h[4]
        vs = [struct.unpack_from("<I", donor_wgt, o+0x20+k*0x20)[0] for k in range(cnt)]
        nodes.append([h, vs])
        if h[5] == 0: break
        o += h[5]
    bone_z = {h[1]: sum(donor_verts[i][2] for i in vs)/len(vs) for h, vs in nodes if vs}
    skin = sorted(bone_z, key=lambda b: bone_z[b])           # spine bones ordered tail->head by Z
    fzs = [donor_verts[i][2] for h, vs in nodes for i in vs]; fzmin, fzmax = min(fzs), max(fzs)
    pzs = [p[2] for p in pris_verts]; pzmin, pzmax = min(pzs), max(pzs)
    def mapz(z): return (z-pzmin)/(pzmax-pzmin+1e-9)*(fzmax-fzmin)+fzmin
    wmap = {b: [] for b in skin}                             # bone -> [(vertIndex, weight)]
    for vi, p in enumerate(pris_verts):
        z = mapz(p[2])
        if z <= bone_z[skin[0]]:
            wmap[skin[0]].append((vi, 100.0))
        elif z >= bone_z[skin[-1]]:
            wmap[skin[-1]].append((vi, 100.0))
        else:
            for i in range(len(skin)-1):
                z0, z1 = bone_z[skin[i]], bone_z[skin[i+1]]
                if z0 <= z < z1:
                    t = (z-z0)/(z1-z0)
                    if t < 0.999: wmap[skin[i]].append((vi, round((1-t)*100, 1)))
                    if t > 0.001: wmap[skin[i+1]].append((vi, round(t*100, 1)))
                    break
    out = bytearray()
    for idx, (h, vs) in enumerate(nodes):
        vw = wmap.get(h[1], []) if vs else []
        h[4] = len(vw); h[5] = 0 if idx == len(nodes)-1 else 0x20 + len(vw)*0x20
        out += struct.pack("<8I", *h)
        for vi, wt in vw:
            out += struct.pack("<I", vi) + b"\0"*12 + struct.pack("<f", wt) + b"\0"*12
    tot = sum(len(v) for v in wmap.values())
    print(f"  reweighted (smooth) 224 verts -> {tot} bone-weights across {len(skin)} bones")
    return bytes(out)

def main():
    donor = dict((n.decode("latin1"), d) for n, d in parse_chr(load_scene(f"chara/{DONOR}.chr")))
    cfg = donor["info.cfg"].decode("latin1")
    mesh_frame = re.search(r'ALLOC_DBUFF\s+"([^"]+)"', cfg).group(1)
    pris_mds = dict((n.decode("latin1"), d) for n, d in parse_chr(open(PRIS, "rb").read()))["f19a.mds"]
    ship = dict((n.decode("latin1"), d) for n, d in parse_chr(open(OUT, "rb").read()))
    img_name = next(n for n in ship if n.endswith(".img")); pris_img = ship[img_name]

    mds = bytearray(donor[f"{DONOR}.mds"])
    o_off, o_sz = mdt_of(mds, mesh_frame)
    s_off, s_sz = mdt_of(pris_mds, "skin1")
    print(f"{DONOR} '{mesh_frame}' MDT @0x{o_off:x} sz 0x{o_sz:x} (ends 0x{o_off+o_sz:x}, mds 0x{len(mds):x}); "
          f"priscleen skin1 MDT sz 0x{s_sz:x}")
    assert o_off + o_sz == len(mds), f"{DONOR} '{mesh_frame}' MDT is not the last block — needs shifting"
    new_mds = bytes(mds[:o_off]) + pris_mds[s_off:s_off+s_sz]
    new_wgt = reweight(donor[f"{DONOR}.wgt"], mdt_verts(donor[f"{DONOR}.mds"], o_off),
                       mdt_verts(pris_mds, s_off))

    cfg = re.sub(r'IMG 0,"[^"]+"', f'IMG 0,"{img_name}"', cfg)
    subs = [
        (b"info.cfg", cfg.encode("latin1")),
        (f"{DONOR}.mds".encode("latin1"), new_mds),
        (f"{DONOR}.mot".encode("latin1"), donor[f"{DONOR}.mot"]),
        (f"{DONOR}.bbp".encode("latin1"), donor[f"{DONOR}.bbp"]),
        (f"{DONOR}.wgt".encode("latin1"), new_wgt),
        (img_name.encode("latin1"), pris_img),
    ]
    open(OUT, "wb").write(write_chr(subs))
    print(f"wrote {OUT} ({os.path.getsize(OUT)}B): {DONOR} rig + Priscleen mesh + texture")

if __name__ == "__main__":
    main()
