#!/usr/bin/env python3
r"""Repack Priscleen (f19a.chr) with the 7 authored DC1-style motions — the final export step.

Input: dc2/blender/priscleen_bake.json — written by running dc2/blender/priscleen_dc1motions.py in
Blender (WYSIWYG: the exact game-space per-bone local quat+trans the preview keyframed, so the .mot
is guaranteed to match what was approved on screen). Re-run the Blender script after any tuning
change to refresh the bake, then re-run this.

What it builds, starting from the DC2 archive's sg/fish/f19a.chr:
  - f19a.mot  — REPLACED: one rot (and, where the pose moves the bone, trans) track per animated
    bone, keyframed every frame of every motion on f00s's exact global timeline (the 7 KEY ranges
    below). Quats conjugated back to DC convention ((w,x,y,z) -> (w,-x,-y,-z), the inverse of the
    viewer's fix). Untouched bones get no track (loader keeps the .mds rest local).
  - f19a.wgt  — PATCHED: the 12 UPPER_LIP skin1 verts move from their original bones (head, 6) to
    a new (skin1, bone 26) block at weight 100 — the hinge-rotated upper lip needs the nose bone
    to own the whole lip rigidly.
  - info.cfg  — DC1 grammar (same skeleton as build_priscleen_chr.py's) with the 7 REAL KEY lines
    replacing the old 3-motion slot mapping. Frame counts were authored to f00s's, so its ranges +
    weights transfer verbatim.
Everything else (mds/bbp/img) copies through untouched. Output goes to both the scratch dir and
the mod's Resources/Fish/f19a.chr (what PriscleenFish.cs injects at runtime).
"""
import json, math, os, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools"))
from dc2_archive import read_file
from build_blender_rig import skeleton
from build_priscleen_chr import parse_pack, entry as pack_entry, align
from build_priscleen_dc1motions import UPPER_LIP
from fix_priscleen_texture import im3_to_im2

BAKE = os.path.join(os.path.dirname(__file__), "blender", "priscleen_bake.json")
OUTS = ["/Users/thomascantwell/ROMs/dc2_extracted/priscleen/f19a.chr",
        "/Users/thomascantwell/DarkCloud-Enhanced/Dark Cloud Improved Version/Resources/Fish/f19a.chr"]

# f00s's exact 7-motion timeline (start, end, weight, comment). Bake motion i maps to KEYS[i];
# each range length == the authored len (asserted below), so frames land 1:1.
KEYS = [(10, 24, 0.25, "normal"), (30, 44, 0.50, "battle-genki"), (53, 73, 0.45, "battle-yowaki"),
        (83, 103, 0.35, "leap"), (113, 133, 0.50, "reeled-in"), (143, 163, 0.45, "caught"),
        (173, 187, 0.20, "idle")]

# .mot block header mirror values (copied from f00s.mot — runtime scratch fields, ignored on load)
MOT_H6, MOT_H7 = 2012564890, 20317968
SKIN1_FRAME, LIP_BONE = 49, 26
STANDIN_BUFFER = 168656              # f01a.chr size — the BG buffer PriscleenFish overwrites; hard cap
ROT_TOL_DEG = 0.35                   # keyframe-reduction tolerances: max slerp error vs the full bake
TRANS_TOL = 0.03                     # (game units) — f00s itself ships sparse tracks, the loader interpolates


def slerp(a, b, f):
    d = sum(x * y for x, y in zip(a, b))
    if d < 0: b = tuple(-v for v in b); d = -d
    if d > 0.9995:
        v = tuple(x + f * (y - x) for x, y in zip(a, b))
    else:
        th = math.acos(min(1.0, d)); s = math.sin(th)
        wa, wb = math.sin((1 - f) * th) / s, math.sin(f * th) / s
        v = tuple(wa * x + wb * y for x, y in zip(a, b))
    n = sum(x * x for x in v) ** 0.5
    return tuple(x / n for x in v)


def reduce_track(kfs, quat):
    """Drop keyframes the loader's interpolation reproduces within tolerance (RDP on the curve).
    Endpoints always kept, so motion-range boundaries and loop seams stay exact."""
    keep = {0, len(kfs) - 1}

    def err(a, b, k):
        f = (kfs[k][0] - kfs[a][0]) / (kfs[b][0] - kfs[a][0])
        if quat:
            iv = slerp(kfs[a][1], kfs[b][1], f)
            d = abs(sum(x * y for x, y in zip(iv, kfs[k][1])))
            return math.degrees(2 * math.acos(min(1.0, d)))
        iv = tuple(x + f * (y - x) for x, y in zip(kfs[a][1], kfs[b][1]))
        return max(abs(x - y) for x, y in zip(iv, kfs[k][1]))

    def rec(a, b):
        if b - a < 2: return
        wi, worst = None, ROT_TOL_DEG if quat else TRANS_TOL
        for k in range(a + 1, b):
            e = err(a, b, k)
            if e > worst: worst, wi = e, k
        if wi is not None:
            keep.add(wi); rec(a, wi); rec(wi, b)

    rec(0, len(kfs) - 1)
    return [kfs[i] for i in sorted(keep)]


def qmul(a, b):
    aw, ax, ay, az = a; bw, bx, by, bz = b
    return (aw*bw - ax*bx - ay*by - az*bz, aw*bx + ax*bw + ay*bz - az*by,
            aw*by - ax*bz + ay*bw + az*bx, aw*bz + ax*by - ay*bx + az*bw)


def qrot(q, v):
    w, x, y, z = q
    u = (x, y, z)
    uv = (u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0])
    uuv = (u[1]*uv[2]-u[2]*uv[1], u[2]*uv[0]-u[0]*uv[2], u[0]*uv[1]-u[1]*uv[0])
    return tuple(v[i] + 2*(w*uv[i] + uuv[i]) for i in range(3))


def fold_root(bake):
    """Fold any bone-0 (root) animation into bone 1 so frame 0 stays at rest. No shipped DC1/DC2
    .mot animates frame 0 (DC2 f19a starts at bone 1, f00s at 3, the working transplant at 2) —
    the nose-pivot's rigid transform landed on the root, so recompose W1 = L0@L1 onto bone 1.
    Bone 1 parents the whole visible skeleton; the skin/hari frames under the root stay put."""
    for mo in bake["motions"]:
        for row in mo["frames"]:
            q0, t0 = tuple(row[0][:4]), tuple(row[0][4:])
            q1, t1 = tuple(row[1][:4]), tuple(row[1][4:])
            q, t = qmul(q0, q1), tuple(a + b for a, b in zip(t0, qrot(q0, t1)))
            row[1] = list(q) + list(t)
            row[0] = [1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]   # bone 0 rest local == identity


def build_mot(bake, rest_local):
    """New f19a.mot: per-bone keyframe tracks over the global KEYS timeline."""
    fold_root(bake)
    nb = len(bake["bones"])
    rot = {i: [] for i in range(nb)}     # boneIdx -> [[(frame, (w,x,y,z))] per motion]  DC convention
    trans = {i: [] for i in range(nb)}
    rot_used, trans_used = set(), set()
    for mi, mo in enumerate(bake["motions"]):
        s, e, _, _ = KEYS[mi]
        assert e - s == mo["len"], f"motion {mi} len {mo['len']} != KEY range {s}-{e}"
        seg_r = {i: [] for i in range(nb)}
        seg_t = {i: [] for i in range(nb)}
        for t, row in enumerate(mo["frames"]):
            for i, (qw, qx, qy, qz, tx, ty, tz) in enumerate(row):
                seg_r[i].append((s + t, (qw, -qx, -qy, -qz)))        # conjugate back to DC handedness
                seg_t[i].append((s + t, (tx, ty, tz)))
        for i in range(nb):
            rot[i].append(seg_r[i]); trans[i].append(seg_t[i])
    # a track is real only if it ever deviates from the .mds rest local
    for i in range(nb):
        L = rest_local[i]
        rx, ry, rz_, rw = quat_of(L)                                  # rest quat (mathutils order w,x,y,z)
        rq = (rw, -rx, -ry, -rz_)
        rt = (L[12], L[13], L[14])
        if any(qdiff(q, rq) > 1e-4 for seg in rot[i] for _, q in seg): rot_used.add(i)
        if any(max(abs(a - b) for a, b in zip(v, rt)) > 1e-3 for seg in trans[i] for _, v in seg):
            trans_used.add(i)
    # keyframe-reduce each motion segment independently, then concatenate into the bone's track.
    # Every shipped track (f00s, f12a transplant) covers frame 0 onward — prepend a frame-0 key
    # (duplicate of the first) so the engine never samples below a track's first keyframe.
    def track(segs, quat):
        kfs = [k for seg in segs for k in reduce_track(seg, quat)]
        return [(0, kfs[0][1])] + kfs
    full = sum(len(s) for s in rot[1])
    blocks = []
    for i in sorted(rot_used):
        blocks.append((i, 0, track(rot[i], True)))
    for i in sorted(trans_used):
        blocks.append((i, 2, track(trans[i], False)))
    kept = sum(len(k) for _, _, k in blocks)
    print(f"keyframe reduction: {kept} keys kept of {full * len(blocks)} "
          f"({kept * 100 // max(1, full * len(blocks))}%), tol rot {ROT_TOL_DEG}deg / trans {TRANS_TOL}")
    out = bytearray()
    for bi, (bone, typ, kfs) in enumerate(blocks):
        stride = 0 if bi == len(blocks) - 1 else 0x20 + len(kfs) * 0x20
        out += struct.pack("<8I", bone, 0, typ, 32, len(kfs), stride, MOT_H6, MOT_H7)
        for f, v in kfs:
            e = bytearray(0x20)
            struct.pack_into("<i", e, 0, f)
            struct.pack_into("<%df" % len(v), e, 0x10, *v)
            out += e
    return bytes(out), sorted(rot_used), sorted(trans_used)


def quat_of(L):
    """Quaternion (x,y,z,w) of a column-major-flat 4x4's rotation (assumed orthonormal)."""
    m00, m01, m02 = L[0], L[4], L[8]
    m10, m11, m12 = L[1], L[5], L[9]
    m20, m21, m22 = L[2], L[6], L[10]
    tr = m00 + m11 + m22
    if tr > 0:
        s = (tr + 1.0) ** 0.5 * 2
        return ((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25 * s)
    if m00 > m11 and m00 > m22:
        s = (1.0 + m00 - m11 - m22) ** 0.5 * 2
        return (0.25 * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s)
    if m11 > m22:
        s = (1.0 + m11 - m00 - m22) ** 0.5 * 2
        return ((m01 + m10) / s, 0.25 * s, (m12 + m21) / s, (m02 - m20) / s)
    s = (1.0 + m22 - m00 - m11) ** 0.5 * 2
    return ((m02 + m20) / s, (m12 + m21) / s, 0.25 * s, (m10 - m01) / s)


def qdiff(a, b):
    """Rotation difference proxy: 1 - |dot| (0 = same rotation, sign-insensitive)."""
    return 1.0 - abs(sum(x * y for x, y in zip(a, b)))


def patch_wgt(wgt):
    """Move the UPPER_LIP skin1 verts to a new (skin1, bone 26) block at weight 100, and DROP the
    skin2 blocks entirely: skin2 is not ALLOC_DBUFF'd (static), and no working config ever bound
    weights to a non-morphable frame — the transplant's .wgt referenced only its dbuff'd skin."""
    lip = set(UPPER_LIP)
    blocks, o, removed = [], 0, 0
    while o + 0x20 <= len(wgt):
        h = struct.unpack_from("<8I", wgt, o)
        skinf, bone, cnt, stride = h[0], h[1], h[4], h[5]
        entries = [bytes(wgt[o + 0x20 + k * 0x20: o + 0x40 + k * 0x20]) for k in range(cnt)]
        if skinf == SKIN1_FRAME:
            kept = [e for e in entries if struct.unpack_from("<I", e)[0] not in lip]
            removed += len(entries) - len(kept)
            blocks.append([skinf, bone, h[2], h[3], kept, h[6], h[7]])
        if stride == 0: break
        o += stride
    # insert the lip block in ascending bone order within the skin1 run (after bone 25's block)
    ins = next(i for i, b in enumerate(blocks) if b[0] == SKIN1_FRAME and b[1] > LIP_BONE)
    tmpl = blocks[0]
    lip_entries = []
    for vi in sorted(lip):
        e = bytearray(0x20)
        struct.pack_into("<I", e, 0, vi)
        struct.pack_into("<f", e, 0x10, 100.0)
        lip_entries.append(bytes(e))
    blocks.insert(ins, [SKIN1_FRAME, LIP_BONE, tmpl[2], tmpl[3], lip_entries, tmpl[5], tmpl[6]])
    out = bytearray()
    for bi, (skinf, bone, h2, h3, entries, h6, h7) in enumerate(blocks):
        stride = 0 if bi == len(blocks) - 1 else 0x20 + len(entries) * 0x20
        out += struct.pack("<8I", skinf, bone, h2, h3, len(entries), stride, h6, h7)
        out += b"".join(entries)
    assert removed == len(lip), f"only found {removed}/{len(lip)} lip verts in skin1 blocks"
    return bytes(out)


def dc1_info_cfg():
    """DC1-grammar info.cfg with the real 7 KEY ranges (values otherwise as build_priscleen_chr.py).
    STATIC=1 env: ALLOC_DBUFF matches no frame -> mesh stays rigid (bones still animate internally) —
    the crash-bisect switch separating bad motion DATA from a bad skin1 MORPH path."""
    dbuff = "skin" if os.environ.get("STATIC") else "skin1"
    lines = [
        'IMG 0,"f19a01.img"', 'IMG_END', 'MATERIAL_ANIME 0', 'VERTEX_ANIME 1', '',
        f'ALLOC_DBUFF "{dbuff}"', 'MODEL "f19a.mds"', 'BODY_SIZE 18,7,60', '',
        'MOTION 0, "f19a.mot", "f19a.bbp", "f19a.wgt"', 'SHADOW_MOTION "", "", ""', 'KEY_START 0',
    ]
    for s, e, w, name in KEYS:
        lines.append(f'KEY\t{s},\t{e},\t{w:.2f},\t//{name}')
    lines += ['MOTION_END', '']
    return ('\r\n'.join(lines)).encode('latin1')


def main():
    if not os.path.exists(BAKE):
        sys.exit(f"missing {BAKE} — run dc2/blender/priscleen_dc1motions.py in Blender first "
                 f"(it writes the bake automatically)")
    bake = json.load(open(BAKE))
    assert len(bake["motions"]) == len(KEYS), f"bake has {len(bake['motions'])} motions, need {len(KEYS)}"

    src = read_file("sg/fish/f19a.chr")
    subs = {n: d for n, _, d in parse_pack(src)}
    frames, _ = skeleton(subs["f19a.mds"])       # rest locals for the track-needed diff
    rest_local = [loc for (_, _, loc) in frames]
    assert len(rest_local) == len(bake["bones"]), "bone count mismatch between bake and .mds"

    mot, rot_used, trans_used = build_mot(bake, rest_local)
    print(f"f19a.mot: {len(mot)} bytes, {len(rot_used)} rot tracks {rot_used},")
    print(f"          {len(trans_used)} trans tracks {trans_used}")

    out = bytearray()
    for name, hdr, data in parse_pack(src):
        low = name.lower()
        if low == "f19a.mot":
            data = mot
        elif low == "f19a.wgt":
            data = patch_wgt(data)
            print(f"f19a.wgt: patched — {len(UPPER_LIP)} lip verts -> bone {LIP_BONE}, {len(data)} bytes")
        elif low == "info.cfg":
            data = dc1_info_cfg()
            print("=== info.cfg ===\n" + data.decode('latin1'))
        elif low.endswith(".img"):
            old = len(data)                            # DC2 ships IM3; DC1 only parses IM2 (else GARBLED
            data = im3_to_im2(data, name[:-4])         # texture + EnterIMGFile walks bogus offsets)
            print(f"{name}: IM3 {old}B -> IM2 {len(data)}B")
        out += pack_entry(name, data)
    out += b'\x00' * 0x50
    if len(out) > STANDIN_BUFFER:
        sys.exit(f"chr is {len(out)} bytes > stand-in buffer {STANDIN_BUFFER} (f01a.chr) — tighten "
                 f"ROT_TOL_DEG/TRANS_TOL or point PriscleenFish at a larger stand-in")
    print(f"chr size {len(out)} / {STANDIN_BUFFER} stand-in buffer ({len(out) * 100 // STANDIN_BUFFER}%)")
    for path in OUTS:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        open(path, "wb").write(bytes(out))
        print(f"wrote {len(out)} bytes -> {path}")


if __name__ == "__main__":
    main()
