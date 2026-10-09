# player_spear_block.s — Babel's risen spear is solid to the PLAYER as well. Assembled at 0x001B5260
# (DebugIfCave.PlayerSpearBlock, in dead DebugInfomationIF). Takes the player's move's two `jal MoveCheck__12CMonstorUnitFPfPfi`
# (dun 0x1DB39AC / 0x1DB3E58, DunPatches): the engine's own player-versus-enemy block runs first, unchanged; then, while the
# mailbox sphere is armed, the player's next position (pos + velocity) within (sphere r + 6 — the engine's player allowance
# against a unit) of the spear's axis, moving toward it, loses the part of its velocity pointing into the column and keeps the
# part along it — sliding round the spear the way a wall slides the player. Moving away (or already inside) is never stopped.
# Height untouched. f0–f19 are caller-saved; f20+ are left alone.
#
# Args of the displaced call: a0 = the monster unit manager, a1 = &the player's position (x, h, y), a2 = &the velocity
# (0x1DC2550: x, h, y), a3 = a flag. Mailbox sphere (0x01FAFAE0, CodeCaves.SpearBlock): +0 flag  +4 x  +8 h  +0xC y  +0x10 r.

    addiu $sp, $sp, -0x20
    sw    $ra, 0x0010($sp)
    sw    $a1, 0x0014($sp)
    sw    $a2, 0x0018($sp)
    jal   0x001DC820               # MoveCheck__12CMonstorUnitFPfPfi, the displaced call
    nop
    lui   $t0, 0x01FB
    lw    $t1, -0x0520($t0)        # the sphere's flag (0x01FAFAE0)
    beq   $t1, $zero, out
    nop
    lw    $a1, 0x0014($sp)
    lw    $a2, 0x0018($sp)
    lwc1  $f0, 0x0000($a1)         # player x
    lwc1  $f2, 0x0008($a1)         # player y
    lwc1  $f4, 0x0000($a2)         # velocity x
    lwc1  $f6, 0x0008($a2)         # velocity y
    add.s $f12, $f0, $f4           # next x
    add.s $f13, $f2, $f6           # next y
    lwc1  $f14, -0x051C($t0)       # sphere x
    lwc1  $f15, -0x0514($t0)       # sphere y
    lwc1  $f16, -0x0510($t0)       # sphere r
    sub.s $f12, $f12, $f14
    sub.s $f13, $f13, $f15
    mul.s $f12, $f12, $f12
    mul.s $f13, $f13, $f13
    add.s $f12, $f12, $f13         # distance², next to the axis
    lui   $t7, 0x40C0              # 6.0
    mtc1  $t7, $f17
    nop                            # (mtc1's latency: the next FPU op would read the old $f17)
    add.s $f16, $f16, $f17
    mul.s $f16, $f16, $f16         # (r + 6)²
    .word 0x460C8034               # c.lt.s $f16,$f12 — (r+6)² < d²: outside?  (EE cond 0x34; keystone's c.lt.s emits the MIPS 0x3C)
    nop
    bc1t  out
    nop
    sub.s $f18, $f14, $f0          # v = sphere − player (x)
    sub.s $f19, $f15, $f2          #                     (y)
    mul.s $f8, $f4, $f18
    mul.s $f9, $f6, $f19
    add.s $f8, $f8, $f9            # velocity · v
    mtc1  $zero, $f11
    nop                            # (mtc1's latency)
    .word 0x46085834               # c.lt.s $f11,$f8 — 0 < dot: moving into it? (EE cond 0x34, as above)
    nop
    bc1f  out                      # moving away (or across): let it move
    nop
    mul.s $f16, $f18, $f18
    mul.s $f1,  $f19, $f19
    add.s $f16, $f16, $f1          # v·v
    div.s $f17, $f8, $f16          # k = velocity·v / v·v
    mul.s $f1,  $f17, $f18
    sub.s $f4,  $f4, $f1           # velocity − k·v : the part along the column (x)
    mul.s $f1,  $f17, $f19
    sub.s $f6,  $f6, $f1           #                                           (y)
    swc1  $f4, 0x0000($a2)
    swc1  $f6, 0x0008($a2)
out:
    lw    $ra, 0x0010($sp)
    addiu $sp, $sp, 0x20
    jr    $ra
    nop
