# ruby_arrows.s — the Crystal Ring's ice arrows homed before the second main-character instance steps. Assembled at 0x00122138
# (DeadClipScreenCave.RubyArrows: the dead sceVu0ClipScreen..sceVpu0Reset bodies, after their `jr ra; li v0,0`). The second-effect
# step cave (ElfConfusePatches.PatchSecondEffect) calls it in place of `jal Step__12CSHOT_EFFECT` for the second instance, a0 = it.
# While CodeCaves.CrystalArrows arms it (+0 ≠ 0), the instance holds the borrowed config (its config pointer is the borrowed-shot
# block's copy, 0x01FAEF50) and the mod has named a target (+4: the guest address of a position, x / height / y), every flying
# sub-shot (active, phase 1) is turned towards that position + (0, +8 height offset, 0) by crystal_home.s's home_to — the Crystal
# Gemron's own steering. Then Step, a0 and ra as the cave gave them. Block (guest 0x01FAE6A0, addressed off t0 = 0x01FB0000):
#   +0 arm   +4 the target's position (guest; 0 = none: they fly on)   +8 the height added to it (float)

arrows:
    lui   $t0, 0x01FB
    lw    $t1, -0x1960($t0)        # armed?
    beq   $t1, $zero, step
    lw    $t2, 0x0000($a0)         # (delay) the instance's config
    lui   $t3, 0x01FB
    addiu $t3, $t3, -0x10B0        # the borrowed-shot block's copy (0x01FAEF50)
    bne   $t2, $t3, step
    lw    $t4, -0x195C($t0)        # (delay) the target
    beq   $t4, $zero, step
    nop
    addiu $sp, $sp, -0x40
    sw    $ra, 0x0000($sp)
    sw    $a0, 0x0004($sp)
    sw    $s0, 0x0008($sp)
    sw    $s1, 0x000C($sp)
    sw    $s2, 0x0010($sp)
    or    $s0, $a0, $zero
    lwc1  $f0, 0x0000($t4)
    lwc1  $f1, 0x0004($t4)
    lwc1  $f2, 0x0008($t4)
    lwc1  $f3, -0x1958($t0)        # the height added
    add.s $f1, $f1, $f3
    swc1  $f0, 0x0020($sp)         # the point aimed at (16-aligned)
    swc1  $f1, 0x0024($sp)
    swc1  $f2, 0x0028($sp)
    lui   $at, 0x0001
    addu  $s2, $s0, $at            # the instance + 0x10000: its per-sub-shot words sit past +0x9FF0
    lw    $t5, -0x5EB4($s2)        # sub-shots (+0xA14C)
    or    $s1, $zero, $zero
next:
    slt   $t6, $s1, $t5
    beq   $t6, $zero, done
    sll   $t7, $s1, 1              # (delay)
    addu  $t7, $s2, $t7
    lh    $t8, -0x6000($t7)        # active (+0xA000)
    beq   $t8, $zero, skip
    lh    $t8, -0x6010($t7)        # (delay) phase (+0x9FF0)
    addiu $t9, $zero, 1
    bne   $t8, $t9, skip           # flying only
    or    $a0, $s0, $zero          # (delay)
    or    $a1, $s1, $zero
    jal   0x0010B328               # crystal_home.s home_to (DeadDiskReadyCave.CrystalHomeTo)
    addiu $a2, $sp, 0x0020         # (delay)
    lw    $t5, -0x5EB4($s2)        # (the call took the t-registers)
skip:
    b     next
    addiu $s1, $s1, 1              # (delay)
done:
    lw    $ra, 0x0000($sp)
    lw    $a0, 0x0004($sp)
    lw    $s0, 0x0008($sp)
    lw    $s1, 0x000C($sp)
    lw    $s2, 0x0010($sp)
    addiu $sp, $sp, 0x40
step:
    j     0x001AC180               # Step__12CSHOT_EFFECT(a0)
    nop
