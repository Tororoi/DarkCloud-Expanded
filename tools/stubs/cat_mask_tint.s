# cat_mask_tint.s — the Super Steve cat's MASK draws under the cape's ambient instead of the cat's.
#
# WHY A CAVE AT ALL. A cloth is easy to recolour: Draw__10CCharacter (0x139560) walks its cloth list calling Draw__6CCloth in a
# loop, so ElfCave.CatCapeTint wraps that one `jal` and gives the cape its own ambient. A MESH has no such seam — the same
# function sets the ambient ONCE, calls MGDraw on the whole frame tree, and restores. So the mask, an ordinary mesh on the cat,
# is stuck with the cat's tint, and because a mesh's tint is ADDED to its lit colour rather than multiplied through its texture,
# the cat's blue lands on the mask and turns its red to pink — with no texture able to undo it.
#
# WHY NOT PATCH THE DRAW. Meshes reach the screen through C++ VIRTUAL calls: __vt__13CVisualMDTVu1 (0x002A11A0, 32 B) holds the
# two DrawVu1 overloads in slots 6 and 7. There is no direct `jal` to hook, and patching the functions themselves would put a
# node test in front of every mesh the game draws.
#
# WHAT THIS DOES INSTEAD. The runtime already gives every cat mesh a PRIVATE CVisualMDT in the mod's own cave (CopyMeshes), so
# the mask's visual is an object nobody else can reach. DivineBeastTitle.MaskTint copies the vtable, points slots 6 and 7 here,
# and writes that copy into the mask visual alone. Not one byte of shared engine code is patched, and nothing else in the game
# can arrive at this cave — the only pointer to it lives in an object the mod allocated itself.
#
# Two entry points, one body: slot 6's overload takes a uint* packet, slot 7's a sceVif1Packet*. They differ only in which
# function the body finally calls, so each entry just loads its target and falls into the shared code.
#
# Frame 0x90. ⚠ THE CALLING CONVENTION IS NOT o32: this build passes arguments 5-8 in t0-t3, not on the stack — the game's own
# call site proves it (DrawVu1__13CVisualShadow at 0x136368 loads t1, t2, t3 out of its frame in the three instructions before
# the jal). DrawVu1 takes `this` plus seven parameters, exactly eight arguments, so ALL of them are in registers and none are on
# the stack at all. A first draft copied stack words down into its own frame and used t0 as the scratch register to do it, which
# would have destroyed argument 5 on every call. So: save a0-a3 AND t0-t3, keep the scratch in v1, restore all eight, call.
#
# 0x10-0x1C t0-t3, 0x30 saved ambient, 0x40 ours, 0x50 v0, 0x54 the target, 0x60-0x6C a0-a3, 0x70 ra.
    lui   $t9, 0x0013
    j     body
    ori   $t9, $t9, 0x60E0         # slot 6 (+0x18 of the vtable): DrawVu1__13CVisualMDTVu1FPUi...
    lui   $t9, 0x0013
    j     body
    ori   $t9, $t9, 0x6200         # slot 7 (+0x1C): DrawVu1__13CVisualMDTVu1FP13sceVif1Packet...
body:
    addiu $sp, $sp, -0x90
    sw    $ra, 0x70($sp)
    sw    $t9, 0x54($sp)
    sw    $a0, 0x60($sp)
    sw    $a1, 0x64($sp)
    sw    $a2, 0x68($sp)
    sw    $a3, 0x6C($sp)
    sw    $t0, 0x10($sp)           # arguments 5-8 — MGGetAmbient/MGSetAmbient are free to clobber these
    sw    $t1, 0x14($sp)
    sw    $t2, 0x18($sp)
    sw    $t3, 0x1C($sp)
    addiu $a0, $sp, 0x30
    jal   0x0012DD30               # MGGetAmbient(&saved)
    nop
    lui   $v1, 0x01FB
    lwc1  $f0, 0x30($sp)
    lwc1  $f1, 0x34($sp)
    lwc1  $f2, 0x38($sp)
    lwc1  $f3, 0x3C($sp)
    lwc1  $f4, 0x428C($v1)         # Mailbox.CatCapeTint — the SAME delta the cape uses, so the two match by construction
    lwc1  $f5, 0x4290($v1)
    lwc1  $f6, 0x4294($v1)
    add.s $f0, $f0, $f4
    add.s $f1, $f1, $f5
    add.s $f2, $f2, $f6
    swc1  $f0, 0x40($sp)
    swc1  $f1, 0x44($sp)
    swc1  $f2, 0x48($sp)
    swc1  $f3, 0x4C($sp)           # the fourth component rides along untouched
    addiu $a0, $sp, 0x40
    jal   0x0012DD00               # MGSetAmbient(&ours)
    nop
    lw    $a0, 0x60($sp)
    lw    $a1, 0x64($sp)
    lw    $a2, 0x68($sp)
    lw    $a3, 0x6C($sp)
    lw    $t0, 0x10($sp)
    lw    $t1, 0x14($sp)
    lw    $t2, 0x18($sp)
    lw    $t3, 0x1C($sp)
    lw    $t9, 0x54($sp)
    jalr  $t9                      # the real DrawVu1 for this visual
    nop
    sw    $v0, 0x50($sp)
    addiu $a0, $sp, 0x30
    jal   0x0012DD00               # MGSetAmbient(&saved) — the cat's own tint is back for whatever draws next
    nop
    lw    $v0, 0x50($sp)
    lw    $ra, 0x70($sp)
    jr    $ra
    addiu $sp, $sp, 0x90
