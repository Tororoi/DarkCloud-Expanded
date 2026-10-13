# ally_aim.s — where the summoned ally's AI goes for its enemy. Assembled at 0x0010CD20 (DeadDmaWatchCave: the dead sceDmaWatch..
# sceDmaCallback bodies). ally_brain.s calls it once a frame while the ally has an enemy, a0 = the enemy's floor slot, a1 = the ally's
# block: v0 = the point its pointer-table entry (16) names — what its script's _GET_POSITION / _GET_DISTANCE read as "the player".
# The enemy's position is its root, deep inside a large body: the pockle walked in until 20-odd u from it — which the body (and the
# block rule against it) never let it reach — and never swung. So the point is taken in the enemy's hit sphere nearest the pockle
# (its active spheres — the enemy is near the pockle, so drawn, so posed — by distance less radius): 8 u in from the sphere's surface
# toward the pockle (its centre for a sphere smaller than that, or a pockle already that deep), at the root's height, written to the
# block's aim point (+0x80). No active sphere: the root itself. f0..f13, t0..t9.

aim:
    ori   $t0, $zero, 0x3510
    mult  $a0, $t0
    mflo  $t0
    lui   $t1, 0x01E1
    ori   $t1, $t1, 0x84B0
    addu  $v0, $t0, $t1            # the enemy's position (the root)
    lui   $t2, 0x0001
    ori   $t2, $t2, 0xFDE0
    addu  $t2, $a1, $t2            # the pockle's position (block + 0x1FDE0)
    lwc1  $f2, 0x00($t2)
    lwc1  $f3, 0x08($t2)
    ori   $t0, $zero, 0x510
    mult  $a0, $t0
    mflo  $t0
    lui   $t1, 0x01E4
    ori   $t1, $t1, 0xDA20         # the floor's body-sphere centres (MainMonstorUnit + 0x55250)
    addu  $t3, $t0, $t1            # this slot's: centres, stride 0x10
    addiu $t8, $t3, 0x140          #              radii (+0x140, stride 4; active flags 0xC0 on)
    addiu $t4, $zero, 16
    lui   $t5, 0x7F00
    mtc1  $t5, $f11                # the best edge so far (huge)
    or    $t6, $zero, $zero        # the best sphere's centre (none)
aloop:
    lw    $t7, 0xC0($t8)           # active?
    beq   $t7, $zero, anext
    nop
    lwc1  $f4, 0x00($t3)           # centre x
    lwc1  $f5, 0x08($t3)           #        y
    sub.s $f6, $f2, $f4            # pockle − centre
    sub.s $f7, $f3, $f5
    mul.s $f8, $f6, $f6
    mul.s $f9, $f7, $f7
    add.s $f8, $f8, $f9
    .word 0x46080204               # sqrt.s f8, f8: its distance from the centre
    lwc1  $f9, 0x00($t8)           # the radius
    sub.s $f12, $f8, $f9           # the edge
    .word 0x460B6034               # c.olt.s f12, f11: nearer than the best
    nop
    bc1f  anext
    nop
    mov.s $f11, $f12
    or    $t6, $t3, $zero
    mov.s $f13, $f9                # its radius
anext:
    addiu $t3, $t3, 0x10
    addiu $t4, $t4, -1
    bne   $t4, $zero, aloop
    addiu $t8, $t8, 4              # (delay)
    beq   $t6, $zero, adone        # none active: the root
    nop
    lwc1  $f4, 0x00($t6)
    lwc1  $f5, 0x08($t6)
    sub.s $f6, $f2, $f4            # toward the pockle
    sub.s $f7, $f3, $f5
    mul.s $f8, $f6, $f6
    mul.s $f9, $f7, $f7
    add.s $f8, $f8, $f9
    .word 0x46080204               # sqrt.s f8, f8: d
    lui   $t5, 0x4100
    mtc1  $t5, $f12                # 8.0
    mtc1  $zero, $f9
    sub.s $f13, $f13, $f12         # depth = radius − 8
    .word 0x460D4834               # c.olt.s f9, f13: depth > 0
    nop
    bc1f  apoint                   # a small sphere: its centre
    nop
    .word 0x46086834               # c.olt.s f13, f8: the pockle outside that depth
    nop
    bc1f  apoint                   # inside it: the centre
    nop
    div.s $f13, $f13, $f8          # depth / d (d > depth > 0)
    mul.s $f6, $f6, $f13
    mul.s $f7, $f7, $f13
    add.s $f4, $f4, $f6
    add.s $f5, $f5, $f7
apoint:
    addiu $t9, $a1, 0x80           # the block's aim point
    swc1  $f4, 0x00($t9)
    lwc1  $f6, 0x04($v0)
    swc1  $f6, 0x04($t9)           # at the root's height
    swc1  $f5, 0x08($t9)
    lui   $t5, 0x3F80
    sw    $t5, 0x0C($t9)
    or    $v0, $t9, $zero
adone:
    jr    $ra
    nop
