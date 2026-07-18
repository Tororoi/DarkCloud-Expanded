# Blender round-trip, STEP 2: export your weight-painted Priscleen mesh back out.
# Open this in Blender's Text editor and Run AFTER you've weight-painted the "fish" mesh to the "rig"
# armature (from f00s_rig.py / whichever donor rig you loaded). It writes per-vertex bone weights to a
# JSON that dc2/repack_priscleen.py bakes into f19a.chr (STEP 3).
#
# The mesh vertex order is Priscleen's skin1 order (from_pydata preserves it), so vertex index i here ==
# MDT index i in the game file. Vertex groups are named "<frameIdx>_<bone>" (frameIdx = the donor .mds
# frame index), which is exactly the bone id the .wgt binds to.  Do NOT add/remove vertices in Blender.
import bpy, json, math
from mathutils import Matrix

OUT = "/Users/thomascantwell/DarkCloud-Enhanced/dc2/blender/priscleen_weights.json"
UP_INV = Matrix.Rotation(math.radians(90), 4, 'X').inverted()   # Blender Z-up -> game Y-up

def find_mesh():
    ob = bpy.context.active_object
    if ob and ob.type == 'MESH' and ob.vertex_groups:
        return ob
    cands = [o for o in bpy.data.objects if o.type == 'MESH' and o.vertex_groups]
    if not cands:
        raise RuntimeError("no mesh with vertex groups found — weight-paint the fish first")
    return max(cands, key=lambda o: len(o.vertex_groups))

def frame_index(group_name):
    return int(group_name.split("_", 1)[0])   # groups are "<frameIdx>_<bone>"

def main():
    mo = find_mesh()
    gi_to_frame = {g.index: frame_index(g.name) for g in mo.vertex_groups}
    weights, unweighted = [], 0
    for v in mo.data.vertices:
        ws = [(gi_to_frame[g.group], g.weight) for g in v.groups if g.weight > 0.0]
        s = sum(w for _, w in ws)
        if s <= 0.0:
            weights.append([]); unweighted += 1                # repack will warn
        else:
            weights.append([[fi, round(w / s * 100.0, 2)] for fi, w in ws])  # DC1 weights sum to 100
    # current bone rest positions (game space) — informational; repack keeps the donor skeleton as-is
    bones = []
    arm = next((m.object for m in mo.modifiers if m.type == 'ARMATURE' and m.object), None)
    if arm:
        for b in arm.data.bones:
            h = UP_INV @ b.head_local
            bones.append({"name": b.name, "pos": [h.x, h.y, h.z]})
    json.dump({"mesh": mo.name, "vert_count": len(weights), "weights": weights, "bones": bones},
              open(OUT, "w"))
    nz = sum(1 for w in weights if w)
    print(f"[export_weights] wrote {OUT}")
    print(f"  mesh '{mo.name}': {len(weights)} verts, {nz} weighted, {unweighted} UNWEIGHTED, {len(bones)} bones")
    if unweighted:
        print(f"  WARNING: {unweighted} verts have no weight — they'll collapse to the origin in-game. "
              f"Paint every vertex (Weights > Normalize All helps).")

main()
