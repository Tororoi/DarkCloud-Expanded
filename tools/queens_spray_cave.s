# Queens waterfall spray cave — lives in the dead CharaChange region (reclaimed ELF code, jal-legal). Hooked in
# at MainDraw @0x17c5a0, REPLACING `jal EditEffectStep2` (0x166de0) — the convergence of the Matataki-spray and
# non-Matataki paths, right before DrawEffect. Delay slot is a nop, $ra = 0x17c5a8 on entry.
#
# Reads a mod table @0x01F18400: word[0] = count, then count × 48-byte entries:
#   +0x00 pos    x,y,z,w   (a1 to EffectWaterSpray)
#   +0x10 spread x,y,z,w   (a2)
#   +0x20 bias   bx,by,bz  (copied to the global @0x01F18300 that the spray-bias shim adds to the particle
#                           velocity — by<0 lowers the plume, bx/bz aim it; see spray_bias_shim.s)
# Per entry: set the bias global, then EffectWaterSpray(group=0x2a2838, &pos, &spread, interval=15, phase=i)
# (5th arg=phase in $t0, as the vanilla Matataki call does). After the loop, ZERO the bias global so Matataki's
# own spray (which runs earlier in MainDraw, and reads the same shim) stays unbiased. count<=0 → skip to that
# zero + the tail call. The count IS the gate; no area check (the mod only fills the table in Queens).
addiu $sp, $sp, -0x20
sw    $ra, 0x1c($sp)
sw    $s0, 0x18($sp)
sw    $s1, 0x14($sp)
sw    $s2, 0x10($sp)
lui   $s1, 0x1f2
addiu $s1, $s1, -0x7c00        # s1 = 0x01F18400 (table base)
lw    $s2, 0x0($s1)            # s2 = count
blez  $s2, zerobias
nop
addiu $s1, $s1, 0x10           # s1 -> first entry
move  $s0, $zero               # i = 0
loop:
lui   $t2, 0x1f2               # t2 = 0x1F20000 (bias-global region base)
lw    $t1, 0x20($s1)           # entry.bias.x
sw    $t1, -0x7d00($t2)        #   -> 0x01F18300
lw    $t1, 0x24($s1)
sw    $t1, -0x7cfc($t2)        #   -> 0x01F18304
lw    $t1, 0x28($s1)
sw    $t1, -0x7cf8($t2)        #   -> 0x01F18308
lui   $a0, 0x2a
ori   $a0, $a0, 0x2838         # a0 = effectGroup 0x2a2838
move  $a1, $s1                 # a1 = &pos
addiu $a2, $s1, 0x10           # a2 = &spread
ori   $a3, $zero, 15           # a3 = interval (frames per particle)
move  $t0, $s0                 # t0 = phase (i)
jal   0x00164f20               # EffectWaterSpray
nop
addiu $s1, $s1, 0x30           # next entry (+48)
addiu $s0, $s0, 1              # i++
slt   $t1, $s0, $s2            # i < count ?
bne   $t1, $zero, loop
nop
zerobias:
lui   $t2, 0x1f2
sw    $zero, -0x7d00($t2)      # bias.x = 0
sw    $zero, -0x7cfc($t2)      # bias.y = 0
sw    $zero, -0x7cf8($t2)      # bias.z = 0
jal   0x00166de0               # EditEffectStep2 (the displaced original call)
nop
lw    $ra, 0x1c($sp)
lw    $s0, 0x18($sp)
lw    $s1, 0x14($sp)
lw    $s2, 0x10($sp)
addiu $sp, $sp, 0x20
jr    $ra
nop
