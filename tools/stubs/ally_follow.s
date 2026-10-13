# ally_follow.s — the summoned ally with no enemy to fight walks beside Ruby. Assembled at 0x001176D8 (DeadSifRpcCave: the dead
# sceSifSetRpcQueue..sceSifRemoveRpcQueue bodies), its turn and clip (after #SPLIT) at 0x0011EEA0 (DeadMcCave, after ally_manager.s).
# ally_brain.s calls it once a frame while its script is parked (CodeCaves.AllyHold, park below), a0 = its block (the manager at + 0x100, its unit 0's
# CCharacter at + 0x1FDD0). The point 16 u to Ruby's right is written to the block's idle point (+0x30, what its pointer-table entry names
# while it idles); its height is the floor's (Step's ground keeps it). Farther than 3 u it walks there (clip 1, its rate 0.68 × its speed:
# d × 0.08 held to 0.5..2.4 u a frame, a step no longer than a sixth of the way; clip 16, its run, at its own rate while Ruby runs —
# her motion 1), facing the point; there, it idles (clip 0 at its own
# rate) facing Ruby's way. The facing is its unit's own turn (Step's, as _SET_ROTATION asks it), never a write to the model.

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
    lui   $t0, 0x0001
    ori   $t0, $t0, 0xFDD0         # (ori is zero-extended: the plain upper half)
    addu  $s1, $s0, $t0            # its CCharacter (block + 0x1FDD0)
    lui   $t0, 0x01EA
    lwc1  $f20, 0x1D84($t0)        # Ruby's yaw
    jal   0x0011D6B0               # cosf
    mov.s $f12, $f20
    mov.s $f21, $f0
    jal   0x0011D8A0               # sinf
    mov.s $f12, $f20
    lui   $t0, 0x4180
    mtc1  $t0, $f1                 # 16.0
    lui   $t0, 0x01EA
    lwc1  $f2, 0x1D30($t0)         # Ruby x
    lwc1  $f3, 0x1D34($t0)         #      height
    lwc1  $f4, 0x1D38($t0)         #      y
    mul.s $f5, $f21, $f1
    sub.s $f5, $f2, $f5            # x − cos·16
    mul.s $f6, $f0, $f1
    add.s $f6, $f4, $f6            # y + sin·16: her right (facing is (sin, cos))
    swc1  $f5, 0x30($s0)           # the idle point
    swc1  $f3, 0x34($s0)
    swc1  $f6, 0x38($s0)
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
    lui   $t0, 0x4120
    mtc1  $t0, $f9                 # 10.0
    lui   $t0, 0xBF80
    mtc1  $t0, $f12                # its clip's own rate
    mul.s $f10, $f0, $f9
    add.s $f13, $f7, $f10          # it faces Ruby's way: a point 10 u ahead of it along her facing (x + sin·10…
    mul.s $f10, $f21, $f9
    add.s $f14, $f8, $f10          # …y + cos·10)
    or    $a0, $zero, $zero        # clip 0 (idle)
    bal   clip
    nop
    b     turn
    nop
walk:
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
    addiu $a0, $zero, 1            # clip 1 (walk)…
    lui   $t0, 0x01EA
    lw    $t1, 0x2988($t0)         # Ruby's motion
    addiu $t2, $zero, 1            # her run
    bne   $t1, $t2, wclip
    lui   $t0, 0xBF80              # (delay)
    mtc1  $t0, $f12                # …or, while Ruby runs, clip 16 (its run) at its own rate
    addiu $a0, $zero, 16
wclip:
    bal   clip
    lwc1  $f13, 0x30($s0)          # (delay) it faces where it is going: the idle point
    lwc1  $f14, 0x38($s0)
    b     turn
    nop

# a0 = its manager: its unit 0's script parked on a YIELD (ally_grow.s's records, 0x0011EFE4) and resumed there, not restarted
# (ScriptRunning 1), so Step goes on moving, animating and hitting it while its AI waits; its own movement stopped.
park:
    lui   $t0, 0x0005
    ori   $t0, $t0, 0x4E00         # unit 0's CRunScript PC (manager + 0x54DD0 + 0x30)
    addu  $t0, $a0, $t0
    lui   $t1, 0x0012
    addiu $t1, $t1, -0x101C        # the YIELD records (0x0011EFE4)
    sw    $t1, 0x00($t0)
    addiu $t1, $zero, 1
    sw    $t1, 0x50($a0)           # ScriptRunning[0]
    lui   $t2, 0x0002
    addu  $t2, $a0, $t2
    jr    $ra
    sw    $zero, -0x1BB0($t2)      # (delay) unit 0's movement_speed (manager + 0x1E450)

# a0 = its manager: its AI back — label 100 from the top on its next Step — and its clips at their own rate.
release:
    lui   $t0, 0x0005
    ori   $t0, $t0, 0x4E00
    addu  $t0, $a0, $t0
    sw    $zero, 0x00($t0)         # nothing to resume
    sw    $zero, 0x08($t0)         # no command outstanding (+0x38)
    sw    $zero, 0x50($a0)         # ScriptRunning[0]: run afresh
    lui   $t1, 0xBF80
    lui   $t2, 0x0002
    addu  $t2, $a0, $t2
    jr    $ra
    sw    $t1, 0x0930($t2)         # (delay) unit 0's rate override off (CCharacter + 0xC60 = manager + 0x20930)
#SPLIT
# f13 / f14 = the point it is to face: its unit's own turn (MONSTOR +0x70 the point, +0x84 the rate — what _SET_ROTATION sets; Step
# turns the model toward it through the model's own rotation and clears the rate once it faces it), 0.3 a frame.
turn:
    lui   $t0, 0x0001
    ori   $t0, $t0, 0xE540         # its unit's turn point (block + 0x1E540 = manager + 0x1E440)
    addu  $t0, $s0, $t0
    swc1  $f13, 0x00($t0)
    lwc1  $f1, 0x14($s1)
    swc1  $f1, 0x04($t0)           # (its own height: the turn reads x and y)
    swc1  $f14, 0x08($t0)
    lui   $t1, 0x3E99
    ori   $t1, $t1, 0x999A
    sw    $t1, 0x14($t0)           # the rate (+0x84), re-armed each frame
    lw    $ra, 0x00($sp)
    lw    $s0, 0x04($sp)
    lw    $s1, 0x08($sp)
    lwc1  $f20, 0x10($sp)
    lwc1  $f21, 0x14($sp)
    lwc1  $f22, 0x18($sp)
    lwc1  $f23, 0x1C($sp)
    jr    $ra
    addiu $sp, $sp, 0x40

# a0 = the clip (switched with the engine's blend when it changes), f12 = its rate (−1 = its own).
clip:
    lw    $t0, 0xC68($s1)
    beq   $t0, $a0, csame
    or    $t1, $zero, $zero        # (delay) looped, blended in as any clip change
    sw    $a0, 0xC68($s1)
    sw    $t1, 0xC64($s1)
csame:
    jr    $ra
    swc1  $f12, 0xC60($s1)
