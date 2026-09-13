#!/usr/bin/env python3
"""Bake a self-contained WebGL page of the Divine Beast Title cat EXACTLY as the disc bake grafts it into Xiao's dungeon
pack: tools/iso_patch/build_cat_pack.py's assemble() is run IN MEMORY on the user's ISO sources (nothing is written), and
the cat subtree (catroot + cat_* nodes, its two meshes, the trimmed cat.mot with the float-up graft, and the MOTION 1 KEY
windows the game plays) is exported through the model viewer's codec (extract_model.py) into viewer_template.html.

A second entry shows the untouched s86 source cat (gedit\\s86\\chara\\c04cat.chr) with every clip its own cfg names, for
comparison, and a third shows Dran (dun\\monstor\\c12a.chr) with every clip — the wing donor for the Angel Shooter / Angel
Gear cat, so the wing poses to port can be judged (user 2026-09-12). Output: cat_viewer.html next to this script (or --out).

    python3 tools/model_viewer/cat_viewer.py [--iso PATH] [--out PATH]
"""
import io
import json
import math
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'iso_patch'))
sys.path.insert(0, os.path.join(HERE, '..', 'lib'))
sys.path.insert(0, HERE)
import ps2iso                                  # noqa: E402
import mot_codec as mc                         # noqa: E402
import build_cat_pack as bcp                   # noqa: E402
import extract_model as em                     # noqa: E402
from mdt_codec import parse_mdt                # noqa: E402


class Archive:
    def __init__(self, iso):
        self.f = open(iso, 'rb')
        recs = ps2iso.parse_root(self.f)
        self.hed = ps2iso.read_file(self.f, recs['DATA.HED'])
        self.hd2 = recs['DATA.HD2']
        self.dat_iso = recs['DATA.DAT']['ext'] * ps2iso.SECTOR

    def read(self, name):
        i = ps2iso.archive_find(self.hed, name)
        if i is None:
            raise SystemExit(f"{name} not in the archive")
        self.f.seek(self.hd2['ext'] * ps2iso.SECTOR + 16 + i * 32)
        off, size = struct.unpack('<II', self.f.read(8))
        self.f.seek(self.dat_iso + off)
        return self.f.read(size)


def subtree_nodes(mds, first, count):
    """read_skeleton on the whole .mds, then the cat's records re-indexed 0..count-1 (parents likewise, root → −1)
    with bind worlds recomputed from the cat root — so build_mesh's auto-skin only ever binds to CAT joints."""
    allnodes = em.read_skeleton(mds)
    out = []
    for k in range(count):
        n = dict(allnodes[first + k])
        n['i'] = k
        n['parent'] = -1 if n['parent'] < 0 else n['parent'] - first
        out.append(n)
    for n in out:
        L = em.mat_from_rt(n['R'], n['T'])
        n['world'] = L if n['parent'] < 0 else em.mat_mul(L, out[n['parent']]['world'])
        n['invworld'] = em.rigid_inv(n['world'])
        n['worldpos'] = (n['world'][3][0], n['world'][3][1], n['world'][3][2])
    return out


def model_from(label, code, folder, nodes, mds, pack, mot_name, motions, mds_name, wgt_name=None, node_base=0):
    """`wgt_name`: the pack's skin-weight record → exact skinning; `node_base`: the .wgt's node indices are offset by this
    (the cat's cat.wgt keeps the rig's own 0..K-1 numbering, so 0 for every case here)."""
    em._MESH_DIM_CACHE.clear()
    weights = em.load_weights(pack, wgt_name)
    meshes = []
    for n in nodes:
        if n['meshoff']:
            per = weights.get(n['i'] + node_base) if weights else None
            mm = em.build_mesh_weighted(mds, n, nodes, per) if per else em.build_mesh(mds, n, nodes)
            if mm:
                meshes.append(mm)
    tracks = em.build_tracks(pack, mot_name, len(nodes))
    return serialize(label, code, folder, nodes, meshes, tracks, motions, mds_name, mot_name)


def serialize(label, code, folder, nodes, meshes, tracks, motions, mds_name, mot_name):
    maxabs = 1.0
    for m in meshes:
        for arr in (m['p0'], m['p1']):
            for p in arr:
                maxabs = max(maxabs, abs(p[0]), abs(p[1]), abs(p[2]))
    for n in nodes:
        maxabs = max(maxabs, abs(n['T'][0]), abs(n['T'][1]), abs(n['T'][2]))
    for t in tracks:
        if t['chan'] == 2:
            for v in t['vals']:
                maxabs = max(maxabs, abs(v[0]), abs(v[1]), abs(v[2]))
    pos_scale = 32000.0 / maxabs
    jnodes = [{'n': n['name'], 'p': n['parent'], 't': [round(c, 4) for c in n['T']], 'q': [round(x, 6) for x in n['quat']]} for n in nodes]
    jmeshes = []
    for m in meshes:
        jmeshes.append({'node': m['node'], 'skin': 1 if m['skin'] else 0, 'nv': m['nv'], 'nt': len(m['tris']), 'tag': m.get('tag', ''),
                        'b0': em.b64_u16(m['b0']), 'b1': em.b64_u16(m['b1']), 'w0': em.b64_u8([w * 255 for w in m['w0']]),
                        'p0': em.b64_i16([c for p in m['p0'] for c in p], pos_scale), 'p1': em.b64_i16([c for p in m['p1'] for c in p], pos_scale),
                        'tri': em.b64_u16([i for t in m['tris'] for i in t])})
    jtracks = []
    for t in tracks:
        scale = 32767.0 if t['chan'] == 0 else pos_scale
        jtracks.append({'node': t['node'], 'chan': t['chan'], 'nk': len(t['frames']), 'f': em.b64_u16(t['frames']),
                        'v': em.b64_i16([c for v in t['vals'] for c in v], scale)})
    maxframe = max((t['frames'][-1] for t in tracks), default=1)
    return {'label': label, 'group': 'Xiao', 'code': code, 'folder': folder, 'name': folder, 'mds': mds_name, 'mot': mot_name,
            'posScale': pos_scale, 'quatScale': 32767.0, 'maxFrame': maxframe, 'skelOnly': 0,
            'nodes': jnodes, 'meshes': jmeshes, 'motions': motions, 'tracks': jtracks,
            '_stats': {'nodes': len(nodes), 'meshes': len(meshes), 'verts': sum(m['nv'] for m in meshes),
                       'tris': sum(len(m['tris']) for m in meshes), 'tracks': len(tracks), 'motions': len(motions), 'rank': 0}}


# ───────────────────────────── the wing graft experiment (viewer only; nothing ships) ─────────────────────────────
WING_BONES = ['r_wing1', 'r_wing2', 'r_wing3', 'r_wing4', 'l_wing1', 'l_wing2', 'l_wing3', 'l_wing4']
WING_ATTACH_NODE = 'cat_sebone2'   # the wing roots sit at this cat bone's bind position (the mid-spine the glow uses)
WING_SCALE = None                  # None = auto: wing length (wing1→wing4 on Dran, ≈59 units) = the cat's body length × WING_SCALE_MUL
WING_SCALE_MUL = 0.5               # user 2026-09-12: 0.6× the first try, then 0.5×
WING_SIZE_MUL = 0.7                # user 2026-09-13: the wings' MESH and bone chain at 0.6× of that, about the wing roots — the roots
                                   # (and so the intersection on the back) stay where they were; only the wing itself shrinks
# The attach point (the midpoint between the two wing roots), cat world in the BIND pose = cat_sebone2's bind position (0, 3.1, 1.0)
# lifted 0.3 and moved 0.7 forward — the position the user approved (2026-09-12); it rides the spine bone from there. (A leap-pose
# re-definition at the shoulder-blade polys put the wings too far back — reverted.)
WING_ATTACH_BIND = (0.0, 3.4, 1.7)
WING_YAW = 0.0                     # radians about the cat's up axis, if Dran's wing orientation needs turning
# Closing the gap between the wing base and the back (user 2026-09-12: solid-colour wings, so the mesh may be extended; moving
# wing vertices spoiled the other poses, and a strip to a seam line stood up as a patch on the spine). The fix is a SLEEVE:
# the wing's base ring — the boundary of the carved membrane where it met Dran's body — is extruded inward into the torso
# as copies FIXED TO THE SPINE BONE (a copy riding the wing bone pivots with the flap and pokes out of the chest when the
# wing is raised), so the wing root continues into the body as a short tube whose cross-section with the back is the
# visible edge, in every pose. Cat units, judged in the leap pose:
WING_SLEEVE = False                # OFF (user 2026-09-12: remove the custom join geometry; extend the wing to the red marks instead)
WING_SLEEVE_LEN = 0.9              # how far the ring is extruded toward the torso
WING_SLEEVE_DROP = 1.2             # the extrusion aims at the midline this far below the attach point: with the root on the skin line, down-inward enters the body fastest from both halves of the ring
WING_PITCH_DEG = 8.0               # angle of attack: the wings tilt back so the leading edge rides high (user 2026-09-12: 3, then +5)
# The COVER (user's red outline, top-down, 2026-09-12): behind the root the membrane runs UNDER the skin and only emerges at the
# body's outline, leaving the shoulder blades bare between the wing and the spine. The cover is a DECAL GRID: a grid of points
# over the outlined region, each lying a hair above the posed back and carrying the bone weights of the NEAREST SKIN VERTEX, so
# … no: skin-weighted points tore into a band in the stand pose, and the skin's own triangles are too coarse. The grid is
# RIGID WITH THE WING-ROOT BONE instead: it lies on the back in the leap pose and swings up with the wing base when raised. Region, cat units in the leap pose: |x| from COVER_X[0] (the inner
# line) to COVER_X[1], z from COVER_Z_BACK to a front edge from COVER_Z_FRONT_IN (inner line) to COVER_Z_FRONT_OUT (outer edge).
WING_COVER = False                 # OFF (see WING_EXTEND)
WING_COVER_X = (0.46, 1.15)
WING_COVER_Z_BACK, WING_COVER_Z_FRONT_IN, WING_COVER_Z_FRONT_OUT = 0.12, 1.2, 1.0
WING_COVER_LIFT = 0.05
WING_COVER_N = 5                   # grid samples per axis
# EXTEND THE WING TO THE RED MARKS (user 2026-09-12, top-down): the wing's base ring is stitched to a line on the back — the
# inner edge at |x| = EXTEND_X_IN from z EXTEND_Z_BACK to EXTEND_Z_FRONT, then out along the front edge to (EXTEND_X_OUT,
# EXTEND_Z_FRONT_OUT). Partner points lie EXTEND_LIFT above the posed back (leap pose) and are rigid with the wing-root bone,
# so the extension is simply more wing. Cat units, leap pose.
WING_EXTEND = True                 # the wing surface over the region the ridges proved (user confirmed 2026-09-12)
# RIDGES: small raised markers along the lines the wings are meant to reach, on the cat's back, weighted like the skin.
WING_RIDGES = False                # the markers served their purpose
WING_RIDGE_H, WING_RIDGE_W, WING_RIDGE_STEP = 0.12, 0.06, 0.08   # crest height above the skin, half-width, sample spacing
WING_EXTEND_X_IN, WING_EXTEND_X_OUT = 0.20, 0.95          # measured from the user's marks (0.33); inner line moved to 0.20 (user
                                                          # 2026-09-13: "extend these tris closer to the spine")
WING_TAB_TILT_DEG = 15.0           # the tabs' dihedral about the CAT'S BODY AXIS (user 2026-09-13: "tilt down towards the cat's
                                   # center … not the axis the wings tilt on"): each cat-anchored corner sits at the rim's height
                                   # (Ri behind, Rf ahead, by z) MINUS its spanwise distance from Ri × tan(tilt) — an absolute slope
                                   # down toward the spine, so the tabs run into the ridge instead of climbing it. None = skin-following
WING_EXTEND_Z_BACK, WING_EXTEND_Z_FRONT, WING_EXTEND_Z_FRONT_OUT = 1.1, 2.03, 1.85   # user's top-down 2026-09-12: the front corner is at the
                                                                                      # neck base (z ≈ 2.0), 0.9 ahead of my first guess
WING_EXTEND_LIFT = 0.1             # the corner points sit this far above the skin
WING_TAB_POLYS = [('Rt', 'Ri', 'ir'), ('Ri', 'if', 'ir'), ('Ri', 'Rf', 'if'),   # the base's top rim (Rt→Ri→Rf) extended to the inner line;
                  ('Rt', 'ir', 'Rm'),                                          # Rf itself is the outer-front corner — no 'of' point below it (user
                  ('Rg', 'Rf', 'if', 'under')]                                 # 2026-09-13: the tabs dipped to it); + the rear tab joined to the
                                   # midpoint Rm of the base's rear edge Rb–Rt; + the lower rim (Rg) joined to the front tab's inner point, closing
                                   # the root's underside ('under' = skipped by the skin-clearance check, it lies inside the body)
WING_TAB_CAP = True                # also close the base ring's open top (the hole Dran's body used to fill) under the fan
WING_ROOT_ANCHOR = True            # ring verts inside WING_ROOT_ANCHOR_X of the midline (the footprint's inward part, which stood up
WING_ROOT_ANCHOR_X = 0.6           # above the back when the humerus folded down) are skinned like the back beneath them; the rim
                                   # (Ri, Rf, Rt, the outer verts) keeps riding the wing so the front base folds cleanly
WING_TAB_SUBDIV = 1                # each top tab → n² triangles laid on the skin (see the block); 1 = the plain triangles
                                   # (user 2026-09-13: back to the plain tabs while the wing size is re-judged)
WING_TAB_ANCHOR_FRAME = 15         # the patch points sit on the skin (+lift) of THIS cat pose (the stand = the folded idle) and copy
                                   # its skinning there; None = the leap reference pose (they sank ≤0.3 into the back when standing)
WING_TAB_HINGE, WING_TAB_HINGE_MAX = 0.35, 0.0   # sub-points closer than this (barycentric) to the ring follow the wing bone, up to this
                                   # much at the ring's own row — a fan hinge instead of sliver triangles tearing between ring and patch
WING_TAB_CLEAR = None              # corners are raised until every tab clears the skin by this (the flat tabs vs the convex shoulder);
                                   # None = off (the tilt sets the heights absolutely; intersecting the back is intended)
# which Dran clip drives which cat clip: cat KEY index → (Dran start, Dran end, Dran speed, loop?)
WING_CLIPS = {4: (200, 205, 0.2, True)}   # cat 'leap' (the fall, 205-214 @0.5) ← Dran motion 3 "charge loop" (user 2026-09-12)
# The LANDING is authored (user 2026-09-13: Dran's charge-end swung the root around; the cat's wings sit lower so the attachment
# must stay put, and the wings must end FOLDED like a bird's — feathers back, tight to the flanks — as the ground idle):
#   cat 215..LAND_FLARE_END ← Dran 70..75 (the forward braking swing = the momentum), root position PINNED;
#   then each bone slerps into WING_FOLD over its own lag window (the tips trail) and stays there: WING_FOLD is the wings' BIND.
WING_LAND = {'cat': (215, 227), 'dran': (70, 75), 'flare_end': 220,
             'lag': [(220.0, 225.0), (220.5, 226.0), (221.0, 226.5), (221.5, 227.0)]}   # per bone (wing1..4): fold start/end frames
WING_FOLD_FRAME = 15               # the cat frame (stand) whose spine orientation the folded pose is authored in
# folded pose, right wing, in the stand pose's world: per bone (span direction root→tip, top-surface normal); the left is mirrored
# in x. A BIRD fold with the dorsal side out (user 2026-09-13). Dran's membrane trails 1.8–2.4 behind every bone line in the
# direction span × top, so: the humerus hangs down the front of the flank (membrane trails back over the flank), the forearm runs
# back along the belly line (membrane rises up the flank), the hand runs back at mid-flank rolled 25° up (vanes graze the back's
# edge); tips just past the rump. The wing's ROOT (base ring + first rows) is pushed out of the shoulder by WING_FOLD_ROOT_SHIFT
# (user 2026-09-13: "push these polys out away from the body a bit"); the humerus is re-aimed so the elbow stays where it was.
WING_FOLD = [((-0.08, -0.96, -0.27), (-0.83, 0.38, 0.10)),   # humerus: down the flank from the shifted root, elbow ≈ at the flank
             ((-0.05,  0.35, -0.94), (-1.00, 0.00, 0.00)),   # forearm: back along the belly line
             (( 0.03,  0.20, -0.98), (-0.90, 0.42, 0.00)),   # hand: back at mid-flank, rolled 25° up
             (( 0.10,  0.15, -0.98), (-0.90, 0.42, 0.00))]   # tips: back, converging over the tail
WING_FOLD_ROOT_SHIFT = (-0.35, 0.10, 0.0)   # the folded wing's root sits this far from the flight pivot (stand world, right wing;
                                            # mirrored for the left): out of the shoulder and up to the back's edge. The root slides
                                            # there over the humerus's fold window and back during the take-off.
WING_PIN_ROOT = True               # ignore Dran's root-bone translation in every clip: the wing root stays on the shoulder and the
                                   # outer bones follow by FK (Dran's per-bone positions ARE an FK chain: +x along the wing, fixed lengths)
WING_LEVEL_AT = 4                  # the cat clip (CAT_KEYS index) in whose middle pose Dran's wing orientation is taken as-is: the wings are
                                   # RIGID to the spine bone; in this pose they come out level, elsewhere they follow the back (user 2026-09-12)


def _slerp(q0, q1, t):
    d = sum(a * b for a, b in zip(q0, q1))
    if d < 0:
        q1 = [-x for x in q1]; d = -d
    if d > 0.9995:
        r = [a + (b - a) * t for a, b in zip(q0, q1)]
    else:
        th = math.acos(max(-1.0, min(1.0, d))); s0 = math.sin((1 - t) * th) / math.sin(th); s1 = math.sin(t * th) / math.sin(th)
        r = [a * s0 + b * s1 for a, b in zip(q0, q1)]
    n = math.sqrt(sum(x * x for x in r)) or 1.0
    return [x / n for x in r]


def _sample(track, f):
    """A .mot track (frames ascending, 4-float values) at fractional frame f — the viewer's own clamp + slerp/lerp."""
    F, V = track['frames'], track['vals']
    if f <= F[0]: return list(V[0])
    if f >= F[-1]: return list(V[-1])
    hi = next(i for i, x in enumerate(F) if x > f); lo = hi - 1
    t = (f - F[lo]) / float(F[hi] - F[lo])
    if track['chan'] == 0:
        return _slerp(V[lo], V[hi], t)
    return [a + (b - a) * t for a, b in zip(V[lo], V[hi])]


def _quat_to_mat(q):
    w, x, y, z = q
    return [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
            [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
            [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]


def _mul3(a, b):
    return [[sum(a[r][k] * b[k][c] for k in range(3)) for c in range(3)] for r in range(3)]


def _t3(a):
    return [[a[c][r] for c in range(3)] for r in range(3)]


def _pitch(v, p):
    """Rotate about the cat's lateral (x) axis so that +p lifts the forward (+z) direction: (0,0,1) → (0, sin p, cos p)."""
    c, s_ = math.cos(p), math.sin(p)
    return [v[0], v[1] * c + v[2] * s_, -v[1] * s_ + v[2] * c]


def _rot_y(v, yaw):
    c, s_ = math.cos(yaw), math.sin(yaw)
    return [v[0] * c + v[2] * s_, v[1], -v[0] * s_ + v[2] * c]


def build_winged_cat(cat_nodes, cat_mds, cat_pack, cat_motions, dran_nodes, dran_mds, dran_pack, dmot_name, log=print):
    """The in-game cat plus Dran's wings: 8 wing bones re-parented under the cat's mid-spine bone, the wing membrane carved
    out of Dran's body mesh BY WEIGHT (dominant bone = a wing bone) and skinned to the new bones, and Dran's chosen clips
    resampled onto the cat's clip windows at Dran's own flap speed. Bind-pose keys bracket each driven window so the
    wings hold still elsewhere."""
    dn = {n['name']: n for n in dran_nodes}
    cn = {n['name']: n for n in cat_nodes}
    r1, l1, r4 = dn['r_wing1']['T'], dn['l_wing1']['T'], dn['r_wing4']['T']       # locals = relative to Dran's root
    center = [(a + b) / 2 for a, b in zip(r1, l1)]
    wing_len = math.dist(r1, r4)
    # cat body length from its skin mesh's bind extents (longest horizontal axis)
    skin = cn['cat_skin']; sm = parse_mdt(cat_mds, skin['meshoff'])
    pts = [em.xform_pt(skin['world'], v[:3]) for v in sm.pos]
    ext = [max(p[i] for p in pts) - min(p[i] for p in pts) for i in range(3)]
    body_len = max(ext[0], ext[2]); body_h = ext[1]
    S = WING_SCALE if WING_SCALE else body_len / wing_len * WING_SCALE_MUL
    attach = None                                         # set below, once the reference pose is known
    log(f"wing graft: cat body {body_len:.1f} long × {body_h:.1f} high; Dran wing {wing_len:.1f} → scale {S:.3f} × size {WING_SIZE_MUL:g} (wing ≈ {wing_len * S * WING_SIZE_MUL:.1f}); roots {14.8 * S:.1f} apart")
    # ── new nodes: Dran's wing bones are children of Dran's root; place them in the CAT's WORLD (Dran's orientation,
    # positioned at the attach point) and convert into catroot-local — neither root's bind rotation is assumed identity ──
    nodes = [dict(n) for n in cat_nodes]
    parent = cn[WING_ATTACH_NODE]['i']                   # the wings RIDE the spine bone (the root stays at the feet while the
    root_inv = nodes[parent]['invworld']                 # body rises in the leap — root-parented wings sat under the belly)
    # the cat's FK at a frame (the viewer's own clamp + slerp rule), for the reference pose
    tracks = em.build_tracks(cat_pack, 'cat.mot', len(cat_nodes))
    ctr = {(t['node'], t['chan']): t for t in tracks}
    def cat_world(frame):
        W = [None] * len(cat_nodes)
        for n in cat_nodes:
            q = _sample(ctr[(n['i'], 0)], frame) if (n['i'], 0) in ctr else n['quat']
            t = _sample(ctr[(n['i'], 2)], frame)[:3] if (n['i'], 2) in ctr else n['T']
            L = em.mat_from_rt(_quat_to_mat(q), t)
            W[n['i']] = L if n['parent'] < 0 else em.mat_mul(L, W[n['parent']])
        return W
    rcs, rce = bcp.CAT_KEYS[WING_LEVEL_AT][0], bcp.CAT_KEYS[WING_LEVEL_AT][1]
    ref_frame = (rcs + rce) // 2
    P_ref = cat_world(ref_frame)[parent]                 # the spine bone in the reference pose (world)
    R_ref = [P_ref[r][:3] for r in range(3)]
    attach = list(WING_ATTACH_BIND)
    anchor_local = list(em.xform_pt(root_inv, attach))                                # the attach point in the spine bone's frame (rides the back)
    attach_ref = em.xform_pt(P_ref, anchor_local)
    log(f"  wings rigid to {WING_ATTACH_NODE}; attach {WING_ATTACH_BIND} at bind = ({attach_ref[0]:.2f}, {attach_ref[1]:.2f}, {attach_ref[2]:.2f}) in the {bcp.CAT_KEYS[WING_LEVEL_AT][3]!r} pose (frame {ref_frame}); spine pitch there vs bind: row0 {[round(c, 2) for c in R_ref[0]]} vs {[round(c, 2) for c in nodes[parent]['world'][0][:3]]}")
    def local_of(Rw, Td):
        """Dran's (world orientation, Dran-root-local position) → this bone's transform in the spine bone's frame, such that
        in the reference pose it reads exactly as Dran's world pose anchored on the back."""
        rel = [(Td[i] - center[i]) for i in range(3)]
        rel = em.xform_pt([list(droot_R[0]) + [0], list(droot_R[1]) + [0], list(droot_R[2]) + [0], [0, 0, 0, 1]], rel)
        rel = [c * S for c in _pitch(_rot_y(rel, WING_YAW), pr)]
        Rl = _mul3(Rw, _t3(R_ref))
        Tl = [anchor_local[i] + sum(rel[k] * _t3(R_ref)[k][i] for k in range(3)) for i in range(3)]
        return Rl, Tl
    droot_R = dran_nodes[0]['R']                         # Dran-root-local → Dran-world rotation
    yawR = [[math.cos(WING_YAW), 0, math.sin(WING_YAW)], [0, 1, 0], [-math.sin(WING_YAW), 0, math.cos(WING_YAW)]]
    pr = math.radians(WING_PITCH_DEG)
    pitchR = [[1, 0, 0], [0, math.cos(pr), -math.sin(pr)], [0, math.sin(pr), math.cos(pr)]]   # row-vector form of _pitch
    def world_R(R_dran_local):                           # a Dran-root-local rotation as a cat-world rotation (yaw, then pitch)
        return _mul3(_mul3(_mul3(R_dran_local, droot_R), yawR), pitchR)
    def world_T(T_dran_local):                           # a Dran-root-local position → cat-world position on the cat
        rel = [T_dran_local[i] - center[i] for i in range(3)]
        rel = em.xform_pt([list(droot_R[0]) + [0], list(droot_R[1]) + [0], list(droot_R[2]) + [0], [0, 0, 0, 1]], rel)
        rel = _pitch(_rot_y(rel, WING_YAW), pr)
        return [attach[i] + rel[i] * S for i in range(3)]
    def to_local(Rw, Tw):                                # cat-world (R, T) → attach-bone-local (R, T), bind pose
        W = em.mat_from_rt(Rw, Tw); L = em.mat_mul(W, root_inv)
        return [L[r][:3] for r in range(3)], list(L[3][:3])
    weights_all = em.load_weights(dran_pack, 'c12a.wgt')[dn['obj1']['i']]
    obj1 = dn['obj1']; om = parse_mdt(dran_mds, obj1['meshoff'])
    vw_all = [em.xform_pt(obj1['world'], v[:3]) for v in om.pos]
    base = len(nodes); wid = {}
    for k, name in enumerate(WING_BONES):
        d = dn[name]
        R, T = local_of(world_R(d['R']), d['T'])
        n = {'i': base + k, 'name': name, 'meshoff': 0, 'parent': parent, 'R': R, 'T': T, 'quat': em.mat_to_quat(R)}
        L = em.mat_from_rt(R, T); n['world'] = em.mat_mul(L, nodes[parent]['world']); n['invworld'] = em.rigid_inv(n['world'])
        n['worldpos'] = (n['world'][3][0], n['world'][3][1], n['world'][3][2])
        nodes.append(n); wid[d['i']] = base + k
    log(f"  wing roots (cat world): r {tuple(round(c, 2) for c in nodes[wid[dn['r_wing1']['i']]]['worldpos'])}, l {tuple(round(c, 2) for c in nodes[wid[dn['l_wing1']['i']]]['worldpos'])}; tips r {tuple(round(c, 2) for c in nodes[wid[dn['r_wing4']['i']]]['worldpos'])}")
    # ── the wing as an FK chain: Dran's four bones per side are siblings, but their positions always satisfy
    # T[k+1] = T[k] + len_k · (bone k's local +x); so with the root pinned on the shoulder every pose is rotations only ──
    SIDES = {'r': ['r_wing1', 'r_wing2', 'r_wing3', 'r_wing4'], 'l': ['l_wing1', 'l_wing2', 'l_wing3', 'l_wing4']}
    seg_len = {sd: [math.dist(dn[a]['T'], dn[b]['T']) * S * WING_SIZE_MUL for a, b in zip(ch, ch[1:])] for sd, ch in SIDES.items()}
    pivot_local = {sd: list(nodes[wid[dn[ch[0]]['i']]]['T']) for sd, ch in SIDES.items()}       # wing1's bind position, spine-local
    def fk_locals(sd, Rls, root=None):
        """spine-local rotations of wing1..4 → spine-local positions, chained from the pinned pivot (or `root`)"""
        T = [list(root if root is not None else pivot_local[sd])]
        for k in range(3):
            T.append([T[k][i] + seg_len[sd][k] * Rls[k][0][i] for i in range(3)])
        return T
    def _unit(v):
        n = math.sqrt(sum(c * c for c in v)) or 1.0; return [c / n for c in v]
    def _cross(a, b):
        return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
    def _basis(span, top):
        span = _unit(span); d = sum(a * b for a, b in zip(top, span)); top = _unit([t - d * s_ for t, s_ in zip(top, span)])
        return [span, top, _cross(span, top)]
    # flight reference per bone: its spine-local rotation in the level pose (Dran frame WING_CLIPS' loop start) and the membrane's
    # top normal there — the fold is expressed as "take the flight (span, top) to the folded (span, top)", so no assumption about
    # either wing's local axis conventions (they are mirrored on Dran)
    loop_start = min(ds for ds, de, sp, lp in WING_CLIPS.values() if lp)
    dtracks = {(t['node'], t['chan']): t for t in em.build_tracks(dran_pack, dmot_name, len(dran_nodes))}
    def dran_local_R(name, df):
        d = dn[name]; rsrc = dtracks.get((d['i'], 0))
        Rw = world_R(_quat_to_mat(_sample(rsrc, df))) if (df is not None and rsrc) else world_R(d['R'])
        return local_of(Rw, d['T'])[0]
    R_stand = [r[:3] for r in cat_world(WING_FOLD_FRAME)[parent][:3]]
    fold_local = {}; fold_root = {}
    for sd, ch in SIDES.items():
        sx = -1.0 if sd == 'l' else 1.0                                           # WING_FOLD is the RIGHT wing (x < 0); mirror for the left
        Rls = []
        for k, name in enumerate(ch):
            Rf = dran_local_R(name, loop_start)                                  # flight pose, spine-local (rows = bone axes)
            import numpy as _np
            bid = dn[name]['i']
            vi_ = [v for v, infl in weights_all.items() if any(b == bid and w >= 20 for b, w in infl)]   # any real influence
            loc = [em.xform_pt(dran_nodes[bid]['invworld'], vw_all[v]) for v in vi_]
            if len(loc) < 4:                                                       # (r_wing3 owns almost nothing) → the sheet as a whole
                loc = [em.xform_pt(dran_nodes[bid]['invworld'], vw_all[v]) for v, infl in weights_all.items()
                       if any(dran_nodes[b]['name'].startswith(name[:2]) for b, w in infl)]
            c = [sum(p[i] for p in loc) / len(loc) for i in range(3)]
            A_ = _np.array(loc) - _np.array(c); _u, _s, _vt = _np.linalg.svd(A_, full_matrices=False); nrm = list(_vt[2])   # membrane normal, bone-local
            span_f = list(Rf[0]); top_f = [sum(nrm[j] * Rf[j][i] for j in range(3)) for i in range(3)]
            top_w = [sum(top_f[k2] * R_ref[k2][i] for k2 in range(3)) for i in range(3)]        # in the leap world: must point UP
            if top_w[1] < 0: top_f = [-c_ for c_ in top_f]
            span_t, top_t = WING_FOLD[k]
            span_t = [span_t[0] * sx, span_t[1], span_t[2]]; top_t = [top_t[0] * sx, top_t[1], top_t[2]]
            span_t = [sum(span_t[j] * _t3(R_stand)[j][i] for j in range(3)) for i in range(3)]  # stand world → spine-local
            top_t = [sum(top_t[j] * _t3(R_stand)[j][i] for j in range(3)) for i in range(3)]
            Bf, Bt = _basis(span_f, top_f), _basis(span_t, top_t)
            A = _mul3(_t3(Bf), Bt)                                                # row vectors: v_t = v_f · Bf^T · Bt
            Rls.append(_mul3(Rf, A))
        shift_w = [WING_FOLD_ROOT_SHIFT[0] * sx, WING_FOLD_ROOT_SHIFT[1], WING_FOLD_ROOT_SHIFT[2]]
        shift_l = [sum(shift_w[j] * _t3(R_stand)[j][i] for j in range(3)) for i in range(3)]     # stand world → spine-local (direction)
        fold_root[sd] = [pivot_local[sd][i] + shift_l[i] for i in range(3)]
        fold_local[sd] = (Rls, fk_locals(sd, Rls, fold_root[sd]))
        for k, name in enumerate(ch):                                             # the folded pose is the wings' bind
            n = nodes[wid[dn[name]['i']]]; n['R'] = Rls[k]; n['T'] = fold_local[sd][1][k]; n['quat'] = em.mat_to_quat(Rls[k])
            L = em.mat_from_rt(n['R'], n['T']); n['world'] = em.mat_mul(L, nodes[parent]['world']); n['invworld'] = em.rigid_inv(n['world'])
            n['worldpos'] = (n['world'][3][0], n['world'][3][1], n['world'][3][2])
        Ws = cat_world(WING_FOLD_FRAME)[parent]
        tipk = fold_local[sd][1][3]; tipd = fold_local[sd][0][3][0]
        tip = [tipk[i] + 2.14 * tipd[i] for i in range(3)]
        log(f"  {sd} folded (stand world): " + ', '.join(f"w{k + 1} {tuple(round(float(c), 2) for c in em.xform_pt(Ws, fold_local[sd][1][k]))}" for k in range(4)) + f", tip ≈ {tuple(round(float(c), 2) for c in em.xform_pt(Ws, tip))}; segments {[round(l, 2) for l in seg_len[sd]]}")
    # ── wing meshes carved from obj1 by weight ──
    weights, vw = weights_all, vw_all
    tris = em.mdt_triangles(om)
    def side(vi):
        infl = weights.get(vi, [])
        if not infl: return None
        b, w = max(infl, key=lambda bw: bw[1])
        nm = dran_nodes[b]['name'] if b < len(dran_nodes) else ''
        return 'r' if nm.startswith('r_wing') else 'l' if nm.startswith('l_wing') else None
    meshes = []; wing_sets = {}
    for sd, root in (('r', 'r_wing1'), ('l', 'l_wing1')):
        wt = [t for t in tris if all(side(v) == sd for v in t)]
        used = sorted({v for t in wt for v in t}); remap = {v: i for i, v in enumerate(used)}
        wing_sets[sd] = (wt, used, remap)
        b0, p0, b1, p1, w0 = [], [], [], [], []
        for vi in used:
            infl = sorted([(b, w) for b, w in weights.get(vi, []) if b in wid], key=lambda bw: -bw[1])[:2]
            if not infl: infl = [(dn[root]['i'], 1.0)]
            (ba, wa) = infl[0]; (bb, wb) = infl[1] if len(infl) > 1 else (ba, 0.0)
            tot = wa + wb
            pa = [c * S * WING_SIZE_MUL for c in em.xform_pt(dran_nodes[ba]['invworld'], vw[vi])]
            pb = [c * S * WING_SIZE_MUL for c in em.xform_pt(dran_nodes[bb]['invworld'], vw[vi])]
            b0.append(wid[ba]); p0.append(pa); b1.append(wid[bb]); p1.append(pb); w0.append(wa / tot if tot else 1.0)
        meshes.append({'node': wid[dn[root]['i']], 'skin': True, 'nv': len(used), 'tris': [(remap[a], remap[b], remap[c]) for a, b, c in wt],
                       'b0': b0, 'p0': p0, 'b1': b1, 'p1': p1, 'w0': w0})
        log(f"  {sd} wing: {len(used)} verts, {len(wt)} tris")
    # cat's own meshes (real weights)
    cw = em.load_weights(cat_pack, 'cat.wgt')
    for n in cat_nodes:
        if n['meshoff']:
            per = cw.get(n['i']) if cw else None
            mm = em.build_mesh_weighted(cat_mds, n, nodes, per) if per else em.build_mesh(cat_mds, n, nodes)
            if mm: meshes.append(mm)
    # ── tracks: the cat's, plus the wing bones' rigid-to-the-spine transforms: constant (their bind) except inside the driven
    # windows, where Dran's sampled pose is converted through the same reference frame; bind keys bracket each window ──
    windows = []
    for ki, (ds, de, dspd, loop) in sorted(WING_CLIPS.items()):
        cs, ce, cspd, _ = bcp.CAT_KEYS[ki]
        win_game = (ce - cs) / cspd; loop_game = (de - ds) / dspd
        cycles = max(1, round(win_game / loop_game)) if loop else 1
        windows.append((cs, ce, ds, de, cycles, loop))
        if loop: log(f"  clip {ki} ({cs}-{ce} @{cspd}): {cycles} flap cycle(s) over {win_game:.0f} game frames (Dran's own rate would give {win_game / loop_game:.2f})")
        else: log(f"  clip {ki} ({cs}-{ce} @{cspd}): Dran {ds}-{de} once over {win_game:.0f} game frames (Dran's own length {loop_game:.0f} → {loop_game / win_game:.2f}× its rate)")
    def _smooth(t):
        t = min(max(t, 0.0), 1.0); return t * t * (3 - 2 * t)
    lcs, lce = WING_LAND['cat']; lds, lde = WING_LAND['dran']; lfe = WING_LAND['flare_end']
    def wing_locals(f):
        """Spine-local (R, T) of wing1..4 per side at cat frame f: Dran's loop sample inside a WING_CLIPS window, the
        authored landing inside WING_LAND, the folded bind elsewhere. Root pinned, positions by FK."""
        out = {}
        for sd, ch in SIDES.items():
            df = None
            for cs, ce, ds, de, cycles, loop in windows:
                if cs <= f <= ce:
                    u = (f - cs) / float(ce - cs); df = ds + (math.fmod(u * cycles * (de - ds), de - ds) if loop else u * (de - ds))
            root = None
            if df is not None:
                Rls = [dran_local_R(name, df) for name in ch]
            elif lcs <= f <= lce:
                dfl = lds + (lde - lds) * min(1.0, (f - lcs) / float(lfe - lcs))             # the flare: Dran 70..75 over 215..flare_end
                Rls = []
                for k, name in enumerate(ch):
                    Rf = dran_local_R(name, dfl); Rt = fold_local[sd][0][k]
                    a, b = WING_LAND['lag'][k]; t = _smooth((f - a) / (b - a))
                    if t <= 0: Rls.append(Rf)
                    elif t >= 1: Rls.append(Rt)
                    else: Rls.append(_quat_to_mat(_slerp(em.mat_to_quat(Rf), em.mat_to_quat(Rt), t)))
                a, b = WING_LAND['lag'][0]; t = _smooth((f - a) / (b - a))                    # the root slides out with the humerus
                root = [pivot_local[sd][i] + (fold_root[sd][i] - pivot_local[sd][i]) * t for i in range(3)]
            else:
                Rls = fold_local[sd][0]; root = fold_root[sd]
            if WING_PIN_ROOT:
                Tls = fk_locals(sd, Rls, root)
            else:
                Tls = [local_of(world_R(dn[name]['R']), _sample(dtracks[(dn[name]['i'], 2)], df)[:3] if df is not None and (dn[name]['i'], 2) in dtracks else dn[name]['T'])[1] for name in ch]
            for k, name in enumerate(ch):
                out[wid[dn[name]['i']]] = (Rls[k], Tls[k])
        return out
    def wing_pose(f):
        """Cat-world matrices of the 8 wing bones at cat frame f and the anchor."""
        Pw = cat_world(f)[parent]
        loc = wing_locals(f)
        return {nid: em.mat_mul(em.mat_from_rt(R, T), Pw) for nid, (R, T) in loc.items()}, em.xform_pt(Pw, anchor_local)
    Wref, anchor_ref = wing_pose(ref_frame)
    Pref_inv = em.rigid_inv(cat_world(ref_frame)[parent])
    from collections import Counter, defaultdict
    def edges_of(t):
        return (frozenset((t[0], t[1])), frozenset((t[1], t[2])), frozenset((t[2], t[0])))
    full_edges = Counter(e for t in tris for e in edges_of(t))
    Wcat = cat_world(ref_frame)
    skin_me = next(x for x in meshes if x['node'] == cn['cat_skin']['i'])
    skin_ref = []
    for i in range(skin_me['nv']):
        pa = em.xform_pt(Wcat[skin_me['b0'][i]], skin_me['p0'][i]); pb = em.xform_pt(Wcat[skin_me['b1'][i]], skin_me['p1'][i]); wa = skin_me['w0'][i]
        skin_ref.append([pa[c] * wa + pb[c] * (1 - wa) for c in range(3)])
    for me, sd in zip(meshes[:2], ('r', 'l')):
        wt, used, remap = wing_sets[sd]
        wedges = Counter(e for t in wt for e in edges_of(t))
        attach = [e for e, c in wedges.items() if c == 1 and full_edges[e] >= 2]   # wing-boundary edges the body shares = the base ring
        adj = defaultdict(list)
        for e in attach:
            va, vb = tuple(e); adj[va].append(vb); adj[vb].append(va)
        chains, seen = [], set()
        for start in sorted(adj, key=lambda v: (len(adj[v]), v)):                  # endpoints first (a closed ring has none)
            if start in seen: continue
            chain, cur, prev = [start], start, None; seen.add(start)
            while True:
                nxt = [v for v in adj[cur] if v != prev and v not in seen]
                if not nxt: break
                prev, cur = cur, nxt[0]; chain.append(cur); seen.add(cur)
            if len(chain) > 2 and chain[0] in adj[chain[-1]]: chain.append(chain[0])   # close the loop
            chains.append(chain)
        def wpos(li):                                                               # a wing vertex in the leap pose (world)
            pa = em.xform_pt(Wref[me['b0'][li]], me['p0'][li]); pb = em.xform_pt(Wref[me['b1'][li]], me['p1'][li]); wa = me['w0'][li]
            return [pa[c] * wa + pb[c] * (1 - wa) for c in range(3)]
        ring = []
        for chain in chains:
            for vi in chain:
                if vi not in ring: ring.append(vi)
        rc = [sum(wpos(remap[vi])[c] for vi in ring) / len(ring) for c in range(3)]   # the ring's centre, leap pose
        aim = [0.0, anchor_ref[1] - WING_SLEEVE_DROP, rc[2]]
        dvec = [a - c for a, c in zip(aim, rc)]; dl = math.sqrt(sum(c * c for c in dvec)) or 1.0
        dw = [c / dl * WING_SLEEVE_LEN for c in dvec]                                 # the extrusion, world, leap pose
        copy_of = {}
        for vi in (ring if WING_SLEEVE else []):
            pos = [c + d for c, d in zip(wpos(remap[vi]), dw)]                         # the ring vertex, pushed into the torso
            pl = list(em.xform_pt(Pref_inv, pos))                                     # fixed to the spine bone
            copy_of[vi] = me['nv']; me['nv'] += 1
            me['b0'].append(parent); me['p0'].append(pl); me['b1'].append(parent); me['p1'].append(pl); me['w0'].append(1.0)
        added = 0
        for chain in (chains if WING_SLEEVE else []):
            for va, vb in zip(chain, chain[1:]):
                A, B, A2, B2 = remap[va], remap[vb], copy_of[va], copy_of[vb]
                me['tris'] += [(A, B, B2), (A, B2, A2), (B, A, B2), (B2, A, A2)]      # both windings
                added += 4
        if WING_SLEEVE:
            log(f"  {sd} wing sleeve: base ring {len(ring)} verts ({'closed' if any(c[0] == c[-1] for c in chains) else 'open'}), extruded {WING_SLEEVE_LEN:g} toward ({aim[0]:.1f}, {aim[1]:.2f}, {aim[2]:.2f}) → {added} tris")
        if WING_EXTEND:
            # A FEW polys (user 2026-09-12: "max 3 to 5 polys attached to the base of each wing to cover and intersect with the
            # cat's back"): tabs from the wing's base ring to the corners of the marked region, so the wing root spreads over
            # the shoulder blade. The ring itself sits under the skin here; the corner points sit on the skin (+ lift) and are
            # weighted like the skin under them (spine inside → arm outside) so the tabs hold through the flap and the other
            # poses. Named points: Rr/Rm/Rf = ring rear/middle/front; ir/if/of/or = region corners inner-rear, inner-front,
            # outer-front, outer-rear (x, z from the user's marks).
            sx = 1.0 if sd == 'l' else -1.0
            arm = cn['cat_arm_hidari' if sd == 'l' else 'cat_arm_migi']['i']
            sb1, sb2 = cn['cat_sebone1']['i'], cn['cat_sebone2']['i']
            def skin_hit(x, z):
                """the topmost cat_skin triangle under (x, z) in the reference pose: (y, tri, (l1, l2, l3)) or None"""
                best = None
                for t in skin_me['tris']:
                    A, B, C = (skin_ref[v] for v in t)
                    det = (B[0] - A[0]) * (C[2] - A[2]) - (C[0] - A[0]) * (B[2] - A[2])
                    if abs(det) < 1e-9: continue
                    l1 = ((B[0] - x) * (C[2] - z) - (C[0] - x) * (B[2] - z)) / det
                    l2 = ((C[0] - x) * (A[2] - z) - (A[0] - x) * (C[2] - z)) / det
                    l3 = 1 - l1 - l2
                    if l1 < -1e-6 or l2 < -1e-6 or l3 < -1e-6: continue
                    y = l1 * A[1] + l2 * B[1] + l3 * C[1]
                    if best is None or y > best[0]: best = (y, t, (l1, l2, l3))
                return best
            def skin_y2(x, z):
                h = skin_hit(x, z); return h[0] if h else None
            def skin_hit_at(x, z, skin_pts):
                best = None
                for t in skin_me['tris']:
                    A, B, C = (skin_pts[v] for v in t)
                    det = (B[0] - A[0]) * (C[2] - A[2]) - (C[0] - A[0]) * (B[2] - A[2])
                    if abs(det) < 1e-9: continue
                    l1 = ((B[0] - x) * (C[2] - z) - (C[0] - x) * (B[2] - z)) / det
                    l2 = ((C[0] - x) * (A[2] - z) - (A[0] - x) * (C[2] - z)) / det
                    l3 = 1 - l1 - l2
                    if l1 < -1e-6 or l2 < -1e-6 or l3 < -1e-6: continue
                    y = l1 * A[1] + l2 * B[1] + l3 * C[1]
                    if best is None or y > best[0]: best = (y, t, (l1, l2, l3))
                return best
            def skin_point_at(frame, x, z, lift):
                """a point on the skin at (x, z) in the given cat pose, lifted, skinned like the skin beneath it:
                (world position, b0, p0, b1, p1, w0) or None (no skin under it there)"""
                Wf = cat_world(frame)
                pts_f = []
                for i in range(skin_me['nv']):
                    pa = em.xform_pt(Wf[skin_me['b0'][i]], skin_me['p0'][i]); pb = em.xform_pt(Wf[skin_me['b1'][i]], skin_me['p1'][i]); wa = skin_me['w0'][i]
                    pts_f.append([pa[c] * wa + pb[c] * (1 - wa) for c in range(3)])
                h = skin_hit_at(x, z, pts_f)
                if not h: return None
                y, t, lam = h; q = [x, y + lift, z]; acc = {}
                for v, l in zip(t, lam):
                    for b, w in ((skin_me['b0'][v], skin_me['w0'][v]), (skin_me['b1'][v], 1.0 - skin_me['w0'][v])):
                        if w > 1e-6: acc[b] = acc.get(b, 0.0) + l * w
                infl = sorted(acc.items(), key=lambda kv: -kv[1])[:2]; tot = sum(w for _, w in infl)
                (ba, wa), (bb, wb) = infl[0], (infl[1] if len(infl) > 1 else infl[0])
                return q, ba, list(em.xform_pt(em.rigid_inv(Wf[ba]), q)), bb, list(em.xform_pt(em.rigid_inv(Wf[bb]), q)), wa / tot
            def skin_weights(x, z, q):
                """the skin's OWN skinning under (x, z), pooled over the triangle's three vertices by barycentric weight and cut
                to two bones, with local positions for the point q — so a patch point moves exactly as the skin beneath it"""
                h = skin_hit(x, z)
                if not h: return None
                _, t, lam = h; acc = {}
                for v, l in zip(t, lam):
                    for b, w in ((skin_me['b0'][v], skin_me['w0'][v]), (skin_me['b1'][v], 1.0 - skin_me['w0'][v])):
                        if w > 1e-6: acc[b] = acc.get(b, 0.0) + l * w
                infl = sorted(acc.items(), key=lambda kv: -kv[1])[:2]; tot = sum(w for _, w in infl)
                (ba, wa), (bb, wb) = infl[0], (infl[1] if len(infl) > 1 else infl[0])
                return ba, list(em.xform_pt(em.rigid_inv(Wcat[ba]), q)), bb, list(em.xform_pt(em.rigid_inv(Wcat[bb]), q)), wa / tot
            chain = max(chains, key=len)
            if len(chain) > 1 and chain[0] == chain[-1]: chain = chain[:-1]
            P = {vi: wpos(remap[vi]) for vi in chain}
            # the base ring is a closed loop: the midline vertex (Dran's wings meet at the back centre) → the UPPER rim
            # (inner-top, outer, front-most) → the lower rim back to the midline; the tabs hang off the upper rim
            bi = min(range(len(chain)), key=lambda k: abs(P[chain[k]][0]))
            chain = chain[bi:] + chain[:bi]
            if len(chain) > 2 and P[chain[1]][1] < P[chain[-1]][1]: chain = [chain[0]] + chain[1:][::-1]
            fi = max(range(len(chain)), key=lambda k: P[chain[k]][2])
            top = chain[1:fi + 1]                                                    # inner-top … front-most
            pts = {'Rb': remap[chain[0]], 'Ri': remap[top[0]], 'Rf': remap[top[-1]],
                   'Rg': remap[chain[fi + 1] if fi + 1 < len(chain) else chain[-1]]}   # Rg = the lower rim's first vertex past Rf (under Ri)
            # Rt = the wing's own rear-top base vertex: the third corner of the (only) wing triangle on the ring edge Rb–Ri
            # (user 2026-09-13: the base polys — this one included — are what gets extended toward the spine)
            eb, ei = chain[0], top[0]
            rt = [v for t in wt if eb in t and ei in t for v in t if v not in (eb, ei)]
            pts['Rt'] = remap[rt[0]] if rt else pts['Ri']
            # ── the ROOT is anchored to the body (user 2026-09-13: the base tri (Ri, Rb, Rt) crossed the tabs mid-fold; "make the
            # mesh more stable"): the base ring, Rt and (through pooling) Rm take the skinning of the skin beneath them in the
            # reference pose, so the intersection with the back is literally fixed and the wing flexes at its first membrane row
            # instead of swinging its root through the tabs ──
            def wp_any(i):                                                            # any wing-mesh vertex, ref pose
                M = lambda b: Wref[b] if b in Wref else Wcat[b]
                pa = em.xform_pt(M(me['b0'][i]), me['p0'][i]); pb = em.xform_pt(M(me['b1'][i]), me['p1'][i]); wa = me['w0'][i]
                return [pa[c] * wa + pb[c] * (1 - wa) for c in range(3)]
            if WING_ROOT_ANCHOR:
                n_sk = 0; anchored = []
                for vi in list(chain) + (rt[:1] if rt else []):
                    li = remap[vi]; q = wpos(li)
                    if abs(q[0]) >= WING_ROOT_ANCHOR_X: continue                         # only the footprint's INNER part (user 2026-09-13:
                    sw_ = skin_weights(q[0], q[2], q)                                    # anchoring the whole ring pulled the base into the
                    if sw_: ba, pa, bb, pb, wa = sw_; n_sk += 1                          # body and wrinkled the front base)
                    else: ba = bb = sb2; pa = pb = list(em.xform_pt(em.rigid_inv(Wcat[sb2]), q)); wa = 1.0
                    me['b0'][li], me['p0'][li], me['b1'][li], me['p1'][li], me['w0'][li] = ba, pa, bb, pb, wa
                    anchored.append(f"({q[0]:.2f}, {q[2]:.2f})")
                log(f"  {sd} wing root anchored: {len(anchored)} inner ring verts (|x| < {WING_ROOT_ANCHOR_X:g}) skinned like the back: {', '.join(anchored)}; the rim rides the wing")
            # Rm = the midpoint of the base's rear edge Rb–Rt (user 2026-09-13: "connect to the midpoint of the back edge"),
            # a new wing vertex skinned EXACTLY as the average of its two ends (their bone influences pooled per bone)
            acc = {}
            for vi in (pts['Rb'], pts['Rt']):
                for b, w, pl in ((me['b0'][vi], me['w0'][vi], me['p0'][vi]), (me['b1'][vi], 1.0 - me['w0'][vi], me['p1'][vi])):
                    if w <= 1e-6: continue
                    a = acc.setdefault(b, [0.0, [0.0, 0.0, 0.0]]); a[0] += 0.5 * w
                    for c in range(3): a[1][c] += 0.5 * w * pl[c]
            infl = sorted(acc.items(), key=lambda kv: -kv[1][0])[:2]
            tot = sum(a[0] for _, a in infl)
            (ba, aa), (bb, ab) = infl[0], (infl[1] if len(infl) > 1 else infl[0])
            pts['Rm'] = me['nv']; me['nv'] += 1
            me['b0'].append(ba); me['p0'].append([c / aa[0] for c in aa[1]]); me['w0'].append(aa[0] / tot)
            me['b1'].append(bb); me['p1'].append([c / ab[0] for c in ab[1]])
            ring_log = ', '.join(f"{'Rb' if k == 0 else 'Ri' if vi == top[0] else 'Rf' if vi == top[-1] else '·'}({P[vi][0]:.2f}, {P[vi][1]:.2f}, {P[vi][2]:.2f}; skin {(skin_y2(P[vi][0], P[vi][2]) or float('nan')):.2f})"
                                 for k, vi in enumerate(chain))
            if rt: q = wp_any(remap[rt[0]]); ring_log += f"; Rt({q[0]:.2f}, {q[1]:.2f}, {q[2]:.2f})"
            q = wp_any(pts['Rm']); ring_log += f"; Rm({q[0]:.2f}, {q[1]:.2f}, {q[2]:.2f})"
            q = wp_any(pts['Rg']); ring_log += f"; Rg({q[0]:.2f}, {q[1]:.2f}, {q[2]:.2f})"
            def back_pt(x, z):
                xx = sx * x; sy = skin_y2(xx, z)
                while sy is None and abs(xx) > 0.05:                                    # beyond the back's silhouette: slide inward
                    xx -= sx * 0.05; sy = skin_y2(xx, z)
                if sy is None: sy = anchor_ref[1] - 0.4
                pos = [xx, sy + WING_EXTEND_LIFT, z]
                spine = sb1 if z < 0.9 else sb2
                w_spine = min(max((0.6 - abs(xx)) / (0.6 - 0.35), 0.0), 1.0)
                i = me['nv']; me['nv'] += 1
                me['b0'].append(spine); me['p0'].append(list(em.xform_pt(em.rigid_inv(Wcat[spine]), pos)))
                me['b1'].append(arm); me['p1'].append(list(em.xform_pt(em.rigid_inv(Wcat[arm]), pos))); me['w0'].append(w_spine)
                return i, pos
            corners = {'ir': (WING_EXTEND_X_IN, WING_EXTEND_Z_BACK), 'if': (WING_EXTEND_X_IN, WING_EXTEND_Z_FRONT),
                       'of': (WING_EXTEND_X_OUT, WING_EXTEND_Z_FRONT_OUT), 'or': (WING_EXTEND_X_OUT, WING_EXTEND_Z_BACK)}
            cpos = {}
            for nm in [p for poly in WING_TAB_POLYS for p in poly]:
                if nm in corners and nm not in pts:
                    pts[nm], cpos[pts[nm]] = back_pt(*corners[nm])
            polys = []
            if WING_TAB_CAP:                                                         # close the base hole under the upper rim
                for k in range(1, len(top) - 1):
                    polys.append((remap[top[0]], remap[top[k]], remap[top[k + 1]]))
            coarse = [tuple(pts[p] for p in poly[:3]) for poly in WING_TAB_POLYS]
            tabs = [t for t, poly in zip(coarse, WING_TAB_POLYS) if len(poly) == 3]     # 'under' polys are inside the body by design
            # clearance: the tab surface vs the skin under it (negative = the skin pokes through), sampled inside each tab;
            # a corner is raised (never a ring vertex) until every tab clears the skin by WING_TAB_CLEAR
            def wp_of(i):                                                            # any wing-mesh vertex, ref pose: wing bones from
                M = lambda b: Wref[b] if b in Wref else Wcat[b]                          # the wing pose, cat bones from the cat pose
                pa = em.xform_pt(M(me['b0'][i]), me['p0'][i]); pb = em.xform_pt(M(me['b1'][i]), me['p1'][i]); wa = me['w0'][i]
                return [pa[c] * wa + pb[c] * (1 - wa) for c in range(3)]
            def clearance():
                worst = None
                for tri in tabs:
                    V = [wp_of(i) for i in tri]
                    for a_ in range(0, 7):
                        for b_ in range(0, 7 - a_):
                            l1, l2 = a_ / 6, b_ / 6; l3 = 1 - l1 - l2
                            p = [l1 * V[0][c] + l2 * V[1][c] + l3 * V[2][c] for c in range(3)]
                            if p[2] < WING_EXTEND_Z_BACK or abs(p[0]) < WING_EXTEND_X_IN: continue   # only inside the marked region
                            sy = skin_y2(p[0], p[2])
                            if sy is None: continue
                            if worst is None or p[1] - sy < worst[0]: worst = (p[1] - sy, tri, p)
                return worst
            raised = {}
            for _ in range(40 if WING_TAB_CLEAR is not None else 0):
                w_ = clearance()
                if w_ is None: break
                c, tri, p = w_
                if c >= WING_TAB_CLEAR: break
                cands = [i for i in tri if i in cpos]
                i = min(cands, key=lambda i: math.hypot(cpos[i][0] - p[0], cpos[i][2] - p[2]))
                d = (WING_TAB_CLEAR - c) * 1.2
                cpos[i][1] += d; raised[i] = raised.get(i, 0.0) + d
                pos = cpos[i]; spine, arm_ = me['b0'][i], me['b1'][i]
                me['p0'][i] = list(em.xform_pt(em.rigid_inv(Wcat[spine]), pos)); me['p1'][i] = list(em.xform_pt(em.rigid_inv(Wcat[arm_]), pos))
            if WING_TAB_TILT_DEG is not None:
                th = math.radians(WING_TAB_TILT_DEG); hx = P[top[0]][0]                   # hinge line: the rim, running fore-aft
                (z0, y0), (z1, y1) = (P[top[0]][2], P[top[0]][1]), (P[top[-1]][2], P[top[-1]][1])   # Ri, Rf
                for i, pos in cpos.items():
                    t = 0.0 if z1 == z0 else min(max((pos[2] - z0) / (z1 - z0), 0.0), 1.0)
                    y_rim = y0 + (y1 - y0) * t                                            # the rim's height beside this corner
                    pos[1] = y_rim - abs(pos[0] - hx) * math.tan(th)                      # lower toward the spine by the dihedral
                    me['p0'][i] = list(em.xform_pt(em.rigid_inv(Wcat[me['b0'][i]]), pos)); me['p1'][i] = list(em.xform_pt(em.rigid_inv(Wcat[me['b1'][i]]), pos))
            # ── subdivide the top tabs (user 2026-09-13: "subdivide these so you can fold the wings while keeping the intersection
            # into the back stable and not clipping"): each tab → n² triangles; new points on a wing–wing edge stay wing-skinned
            # (their ends' influences pooled), every other new point is laid on the skin (+lift, never below the tab's own plane)
            # and weighted like the skin under it — so the patch hugs the back in every pose and only the one ring-side row
            # stretches to the wing as it folds (the hinge = the stable intersection) ──
            n = WING_TAB_SUBDIV; wing_vert = lambda i: i < len(used) or i == pts.get('Rm'); ch_root = SIDES[sd][0]
            P_anchor = cat_world(WING_TAB_ANCHOR_FRAME)[parent] if WING_TAB_ANCHOR_FRAME is not None else None
            cache = {}
            def sub_vertex(key, pos, ends):
                if key in cache: return cache[key]
                if all(wing_vert(e) for e, _ in ends):                                   # on a wing–wing edge: pool the ends
                    acc = {}
                    for vi, wgt in ends:
                        for b, w, pl in ((me['b0'][vi], me['w0'][vi], me['p0'][vi]), (me['b1'][vi], 1.0 - me['w0'][vi], me['p1'][vi])):
                            if w <= 1e-6: continue
                            a = acc.setdefault(b, [0.0, [0.0, 0.0, 0.0]]); a[0] += wgt * w
                            for c in range(3): a[1][c] += wgt * w * pl[c]
                    infl = sorted(acc.items(), key=lambda kv: -kv[1][0])[:2]; tot = sum(a[0] for _, a in infl)
                    (ba, aa), (bb, ab) = infl[0], (infl[1] if len(infl) > 1 else infl[0])
                    i = me['nv']; me['nv'] += 1
                    me['b0'].append(ba); me['p0'].append([c / aa[0] for c in aa[1]]); me['w0'].append(aa[0] / tot)
                    me['b1'].append(bb); me['p1'].append([c / ab[0] for c in ab[1]])
                else:                                                                    # on the back: skin height, skin-like weights
                    xx, z = pos[0], pos[2]; sy = skin_y2(xx, z)
                    y = max(pos[1], sy + WING_EXTEND_LIFT) if sy is not None else pos[1]
                    q = [xx, y, z]
                    spine = sb1 if z < 0.9 else sb2
                    w_spine = min(max((0.6 - abs(xx)) / (0.6 - 0.35), 0.0), 1.0)
                    skin_b = spine if w_spine >= 0.5 else arm                             # the dominant skin bone (one slot left)
                    sw = sum(wgt for e, wgt in ends if wing_vert(e))                       # how far toward the ring this point sits
                    w_wing = min(max((sw - WING_TAB_HINGE) / (1.0 - WING_TAB_HINGE), 0.0), 1.0) * WING_TAB_HINGE_MAX
                    i = me['nv']; me['nv'] += 1
                    if w_wing > 0:                                                        # hinge: wing bone + the dominant skin bone
                        wb = wid[dn[ch_root]['i']]                                         # (the ring's bone: this wing's wing1)
                        me['b0'].append(wb); me['p0'].append(list(em.xform_pt(em.rigid_inv(Wref[wb]), q)))
                        me['b1'].append(skin_b); me['p1'].append(list(em.xform_pt(em.rigid_inv(Wcat[skin_b]), q))); me['w0'].append(w_wing)
                    else:                                                                 # on the back: skinned like the skin beneath it,
                        sw_ = None                                                        # placed on the skin of the STAND pose (the idle
                        if WING_TAB_ANCHOR_FRAME is not None:                             # the fold is judged in) — same spine-relative spot
                            l_ = em.xform_pt(Pref_inv, q); q15 = em.xform_pt(P_anchor, l_)
                            r_ = skin_point_at(WING_TAB_ANCHOR_FRAME, q15[0], q15[2], WING_EXTEND_LIFT)
                            if r_: sw_ = r_[1:]
                        if sw_ is None: sw_ = skin_weights(xx, z, q)
                        if sw_:
                            ba, pa, bb, pb, wa = sw_
                            me['b0'].append(ba); me['p0'].append(pa); me['b1'].append(bb); me['p1'].append(pb); me['w0'].append(wa)
                        else:                                                             # beyond the silhouette: the spine↔arm guess
                            me['b0'].append(spine); me['p0'].append(list(em.xform_pt(em.rigid_inv(Wcat[spine]), q)))
                            me['b1'].append(arm); me['p1'].append(list(em.xform_pt(em.rigid_inv(Wcat[arm]), q))); me['w0'].append(w_spine)
                cache[key] = i; return i
            fine = []
            for tri, poly in zip(coarse, WING_TAB_POLYS):
                if len(poly) > 3: polys.append(tri); continue                             # 'under': one tri, inside the body
                V = [wp_of(i) for i in tri]
                grid = {}
                for a_ in range(n + 1):
                    for b_ in range(n + 1 - a_):
                        c_ = n - a_ - b_; bc = (a_, b_, c_)
                        if bc.count(n) == 1: grid[bc] = tri[bc.index(n)]; continue          # a corner
                        pos = [(a_ * V[0][k] + b_ * V[1][k] + c_ * V[2][k]) / n for k in range(3)]
                        zero = [k for k in range(3) if bc[k] == 0]
                        if len(zero) == 1:                                                  # on an edge: shared with the neighbour tab
                            e0, e1 = [k for k in range(3) if k != zero[0]]
                            lo, hi = (e0, e1) if tri[e0] < tri[e1] else (e1, e0)
                            key = ('e', tri[lo], tri[hi], bc[hi]); ends = [(tri[e0], bc[e0] / n), (tri[e1], bc[e1] / n)]
                        else:
                            key = ('t', tri, bc); ends = [(tri[k], bc[k] / n) for k in range(3)]
                        grid[bc] = sub_vertex(key, pos, ends)
                for a_ in range(n):
                    for b_ in range(n - a_):
                        c_ = n - a_ - b_
                        fine.append((grid[(a_ + 1, b_, c_ - 1)], grid[(a_, b_ + 1, c_ - 1)], grid[(a_, b_, c_)]))
                        if c_ >= 2: fine.append((grid[(a_ + 1, b_, c_ - 1)], grid[(a_ + 1, b_ + 1, c_ - 2)], grid[(a_, b_ + 1, c_ - 1)]))
            polys += fine; tabs = fine
            me['tris'] += polys
            w_ = clearance(); c = w_[0] if w_ else float('nan')
            corner_log = [f"{nm} ({cpos[i][0]:.2f}, {cpos[i][1]:.2f}, {cpos[i][2]:.2f}{' ↑%.2f' % raised[i] if i in raised else ''})" for nm, i in pts.items() if i in cpos]
            log(f"  {sd} wing tabs: ring {ring_log}")
            log(f"  {sd} wing tabs: corners {', '.join(corner_log)}; cap {len(polys) - len(fine) - sum(1 for q in WING_TAB_POLYS if len(q) > 3)} + tabs {[q[:3] for q in WING_TAB_POLYS]} subdivided ×{n} → {len(fine)} tris (+ under) = {len(polys)} tris, {len(cache)} new verts; dihedral {WING_TAB_TILT_DEG}°; min clearance over the skin {c:+.2f}")
    if WING_RIDGES:
        def skin_y3(x, z):
            best = None
            for t in skin_me['tris']:
                A, B, C = (skin_ref[v] for v in t)
                det = (B[0] - A[0]) * (C[2] - A[2]) - (C[0] - A[0]) * (B[2] - A[2])
                if abs(det) < 1e-9: continue
                l1 = ((B[0] - x) * (C[2] - z) - (C[0] - x) * (B[2] - z)) / det
                l2 = ((C[0] - x) * (A[2] - z) - (A[0] - x) * (C[2] - z)) / det
                l3 = 1 - l1 - l2
                if l1 < -1e-6 or l2 < -1e-6 or l3 < -1e-6: continue
                y = l1 * A[1] + l2 * B[1] + l3 * C[1]
                if best is None or y > best: best = y
            return best
        sb1, sb2 = cn['cat_sebone1']['i'], cn['cat_sebone2']['i']
        ridge = {'node': sb2, 'skin': True, 'nv': 0, 'tris': [], 'b0': [], 'p0': [], 'b1': [], 'p1': [], 'w0': [], 'tag': 'ridge'}
        def add_pt(pos, sx):
            arm = cn['cat_arm_hidari' if sx > 0 else 'cat_arm_migi']['i']
            spine = sb1 if pos[2] < 0.9 else sb2
            w_spine = min(max((0.6 - abs(pos[0])) / (0.6 - 0.35), 0.0), 1.0)
            i = ridge['nv']; ridge['nv'] += 1
            ridge['b0'].append(spine); ridge['p0'].append(list(em.xform_pt(em.rigid_inv(Wcat[spine]), pos)))
            ridge['b1'].append(arm); ridge['p1'].append(list(em.xform_pt(em.rigid_inv(Wcat[arm]), pos))); ridge['w0'].append(w_spine)
            return i
        for sx in (-1.0, 1.0):
            poly = [(sx * WING_EXTEND_X_IN, WING_EXTEND_Z_BACK), (sx * WING_EXTEND_X_IN, WING_EXTEND_Z_FRONT), (sx * WING_EXTEND_X_OUT, WING_EXTEND_Z_FRONT_OUT)]
            samples = []
            for (x0, z0), (x1, z1) in zip(poly, poly[1:]):
                L = math.hypot(x1 - x0, z1 - z0); n = max(1, int(L / WING_RIDGE_STEP))
                nx, nz = -(z1 - z0) / L, (x1 - x0) / L                                # perpendicular in the ground plane
                for k in range(n + 1):
                    t = k / n; samples.append((x0 + (x1 - x0) * t, z0 + (z1 - z0) * t, nx, nz))
            prev = None
            for x, z, nx, nz in samples:
                sy = skin_y3(x, z)
                if sy is None: prev = None; continue
                Lp = add_pt([x - nx * WING_RIDGE_W, sy + 0.02, z - nz * WING_RIDGE_W], sx)
                Rp = add_pt([x + nx * WING_RIDGE_W, sy + 0.02, z + nz * WING_RIDGE_W], sx)
                Cp = add_pt([x, sy + WING_RIDGE_H, z], sx)
                if prev:
                    L0, R0, C0 = prev
                    for (a_, b_, c_, d_) in ((L0, Lp, Cp, C0), (R0, C0, Cp, Rp)):
                        ridge['tris'] += [(a_, b_, c_), (a_, c_, d_), (b_, a_, c_), (c_, a_, d_)]
                prev = (Lp, Rp, Cp)
        meshes.append(ridge)
        log(f"  ridges: {ridge['nv']} verts, {len(ridge['tris'])} tris along the two L-lines (|x| {WING_EXTEND_X_IN:g}, z {WING_EXTEND_Z_BACK:g}..{WING_EXTEND_Z_FRONT:g}; front edge to ({WING_EXTEND_X_OUT:g}, {WING_EXTEND_Z_FRONT_OUT:g}))")
    spans = [(cs, ce) for cs, ce, ds, de, cycles, loop in windows] + [WING_LAND['cat']]
    keyed = sorted({f for cs, ce in spans for f in range(cs, ce + 1)})
    frames_all = sorted(set(keyed) | {f for cs, ce in spans for f in (cs - 1, ce + 1)})   # bind brackets where nothing else keys
    per_frame = {f: wing_locals(f) for f in frames_all}
    for k, name in enumerate(WING_BONES):
        nid = base + k
        tracks.append({'node': nid, 'chan': 0, 'frames': frames_all, 'vals': [list(em.mat_to_quat(per_frame[f][nid][0])) for f in frames_all]})
        tracks.append({'node': nid, 'chan': 2, 'frames': frames_all, 'vals': [list(per_frame[f][nid][1]) + [0.0] for f in frames_all]})
    log(f"  wing tracks: keys at {frames_all[0]}..{frames_all[-1]} ({len(frames_all)} frames); landing {lcs}-{lce}: Dran {lds}-{lde} flare to {lfe}, then fold (lag {WING_LAND['lag']}); folded = bind")
    return serialize("Divine Beast cat + Dran's wings — leap = charge loop, land = flare + fold (experiment)", 'c04b+cat+wings', 'viewer experiment (nothing baked)',
                     nodes, meshes, tracks, cat_motions, 'c04b.mds + c12a obj1 wings', 'cat.mot + c12a.mot (wings)')


def main():
    a = sys.argv[1:]
    iso = a[a.index('--iso') + 1] if '--iso' in a else os.path.expanduser('~/ROMs/Dark Cloud (USA).iso')
    out = a[a.index('--out') + 1] if '--out' in a else os.path.join(HERE, 'cat_viewer.html')
    arc = Archive(iso)

    # ── the in-game cat: the disc bake, assembled in memory ──
    base, cat, flt, glow = (arc.read(n) for n in (bcp.HOST_CHR, bcp.CAT_CHR, bcp.FLOAT_CHR, bcp.GLOW_SRC))
    packed, rep = bcp.assemble(base, cat, flt, glow)
    pack = mc.Pack.parse(packed)
    nb, K = rep['nodes']
    mds = bytearray(pack.find(bcp.HOST_MDS).payload)
    root = 0x18 + nb * 0x70
    assert mds[root:root + 0x20].split(b'\0')[0] == bcp.CAT_ROOT_NAME.encode(), 'cat root record'
    m = list(struct.unpack_from('<16f', mds, root + 0x28))              # the bake hides the cat on Xiao by scaling the
    for r in range(3):                                                    # root's bind 3x3 by HIDE_SCALE; the runtime copy
        for c in range(3):                                                # restores 1.0 — do the same for the view
            m[r * 4 + c] /= bcp.HIDE_SCALE
    struct.pack_into('<16f', mds, root + 0x28, *m)
    mds = bytes(mds)
    nodes = subtree_nodes(mds, nb, K)
    motions = [{'name': cm, 'gloss': '', 'start': s, 'end': e, 'speed': sp, 'id': bcp.KEY_START + i, 'empty': 0}
               for i, (s, e, sp, cm) in enumerate(bcp.CAT_KEYS)]
    lo, hi = min(k[0] for k in bcp.CAT_KEYS), max(k[1] for k in bcp.CAT_KEYS)
    motions.append({'name': 'every clip (timeline)', 'gloss': '', 'start': lo, 'end': hi, 'speed': 0.5, 'id': -1, 'empty': 0})
    game = model_from('Divine Beast cat — as baked into c04b.chr', 'c04b+cat', 'dun/mainchara/c04b.chr (patched)',
                      nodes, mds, pack, 'cat.mot', motions, bcp.HOST_MDS, wgt_name='cat.wgt')
    print(f"in-game cat: {K} nodes appended after her {nb}, {game['_stats']['meshes']} meshes, {game['_stats']['tris']} tris, "
          f"{game['_stats']['tracks']} tracks, {len(motions)} clips; pack {rep['size'][0]:,} → {rep['size'][1]:,} B; textures {rep['textures']}")

    # ── the untouched s86 source cat, every clip its cfg names ──
    src = mc.Pack.parse(cat)
    cfg = em.find_cfg(src)
    mds_name, mot_name, smotions = em.parse_cfg(cfg.payload)
    smds = src.find(mds_name or 'c04cat.mds').payload
    snodes = em.read_skeleton(smds)
    source = model_from('s86 source cat — every clip', 'c04cat', bcp.CAT_CHR.replace('\\', '/'),
                        snodes, smds, src, mot_name or 'c04cat.mot', smotions, mds_name or 'c04cat.mds', wgt_name='c04cat.wgt')
    print(f"source cat: {len(snodes)} nodes, {source['_stats']['tris']} tris, {len(smotions)} clips, max frame {source['maxFrame']}")

    # ── Dran, the wing donor: its rig and every clip (wings = r_wing1..4 / l_wing1..4 bones under the root) ──
    dran_name = r'dun\monstor\c12a.chr'
    dran = mc.Pack.parse(arc.read(dran_name))
    dcfg = em.find_cfg(dran)
    dmds_name, dmot_name, dmotions = em.parse_cfg(dcfg.payload)
    DRAN_GLOSS = {'飛行': 'flying (hover)', '突進（離陸      ）': 'charge: take-off', '突進ループ': 'charge loop', '突進（74': 'charge (end)',
                  'ダメージ': 'damage', '死亡': 'death', '離陸': 'take-off', '火': 'fire breath', '毛繕い(入り）': 'preening (enter)',
                  '毛繕いループ': 'preening loop', '毛繕い（戻り）': 'preening (return)', 'じたじた': 'flailing'}
    for m in dmotions:
        m['gloss'] = DRAN_GLOSS.get(m['name'], m['gloss'] or ('(unnamed)' if not m['name'] else ''))
    dmds = dran.find(dmds_name).payload
    dnodes = em.read_skeleton(dmds)
    donor = model_from('Dran — wing donor, every clip', 'c12a', dran_name.replace('\\', '/'), dnodes, dmds, dran, dmot_name, dmotions, dmds_name, wgt_name='c12a.wgt')
    donor['group'] = 'Dran'
    wings = [n['name'] for n in dnodes if 'wing' in n['name'].lower()]
    print(f"Dran: {len(dnodes)} nodes ({len(wings)} wing bones: {', '.join(wings)}), {donor['_stats']['tris']} tris, {donor['_stats']['tracks']} tracks, {len(dmotions)} clips, max frame {donor['maxFrame']}")

    winged = build_winged_cat(nodes, mds, pack, motions, dnodes, dmds, dran, dmot_name)
    winged['group'] = 'Xiao'

    tpl = io.open(os.path.join(HERE, 'viewer_template.html'), encoding='utf-8').read()
    html = tpl.replace('<title>Dark Cloud Model Viewer</title>', '<title>Divine Beast Cat Rig</title>')
    html = html.replace('/*__MODEL_DATA__*/', 'const MODELS = ' + json.dumps([winged, game, source, donor], separators=(',', ':'), ensure_ascii=False) + ';')
    io.open(out, 'w', encoding='utf-8').write(html)
    print(f"wrote {out}: {len(html.encode('utf-8')) / 1e6:.2f} MB")


if __name__ == '__main__':
    main()
