# ally_settle.s — the summoned ally's last move finished before it idles. Assembled at 0x0010B5A8 (DeadCdTypeCave: the dead sceCdGetDiskType
# and sceCdGetDiskStatus bodies). ally_brain.s calls it each frame it has no enemy, a0 = its manager: v0 = 1 while its AI is still in a
# move of its own (it was running — AllyHold 0 — and its clip is neither idle nor walk), so the brain leaves it running; its "player"
# (pointer-table entry 16, the block's aim point) is then put 32 times as far past the summoner as it stands from them, so its AI's
# next choice is a walk toward the summoner rather than another swing at where its enemy was. v0 = 0 once the clip is idle or walk (its
# AI between moves), after 180 frames at the most, or when it already idles: AllyHold set, the brain parks it and walks it beside the
# summoner. Block + 0x78 counts the frames spent finishing. t0..t8, f0..f5.

settle:
    lui   $t4, 0x01FB
    lw    $t5, -0x3C($t4)          # AllyHold: already idling — nothing of its own to finish
    bne   $t5, $zero, sfree
    lui   $t0, 0x0002              # (delay)
    addu  $t0, $a0, $t0
    lw    $t1, 0x0938($t0)         # its clip (unit 0's CCharacter + 0xC68: manager + 0x20938)
    sltiu $t2, $t1, 2
    bne   $t2, $zero, sfree        # idle (0) or walk (1): its AI is between moves
    lw    $t3, -0x88($a0)          # (delay) frames spent finishing (block + 0x78)
    addiu $t3, $t3, 1
    slti  $t2, $t3, 180
    beq   $t2, $zero, sfree        # 3 s at the most
    sw    $t3, -0x88($a0)          # (delay)
    lui   $t6, 0x01EA
    lwc1  $f0, 0x1D30($t6)         # the summoner's x
    lwc1  $f1, 0x1D38($t6)         #                y
    lwc1  $f2, 0x1D34($t6)         #                height
    lwc1  $f3, -0x0320($t0)        # its x (manager + 0x1FCE0)
    lwc1  $f4, -0x0318($t0)        #     y
    lui   $t7, 0x4200
    mtc1  $t7, $f5                 # 32.0
    sub.s $f0, $f0, $f3
    sub.s $f1, $f1, $f4
    mul.s $f0, $f0, $f5
    mul.s $f1, $f1, $f5
    add.s $f0, $f0, $f3            # its position + 32 × (the summoner − it)
    add.s $f1, $f1, $f4
    swc1  $f0, -0x80($a0)          # the block's aim point (+0x80)
    swc1  $f2, -0x7C($a0)
    swc1  $f1, -0x78($a0)
    addiu $t7, $a0, -0x80
    lui   $t8, 0x01F2
    sw    $t7, -0x6FC0($t8)        # PtrTable[16] (0x01F19040)
    jr    $ra
    addiu $v0, $zero, 1            # (delay)
sfree:
    sw    $zero, -0x88($a0)
    addiu $t5, $zero, 1
    sw    $t5, -0x3C($t4)          # AllyHold: its AI parked from here
    jr    $ra
    or    $v0, $zero, $zero        # (delay)
