# crystal_home.s — one flying ice arrow turned towards the player. Assembled at 0x0010B300 (DeadDiskReadyCave.CrystalHome: the
# dead sceCdDiskReady body); crystal_shots.s calls it with a0 = the pack object, a1 = the sub-shot.
# The target is the player 10.1 above his feet (CharaMain +0x10, where the Ice Queen's korinoya aims). Within 60 degrees of the
# flight (cos ≥ 0.5) the flight turns 0.15 of the way towards it, keeping its speed (velocity +0x9F40 + sub-shot × 0x10); the
# model is then faced along it as CSHOT_EFFECT::Set faces it: LookAtMatrixZ(matrix, velocity), CFrame::SetTransMatrix(its frame,
# matrix). Comparisons go by the sign bit of a difference (keystone's c.lt.s carries the wrong condition code for the EE).

    addiu $sp, $sp, -0x70
    sw    $ra, 0x0000($sp)
    sw    $s0, 0x0004($sp)
    sw    $s1, 0x0008($sp)
    sw    $s2, 0x000C($sp)
    move  $s0, $a0
    sll   $t0, $a1, 4
    addu  $t0, $t0, $s0
    lui   $at, 0x0001
    addu  $s1, $t0, $at            # velocity − 0x10000 + 0x9F40: at −0x60C0
    addiu $t0, $zero, 0x11B0
    mult  $a1, $t0
    mflo  $t0
    addu  $s2, $s0, $t0            # the sub-shot's character, at +0x11C0
    lui   $t9, 0x01EA
    ori   $t9, $t9, 0x1D20         # CharaMain
    lwc1  $f3, 0x0010($t9)
    lwc1  $f4, 0x0014($t9)
    lwc1  $f5, 0x0018($t9)
    lui   $t0, 0x4121
    ori   $t0, $t0, 0x999A         # 10.1
    mtc1  $t0, $f6
    add.s $f4, $f4, $f6            # the target
    lwc1  $f6, 0x11D0($s2)         # the arrow
    lwc1  $f7, 0x11D4($s2)
    lwc1  $f8, 0x11D8($s2)
    sub.s $f3, $f3, $f6            # d = target − arrow
    sub.s $f4, $f4, $f7
    sub.s $f5, $f5, $f8
    mul.s $f9, $f3, $f3
    mul.s $f10, $f4, $f4
    add.s $f9, $f9, $f10
    mul.s $f10, $f5, $f5
    add.s $f9, $f9, $f10
    sqrt.s $f9, $f9                # |d|
    lwc1  $f0, -0x60C0($s1)        # v
    lwc1  $f1, -0x60BC($s1)
    lwc1  $f2, -0x60B8($s1)
    mul.s $f10, $f0, $f0
    mul.s $f11, $f1, $f1
    add.s $f10, $f10, $f11
    mul.s $f11, $f2, $f2
    add.s $f10, $f10, $f11
    sqrt.s $f10, $f10              # |v|: the speed
    mul.s $f11, $f9, $f10
    mfc1  $t0, $f11
    beq   $t0, $zero, ret          # no flight or on top of him
    nop
    mul.s $f12, $f0, $f3
    mul.s $f13, $f1, $f4
    add.s $f12, $f12, $f13
    mul.s $f13, $f2, $f5
    add.s $f12, $f12, $f13         # v·d
    div.s $f12, $f12, $f11         # cos
    lui   $t0, 0x3F00              # 0.5: 60 degrees
    mtc1  $t0, $f13
    sub.s $f12, $f12, $f13
    mfc1  $t0, $f12
    bltz  $t0, ret                 # outside the cone: straight on
    nop
    div.s $f0, $f0, $f10           # u = v / |v|
    div.s $f1, $f1, $f10
    div.s $f2, $f2, $f10
    div.s $f3, $f3, $f9            # w = d / |d|
    div.s $f4, $f4, $f9
    div.s $f5, $f5, $f9
    lui   $t0, 0x3E19
    ori   $t0, $t0, 0x999A         # 0.15
    mtc1  $t0, $f13
    sub.s $f3, $f3, $f0            # n = u + 0.15 (w − u)
    sub.s $f4, $f4, $f1
    sub.s $f5, $f5, $f2
    mul.s $f3, $f3, $f13
    mul.s $f4, $f4, $f13
    mul.s $f5, $f5, $f13
    add.s $f0, $f0, $f3
    add.s $f1, $f1, $f4
    add.s $f2, $f2, $f5
    mul.s $f11, $f0, $f0
    mul.s $f12, $f1, $f1
    add.s $f11, $f11, $f12
    mul.s $f12, $f2, $f2
    add.s $f11, $f11, $f12
    sqrt.s $f11, $f11
    div.s $f11, $f10, $f11         # |v| / |n|
    mul.s $f0, $f0, $f11
    mul.s $f1, $f1, $f11
    mul.s $f2, $f2, $f11
    swc1  $f0, -0x60C0($s1)        # the new flight
    swc1  $f1, -0x60BC($s1)
    swc1  $f2, -0x60B8($s1)
    addiu $a0, $sp, 0x0020         # the matrix (16-aligned)
    jal   0x001236D0               # LookAtMatrixZ(matrix, velocity)
    addiu $a1, $s1, -0x60C0        # (delay)
    lw    $a0, 0x127C($s2)         # the character's frame (+0x11C0 + 0xBC)
    beq   $a0, $zero, ret
    nop
    jal   0x00128560               # CFrame::SetTransMatrix(frame, matrix)
    addiu $a1, $sp, 0x0020         # (delay)
ret:
    lw    $ra, 0x0000($sp)
    lw    $s0, 0x0004($sp)
    lw    $s1, 0x0008($sp)
    lw    $s2, 0x000C($sp)
    jr    $ra
    addiu $sp, $sp, 0x70
