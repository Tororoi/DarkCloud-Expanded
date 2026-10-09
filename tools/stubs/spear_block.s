# spear_block.s — a solid sphere enemies cannot walk through (Babel's Spear's risen copy). Assembled at 0x001B5030
# (DebugIfCave.SpearBlock, in dead DebugInfomationIF). Takes Step__12CMonstorUnit's `jal MoveChecMonster` (main 0x1DE344,
# ElfWeaponPatches.PatchSpearBlock): the engine's own enemy-versus-enemy block runs first, unchanged; then, while the
# mailbox sphere is armed, the same test is made against it — the unit's next position (pos + dir × speed) within
# (sphere r + the unit's move radius) of the sphere's axis, and the unit heading toward it. A hit does not stop the unit
# (MoveChecMonster's answer for another unit, which left enemies stuck against the spear): its heading loses the part
# pointing into the column and keeps the part along it, renormalised at the same speed, so it slides round the spear the
# way the collision polys slide it along a wall — and the script, re-aiming at its target each frame, is carried past.
# Head-on (nothing left along it) it takes the perpendicular. f0–f19 are caller-saved; f20+ are left alone.
#
# Mailbox sphere (0x01FAFAE0, CodeCaves.SpearBlock): +0 flag (int, 0 = off)  +4 x  +8 height (unused: a column)  +0xC y  +0x10 r
# CMainMonstorUnit (a0 = this at the site): +0x90 the unit's slot; per-slot record = this + slot×0x190: +0x1E418 move radius,
# +0x1E430/34/38 move direction, +0x1E450 speed; the unit's CCharacter = this + slot×0x3510 + 0x1FCD0, position +0x10 (x, h, y).

    addiu $sp, $sp, -0x20
    sw    $ra, 0x0010($sp)
    sw    $a0, 0x0014($sp)
    jal   0x001DD140               # MoveChecMonster(this), the displaced call
    nop
    lui   $t0, 0x01FB
    lw    $t1, -0x0520($t0)        # the sphere's flag (0x01FAFAE0)
    beq   $t1, $zero, out
    nop
    lw    $a0, 0x0014($sp)         # this
    lw    $t2, 0x0090($a0)         # slot
    addiu $t3, $zero, 0x0190
    mult  $t2, $t3
    mflo  $t3
    addu  $t3, $a0, $t3            # the slot's record
    lui   $t4, 0x0002
    addiu $t4, $t4, -0x1BB0        # 0x1E450
    addu  $t4, $t3, $t4            # &speed; direction at -0x20, radius at -0x38
    lwc1  $f10, 0x0000($t4)        # speed
    mtc1  $zero, $f11
    nop                            # (mtc1's latency: the next FPU op would read the old $f11)
    c.eq.s $f10, $f11
    nop
    bc1t  out                      # not moving (or already blocked): nothing to do
    nop
    addiu $t5, $zero, 0x3510
    mult  $t2, $t5
    mflo  $t5
    addu  $t5, $a0, $t5
    lui   $t6, 0x0002
    addiu $t6, $t6, -0x0320        # 0x1FCE0 = the CCharacter's position
    addu  $t5, $t5, $t6
    lwc1  $f0, 0x0000($t5)         # unit x
    lwc1  $f2, 0x0008($t5)         # unit y
    lwc1  $f4, -0x0020($t4)        # dir x
    lwc1  $f6, -0x0018($t4)        # dir y
    mul.s $f12, $f4, $f10
    add.s $f12, $f0, $f12          # next x
    mul.s $f13, $f6, $f10
    add.s $f13, $f2, $f13          # next y
    lwc1  $f14, -0x051C($t0)       # sphere x
    lwc1  $f15, -0x0514($t0)       # sphere y
    lwc1  $f16, -0x0510($t0)       # sphere r
    sub.s $f12, $f12, $f14
    sub.s $f13, $f13, $f15
    mul.s $f12, $f12, $f12
    mul.s $f13, $f13, $f13
    add.s $f12, $f12, $f13         # distance², next to the axis
    lwc1  $f17, -0x0038($t4)       # the unit's move radius
    add.s $f16, $f16, $f17
    mul.s $f16, $f16, $f16         # (r + R)²
    .word 0x460C8034               # c.lt.s $f16,$f12 — (r+R)² < d²: outside?  (EE cond 0x34; keystone's c.lt.s emits the MIPS 0x3C, not an R5900 condition)
    nop
    bc1t  out
    nop
    sub.s $f18, $f14, $f0          # toward the sphere …
    sub.s $f19, $f15, $f2
    mul.s $f18, $f18, $f4          # … dotted with the heading
    mul.s $f19, $f19, $f6
    add.s $f18, $f18, $f19
    .word 0x46125834               # c.lt.s $f11,$f18 — 0 < dot: heading into it? (EE cond 0x34, as above)
    nop
    bc1f  out                      # heading away (or across): let it move
    nop
    sub.s $f18, $f14, $f0          # blocked: v = sphere − unit (x)
    sub.s $f19, $f15, $f2          #                          (y)
    mul.s $f16, $f18, $f18
    mul.s $f1,  $f19, $f19
    add.s $f16, $f16, $f1          # v·v
    mul.s $f17, $f4, $f18
    mul.s $f1,  $f6, $f19
    add.s $f17, $f17, $f1          # d·v
    div.s $f17, $f17, $f16         # k = d·v / v·v
    mul.s $f1,  $f17, $f18
    sub.s $f12, $f4, $f1           # d' = d − k·v : the part along the column (x)
    mul.s $f1,  $f17, $f19
    sub.s $f13, $f6, $f1           #                                         (y)
    mul.s $f5,  $f12, $f12
    mul.s $f1,  $f13, $f13
    add.s $f5,  $f5, $f1           # |d'|²
    lui   $t7, 0x3C23
    ori   $t7, $t7, 0xD70A         # 0.01
    mtc1  $t7, $f7
    nop                            # (mtc1's latency)
    .word 0x46072834               # c.lt.s $f5,$f7 — head-on? (EE cond 0x34)
    nop
    bc1f  norm
    nop
    neg.s $f12, $f19               # head-on: the perpendicular (−vy, vx), |·|² = v·v
    mov.s $f13, $f18
    mov.s $f5,  $f16
norm:
    .word 0x46050144               # sqrt.s $f5, $f5 (EE: the operand in ft — keystone's form reads $f0)
    div.s $f12, $f12, $f5
    div.s $f13, $f13, $f5
    swc1  $f12, -0x0020($t4)       # the new heading (x, y); height and speed untouched
    swc1  $f13, -0x0018($t4)
out:
    lw    $ra, 0x0010($sp)
    addiu $sp, $sp, 0x20
    jr    $ra
    nop
