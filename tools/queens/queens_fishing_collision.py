#!/usr/bin/env python3
"""Queens (map 2) custom fishing collision — the triangles the mod appends to cpoly at fishing time
(FishingCollision.AddMeshTriangles reads queens_2.bin; built by tools/build_fishing_collision.py).

Three groups, ALL kept at full mesh resolution (no simplification, per design):
  - bridges: the two canal bridge meshes obj40 (X~800) + obj44 (X~-48), entire meshes.
  - pipes:   the obj9 water-outlet pipes (both instances), entire meshes.
  - contain: hand-authored canal containment walls (world-space triangles) that box the fish into the canal.

fishing_collision_tris() -> {'bridges':[...], 'pipes':[...], 'contain':[...], 'all':[...]}
  each triangle is [[x,y,z],[x,y,z],[x,y,z]] in world space.
"""
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'lib'))
import toolpath  # noqa: F401 — puts every tools/ subfolder on sys.path
import os, sys, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from scene_placed import placed_meshes

SCENE, MAPINFO = 'gedit/e03/scene.scn', 'gedit/e03/mapinfo.cfg'

# Canal containment walls — verbatim design list (each row = 3 verts = one triangle).
CONTAIN_RAW = """
-100,0,-50, -200,70,-50, -200,50,-50
-100,0,-50, -100,70,-50, -200,70,-50
-200,50,-50, -200,0,-50, -100,0,-50
-200,50,-50, -200,70,-50, -200,70,50
-200,50,-50, -200,70,50, -200,50,50
-600,50,-50, -600,0,-50, -200,0,-50
-600,50,-50, -200,0,-50, -200,50,-50
-200,50,50, -200,0,50, -600,0,50
-200,50,50, -600,0,50, -600,50,50
-200,50,50, -100,70,50, -100,0,50
-100,0,50, -200,0,50, -200,50,50
-200,50,50, -200,70,50, -100,70,50
-100,70,50, 0,0,50, -100,0,50
-100,70,50, 0,70,50, 0,0,50
0,70,-50, -100,70,-50, -100,0,-50
0,70,-50, -100,0,-50, 0,0,-50
100,70,-50, 0,70,-50, 0,0,-50
100,70,-50, 0,0,-50, 100,0,-50
200,70,-50, 100,70,-50, 100,0,-50
200,70,-50, 100,0,-50, 200,0,-50
300,70,-50, 200,70,-50, 200,0,-50
300,70,-50, 200,0,-50, 300,0,-50
400,70,-50, 300,70,-50, 300,0,-50
400,70,-50, 300,0,-50, 400,0,-50
500,70,-50, 400,70,-50, 400,0,-50
500,70,-50, 400,0,-50, 500,0,-50
500,0,-50, 600,70,-50, 500,70,-50
500,0,-50, 600,0,-50, 600,70,-50
700,70,-50, 600,0,-50, 700,0,-50
700,70,-50, 600,70,-50, 600,0,-50
800,70,-50, 700,0,-50, 800,0,-50
800,70,-50, 700,70,-50, 700,0,-50
900,70,-50, 800,70,-50, 800,0,-50
900,70,-50, 800,0,-50, 900,0,-50
0,70,50, 100,0,50, 0,0,50
0,70,50, 100,70,50, 100,0,50
100,70,50, 200,0,50, 100,0,50
100,70,50, 200,70,50, 200,0,50
200,70,50, 300,0,50, 200,0,50
200,70,50, 300,70,50, 300,0,50
300,70,50, 400,0,50, 300,0,50
300,70,50, 400,70,50, 400,0,50
500,0,50, 400,0,50, 400,70,50
500,0,50, 400,70,50, 500,70,50
500,70,50, 600,0,50, 500,0,50
500,70,50, 600,70,50, 600,0,50
600,70,50, 700,0,50, 600,0,50
600,70,50, 700,70,50, 700,0,50
700,70,50, 800,0,50, 700,0,50
700,70,50, 800,70,50, 800,0,50
800,70,50, 900,70,50, 900,0,50
800,70,50, 900,0,50, 800,0,50
1000,98,-100, 900,70,-100, 900,70,-50
1000,98,-100, 900,70,-50, 1000,98,-50
1000,98,100, 1000,98,50, 900,70,50
900,70,50, 1000,98,200, 1000,98,100
900,70,50, 900,70,200, 1000,98,200
"""


def _parse_contain(raw):
    tris = []
    for ln in raw.strip().splitlines():
        nums = [float(x) for x in re.split(r'[,\s]+', ln.strip()) if x]
        if len(nums) != 9:
            raise ValueError(f"containment row is not 9 numbers: {ln!r}")
        tris.append([nums[0:3], nums[3:6], nums[6:9]])
    return tris


def _objn(name):
    m = re.match(r'obj(\d+)', name)
    return int(m.group(1)) if m else None


def _mesh_tris(obj_nums):
    out = []
    for pm in placed_meshes(SCENE, MAPINFO):
        if _objn(pm['name']) in obj_nums:
            for a, b, c in pm['tris']:
                out.append([list(pm['verts'][a]), list(pm['verts'][b]), list(pm['verts'][c])])
    return out


def fishing_collision_tris():
    bridges = _mesh_tris({40, 44})
    pipes = _mesh_tris({9})
    contain = _parse_contain(CONTAIN_RAW)   # exactly as authored — walls stay vertical
    # NOTE: fish leaking the SOUTH (+Z) wall is NOT winding (CheckHit 0x149d50 is two-sided) and NOT geometry —
    # it's the collision-GATHER box in Step__5CFish: PickUpNearPoly (0x149c30) tests each poly's AABB against
    # [x±10]×[y,y+10]×[z-10,z], which reaches 10u toward -Z (north) but 0 toward +Z, so a thin +Z plane is
    # never gathered in time. Fixed in the engine instead (IsoPatcher symmetrises the box), not by tilting the
    # walls — see IsoPatcher.PatchFishBox.
    return {'bridges': bridges, 'pipes': pipes, 'contain': contain,
            'all': bridges + pipes + contain}


if __name__ == '__main__':
    g = fishing_collision_tris()
    print(f"bridges (obj40/44): {len(g['bridges'])} tris")
    print(f"pipes   (obj9):     {len(g['pipes'])} tris")
    print(f"contain (canal):    {len(g['contain'])} tris")
    print(f"TOTAL:              {len(g['all'])} tris")
