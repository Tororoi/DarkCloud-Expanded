# atla_draw.s — the Atla Gemron's atla bouncing free of its dying grip. Assembled at 0x0010AAC0 (DeadSeekCave.AtlaDraw: the dead
# sceCdSeek body after atla_shot.s, running on into the dead sceCdStandby / sceCdStop / sceCdPause bodies). ElfSpeciesPatches.
# PatchAtlaDraw points CDungeonMap::DrawAtraBoll's `jal MGDraw` (0x1C51F0) here: a0 = the atla model's frame (posed at the atla's place
# and bob), s0 = the atla, s1 = &its bob phase, s3 = the map.
#
# AtlaGemron spawns the real atla as its Gemron dies (CDungeonMap::SetAtraBoll at its resting place) and fills an entry of
# CodeCaves.AtlaBounce (2 × 0x50): +0 the atla (−1 none), +4 the map, +8 the dying unit's model block, +0xC Ω and +0x10 1 / sin Ω
# (the turn), +0x14 q0 and +0x24 q1 (w, x, y, z: the grafted atla's world turn at the swap, a real atla's), +0x34 the start scale,
# +0x38 K0 and +0x44 K1 (where the frame's position starts and ends: the sphere's centre less the root's offset). The bounce runs on
# the unit's own death frame (its motion 11, AtlaGemronBake: the swap on 217, settled by 229):
#  · before 217: not drawn (the graft is still in the Gemron's grip); its bob held at 0;
#  · 217 → 229 (t = 0 → 1, e = 1 − (1 − t)²): turned by slerp(q0, q1, e) (CFrame::SetTransMatrix keeps the root's own offset), scaled
#    from the start scale to 1, placed at K0 → K1 by e plus 6 × 4t(1 − t) of lift, drawn, then given back its record's turn
#    (CodeCaves.AtlaRootQuat) and scale 1; its bob held at 0, so it bobs from rest as it settles;
#  · past it, or once the unit is no longer dying: the entry freed and the atla's pickup radius back to 13 (AtlaGemron holds it at 0
#    while it bounces) — a real atla, drawn as any.
# The frame keeps ra, a0 and the entry; f-registers and t-registers only besides (DrawAtraBoll reloads what it needs).

    lui   $t0, 0x01FB
    addiu $t0, $t0, -0x1AC0        # 0x01FAE540 CodeCaves.AtlaBounce, entry 0
    addiu $t1, $zero, 2
find:
    lw    $t2, 0x0000($t0)         # its atla
    bne   $t2, $s0, next
    lw    $t3, 0x0004($t0)         # (delay) its map
    beq   $t3, $s3, found
    nop
next:
    addiu $t1, $t1, -1
    bne   $t1, $zero, find
    addiu $t0, $t0, 0x0050         # (delay)
    j     0x0012ED80               # MGDraw, as before
    nop
found:
    lw    $t4, 0x0008($t0)         # the unit's model block
    lw    $t5, 0x0BD8($t4)         # its playing motion
    addiu $t6, $zero, 11
    bne   $t5, $t6, over           # not dying any more
    lw    $t5, 0x0260($t4)         # (delay) its frame: a positive float, so it compares as an int
    lui   $t6, 0x4359              # 217.0 (AtlaGemronBake.Swap)
    slt   $t7, $t5, $t6
    beq   $t7, $zero, swap
    nop
    jr    $ra                      # still in the grip: not drawn
    sw    $zero, 0x0000($s1)       # (delay) its bob held
over:
    addiu $t5, $zero, -1
    sw    $t5, 0x0000($t0)         # a real atla from now on:
    lui   $at, 0x0001
    addu  $t2, $s3, $at
    addiu $t2, $t2, -0x72A8        # its event (map + 0x8D58, 48 × 0x50: kind +0, radius +0x1C, atla +0x20)
    addiu $t1, $zero, 48
    addiu $t5, $zero, 3            # an atla's
ev:
    lw    $t3, 0x0000($t2)
    bne   $t3, $t5, evnext
    lw    $t3, 0x0020($t2)         # (delay) its atla
    bne   $t3, $s0, evnext
    lui   $t3, 0x4150              # (delay) 13.0, SetAtraBoll's
    sw    $t3, 0x001C($t2)         # can be picked up
evnext:
    addiu $t1, $t1, -1
    bne   $t1, $zero, ev
    addiu $t2, $t2, 0x0050         # (delay)
    j     0x0012ED80
    nop
swap:
    mtc1  $t5, $f0                 # the frame
    mtc1  $t6, $f1                 # 217
    lui   $t7, 0x3DAA
    ori   $t7, $t7, 0xAAAB         # 1 / 12 (AtlaGemronBake: 12 frames to settle)
    mtc1  $t7, $f2
    lui   $t7, 0x3F80
    mtc1  $t7, $f3                 # 1.0
    sub.s $f0, $f0, $f1
    mul.s $f0, $f0, $f2            # t
    mfc1  $t8, $f0
    slt   $t9, $t8, $t7            # t < 1 (both positive)
    beq   $t9, $zero, over
    nop
    addiu $sp, $sp, -0x40
    sw    $ra, 0x0000($sp)
    sw    $a0, 0x0004($sp)         # the frame
    sw    $t0, 0x0008($sp)         # the entry
    sw    $zero, 0x0000($s1)       # its bob held
    swc1  $f0, 0x000C($sp)         # t
    sub.s $f4, $f3, $f0
    mul.s $f4, $f4, $f4
    sub.s $f4, $f3, $f4            # e = 1 − (1 − t)²
    swc1  $f4, 0x0010($sp)
    lwc1  $f5, 0x000C($t0)         # Ω
    sub.s $f6, $f3, $f4            # 1 − e
    jal   0x0011D8A0               # sinf
    mul.s $f12, $f6, $f5           # (delay) (1 − e)Ω
    swc1  $f0, 0x0014($sp)
    lw    $t0, 0x0008($sp)
    lwc1  $f12, 0x0010($sp)
    lwc1  $f5, 0x000C($t0)
    jal   0x0011D8A0               # sinf
    mul.s $f12, $f12, $f5          # (delay) eΩ
    lw    $t0, 0x0008($sp)
    lwc1  $f6, 0x0010($t0)         # 1 / sin Ω
    lwc1  $f7, 0x0014($sp)
    mul.s $f7, $f7, $f6            # q0's weight
    mul.s $f8, $f0, $f6            # q1's weight
    lwc1  $f9, 0x0014($t0)         # the turn: q0 × its weight + q1 × its
    lwc1  $f10, 0x0024($t0)
    mul.s $f9, $f9, $f7
    mul.s $f10, $f10, $f8
    add.s $f9, $f9, $f10
    swc1  $f9, 0x0020($sp)
    lwc1  $f9, 0x0018($t0)
    lwc1  $f10, 0x0028($t0)
    mul.s $f9, $f9, $f7
    mul.s $f10, $f10, $f8
    add.s $f9, $f9, $f10
    swc1  $f9, 0x0024($sp)
    lwc1  $f9, 0x001C($t0)
    lwc1  $f10, 0x002C($t0)
    mul.s $f9, $f9, $f7
    mul.s $f10, $f10, $f8
    add.s $f9, $f9, $f10
    swc1  $f9, 0x0028($sp)
    lwc1  $f9, 0x0020($t0)
    lwc1  $f10, 0x0030($t0)
    mul.s $f9, $f9, $f7
    mul.s $f10, $f10, $f8
    add.s $f9, $f9, $f10
    swc1  $f9, 0x002C($sp)
    lw    $a0, 0x0004($sp)
    jal   0x001285A0               # CFrame::SetTransMatrix(float *): its rows turned, its own offset kept
    addiu $a1, $sp, 0x0020         # (delay)
    lw    $t0, 0x0008($sp)
    lw    $a0, 0x0004($sp)
    lui   $t7, 0x3F80
    mtc1  $t7, $f3                 # 1.0
    lwc1  $f4, 0x0010($sp)         # e
    lwc1  $f5, 0x0034($t0)         # the start scale
    sub.s $f6, $f3, $f5
    mul.s $f6, $f6, $f4
    add.s $f5, $f5, $f6            # its scale now
    swc1  $f5, 0x0210($a0)
    swc1  $f5, 0x0214($a0)
    swc1  $f5, 0x0218($a0)
    lwc1  $f6, 0x000C($sp)         # t
    sub.s $f7, $f3, $f6
    mul.s $f7, $f7, $f6
    lui   $t7, 0x41C0              # 24.0: a lift of 6 at the middle
    mtc1  $t7, $f8
    lwc1  $f9, 0x0038($t0)         # x: K0 + (K1 − K0)e
    lwc1  $f10, 0x0044($t0)
    sub.s $f10, $f10, $f9
    mul.s $f10, $f10, $f4
    add.s $f9, $f9, $f10
    swc1  $f9, 0x0220($a0)
    mul.s $f7, $f7, $f8            # 24 t(1 − t)
    lwc1  $f9, 0x003C($t0)         # y, with the lift
    lwc1  $f10, 0x0048($t0)
    sub.s $f10, $f10, $f9
    mul.s $f10, $f10, $f4
    add.s $f9, $f9, $f10
    add.s $f9, $f9, $f7
    swc1  $f9, 0x0224($a0)
    lwc1  $f9, 0x0040($t0)         # z
    lwc1  $f10, 0x004C($t0)
    sub.s $f10, $f10, $f9
    mul.s $f10, $f10, $f4
    add.s $f9, $f9, $f10
    swc1  $f9, 0x0228($a0)
    jal   0x0012ED80               # MGDraw
    sw    $zero, 0x0240($a0)       # (delay) its world matrix rebuilt
    lw    $a0, 0x0004($sp)
    lui   $a1, 0x01FB
    jal   0x001285A0               # its record's turn back
    addiu $a1, $a1, -0x0370        # (delay) 0x01FAFC90 CodeCaves.AtlaRootQuat
    lw    $a0, 0x0004($sp)
    lui   $t7, 0x3F80
    sw    $t7, 0x0210($a0)
    sw    $t7, 0x0214($a0)
    sw    $t7, 0x0218($a0)         # its scale back
    sw    $zero, 0x0240($a0)
    lw    $ra, 0x0000($sp)
    jr    $ra
    addiu $sp, $sp, 0x40
