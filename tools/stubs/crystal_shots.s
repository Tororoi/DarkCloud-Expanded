# crystal_shots.s — the Crystal Gemron's two shots driven before each step: the ice arrow homes, and an arrow that freezes the
# player brings the ice prison down on him. Assembled at 0x0010BBD0 (DeadApplyNCmdCave.CrystalShots: the dead sceCdApplyNCmd
# body). DunPatches points the monster pack's per-object `jal Step__12CSHOT_EFFECT` (dun 0x1DB86F4; a0 = the object) here; the
# work below is done, then Step runs as before.
#  · Ice arrow (config 34, ElfSpeciesPatches.PatchIceArrowConfig): each flying sub-shot (phase 1) whose target — the player, 10.1
#    above his feet — lies within 60 degrees of its flight turns 0.15 of the way towards it each step, at its own speed, and its
#    model is turned to the new flight (LookAtMatrixZ + CFrame::SetTransMatrix, as CSHOT_EFFECT::Set faces it).
#  · An arrow in its impact (phase 2) within 20 of the player, with the player frozen (UserStatus ailments, Freeze 0x04) and no
#    ice prison standing: the arrow's firing Gemron (+0xA060) is asked for its second shot at the player's feet — its event2 record
#    (NowMonstorUnit + 0x60250 + unit × 0x30: target, position, damage −1, timer 2 last), which CMonstorUnit::Step fires as
#    _SET_SHOT2 would. Once per arrow (CodeCaves.IceArrowFired, a byte per sub-shot, cleared outside the impact).
#  · Ice prison (config 35, the kori): crystal_prison.s holds it while the player is frozen and lets it shatter after.
# The frame keeps ra, a0 and the s-registers used; f0–f19 only.

    addiu $sp, $sp, -0x80
    sw    $ra, 0x0000($sp)
    sw    $s0, 0x0004($sp)
    sw    $s1, 0x0008($sp)
    sw    $s2, 0x000C($sp)
    sw    $s3, 0x0010($sp)
    sw    $a0, 0x0014($sp)
    move  $s0, $a0                 # the object
    lw    $t0, 0x0000($s0)         # its config
    lui   $t1, 0x0011
    ori   $t2, $t1, 0x8650         # the ice arrow's
    beq   $t0, $t2, arrows
    ori   $t2, $t1, 0x86C0         # (delay) the ice prison's
    bne   $t0, $t2, done
    nop
# ── the ice prison: crystal_prison.s holds or breaks it ──
    jal   0x00118740               # crystal_prison.s Prison(a0 = the object)
    move  $a0, $s0                 # (delay)
    b     done
    nop
# ── the ice arrows ──
arrows:
    lui   $s3, 0x01FB
    addiu $s3, $s3, -0x0810        # 0x01FAF7F0 CodeCaves.IceArrowFired
    addu  $s1, $zero, $zero
arrow:
    sll   $t0, $s1, 1
    addu  $t0, $t0, $s0
    lui   $at, 0x0001
    addu  $t0, $t0, $at
    lh    $t1, -0x6000($t0)        # active
    beq   $t1, $zero, arrow_clear
    lh    $t2, -0x6010($t0)        # (delay) phase
    addiu $t3, $zero, 1
    bne   $t2, $t3, arrow_impact
    nop
    move  $a0, $s0
    jal   0x0010B300               # crystal_home.s: home this sub-shot (a0 = the object, a1 = the sub-shot)
    move  $a1, $s1                 # (delay)
    b     arrow_clear              # flying: not fired yet
    nop
arrow_impact:
    addiu $t3, $zero, 2
    bne   $t2, $t3, arrow_clear
    addu  $t4, $s3, $s1            # (delay)
    lbu   $t5, 0x0000($t4)
    bne   $t5, $zero, arrow_next   # this arrow's prison is asked for already
    nop
    jal   0x00118730               # crystal_prison.s Frozen
    nop
    beq   $v0, $zero, arrow_next
    nop
    jal   0x00118738               # crystal_prison.s Standing: an ice prison already up, none more
    nop
    bne   $v0, $zero, arrow_next
    addiu $t0, $zero, 0x11B0       # (delay)
    mult  $s1, $t0
    mflo  $t0
    addu  $s2, $s0, $t0            # the sub-shot's character (+0x11C0)
    lui   $t9, 0x01EA
    ori   $t9, $t9, 0x1D20         # CharaMain
    lwc1  $f0, 0x11D0($s2)         # the arrow: x,
    lwc1  $f1, 0x11D4($s2)         # height,
    lwc1  $f2, 0x11D8($s2)         # y
    lwc1  $f3, 0x0010($t9)         # the player
    lwc1  $f4, 0x0014($t9)
    lwc1  $f5, 0x0018($t9)
    sub.s $f0, $f0, $f3
    sub.s $f1, $f1, $f4
    sub.s $f2, $f2, $f5
    mul.s $f0, $f0, $f0
    mul.s $f1, $f1, $f1
    mul.s $f2, $f2, $f2
    add.s $f0, $f0, $f1
    add.s $f0, $f0, $f2
    lui   $t0, 0x43C8              # 400.0: within 20
    mtc1  $t0, $f1
    nop                            # (mtc1's latency)
    sub.s $f0, $f0, $f1
    mfc1  $t0, $f0
    bgez  $t0, arrow_next          # sign clear: too far (a wall, not him)
    sll   $t0, $s1, 1              # (delay)
    addu  $t0, $t0, $s0
    lui   $at, 0x0001
    addu  $t0, $t0, $at
    lh    $t1, -0x5FA0($t0)        # its Gemron (+0xA060)
    sltiu $t2, $t1, 16
    beq   $t2, $zero, arrow_next
    addiu $t2, $zero, 0x30         # (delay)
    mult  $t1, $t2
    mflo  $t1
    lui   $t2, 0x002A
    lw    $t2, 0x34D0($t2)         # NowMonstorUnit
    beq   $t2, $zero, arrow_next
    addu  $t1, $t1, $t2            # (delay)
    lui   $at, 0x0006
    ori   $at, $at, 0x0250
    addu  $t1, $t1, $at            # its event2
    lui   $t3, 0x3F80              # 1.0
    swc1  $f3, 0x0000($t1)         # the target: his feet
    swc1  $f4, 0x0004($t1)
    swc1  $f5, 0x0008($t1)
    sw    $t3, 0x000C($t1)
    swc1  $f3, 0x0010($t1)         # the position: his feet
    swc1  $f4, 0x0014($t1)
    swc1  $f5, 0x0018($t1)
    sw    $t3, 0x001C($t1)
    addiu $t2, $zero, -1
    sw    $t2, 0x0028($t1)         # its own damage
    addiu $t2, $zero, 2
    sw    $t2, 0x0024($t1)         # timer 2, last: Step fires it
    addu  $t4, $s3, $s1
    addiu $t5, $zero, 1
    b     arrow_next
    sb    $t5, 0x0000($t4)         # (delay) asked for
arrow_clear:
    addu  $t4, $s3, $s1
    sb    $zero, 0x0000($t4)
arrow_next:
    addiu $s1, $s1, 1
    slti  $t0, $s1, 8
    bne   $t0, $zero, arrow
    nop
done:
    lw    $ra, 0x0000($sp)
    lw    $s0, 0x0004($sp)
    lw    $s1, 0x0008($sp)
    lw    $s2, 0x000C($sp)
    lw    $s3, 0x0010($sp)
    lw    $a0, 0x0014($sp)
    j     0x001AC180               # Step__12CSHOT_EFFECT, as before
    addiu $sp, $sp, 0x80
