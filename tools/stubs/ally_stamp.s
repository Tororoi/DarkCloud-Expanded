# ally_stamp.s — the floor stamp the summoned pockle's block carries (+0x44), so a block left from another floor is never ridden.
# Assembled at 0x0027D460 (the dead SmoothRest body's free tail, DeadSmoothRestCave.AllyStamp). ally_brain.s calls it from its brain
# and its summon. v0 = (dungeon << 16) | (floor << 8) | back floor. t0..t2.

stamp:
    lbu   $t0, -0x625C($gp)        # checkDungeon (0x2A3594)
    lui   $t1, 0x01CE
    lbu   $t1, -0x6AB2($t1)        # checkFloor (0x01CD954E)
    lbu   $t2, -0x633C($gp)        # the back-floor flag (0x2A34B4)
    sll   $t0, $t0, 16
    sll   $t1, $t1, 8
    or    $v0, $t0, $t1
    sltu  $t2, $zero, $t2
    jr    $ra
    or    $v0, $v0, $t2
