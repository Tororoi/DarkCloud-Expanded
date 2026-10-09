# crystal_prison.s — the Crystal Gemron's ice prison (shot config 35, the Ice Queen's kori) and two tests crystal_shots.s uses.
# Assembled at 0x00118730 (DeadIoctlCave.CrystalPrison: the dead sceIoctl body, after the two shot configs). Entries:
#   +0x00 Frozen   → v0 = the active character's Freeze bit (UserStatus +0x42C8 + chara × 4, 0x04).
#   +0x08 Standing → v0 = 1 when a monster-pack object holding the prison's config has a sub-shot active.
#   +0x10 Prison(a0 = the pack object holding the prison): each standing sub-shot (phase 1) has its countdown (+0x9FD0) held at 2
#         while the player is frozen and set to 0 once he is not, so Step__12CSHOT_EFFECT plays its shatter (phase 3) then.

    j     frozen
    nop
    j     standing
    nop
prison:
    addiu $sp, $sp, -0x10
    sw    $ra, 0x0000($sp)
    sw    $a0, 0x0004($sp)
    jal   frozen
    nop
    lw    $a0, 0x0004($sp)
    addiu $t5, $zero, 2
    movz  $t5, $zero, $v0          # not frozen: 0 — the shatter
    lui   $at, 0x0001
    addu  $t0, $a0, $at            # the object + 0x10000: 2 B a sub-shot (active, phase) …
    addu  $t6, $a0, $at            # … and 4 B a sub-shot (phase_delay)
    addiu $t7, $zero, 8
prison_sub:
    lh    $t1, -0x6000($t0)        # active (+0xA000)
    beq   $t1, $zero, prison_next
    lh    $t2, -0x6010($t0)        # (delay) phase (+0x9FF0)
    addiu $t3, $zero, 1
    bne   $t2, $t3, prison_next
    nop
    sw    $t5, -0x6030($t6)        # phase_delay (+0x9FD0)
prison_next:
    addiu $t0, $t0, 2
    addiu $t7, $t7, -1
    bne   $t7, $zero, prison_sub
    addiu $t6, $t6, 4              # (delay)
    lw    $ra, 0x0000($sp)
    jr    $ra
    addiu $sp, $sp, 0x10

frozen:
    lui   $v0, 0x002A
    lw    $v0, 0x3468($v0)         # UserStatus
    beq   $v0, $zero, frozen_ret
    nop
    lb    $t8, 0x0004($v0)         # cur_chara
    sll   $t8, $t8, 2
    addu  $v0, $v0, $t8
    lw    $v0, 0x42C8($v0)
    andi  $v0, $v0, 0x0004
frozen_ret:
    jr    $ra
    nop

standing:
    lui   $t8, 0x002A
    lw    $t8, 0x35D8($t8)         # NowShotEffect: the five objects, 0xA160 apart
    beq   $t8, $zero, standing_no
    lui   $t9, 0x0011              # (delay)
    ori   $t9, $t9, 0x86C0         # the ice prison's config
    addiu $t7, $zero, 5
standing_obj:
    lw    $t6, 0x0000($t8)
    bne   $t6, $t9, standing_next
    lui   $at, 0x0001              # (delay)
    addu  $t5, $t8, $at
    addiu $t4, $zero, 8
standing_sub:
    lh    $t6, -0x6000($t5)        # active
    bne   $t6, $zero, standing_yes
    addiu $t4, $t4, -1             # (delay)
    bne   $t4, $zero, standing_sub
    addiu $t5, $t5, 2              # (delay)
standing_next:
    addiu $t7, $t7, -1
    ori   $at, $zero, 0xA160
    bne   $t7, $zero, standing_obj
    addu  $t8, $t8, $at            # (delay)
standing_no:
    jr    $ra
    addu  $v0, $zero, $zero
standing_yes:
    jr    $ra
    addiu $v0, $zero, 1
