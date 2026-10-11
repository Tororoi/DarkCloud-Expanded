# atla_shot.s — the Atla Gemron's shot (config 36, ElfSpeciesPatches.PatchAtlaShot) driven before each step: its hit sphere grows
# and rides forward with the burst's orbs. Assembled at 0x0010A998 (DeadCdCave.AtlaShot: the dead sceCdGetToc body's tail, running on
# into the dead sceCdSeek body, DeadSeekCave). crystal_shots.s ends in a jump here instead of Step__12CSHOT_EFFECT (a0 = the monster
# pack's object); for any other config this goes straight on to Step.
#
# The shot has one phase (its motion 0: e503ex_atall's key 9 tail, frames 221–250 at 0.35) and its config holds the rest in the
# phases it never reaches:
#  · its hit radius (radius[0], +0x28), written here every step — the largest of its live sub-shots' (the config is shared):
#    r = 1 + (frame − 224.5) × 17 / 8.75 held to 1 … 18 (attack frames 140 → 145 at the script's 0.2: 10 → 35 game frames after
#    the fire), from k = radius[1] (+0x2C), c = radius[2] (+0x30), 1.0 = radius[3] (+0x34), 18.0 = speed[1] (+0x1C);
#  · its flight: Set gives the sub-shot speed[0] (+0x18, 2^-20) along its aim, and each live sub-shot is moved here by that velocity ×
#    speed[2] (+0x20, the flight a frame / 2^-20) through CCharacter::SetPosition — every frame, whatever it meets (the engine holds a
#    shot that touches the player or a wall; the pack's root backs off the flight a frame by frame, so the burst stays where it was
#    fired only while the shot keeps moving).
# A regular monster's shot has two sub-shots (SetupBaseModel: six only for a monster that cannot be locked on).
# The frame keeps ra, a0, s0, s1 and f20; f0–f9 and t-registers only besides.

    lw    $t0, 0x0000($a0)         # its config
    lui   $t1, 0x0010
    ori   $t1, $t1, 0xA2D0         # 0x0010A2D0 DeadChainCave.AtlaShotConfig
    bne   $t0, $t1, step
    nop
    addiu $sp, $sp, -0x40
    sw    $ra, 0x0000($sp)
    sw    $s0, 0x0004($sp)
    sw    $s1, 0x0008($sp)
    sw    $a0, 0x000C($sp)
    swc1  $f20, 0x0010($sp)
    move  $s0, $a0                 # the object
    addu  $s1, $zero, $zero        # the sub-shot
    mtc1  $zero, $f20              # the radius: 0 with none live
sub:
    sll   $t0, $s1, 1
    addu  $t0, $t0, $s0
    lui   $at, 0x0001
    addu  $t0, $t0, $at
    lh    $t0, -0x6000($t0)        # active (+0xA000)
    beq   $t0, $zero, next
    addiu $t1, $zero, 0x11B0       # (delay)
    mult  $s1, $t1
    mflo  $a0
    addu  $a0, $a0, $s0
    addiu $a0, $a0, 0x11C0         # its character (+0x11C0 + sub × 0x11B0)
    lui   $t2, 0x0010
    ori   $t2, $t2, 0xA2D0         # the config
    lwc1  $f0, 0x02F0($a0)         # its motion's frame
    lwc1  $f1, 0x002C($t2)         # k
    lwc1  $f2, 0x0030($t2)         # c
    lwc1  $f3, 0x0034($t2)         # 1.0
    lwc1  $f4, 0x001C($t2)         # 18.0
    mul.s $f0, $f0, $f1
    add.s $f0, $f0, $f2
    .word 0x460300A8               # max.s $f2, $f0, $f3: no smaller than 1
    .word 0x460410A9               # min.s $f2, $f2, $f4: no larger than 18
    .word 0x4602A528               # max.s $f20, $f20, $f2: the largest live one
    lwc1  $f5, 0x0020($t2)         # the flight over its speed
    sll   $t3, $s1, 4
    addu  $t3, $t3, $s0
    lui   $at, 0x0001
    addu  $t3, $t3, $at            # t3 − 0x60C0 = its velocity (+0x9F40 + sub × 0x10)
    lwc1  $f6, -0x60C0($t3)
    lwc1  $f7, -0x60BC($t3)
    lwc1  $f8, -0x60B8($t3)
    mul.s $f6, $f6, $f5
    mul.s $f7, $f7, $f5
    mul.s $f8, $f8, $f5
    lwc1  $f0, 0x0010($a0)         # its position
    lwc1  $f1, 0x0014($a0)
    lwc1  $f2, 0x0018($a0)
    add.s $f0, $f0, $f6
    add.s $f1, $f1, $f7
    add.s $f2, $f2, $f8
    swc1  $f0, 0x0020($sp)
    swc1  $f1, 0x0024($sp)
    swc1  $f2, 0x0028($sp)
    jal   0x001390E0               # CCharacter::SetPosition(float *): its frame too
    addiu $a1, $sp, 0x0020         # (delay)
next:
    addiu $s1, $s1, 1
    slti  $t0, $s1, 2
    bne   $t0, $zero, sub
    nop
    lui   $t2, 0x0010
    ori   $t2, $t2, 0xA2D0
    swc1  $f20, 0x0028($t2)        # radius[0]
    lw    $ra, 0x0000($sp)
    lw    $s0, 0x0004($sp)
    lw    $s1, 0x0008($sp)
    lw    $a0, 0x000C($sp)
    lwc1  $f20, 0x0010($sp)
    addiu $sp, $sp, 0x40
step:
    j     0x001AC180               # Step__12CSHOT_EFFECT, as before
    nop
