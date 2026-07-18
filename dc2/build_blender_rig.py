#!/usr/bin/env python3
"""Blender round-trip, step 1: extract a DC1 fish's SKELETON + its 7 MOTIONS + a mesh, and emit a
self-contained Blender script that builds an armature, imports the mesh, and bakes each motion as a Blender
ACTION. Open the emitted .py in Blender's Text editor and Run: you get the rig posed and every motion
playable on the timeline (switch actions in the Dope Sheet / Action Editor), so you can weight-paint your
mesh while watching it deform. Step 2 (export weights) + step 3 (repack) are separate tools.

Re-running the emitted script REUSES an existing 'rig'/'fish' in the scene (your painted weights are kept)
and only re-bakes the motion actions — so it's safe to iterate.

Motion baking (the important part): DC1 drives its skeleton by, per bone per frame, taking the bone's rest
LOCAL matrix and swapping its rotation for the keyframe quaternion, then composing world = parent @ local.
We reproduce that exactly, compute the game deformation D = world_anim @ world_rest^-1, conjugate it into
Blender space, and solve for each pose-bone's basis matrix. This is immune to Blender's bone-Y-axis
convention (which is what makes the naive "set rotation_quaternion = game quat" approach look scrambled).

Usage: python3 dc2/build_blender_rig.py [DONOR] [MESH_CHR]
  DONOR    DC1 donor fish (default f12a) — its skeleton + motions
  MESH_CHR .chr providing the mesh to weight (default the shipped Priscleen f19a.chr); uses node 'skin1'
Emits: dc2/blender/<DONOR>_rig.py
"""
import os, sys, struct, re
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools"))
from extract_scene_mesh import load_scene, parse_mds, read_tris

DONOR = sys.argv[1] if len(sys.argv) > 1 else "f12a"
MESH_CHR = sys.argv[2] if len(sys.argv) > 2 else "/Users/thomascantwell/ROMs/dc2_extracted/priscleen/f19a.chr"
MESH_MDS_NAME = "f19a.mds"     # sub-file that holds the mesh
MESH_NODE = "skin1"            # mesh node within it (Priscleen's body)
OUT = os.path.join(os.path.dirname(__file__), "blender", f"{DONOR}_rig.py")

def parse_chr(data):
    subs = []; off = 0
    while off + 0x50 <= len(data):
        name = data[off:off+0x40].split(b"\0")[0].decode("latin1", "replace")
        do = int.from_bytes(data[off+0x40:off+0x44], "little")
        sz = int.from_bytes(data[off+0x44:off+0x48], "little")
        st = int.from_bytes(data[off+0x48:off+0x4c], "little")
        if not name and sz == 0: break
        subs.append((name, data[off+do:off+do+sz]))
        if st == 0: break
        off += st
    return subs

def matmul(a, b):
    """Compose so that xform(result,p) == xform(a, xform(b,p)); matrices are stored m[col*4+row]."""
    r = [0.0]*16
    for c in range(4):
        for row in range(3):
            r[c*4+row] = sum(a[k*4+row]*b[c*4+k] for k in range(3)) + (a[12+row] if c == 3 else 0.0)
    r[15] = 1.0
    return r

def skeleton(mds):
    n = struct.unpack_from("<I", mds, 8)[0]
    frames = []
    for i in range(n):
        b = 0x10 + i*0x70
        name = mds[b+0x08:b+0x18].split(b"\0")[0].decode("latin1", "replace") or f"bone{i}"
        parent = struct.unpack_from("<i", mds, b+0x2c)[0]
        local = list(struct.unpack_from("<16f", mds, b+0x30))
        frames.append([name, parent, local])
    world = [None]*n
    for i, (nm, pa, loc) in enumerate(frames):
        world[i] = loc if pa < 0 else matmul(world[pa], loc)
    return frames, world

def motions(mot, cfg):
    """Return keys=[(start,end,name)], rot_tracks={frameIdx:[(time,(w,x,y,z))]},
    trans_tracks={frameIdx:[(time,(x,y,z))]}."""
    keys = []
    for l in cfg.splitlines():
        m = re.match(r"\s*KEY[\t ]+(\d+),\s*(\d+),\s*[\d.]+,?\s*//(.*)", l)
        if m: keys.append((int(m.group(1)), int(m.group(2)), m.group(3).strip()))
    rot, trans = {}, {}
    o = 0
    while o + 0x20 <= len(mot):
        h = struct.unpack_from("<8I", mot, o); frame, typ, cnt = h[0], h[2], h[4]
        if typ == 0:                                        # rotation track (quaternion)
            kfs = []
            for k in range(cnt):
                base = o+0x20+k*0x20
                fnum = struct.unpack_from("<i", mot, base)[0]        # keyframe frame number (INT, not float!)
                q = struct.unpack_from("<4f", mot, base+0x10)        # quaternion w,x,y,z @ +0x10
                kfs.append((fnum, q))
            rot[frame] = kfs
        elif typ == 2:                                      # translation track (x,y,z)
            kfs = []
            for k in range(cnt):
                base = o+0x20+k*0x20
                fnum = struct.unpack_from("<i", mot, base)[0]
                t = struct.unpack_from("<3f", mot, base+0x10)
                kfs.append((fnum, t))
            trans[frame] = kfs
        if h[5] == 0: break
        o += h[5]
    return keys, rot, trans

def main():
    donor = dict(parse_chr(load_scene(f"chara/{DONOR}.chr")))
    frames, world = skeleton(donor[f"{DONOR}.mds"])
    keys, rot, trans = motions(donor[f"{DONOR}.mot"], donor["info.cfg"].decode("shift_jis", "replace"))
    # mesh to weight
    mesh_mds = dict(parse_chr(open(MESH_CHR, "rb").read()))[MESH_MDS_NAME]
    mo = next(mo for nm, mo, _ in parse_mds(mesh_mds, 0) if nm == MESH_NODE)
    vc = struct.unpack_from("<I", mesh_mds, mo+0xc)[0]; vo = mo + struct.unpack_from("<I", mesh_mds, mo+0x10)[0]
    verts = [struct.unpack_from("<3f", mesh_mds, vo+i*0x10) for i in range(vc)]
    tris = read_tris(mesh_mds, mo)

    bones = [{"name": f"{i}_{nm}", "parent": pa, "local": loc}
             for i, (nm, pa, loc) in enumerate(frames)]
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w") as f:
        f.write("# AUTO-GENERATED by dc2/build_blender_rig.py — open in Blender's Text editor and Run.\n")
        f.write("# First run builds armature '%s_rig' + mesh + baked motions. Re-running reuses them and\n" % DONOR)
        f.write("# only re-bakes motions (your painted weights are preserved).\n")
        f.write("import bpy, math\nfrom mathutils import Vector, Quaternion, Matrix\n\n")
        f.write("BONES = %r\n" % bones)
        f.write("VERTS = %r\n" % [list(v) for v in verts])
        f.write("TRIS  = %r\n" % [list(t) for t in tris])
        f.write("KEYS  = %r\n" % keys)
        f.write("ROT_TRACKS   = %r\n" % {k: [(t, list(q)) for t, q in v] for k, v in rot.items()})
        f.write("TRANS_TRACKS = %r\n" % {k: [(t, list(q)) for t, q in v] for k, v in trans.items()})
        f.write(BLENDER_BODY)
    print(f"wrote {OUT}: {len(bones)} bones, {len(verts)}v/{len(tris)}t mesh, "
          f"{len(keys)} motions, {len(rot)} rot / {len(trans)} trans tracks")

BLENDER_BODY = r'''
UP = Matrix.Rotation(math.radians(90), 4, 'X')   # game Y-up -> Blender Z-up for display
I4 = Matrix.Identity(4)

def to_mat(m):                                    # game col-major m[col*4+row] -> mathutils Matrix
    return Matrix(((m[0], m[4], m[8],  m[12]),
                   (m[1], m[5], m[9],  m[13]),
                   (m[2], m[6], m[10], m[14]),
                   (m[3], m[7], m[11], m[15])))

def sample(kfs, t, quat):
    a = kfs[0]
    if t <= a[0]: return Quaternion(a[1]) if quat else Vector(a[1])
    if t >= kfs[-1][0]: return Quaternion(kfs[-1][1]) if quat else Vector(kfs[-1][1])
    for lo, hi in zip(kfs, kfs[1:]):
        if lo[0] <= t <= hi[0]:
            f = (t-lo[0])/(hi[0]-lo[0]) if hi[0] != lo[0] else 0.0
            if quat: return Quaternion(lo[1]).slerp(Quaternion(hi[1]), f)
            return Vector(lo[1]).lerp(Vector(hi[1]), f)
    return Quaternion(kfs[-1][1]) if quat else Vector(kfs[-1][1])

def game_world(t):
    """Per-frame world matrices, game convention: swap the keyframe rotation/translation into each bone's
    rest local matrix, then world = parent @ local."""
    W = [None]*len(BONES)
    for i, b in enumerate(BONES):
        L = to_mat(b["local"])
        if i in ROT_TRACKS or i in TRANS_TRACKS:
            trans = L.to_translation(); rot = L.to_3x3()
            if i in ROT_TRACKS:   rot   = sample(ROT_TRACKS[i], t, True).to_matrix()
            if i in TRANS_TRACKS: trans = sample(TRANS_TRACKS[i], t, False)
            L = Matrix.Translation(trans) @ rot.to_4x4()
        p = b["parent"]
        W[i] = L if p < 0 else W[p] @ L
    return W

def rest_world():
    W = [None]*len(BONES)
    for i, b in enumerate(BONES):
        L = to_mat(b["local"]); p = b["parent"]
        W[i] = L if p < 0 else W[p] @ L
    return W

def build():
    ao = bpy.data.objects.get("rig")
    if ao is None:
        arm = bpy.data.armatures.new("rig"); ao = bpy.data.objects.new("rig", arm)
        bpy.context.collection.objects.link(ao); bpy.context.view_layer.objects.active = ao
        RW = rest_world()
        bpy.ops.object.mode_set(mode='EDIT'); eb = {}
        for i, b in enumerate(BONES):
            bone = arm.edit_bones.new(b["name"]); eb[i] = bone
            h = (UP @ RW[i]).to_translation(); bone.head = h; bone.tail = h + Vector((0, 0, 0.5))
        for i, b in enumerate(BONES):
            if b["parent"] >= 0: eb[i].parent = eb[b["parent"]]
        for i, b in enumerate(BONES):
            kids = [eb[j] for j, bb in enumerate(BONES) if bb["parent"] == i]
            # ONLY point at a child that's far enough away. If head==child.head the bone becomes zero-length,
            # and Blender DELETES zero-length bones on leaving edit mode — silently shredding the spine chain
            # (this fish's null/chn/jnt links sit right on top of each other). Keep the default tail instead.
            if kids and (kids[0].head - eb[i].head).length > 1e-3:
                eb[i].tail = kids[0].head
        bpy.ops.object.mode_set(mode='OBJECT')
        me = bpy.data.meshes.new("fish"); me.from_pydata([list(UP @ Vector(v)) for v in VERTS], [], TRIS)
        me.update(); mo = bpy.data.objects.new("fish", me); bpy.context.collection.objects.link(mo)
        md = mo.modifiers.new("arm", 'ARMATURE'); md.object = ao; mo.parent = ao
        grp = {i: mo.vertex_groups.new(name=b["name"]) for i, b in enumerate(BONES)}
        # Deterministic rigid weights: glue each vertex to its NEAREST bone segment (weight 1.0). This never
        # fails (unlike Blender's bone-heat auto-weights, which silently give up on fin meshes), so the fish
        # is guaranteed to deform. It's rough (tears a little at joints) — refine later by weight-painting.
        RW = rest_world()
        heads = [(UP @ RW[i]).to_translation() for i in range(len(BONES))]
        tails = list(heads)
        for i, b in enumerate(BONES):
            kids = [j for j, bb in enumerate(BONES) if bb["parent"] == i]
            tails[i] = heads[kids[0]] if kids else heads[i] + Vector((0, 0, 0.01))
        def seg_dist(p, a, b):
            ab = b - a; d = ab.length_squared
            t = max(0.0, min(1.0, (p - a).dot(ab) / d)) if d > 0 else 0.0
            return (p - (a + ab * t)).length
        skinnable = [i for i, b in enumerate(BONES) if "cube" not in b["name"]]  # skip the eyeball meshes
        for vi, v in enumerate(mo.data.vertices):
            best = min(skinnable, key=lambda i: seg_dist(v.co, heads[i], tails[i]))
            grp[best].add([vi], 1.0, 'REPLACE')
        print(f"created rig + mesh, weighted {len(mo.data.vertices)} verts to nearest bone — press Play")
    else:
        print("reusing existing rig + mesh (painted weights preserved) — rebaking motions")

    # make the armature the active + selected object so the Action Editor tracks IT (not the mesh)
    for o in bpy.context.selected_objects: o.select_set(False)
    ao.select_set(True); bpy.context.view_layer.objects.active = ao

    for pb in ao.pose.bones: pb.rotation_mode = 'QUATERNION'
    dbones = ao.data.bones
    restM = [dbones[b["name"]].matrix_local.copy() if b["name"] in dbones else None for b in BONES]
    missing = [b["name"] for b, m in zip(BONES, restM) if m is None]
    if missing: print("WARNING: bones missing from armature (skipped):", missing)
    GR = rest_world()                                                       # game rest world

    for mi, (s, e, label) in enumerate(KEYS):                               # drop our old actions first
        old = bpy.data.actions.get(f"m{mi}_{label}")
        if old: bpy.data.actions.remove(old)

    ao.animation_data_create()
    acts = []
    for mi, (s, e, label) in enumerate(KEYS):
        act = bpy.data.actions.new(f"m{mi}_{label}"); act.use_fake_user = True; acts.append((act, e-s))
        ao.animation_data.action = act
        for t in range(s, e+1):
            GW = game_world(t)
            pose = []
            for i, b in enumerate(BONES):
                if restM[i] is None: pose.append(None); continue
                D = UP @ (GW[i] @ GR[i].inverted()) @ UP.inverted()        # deformation in Blender space
                pose.append(D @ restM[i])                                  # target pose (armature space)
            for i, b in enumerate(BONES):
                if restM[i] is None: continue
                p = b["parent"]
                rp = restM[p] if p >= 0 and restM[p] is not None else I4
                pp = pose[p] if p >= 0 and pose[p] is not None else I4
                basis = restM[i].inverted() @ rp @ pp.inverted() @ pose[i]
                loc, quat, _ = basis.decompose()
                pb = ao.pose.bones[b["name"]]
                pb.location = loc; pb.rotation_quaternion = quat
                pb.keyframe_insert("location", frame=t-s)
                pb.keyframe_insert("rotation_quaternion", frame=t-s)
        ao.animation_data.action = None
    # Freeze the fish mid-swing on a BIG motion so it's visibly BENT the instant the script finishes —
    # this confirms rig + weights + baking all work WITHOUT needing the play button. (Motion 3 = the jump,
    # the largest movement.) Once you see it bent, scrub the timeline or pick m5 to judge the caught flap.
    verify = min(3, len(acts)-1)
    act, length = acts[verify]
    ao.animation_data.action = act
    bpy.context.scene.frame_start = 0; bpy.context.scene.frame_end = length
    bpy.context.scene.frame_set(length // 2)   # sit at mid-swing so the pose is extreme, not rest
    print(f"baked {len(acts)} motions; froze '{act.name}' mid-swing (frame {length//2} of {length}). "
          f"The fish should look BENT right now.")

build()
'''

if __name__ == "__main__":
    main()
