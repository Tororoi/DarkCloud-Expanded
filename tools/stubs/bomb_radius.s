# bomb_radius.s — a shot config's own blast radius. Assembled at 0x0010A880 (DeadCdCave.BombRadius, after the blow-direction cave in
# the dead sceCdGetToc body). CSHOT_EFFECT::Step ends a shot whose config has end_effect 100 with SetBombEffect(pos, target, damage,
# scale) (0x1AC824, 0x1AC978, 0x1ACAE0; a3 = the config), whose hit radius is 20 × scale and shock ring (drawn above scale 1)
# 30 × scale. ElfSpeciesPatches.PatchBombRadius points the three calls here: the call as before, then — for a config whose padding
# halfword +0x56 holds a radius per unit of scale (0 in every vanilla config) — that × the config's scale (+0x58) for the hit entry
# it made (*NowColData + index × 0xA0, +0x3C) and, above scale 1, for the shock ring (*NowShockWave: base and expand radius
# +0x10 / +0x14, its steps +0x1C at half, as the Big Bang's drop draws it). The Bomb Gemron's blasts: 25 × scale.
# Clobbers t0–t3, f0, f1 (the call returns into a branch to the step's loop).

    addiu $sp, $sp, -0x20
    sw    $ra, 0x0010($sp)
    sw    $a3, 0x0014($sp)         # the config
    jal   0x001D5940               # SetBombEffect, as before (v0 = the hit entry's index, or −1)
    nop
    lw    $a3, 0x0014($sp)
    lhu   $t0, 0x0056($a3)         # the config's radius per unit of scale, or 0
    beq   $t0, $zero, ret
    nop
    mtc1  $t0, $f0
    lwc1  $f1, 0x0058($a3)         # the scale (here, so cvt does not read $f0 straight after its mtc1)
    cvt.s.w $f0, $f0
    mul.s $f0, $f0, $f1            # the radius
    bltz  $v0, ring                # no hit entry made
    lui   $t1, 0x002A              # (delay)
    lw    $t2, 0x35E0($t1)         # NowColData
    sll   $t3, $v0, 2
    addu  $t3, $t3, $v0
    sll   $t3, $t3, 5              # × 0xA0
    addu  $t2, $t2, $t3
    swc1  $f0, 0x003C($t2)         # the entry's radius
ring:
    lw    $t0, 0x0058($a3)         # the scale, a positive float: compares as an int
    lui   $t3, 0x3F80
    slt   $t3, $t3, $t0
    beq   $t3, $zero, ret          # 1.0 or less: no ring drawn
    nop
    lui   $t0, 0x3F00
    mtc1  $t0, $f1                 # 0.5 (loaded first: the stores below keep mul.s off its mtc1)
    lw    $t2, 0x35E8($t1)         # NowShockWave
    swc1  $f0, 0x0010($t2)         # base radius
    swc1  $f0, 0x0014($t2)         # expand radius
    mul.s $f1, $f0, $f1
    swc1  $f1, 0x001C($t2)         # its steps
ret:
    lw    $ra, 0x0010($sp)
    jr    $ra
    addiu $sp, $sp, 0x20
