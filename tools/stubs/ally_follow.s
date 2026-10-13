# ally_follow.s — the summoned ally with no enemy to fight walks beside Ruby. Assembled at 0x001176D8 (DeadSifRpcCave: the dead
# sceSifSetRpcQueue..sceSifRemoveRpcQueue bodies), its turn and clip (after #SPLIT) at 0x0011EEA0 (DeadMcCave, after ally_manager.s).
# ally_brain.s calls it once a frame while the ally's Step is held (CodeCaves.AllyHold), a0 = its block (the manager at + 0x100, its unit 0's
# CCharacter at + 0x1FDD0). The point 14 u to Ruby's right is written to the block's idle point (+0x30, what its pointer-table entry names
# while it idles); its height is Ruby's. Farther than 3 u it walks there (clip 1, its rate 0.68 × its speed: d × 0.08 held to 0.5..2.4 u a
# frame, a step no longer than a sixth of the way), turning to its path; there, it idles (clip 0 at its own rate) turning to Ruby's way.
# Its turn takes 0.3 of what is left each frame.

follow:
    addiu $sp, $sp, -0x40
    sw    $ra, 0x00($sp)
    sw    $s0, 0x04($sp)
    sw    $s1, 0x08($sp)
    swc1  $f20, 0x10($sp)
    swc1  $f21, 0x14($sp)
    swc1  $f22, 0x18($sp)
    swc1  $f23, 0x1C($sp)
    or    $s0, $a0, $zero
    lui   $t0, 0x0002
    ori   $t0, $t0, 0xFDD0
    addu  $s1, $s0, $t0            # its CCharacter
    lui   $t0, 0x01EA
    lwc1  $f20, 0x1D84($t0)        # Ruby's yaw
    jal   0x0011D6B0               # cosf
    mov.s $f12, $f20
    mov.s $f21, $f0
    jal   0x0011D8A0               # sinf
    mov.s $f12, $f20
    lui   $t0, 0x4160
    mtc1  $t0, $f1                 # 14.0
    lui   $t0, 0x01EA
    lwc1  $f2, 0x1D30($t0)         # Ruby x
    lwc1  $f3, 0x1D34($t0)         #      height
    lwc1  $f4, 0x1D38($t0)         #      y
    mul.s $f5, $f21, $f1
    add.s $f5, $f2, $f5            # x + cos·14
    mul.s $f6, $f0, $f1
    sub.s $f6, $f4, $f6            # y − sin·14
    swc1  $f5, 0x30($s0)           # the idle point
    swc1  $f3, 0x34($s0)
    swc1  $f6, 0x38($s0)
    swc1  $f3, 0x14($s1)           # its height is Ruby's
    lwc1  $f7, 0x10($s1)
    sub.s $f22, $f5, $f7           # dx
    lwc1  $f8, 0x18($s1)
    sub.s $f23, $f6, $f8           # dy
    mul.s $f9, $f22, $f22
    mul.s $f10, $f23, $f23
    add.s $f10, $f9, $f10
    .word 0x460A0284               # sqrt.s f10, f10: d
    lui   $t0, 0x4040
    mtc1  $t0, $f11                # 3.0
    nop
    .word 0x460A5834               # c.olt.s f11, f10: farther than 3
    nop
    bc1t  walk
    nop
    lui   $t0, 0xBF80
    mtc1  $t0, $f12                # its clip's own rate
    or    $a0, $zero, $zero        # clip 0 (idle)
    bal   clip
    mov.s $f13, $f20               # (delay) turning to Ruby's way
    b     turn
    nop
walk:
    swc1  $f10, 0x20($sp)
    mov.s $f12, $f22
    jal   0x0011DD40               # atan2f(dx, dy): its path's yaw
    mov.s $f13, $f23
    swc1  $f0, 0x24($sp)
    lwc1  $f10, 0x20($sp)
    lui   $t0, 0x3DA3
    ori   $t0, $t0, 0xD70A
    mtc1  $t0, $f1                 # 0.08
    lui   $t0, 0x3F00
    mtc1  $t0, $f3                 # 0.5
    mul.s $f2, $f10, $f1           # speed = d × 0.08
    .word 0x46031034               # c.olt.s f2, f3
    nop
    bc1t  slow
    lui   $t0, 0x4019              # (delay)
    ori   $t0, $t0, 0x999A
    mtc1  $t0, $f3                 # 2.4
    nop
    .word 0x46021834               # c.olt.s f3, f2
    nop
    bc1f  sok
    nop
slow:
    mov.s $f2, $f3                 # held to 0.5..2.4
sok:
    lui   $t0, 0x40C0
    mtc1  $t0, $f4                 # 6.0
    nop
    div.s $f5, $f10, $f4           # a sixth of the way
    .word 0x46022834               # c.olt.s f5, f2
    mov.s $f6, $f2
    bc1f  stok
    nop
    mov.s $f6, $f5
stok:
    div.s $f7, $f6, $f10           # step / d
    lwc1  $f9, 0x10($s1)
    mul.s $f8, $f22, $f7
    add.s $f9, $f9, $f8
    swc1  $f9, 0x10($s1)
    lwc1  $f9, 0x18($s1)
    mul.s $f8, $f23, $f7
    add.s $f9, $f9, $f8
    swc1  $f9, 0x18($s1)
    lui   $t0, 0x3F2E
    ori   $t0, $t0, 0x147B
    mtc1  $t0, $f1                 # 0.68
    nop
    mul.s $f12, $f2, $f1
    addiu $a0, $zero, 1            # clip 1 (walk)
    bal   clip
    lwc1  $f13, 0x24($sp)          # (delay) turning to its path
    b     turn
    nop
#SPLIT
turn:
    lwc1  $f1, 0x64($s1)           # its yaw
    sub.s $f2, $f13, $f1           # what is left
    lui   $t0, 0x4049
    ori   $t0, $t0, 0x0FDB
    mtc1  $t0, $f3                 # π
    addiu $t1, $zero, 4            # wraps at most (a bad yaw never spins here)
    add.s $f4, $f3, $f3            # 2π
    neg.s $f5, $f3
wrap:
    .word 0x46021834               # c.olt.s f3, f2: past π
    nop
    bc1f  wlow
    nop
    sub.s $f2, $f2, $f4
wlow:
    .word 0x46051034               # c.olt.s f2, f5: past −π
    nop
    bc1f  wnext
    nop
    add.s $f2, $f2, $f4
wnext:
    addiu $t1, $t1, -1
    bne   $t1, $zero, wrap
    lui   $t0, 0x3E99              # (delay)
    ori   $t0, $t0, 0x999A
    mtc1  $t0, $f6                 # 0.3
    nop
    mul.s $f2, $f2, $f6
    add.s $f1, $f1, $f2
    swc1  $f1, 0x64($s1)
    lw    $ra, 0x00($sp)
    lw    $s0, 0x04($sp)
    lw    $s1, 0x08($sp)
    lwc1  $f20, 0x10($sp)
    lwc1  $f21, 0x14($sp)
    lwc1  $f22, 0x18($sp)
    lwc1  $f23, 0x1C($sp)
    jr    $ra
    addiu $sp, $sp, 0x40

# a0 = the clip (restarted when it changes), f12 = its rate (−1 = its own).
clip:
    lw    $t0, 0xC68($s1)
    beq   $t0, $a0, csame
    addiu $t1, $zero, 4            # (delay) a restart
    sw    $a0, 0xC68($s1)
    sw    $t1, 0xC64($s1)
csame:
    jr    $ra
    swc1  $f12, 0xC60($s1)
