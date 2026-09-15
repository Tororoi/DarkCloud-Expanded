"""Bake the wing graft (tools/lib/cat_wings.py, build_winged_cat) into the cat pack as real disc data.

The viewer authors the wings in its own skinning model (per-bone positions p0/p1 per vertex, some of them fitted to TWO
poses at once). The engine's skinner (MotionProc2, docs/custom-fish-pipeline.md §3) is plain linear blend skinning: one
position per vertex in the mesh node's space, per-bone weights from the .wgt, posed world × inverse bind. The BIND pose
chosen here is Dran's own bind mapped onto the cat (the open wing): there every carved membrane vertex's bone-local copies
agree, so the engine reproduces the viewer EXACTLY in every pose for those. A vertex whose copies disagree there (the
two-pose-fitted tab midpoints and fold lifts, the pooled base verts) is placed where the viewer puts it in the FOLDED
idle — the pose the player sees most — and the open poses get LBS's version (cat_viewer's "as baked" view measures it).

What this produces, all cat-relative (the cat root is node 0 of the copy the runtime plays):
  nodes  – 8 wing bones as a CHAIN under cat_sebone2 (wing2..4 children of the previous bone: the engine's key-change
           cross-fade slerps LOCAL rotations, so a chain keeps every segment its length), bind = the open pose, then two
           mesh nodes under the cat root with identity locals (mesh space = cat-root space, like cat_skin);
  mdts   – one indexed triangle-list MDT per wing: positions = the open-bind world positions, Dran's own UV / NORM
           entries per vertex (a flat white texture makes the UVs moot, the NORM block's meaning is the engine's), one
           material naming the flat white texture;
  wgt    – one .wgt RUN per mesh: the reset entry (bone = the mesh node's parent, no keys) then one track per bone in
           ascending index with (vertex, percent) keys summing to 100 on every vertex;
  mot    – the wing bones' rotation (chan 0) tracks and wing1's translation (chan 2) over the authored windows;
  bbp    – the 10 new nodes' local bind matrices (the .bbp rows ARE the .mds locals on this rig).
"""
import math
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
for sub in ('lib',):
    p = os.path.join(HERE, '..', sub)
    if p not in sys.path:
        sys.path.insert(0, p)
import mot_codec as mc                       # noqa: E402
import extract_model as em                   # noqa: E402
from mdt_codec import Mdt, build_mdt, parse_mdt   # noqa: E402

BONE_NAMES = ['cat_rwing1', 'cat_rwing2', 'cat_rwing3', 'cat_rwing4', 'cat_lwing1', 'cat_lwing2', 'cat_lwing3', 'cat_lwing4']
MESH_NAMES = {'r': 'cat_rwingm', 'l': 'cat_lwingm'}   # no "__" suffix: no backface cull → the membrane draws from both sides (as Dran's)
MASK_NODE = 'cat_mask'                                  # the Super Steve cat's domino mask: one rigid mesh node, weighted whole
MASK_TEX = 'catcape'                                    # to cat_kao, so it rides the head with no skinning of its own. It
                                                        # SHARES the cape's texture (CAPE_TEX) rather than carrying its own:
                                                        # the two are the same red, and a mesh's texture is resolved by NAME
                                                        # when the pack loads and baked into its VU packet — nothing looks it
                                                        # up again at draw time (DrawVu1 makes no texture call at all). A
                                                        # private 'catmask' entry never came through: the mask drew untextured
                                                        # and so took the ambient straight, which is what made it pink instead
                                                        # of red. catcape is the one name proven to resolve (2026-09-15).
WING_TEX = 'catwing'                                    # the flat white texture (build_cat_pack bakes it)
CAPE_NODE = 'cat_cape'                                  # the cloth's FRAME node: its MDT is the rest lattice (engine order, see below)
CAPE_TEX = 'catcape'                                    # the flat yellow texture
CAPE_CLO = 'catcape.clo'                                # the cloth definition record (CommandCLOTH → InitCloth)
# The .clo grammar (RE'd 2026-09-13, Step__6CCloth 0x13b8a0 / Initialize 0x13d050 / CreateVUData 0x13c5f0). The physics numbers
# come from cat_wings.CAPE_PHYSICS (which documents why this cape carries no gravity): SIZE outer, inner
# (1..16 each; outer → CCloth+0x2C = the WIDTH, inner → +0x30 = the HANG). Particle (a, b) = MDT vertex a*inner + b, slot a*16 + b;
# the engine pins every (a, 0) to LW(anchor) × rest, so inner index 0 is the collar edge and the rest hangs along b; the draw is
# one triangle strip per outer pair (a, a+1) and POLYDIV holds outer-1 chars ('1' flips that strip's winding).
CAPE_CLO_TEXT = ('SIZE\t{outer},\t{inner}\r\nFRAME\t"{node}"\r\nWINDEFFECT\t{wind:.6f}\r\nNORMAL\t{normal:.6f}\r\n'
                 'GRAVITY\t{gx:.6f},\t{gy:.6f},\t{gz:.6f}\r\nFOLLOW\t{fx:.6f},\t{fy:.6f},\t{fz:.6f}\r\n'
                 'K\t{kx:.6f},\t{ky:.6f},\t{kz:.6f}\r\nPOLYDIV\t"{polydiv}"\r\n')
# BOUND "frame" / up / A / B / radii / damp (CommandBOUND 0x13fdb0 → SetDir mode 1 on that frame; vanilla poncho1.clo layout). The
# frame must resolve in HER tree at load, so every bound names the cape node itself and DivineBeastCat.SpawnCape re-points each
# CBound's frame (+0xE4) to the cat bone of the SAME INDEX in cat_wings.CAPE_BOUNDS (keep CapeBoundBones in that order).
CAPE_BOUND_TEXT = ('BOUND\t"{node}"\r\n\t{ux:.6f},\t{uy:.6f},\t{uz:.6f}\r\n\t{ax:.6f},\t{ay:.6f},\t{az:.6f}\r\n'
                   '\t{bx:.6f},\t{by:.6f},\t{bz:.6f}\r\n\t{rx:.6f},\t{ry:.6f},\t{rz:.6f}\r\n\t{damp:.6f}\r\n')
WGT_CHAN = 20
SUBMESH_RECS = 150                                      # triangle-list records per submesh (50 tris): the disc's own meshes never
                                                        # exceed 549 in one list; keep well inside what the VU builder sees in vanilla


def _kf(frame, vals):
    """A .mot/.wgt keyframe: u32 frame, 12 zero bytes, 4 floats."""
    v = list(vals) + [0.0] * (4 - len(vals))
    return mc.Keyframe(struct.pack('<I', int(frame)) + bytes(12) + struct.pack('<4f', *v[:4]))


def _local16(R, T):
    M = em.mat_from_rt(R, T)
    return [float(c) for row in M for c in row]


def build(read, packed, rep, cat_bytes, log=print):
    """`read(name)` fetches disc files (Dran); `packed`/`rep` = the wingless bake; `cat_bytes` = the s86 cat pack (for the
    skin MDT's header template and the .wgt tag words). Returns a dict for build_cat_pack.assemble."""
    import cat_wings as cw
    data = cw.wing_graft(read, packed, rep, log=log)
    nodes, base, parent = data['nodes'], data['base'], data['parent']
    K = base                                                           # the cat's own node count (wing nodes follow)
    # The open bind: Dran's bind ROTATIONS mapped onto the cat (as the viewer created the nodes), with the bone origins
    # FK'd from the pivot by the segment lengths — the viewer's scaled bone-local vertex copies agree exactly there. (The
    # creation-time origins are Dran's positions at the body scale only, not the wing size factor: not a coherent pose.)
    bind_open = data['bind_open']                                      # wing node → (R, T, world) as created
    Psp = nodes[parent]['world']                                       # the spine bone's bind world
    W_open_map = {}
    for side0 in (base, base + 4):
        R1, T1, _ = bind_open[side0]
        Rs = [bind_open[side0 + k][0] for k in range(4)]
        Ts = [list(T1)]
        for k in range(3):
            seg = nodes[side0 + k + 1]['T'][0]                             # the chained bind's local x = the segment length
            Ts.append([Ts[k][c] + seg * Rs[k][0][c] for c in range(3)])
        for k in range(4):
            W_open_map[side0 + k] = em.mat_mul(em.mat_from_rt(Rs[k], Ts[k]), Psp)
    def W_open(i): return W_open_map[i] if i in W_open_map else nodes[i]['world']   # cat bones: their own bind
    cat = mc.Pack.parse(cat_bytes)
    cmds = cat.find('c04cat.mds').payload
    cnodes = em.read_skeleton(cmds)
    skin = next(n for n in cnodes if n['name'] == 'skin')
    skin_mdt = parse_mdt(cmds, skin['meshoff'])
    wgt0 = mc.Mot.from_pack(cat, 'c04cat.wgt')
    tagged = next(t for t in wgt0.tracks)
    W6, W7 = tagged.w6, tagged.w7

    # ── nodes: 8 bones (chain) + 2 mesh nodes ──
    out_nodes = []                                                     # (name, parent cat-relative, local16, mdt bytes or None)
    for k, name in enumerate(BONE_NAMES):
        n = nodes[base + k]
        assert 0 <= n['parent'] < base + k, (name, n['parent'])        # parents precede children (the .wgt chain rule)
        L = em.mat_mul(W_open(base + k), em.rigid_inv(W_open(n['parent'])))   # local in the open bind (spine-local for wing1, chained after)
        R, T = [list(L[r][:3]) for r in range(3)], list(L[3][:3])
        if n['parent'] != parent:
            assert abs(T[1]) < 1e-3 and abs(T[2]) < 1e-3 and abs(T[0] - n['T'][0]) < 1e-3, (name, T, n['T'])   # along the parent's +x, the segment length
        out_nodes.append((name, n['parent'], _local16(R, T), None))
    ident = _local16([[1, 0, 0], [0, 1, 0], [0, 0, 1]], [0, 0, 0])
    mesh_index = {}
    for sd in ('r', 'l'):
        mesh_index[sd] = K + len(out_nodes)
        out_nodes.append([MESH_NAMES[sd], 0, ident, None])            # MDT filled below
    # the mask goes AFTER the wing meshes: their MDTs are written back by fixed slot (8 and 9, the eight wing bones then the two
    # meshes), so anything inserted ahead of them lands in their place instead
    mask_slot = len(out_nodes)                                        # its place in out_nodes…
    mask_index = K + mask_slot                                        # …and the node index the .wgt must name
    out_nodes.append([MASK_NODE, 0, ident, None])

    # ── Dran's per-vertex UV / NORM entries, keyed by its obj1 position index (first record that uses it) ──
    om = data['om']
    dran_uvn = {}
    for prim, midx, recs in om.submeshes:
        for r in recs:
            dran_uvn.setdefault(r[0], (om.uv[r[1]], om.norm[r[2]] if om.norm else (0.0, 0.0, 1.0, 0.0)))

    mat_template = skin_mdt.materials[0]
    material = bytearray(mat_template); material[0x34:0x44] = WING_TEX.encode('ascii').ljust(16, b'\0')
    wgt_tracks, mot_tracks, stats = [], [], {}
    for mi, sd in enumerate(('r', 'l')):
        me = data['meshes'][mi]
        wt, used, remap = data['wing_sets'][sd]
        nv = me['nv']
        # single positions: where the two bone-local copies agree in the open bind, that point (exact in every pose);
        # else — the pooled base verts and the two-pose-fitted tab midpoints / fold lifts, all built to be exact in the
        # LEAP reference pose — the bind-space point that LBS carries to the viewer's position THERE (the open wing's base
        # shape stays; the fold loses only the ±0.2 bulge/lift tweaks): the blend of the two bones' (inverse open bind ×
        # reference pose) matrices is affine and invertible, so solve it
        Wref, Wcat = data['Wref'], data['Wcat']
        def W_ref(b): return Wref[b] if b in Wref else Wcat[b]
        P = []; two_pose = 0
        for i in range(nv):
            b0, b1, w = me['b0'][i], me['b1'][i], me['w0'][i]
            pa, pb = em.xform_pt(W_open(b0), me['p0'][i]), em.xform_pt(W_open(b1), me['p1'][i])
            if w >= 0.999 or math.dist(pa, pb) < 1e-3:
                P.append([pa[c] * w + pb[c] * (1 - w) for c in range(3)])
            else:
                two_pose += 1
                fa, fb = em.xform_pt(W_ref(b0), me['p0'][i]), em.xform_pt(W_ref(b1), me['p1'][i])
                target = [fa[c] * w + fb[c] * (1 - w) for c in range(3)]
                A0 = em.mat_mul(em.rigid_inv(W_open(b0)), W_ref(b0))              # row vectors: v · inv(bind) · posed
                A1 = em.mat_mul(em.rigid_inv(W_open(b1)), W_ref(b1))
                Mb = [[w * A0[r][c] + (1 - w) * A1[r][c] for c in range(4)] for r in range(4)]
                inv = cw._inv4(Mb)
                if inv is None: raise SystemExit(f"wing vertex {i}: singular LBS blend")
                v = em.xform_pt(inv, target)
                P.append([float(v[0]), float(v[1]), float(v[2])])
        # UV / NORM: Dran's entries for carved vertices, the nearest carved vertex's for authored ones
        src_pos = {i: P[i] for i in range(len(used))}
        uv, nrm = [], []
        for i in range(nv):
            if i < len(used):
                e = dran_uvn.get(used[i])
            else:
                j = min(src_pos, key=lambda j_: math.dist(P[i], src_pos[j_]))
                e = dran_uvn.get(used[j])
            if e is None:
                e = ((0.5, 0.5, 1.0, 1.0), (0.0, 0.0, 1.0, 0.0))
            uv.append(tuple(float(c) for c in e[0])); nrm.append(tuple(float(c) for c in e[1]))
        m = Mdt()
        m.hdr = list(skin_mdt.hdr)
        m.pos = [(float(p[0]), float(p[1]), float(p[2]), 1.0) for p in P]
        m.uv = uv; m.norm = nrm; m.col = None; m.has_col = False
        recs = [(int(v), int(v), int(v)) for t in me['tris'] for v in t]
        m.submeshes = [[3, 0, recs[k:k + SUBMESH_RECS]] for k in range(0, len(recs), SUBMESH_RECS)]   # vanilla-sized lists
        m.materials = [bytes(material)]
        m.preamble = [0, 16, len(m.submeshes), 0]
        m.order = ['POS', 'DL', 'UV', 'NORM', 'MAT']
        dl = 16 + sum(12 + 12 * len(r) for _, _, r in m.submeshes)
        m.pads = {'POS': b'', 'DL': bytes((-dl) % 16), 'UV': b'', 'NORM': b'', 'MAT': b''}
        m.hdr[5] = nv; m.hdr[7] = 0; m.hdr[8] = 0xFFFFFFFF; m.hdr[11] = nv; m.hdr[13] = 1; m.hdr[15] = 0
        blob = build_mdt(m)
        assert len(blob) % 16 == 0 and blob[:4] == b'MDT\0', 'wing MDT'
        back = parse_mdt(blob, 0)
        assert len(back.pos) == nv and len(back.materials) == 1 and sum(len(r) for _, _, r in back.submeshes) == 3 * len(me['tris']), 'wing MDT round-trip'
        out_nodes[8 + mi][3] = blob
        out_nodes[8 + mi] = tuple(out_nodes[8 + mi])
        # .wgt run: reset entry, then bones ascending; percents sum to 100 per vertex
        M = mesh_index[sd]
        per_bone = {}
        for i in range(nv):
            b0, b1, w = int(me['b0'][i]), int(me['b1'][i]), me['w0'][i]
            pa = int(round(w * 100))
            if b0 == b1 or pa >= 100: pa = 100
            if pa <= 0: b0, pa = b1, 100
            per_bone.setdefault(b0, []).append((i, pa))
            if pa < 100: per_bone.setdefault(b1, []).append((i, 100 - pa))
        run = [mc.Track(M, 0, WGT_CHAN, 32, W6, W7, [])]                  # the reset node: the mesh node's parent (the cat root)
        for b in sorted(per_bone):
            run.append(mc.Track(M, b, WGT_CHAN, 32, W6, W7, [_kf(i, [float(pct)]) for i, pct in sorted(per_bone[b])]))
        wgt_tracks += run
        stats[sd] = {'verts': nv, 'tris': len(me['tris']), 'mdt': len(blob), 'bones': sorted(per_bone), 'wgt_keys': sum(len(t.keyframes) for t in run), 'two_pose_verts': two_pose}
    # ── .mot tracks (already cat-relative node ids) ──
    for t in data['tracks']:
        if t['node'] < base: continue                                  # the cat's own tracks stay as they are
        assert t['node'] < base + 8 and t['chan'] in (0, 2), t['node']
        mot_tracks.append(mc.Track(t['node'], 0, t['chan'], 32, mc.TAG_W6, mc.TAG_W7, [_kf(f, v) for f, v in zip(t['frames'], t['vals'])]))
    # ── the Super Steve cape: a cloth FRAME node (must be reachable from HER root for SearchFrame — parent = her node 0, its bind
    #    3×3 scaled by HIDE_SCALE so the cloth she builds from it collapses to a point) whose MDT is the rest lattice in the cat's
    #    anchor bone's space; the runtime clones her CCloth onto the copy and re-anchors it to that bone ──
    cat_nodes = data['cat_nodes']
    skin_node = next(n for n in cat_nodes if n['name'] == 'cat_skin')
    skin_w = em.load_weights(data['pack'], 'cat.wgt').get(skin_node['i'])
    skin_me = em.build_mesh_weighted(data['mds'], skin_node, cat_nodes, skin_w)
    rows, cols, cverts = cw.cape_rest_local(cat_nodes, skin_me)                    # row-major from the collar row (rows = hang)
    assert 1 <= rows <= 16 and 1 <= cols <= 16 and len(cverts) == rows * cols
    cverts = [cverts[r * cols + c] for c in range(cols) for r in range(rows)]        # → engine order: outer = width c, inner = hang r
    cm = Mdt(); cm.hdr = list(skin_mdt.hdr)
    cm.pos = [(float(v[0]), float(v[1]), float(v[2]), 1.0) for v in cverts]
    cm.uv = [(0.5, 0.5, 1.0, 1.0)] * len(cverts); cm.norm = [(0.5, 0.5, 1.0, 0.0)] * len(cverts); cm.col = None; cm.has_col = False
    ctris = []
    for c in range(cols - 1):
        for r in range(rows - 1):
            a = c * rows + r; ctris += [(a, a + rows, a + rows + 1), (a, a + rows + 1, a + 1)]
    crecs = [(int(v), int(v), int(v)) for t in ctris for v in t]
    cm.submeshes = [[3, 0, crecs[k:k + SUBMESH_RECS]] for k in range(0, len(crecs), SUBMESH_RECS)]
    cmat = bytearray(mat_template); cmat[0x34:0x44] = CAPE_TEX.encode('ascii').ljust(16, b'\0'); cm.materials = [bytes(cmat)]
    cm.preamble = [0, 16, len(cm.submeshes), 0]; cm.order = ['POS', 'DL', 'UV', 'NORM', 'MAT']
    cdl = 16 + sum(12 + 12 * len(r) for _, _, r in cm.submeshes)
    cm.pads = {'POS': b'', 'DL': bytes((-cdl) % 16), 'UV': b'', 'NORM': b'', 'MAT': b''}
    cm.hdr[5] = len(cverts); cm.hdr[7] = 0; cm.hdr[8] = 0xFFFFFFFF; cm.hdr[11] = len(cverts); cm.hdr[13] = 1; cm.hdr[15] = 0
    cape_mdt = build_mdt(cm)
    assert len(parse_mdt(cape_mdt, 0).pos) == rows * cols
    hs = 0.001                                                                     # build_cat_pack.HIDE_SCALE
    cape_local = [hs, 0, 0, 0, 0, hs, 0, 0, 0, 0, hs, 0, 0, 0, 0, 1]
    cape = {'name': CAPE_NODE, 'parent_abs': 0, 'local16': cape_local, 'mdt': cape_mdt, 'rows': rows, 'cols': cols,
            'clo': (CAPE_CLO_TEXT.format(outer=cols, inner=rows, node=CAPE_NODE, polydiv='0' * (cols - 1),
                                         wind=cw.CAPE_PHYSICS['wind'], normal=cw.CAPE_PHYSICS['normal'],
                                         gx=cw.CAPE_PHYSICS['gravity'][0], gy=cw.CAPE_PHYSICS['gravity'][1], gz=cw.CAPE_PHYSICS['gravity'][2],
                                         fx=cw.CAPE_PHYSICS['follow'][0], fy=cw.CAPE_PHYSICS['follow'][1], fz=cw.CAPE_PHYSICS['follow'][2],
                                         kx=cw.CAPE_PHYSICS['K'][0], ky=cw.CAPE_PHYSICS['K'][1], kz=cw.CAPE_PHYSICS['K'][2])
                    + ''.join(CAPE_BOUND_TEXT.format(node=CAPE_NODE, ux=b['up'][0], uy=b['up'][1], uz=b['up'][2],
                                                     ax=b['A'][0], ay=b['A'][1], az=b['A'][2], bx=b['B'][0], by=b['B'][1], bz=b['B'][2],
                                                     rx=b['radii'][0], ry=b['radii'][1], rz=b['radii'][2], damp=b['damp'])
                              for b in cw.cape_bounds_local(cat_nodes))).encode('ascii'),
            'bounds': [b['bone'] for b in cw.CAPE_BOUNDS],
            'texture': CAPE_TEX, 'anchor': cw.CAPE_ANCHOR}
    stats['cape'] = {'rows': rows, 'cols': cols, 'mdt': len(cape_mdt), 'anchor': cw.CAPE_ANCHOR, 'bounds': cape['bounds']}
    # ── the mask: a ring of geometry around each eye with a real hole in it, bound 100% to the head bone ──
    mask_me = cw.build_mask_mesh(cat_nodes, skin_me)
    head_i = next(n['i'] for n in cat_nodes if n['name'] == cw.MASK_ANCHOR)
    Wh = cat_nodes[head_i]['world']
    mk = Mdt(); mk.hdr = list(skin_mdt.hdr)
    mworld = [em.xform_pt(Wh, v) for v in mask_me['p0']]              # the viewer keeps it head-local; the MDT wants model space
    mk.pos = [(float(v[0]), float(v[1]), float(v[2]), 1.0) for v in mworld]
    mk.uv = [(0.5, 0.5, 1.0, 1.0)] * len(mworld); mk.norm = [(0.5, 0.5, 1.0, 0.0)] * len(mworld)
    mk.col = None; mk.has_col = False
    mrecs = [(int(v), int(v), int(v)) for t in mask_me['tris'] for v in t]
    mk.submeshes = [[3, 0, mrecs[k:k + SUBMESH_RECS]] for k in range(0, len(mrecs), SUBMESH_RECS)]
    mmat = bytearray(mat_template); mmat[0x34:0x44] = MASK_TEX.encode('ascii').ljust(16, b'\0'); mk.materials = [bytes(mmat)]
    mk.preamble = [0, 16, len(mk.submeshes), 0]; mk.order = ['POS', 'DL', 'UV', 'NORM', 'MAT']
    mdl = 16 + sum(12 + 12 * len(r) for _, _, r in mk.submeshes)
    mk.pads = {'POS': b'', 'DL': bytes((-mdl) % 16), 'UV': b'', 'NORM': b'', 'MAT': b''}
    mk.hdr[5] = len(mworld); mk.hdr[7] = 0; mk.hdr[8] = 0xFFFFFFFF; mk.hdr[11] = len(mworld); mk.hdr[13] = 1; mk.hdr[15] = 0
    out_nodes[mask_slot][3] = build_mdt(mk)
    wgt_tracks.append(mc.Track(mask_index, 0, WGT_CHAN, 32, W6, W7, []))          # the reset entry: the mesh node's parent
    wgt_tracks.append(mc.Track(mask_index, head_i, WGT_CHAN, 32, W6, W7,           # …then every vertex, 100% on the head bone
                               [_kf(v, [100.0]) for v in range(len(mworld))]))
    stats['mask'] = {'verts': len(mworld), 'tris': len(mask_me['tris']), 'mdt': len(out_nodes[mask_slot][3]), 'anchor': cw.MASK_ANCHOR}
    bbp = b''.join(struct.pack('<16f', *n[2]) for n in out_nodes)
    stats['tracks'] = len(mot_tracks); stats['mot_keys'] = sum(len(t.keyframes) for t in mot_tracks)
    stats['frames'] = (data['frames_all'][0], data['frames_all'][-1])
    return {'nodes': out_nodes, 'wgt_tracks': wgt_tracks, 'mot_tracks': mot_tracks, 'bbp': bbp,
            'alloc_dbuff': [MESH_NAMES['r'], MESH_NAMES['l'], MASK_NODE], 'texture': WING_TEX, 'stats': stats, 'cape': cape,
            'mask': {'name': MASK_NODE, 'texture': MASK_TEX}}
