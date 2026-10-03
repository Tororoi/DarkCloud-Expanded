# shot_spear_block.s — Babel's risen spear / the Cactus spike stop enemy projectiles as a wall does. Assembled at 0x001B5350
# (DebugIfCave.ShotSpearBlock, in dead DebugInfomationIF). Takes Step__12CSHOT_EFFECT's `jal checkCollision` (main 0x1AC3E8,
# ElfWeaponPatches.PatchSpearBlock): the engine's own test runs first, unchanged; when it met nothing, the shot is not the
# player's (a3, the config's victim mask, ≠ 2 — Ungaga's charge effect and every player shot pass 2 and go through), and the
# mailbox column is armed, the shot's next position (pos + velocity) inside (column r + the shot's radius) of the axis and
# between the floor − 2 and the column's top is a WALL hit: result 1, the contact point the shot's current position.
#
# checkCollision(f12 = the shot's radius, a0 = &out point, a1 = &position, a2 = &velocity, a3 = mode) → 0 nothing, 1 wall,
# 2 the player, 3 an enemy. Mailbox (0x01FAFAE0, CodeCaves.SpearBlock): +0 flag  +4 x  +8 floor  +0xC y  +0x10 r  +0x14 top.

    addiu $sp, $sp, -0x30
    sw    $ra, 0x0010($sp)
    sw    $a0, 0x0014($sp)
    sw    $a1, 0x0018($sp)
    sw    $a2, 0x001C($sp)
    sw    $a3, 0x0020($sp)
    swc1  $f12, 0x0024($sp)
    jal   0x001AB740               # checkCollision, the displaced call
    nop
    bne   $v0, $zero, out          # it met something already
    nop
    lw    $t3, 0x0020($sp)
    addiu $t4, $zero, 2
    beq   $t3, $t4, out            # a player shot (victim mask 2): passes
    nop
    lui   $t0, 0x01FB
    lw    $t1, -0x0520($t0)        # the column's flag (0x01FAFAE0)
    beq   $t1, $zero, out
    nop
    lw    $a1, 0x0018($sp)
    lw    $a2, 0x001C($sp)
    lwc1  $f0, 0x0000($a1)         # position
    lwc1  $f1, 0x0004($a1)
    lwc1  $f2, 0x0008($a1)
    lwc1  $f4, 0x0000($a2)         # + velocity = next
    lwc1  $f5, 0x0004($a2)
    lwc1  $f6, 0x0008($a2)
    add.s $f4, $f0, $f4
    add.s $f5, $f1, $f5
    add.s $f6, $f2, $f6
    lwc1  $f8, -0x0518($t0)        # the floor
    lui   $t7, 0x4000              # 2.0
    mtc1  $t7, $f9
    sub.s $f8, $f8, $f9
    .word 0x46082834               # c.lt.s $f5,$f8 — below the floor − 2?  (EE cond 0x34; keystone's c.lt.s emits the MIPS 0x3C)
    nop
    bc1t  out
    nop
    lwc1  $f9, -0x050C($t0)        # the column's top
    .word 0x46054834               # c.lt.s $f9,$f5 — above the top? (EE cond 0x34)
    nop
    bc1t  out
    nop
    lwc1  $f14, -0x051C($t0)       # axis x
    lwc1  $f15, -0x0514($t0)       # axis y
    lwc1  $f16, -0x0510($t0)       # r
    lwc1  $f12, 0x0024($sp)
    add.s $f16, $f16, $f12         # r + the shot's radius
    mul.s $f16, $f16, $f16
    sub.s $f4, $f4, $f14
    sub.s $f6, $f6, $f15
    mul.s $f4, $f4, $f4
    mul.s $f6, $f6, $f6
    add.s $f4, $f4, $f6            # distance², next to the axis
    .word 0x46048034               # c.lt.s $f16,$f4 — outside? (EE cond 0x34)
    nop
    bc1t  out
    nop
    lw    $a0, 0x0014($sp)         # a wall: stopped where it is
    swc1  $f0, 0x0000($a0)
    swc1  $f1, 0x0004($a0)
    swc1  $f2, 0x0008($a0)
    addiu $v0, $zero, 1
out:
    lw    $ra, 0x0010($sp)
    addiu $sp, $sp, 0x30
    jr    $ra
    nop
