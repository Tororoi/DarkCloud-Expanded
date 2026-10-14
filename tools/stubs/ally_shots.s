# ally_shots.s — the summoned ally's shot configs taken back out of the floor's shot pack. Assembled at 0x001191A0 (DeadSifElfCave:
# the dead sceSifLoadElfPart..sceSifSetIopAddr bodies). A monster's load (SetupBaseModel) enters its species' shot configs into the
# pack (NowShotEffect, gp−0x6218: five CSHOT_EFFECT slots of 0xA160 B; a slot's +0 is its config, 0 = empty) with their DATA in the
# allocator it was handed — for the pockle, its own block — and the pack keeps any config it finds already in a slot (by config
# pointer). Left there, a later pockle's model overwrote that data while the slot still named it, and the next pockle of that day
# fired its shot out of garbage (a dead EE). So: before (+0) the summon's load, the empty slots are noted; after (+0x40), the
# slots the load filled and their configs (two at most) are kept in CodeCaves.AllyShots (0x01FAF824: +0 mask, +4 / +8 the configs);
# clear (+0xA4, from ally_despawn.s on the floor the pockle was summoned on) empties each such slot that still holds its config
# (Initialize__12CSHOT_EFFECT, its sub-shots off), so the next load enters it afresh into the new block.

before:
    lw    $t0, -0x6218($gp)        # the pack
    or    $t1, $zero, $zero
    or    $t2, $zero, $zero
    ori   $t6, $zero, 0xA160
bloop:
    lw    $t3, 0x00($t0)
    bne   $t3, $zero, bnext
    addiu $t4, $zero, 1            # (delay)
    sllv  $t4, $t4, $t1
    or    $t2, $t2, $t4            # empty
bnext:
    addiu $t1, $t1, 1
    slti  $t5, $t1, 5
    bne   $t5, $zero, bloop
    addu  $t0, $t0, $t6            # (delay)
    lui   $t6, 0x01FB
    jr    $ra
    sw    $t2, -0x07DC($t6)        # (delay) AllyShots + 0: the empty slots, for now

after:
    lui   $t6, 0x01FB
    lw    $t2, -0x07DC($t6)        # the slots that were empty
    lw    $t0, -0x6218($gp)
    or    $t1, $zero, $zero
    or    $t7, $zero, $zero        # the slots it filled
    addiu $t8, $t6, -0x07D8        # where the next config is kept (AllyShots + 4)
    ori   $t9, $zero, 0xA160
aloop:
    srlv  $t3, $t2, $t1
    andi  $t3, $t3, 1
    beq   $t3, $zero, anext        # held a config before: the floor's
    lw    $t4, 0x00($t0)           # (delay)
    beq   $t4, $zero, anext        # still empty
    addiu $t3, $t6, -0x07D0        # (delay) past AllyShots + 8
    beq   $t8, $t3, anext          # two kept already
    addiu $t5, $zero, 1            # (delay)
    sllv  $t5, $t5, $t1
    or    $t7, $t7, $t5
    sw    $t4, 0x00($t8)
    addiu $t8, $t8, 4
anext:
    addiu $t1, $t1, 1
    slti  $t3, $t1, 5
    bne   $t3, $zero, aloop
    addu  $t0, $t0, $t9            # (delay)
    jr    $ra
    sw    $t7, -0x07DC($t6)        # (delay) AllyShots + 0: the slots it filled

clear:
    lui   $t6, 0x01FB
    lw    $t2, -0x07DC($t6)
    beq   $t2, $zero, cnone
    sw    $zero, -0x07DC($t6)      # (delay) once
    addiu $sp, $sp, -0x20
    sw    $ra, 0x00($sp)
    sw    $s0, 0x04($sp)
    sw    $s1, 0x08($sp)
    sw    $s2, 0x0C($sp)
    sw    $s3, 0x10($sp)
    lw    $s0, -0x6218($gp)        # slot 0
    or    $s1, $zero, $zero
    addiu $s2, $t6, -0x07D8        # its first kept config
    or    $s3, $t2, $zero
cloop:
    srlv  $t3, $s3, $s1
    andi  $t3, $t3, 1
    beq   $t3, $zero, cnext
    nop
    lw    $t4, 0x00($s2)           # the config it put here
    addiu $s2, $s2, 4
    lw    $t5, 0x00($s0)
    bne   $t4, $t5, cnext          # something else there now: left alone
    nop
    jal   0x001AE440               # Initialize__12CSHOT_EFFECTFv: empty again
    or    $a0, $s0, $zero          # (delay)
cnext:
    addiu $s1, $s1, 1
    ori   $t9, $zero, 0xA160
    addu  $s0, $s0, $t9
    slti  $t3, $s1, 5
    bne   $t3, $zero, cloop
    nop
    lw    $ra, 0x00($sp)
    lw    $s0, 0x04($sp)
    lw    $s1, 0x08($sp)
    lw    $s2, 0x0C($sp)
    lw    $s3, 0x10($sp)
    jr    $ra
    addiu $sp, $sp, 0x20
cnone:
    jr    $ra
    nop
