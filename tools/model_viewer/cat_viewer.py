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
        jmeshes.append({'node': m['node'], 'skin': 1 if m['skin'] else 0, 'nv': m['nv'], 'nt': len(m['tris']),
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
WING_LIFT = 0.3                    # extra height on the attach point (units, after scaling) — user 2026-09-12
WING_FORWARD = 0.7                 # attach point moved along the cat's forward axis (+z, toward cat_kao): toward the shoulders (user 2026-09-12: 1.0, then back 0.3)
WING_YAW = 0.0                     # radians about the cat's up axis, if Dran's wing orientation needs turning
# Closing the gap between the wing base and the back (user 2026-09-12: solid-colour wings, so the mesh may be extended; moving
# wing vertices spoiled the other poses, so the fix is ADDED geometry): a SOCKET STRIP. The wing's true attachment line is the
# boundary of the carved membrane where it met Dran's body; a copy of that line is placed inside the cat's torso and weighted
# to the spine bone (it never flaps), and a strip of triangles joins the two — stretched when the wing is level, tucked away
# when it is raised. Cat units, judged in the leap pose (the reference frame):
WING_SOCKET_DROP = 1.3             # the torso centre line the copies are pushed toward sits this far below the attach point
WING_SOCKET_SINK = 1.2             # how far each copy moves toward it (capped at the line itself)
# which Dran clip drives which cat clip: cat KEY index → (Dran start, Dran end, Dran speed, loop?)
WING_CLIPS = {4: (200, 205, 0.2, True)}   # cat 'leap' (the fall, 205-214 @0.5) ← Dran motion 3 "charge loop" (user 2026-09-12)
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
    attach = list(cn[WING_ATTACH_NODE]['worldpos']); attach[1] += WING_LIFT; attach[2] += WING_FORWARD
    log(f"wing graft: cat body {body_len:.1f} long × {body_h:.1f} high; Dran wing {wing_len:.1f} → scale {S:.3f} (wing ≈ {wing_len * S:.1f}); attach {WING_ATTACH_NODE} at ({attach[0]:.2f}, {attach[1]:.2f}, {attach[2]:.2f}); roots {14.8 * S:.1f} apart")
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
    anchor_local = list(em.xform_pt(root_inv, attach))   # the attach point in the spine bone's frame (constant: rides the back)
    log(f"  wings rigid to {WING_ATTACH_NODE}; Dran's orientation taken as-is in the {bcp.CAT_KEYS[WING_LEVEL_AT][3]!r} pose (frame {ref_frame}); spine pitch there vs bind: row0 {[round(c, 2) for c in R_ref[0]]} vs {[round(c, 2) for c in nodes[parent]['world'][0][:3]]}")
    def local_of(Rw, Td):
        """Dran's (world orientation, Dran-root-local position) → this bone's transform in the spine bone's frame, such that
        in the reference pose it reads exactly as Dran's world pose anchored on the back."""
        rel = [(Td[i] - center[i]) for i in range(3)]
        rel = em.xform_pt([list(droot_R[0]) + [0], list(droot_R[1]) + [0], list(droot_R[2]) + [0], [0, 0, 0, 1]], rel)
        rel = [c * S for c in _rot_y(rel, WING_YAW)]
        Rl = _mul3(Rw, _t3(R_ref))
        Tl = [anchor_local[i] + sum(rel[k] * _t3(R_ref)[k][i] for k in range(3)) for i in range(3)]
        return Rl, Tl
    droot_R = dran_nodes[0]['R']                         # Dran-root-local → Dran-world rotation
    yawR = [[math.cos(WING_YAW), 0, math.sin(WING_YAW)], [0, 1, 0], [-math.sin(WING_YAW), 0, math.cos(WING_YAW)]]
    def world_R(R_dran_local):                           # a Dran-root-local rotation as a cat-world rotation
        return _mul3(_mul3(R_dran_local, droot_R), yawR)
    def world_T(T_dran_local):                           # a Dran-root-local position → cat-world position on the cat
        rel = [T_dran_local[i] - center[i] for i in range(3)]
        rel = em.xform_pt([list(droot_R[0]) + [0], list(droot_R[1]) + [0], list(droot_R[2]) + [0], [0, 0, 0, 1]], rel)
        rel = _rot_y(rel, WING_YAW)
        return [attach[i] + rel[i] * S for i in range(3)]
    def to_local(Rw, Tw):                                # cat-world (R, T) → attach-bone-local (R, T), bind pose
        W = em.mat_from_rt(Rw, Tw); L = em.mat_mul(W, root_inv)
        return [L[r][:3] for r in range(3)], list(L[3][:3])
    base = len(nodes); wid = {}
    for k, name in enumerate(WING_BONES):
        d = dn[name]
        R, T = local_of(world_R(d['R']), d['T'])
        n = {'i': base + k, 'name': name, 'meshoff': 0, 'parent': parent, 'R': R, 'T': T, 'quat': em.mat_to_quat(R)}
        L = em.mat_from_rt(R, T); n['world'] = em.mat_mul(L, nodes[parent]['world']); n['invworld'] = em.rigid_inv(n['world'])
        n['worldpos'] = (n['world'][3][0], n['world'][3][1], n['world'][3][2])
        nodes.append(n); wid[d['i']] = base + k
    log(f"  wing roots (cat world): r {tuple(round(c, 2) for c in nodes[wid[dn['r_wing1']['i']]]['worldpos'])}, l {tuple(round(c, 2) for c in nodes[wid[dn['l_wing1']['i']]]['worldpos'])}; tips r {tuple(round(c, 2) for c in nodes[wid[dn['r_wing4']['i']]]['worldpos'])}")
    # ── wing meshes carved from obj1 by weight ──
    weights = em.load_weights(dran_pack, 'c12a.wgt')[dn['obj1']['i']]
    obj1 = dn['obj1']; om = parse_mdt(dran_mds, obj1['meshoff'])
    vw = [em.xform_pt(obj1['world'], v[:3]) for v in om.pos]
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
            pa = [c * S for c in em.xform_pt(dran_nodes[ba]['invworld'], vw[vi])]
            pb = [c * S for c in em.xform_pt(dran_nodes[bb]['invworld'], vw[vi])]
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
    dtracks = {(t['node'], t['chan']): t for t in em.build_tracks(dran_pack, dmot_name, len(dran_nodes))}
    windows = []
    for ki, (ds, de, dspd, loop) in sorted(WING_CLIPS.items()):
        cs, ce, cspd, _ = bcp.CAT_KEYS[ki]
        win_game = (ce - cs) / cspd; loop_game = (de - ds) / dspd
        cycles = max(1, round(win_game / loop_game)) if loop else 1
        windows.append((cs, ce, ds, de, cycles, loop))
        log(f"  clip {ki} ({cs}-{ce} @{cspd}): {cycles} flap cycle(s) over {win_game:.0f} game frames (Dran's own rate would give {win_game / loop_game:.2f})")
    def wing_pose(f):
        """Cat-world matrices of the 8 wing bones at cat frame f (Dran's bind, or the driven clip's sample) and the anchor."""
        df = None
        for cs, ce, ds, de, cycles, loop in windows:
            if cs <= f <= ce:
                u = (f - cs) / float(ce - cs); df = ds + (math.fmod(u * cycles * (de - ds), de - ds) if loop else u * (de - ds))
        Pw = cat_world(f)[parent]
        out = {}
        for k, name in enumerate(WING_BONES):
            d = dn[name]
            rsrc, tsrc = dtracks.get((d['i'], 0)), dtracks.get((d['i'], 2))
            Rw = world_R(_quat_to_mat(_sample(rsrc, df))) if (df is not None and rsrc) else world_R(d['R'])
            Td = _sample(tsrc, df)[:3] if (df is not None and tsrc) else d['T']
            Rl, Tl = local_of(Rw, Td)
            out[base + k] = em.mat_mul(em.mat_from_rt(Rl, Tl), Pw)
        return out, em.xform_pt(Pw, anchor_local)
    Wref, anchor_ref = wing_pose(ref_frame)
    Pref_inv = em.rigid_inv(cat_world(ref_frame)[parent])
    from collections import Counter, defaultdict
    def edges_of(t):
        return (frozenset((t[0], t[1])), frozenset((t[1], t[2])), frozenset((t[2], t[0])))
    full_edges = Counter(e for t in tris for e in edges_of(t))
    for me, sd in zip(meshes[:2], ('r', 'l')):
        wt, used, remap = wing_sets[sd]
        wedges = Counter(e for t in wt for e in edges_of(t))
        attach = [e for e, c in wedges.items() if c == 1 and full_edges[e] >= 2]   # wing-boundary edges the body shares = the attachment line
        adj = defaultdict(list)
        for e in attach:
            va, vb = tuple(e); adj[va].append(vb); adj[vb].append(va)
        chains, seen = [], set()
        for start in sorted(adj, key=lambda v: (len(adj[v]), v)):                  # endpoints first
            if start in seen: continue
            chain, cur, prev = [start], start, None; seen.add(start)
            while True:
                nxt = [v for v in adj[cur] if v != prev and v not in seen]
                if not nxt: break
                prev, cur = cur, nxt[0]; chain.append(cur); seen.add(cur)
            chains.append(chain)
        def wpos(li):                                                               # a wing vertex in the leap pose (world)
            pa = em.xform_pt(Wref[me['b0'][li]], me['p0'][li]); pb = em.xform_pt(Wref[me['b1'][li]], me['p1'][li]); wa = me['w0'][li]
            return [pa[c] * wa + pb[c] * (1 - wa) for c in range(3)]
        copy_of = {}
        for chain in chains:
            for vi in chain:
                v = wpos(remap[vi])
                target = (0.0, anchor_ref[1] - WING_SOCKET_DROP, v[2])
                dvec = [t - c for t, c in zip(target, v)]; dl = math.sqrt(sum(c * c for c in dvec)) or 1.0
                pos = [c + d * min(1.0, WING_SOCKET_SINK / dl) for c, d in zip(v, dvec)]
                pl = list(em.xform_pt(Pref_inv, pos))                                 # fixed to the spine bone
                copy_of[vi] = me['nv']; me['nv'] += 1
                me['b0'].append(parent); me['p0'].append(pl); me['b1'].append(parent); me['p1'].append(pl); me['w0'].append(1.0)
        added = 0
        for chain in chains:
            for va, vb in zip(chain, chain[1:]):
                A, B, A2, B2 = remap[va], remap[vb], copy_of[va], copy_of[vb]
                me['tris'] += [(A, B, B2), (A, B2, A2), (B, A, B2), (B2, A, A2)]     # both windings (the viewer is single-sided)
                added += 4
        log(f"  {sd} wing socket: attachment line {len(attach)} edges in {len(chains)} chain(s) ({sum(len(c) for c in chains)} verts) → {len(copy_of)} spine-fixed copies, {added} strip tris")
    for k, name in enumerate(WING_BONES):
        d = dn[name]; nid = base + k
        rsrc, tsrc = dtracks.get((d['i'], 0)), dtracks.get((d['i'], 2))
        bq, bt = nodes[nid]['quat'], nodes[nid]['T']
        rf, rv, tf, tv = [], [], [], []
        for cs, ce, ds, de, cycles, loop in windows:
            rf.append(cs - 1); rv.append(list(bq)); tf.append(cs - 1); tv.append(list(bt) + [0.0])
            for f in range(cs, ce + 1):
                u = (f - cs) / float(ce - cs)
                df = ds + (math.fmod(u * cycles * (de - ds), de - ds) if loop else u * (de - ds))
                Rw = world_R(_quat_to_mat(_sample(rsrc, df))) if rsrc else world_R(d['R'])
                Td = _sample(tsrc, df)[:3] if tsrc else d['T']
                Rl, Tl = local_of(Rw, Td)
                rf.append(f); rv.append(list(em.mat_to_quat(Rl))); tf.append(f); tv.append(list(Tl) + [0.0])
            rf.append(ce + 1); rv.append(list(bq)); tf.append(ce + 1); tv.append(list(bt) + [0.0])
        tracks.append({'node': nid, 'chan': 0, 'frames': rf, 'vals': rv})
        tracks.append({'node': nid, 'chan': 2, 'frames': tf, 'vals': tv})
    return serialize("Divine Beast cat + Dran's wings — fall = Dran charge loop (experiment)", 'c04b+cat+wings', 'viewer experiment (nothing baked)',
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
