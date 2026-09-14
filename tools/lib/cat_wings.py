"""The Divine Beast cat's wings: Dran's (dun\\monstor\\c12a.chr) wing bones and membrane grafted onto the s86 cat rig that
build_cat_pack.py bakes into Xiao's dungeon pack — every pose, tab polygon, weight and motion key authored here, every
vertex and texture taken from the user's own disc at patch time (nothing of the game's is stored in the repo).

Entry points: build_winged_cat(...) → the raw rig/mesh/track data (viewer indices = cat-relative node ids); wing_graft(read,
packed, rep) → the same on top of a wingless assemble() output, for tools/iso_patch/wing_bake.py. The WING_* knobs below are
the authored design (tuned in the WebGL viewer, tools/model_viewer/cat_viewer.py, which only displays what this builds).
Pure Python: the bake runs under the stock python3 the app launches.
"""
import math
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'iso_patch'))
sys.path.insert(0, HERE)
import mot_codec as mc                         # noqa: E402
import build_cat_pack as bcp                   # noqa: E402
import extract_model as em                     # noqa: E402
from mdt_codec import parse_mdt                # noqa: E402


# ───────────────────────────── small linear algebra (no numpy: the bake runs on the stock interpreter) ─────────────────────────────
def _solve(A, b):
    """Gaussian elimination with partial pivoting; A n×n (lists), b n → x (None if singular)."""
    n = len(b); M = [list(map(float, A[i])) + [float(b[i])] for i in range(n)]
    for c in range(n):
        p = max(range(c, n), key=lambda r: abs(M[r][c]))
        if abs(M[p][c]) < 1e-15: return None
        M[c], M[p] = M[p], M[c]
        for r in range(n):
            if r == c: continue
            f = M[r][c] / M[c][c]
            if f: M[r] = [a - f * bb for a, bb in zip(M[r], M[c])]
    return [M[i][n] / M[i][i] for i in range(n)]


def _inv4(M):
    """Inverse of a 4×4 (lists); None if singular."""
    n = 4; A = [list(map(float, M[i])) + [1.0 if j == i else 0.0 for j in range(n)] for i in range(n)]
    for c in range(n):
        p = max(range(c, n), key=lambda r: abs(A[r][c]))
        if abs(A[p][c]) < 1e-15: return None
        A[c], A[p] = A[p], A[c]
        f = A[c][c]; A[c] = [a / f for a in A[c]]
        for r in range(n):
            if r != c and A[r][c]:
                g = A[r][c]; A[r] = [a - g * bb for a, bb in zip(A[r], A[c])]
    return [row[n:] for row in A]


def _col(b, Wwing, Wc):
    """A bone's posed world (row-vector 4×4) as column form: (R with world = R·p + t, t)."""
    M = Wwing[b] if b in Wwing else Wc[b]
    return [[float(M[j][i]) for j in range(3)] for i in range(3)], [float(M[3][0]), float(M[3][1]), float(M[3][2])]


def _blend(w, M, p0, p1):
    """Linear-blend position of a two-bone vertex: w·(R0·p0 + t0) + (1 − w)·(R1·p1 + t1)."""
    (R0, t0), (R1, t1) = M
    return [w * (sum(R0[i][j] * p0[j] for j in range(3)) + t0[i]) + (1 - w) * (sum(R1[i][j] * p1[j] for j in range(3)) + t1[i]) for i in range(3)]


def _two_pose_fit(w, MA, MB, tgtA, tgtB, guard=50.0):
    """Bone-local positions (p0, p1) of a two-bone vertex that land on tgtA under pose A and tgtB under pose B — a 6×6 system
    (2 poses × 3 coords); singular along the fold's own axis, so the minimum-norm least-squares solution (Tikhonov, λ tiny).
    None if it explodes (the caller keeps its old locals)."""
    (RA0, tA0), (RA1, tA1) = MA; (RB0, tB0), (RB1, tB1) = MB
    A, rhs = [], []
    for (R0, t0, R1, t1, tgt) in ((RA0, tA0, RA1, tA1, tgtA), (RB0, tB0, RB1, tB1, tgtB)):
        for i in range(3):
            A.append([w * R0[i][j] for j in range(3)] + [(1 - w) * R1[i][j] for j in range(3)])
            rhs.append(tgt[i] - w * t0[i] - (1 - w) * t1[i])
    N = [[sum(A[k][i] * A[k][j] for k in range(6)) for j in range(6)] for i in range(6)]
    g = [sum(A[k][i] * rhs[k] for k in range(6)) for i in range(6)]
    lam = 1e-8 * (max(N[i][i] for i in range(6)) + 1e-12)
    for i in range(6): N[i][i] += lam
    x = _solve(N, g)
    if x is None or not all(math.isfinite(v) for v in x) or max(abs(v) for v in x) >= guard: return None
    return [x[0], x[1], x[2]], [x[3], x[4], x[5]]


def _pca_normal(pts, c):
    """The least principal axis of a point cloud (unit vector): the eigenvector of the 3×3 scatter matrix with the smallest
    eigenvalue, by cyclic Jacobi rotations."""
    S = [[sum((p[i] - c[i]) * (p[j] - c[j]) for p in pts) for j in range(3)] for i in range(3)]
    V = [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]
    for _ in range(60):
        off = sum(S[i][j] ** 2 for i in range(3) for j in range(3) if i != j)
        if off < 1e-18: break
        for p in range(3):
            for q in range(p + 1, 3):
                if abs(S[p][q]) < 1e-30: continue
                th = 0.5 * math.atan2(2 * S[p][q], S[q][q] - S[p][p])
                cs, sn = math.cos(th), math.sin(th)
                for k in range(3):                                            # S ← Jᵀ S J on columns p, q
                    skp, skq = S[k][p], S[k][q]
                    S[k][p], S[k][q] = cs * skp - sn * skq, sn * skp + cs * skq
                for k in range(3):                                            # … and rows
                    spk, sqk = S[p][k], S[q][k]
                    S[p][k], S[q][k] = cs * spk - sn * sqk, sn * spk + cs * sqk
                for k in range(3):
                    vkp, vkq = V[k][p], V[k][q]
                    V[k][p], V[k][q] = cs * vkp - sn * vkq, sn * vkp + cs * vkq
    m = min(range(3), key=lambda i: S[i][i])
    v = [V[k][m] for k in range(3)]; n = math.sqrt(sum(a * a for a in v)) or 1.0
    return [a / n for a in v]


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




# ───────────────────────────── the wing graft (build_cat_pack bakes it through wing_bake.py) ─────────────────────────────
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
WING_RAISE = 0.2                   # the whole wing (its roots, so every pose) sits this much higher than WING_ATTACH_BIND; the tab
                                   # corners on the back stay where they are (user 2026-09-13: "raise the entirety of the wings 0.1
                                   # while keeping the attachment points on the back the same")
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
WING_TAB_POLYS = [('Rb', 'Ri', 'ir'),          # the rear tab hangs off the base triangle's INNER edge Rb–Ri (user 2026-09-13: the tabs
                  ('Ri', 'if', 'ir'),          # must connect to the edge of the base polys, not share their other edges)
                  ('RIM', 'if'),               # the front: a fan from 'if' over EVERY upper-rim segment Ri→…→Rf (a single chord
                  ('Rg', 'Rf', 'if', 'under')] # Ri–Rf left a gap against the rim's middle vertices)
WING_TAB_CAP = True                # also close the base ring's open top (the hole Dran's body used to fill) under the fan
WING_TAB_FLOOR = True              # and FLOOR the base: a fan from the rear corner 'ir' over the ring's LOWER rim (Rb → A → Rg) and
                                   # on to 'if', so the roof (tabs + fan), the cap, the 'under' wall and this floor make a closed boot.
                                   # Without it the roof's two ends (Rb–ir at the back, if–Rg at the front) were open edges lying on
                                   # the cat's back — hidden while the ring sat on the back, a see-through gap once the float-up's
                                   # stroke lifted the ring 0.7 off it (user 2026-09-13, polys 7/174 and 120/121). Existing verts only.
WING_APEX_PULL = (0.30, 0.6)       # (pull at the midline, |x| where it fades to 0): the inboard fan's tip is moved OUTWARD toward
                                   # the wing root in the mesh itself — it lies under the back's skin in flight, so the open wing
                                   # is unchanged, and the shorter fan rises less when the humerus folds (user 2026-09-13: "reduce
                                   # the height the polys spike to so their slope when folded is a little gentler")
WING_ROOT_FORWARD = (0.15, 0.8, 0.85, 0.45)   # (forward shift, z below which, |x| where it fades to 0, |x| up to which it is full —
                                   # leap pose): the fan's back edge moves forward so it no longer cuts into the folded wing behind
                                   # it; the outer rear vertices get less so the fold behind them closes up (user 2026-09-13)
WING_FOLD_LIFT = {28: 0.25, 24: 0.15, 29: 0.10, 19: 0.05}   # wing-mesh vertex → lift in the folded pose (two-pose fit; the
                                   # leap is exact): the top edge of the membrane behind the root, whose blend sagged into a V
WING_ROOT_LOWER = None             # (drop at the midline, |x| where it fades to 0): the inboard fan's apex is LOWERED by this much
                                   # in the folded pose only — a two-pose fit (leap exact, fold = rigid spot moved straight down),
                                   # so the fan's slope is gentler when folded without pulling it anywhere (user 2026-09-13)
WING_ROOT_SOFTEN = None            # (share at the midline, |x| where it fades to 0): the inboard fan's apex leans on the body
WING_ROOT_ANCHOR = False           # ring verts inside WING_ROOT_ANCHOR_X of the midline (the footprint's inward part, which stood up
WING_ROOT_ANCHOR_X = None          # above the back when the humerus folded down) are skinned like the back beneath them; the rim
                                   # (Ri, Rf, Rt, the outer verts) keeps riding the wing so the front base folds cleanly
WING_TAB_SUBDIV = 2                # each top tab → n² triangles laid on the skin (see the block); 1 = the plain triangles
                                   # (user 2026-09-13: back to the plain tabs while the wing size is re-judged)
WING_TAB_SUBDIV_POOL = True        # new sub-points are the plain average of their edge's ends — position AND skinning pooled — so the
                                   # base bends smoothly between the wing and the body (user 2026-09-13: "subdivide these polys once
                                   # … fold the base of the wing a bit more smoothly"); False = the older skin-projected points
WING_TAB_BULGE = 0.2               # the pooled wing↔body midpoints are pushed this far out along the shoulder in the FOLDED pose
                                   # (two-pose fit; the open wing is unchanged) so the folded base rounds over the blade
WING_TAB_SUBDIV_NEIGHBOURS = True  # also split the wing polys that share an edge with a tab (the base triangles), so no T-junctions
WING_TAB_ANCHOR_FRAME = 15         # the patch points sit on the skin (+lift) of THIS cat pose (the stand = the folded idle) and copy
                                   # its skinning there; None = the leap reference pose (they sank ≤0.3 into the back when standing)
WING_TAB_HINGE, WING_TAB_HINGE_MAX = 0.35, 0.0   # sub-points closer than this (barycentric) to the ring follow the wing bone, up to this
                                   # much at the ring's own row — a fan hinge instead of sliver triangles tearing between ring and patch
WING_TAB_CLEAR = None              # corners are raised until every tab clears the skin by this (the flat tabs vs the convex shoulder);
                                   # None = off (the tilt sets the heights absolutely; intersecting the back is intended)
# which Dran clip drives which cat clip: cat KEY index → (Dran start, Dran end, Dran speed, loop?)
WING_CLIPS = {4: (200, 205, 0.2, True),   # cat 'leap' (the fall, 205-214 @0.5) ← Dran motion 3 "charge loop" (user 2026-09-12)
              7: (295, 300, 0.15, False)}  # cat 'float-up' (285-294 @0.6) ← Dran motion 2 "charge: take-off" frames 295-300 = the
                                          # DOWNSTROKE, wings raised at the first frame; the game's 10-frame cross-fade from the
                                          # ready pose (wings folded) then IS the unfold/upstroke (user 2026-09-13)
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
WING_FOLD_ROOT_SHIFT = (-0.43, 0.08, 0.0)   # the folded wing's root sits this far from the flight pivot (stand world, right wing;
                                            # mirrored for the left): out of the shoulder and up to the back's edge. The root slides
                                            # there over the humerus's fold window and back during the take-off.
WING_HAND_DROOP = {'bones': {1: -10.0, 2: 8.0, 3: -2.0}, 'frames': (224, 227)}   # extra pitch (degrees, about the cat's lateral axis, in the spine
                                   # frame) per wing bone index (0 = wing1 … 3 = wing4), eased in over these landing frames and held
                                   # in the idle. Pitching the FOREARM (1) lowers the whole hand: the back feathers come DOWN to the
                                   # back's line without leaning inward or tipping forward (user 2026-09-13)
WING_HAND_ROLL = {'bones': {2: 6.0, 3: 10.0}, 'frames': (224, 227)}   # extra roll (degrees, about each bone's own span) per
                                   # wing bone index, eased over these landing frames and held in the idle: + = the feathers above
                                   # the hand line lean IN toward the cat (checked in v59; the sign is mirrored per side in code)
WING_STROKE = {7: {'phi': [(285.0, 78.0), (287.5, 100.0), (288.5, 100.0), (292.0, -20.0), (294.0, 0.0)], 'extend': [(285.0, 0.0), (287.5, 1.0)],
                   # (110° with the inboard hinge put the vanes ON the midline — top 100° with roots at lateral 0.4 = tips ~1.5 apart)
                   # top 110° held 287.5-288.5 so the lagging hand catches up: the glide droops 19° below lateral, so the stretched
                   # wings end ~5-10° from vertical, tips ~1 apart (user 2026-09-13: "at the peak of the stretch the wings should be
                   # almost parallel"; 80° gave a 58° V, 100° with no hold still ~35°)
                   'lag': 0.2, 'sweep': 60.0, 'axis': 'spine', 'hinge_in': 0.25}}
                                   # 'hinge_in': the flap rotates about an axis this far INBOARD of the wing1 origin (toward the spine),
                                   # so the root rides up on a small arc and the base ring's inboard edge stays on the back — the two
                                   # wings keep protruding close together as they rise (user 2026-09-13: "the visible back between the
                                   # wings increases as the wings extend"; about the origin itself the ring stood up 0.7 above the back
                                   # and only the stretched tabs bridged to the spine). 0 = the pinned origin. Root at 294 = the origin.
                                   # 'phi': (frame, deg) keys of the flap angle (+ = up from the glide), smoothstepped; 'extend': (frame,
                                   # 0..1) keys of the wing's EXTENSION: 0 = the forearm and hand keep the folded idle's chain-local
                                   # rotations (the wing rises FLEXED, as a bird's does: the engine's ready → float-up fade then moves
                                   # only the humerus and the body), 1 = the stroke's stretched shape — "when the first half of the wing
                                   # is fully risen the primaries extend upward" (user 2026-09-13). Both are read at each bone's LAGGED
                                   # frame, so the elbow opens before the wrist and the hand trails the arm through the stroke.
                                   # 'axis': 'spine' = flap about the cat's own fore-aft axis; 'world' = about the world-horizontal
                                   # fore-aft axis (per frame, from the spine's pitch) — REJECTED: with the body reared ~60° the
                                   # glide's sweep-back rotates too, so the raised wings pointed FORWARD over the head and the stroke
                                   # bottom went under the belly (30+ verts inside). The glide already droops 19° below lateral in
                                   # the spine frame, so 'bottom' −20° puts the tips ~40° below lateral: a spread V, not flat along
                                   # the flanks (−40/−45° did that, and clipped the hind flanks).
                                   # per WING_CLIPS key: an AUTHORED flap instead of Dran's frames (user 2026-09-13: "from 289 the wings
                                   # make an arc shape instead of trailing the momentum like a real bird's downflap … Dran's motions
                                   # aren't the best flapping motion"). Base pose = the clip's Dran END frame (300 = the charge-loop
                                   # glide = the leap). Every bone is rotated about the cat's fore-aft axis (through the pinned
                                   # shoulder pivot) by a flap angle φ: 'top' (deg, + = up) at the clip's first frame (the fade
                                   # target), 'bottom' at 'bottom_at', 0 (the glide) at the clip's end, smoothstepped. Bone k runs on
                                   # the warped phase u^(1 + k·lag), so the outer wing LAGS the arm: at the arm's bottom the hand is
                                   # still up (the primaries flex up under load), and on the raise the hand trails below — the
                                   # momentum taper; the lag is zero at both ends so 285 is fully stretched and 294 is the glide.
                                   # 'sweep' (deg per unit of phase lag) sweeps a lagging bone BACK about the vertical axis as well.
                                   # WING_CLIP_PITCH still applies on top (the reared body: aim the stroke at the ground).
WING_HOLD = {8: 285.0}             # per CAT_KEYS index: the clip HOLDS the wings' pose of this cat frame (spine-local, root included)
                                   # over its whole range — the sit (30-40, "in place when there is no enemy") wears the float-up's
                                   # first frame: humerus raised, forearm and hand still flexed (user 2026-09-13). Keyed like the
                                   # clip windows, with folded brackets one frame outside, so the engine's fades in/out work as usual.
WING_CLIP_PITCH = {7: [(285.0, 30.0), (292.0, 15.0), (294.0, 10.0)]}
                                   # (with the authored WING_STROKE the flap is about the fore-aft axis and already aims down; the +45°
                                   # that aimed Dran's forward arc at the ground folded the authored wing flat against the flanks → +15°)
                                   # per WING_CLIPS key: (frame, deg) keys of an extra pitch of the WHOLE wing (all four bones rigidly
                                   # about the pivot) about the cat's lateral axis, positive = tips UP, smoothstepped between keys, held
                                   # outside them. Float-up: the fade from the folded ready ends on frame 285, so the first key is the
                                   # "top of the upstroke": Dran 295's stretched wing sat 58° above the spine line (user saw ~45° from
                                   # profile), user 2026-09-13: "closer to 75°, not the full 90° right away" → +30°. The stroke bottom
                                   # (Dran 298 ↔ cat 290.4, 76° below the spine axis on the BACK side) pointed at the FRONT PAWS: the
                                   # float-up rears the body ~59° nose-up, so "down relative to the spine" sweeps under the belly (world:
                                   # 44° below horizontal, forward); user: "pointed a little more at the ground" → keep the stroke plane
                                   # pitched DORSALLY through the downstroke; +25° at the bottom gave world 70° below horizontal, user:
                                   # "around 291 still too far forward" → +45° at the bottom (291 ≈ straight down), easing to a +10°
                                   # residual at 294 (the held glide's sweep points down-back instead of straight down; the 16-step
                                   # float-up → leap fade dissolves it). (−20° there aimed it at the paws even more: −25°.)
WING_CHAIN = True                  # rig wing2/3/4 as CHILDREN of the previous bone (constant local translation = the segment length)
                                   # instead of Dran's siblings-under-the-root: the engine's key-change cross-fade slerps every bone's
                                   # LOCAL rotation and lerps its translation independently, so sibling bones whose positions are 144°
                                   # apart (folded ready → wings-up float-up) pass through a chord — the upper arm collapsed to 0.59 of
                                   # its 1.93 mid-fade. A chain keeps every segment its length through any fade (2026-09-13).
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


DRAN_CHR = r'dun\monstor\c12a.chr'   # the wing donor


def wing_graft(read, packed, rep, log=print):
    """The bake's entry: run the wing graft on the WINGLESS cat pack `packed` (build_cat_pack.assemble(..., wings=False) output;
    `rep['nodes']` = (host nodes, cat nodes)) with Dran read through `read(name)`, and return build_winged_cat's raw data plus
    the cat rig it was built on."""
    pack = mc.Pack.parse(packed); nb, K = rep['nodes']
    mds = bytearray(pack.find(bcp.HOST_MDS).payload); root = 0x18 + nb * 0x70
    assert mds[root:root + 0x20].split(b'\0')[0] == bcp.CAT_ROOT_NAME.encode(), 'cat root record'
    m = list(struct.unpack_from('<16f', mds, root + 0x28))
    for r in range(3):
        for c in range(3): m[r * 4 + c] /= bcp.HIDE_SCALE
    struct.pack_into('<16f', mds, root + 0x28, *m); mds = bytes(mds)
    nodes = subtree_nodes(mds, nb, K)
    motions = [{'name': cm, 'gloss': '', 'start': s_, 'end': e, 'speed': sp, 'id': bcp.KEY_START + i, 'empty': 0}
               for i, (s_, e, sp, cm) in enumerate(bcp.CAT_KEYS)]
    dran = mc.Pack.parse(read(DRAN_CHR)); dcfg = em.find_cfg(dran)
    dmds_name, dmot_name, _ = em.parse_cfg(dcfg.payload); dmds = dran.find(dmds_name).payload; dnodes = em.read_skeleton(dmds)
    out = build_winged_cat(nodes, mds, pack, motions, dnodes, dmds, dran, dmot_name, log=log)
    out['cat_nodes'] = nodes; out['mds'] = mds; out['pack'] = pack
    return out


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
    attach = [WING_ATTACH_BIND[0], WING_ATTACH_BIND[1] + WING_RAISE, WING_ATTACH_BIND[2]]
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
    bind_open = {n['i']: (n['R'], n['T'], n['world']) for n in nodes[base:]}    # Dran's bind mapped onto the cat: the pose where every
                                                                                  # carved vertex's bone-local copies agree (the bake's bind)
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
            bid = dn[name]['i']
            vi_ = [v for v, infl in weights_all.items() if any(b == bid and w >= 20 for b, w in infl)]   # any real influence
            loc = [em.xform_pt(dran_nodes[bid]['invworld'], vw_all[v]) for v in vi_]
            if len(loc) < 4:                                                       # (r_wing3 owns almost nothing) → the sheet as a whole
                loc = [em.xform_pt(dran_nodes[bid]['invworld'], vw_all[v]) for v, infl in weights_all.items()
                       if any(dran_nodes[b]['name'].startswith(name[:2]) for b, w in infl)]
            c = [sum(p[i] for p in loc) / len(loc) for i in range(3)]
            nrm = _pca_normal(loc, c)                                                # membrane normal, bone-local (the least principal axis)
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
        if WING_CHAIN:                                                            # same bind worlds, expressed as a chain
            for k, name in enumerate(ch[1:], 1):
                n = nodes[wid[dn[name]['i']]]; pn = nodes[wid[dn[ch[k - 1]]['i']]]
                L = em.mat_mul(n['world'], em.rigid_inv(pn['world']))
                n['parent'] = pn['i']; n['R'] = [list(L[r][:3]) for r in range(3)]; n['T'] = list(L[3][:3]); n['quat'] = em.mat_to_quat(n['R'])
                assert abs(n['T'][0] - seg_len[sd][k - 1]) < 1e-3 and abs(n['T'][1]) < 1e-3 and abs(n['T'][2]) < 1e-3, (name, n['T'], seg_len[sd][k - 1])
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
        windows.append((cs, ce, ds, de, cycles, loop, ki))
        if loop: log(f"  clip {ki} ({cs}-{ce} @{cspd}): {cycles} flap cycle(s) over {win_game:.0f} game frames (Dran's own rate would give {win_game / loop_game:.2f})")
        else: log(f"  clip {ki} ({cs}-{ce} @{cspd}): Dran {ds}-{de} once over {win_game:.0f} game frames (Dran's own length {loop_game:.0f} → {loop_game / win_game:.2f}× its rate)")
    def _smooth(t):
        t = min(max(t, 0.0), 1.0); return t * t * (3 - 2 * t)
    lcs, lce = WING_LAND['cat']; lds, lde = WING_LAND['dran']; lfe = WING_LAND['flare_end']
    def rot_about(axis, deg):
        """row-vector rotation (v' = v · Q) of +deg about the unit axis (Rodrigues, transposed)"""
        x, y, z = _unit(axis); c, sn = math.cos(math.radians(deg)), math.sin(math.radians(deg)); t = 1 - c
        R = [[t * x * x + c, t * x * y - sn * z, t * x * z + sn * y],
             [t * x * y + sn * z, t * y * y + c, t * y * z - sn * x],
             [t * x * z - sn * y, t * y * z + sn * x, t * z * z + c]]
        return _t3(R)
    def stroke_axes(f, mode):
        """(flap axis, sweep axis) in spine-local: the cat's fore-aft / vertical ('spine') or the world-horizontal fore-aft / world
        vertical expressed in the spine's frame at cat frame f ('world')"""
        if mode != 'world': return [1, 0, 0], [0, 1, 0]
        Pw = cat_world(f)[parent]; rows = [Pw[r][:3] for r in range(3)]
        h = _unit([rows[0][0], 0.0, rows[0][2]]); v = [0.0, 1.0, 0.0]
        return [sum(h[i] * rows[r][i] for i in range(3)) for r in range(3)], [sum(v[i] * rows[r][i] for i in range(3)) for r in range(3)]
    def keyed_val(keys, f):
        """(frame, value) keys → value at f: held outside, smoothstepped between"""
        v = keys[0][1] if f <= keys[0][0] else keys[-1][1]
        for (fa_, va_), (fb_, vb_) in zip(keys, keys[1:]):
            if fa_ <= f <= fb_: v = va_ + (vb_ - va_) * _smooth((f - fa_) / (fb_ - fa_))
        return v
    idle_rots = {}
    flap_sign, sweep_sign = {}, {}
    for wki_, st_ in WING_STROKE.items():
        wcs_, wde_ = next((cs, de) for cs, ce, ds, de, cycles, loop, ki in windows if ki == wki_)
        fa, va = stroke_axes(wcs_, st_.get('axis', 'spine'))
        for sd, ch in SIDES.items():
            G = [dran_local_R(name, wde_) for name in ch]
            def tip_of(Rs): return fk_locals(sd, Rs)[3]
            up = tip_of([_mul3(R, rot_about(fa, 80)) for R in G])
            flap_sign[sd] = 1.0 if up[1] < tip_of(G)[1] else -1.0                  # + must take the tip UP (spine-local −y)
            back = tip_of([_mul3(R, rot_about(va, 30)) for R in G])
            sweep_sign[sd] = 1.0 if back[0] < tip_of(G)[0] else -1.0                # + must take the tip BACK (spine-local −x)
        log(f"  clip {wki_}: authored stroke from Dran {wde_} (flap {st_['phi']}, extend {st_.get('extend')}, lag {st_['lag']:g}, sweep {st_.get('sweep', 0):g}, axis {st_.get('axis', 'spine')}); flap sign r {flap_sign['r']:+.0f} l {flap_sign['l']:+.0f}, sweep sign r {sweep_sign['r']:+.0f} l {sweep_sign['l']:+.0f}")
    def wing_locals(f):
        """Spine-local (R, T) of wing1..4 per side at cat frame f: Dran's loop sample inside a WING_CLIPS window, the
        authored landing inside WING_LAND, the folded bind elsewhere. Root pinned, positions by FK."""
        for ki, src in WING_HOLD.items():
            hs, he = bcp.CAT_KEYS[ki][0], bcp.CAT_KEYS[ki][1]
            if hs <= f <= he: return wing_locals(float(src))
        out = {}
        for sd, ch in SIDES.items():
            df = None
            for cs, ce, ds, de, cycles, loop, ki in windows:
                if cs <= f <= ce:
                    u = (f - cs) / float(ce - cs); df = ds + (math.fmod(u * cycles * (de - ds), de - ds) if loop else u * (de - ds)); wki = ki
            root = None
            if df is not None:
                if wki in WING_STROKE:
                    st = WING_STROKE[wki]; wcs, wce, wde = next((cs, ce, de) for cs, ce, ds, de, cycles, loop, ki in windows if ki == wki)
                    u = (f - wcs) / float(wce - wcs)
                    fa, va = stroke_axes(f, st.get('axis', 'spine'))
                    if sd not in idle_rots:                                                   # the folded idle (with its droop/roll) —
                        idle = wing_locals(WING_FOLD_FRAME)                                   # what the ready clip holds and the fade leaves
                        idle_rots[sd] = [idle[wid[dn[name]['i']]][0] for name in ch]
                    stroke, Rls = [], []
                    if st.get('hinge_in', 0.0):                                                # the root rides on an arc about the inboard hinge
                        piv = pivot_local[sd]; hz = st['hinge_in'] * (1.0 if piv[2] < 0 else -1.0)
                        C = [piv[0], piv[1], piv[2] + hz]; arm = [piv[i] - C[i] for i in range(3)]
                        Q = rot_about(fa, keyed_val(st['phi'], f) * flap_sign[sd])
                        root = [C[i] + sum(arm[j] * Q[j][i] for j in range(3)) for i in range(3)]
                    for k, name in enumerate(ch):
                        uk = u ** (1.0 + k * st['lag']); fk = wcs + uk * (wce - wcs)              # this bone's lagged frame
                        R = _mul3(dran_local_R(name, wde), rot_about(fa, keyed_val(st['phi'], fk) * flap_sign[sd]))   # the flap, about the fore-aft axis
                        psi = st.get('sweep', 0.0) * (u - uk)
                        if abs(psi) > 1e-6: R = _mul3(R, rot_about(va, psi * sweep_sign[sd]))  # a lagging bone also sweeps back
                        stroke.append(R)
                        if k == 0 or 'extend' not in st: Rls.append(R); continue
                        e = keyed_val(st['extend'], fk)
                        rel_s = _mul3(stroke[k], _t3(stroke[k - 1])); rel_i = _mul3(idle_rots[sd][k], _t3(idle_rots[sd][k - 1]))
                        rel = rel_s if e >= 1 else rel_i if e <= 0 else _quat_to_mat(_slerp(em.mat_to_quat(rel_i), em.mat_to_quat(rel_s), e))
                        Rls.append(_mul3(rel, Rls[k - 1]))                                    # chain-local rotation on the ACTUAL parent
                else:
                    Rls = [dran_local_R(name, df) for name in ch]
                if wki in WING_CLIP_PITCH:                                                # the clip's extra whole-wing pitch (see the knob)
                    keys = WING_CLIP_PITCH[wki]; deg = keys[0][1] if f <= keys[0][0] else keys[-1][1]
                    for (fa, da), (fb, db) in zip(keys, keys[1:]):
                        if fa <= f <= fb: deg = da + (db - da) * _smooth((f - fa) / (fb - fa))
                    if abs(deg) > 1e-6:
                        th = math.radians(deg); c, sn = math.cos(th), math.sin(th)
                        Qz = [[c, sn, 0], [-sn, c, 0], [0, 0, 1]]                          # about the spine's z = the cat's lateral axis, post-multiplied
                        Rls = [_mul3(R, Qz) for R in Rls]
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
            if WING_HAND_DROOP and (lcs <= f <= lce or df is None):
                # DROOP the hand (wing3, wing4): pitch the back feathers downward about the cat's lateral axis over
                # WING_HAND_DROOP['frames'] and hold it in the idle (user 2026-09-13: "morph the entire back two feathers angled
                # further … downward, not inward towards the spine … the top feather edge in line with the top of the back").
                # A rotation in the PARENT (spine) frame is post-multiplied; the spine's local z is the cat's lateral axis.
                a, b = WING_HAND_DROOP['frames']
                s_ = 1.0 if df is None and not (lcs <= f <= lce) else _smooth((f - a) / (b - a))
                if s_ > 0:
                    Rls = list(Rls)
                    for k, deg in WING_HAND_DROOP['bones'].items():                            # a pitch of the FOREARM lowers the whole
                        th = math.radians(deg * s_); c, sn = math.cos(th), math.sin(th)          # hand (its bones keep their own
                        Qz = [[c, sn, 0], [-sn, c, 0], [0, 0, 1]]                                # orientation): the feathers move DOWN,
                        Rls[k] = _mul3(Rls[k], Qz)                                              # not inward and not tilted forward
            if WING_HAND_ROLL and (lcs <= f <= lce or df is None):
                # ROLL the hand (wing3, wing4) about its own span so the feathers above the hand line lean in toward the cat, eased
                # over WING_HAND_ROLL['frames'] and held in the idle (user 2026-09-13: "roll the top 3 feathers toward the cat in
                # the folded pose … now that the wings sit higher"). A roll about the bone's own x is PRE-multiplied; the wings'
                # local frames are mirrored, so the sign flips per side. Vanes near the hand line barely move, the top ones most.
                a, b = WING_HAND_ROLL['frames']
                s_ = 1.0 if df is None and not (lcs <= f <= lce) else _smooth((f - a) / (b - a))
                if s_ > 0:
                    sgn = 1.0 if sd == 'r' else -1.0
                    Rls = list(Rls)
                    for k, deg in WING_HAND_ROLL['bones'].items():
                        th = math.radians(deg * s_ * sgn); c, sn = math.cos(th), math.sin(th)
                        Rx = [[1, 0, 0], [0, c, sn], [0, -sn, c]]
                        Rls[k] = _mul3(Rx, Rls[k])
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
    mirror_bone = {}
    for n in cat_nodes:
        other = n['name'].replace('migi', '\0').replace('hidari', 'migi').replace('\0', 'hidari')
        if other != n['name'] and other in cn: mirror_bone[n['i']] = cn[other]['i']
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
            def snap_to_skin(q):
                """the skinning of the nearest cat_skin vertex to q (reference pose) — its exact (b0, b1, w0) — with q's offset from
                that vertex carried in each bone's frame, so q rides the surface in every pose exactly as its neighbour does (a
                triangle-sampled blend sank ≤0.6 into the back in the poses it was not fitted in). The nearest vertex to q's MIRROR
                image is also considered and taken when it leans less on an arm bone (bones mirrored back): the cat's skin is
                asymmetric, and a rim point owned by a leg swung with the leg mid-fold"""
                def nearest(pt):
                    return min(range(skin_me['nv']), key=lambda i: (skin_ref[i][0] - pt[0]) ** 2 + (skin_ref[i][1] - pt[1]) ** 2 + (skin_ref[i][2] - pt[2]) ** 2)
                def arm_share(i):
                    return sum(w for b, w in ((skin_me['b0'][i], skin_me['w0'][i]), (skin_me['b1'][i], 1 - skin_me['w0'][i])) if 'arm' in cat_nodes[b]['name'])
                j = nearest(q); jm = nearest([-q[0], q[1], q[2]])
                if arm_share(jm) < arm_share(j) - 1e-6:
                    b0_, b1_, w_ = mirror_bone.get(skin_me['b0'][jm], skin_me['b0'][jm]), mirror_bone.get(skin_me['b1'][jm], skin_me['b1'][jm]), skin_me['w0'][jm]
                    return b0_, list(em.xform_pt(em.rigid_inv(Wcat[b0_]), q)), b1_, list(em.xform_pt(em.rigid_inv(Wcat[b1_]), q)), w_
                b0_, b1_, w_ = skin_me['b0'][j], skin_me['b1'][j], skin_me['w0'][j]
                off = [q[i] - skin_ref[j][i] for i in range(3)]
                def carried(b, pl):
                    R = Wcat[b]                                                          # rows = the bone's axes in world → v_local = v · R^T
                    return [pl[i] + sum(off[k] * R[i][k] for k in range(3)) for i in range(3)]
                return b0_, carried(b0_, skin_me['p0'][j]), b1_, carried(b1_, skin_me['p1'][j]), w_
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
                # the whole intersection — the ring where the wing's top enters the back — is held to the body (user 2026-09-13:
                # "stabilize the entire intersection where the tops of the wings enter the back with the wings open"): each ring
                # vertex (+Rt) copies the nearest skin vertex's exact skinning with its offset carried, so it sits where it sits in
                # every pose; the base triangles and tabs then never span a moving and a fixed corner (the notch in the fold)
                anchored = []
                for vi in list(chain) + (rt[:1] if rt else []):
                    li = remap[vi]; q = wpos(li)
                    if WING_ROOT_ANCHOR_X is not None and abs(q[0]) >= WING_ROOT_ANCHOR_X: continue
                    ba, pa, bb, pb, wa = snap_to_skin(q)
                    me['b0'][li], me['p0'][li], me['b1'][li], me['p1'][li], me['w0'][li] = ba, pa, bb, pb, wa
                    anchored.append(f"({q[0]:.2f}, {q[2]:.2f})")
                log(f"  {sd} wing root anchored: {len(anchored)} ring verts held to the back: {', '.join(anchored)}")
            if WING_APEX_PULL:
                pull, reach = WING_APEX_PULL; pulled = []
                for vi in used:
                    li = remap[vi]; q = wpos(li); x = abs(q[0])
                    if x >= reach: continue
                    d = pull * (reach - x) / reach
                    q2 = [q[0] + (-d if sd == 'r' else d), q[1], q[2]]                     # toward this wing's root, along x
                    for slot in ('0', '1'):
                        b = me['b' + slot][li]; M = Wref[b] if b in Wref else Wcat[b]
                        me['p' + slot][li] = list(em.xform_pt(em.rigid_inv(M), q2))
                    pulled.append(f"v{li} {d:.2f}")
                log(f"  {sd} wing fan tip pulled toward the root: {', '.join(pulled)}")
            if WING_ROOT_FORWARD:
                # the fan's BACK EDGE (the rear-most root vertices) is moved forward in the mesh — the offset is taken as "the cat's
                # forward" in the FOLDED pose and carried in each bone's frame — so it stops cutting into the folded wing behind it
                # (user 2026-09-13: "move the back edge of these polys forward a little bit")
                fwd, zmax, xmax, xfull = WING_ROOT_FORWARD; moved = []
                W_fold = wing_pose(WING_FOLD_FRAME)[0]; Wc_fold = cat_world(WING_FOLD_FRAME)
                for vi in used:
                    li = remap[vi]; q = wpos(li); x = abs(q[0])
                    if q[2] >= zmax or x >= xmax: continue
                    f_ = fwd * min(1.0, (xmax - x) / (xmax - xfull))                      # full shift inboard of xfull, fading to 0 at xmax
                    for slot in ('0', '1'):                                               # (user 2026-09-13: the outer rear verts came
                        b = me['b' + slot][li]; M = W_fold[b] if b in W_fold else Wc_fold[b]   # too far forward → a gap behind them)
                        R = M[:3]                                                         # rows = the bone's axes in world (fold pose)
                        dl = [f_ * R[i][2] for i in range(3)]                             # world +z → bone-local (v · R^T)
                        me['p' + slot][li] = [me['p' + slot][li][i] + dl[i] for i in range(3)]
                    moved.append(f"v{li} {f_:.2f}")
                log(f"  {sd} wing fan back edge moved forward: {', '.join(moved)}")
            if WING_FOLD_LIFT:
                # LIFT specific wing vertices in the FOLDED pose only (user 2026-09-13: "allow the top edge of these polys to stay
                # higher so they don't create such a deep V when folded"): the deep V is the linear-blend collapse at the elbow —
                # a 50/50 wing1/wing2 vertex lands on the chord between its two rigid images. A two-pose fit per vertex keeps the
                # leap position exact and raises the folded one; keys = (side-independent) vertex index in the wing mesh
                W_fold = wing_pose(WING_FOLD_FRAME)[0]; Wc_fold = cat_world(WING_FOLD_FRAME)
                lifted = []
                for li, d in WING_FOLD_LIFT.items():
                    if li >= len(used): continue
                    b0_, b1_, w_ = me['b0'][li], me['b1'][li], me['w0'][li]
                    MA = (_col(b0_, Wref, Wcat), _col(b1_, Wref, Wcat)); MB = (_col(b0_, W_fold, Wc_fold), _col(b1_, W_fold, Wc_fold))
                    tgtA = _blend(w_, MA, me['p0'][li], me['p1'][li]); tgtB = _blend(w_, MB, me['p0'][li], me['p1'][li]); tgtB[1] += d
                    sol = _two_pose_fit(w_, MA, MB, tgtA, tgtB)
                    if sol:
                        me['p0'][li], me['p1'][li] = sol
                        lifted.append(f"v{li} +{d:g}")
                log(f"  {sd} wing verts lifted in the fold: {', '.join(lifted)}")
            if WING_ROOT_LOWER:
                drop, reach = WING_ROOT_LOWER
                W_fold = wing_pose(WING_FOLD_FRAME)[0]; Wc_fold = cat_world(WING_FOLD_FRAME)
                lowered = []
                for vi in used:
                    li = remap[vi]; q = wpos(li); x = abs(q[0])
                    if x >= reach: continue
                    d = drop * (reach - x) / reach
                    b0_, b1_, w_ = me['b0'][li], me['b1'][li], me['w0'][li]
                    MBold = (_col(b0_, W_fold, Wc_fold), _col(b1_, W_fold, Wc_fold))
                    tgtA = list(q); tgtB = _blend(w_, MBold, me['p0'][li], me['p1'][li]); tgtB[1] -= d
                    # re-skin as wing bone + the spine (the wings' parent), half each, and solve both poses at once
                    nb0, nb1, nw = b0_, sb2, 0.5
                    MA = (_col(nb0, Wref, Wcat), _col(nb1, Wref, Wcat)); MB = (_col(nb0, W_fold, Wc_fold), _col(nb1, W_fold, Wc_fold))
                    sol = _two_pose_fit(nw, MA, MB, tgtA, tgtB)
                    if sol:
                        me['b0'][li], me['b1'][li], me['w0'][li] = nb0, nb1, nw
                        me['p0'][li], me['p1'][li] = sol
                        lowered.append(f"v{li} −{d:.2f}")
                log(f"  {sd} wing root apex lowered in the fold: {', '.join(lowered)}")
            if WING_ROOT_SOFTEN:
                # SOFTEN the inboard fan's apex (user 2026-09-13: the polys around the midline vertex "spike upward during the
                # fold"): the membrane that lay flat on Dran's back is rigid with the wing, so when the humerus folds down its
                # inboard tip points up. The midline vertex gets a modest share of the body (its dominant skin bone beneath, via
                # snap_to_skin), fading to nothing by |x| = WING_ROOT_SOFTEN[1] — the apex drops toward the back without the
                # full anchoring that tore the base. Slot 0 keeps the vertex's own wing bone.
                share, reach = WING_ROOT_SOFTEN; soft = []
                for vi in used:
                    li = remap[vi]; q = wpos(li); x = abs(q[0])
                    if x >= reach: continue
                    w_b = share * (reach - x) / reach
                    ba, pa, bb, pb, wa = snap_to_skin(q)
                    me['b1'][li], me['p1'][li] = ba, pa; me['w0'][li] = 1.0 - w_b
                    soft.append(f"v{li} {w_b:.0%}")
                log(f"  {sd} wing root softened: {', '.join(soft)}")
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
            if WING_TAB_CAP:
                # close the base as a CLOSED FORM by ZIPPING the ring's upper rim to its lower rim (user 2026-09-13: a lid fanned
                # from a centre vertex made flat facets that stuck out when folded; the closure must taper toward the base like
                # the membrane does). Both rims run Rb → … → Rf; walk them together, always closing the shorter diagonal, so the
                # closure is the thin wedge between the membrane's top and bottom surfaces and the top surface stays smooth.
                upper = [remap[v] for v in chain[:fi + 1]]                                # Rb, Ri, (D, E), Rf
                lower = [remap[chain[0]]] + [remap[v] for v in reversed(chain[fi + 1:])] + [remap[chain[fi]]]   # Rb, (A, Rg), Rf
                i_, j_ = 0, 0; zipped = 0
                while i_ < len(upper) - 1 or j_ < len(lower) - 1:
                    if i_ == len(upper) - 1: adv_upper = False
                    elif j_ == len(lower) - 1: adv_upper = True
                    else:
                        du = math.dist(wp_any(upper[i_ + 1]), wp_any(lower[j_])); dl = math.dist(wp_any(upper[i_]), wp_any(lower[j_ + 1]))
                        adv_upper = du <= dl
                    if adv_upper:
                        if upper[i_ + 1] != lower[j_]: polys.append((upper[i_], upper[i_ + 1], lower[j_])); zipped += 1
                        i_ += 1
                    else:
                        if lower[j_ + 1] != upper[i_]: polys.append((upper[i_], lower[j_ + 1], lower[j_])); zipped += 1
                        j_ += 1
                log(f"  {sd} wing base closed: upper rim {len(upper)} verts zipped to lower rim {len(lower)} verts → {zipped} tris")
            tab_polys = []
            for poly in WING_TAB_POLYS:
                if poly[0] == 'RIM':
                    rim = [remap[v] for v in top]                                        # Ri … Rf along the upper rim
                    tab_polys += [(a_, b_, pts[poly[1]]) for a_, b_ in zip(rim, rim[1:])]
                else:
                    tab_polys.append(tuple(pts[p] for p in poly[:3]) + tuple(poly[3:]))
            coarse = [t[:3] for t in tab_polys]
            tabs = [t for t, poly in zip(coarse, tab_polys) if len(poly) == 3]         # 'under' polys are inside the body by design
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
                    y_rim = y0 + (y1 - y0) * t - WING_RAISE                               # the rim's height beside this corner, before
                    pos[1] = y_rim - abs(pos[0] - hx) * math.tan(th)                      # lower toward the spine by the dihedral
                    me['p0'][i] = list(em.xform_pt(em.rigid_inv(Wcat[me['b0'][i]]), pos)); me['p1'][i] = list(em.xform_pt(em.rigid_inv(Wcat[me['b1'][i]]), pos))
            # ── subdivide the top tabs (user 2026-09-13: "subdivide these so you can fold the wings while keeping the intersection
            # into the back stable and not clipping"): each tab → n² triangles; new points on a wing–wing edge stay wing-skinned
            # (their ends' influences pooled), every other new point is laid on the skin (+lift, never below the tab's own plane)
            # and weighted like the skin under it — so the patch hugs the back in every pose and only the one ring-side row
            # stretches to the wing as it folds (the hinge = the stable intersection) ──
            n = WING_TAB_SUBDIV; wing_vert = lambda i: i < len(used) or i == pts.get('Rm'); ch_root = SIDES[sd][0]
            mixed = []                                                                   # pooled points between the wing and the body
            P_anchor = cat_world(WING_TAB_ANCHOR_FRAME)[parent] if WING_TAB_ANCHOR_FRAME is not None else None
            cache = {}
            def sub_vertex(key, pos, ends):
                if key in cache: return cache[key]
                if WING_TAB_SUBDIV_POOL or all(wing_vert(e) for e, _ in ends):           # pool the ends (always on a wing–wing edge)
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
                    if any(wing_vert(e) for e, _ in ends) and not all(wing_vert(e) for e, _ in ends): mixed.append(i)
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
            work = list(zip(coarse, tab_polys))
            for tri, poly in work:
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
            if WING_TAB_SUBDIV_NEIGHBOURS and n == 2:
                # CONFORMING split of everything that shares an edge with a split tab — the base triangles, the lid, the 'under'
                # poly, any wing poly — on exactly those edges (1 → 2 tris, 2 → 3, 3 → 4), so no edge is left with a midpoint
                # on one side only (user 2026-09-13: thin gaps along the fore edge of the base opened and inverted as the bulged
                # midpoints moved off the neighbour's straight edge). Nothing new is created, so it never cascades.
                def mid_of(a_, b_):
                    return cache.get(('e', min(a_, b_), max(a_, b_), 1))
                def conform(t):
                    a_, b_, c_ = t; m = [mid_of(a_, b_), mid_of(b_, c_), mid_of(c_, a_)]
                    k = sum(x is not None for x in m)
                    if k == 0: return [t]
                    if k == 3: return [(a_, m[0], m[2]), (m[0], b_, m[1]), (m[2], m[1], c_), (m[0], m[1], m[2])]
                    # rotate so the split edge(s) start at index 0
                    while m[0] is None or (k == 2 and m[1] is None):
                        a_, b_, c_ = b_, c_, a_; m = m[1:] + m[:1]
                    if k == 1: return [(a_, m[0], c_), (m[0], b_, c_)]
                    return [(m[0], b_, m[1]), (a_, m[0], m[1]), (a_, m[1], c_)]                # k == 2: edges ab and bc
                before = len(me['tris']) + len(polys)
                me['tris'] = [u for t in me['tris'] for u in conform(t)]
                polys = [u for t in polys for u in conform(t)]
                log(f"  {sd} wing tabs: conforming split of the neighbours: {len(me['tris']) + len(polys) - before} extra tris")
            if WING_TAB_FLOOR and WING_TAB_CAP and 'ir' in pts and 'if' in pts:
                floor = [(pts['ir'], lower[k], lower[k + 1]) for k in range(len(lower) - 2)] + [(pts['ir'], lower[-2], pts['if'])]
                if WING_TAB_SUBDIV_NEIGHBOURS and n == 2: floor = [u for t in floor for u in conform(t)]   # meet the split edges
                polys += floor
                log(f"  {sd} wing base floored: {len(floor)} tris from ir over the lower rim ({len(lower) - 1} verts) to if")
            polys += fine; tabs = fine
            if WING_TAB_BULGE and mixed:
                # ROUND THE FOLD (user 2026-09-13: "take advantage of the subdivision to make the folded pose smoother"): a pooled
                # midpoint sits on the straight chord between its wing end and its body end, so the folded tab was two flat planes
                # meeting at a crease. Each mixed point now gets bone-local positions solved from TWO targets: its reference-pose
                # spot (the open wing is untouched) and, in the folded idle pose, that chord point pushed WING_TAB_BULGE out along
                # the shoulder's normal — a 6×6 linear system per point (2 poses × 3 coords, unknowns = p0 and p1)
                W_fold = wing_pose(WING_FOLD_FRAME)[0]; Wc_fold = cat_world(WING_FOLD_FRAME)
                nrm = _unit([0.6 * (1.0 if sd == 'l' else -1.0), 0.8, 0.0])
                for i in mixed:
                    b0_, b1_, w_ = me['b0'][i], me['b1'][i], me['w0'][i]
                    MA = (_col(b0_, Wref, Wcat), _col(b1_, Wref, Wcat))                         # pose A: the leap reference
                    MB = (_col(b0_, W_fold, Wc_fold), _col(b1_, W_fold, Wc_fold))               # pose B: folded (stand)
                    tgtA = _blend(w_, MA, me['p0'][i], me['p1'][i])                             # where it is now, both poses
                    tgtB = [c + WING_TAB_BULGE * n_ for c, n_ in zip(_blend(w_, MB, me['p0'][i], me['p1'][i]), nrm)]
                    # the system is singular by construction: a push along the fold's own rotation axis can't be produced by
                    # bone-local offsets, so take the minimum-norm least-squares solution (the achievable part of the push) and
                    # keep the old locals if it still explodes
                    sol = _two_pose_fit(w_, MA, MB, tgtA, tgtB)
                    if sol: me['p0'][i], me['p1'][i] = sol
                log(f"  {sd} wing tabs: {len(mixed)} wing↔body midpoints rounded by {WING_TAB_BULGE:g} along the shoulder in the folded pose (exact in the leap)")
            me['tris'] += polys
            w_ = clearance(); c = w_[0] if w_ else float('nan')
            corner_log = [f"{nm} ({cpos[i][0]:.2f}, {cpos[i][1]:.2f}, {cpos[i][2]:.2f}{' ↑%.2f' % raised[i] if i in raised else ''})" for nm, i in pts.items() if i in cpos]
            log(f"  {sd} wing tabs: ring {ring_log}")
            log(f"  {sd} wing tabs: corners {', '.join(corner_log)}; cap {len(polys) - len(fine) - sum(1 for q in tab_polys if len(q) > 3)} + tabs {len(tabs)} subdivided ×{n} → {len(fine)} tris (+ under) = {len(polys)} tris, {len(cache)} new verts; dihedral {WING_TAB_TILT_DEG}°; min clearance over the skin {c:+.2f}")
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
    spans = [(cs, ce) for cs, ce, ds, de, cycles, loop, ki in windows] + [WING_LAND['cat']] + [bcp.CAT_KEYS[ki][:2] for ki in WING_HOLD]
    keyed = sorted({f for cs, ce in spans for f in range(cs, ce + 1)})
    frames_all = sorted(set(keyed) | {f for cs, ce in spans for f in (cs - 1, ce + 1)})   # bind brackets where nothing else keys
    per_frame = {f: wing_locals(f) for f in frames_all}
    if WING_CHAIN:                                                                # spine-local (R, T) per bone → parent-bone-local
        for f in frames_all:
            loc = per_frame[f]
            for sd, ch in SIDES.items():
                ids = [wid[dn[name]['i']] for name in ch]
                Ms = [em.mat_from_rt(*loc[i]) for i in ids]
                for k in range(1, 4):
                    L = em.mat_mul(Ms[k], em.rigid_inv(Ms[k - 1]))
                    dev = max(abs(L[3][0] - seg_len[sd][k - 1]), abs(L[3][1]), abs(L[3][2]))
                    assert dev < 1e-3, (f, ch[k], L[3][:3], seg_len[sd][k - 1])
                    loc[ids[k]] = ([list(L[r][:3]) for r in range(3)], nodes[ids[k]]['T'])   # translation = the bind's, constant
    for k, name in enumerate(WING_BONES):
        nid = base + k
        tracks.append({'node': nid, 'chan': 0, 'frames': frames_all, 'vals': [list(em.mat_to_quat(per_frame[f][nid][0])) for f in frames_all]})
        if not (WING_CHAIN and nodes[nid]['parent'] != parent):                  # a chained bone's translation never changes
            tracks.append({'node': nid, 'chan': 2, 'frames': frames_all, 'vals': [list(per_frame[f][nid][1]) + [0.0] for f in frames_all]})
    log(f"  wing tracks: keys at {frames_all[0]}..{frames_all[-1]} ({len(frames_all)} frames); landing {lcs}-{lce}: Dran {lds}-{lde} flare to {lfe}, then fold (lag {WING_LAND['lag']}); folded = bind")
    return {'nodes': nodes, 'meshes': meshes, 'tracks': tracks, 'base': base, 'parent': parent, 'wing_sets': wing_sets, 'om': om,
            'frames_all': frames_all, 'ref_frame': ref_frame, 'Wref': Wref, 'Wcat': Wcat, 'bind_open': bind_open}
