#!/usr/bin/env python3
"""Emit a Blender script showing Priscleen's OWN DC2 rig with APPROXIMATE DC1-style motions.

build_priscleen_native.py shows the 3 real DC2 motions (which look fine alone but don't match DC1's
animation style). This script instead uses Priscleen's native skeleton + native weights + both body
meshes (skin1 = body, skin2 = fin membranes), and PROCEDURALLY authors the 7 DC1 fish motions on
those bones:

    0 通常 normal swim   1 バトル元気 battle-energetic   2 バトル弱気 battle-timid
    3 飛びはね leap       4 釣り上げられ中 being-reeled    5 釣り上げられた caught/flop
    6 アイドリング idle

The motions are built from anatomy, not retargeted from a donor skeleton (that path stretched the
mesh). Each is a set of world-axis rotations — a traveling yaw wave down the spine (swim), up/down fin
flaps, a jaw gasp, and a body pitch for the hanging poses — converted into each bone's local frame and
baked through the SAME matrix pipeline the native script uses, so the mesh sits correctly on the rig.

Everything is tunable: the MP (motion-params) table and the tuning constants live at the top of the
generated Blender file — tweak amplitudes/frequencies there and re-run in Blender, no regen needed.

Source: DC2 sg/fish/f19a.chr. Output: dc2/blender/priscleen_dc1motions.py
"""
import os, sys, struct, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools"))
from dc2_archive import read_file
from extract_scene_mesh import load_scene
from build_blender_rig import parse_chr, skeleton, motions, matmul
from build_priscleen_native import mesh_of, parse_wgt

OUT = os.path.join(os.path.dirname(__file__), "blender", "priscleen_dc1motions.py")

def subtree(parent, root):
    """All bone indices in the subtree rooted at `root` (inclusive)."""
    out, stack = [], [root]
    while stack:
        i = stack.pop(); out.append(i)
        stack += [j for j, p in enumerate(parent) if p == i]
    return sorted(out)

def _stations(idxs, z_of):
    """Group a spine chain into coincident-Z stations (front->tip), like the Blender-side SP_MULT."""
    st, cur, cz = [], [], None
    for i in idxs:
        z = round(z_of(i), 1)
        if cz is None or z == cz:
            cur.append(i)
        else:
            st.append(cur); cur = [i]
        cz = z
    st.append(cur)
    return st

def retarget_f00s_m5(pris_frames, pris_world, pris_tail):
    """Extract f00s motion 5's spine EXACTLY: per station (coincident jnt/eff group), the incremental
    world-frame rotation added at that station each frame. Mapped onto Priscleen's spine stations
    TIP-ALIGNED (the user compares bones counted from the tail tip), so every Priscleen spine bone
    twists in the same direction/amount as its f00s counterpart — only the segment lengths differ."""
    fchr = dict(parse_chr(load_scene("chara/f00s.chr")))
    ffr, fw = skeleton(fchr["f00s.mds"])
    _, frot, ftrans = motions(fchr["f00s.mot"], fchr["info.cfg"].decode("shift_jis", "replace"))

    def qm(q):                                     # conjugated DC quat -> col-major matrix (viewer-proven)
        w, x, y, z = q[0], -q[1], -q[2], -q[3]
        n = w * w + x * x + y * y + z * z or 1.0
        s = 2.0 / n
        return [1 - s * (y * y + z * z), s * (x * y + w * z), s * (x * z - w * y), 0,
                s * (x * y - w * z), 1 - s * (x * x + z * z), s * (y * z + w * x), 0,
                s * (x * z + w * y), s * (y * z - w * x), 1 - s * (x * x + y * y), 0, 0, 0, 0, 1]

    def samp(kfs, t, n):
        if t <= kfs[0][0]: return list(kfs[0][1])
        if t >= kfs[-1][0]: return list(kfs[-1][1])
        for lo, hi in zip(kfs, kfs[1:]):
            if lo[0] <= t <= hi[0]:
                f = (t - lo[0]) / (hi[0] - lo[0]) if hi[0] != lo[0] else 0.0
                return [lo[1][j] + (hi[1][j] - lo[1][j]) * f for j in range(n)]

    def fworld(t):
        W = [None] * len(ffr)
        for i, (nm, pa, loc) in enumerate(ffr):
            L = list(loc)
            if t is not None and i in frot:
                m = qm(samp(frot[i], t, 4)); m[12], m[13], m[14] = L[12], L[13], L[14]; L = m
            if t is not None and i in ftrans:
                tt = samp(ftrans[i], t, 3); L[12], L[13], L[14] = tt
            W[i] = L if pa < 0 else matmul(W[pa], L)
        return W

    def r3(m):   return [[m[0], m[4], m[8]], [m[1], m[5], m[9]], [m[2], m[6], m[10]]]
    def mm3(a, b): return [[sum(a[r][k] * b[k][c] for k in range(3)) for c in range(3)] for r in range(3)]
    def t3(m):   return [[m[c][r] for c in range(3)] for r in range(3)]
    def axang(R):
        tr = R[0][0] + R[1][1] + R[2][2]
        ang = math.acos(max(-1.0, min(1.0, (tr - 1.0) / 2.0)))
        if ang < 1e-6: return (0.0, 1.0, 0.0, 0.0)
        s = 2.0 * math.sin(ang)
        return ((R[2][1] - R[1][2]) / s, (R[0][2] - R[2][0]) / s, (R[1][0] - R[0][1]) / s, math.degrees(ang))

    def rz(deg):
        a = math.radians(deg); c, s = math.cos(a), math.sin(a)
        return [[c, -s, 0.0], [s, c, 0.0], [0.0, 0.0, 1.0]]

    F_SPINE = list(range(16, 34))                  # chn2..eff7 (f00s spine chain)
    ROOT = 4                                       # eff1 — reference just ahead of the spine
    fst = _stations(F_SPINE, lambda i: fw[i][14])
    fr_w = fworld(None)
    pst = _stations(pris_tail, lambda i: pris_world[i][14])
    n = min(len(fst), len(pst))
    drop = len(fst) - n                            # extra f00s FRONT stations: FOLDED into the first mapped
                                                   # station (compose, don't drop — f00s's front rotations
                                                   # partially cancel; dropping half bent pris's mid wrongly)
    # tapered TWIST redistribution: f00s applies its roll as one jump at a single station; on Priscleen
    # ramp it instead — zero at the mid-body station, growing to the full f00s roll just before the tail.
    CUM = [0.0, 0.2, 0.5, 0.8, 1.0, 1.0][:n]
    rows = []
    for t in range(143, 164):                      # f00s KEY slot 5 (釣れた), 21 frames
        aw = fworld(t)
        Ms = [mm3(r3(aw[st[-1]]), t3(r3(fr_w[st[-1]]))) for st in fst]   # world delta up to each station
        root = mm3(r3(aw[ROOT]), t3(r3(fr_w[ROOT])))
        th = [math.degrees(math.asin(max(-1.0, min(1.0, -Ms[k][0][1])))) for k in range(len(fst))]
        row, prev, prev_th, j = [], root, 0.0, 0
        for k in range(drop, len(fst)):
            D = mm3(t3(prev), Ms[k])               # increment (first one spans the folded front stations)
            droll = th[k] - prev_th                # f00s's roll increment at this station...
            want = (CUM[j] - (CUM[j - 1] if j else 0.0)) * th[len(fst) - 1]   # ...retargeted to the taper
            row.append(axang(mm3(rz(want - droll), D)))
            prev, prev_th, j = Ms[k], th[k], j + 1
        rows.append(row)

    m5st = pst[-n:]
    m5spine = [[tuple(round(v, 6) for v in r) for r in row] for row in rows]
    return m5st, m5spine

# Priscleen ships its upper-lip verts weighted to the head bone (6) with the nose bone (26) unused.
# Re-home the whole upper lip to bone 26 as one rigid piece. The lip then opens the SAME way the lower
# jaw does — bone 26 is ROTATED about a hinge at the back of the mouth (see LIP_HINGE / jaw code), not
# translated. A rigid translation sheared the cluster against the static head; a hinge rotation pins the
# back (junction with the head, motion ~0) and swings the front up, like a real mouth. Full weights, so
# the lip stays coherent (no mid-lip crease). Applied to f19a.wgt by repack_priscleen_dc1motions.py
# (which imports this list). skin1 only.
UPPER_LIP = [16, 117, 63, 104, 44, 91,   # lower rim (mouth edge)
             115, 30, 84, 24, 28, 22]    # upper edge / front peak of the lip

def main():
    chr = dict(parse_chr(read_file("sg/fish/f19a.chr")))
    mds = chr["f19a.mds"]
    frames, world = skeleton(mds)
    parent = [pa for (_, pa, _) in frames]
    bones = [{"name": f"{i}_{nm}", "parent": pa, "local": loc} for i, (nm, pa, loc) in enumerate(frames)]
    wmap = parse_wgt(chr["f19a.wgt"], frames)

    meshes = []
    for mname in ("skin1", "skin2"):
        verts, tris = mesh_of(mds, mname)
        w = wmap.get(mname, {})
        meshes.append({"name": mname, "verts": verts, "tris": tris,
                       "weights": [w.get(vi, []) for vi in range(len(verts))]})

    for vi in UPPER_LIP:
        meshes[0]["weights"][vi] = [[26, 1.0]]

    # Anatomy groups by subtree (see docstring / bone map). Tail excludes the pelvic-fin subtrees.
    tail = [i for i in subtree(parent, 27) if i not in subtree(parent, 42) + subtree(parent, 45)]
    groups = {
        "SPINE":   tail,                       # 27..41  traveling yaw wave (swim)
        "NECK":    [5, 6],                      # neck joints — swing the nose for the front of the S-curve
        "DORSAL":  subtree(parent, 23),        # 23..25  upper fin (rudder sway)
        "PECT_R":  subtree(parent, 17),        # 17..22  +X pectoral
        "PECT_L":  subtree(parent, 11),        # 11..16  -X pectoral
        "PELV_R":  subtree(parent, 45),        # 45..47  +X pelvic
        "PELV_L":  subtree(parent, 42),        # 42..44  -X pelvic
        "JAW":     subtree(parent, 8),         # 8..10   lower mouth (gasp)
        "NOSE":    [26],                       # upper mouth / snout
        "BODY":    [1],                        # whole-fish pitch pivot
    }

    m5st, m5spine = retarget_f00s_m5(frames, world, tail)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w") as f:
        f.write("# AUTO-GENERATED by dc2/build_priscleen_dc1motions.py — Priscleen's native rig, DC1-style motions.\n")
        f.write("# Open in Blender's Text editor and Run. Tune the MP table + constants near the top of BODY.\n")
        f.write("import bpy, math, json, os\nfrom mathutils import Vector, Quaternion, Matrix\n\n")
        f.write("BAKE_OUT = %r\n" % os.path.join(os.path.dirname(OUT), "priscleen_bake.json"))
        f.write("BONES  = %r\n" % bones)
        f.write("MESHES = %r\n" % meshes)
        f.write("GROUPS = %r\n" % groups)
        f.write("M5ST = %r\n" % m5st)          # pris spine stations (front->tip) driven by the f00s copy
        f.write("M5SPINE = %r\n" % m5spine)    # per frame, per station: world-axis (x,y,z,deg) from f00s m5
        f.write(BODY)
    print(f"wrote {OUT}: {len(bones)} bones, {len(meshes)} meshes, "
          f"groups spine={len(tail)} pectR={len(groups['PECT_R'])} pelvR={len(groups['PELV_R'])} "
          f"dorsal={len(groups['DORSAL'])} jaw={len(groups['JAW'])}")

BODY = r'''
# ============================================================================================
# TUNING — edit freely, re-run (File > New, paste/open, Run). All amplitudes in DEGREES.
# Per-motion: len (frames), loop, body (tail-tip yaw), bfreq (body cycles), fin (flap amp),
#   ffreq (fin cycles), jaw (mouth open), pitch (static nose-up), arc (leap pitch arc), wob (erratic).
# `len` = keyframe count-1; matched to DC1 frame counts from f00s (the only 7-distinct-motion fish;
#   its named slots map 1:1 to these). Regular fish (f07a/f08a — Priscleen's topology match) reuse
#   just slots 0/1/6. Loop motions use whole-number freqs so the seam is clean.
# ============================================================================================
MP = {
    "0_normal 通常 15f":   dict(len=14, loop=True,  body=40, bfreq=1.0, fin=12, ffreq=1.0, jaw=30, jfreq=2, pect=38, prow=9,  rowf=2, palt=0, pivot=7, vtail=0, warp=0.0, warpc=0.0,  jawmin=0.0,  pitch=0, arc=0, wob=0),
    "1_battle元気 15f":    dict(len=14, loop=True,  body=40, bfreq=1.0, fin=12, ffreq=1.0, jaw=20, jfreq=1, pect=70, prow=5,  rowf=2, palt=0, pivot=7, vtail=0, warp=0.0, warpc=0.0,  jawmin=0.0,  pitch=0, arc=0, wob=0),
    "2_battle弱気 21f":    dict(len=20, loop=True,  body=14, bfreq=2.0, fin=8,  ffreq=2.0, jaw=28, jfreq=1, pect=32, prow=9,  rowf=2, palt=0, pivot=0, vtail=0, warp=0.0, warpc=0.0,  jawmin=0.0,  pitch=0, arc=0, wob=0),
    "3_leap 飛びはね 21f": dict(len=20, loop=False, body=58, bfreq=2.0, fin=30, ffreq=2.0, jaw=50, jfreq=3, pect=20, prow=28, rowf=3, palt=0, pivot=0, vtail=7, warp=0.8, warpc=0.12, jawmin=0.0,  prowv=10, pitch=0, arc=0, wob=0),
    "4_reeled 釣中 21f":   dict(len=20, loop=True,  body=32, bfreq=2.0, fin=10, ffreq=2.0, jaw=26, jfreq=1, pect=25, prow=50, rowf=3, palt=1, pivot=0, vtail=3, warp=0.0, warpc=0.0,  jawmin=0.5, rowdwell=0.7, bwarp=0.6, bwarpc=0.08, benv=0.58, pitch=0, arc=0, wob=0),
    "5_caught 釣れた 21f": dict(len=20, loop=True,  body=0,  bfreq=1.0, fin=6,  ffreq=1.0, jaw=30, jfreq=1, pect=55, prow=12, rowf=1, palt=0, pivot=0, vtail=0, warp=0.0, warpc=0.0,  jawmin=0.85, copySpine=1, rowph=-2.7, prowv=10, prowvph=-1.57, pitch=0, arc=0, wob=0),
    "6_idle アイドル 15f": dict(len=14, loop=True,  body=19, bfreq=1.0, fin=5,  ffreq=1.0, jaw=24, jfreq=1, pect=22, prow=15, rowf=1, palt=1, pivot=2, vtail=0, warp=0.0, warpc=0.0,  jawmin=0.0,  rowdwell=0.45, prowv=16, pitch=0, arc=0, wob=0),
}
# pect=pectoral sweep-back, prow=pectoral row amp, rowf=pectoral row cycles, palt=pectorals alternate L/R (1=out
# of phase, the reeled-in paddle), rowdwell=uneven stroke speed: linger at the BACK extreme, snap through the
# front (0=steady, <1), prowv=vertical rowing amp (deg; ELLIPTICAL paddle: forward low, back raised —
# true rowing, f00s idle; vertical is 90deg behind the fore/aft), rowph=row phase shift (rad; m5: fins BACK
# at the tail-muscle contraction peak,
# FORWARD as it relaxes), jaw=mouth-open, jfreq=mouth cycles, jawmin=mouth-open FLOOR (never fully closes
# — the tired gasp), pivot=nose-pivot yaw (0=head fixed), vtail=vertical tail-lift (single flick @ snap),
# warp=time-warp (0=uniform; >0 slow-fast-slow burst mid-motion), warpc=warp center shift (later snap),
# bwarp/bwarpc=TAIL-ONLY wind-up/flick warp (fins untouched), benv=tail amplitude envelope (quiet ends, full flick),
# copySpine=1: spine driven by the M5SPINE table — f00s motion 5's EXACT per-station rotations (extracted
# from its .mot, tip-aligned) instead of any procedural wave; bend/twist/twistph=procedural fallbacks for
# other motions (static side-curl / peduncle-muscle roll at the u~0.43 station / roll-swing phase align)
# --- carangiform spine wave (tuned to f00s motion 0: pivots on a fixed nose, tail whips ~19u; no tilt) ---
WAVE_K = 0.22           # phase travel down the body (rad per game-unit): drives the traveling S-node
SPINE_BASE_FRAC = 0.16  # anterior-body floor of the tail wave (the nose pivot below adds the front swing)
SPINE_POWER = 1.0       # tail-weighting exponent (higher = motion concentrated further back)
BODY_PHASE = 2.25       # rad: start-of-loop phase — aligned so Priscleen swings the SAME way as f00s
                        # (nose-pivot yaw is per-motion: MP[...]["pivot"] — f00s "pivots on its nose")
DORSAL_FOLLOW = 0.25    # dorsal fin sways this fraction of the body amp (acts as a rudder)
PECT_DROOP = 25         # pectoral fins angled downward (deg at the fin base, static rest pose)
                        # (pectoral sweep-back MP[...]["pect"] and row amplitude MP[...]["prow"] are per-motion)
JAW_SCALE = 0.65        # global gape multiplier (scales EVERY motion's mouth-open — dial the intensity here)
UPPER_JAW_FRAC = 0.4    # upper lip opens this fraction of the lower jaw's angle — Priscleen's kissy-face gape
LIP_HINGE = (0.0, 0.5, -3.0)  # world pivot the upper lip rotates about (back of the mouth, mirrors the lower
                        # jaw hinge at (0,-0.75,-3.0)) — pins the head junction, swings the lip front up

# Game-space axes (Y up, Z fore/aft with the fish facing +Z, X left/right).
UPv, RIGHT, FWD = Vector((0, 1, 0)), Vector((1, 0, 0)), Vector((0, 0, 1))
TAU = math.tau

# ============================================================================================
UP = Matrix.Rotation(math.radians(90), 4, 'X')     # game Y-up -> Blender Z-up
I4 = Matrix.Identity(4)

def to_mat(m):
    return Matrix(((m[0], m[4], m[8],  m[12]), (m[1], m[5], m[9],  m[13]),
                   (m[2], m[6], m[10], m[14]), (m[3], m[7], m[11], m[15])))

REST_L = [to_mat(b["local"]) for b in BONES]

def rest_world():
    W = [None] * len(BONES)
    for i, b in enumerate(BONES):
        p = b["parent"]
        W[i] = REST_L[i] if p < 0 else W[p] @ REST_L[i]
    return W

GR = rest_world()                                  # rest game-world per bone
GZ = [GR[i].to_translation().z for i in range(len(BONES))]   # rest fore/aft position

# spine wave geometry: front->tail axis + per-Z-station joint multiplicity. Coincident jnt/eff bones
# share a station; splitting the bend among them keeps the envelope independent of joint count.
_SP = GROUPS["SPINE"]
SP_Z0 = GZ[_SP[0]]; SP_SPAN = GZ[_SP[-1]] - GZ[_SP[0]]
SP_MULT = {i: sum(1 for j in _SP if round(GZ[j], 1) == round(GZ[i], 1)) for i in _SP}
# TWIST station: f00s m5's roll is EXACTLY 0 through the front/mid body, then jumps to full at one
# joint (u~0.43, the peduncle muscles) and stays constant to the tip. Roll applied in-chain here twists
# only the aft body; the dorsal/pelvic/pectoral attachment region ahead of it never rolls (no shear).
_twz = SP_Z0 + 0.43 * SP_SPAN
_tbest = min((GZ[i] for i in _SP), key=lambda z: abs(z - _twz))
TW_ST = [i for i in _SP if round(GZ[i], 1) == round(_tbest, 1)]
NOSE_N = GR[GROUPS["NOSE"][0]].to_translation()   # rest world position of the nose (the pivot point)

def game_world(deltas, pivot_deg=0.0, jaw_rot=0.0):
    """Rebuild game-world matrices with per-bone LOCAL rotation deltas (identity where absent).
    pivot_deg yaws the WHOLE fish about the fixed nose point (game Y-up) — f00s 'pivots on its nose'.
    jaw_rot rotates the nose bone (upper lip) UP about the back-of-mouth hinge for the kissy-face gape."""
    W = [None] * len(BONES)
    for i, b in enumerate(BONES):
        L = REST_L[i]
        d = deltas.get(i)
        if d is not None:
            L = Matrix.Translation(L.to_translation()) @ (L.to_3x3() @ d.to_matrix()).to_4x4()
        p = b["parent"]
        W[i] = L if p < 0 else W[p] @ L
    if abs(pivot_deg) > 1e-6:                        # rigid yaw about the nose: anchor there, body swings
        T = Matrix.Translation(NOSE_N) @ Matrix.Rotation(math.radians(pivot_deg), 4, 'Y') @ Matrix.Translation(-NOSE_N)
        W = [T @ w for w in W]
    if abs(jaw_rot) > 1e-6:                          # upper lip rotates UP about the hinge (like the lower
        ni = GROUPS["NOSE"][0]                        # jaw): back pinned, front swings up — no rigid shear.
        H = Vector(LIP_HINGE)                         # -jaw_rot about game-X lifts the front (fore = +Z).
        R = Matrix.Translation(H) @ Matrix.Rotation(math.radians(-jaw_rot), 4, 'X') @ Matrix.Translation(-H)
        W[ni] = R @ W[ni]
    return W

def wq(i, axis, deg):
    """Quaternion (bone-LOCAL frame) that rotates bone i about a GAME-WORLD axis by deg."""
    a = GR[i].to_3x3().inverted() @ axis
    return Quaternion(a, math.radians(deg))

def add(d, i, axis, deg):
    if abs(deg) < 1e-6: return
    q = wq(i, axis, deg)
    d[i] = (d[i] @ q) if i in d else q

def warp_prog(mp, rp, key="warp", ckey="warpc"):
    """Time-warp raw progress rp (0..1) -> slow-fast-slow when mp[key]>0, identity at 0. Keeps the
    endpoints fixed and total cycle count intact; just redistributes speed so the motion bursts.
    The center shift (ckey) moves the fast point later (peaks at center+0.5). key/ckey select the
    global warp ('warp') or the tail-only one ('bwarp' — wind-up/flick without touching the fins)."""
    w = mp.get(key, 0.0)
    if not w:
        return rp
    s = mp.get(ckey, 0.0)
    return rp - (w / TAU) * (math.sin(TAU * (rp - s)) + math.sin(TAU * s))

def snap_window(rp):
    """Narrow bump peaking at mid-motion (rp=0.5), 0 at both ends — the fast tail snap's timing."""
    return max(0.0, math.cos(math.pi * (rp - 0.5))) ** 3

def deltas(mp, t):
    """Local-frame rotation deltas for every animated bone at frame t (0..len)."""
    d = {}
    rp = t / mp["len"] if mp["len"] else 0.0        # raw progress (window timing lives in real time)
    prog = warp_prog(mp, rp)                         # warped progress drives the oscillations (burst)
    # ---- spine: carangiform traveling yaw wave — stiff front, tail whip (tuned to f00s motion 0) ----
    # bwarp/bwarpc: TAIL-ONLY wind-up/flick time-warp (fins keep their own cadence); benv: amplitude
    # envelope — quiet at the loop ends, full size at the flick (center = bwarpc+0.5).
    tprog = warp_prog(mp, rp, "bwarp", "bwarpc") if mp.get("bwarp") else prog
    benv = mp.get("benv", 0.0)
    env = 1.0 if not benv else \
        (1.0 - benv) + benv * max(0.0, math.cos(math.pi * (rp - (mp.get("bwarpc", 0.0) + 0.5)))) ** 2
    if mp.get("copySpine"):                          # spine copied STATION-FOR-STATION from f00s (m5): each
        for k, bones_k in enumerate(M5ST):           # Priscleen station gets its tip-aligned f00s station's
            ax, ay, az, ang = M5SPINE[t][k]          # exact incremental world rotation — same direction,
            if abs(ang) > 1e-4:                      # same twist, same timing; only segment lengths differ.
                for i in bones_k:
                    add(d, i, Vector((ax, ay, az)), ang / len(bones_k))
    else:
        for i in GROUPS["SPINE"]:
            u = (GZ[i] - SP_Z0) / SP_SPAN if SP_SPAN else 0.0    # 0 at front of body, 1 at tail tip
            prof = SPINE_BASE_FRAC + (1 - SPINE_BASE_FRAC) * u ** SPINE_POWER
            A = env * mp["body"] * prof
            ph = TAU * mp["bfreq"] * tprog + BODY_PHASE - WAVE_K * (SP_Z0 - GZ[i])
            ang = (A * math.sin(ph) + mp.get("bend", 0.0) * prof) / SP_MULT[i]
            if mp["wob"]:                           # deterministic pseudo-noise for the struggle
                ang += (mp["wob"] / SP_MULT[i]) * math.sin(6.3 * TAU * prog + 0.9 * i)
            add(d, i, UPv, ang)
            if mp["vtail"]:                         # vertical tail flick: ONE upward lift, windowed to the
                Av = mp["vtail"] * prof                                       # fast snap only, tail-weighted
                add(d, i, RIGHT, Av / SP_MULT[i] * snap_window(rp))
        tw = mp.get("twist", 0.0)                    # TWIST: peduncle-muscle roll — ONE in-chain roll at the
        if tw:                                       # u~0.43 station (zero ahead, constant aft), one-sided,
            s = math.sin(TAU * mp["bfreq"] * tprog + BODY_PHASE + mp.get("twistph", 0.0))
            ang = tw * 0.5 * (1.0 + s)               # peaking with the tail swing (twistph aligns)
            for i in TW_ST:
                add(d, i, FWD, ang / len(TW_ST))
    # (the anterior/nose swing is handled by the per-motion nose-pivot yaw in game_world, not here)
    # ---- dorsal fin: gentle rudder sway following the body ----------------------------------
    for i in GROUPS["DORSAL"]:
        add(d, i, UPv, DORSAL_FOLLOW * mp["body"] / max(len(GROUPS["DORSAL"]), 1)
                        * math.sin(TAU * mp["bfreq"] * prog + BODY_PHASE))
    # ---- fins: pectorals row front-to-back (sweep oscillates); pelvics flap up/down -------
    fin = math.sin(TAU * mp["ffreq"] * prog)
    theta = TAU * mp["rowf"] * prog + BODY_PHASE + mp.get("rowph", 0.0)   # pectoral row phase (rowf strokes
                                                    # per motion; rowph shifts where the stroke lands)
    rd = mp.get("rowdwell", 0.0)     # >0 = linger at the BACK extreme, snap through the front (per-stroke warp)
    row_hi = math.sin(theta + rd * math.cos(theta))            # dwells at +1 (= back for the +coeff fins)
    row_lo = math.sin(theta - rd * math.cos(theta))            # dwells at -1 (= back for the mirrored fins)
    for name, sign in (("PECT_R", +1), ("PECT_L", -1), ("PELV_R", +1), ("PELV_L", -1)):
        g = GROUPS[name]; n = max(len(g), 1)
        if name.startswith("PECT"):                    # whole-fin pose at the base: swept back + drooped
            base = g[0]
            altf = -1 if (mp["palt"] and name == "PECT_L") else 1   # palt: flip the left row -> L/R alternate
            row = row_lo if (name == "PECT_L" and mp["palt"]) else row_hi   # dwell always lands at the fin's back
            add(d, base, UPv, sign * mp["pect"])        # per-motion sweep (calmer motions less back)
            add(d, base, FWD, -sign * PECT_DROOP)       # drooped down
            add(d, base, UPv, sign * altf * mp["prow"] * row)  # ROW: fore/aft (alternating L/R when palt)
            vamp = mp.get("prowv", 0.0)                # prowv: VERTICAL rowing component — the tip traces an
            if vamp:                                   # ELLIPSE instead of a flat swing (f00s). Default phase
                lo = (name == "PECT_L" and mp["palt"])  # = 90deg behind the fore/aft (reach forward lowered,
                wp = theta - rd * math.cos(theta) if lo else theta + rd * math.cos(theta)   # sweep back
                wp += mp.get("prowvph", 0.0)           # raised); prowvph tilts the loop (m5: +pi/2 -> fin
                add(d, base, FWD, sign * vamp * (-math.cos(wp) if lo else math.cos(wp)))    # low at the back)
        else:                                          # pelvics: flap distributed along the fin
            amp = sign * mp["fin"] / n
            w = fin
            if mp["wob"]:
                w += 0.5 * math.sin(5.1 * TAU * prog + hash(name) % 7)
            for i in g:
                add(d, i, FWD, amp * w)
    # ---- jaw: lower jaw drops open here (jfreq cycles); upper lip lifts via jaw_lift in game_world ----
    openf = mp["jawmin"] + (1 - mp["jawmin"]) * (0.5 - 0.5 * math.cos(TAU * mp["jfreq"] * prog))  # jawmin=floor
    add(d, GROUPS["JAW"][0], RIGHT, mp["jaw"] * JAW_SCALE * openf)          # lower jaw drops (opens more)
    # ---- whole-body pitch: static hang (reeled/caught) + leap arc --------------------------
    pitch = mp["pitch"]
    if mp["arc"]:
        pitch += mp["arc"] * math.sin(math.pi * prog)     # rise-and-fall leap
    add(d, GROUPS["BODY"][0], RIGHT, pitch)
    return d

def build():
    arm = bpy.data.armatures.new("rig"); ao = bpy.data.objects.new("rig", arm)
    bpy.context.collection.objects.link(ao); bpy.context.view_layer.objects.active = ao
    bpy.ops.object.mode_set(mode='EDIT'); eb = {}
    for i, b in enumerate(BONES):
        bone = arm.edit_bones.new(b["name"]); eb[i] = bone
        h = (UP @ GR[i]).to_translation(); bone.head = h; bone.tail = h + Vector((0, 0, 0.5))
    for i, b in enumerate(BONES):
        if b["parent"] >= 0: eb[i].parent = eb[b["parent"]]
    for i, b in enumerate(BONES):
        kids = [eb[j] for j, bb in enumerate(BONES) if bb["parent"] == i]
        if kids and (kids[0].head - eb[i].head).length > 1e-3:   # zero-length bones get deleted by Blender
            eb[i].tail = kids[0].head
    bpy.ops.object.mode_set(mode='OBJECT')

    for M in MESHES:
        me = bpy.data.meshes.new(M["name"])
        me.from_pydata([list(UP @ Vector(v)) for v in M["verts"]], [], M["tris"]); me.update()
        mo = bpy.data.objects.new(M["name"], me); bpy.context.collection.objects.link(mo)
        md = mo.modifiers.new("arm", 'ARMATURE'); md.object = ao; mo.parent = ao
        vg = {}
        for vi, ws in enumerate(M["weights"]):
            for bi, w in ws:
                g = vg.get(bi)
                if g is None: g = mo.vertex_groups.new(name=BONES[bi]["name"]); vg[bi] = g
                g.add([vi], w, 'REPLACE')
    print("built native rig + %d meshes with authored weights" % len(MESHES))

    for pb in ao.pose.bones: pb.rotation_mode = 'QUATERNION'
    restM = [ao.data.bones[b["name"]].matrix_local.copy() for b in BONES]
    ao.animation_data_create(); acts = []; bake = []
    for name, mp in MP.items():
        act = bpy.data.actions.new(name); act.use_fake_user = True; acts.append((act, mp["len"]))
        act.use_frame_range = True; act.frame_start = 0; act.frame_end = mp["len"]   # per-motion end frame
        act.use_cyclic = mp["loop"]
        ao.animation_data.action = act
        mframes = []
        for t in range(0, mp["len"] + 1):
            rp = t / mp["len"] if mp["len"] else 0.0
            prog = warp_prog(mp, rp)                                    # match deltas' warped time
            pivot_deg = mp["pivot"] * math.sin(TAU * mp["bfreq"] * prog + BODY_PHASE)   # per-motion nose-pivot yaw
            openf = mp["jawmin"] + (1 - mp["jawmin"]) * (0.5 - 0.5 * math.cos(TAU * mp["jfreq"] * prog))
            jaw_rot = UPPER_JAW_FRAC * mp["jaw"] * JAW_SCALE * openf   # upper lip opening angle (deg); rot up @ hinge
            GW = game_world(deltas(mp, t), pivot_deg, jaw_rot)
            row = []                                     # per-bone GAME-space local (vs parent): the .mot payload
            for i, b in enumerate(BONES):
                p = b["parent"]
                L = GW[i] if p < 0 else GW[p].inverted() @ GW[i]
                q = L.to_3x3().to_quaternion(); loc = L.to_translation()
                row.append([round(v, 6) for v in (q.w, q.x, q.y, q.z, loc.x, loc.y, loc.z)])
            mframes.append(row)
            pose = [(UP @ (GW[i] @ GR[i].inverted()) @ UP.inverted()) @ restM[i] for i in range(len(BONES))]
            for i, b in enumerate(BONES):
                p = b["parent"]
                rp = restM[p] if p >= 0 else I4; pp = pose[p] if p >= 0 else I4
                basis = restM[i].inverted() @ rp @ pp.inverted() @ pose[i]
                loc, quat, _ = basis.decompose()
                pbn = ao.pose.bones[b["name"]]; pbn.location = loc; pbn.rotation_quaternion = quat
                pbn.keyframe_insert("location", frame=t); pbn.keyframe_insert("rotation_quaternion", frame=t)
        ao.animation_data.action = None
        bake.append({"name": name, "len": mp["len"], "loop": mp["loop"], "frames": mframes})
    try:                                            # WYSIWYG export: exactly what was just keyframed,
        with open(BAKE_OUT, "w") as bf:             # as game-space local quat+trans per bone per frame
            json.dump({"bones": [b["name"] for b in BONES], "motions": bake}, bf)
        print("wrote motion bake:", BAKE_OUT)
    except OSError as e:
        print("bake export skipped:", e)
    install_sync()                                  # make the timeline follow the active action's range
    act, length = acts[0]                           # show the normal swim by default
    ao.animation_data.action = act
    bpy.context.scene.frame_start = 0; bpy.context.scene.frame_end = length
    bpy.context.scene.frame_set(0)
    print(f"baked {len(acts)} DC1-style motions: " + ", ".join(a.name for a, _ in acts))
    print("Pick a motion in the Action editor / dope sheet; the timeline end follows it. Space to play.")

def _sync_action_range(scene, depsgraph=None):
    """Keep the scene play range == the active action's own frame range, so switching motions in the
    Action editor updates the timeline end automatically."""
    try:
        ao = bpy.data.objects.get("rig")
        act = getattr(getattr(ao, "animation_data", None), "action", None)
        if act and act.use_frame_range:
            s, e = int(act.frame_start), int(act.frame_end)
            if scene.frame_start != s or scene.frame_end != e:
                scene.frame_start, scene.frame_end = s, e
    except Exception:
        pass

def install_sync():
    hs = bpy.app.handlers.depsgraph_update_post
    hs[:] = [h for h in hs if getattr(h, "__name__", "") != "_sync_action_range"]
    hs.append(_sync_action_range)

build()
'''

if __name__ == "__main__":
    main()
