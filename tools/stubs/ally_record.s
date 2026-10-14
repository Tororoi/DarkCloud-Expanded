# ally_record.s — while an ally stands, every attack sphere CheckDmg places is recorded frame by frame, the ally's and the floor's
# enemies', so the mod's friendly fire tests each frame's sphere (an app pass sees only some frames, and none while it stalls).
# Assembled at 0x0010B648 (DeadCdTypeCave, after ally_settle.s). Entered from ally_hooks.s swing (CheckDmg's `jal Set__14CCollisionData`,
# 0x1DA098) when CodeCaves.AllyManager is set, t9 = it, s5 = the manager being checked, with Set's own arguments kept for it: a1 = the
# sphere's centre (x, height, y), f12 its radius, a2 its damage, t1 its reaction, t2 its flags.
# The ring (the ally's manager + 0xAB00, in its unused model slots; the summon zeroes its count): +0 the records ever written, +0x10
# 256 records of 32 B — x, height, y, radius, damage, reaction, flags, (vblank << 16) | (mask << 8) | who (16 = the ally, else the
# floor slot; the mask as CheckDmg gave it, bit 0 = hurts the player).
# The ally's own sphere then goes in with mask 0 (it never hits the player); then Set. t4..t8.

record:
    ori   $t8, $zero, 0xAB00
    addu  $t8, $t9, $t8            # the ring
    lw    $t7, 0x00($t8)           # records written
    andi  $t6, $t7, 0xFF
    sll   $t6, $t6, 5
    addu  $t6, $t8, $t6            # this record (its words at + 0x10)
    lw    $t5, 0x00($a1)
    sw    $t5, 0x10($t6)           # x
    lw    $t5, 0x04($a1)
    sw    $t5, 0x14($t6)           # height
    lw    $t5, 0x08($a1)
    sw    $t5, 0x18($t6)           # y
    swc1  $f12, 0x1C($t6)          # radius
    sw    $a2, 0x20($t6)           # damage
    sw    $t1, 0x24($t6)           # reaction
    sw    $t2, 0x28($t6)           # flags
    andi  $t4, $t0, 0x00FF
    sll   $t4, $t4, 8              # its mask
    lui   $t5, 0x002A
    lw    $t5, 0x2400($t5)         # the vblank count (0x2A2400)
    sll   $t5, $t5, 16
    or    $t5, $t5, $t4
    beq   $s5, $t9, rwho
    ori   $t4, $t5, 16             # (delay) the ally
    lw    $t4, 0x90($s5)           # the floor slot being checked
    or    $t4, $t4, $t5
rwho:
    sw    $t4, 0x2C($t6)
    addiu $t7, $t7, 1
    sw    $t7, 0x00($t8)
    bne   $s5, $t9, rset
    nop
    or    $t0, $zero, $zero        # the ally's: hurts no one
rset:
    j     0x001B57A0               # Set__14CCollisionDataFPfiiffiiii
    nop
