# FishLine split caves — select the rope rest length PER SEGMENT: distpAbove (the existing distp @0x202A1FA4,
# via gp-0x784c; LineScale tunes it = cast reach / rod->bobber) vs distpBelow (mailbox float @0x01F10048; the
# mod writes it = hook depth / bobber->hook). Split at the FIXED bobber anchor A=18: segment index s0 <= 18 ->
# above/aerial, s0 >= 19 -> below/hang. One cave per loop. Each is reached by replacing the loop's
# `lwc1 f,-0x784c(gp)` with `j <cave>` and the FOLLOWING `sub.S` with `nop`; the cave does the select, the
# displaced sub.S, and j's back to the instruction after it. $t0/$t1 are dead here (a jal precedes each load).
# Assembled at 0x228DC0 (dead CharaChange, after capeEarlyDraw @0x228DBC).

# ── initCave (FishLineInit @0x1a9cac): loads distp->f0, then `sub.S f0,f1,f0`; return to 0x1a9cb4 ──
init_cave:
slti  $t0, $s0, 19             # t0 = (s0 <= 18)
beq   $t0, $zero, init_below
nop
lwc1  $f0, -0x784c($gp)        # ABOVE: distpAbove @0x202A1FA4
j     init_done
nop
init_below:
lui   $t1, 0x1f1
lwc1  $f0, 0x48($t1)           # BELOW: distpBelow @0x01F10048
init_done:
sub.S $f0, $f1, $f0            # displaced (point[s0].y = point[s0-1].y - rest)
j     0x001a9cb4
nop

# ── stepCave (FishLineStep @0x1aa7c8): loads distp->f1, then `sub.S f2,f0,f1`; return to 0x1aa7d0 ──
step_cave:
slti  $t0, $s0, 19             # t0 = (s0 <= 18)
beq   $t0, $zero, step_below
nop
lwc1  $f1, -0x784c($gp)        # ABOVE: distpAbove
j     step_done
nop
step_below:
lui   $t1, 0x1f1
lwc1  $f1, 0x48($t1)           # BELOW: distpBelow
step_done:
sub.S $f2, $f0, $f1            # displaced (corr uses f0 = live segment length)
j     0x001aa7d0
nop
