# crystal_home.s — one flying ice arrow steered after the Ice Queen's korinoya. Assembled at 0x0010B300 (DeadDiskReadyCave.
# CrystalHome: the dead sceCdDiskReady body); crystal_shots.s calls it with a0 = the pack object, a1 = the sub-shot.
# The korinoya re-aims at the player while he is within 60 degrees of where it points (`_CHK_USER_INNER_PRODUCT(60)`) and turns
# its body towards him by a fixed 0.12 radian a frame (`_SET_ROTATION(…, 0.12)`: AngleInterpolate's fixed step); outside the cone it
# flies on as it points. Here the arrow points along its flight (velocity +0x9F40 + sub-shot × 0x10): the target is the player
# 10.1 above his feet (CharaMain +0x10); within the cone the flight turns towards it by a 0.12 chord (snapping when closer) and the
# model is faced along it as CSHOT_EFFECT::Set faces it (LookAtMatrixZ, CFrame::SetTransMatrix); either way the flight runs at the
# config's flight speed (+0x1C). Comparisons go by the sign bit of a difference (keystone's c.lt.s carries the wrong condition code).

    addiu $sp, $sp, -0x70
    sw    $ra, 0x0000($sp)
    sw    $s1, 0x0008($sp)
    sw    $s2, 0x000C($sp)
    sll   $t0, $a1, 4
    addu  $t0, $t0, $a0
    lui   $at, 0x0001
    addu  $s1, $t0, $at            # velocity − 0x10000 + 0x9F40: at −0x60C0
    addiu $t0, $zero, 0x11B0
    mult  $a1, $t0
    mflo  $t0
    addu  $s2, $a0, $t0            # the sub-shot's character, at +0x11C0
    lw    $t0, 0x0000($a0)         # the config
    lwc1  $f15, 0x001C($t0)        # its flight speed
    addu  $t7, $zero, $zero        # 1 once the arrow has turned: its model is faced anew
    lwc1  $f0, -0x60C0($s1)        # the flight
    lwc1  $f1, -0x60BC($s1)
    lwc1  $f2, -0x60B8($s1)
    mul.s $f10, $f0, $f0
    mul.s $f11, $f1, $f1
    add.s $f10, $f10, $f11
    mul.s $f11, $f2, $f2
    add.s $f10, $f10, $f11
    sqrt.s $f10, $f10
    mfc1  $t0, $f10
    beq   $t0, $zero, ret          # no direction to keep
    nop
    div.s $f0, $f0, $f10           # u: where it points
    div.s $f1, $f1, $f10
    div.s $f2, $f2, $f10
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
    sqrt.s $f9, $f9                # |d| (never 0: the arrow's contact ends it 13 short of him)
    div.s $f3, $f3, $f9            # w = d / |d|
    div.s $f4, $f4, $f9
    div.s $f5, $f5, $f9
    mul.s $f12, $f0, $f3
    mul.s $f13, $f1, $f4
    add.s $f12, $f12, $f13
    mul.s $f13, $f2, $f5
    add.s $f12, $f12, $f13         # u · w: the cosine
    lui   $t0, 0x3F00              # 0.5: 60 degrees
    mtc1  $t0, $f13
    sub.s $f12, $f12, $f13
    mfc1  $t0, $f12
    bltz  $t0, fly                 # outside the cone: on as it points
    nop
    sub.s $f6, $f3, $f0            # e = w − u
    sub.s $f7, $f4, $f1
    sub.s $f8, $f5, $f2
    mul.s $f10, $f6, $f6
    mul.s $f11, $f7, $f7
    add.s $f10, $f10, $f11
    mul.s $f11, $f8, $f8
    add.s $f10, $f10, $f11
    sqrt.s $f10, $f10              # |e|: the chord still to turn
    lui   $t0, 0x3DF5
    ori   $t0, $t0, 0xC28F         # 0.12
    mtc1  $t0, $f11
    sub.s $f12, $f11, $f10
    mfc1  $t0, $f12
    bltz  $t0, step
    div.s $f11, $f11, $f10         # (delay) the share of e to turn: 0.12 / |e| …
    lui   $t0, 0x3F80              # … or, within a step, all of it: face him
    mtc1  $t0, $f11
step:
    mul.s $f6, $f6, $f11
    mul.s $f7, $f7, $f11
    mul.s $f8, $f8, $f11
    add.s $f0, $f0, $f6            # u + e × the share, normalised
    add.s $f1, $f1, $f7
    add.s $f2, $f2, $f8
    mul.s $f10, $f0, $f0
    mul.s $f11, $f1, $f1
    add.s $f10, $f10, $f11
    mul.s $f11, $f2, $f2
    add.s $f10, $f10, $f11
    sqrt.s $f10, $f10
    div.s $f0, $f0, $f10
    div.s $f1, $f1, $f10
    div.s $f2, $f2, $f10
    swc1  $f0, 0x0060($sp)         # the new heading (16-aligned)
    swc1  $f1, 0x0064($sp)
    swc1  $f2, 0x0068($sp)
    addiu $t7, $zero, 1
fly:
    mul.s $f6, $f0, $f15           # the flight along the heading, at its speed
    mul.s $f7, $f1, $f15
    mul.s $f8, $f2, $f15
    swc1  $f6, -0x60C0($s1)
    swc1  $f7, -0x60BC($s1)
    beq   $t7, $zero, ret
    swc1  $f8, -0x60B8($s1)        # (delay)
    addiu $a0, $sp, 0x0020         # the matrix (16-aligned)
    jal   0x001236D0               # LookAtMatrixZ(matrix, heading)
    addiu $a1, $sp, 0x0060         # (delay)
    lw    $a0, 0x127C($s2)         # the character's frame (+0x11C0 + 0xBC; Set faces it the same way)
    jal   0x00128560               # CFrame::SetTransMatrix(frame, matrix)
    addiu $a1, $sp, 0x0020         # (delay)
ret:
    lw    $ra, 0x0000($sp)
    lw    $s1, 0x0008($sp)
    lw    $s2, 0x000C($sp)
    jr    $ra
    addiu $sp, $sp, 0x70
