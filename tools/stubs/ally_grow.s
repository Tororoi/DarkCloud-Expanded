# ally_grow.s — the summoned ally appears small and grows, overshooting a little before it settles (Cactus' Desert Bloom timing:
# 1/30 of its size to 9.2 frames, 1.133× at 16, 1× at 18.3). Assembled at 0x0011EF4C (DeadMcCave, after ally_follow.s's turn).
# ally_brain.s calls it once an unpaused frame while the ally stands, a0 = its block: the block's frame count (+0x74, zeroed by the
# summon) picks the frame's scale from the table, written to its unit 0's render scale (CCharacter +0x90 x/y/z: the manager's
# +0x1FD60, the block's +0x1FE60); from frame 20 on nothing is written. t0..t5.

grow:
    lw    $t0, 0x74($a0)          # its frames since the summon
    sltiu $t1, $t0, 20
    beq   $t1, $zero, gdone
    addiu $t2, $t0, 1             # (delay)
    sw    $t2, 0x74($a0)
    sll   $t0, $t0, 2
    lui   $t3, 0x0012
    addiu $t3, $t3, -0x106C       # the table (0x0011EF94)
    addu  $t3, $t3, $t0
    lw    $t4, 0x00($t3)
    lui   $t5, 0x0002
    ori   $t5, $t5, 0xFE60
    addu  $t5, $a0, $t5          # its render scale
    sw    $t4, 0x00($t5)
    sw    $t4, 0x04($t5)
    sw    $t4, 0x08($t5)
gdone:
    jr    $ra
    nop

# The scale a frame (frames 0..19).
table:
    .word 0x3D088889               # frame  0: 0.0333
    .word 0x3D088889               # frame  1: 0.0333
    .word 0x3D088889               # frame  2: 0.0333
    .word 0x3D088889               # frame  3: 0.0333
    .word 0x3D088889               # frame  4: 0.0333
    .word 0x3D088889               # frame  5: 0.0333
    .word 0x3D088889               # frame  6: 0.0333
    .word 0x3D088889               # frame  7: 0.0333
    .word 0x3D088889               # frame  8: 0.0333
    .word 0x3D088889               # frame  9: 0.0333
    .word 0x3E2B7FCB               # frame 10: 0.1675
    .word 0x3EA82B4B               # frame 11: 0.3285
    .word 0x3EFA96B0               # frame 12: 0.4894
    .word 0x3F26810A               # frame 13: 0.6504
    .word 0x3F4FB6BD               # frame 14: 0.8114
    .word 0x3F78EC70               # frame 15: 0.9724
    .word 0x3F911111               # frame 16: 1.1333
    .word 0x3F89C09C               # frame 17: 1.0762
    .word 0x3F827027               # frame 18: 1.0190
    .word 0x3F800000               # frame 19: 1.0000
