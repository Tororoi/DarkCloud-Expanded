# ally_despawn.s — the summoned ally's end. Assembled at 0x00117080 (DeadSifDataCave: the dead sceSifGetOtherData body). ally_brain.s
# calls it, a0 = the event code (2 fallen, 3 disarmed / the summoner not in hand, 4 its block or floor gone / an event, 8 not stood), and
# so does ally_hooks.s's full event. The manager word goes first (the game stops riding it), then the hold; its pointer-table entry (16)
# names the player again. On the floor it was
# summoned on (CodeCaves.AllyTex's stamp) the texture entries its load added to the monsters' group 42 are emptied and the group's VRAM
# top and end put back — kept outside its block, so a character change that overwrote the block still cleans them — and the shot
# configs its load entered into the floor's shot pack taken back out (ally_shots.s clear). Caller-saved registers.

despawn:
    addiu $sp, $sp, -0x10
    sw    $ra, 0x00($sp)
    sw    $a0, 0x04($sp)
    lui   $t0, 0x01FB
    sw    $zero, -0x808($t0)       # AllyManager
    sw    $zero, -0x3C($t0)        # AllyHold
    lui   $t1, 0x01EA
    ori   $t1, $t1, 0x1D30         # the player's position
    lui   $t2, 0x01F2
    sw    $t1, -0x6FC0($t2)        # PtrTable[16]
    lw    $t1, -0x354($t0)         # AllyTex + 0xC: (first << 16) | end
    beq   $t1, $zero, dnote
    sw    $zero, -0x354($t0)       # (delay) once
    lw    $t2, -0x360($t0)         # AllyTex + 0: its floor
    lbu   $t3, -0x625C($gp)        # checkDungeon
    lui   $t4, 0x01CE
    lbu   $t4, -0x6AB2($t4)        # checkFloor
    lbu   $t5, -0x633C($gp)        # the back-floor flag
    sll   $t3, $t3, 16
    sll   $t4, $t4, 8
    or    $t3, $t3, $t4
    sltu  $t5, $zero, $t5
    or    $t3, $t3, $t5
    bne   $t2, $t3, dnote          # another floor: its textures and shots went with the last
    sw    $t1, 0x08($sp)           # (delay)
    jal   0x00119244               # ally_shots.s clear: the shot configs its load entered taken back out of the pack
    nop
    lui   $t0, 0x01FB
    lw    $t1, 0x08($sp)
    lw    $a0, 0x04($sp)
    srl   $t4, $t1, 16             # the first entry
    andi  $t5, $t1, 0xFFFF         # the end
    lui   $t6, 0x01C7
    ori   $t6, $t6, 0x5870         # the texture manager
    addiu $t7, $zero, 0x50
    mult  $t4, $t7
    mflo  $t7
    addu  $t7, $t7, $t6
    addiu $t7, $t7, 0x10F8         # its first entry
dent:
    lhu   $t8, 0x00($t7)
    xori  $t8, $t8, 42
    bne   $t8, $zero, dskip        # not the monsters' group (left as it is)
    addiu $t8, $zero, 20           # (delay)
    or    $t9, $t7, $zero
dzero:
    sw    $zero, 0x00($t9)
    addiu $t8, $t8, -1
    bne   $t8, $zero, dzero
    addiu $t9, $t9, 4              # (delay)
dskip:
    addiu $t4, $t4, 1
    bne   $t4, $t5, dent
    addiu $t7, $t7, 0x50           # (delay)
    lw    $t1, -0x35C($t0)         # AllyTex + 4
    sw    $t1, 0xA10($t6)          # group 42's VRAM top
    lw    $t1, -0x358($t0)         # AllyTex + 8
    sw    $t1, 0xA14($t6)          #          VRAM end
dnote:
    lw    $t1, -0x2C($t0)          # AllyEvent = ((count + 1) << 8) | code
    srl   $t1, $t1, 8
    addiu $t1, $t1, 1
    sll   $t1, $t1, 8
    or    $t1, $t1, $a0
    sw    $t1, -0x2C($t0)
    lw    $ra, 0x00($sp)
    jr    $ra
    addiu $sp, $sp, 0x10
