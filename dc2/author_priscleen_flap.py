#!/usr/bin/env python3
"""Author a CLEAN minimal skinning rig for Priscleen to test whether DC1 crashes on the DC2 .mot/.wgt DATA
or on something structural. Keeps Priscleen's mesh (mds), IM2 texture and bbp; REPLACES the .mot/.wgt with
hand-authored ones: all of skin1's 224 verts bound rigidly (weight 100) to ONE bone, and a .mot that
oscillates that bone (a quaternion swing). info.cfg gets ALLOC_DBUFF skin1 + 7 identical KEYs so any motion
index the fish requests (SetMotion 1..6) plays the same safe flap range — no out-of-range KEY reads.

Formats (RE'd): a .mot/.wgt is a linked node list. node = 0x20 header {[0]=frame/bone idx, [2]=type(20 for
wgt), [3]=0x20, [4]=count, [5]=byte-stride-to-next (0 = last node)} + count*0x20 entries. .wgt entry =
{[0]=vertexIndex, [4]=weight(float 100.0)}. .mot keyframe = {[0]=frameNum, [4]=quat.w, [5]=quat.x,
[6]=quat.y, [7]=quat.z}. CreateAnimeDataEX 0x149090 walks the list; AnimeDataInit 0x1493a0 binds by index.

Run: python3 dc2/author_priscleen_flap.py   ->  rewrites Resources/Fish/f19a.chr
"""
import os, struct, math

CHR = os.path.normpath(os.path.join(os.path.dirname(__file__), "..",
                       "Dark Cloud Improved Version", "Resources", "Fish", "f19a.chr"))
SKIN1_FRAME = 49      # skin1 mesh frame index (from the wgt)
BONE        = 6       # skin1's main body bone (most-weighted in Priscleen's own wgt)
NVERTS      = 224     # skin1 vertex count
NKF         = 30      # keyframes (one flap cycle)
AMP         = math.radians(25.0)

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
        hdr = bytearray(0x50); hdr[0:len(name)] = name
        pad = (len(data) + 15)//16*16
        struct.pack_into("<III", hdr, 0x40, 0x50, len(data), 0x50 + pad)
        out += hdr + data + b"\0"*(pad - len(data))
    return bytes(out) + bytes(0x50)

def author_mot():
    node = bytearray(struct.pack("<8I", BONE, 0, 0, 0x20, NKF, 0, 0, 0))   # single last node
    for f in range(NKF):
        th = AMP * math.sin(2*math.pi*f/NKF)
        w, y = math.cos(th/2), math.sin(th/2)
        node += struct.pack("<I", f+1) + b"\0"*12 + struct.pack("<f", w) + struct.pack("<f", 0.0) \
                + struct.pack("<f", y) + struct.pack("<f", 0.0)
    return bytes(node)

def author_wgt():
    node = bytearray(struct.pack("<8I", SKIN1_FRAME, BONE, 20, 0x20, NVERTS, 0, 0, 0))  # single last node
    for v in range(NVERTS):
        node += struct.pack("<I", v) + b"\0"*12 + struct.pack("<f", 100.0) + b"\0"*12
    return bytes(node)

def author_cfg():
    lines = ['IMG 0,"f19a01.img"', 'IMG_END', 'MATERIAL_ANIME 0', 'VERTEX_ANIME 1', '',
             'ALLOC_DBUFF "skin1"', 'MODEL "f19a.mds"', 'BODY_SIZE 18,7,60', '',
             'MOTION 0, "f19a.mot", "f19a.bbp", "f19a.wgt"', 'SHADOW_MOTION "", "", ""', 'KEY_START 0']
    for i in range(7):                                    # motions 0..6 all = the same safe flap range
        lines.append(f'KEY\t1,\t{NKF},\t0.50,\t//{i}')
    lines += ['MOTION_END', '']
    return ('\r\n'.join(lines)).encode('latin1')

def main():
    subs = parse_chr(open(CHR, "rb").read())
    for e in subs:
        n = e[0].decode("latin1")
        if n.endswith(".mot"): e[1] = author_mot(); print(f"authored {n}: {len(e[1])}B")
        elif n.endswith(".wgt"): e[1] = author_wgt(); print(f"authored {n}: {len(e[1])}B")
        elif n.endswith("info.cfg"): e[1] = author_cfg(); print(f"authored {n}: {len(e[1])}B")
    open(CHR, "wb").write(write_chr(subs))
    print(f"wrote {CHR} ({os.path.getsize(CHR)}B) — clean 1-bone flap on skin1")

if __name__ == "__main__":
    main()
