# ally_collide.s — the ally and the floor's enemies block each other as monsters do. Assembled at 0x00118C88 (DeadSifCave: the dead
# sceSifLoadModuleBuffer body). ElfAllyPatches points CMonstorUnit::Step's `jal MoveChecMonster` (0x1DE344, a0 = the stepping manager,
# its current_monster the moving unit) here — the Babel's-spear cave had it, so this calls that cave (which makes MoveChecMonster's
# check, then the spear's): the checks as before, then — while CodeCaves.AllyManager (0x01FAF7F8) holds a manager — the
# same rule against the OTHER manager's live units (the floor's enemies against the ally's unit, the ally's unit against the floor's):
# the moving unit's next place within both collision radii of one, below its top (height + 2 × radius), and moving toward it stops it
# (movement and speed zeroed, as MoveChecMonster does). Caller-saved registers only. pblock (+0x1A0) blocks the player at it.

collide:
    addiu $sp, $sp, -0x20
    sw    $ra, 0x00($sp)
    sw    $a0, 0x04($sp)
    jal   0x001B5030               # the spear-block cave (DebugIfCave.SpearBlock), which makes MoveChecMonster's check, as before
    nop
    lw    $a0, 0x04($sp)
    lui   $t0, 0x01FB
    lw    $t1, -0x0808($t0)        # CodeCaves.AllyManager
    beq   $t1, $zero, cdone
    nop
    lui   $t2, 0x01DF
    ori   $t2, $t2, 0x87D0         # MainMonstorUnit
    beq   $a0, $t1, cmine
    or    $t3, $t2, $zero          # (delay) the ally's unit: the others are the floor's
    bne   $a0, $t2, cdone          # neither manager
    nop
    or    $t3, $t1, $zero          # a floor unit: the other is the ally's
cmine:
    lw    $t4, 0x90($a0)           # current_monster
    addiu $t5, $zero, 0x190
    mult  $t4, $t5
    mflo  $t6
    lui   $t7, 0x0001
    ori   $t7, $t7, 0xE3D0         # monster[] at +0x1E3D0
    addu  $t6, $t6, $t7
    addu  $t6, $t6, $a0            # its MONSTOR
    ori   $t5, $zero, 0x3510
    mult  $t4, $t5
    mflo  $t8
    lui   $t7, 0x0001
    ori   $t7, $t7, 0xFCD0         # chara[] at +0x1FCD0
    addu  $t8, $t8, $t7
    addu  $t8, $t8, $a0            # its CCharacter
    lwc1  $f0, 0x10($t8)           # x
    lwc1  $f1, 0x14($t8)           # height
    lwc1  $f2, 0x18($t8)           # y
    lwc1  $f3, 0x80($t6)           # movement_speed
    lwc1  $f4, 0x60($t6)
    mul.s $f4, $f4, $f3            # its step: movement × speed
    lwc1  $f5, 0x64($t6)
    mul.s $f5, $f5, $f3
    lwc1  $f6, 0x68($t6)
    mul.s $f6, $f6, $f3
    add.s $f7, $f0, $f4            # where it is going
    add.s $f8, $f1, $f5
    add.s $f9, $f2, $f6
    lwc1  $f10, 0x48($t6)          # its collision_radius
    lui   $t7, 0x0001
    ori   $t7, $t7, 0xE3D0
    addu  $t5, $t3, $t7            # the other manager's monster[0]
    lui   $t7, 0x0001
    ori   $t7, $t7, 0xFCD0
    addu  $t7, $t3, $t7            # its chara[0]
    addiu $t9, $zero, 16
cloop:
    lw    $t4, 0x00($t5)           # state
    addiu $t4, $t4, -2
    bne   $t4, $zero, cnext        # not on the floor
    nop
    lwc1  $f11, 0x10($t7)
    lwc1  $f12, 0x14($t7)
    lwc1  $f13, 0x18($t7)
    lwc1  $f14, 0x48($t5)          # its radius
    sub.s $f15, $f11, $f7
    sub.s $f16, $f13, $f9
    mul.s $f15, $f15, $f15
    mul.s $f16, $f16, $f16
    add.s $f15, $f15, $f16         # flat distance²
    add.s $f16, $f14, $f10
    mul.s $f16, $f16, $f16         # (both radii)²
    .word 0x460F8034               # c.olt.s f16, f15: farther than both radii
    nop
    bc1t  cnext
    nop
    add.s $f17, $f14, $f14
    add.s $f17, $f12, $f17         # its top: height + 2 × radius
    .word 0x46114034               # c.olt.s f8, f17: below it
    nop
    bc1f  cnext
    nop
    sub.s $f18, $f11, $f0
    sub.s $f19, $f13, $f2
    mul.s $f18, $f18, $f4
    mul.s $f19, $f19, $f6
    add.s $f18, $f18, $f19         # step · toward it
    mtc1  $zero, $f19
    nop
    .word 0x46129834               # c.olt.s f19, f18: moving toward it
    nop
    bc1f  cnext
    nop
    sw    $zero, 0x60($t6)
    sw    $zero, 0x64($t6)
    sw    $zero, 0x68($t6)
    sw    $zero, 0x80($t6)         # stopped, as MoveChecMonster stops it
    j     cdone
    nop
cnext:
    addiu $t5, $t5, 0x190
    ori   $t4, $zero, 0x3510
    addu  $t7, $t7, $t4
    addiu $t9, $t9, -1
    bne   $t9, $zero, cloop
    nop
cdone:
    lw    $ra, 0x00($sp)
    jr    $ra
    addiu $sp, $sp, 0x20

# The player's move (a0 = the floor's manager, a1 = &her position, a2 = &her velocity, a3 = a flag — MoveCheck__12CMonstorUnitFPfPfi's
# arguments; the player_spear_block.s cave's call comes here, ElfAllyPatches): the engine's own player-against-unit block on the floor's
# units, as before, then on the ally's manager while one stands (MoveCheck reads `this` alone), so Ruby cannot walk through it.
pblock:
    addiu $sp, $sp, -0x20
    sw    $ra, 0x00($sp)
    sw    $a1, 0x04($sp)
    sw    $a2, 0x08($sp)
    jal   0x001DC820               # MoveCheck__12CMonstorUnitFPfPfi: the floor's units
    sw    $a3, 0x0C($sp)           # (delay)
    lui   $t0, 0x01FB
    lw    $a0, -0x0808($t0)        # CodeCaves.AllyManager
    beq   $a0, $zero, pdone
    lw    $a1, 0x04($sp)           # (delay)
    lw    $a2, 0x08($sp)
    jal   0x001DC820               # the ally's unit
    lw    $a3, 0x0C($sp)           # (delay)
pdone:
    lw    $ra, 0x00($sp)
    jr    $ra
    addiu $sp, $sp, 0x20
