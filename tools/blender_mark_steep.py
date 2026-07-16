"""Blender helper — highlight rock faces that are INVALID for Dark Cloud fishing collision.

A face is collidable ("floor-ish", the bobber can land on it) only if |normal.Y|/|n| > 0.2 in the game.
After importing the rock OBJ with Up = Y, Blender is Z-up, so the same test is |normal.Z| > 0.2. Faces at
or below that (steeper than ~78.5 deg from horizontal) are near-vertical walls the bobber can't collide
with — this script flags them.

USE: Scripting workspace (or any Text Editor) -> open this file -> select the rock mesh object -> Run.
  * It switches to Edit / Face-select mode and SELECTS the too-steep faces (they show highlighted).
  * Re-run after edits to re-evaluate. It prints the count in the system console / info log.
  * Set MARK_RED = True to also paint them with a red 'steep_invalid' material so they stay visible while
    you work (view in Solid + Color:Material, or Material Preview).

Note: near-vertical SIDES of pillars are expected to flag — that's fine, the bobber lands on the flat TOP.
Use this mainly to check the surfaces the bobber actually rests on (the tops) aren't accidentally steep,
and to find spiky/jagged faces.
"""
import bpy
import bmesh

THRESHOLD = 0.2      # |normal.Z| <= THRESHOLD  ==  too steep to collide (matches game |normal.Y| <= 0.2)
MARK_RED  = False    # True: also assign a red material to steep faces for a persistent highlight

obj = bpy.context.active_object
if obj is None or obj.type != 'MESH':
    raise RuntimeError("Select the rock mesh object first.")

if bpy.context.mode != 'EDIT_MESH':
    bpy.ops.object.mode_set(mode='EDIT')

me = obj.data
bm = bmesh.from_edit_mesh(me)
bm.select_mode = {'FACE'}
bm.normal_update()                            # make sure face normals are current

# f.normal is in LOCAL (mesh) space. A Y-up OBJ import usually bakes the up-conversion into the object's
# ROTATION, so local normals stay Y-up while the viewport shows Z-up — testing local .z then wrongly flags
# flat tops. Transform the normal to WORLD space (the frame you see) and test its up (Z) component instead.
mw = obj.matrix_world.to_3x3()

steep = 0
for f in bm.faces:
    n = mw @ f.normal
    L = n.length or 1.0
    bad = abs(n.z) / L <= THRESHOLD           # world-space up-component; <=0.2 == too steep
    f.select_set(bad)
    if bad:
        steep += 1
bm.select_flush_mode()

if MARK_RED:
    mat = bpy.data.materials.get('steep_invalid') or bpy.data.materials.new('steep_invalid')
    mat.diffuse_color = (1.0, 0.0, 0.0, 1.0)
    if mat.name not in me.materials:
        me.materials.append(mat)
    slot = me.materials.find(mat.name)
    for f in bm.faces:
        if f.select:
            f.material_index = slot

bmesh.update_edit_mesh(me)

_msg = (f"{steep} of {len(bm.faces)} faces too steep for collision "
        f"(|normal.Z| <= {THRESHOLD}) -- now selected" + (" + marked red" if MARK_RED else ""))
print("[mark_steep]", _msg)
# print() only reaches the system console (invisible on macOS unless Blender was launched from a terminal),
# so also show the result as a popup in the Blender UI:
def _draw(self, _ctx):
    self.layout.label(text=_msg)
bpy.context.window_manager.popup_menu(_draw, title="Steep collision faces", icon='ERROR')
