#!/usr/bin/env python3
r"""extract_model.py -- Dark Cloud character `.chr` -> compact JSON for the WebGL model viewer.

Cracks and exports, per character model, everything the viewer needs to render the mesh and play a
motion with forward kinematics (see game_data/docs/mot-format.md sec.10 for the format write-up):

  SKELETON  -- the `.mds` CFrame node table (77-ish 0x70-byte records after the 0x18 header):
      +0x00  char[0x20]  node name
      +0x20  u32         MESH offset: absolute offset (within the .mds payload) of this node's MDT
                         mesh chunk, or 0 if the node carries no geometry
      +0x24  i32         PARENT node index (-1 = root)
      +0x28  f32[16]     4x4 LOCAL BIND matrix, ROW-MAJOR, row-vector convention: rows 0..2 = the 3x3
                         rotation, row 3 = translation (tx,ty,tz,1). world = local * parentWorld.
      +0x68  u32         self id (index+1); +0x6C u32 = 0x70 stride (both ignored by us; the LAST
                         record's two trailing words overlap the first MDT header, so only 0x00..0x68
                         of a record are trusted -- name/mesh/parent/matrix all live below 0x68).

  GEOMETRY  -- DC1 characters are PER-NODE RIGID mesh parts. Each node with a nonzero +0x20 owns a
      clean indexed MDT chunk (same codec as scene meshes, tools/lib/mdt_codec.py) rigidly attached to
      that node -- hands, feet, face, hat, hair, cloth, bag, belt... plus one big body mesh "skin"
      (whole T-pose body, ~400 verts) attached at/near the root. The small parts animate perfectly by
      drawing them at their owner node's animated world transform. The big body mesh spans many joints,
      so it is AUTO-SKINNED here: every body vertex is bound to its 2 nearest bind-pose joints
      (inverse-distance weights) -> linear-blend skinning at runtime. There are no per-vertex weights
      in the file; nearest-bone auto-skin is a preview heuristic, good enough to judge a motion.

  ANIMATION -- the `.mot` track list (mot_codec): per (node index w0, channel w2) a sparse ascending
      list of keyframes. chan 0 = ABSOLUTE local rotation quaternion (scalar-first w,x,y,z); chan 2 =
      ABSOLUTE local translation. At frame 1 these EQUAL the bind matrix (verified), so FK per frame is:
          localRot   = slerp(chan0 keys)  if the node has a chan0 track, else bind rotation
          localTrans = lerp (chan2 keys)  if the node has a chan2 track, else bind translation
          world      = local * parentWorld
      quat->matrix uses the convention that reproduces the stored bind 3x3:
          [[1-2(yy+zz), 2(xy-zw), 2(xz+yw)], [2(xy+zw), 1-2(xx+zz), 2(yz-xw)], [2(xz-yw),2(yz+xw),1-2(xx+yy)]]

  MOTIONS   -- named `KEY start,end,speed //<shift-jis name>  <id>` lines in the `.chr` cfg. The id
      (ordinal) is what the town engine hard-wires (idle=0, run=1, walk=2, fall=8, land=9); we surface
      the decoded Japanese name + an English gloss + the [start,end] window + baked speed.

Usage:
    source .env
    python3 tools/model_viewer/extract_model.py            # build every model, bake model_viewer.html
    python3 tools/model_viewer/extract_model.py --dump c01d # print one model's skeleton/motion summary

Heavy arrays (mesh positions, triangle indices, keyframe values) are quantized to int16/uint16 and
base64-packed so the whole multi-model viewer stays small. The viewer template
(tools/model_viewer/viewer_template.html) has a `/*__MODEL_DATA__*/` placeholder that this script
replaces with `const MODELS = {...}` to produce the self-contained tools/model_viewer/model_viewer.html.
"""
import os, sys, re, json, math, base64, struct

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'lib'))
from mot_codec import load_pack, Mot                       # noqa: E402
from mdt_codec import parse_mdt                            # noqa: E402

# ---- character code -> name, in the dropdown group order the coordinator asked for ----
CHAR_MAP = [('c01', 'Toan'), ('c04', 'Xiao'), ('c05', 'Ruby'),
            ('c06', 'Goro'), ('c10', 'Ungaga'), ('c18', 'Osmond')]
CHAR_ORDER = [n for _, n in CHAR_MAP]

# ---- explicit motion-only packs the enumeration filter would otherwise drop (their `*d02s` basename
#      is on the effect-model exclude list). These are the dungeon STAIR-CLIMB overlays: no `.mds`, so
#      they borrow the character's canonical rig via their `.mot` node indices (same as c01dhashigo).
#      (char, chr subpath, rank). c01d02s is intentionally omitted (no usable motion). ----
EXTRA_MOTION_PACKS = [
    ('Goro', 'dun\\d02\\stair_mos\\c06d02s.chr', 1),   # borrows c06b rig (60 nodes; mot max w0 57)
    ('Xiao', 'dun\\d02\\stair_mos\\c04d02s.chr', 1),   # borrows c04b rig (79 nodes; mot max w0 78)
]

# ---- e02 Matataki event-cast models (NOT the six allies: villagers / elder / object model). Own an
#      `.mds`, so full models; grouped last for the user to eyeball. e100 / e127_01 ship no `.mds`
#      (motion-only, no known rig) -> nothing to render, skipped with a note. ----
E02_GROUP = 'Matataki e02'
E02_STEMS = ['e100', 'e120', 'e121', 'e122', 'e123', 'e124', 'e125', 'e126',
             'e127', 'e127_01', 'e128', 'e129', 'e131']
GROUP_ORDER = CHAR_ORDER + [E02_GROUP]

# ---- Goro's cutscene models from his House (s01) and the Spirit Tree (s03). Each bundles Toan + Goro
#      with a cfg per character; we force the `c06` cfg so the GORO body/motion is the one shown. All
#      carry their own c06 `.mds`; grouped under Goro as event models. (stem, folder). ----
GORO_EXTRA = [('e101', 'gedit\\s01\\chara'), ('e101eb', 'gedit\\s01\\chara'),
              ('e102', 'gedit\\s01\\chara'), ('e103', 'gedit\\s01\\chara'),
              ('e105', 'gedit\\s01\\chara'), ('e130', 'gedit\\s03\\chara')]


def _stem(hedname):
    return hedname.replace('/', '\\').rsplit('\\', 1)[-1].lower()[:-4]      # basename, no .chr


def _folder(hedname):
    p = hedname.replace('/', '\\')
    return p.rsplit('\\', 1)[0] if '\\' in p else ''


def _char_of(stem):
    for code, name in CHAR_MAP:
        if code in stem:
            return name
    return None


def _excluded(hedname):
    """The coordinator's exclusion set: weapons, menu, and effect/util models."""
    xl = hedname.lower().replace('/', '\\')
    stem = _stem(hedname)
    if 'commenu\\' in xl or 'wep_eff\\' in xl:
        return True
    if re.search(r'c\d\dw\d\d', stem):        # weapon  c##w##
        return True
    if stem.endswith('wtes'):                 # weapon test rig
        return True
    if re.search(r'_ex\d*$', stem):           # *_ex / *_ex00 effect-extra meshes
        return True
    if stem.endswith('_tameex'):
        return True
    if 'fuusya' in stem:                      # whirlwind effect
        return True
    if stem.startswith('urac'):
        return True
    if stem.endswith('d02s'):
        return True
    return False


def _rank(stem, folder):
    """Sort order within a character group: 0 town/field, 1 dungeon, 2 event/cutscene."""
    if 'dun\\mainchara' in folder:
        return 1
    if '\\event' in folder or 'opdat' in folder or re.match(r'^(e\d|03|d0|_|ec)', stem):
        return 2
    return 0


def enumerate_models(dc_dir=None):
    """Scan data.hed for every character body `.chr` (codes c01/c04/c05/c06/c10/c18), drop the
    excluded weapon/menu/effect models, DEDUPE byte-identical copies by content hash (collapsing each
    identical set to one entry that records its extra scene folders), and return build specs sorted by
    character group then rank(town/field, dungeon, event) then stem."""
    import hashlib
    dc = dc_dir or os.environ.get('DC1_DATA_DIR')
    if not dc:
        raise SystemExit('set $DC1_DATA_DIR')
    hed = open(os.path.join(dc, 'data.hed'), 'rb').read()
    hd2 = open(os.path.join(dc, 'data.hd2'), 'rb').read()
    names = [hed[i * 80:i * 80 + 80].split(b'\x00')[0].decode('latin1') for i in range(len(hed) // 80)]
    dat = open(os.path.join(dc, 'data.dat'), 'rb')

    seen = {}                                  # content-hash -> spec
    order = []
    for i, nm in enumerate(names):
        if not nm.lower().endswith('.chr'):
            continue
        stem = _stem(nm)
        ch = _char_of(stem)
        if ch is None or _excluded(nm):
            continue
        off, size, _ = struct.unpack_from('<III', hd2, 16 + i * 32)
        dat.seek(off)
        h = hashlib.sha256(dat.read(size)).hexdigest()
        folder = _folder(nm)
        if h in seen:
            seen[h]['dups'].append(folder)     # byte-identical copy in another scene
            continue
        spec = {'char': ch, 'stem': stem, 'folder': folder, 'sub': nm,
                'size': size, 'dups': [], 'rank': _rank(stem, folder)}
        seen[h] = spec
        order.append(spec)
    dat.close()

    # explicit extras (excluded by filter) — verify each motion-only pack's max w0 fits its ref rig
    for char, sub, rank in EXTRA_MOTION_PACKS:
        try:
            _, pack = load_pack(sub)
        except Exception as e:
            sys.stderr.write(f"  SKIP extra {sub}: {e}\n"); continue
        cfg = find_cfg(pack)
        mot_name = parse_cfg(cfg.payload)[1] if cfg else None
        rec = pack.find(mot_name) if mot_name else None
        if rec is None:
            rec = next((r for r in pack.records if r.name.lower().endswith('.mot')), None)
        maxw0 = max((t.w0 for t in Mot.from_record(rec).tracks), default=-1) if rec else -1
        nbase = len(get_base_skeleton(char)[0])
        if maxw0 >= nbase:
            sys.stderr.write(f"  SKIP extra {sub}: max w0 {maxw0} >= ref rig {nbase} nodes\n"); continue
        order.append({'char': char, 'stem': _stem(sub), 'folder': _folder(sub), 'sub': sub,
                      'size': 0, 'dups': [], 'rank': rank})

    # Goro cutscene models (s01 House / s03 Spirit Tree): force the c06 cfg so Goro's body renders
    for st, folder in GORO_EXTRA:
        order.append({'char': 'Goro', 'stem': st, 'folder': folder, 'sub': f'{folder}\\{st}.chr',
                      'size': 0, 'dups': [], 'rank': 2, 'cfg_prefer': 'c06'})

    # e02 Matataki cast -> a trailing group of their own (build_model skips the two motion-only ones)
    for st in E02_STEMS:
        sub = f'gedit\\e02\\chara\\{st}.chr'
        order.append({'char': E02_GROUP, 'stem': st, 'folder': 'gedit\\e02\\chara', 'sub': sub,
                      'size': 0, 'dups': [], 'rank': 0})

    order.sort(key=lambda s: (GROUP_ORDER.index(s['char']), s['rank'], s['stem'], s['folder']))
    return order


def _scene_tag(folder):
    """A compact scene token for a folder, e.g. gedit\\s86\\chara -> s86, dun\\mainchara -> mainchara."""
    parts = [p for p in folder.split('\\') if p not in ('gedit', 'chara', 'in')]
    return parts[-1] if parts else folder


def make_label(spec):
    """Dropdown label: `<code> · <folder>` plus a note when it is one file shared across scenes."""
    lbl = f"{spec['stem']} · {spec['folder']}"
    if spec['dups']:
        tags = ', '.join(_scene_tag(f) for f in spec['dups'])
        lbl += f"  (+{len(spec['dups'])}: {tags})"
    return lbl

# Japanese (shift-jis) motion name -> short English gloss, for the ones a human is shopping for.
# Order matters: more specific phrases first (checked left-to-right).
GLOSS = [
    ('立ち＞ガード', 'idle->guard'), ('ガード＞立ち', 'guard->idle'), ('ガードループ', 'guard-loop'),
    ('構え＞ガード', 'ready->guard'), ('起き上がり', 'get-up'), ('ガード', 'guard'), ('構え', 'ready'),
    ('落下', 'fall'), ('着地', 'land'),
    ('はしご', 'ladder'), ('ハシゴ', 'ladder'), ('梯子', 'ladder'),
    ('登り', 'climb-up'), ('のぼり', 'climb-up'), ('降り', 'climb-down'), ('上り', 'climb-up'),
    ('立ち', 'idle'), ('歩き', 'walk'), ('走り', 'run'),
    ('ジャンプ', 'jump'), ('ダメダメ', 'no/shake-head'), ('ダメージ', 'damage'),
    ('手を振', 'wave'), ('押し開き', 'push-open'), ('引き開き', 'pull-open'), ('押し', 'push'), ('引き', 'pull'),
    ('アイテムゲット', 'item-get'), ('アイテム', 'item'), ('ポーズ', 'pose'), ('気を付', 'attention'),
    ('腰に手', 'hand-on-hip'), ('背伸び', 'stretch'), ('リングを見る', 'look-at-ring'),
    ('呼び', 'call'), ('泳', 'swim'), ('水', 'water'), ('ドア', 'door'), ('両開き', 'double-door'),
    ('呼吸', 'breathe'), ('投げ', 'throw'), ('攻撃', 'attack'), ('撃', 'attack'),
    ('魔法', 'magic'), ('リアクション', 'react'), ('ループ', 'loop'), ('着', 'land'),
]


def gloss_of(jp):
    for key, en in GLOSS:
        if key and key in jp:
            return en
    return ''


# ---------------------------------------------------------------- small linear algebra (row-vector)
def mat_mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def mat_from_rt(R, T):
    return [[R[0][0], R[0][1], R[0][2], 0.0],
            [R[1][0], R[1][1], R[1][2], 0.0],
            [R[2][0], R[2][1], R[2][2], 0.0],
            [T[0], T[1], T[2], 1.0]]


def rigid_inv(m):
    """Inverse of a rigid (rotation+translation) row-vector 4x4."""
    R = [[m[i][j] for j in range(3)] for i in range(3)]
    Rt = [[R[j][i] for j in range(3)] for i in range(3)]      # transpose
    t = [m[3][0], m[3][1], m[3][2]]
    nt = [-(t[0] * Rt[0][j] + t[1] * Rt[1][j] + t[2] * Rt[2][j]) for j in range(3)]
    return mat_from_rt(Rt, nt)


def xform_pt(m, v):
    x, y, z = v
    return (x * m[0][0] + y * m[1][0] + z * m[2][0] + m[3][0],
            x * m[0][1] + y * m[1][1] + z * m[2][1] + m[3][1],
            x * m[0][2] + y * m[1][2] + z * m[2][2] + m[3][2])


def mat_to_quat(R):
    """3x3 (row-major R[i][j], as stored in .mds) -> quaternion (w,x,y,z), the EXACT inverse of the
    viewer's quatMat (the game's convention):
        R = [[1-2(yy+zz), 2(xy-zw), 2(xz+yw)],
             [2(xy+zw), 1-2(xx+zz), 2(yz-xw)],
             [2(xz-yw), 2(yz+xw), 1-2(xx+yy)]]
    Shepperd's method with the signs matching that convention (verified: quatMat(mat_to_quat(R)) == R)."""
    m00, m01, m02 = R[0]
    m10, m11, m12 = R[1]
    m20, m21, m22 = R[2]
    tr = m00 + m11 + m22
    if tr > 0:
        s = math.sqrt(tr + 1.0) * 2
        w = 0.25 * s
        x = (m21 - m12) / s
        y = (m02 - m20) / s
        z = (m10 - m01) / s
    elif m00 >= m11 and m00 >= m22:
        s = math.sqrt(1.0 + m00 - m11 - m22) * 2
        w = (m21 - m12) / s
        x = 0.25 * s
        y = (m01 + m10) / s
        z = (m02 + m20) / s
    elif m11 >= m22:
        s = math.sqrt(1.0 + m11 - m00 - m22) * 2
        w = (m02 - m20) / s
        x = (m01 + m10) / s
        y = 0.25 * s
        z = (m12 + m21) / s
    else:
        s = math.sqrt(1.0 + m22 - m00 - m11) * 2
        w = (m10 - m01) / s
        x = (m02 + m20) / s
        y = (m12 + m21) / s
        z = 0.25 * s
    n = math.sqrt(w * w + x * x + y * y + z * z) or 1.0
    return [w / n, x / n, y / n, z / n]


# ---------------------------------------------------------------- cfg parsing
def find_cfg(pack, prefer=None):
    """First `.cfg` record, or — when `prefer` is given — the first whose name contains that substring.
    Bundled multi-character `.chr`s ship one cfg per character (e.g. e101c01d.cfg + e101c06a.cfg); pass
    prefer='c06' to select Goro's cfg (its MODEL/MOTION/KEY lines then drive the build)."""
    if prefer:
        for r in pack.records:
            if r.name.lower().endswith('.cfg') and prefer in r.name.lower():
                return r
    for r in pack.records:
        if r.name.lower().endswith('.cfg'):
            return r
    return None


def parse_cfg(cfg_payload):
    """Return (mds_name, mot_name, motions[]). motions = [{name,gloss,start,end,speed,id}]."""
    txt = cfg_payload.decode('latin1', 'replace')          # keep bytes; decode names as shift-jis below
    raw = cfg_payload
    mds_name = mot_name = None
    m = re.search(rb'MODEL\s+"([^"]+)"', raw)
    if m:
        mds_name = m.group(1).decode('latin1')
    m = re.search(rb'MOTION\s+\d+\s*,\s*"([^"]+\.mot)"', raw)   # index may be 0/1/3/... (first wins)
    if m:
        mot_name = m.group(1).decode('latin1')
    motions = []
    idx = 0
    for line in txt.splitlines():
        before, _, comment = line.partition('//')
        code = before.strip()
        if not re.match(r'KEY\b', code) or code.startswith('KEY_START'):
            continue
        nums = re.findall(r'\d+\.?\d*', code[3:])            # start, end, speed (any separators)
        if len(nums) < 2:
            continue
        start = int(float(nums[0]))
        end = int(float(nums[1]))
        speed = float(nums[2]) if len(nums) > 2 else 0.3
        # comment is shift-jis; the ASCII id digits sit at the FRONT (base.cfg //0name) or BACK
        # (info.cfg //name  0). Names use full-width digits, so stripping ASCII digit runs is safe.
        try:
            jp = comment.encode('latin1', 'replace').decode('shift_jis', 'replace')
        except Exception:
            jp = comment
        jp = re.sub(r'^\s*\d+\s*', '', jp)                  # leading id column
        jp = re.sub(r'\s*\d+\s*$', '', jp)                  # trailing id column
        jp = jp.strip().replace('�', '')
        empty = (start == 0 and end == 0)
        motions.append({'name': jp, 'gloss': gloss_of(jp), 'start': start, 'end': end,
                        'speed': speed if speed > 0 else 0.3, 'id': idx, 'empty': 1 if empty else 0})
        idx += 1
    return mds_name, mot_name, motions


# ---------------------------------------------------------------- skeleton
def read_skeleton(mds):
    count = struct.unpack_from('<I', mds, 0x08)[0]
    nodes = []
    for i in range(count):
        p = 0x18 + i * 0x70
        name = mds[p:p + 0x20].split(b'\x00')[0].decode('latin1', 'replace')
        meshoff = struct.unpack_from('<I', mds, p + 0x20)[0]
        parent = struct.unpack_from('<i', mds, p + 0x24)[0]
        R = [list(struct.unpack_from('<3f', mds, p + 0x28 + r * 16)) for r in range(3)]
        T = list(struct.unpack_from('<3f', mds, p + 0x58))
        nodes.append({'i': i, 'name': name, 'meshoff': meshoff, 'parent': parent,
                      'R': R, 'T': T, 'quat': mat_to_quat(R)})
    # bind world + inverse
    for n in nodes:
        L = mat_from_rt(n['R'], n['T'])
        n['world'] = L if n['parent'] < 0 else mat_mul(L, nodes[n['parent']]['world'])
        n['invworld'] = rigid_inv(n['world'])
        n['worldpos'] = (n['world'][3][0], n['world'][3][1], n['world'][3][2])
    return nodes


# ---------------------------------------------------------------- geometry
def mdt_triangles(m):
    """Expand an Mdt's submeshes to a flat triangle-index list over m.pos (position index only)."""
    tris = []
    for prim, midx, recs in m.submeshes:
        if prim == 3:
            for k in range(0, len(recs) - 2, 3):
                tris.append((recs[k][0], recs[k + 1][0], recs[k + 2][0]))
        elif prim == 4:                                    # tri strip
            for k in range(len(recs) - 2):
                a, b, c = recs[k][0], recs[k + 1][0], recs[k + 2][0]
                if a == b or b == c or a == c:
                    continue
                if k & 1:
                    tris.append((b, a, c))
                else:
                    tris.append((a, b, c))
    return tris


def build_mesh(mds, node, nodes):
    """Return a per-vertex-skinned mesh dict for one node's MDT (verts in MODEL space, with up to two
    bone influences each). Rigid parts -> 1 bone (the owner). Large body meshes -> 2 nearest joints."""
    m = parse_mdt(mds, node['meshoff'])
    local_pos = [v[:3] for v in m.pos]
    tris = mdt_triangles(m)
    if not tris:
        return None
    # local bbox -> decide skin vs rigid
    xs = [v[0] for v in local_pos]; ys = [v[1] for v in local_pos]; zs = [v[2] for v in local_pos]
    dim = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    is_skin = dim > 5.0
    owner = node['i']
    ow = node['world']

    # candidate deform joints for auto-skin: every node except the model root and skin-mesh owners
    cand = [n for n in nodes if n['parent'] >= 0 and not (n['meshoff'] and _mesh_dim(mds, n) > 5.0)]

    infl0_bone, infl0_pos, infl1_bone, infl1_pos, w0 = [], [], [], [], []
    for v in local_pos:
        vm = xform_pt(ow, v)                               # model-space position
        if not is_skin or not cand:
            infl0_bone.append(owner); infl0_pos.append(v)  # v is already owner-local
            infl1_bone.append(owner); infl1_pos.append((0.0, 0.0, 0.0)); w0.append(1.0)
            continue
        # two nearest bind joints
        d = sorted(((_d2(vm, n['worldpos']), n) for n in cand), key=lambda t: t[0])
        (d0, n0), (d1, n1) = d[0], d[1]
        d0 = math.sqrt(d0) + 1e-4; d1 = math.sqrt(d1) + 1e-4
        wa = (1.0 / d0) / (1.0 / d0 + 1.0 / d1)
        infl0_bone.append(n0['i']); infl0_pos.append(xform_pt(n0['invworld'], vm))
        infl1_bone.append(n1['i']); infl1_pos.append(xform_pt(n1['invworld'], vm))
        w0.append(wa)
    return {'node': owner, 'skin': is_skin, 'nv': len(local_pos), 'tris': tris,
            'b0': infl0_bone, 'p0': infl0_pos, 'b1': infl1_bone, 'p1': infl1_pos, 'w0': w0}


_MESH_DIM_CACHE = {}


def _mesh_dim(mds, node):
    key = (id(mds), node['meshoff'])
    if key in _MESH_DIM_CACHE:
        return _MESH_DIM_CACHE[key]
    try:
        m = parse_mdt(mds, node['meshoff'])
        xs = [v[0] for v in m.pos]; ys = [v[1] for v in m.pos]; zs = [v[2] for v in m.pos]
        dim = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    except Exception:
        dim = 0.0
    _MESH_DIM_CACHE[key] = dim
    return dim


def _d2(a, b):
    return (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2


# ---------------------------------------------------------------- motion tracks
def build_tracks(pack, mot_name, node_count):
    mot = Mot.from_pack(pack, mot_name)
    tracks = []
    for t in mot.tracks:
        if t.w0 >= node_count or t.w2 not in (0, 2):
            continue
        if not t.keyframes:
            continue
        frames = [kf.frame for kf in t.keyframes]
        vals = [list(kf.value) for kf in t.keyframes]      # 4 floats each
        tracks.append({'node': t.w0, 'chan': t.w2, 'frames': frames, 'vals': vals})
    return tracks


# ---------------------------------------------------------------- packing helpers
def b64_i16(vals, scale):
    a = bytearray()
    for v in vals:
        q = int(round(v * scale))
        q = max(-32768, min(32767, q))
        a += struct.pack('<h', q)
    return base64.b64encode(bytes(a)).decode('ascii')


def b64_u16(vals):
    a = bytearray()
    for v in vals:
        a += struct.pack('<H', max(0, min(65535, int(v))))
    return base64.b64encode(bytes(a)).decode('ascii')


def b64_u8(vals):
    return base64.b64encode(bytes(max(0, min(255, int(round(v)))) for v in vals)).decode('ascii')


# ---------------------------------------------------------------- decimation (size control)
def decimate_mesh(m, keep):
    """Drop triangles to ~`keep` fraction and remap to only the vertices still used, shrinking both the
    index and the (dominant) vertex arrays. Preview-only: leaves holes, which the coordinator OK'd."""
    if keep >= 1.0 or len(m['tris']) < 8:
        return m
    step = max(1, round(1.0 / keep))
    tris = m['tris'][::step]
    used = sorted({i for t in tris for i in t})
    remap = {old: new for new, old in enumerate(used)}
    return {
        'node': m['node'], 'skin': m['skin'], 'nv': len(used),
        'tris': [(remap[a], remap[b], remap[c]) for a, b, c in tris],
        'b0': [m['b0'][i] for i in used], 'p0': [m['p0'][i] for i in used],
        'b1': [m['b1'][i] for i in used], 'p1': [m['p1'][i] for i in used],
        'w0': [m['w0'][i] for i in used],
    }


def decimate_tracks(tracks, stride):
    """Keep every `stride`-th keyframe per track (always the first and last, to preserve range+loop)."""
    if stride <= 1:
        return tracks
    out = []
    for t in tracks:
        n = len(t['frames'])
        if n <= 3:
            out.append(t); continue
        keep = list(range(0, n, stride))
        if keep[-1] != n - 1:
            keep.append(n - 1)
        out.append({'node': t['node'], 'chan': t['chan'],
                    'frames': [t['frames'][i] for i in keep],
                    'vals': [t['vals'][i] for i in keep]})
    return out


# ---- base body per character: motion-only `.chr` packs (no .mds, e.g. c01dhashigo = Toan's ladder,
#      the event/cutscene overlays) borrow this rig; their .mot w0 indexes the same node table. ----
BASE_BODY = {'Toan': 'chara\\c01d.chr', 'Xiao': 'dun\\mainchara\\c04b.chr',
             'Ruby': 'dun\\mainchara\\c05a.chr', 'Goro': 'dun\\mainchara\\c06b.chr',
             'Ungaga': 'dun\\mainchara\\c10b.chr', 'Osmond': 'dun\\mainchara\\c18a.chr'}
_BASE_SKEL_CACHE = {}


def get_base_skeleton(char):
    """(nodes, full_res_meshes, mds_name) for a character's canonical body, cached. Used to render a
    motion-only pack (no geometry of its own) on the right rig."""
    if char in _BASE_SKEL_CACHE:
        return _BASE_SKEL_CACHE[char]
    _, pack = load_pack(BASE_BODY[char])
    cfg = find_cfg(pack)
    mds_name = parse_cfg(cfg.payload)[0] if cfg else None
    mds_rec = pack.find(mds_name) if mds_name else None
    if mds_rec is None:
        mds_rec = next(r for r in pack.records if r.name.lower().endswith('.mds'))
    mds = mds_rec.payload
    nodes = read_skeleton(mds)
    _MESH_DIM_CACHE.clear()
    meshes = []
    for n in nodes:
        if n['meshoff']:
            try:
                mm = build_mesh(mds, n, nodes)
                if mm:
                    meshes.append(mm)
            except Exception:
                pass
    _BASE_SKEL_CACHE[char] = (nodes, meshes, mds_rec.name)
    return _BASE_SKEL_CACHE[char]


# ---------------------------------------------------------------- per-model build
def build_model(spec, kf_stride=1, tri_keep=1.0, skeleton_only=False):
    label, sub = make_label(spec), spec['sub']
    name, pack = load_pack(sub)
    cfg = find_cfg(pack, spec.get('cfg_prefer'))
    if cfg is None:
        raise ValueError('no cfg')
    mds_name, mot_name, motions = parse_cfg(cfg.payload)
    mds_rec = pack.find(mds_name) if mds_name else None
    if mds_rec is None:
        mds_rec = next((r for r in pack.records if r.name.lower().endswith('.mds')), None)

    if mds_rec is None:                                    # motion-only pack -> borrow the base rig
        if spec['char'] not in BASE_BODY:
            raise ValueError('motion-only pack with no base rig (skipped)')
        nodes, base_meshes, base_mds = get_base_skeleton(spec['char'])
        mds_rec_name = base_mds + ' (borrowed)'
        full_meshes = [] if skeleton_only else base_meshes
    else:
        mds = mds_rec.payload
        nodes = read_skeleton(mds)
        _MESH_DIM_CACHE.clear()
        full_meshes = []
        if not skeleton_only:
            for n in nodes:
                if n['meshoff']:
                    try:
                        mm = build_mesh(mds, n, nodes)
                        if mm:
                            full_meshes.append(mm)
                    except Exception as e:                 # skip an unparseable chunk, keep the rest
                        sys.stderr.write(f"    [warn] {label} node {n['name']} mesh skipped: {e}\n")
        mds_rec_name = mds_rec.name

    if mot_name is None or pack.find(mot_name) is None:
        mot_name = next(r.name for r in pack.records if r.name.lower().endswith('.mot'))
    meshes = [decimate_mesh(mm, tri_keep) for mm in full_meshes]

    tracks = decimate_tracks(build_tracks(pack, mot_name, len(nodes)), kf_stride)

    # ---- choose a single position scale covering every quantized magnitude ----
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

    # ---- serialize ----
    jnodes = [{'n': n['name'], 'p': n['parent'],
               't': [round(n['T'][0], 4), round(n['T'][1], 4), round(n['T'][2], 4)],
               'q': [round(x, 6) for x in n['quat']]} for n in nodes]

    jmeshes = []
    for m in meshes:
        flat0 = [c for p in m['p0'] for c in p]
        flat1 = [c for p in m['p1'] for c in p]
        tri = [i for t in m['tris'] for i in t]
        jmeshes.append({
            'node': m['node'], 'skin': 1 if m['skin'] else 0, 'nv': m['nv'], 'nt': len(m['tris']),
            'b0': b64_u16(m['b0']), 'b1': b64_u16(m['b1']), 'w0': b64_u8([w * 255 for w in m['w0']]),
            'p0': b64_i16(flat0, pos_scale), 'p1': b64_i16(flat1, pos_scale),
            'tri': b64_u16(tri),
        })

    jtracks = []
    for t in tracks:
        scale = 32767.0 if t['chan'] == 0 else pos_scale
        flatv = [c for v in t['vals'] for c in v]
        jtracks.append({'node': t['node'], 'chan': t['chan'], 'nk': len(t['frames']),
                        'f': b64_u16(t['frames']), 'v': b64_i16(flatv, scale)})

    maxframe = max((t['frames'][-1] for t in tracks), default=1)
    total_verts = sum(m['nv'] for m in meshes)
    total_tris = sum(len(m['tris']) for m in meshes)
    return {
        'label': label, 'group': spec['char'], 'code': spec['stem'], 'folder': spec['folder'],
        'name': name.replace('\\', '/'), 'mds': mds_rec_name, 'mot': mot_name,
        'posScale': pos_scale, 'quatScale': 32767.0, 'maxFrame': maxframe,
        'skelOnly': 1 if skeleton_only or not jmeshes else 0,
        'nodes': jnodes, 'meshes': jmeshes, 'motions': motions, 'tracks': jtracks,
        '_stats': {'nodes': len(nodes), 'meshes': len(meshes), 'verts': total_verts,
                   'tris': total_tris, 'tracks': len(tracks), 'motions': len(motions),
                   'rank': spec['rank']},
    }


# ---------------------------------------------------------------- html bake
# fidelity levels, coarsest last: (keyframe stride, triangle keep-fraction, skeleton-only)
LEVELS = [(1, 1.0, False), (2, 1.0, False), (2, 0.5, False), (3, 0.35, False), (3, 0.0, True)]


def _model_bytes(m):
    return len(json.dumps(m, separators=(',', ':'), ensure_ascii=False).encode('utf-8'))


def bake_html(budget_mb=15.5):
    specs = enumerate_models()
    print(f"Enumerated {len(specs)} unique character models (deduped by content hash). Building...")

    models, used_specs, levels, nbytes = [], [], [], []
    for sp in specs:
        try:
            m = build_model(sp, *LEVELS[0])
        except Exception as e:
            sys.stderr.write(f"  SKIP {sp['stem']}·{sp['folder']}: {e}\n")
            continue
        models.append(m); used_specs.append(sp); levels.append(0); nbytes.append(_model_bytes(m))

    tpl_path = os.path.join(HERE, 'viewer_template.html')
    out_path = os.path.join(HERE, 'model_viewer.html')
    with open(tpl_path, 'r', encoding='utf-8') as f:
        tpl = f.read()
    budget = int(budget_mb * 1024 * 1024) - len(tpl.encode('utf-8')) - 4096   # data budget

    # ---- escalating decimation to fit the budget, in the coordinator's priority order ----
    # tier 0 = e02 Matataki (decimate first — confirmed NOT the target, reference only),
    # tier 1 = Toan cutscene variants, tier 2 = other event/cutscene models (incl. the new Goro
    # cutscenes), tier 3 = PROTECTED: every ally PRIMARY body (rank 0 town/field + rank 1 dungeon,
    # incl. the stair packs) — never decimated, never dropped. Dropping (last resort) hits tier 0
    # then 1 then 2; tier 3 is untouchable.
    def tier(sp):
        if sp['char'] == E02_GROUP:
            return 0
        if sp['rank'] != 2:
            return 3                                   # ally primary bodies + stair packs
        return 1 if sp['char'] == 'Toan' else 2        # cutscene/event variants

    def cap(sp):
        return 0 if tier(sp) == 3 else (len(LEVELS) - 1)

    reduced, dropped = [], []
    full_total = sum(nbytes)
    while sum(nbytes) > budget:
        chosen = -1
        for T in (0, 1, 2):                            # exhaust a whole tier before touching the next
            best = -1
            for i, sp in enumerate(used_specs):
                if tier(sp) == T and levels[i] < cap(sp) and nbytes[i] > best:
                    best, chosen = nbytes[i], i
            if chosen >= 0:
                break
        if chosen < 0:                                 # nothing left to decimate -> drop (never tier 3)
            di, best = -1, -1
            for pref in (0, 1, 2):
                for i, sp in enumerate(used_specs):
                    if tier(sp) == pref and nbytes[i] > best:
                        best, di = nbytes[i], i
                if di >= 0:
                    break
            if di < 0:
                break
            dropped.append(models[di]['label'])
            for lst in (models, used_specs, levels, nbytes):
                del lst[di]
            continue
        levels[chosen] += 1
        models[chosen] = build_model(used_specs[chosen], *LEVELS[levels[chosen]])
        nbytes[chosen] = _model_bytes(models[chosen])
    for i, lv in enumerate(levels):
        if lv > 0:
            reduced.append((models[i]['label'], lv))

    data_js = 'const MODELS = ' + json.dumps(models, separators=(',', ':'), ensure_ascii=False) + ';'
    html = tpl.replace('/*__MODEL_DATA__*/', data_js)
    with open(out_path, 'w', encoding='utf-8') as f:
        f.write(html)

    # ---- report ----
    print("\nEmbedded models (grouped, in dropdown order):")
    cur = None
    for m in models:
        if m['group'] != cur:
            cur = m['group']; print(f"  == {cur} ==")
        s = m['_stats']
        tag = 'cast' if m['group'] == E02_GROUP else ['town/field', 'dungeon', 'event'][s['rank']]
        print(f"    {m['label']:52} {tag:10} meshes={s['meshes']:2} verts={s['verts']:5} "
              f"tris={s['tris']:5} tracks={s['tracks']:3} motions={s['motions']:2}"
              f"{'  SKEL-ONLY' if m['skelOnly'] else ''}")
    kb = os.path.getsize(out_path) / 1024
    print(f"\nFull (undecimated) data would be {full_total/1024/1024:.2f} MB; budget {budget_mb} MB.")
    if reduced:
        lv_names = ['full', 'kf/2', 'kf/2+tri/2', 'kf/3+tri/3', 'skeleton-only']
        print(f"Decimated {len(reduced)} models to fit: " +
              ', '.join(f"{lbl.split(' · ')[0]}={lv_names[lv]}" for lbl, lv in reduced))
    if dropped:
        print(f"DROPPED {len(dropped)} (Toan cutscene, last resort): " +
              ', '.join(d.split(' · ')[0] for d in dropped))
    print(f"\nWrote {out_path}  ({kb:.0f} KB, {kb/1024:.2f} MB)  with {len(models)} models")
    return out_path, models


def dump_model(short):
    for sp in enumerate_models():
        if short.lower() in sp['sub'].lower():
            m = build_model(sp)
            print(json.dumps({'label': m['label'], 'group': m['group'], 'stats': m['_stats'],
                              'motions': m['motions']}, indent=2, ensure_ascii=False))
            return
    print("no match for", short)


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == '--dump':
        dump_model(sys.argv[2])
    else:
        budget = float(sys.argv[1]) if len(sys.argv) > 1 else 15.5
        bake_html(budget)
