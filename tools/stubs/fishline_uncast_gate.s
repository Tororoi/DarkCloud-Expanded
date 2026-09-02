# FishingCheckUkiHook SETTLED-GATED height check — cave @0x228E20 (dead CharaChange region; the slot the
# removed cast-scale cave used to occupy). Two-part patch (IsoPatcher.PatchFishingUncastGate):
#
#  (1) EdMoveChara's waiting-state gate `slti at,st_cnt,0x1f` @0x16C6D0 is left VANILLA (31 frames).
#      A 31->4 acceleration shipped for a while (fast rim-deposit rejection) but was REVERTED 2026-09-02:
#      the cast collision (FishLineClamp / QueensDragCheck / uki ground gate) already prevents the casts
#      it existed for, and at 4 frames the function's UN-gated 3-axis box check rejected legit long casts
#      still mid-arc (seen at the Matataki falls).
#  (2) THIS cave replaces the function's height-check tail (entered by `j` over the `lui v0,0x40a0` @0x1AA2D4;
#      its old mtc1 delay slot is NOP'd). Vanilla tail: hook.y > water+5 OR uki.y > water+5 -> invalid(1).
#      Firing that early would kill LEGIT long casts (still airborne above water+5 when the waiting state
#      begins), so the cave adds a SETTLED gate: the height violation only counts when the bobber's Verlet
#      velocity (ukiv[0] @0x1d563d0, damped to ~0 once resting) is ~zero. In flight -> report valid and let
#      the cast play out; resting on land -> invalid -> the native auto-uncast fires.
#
# On entry (from 0x1AA2D4): f0 = water level (FishingGetWaterLevel just returned), sp = CheckUkiHook's frame
# (uki vec @sp+0x10 -> y @+0x14; hook vec @sp+0x20 -> y @+0x24). The rect checks already passed (loop above).
# v0 = verdict out; exit jumps to the epilogue @0x1AA328 (lq ra there — ra untouched here). t0/f0-f2 are dead.
# R5900: c.le.s / c.lt.s are .word-encoded (keystone lacks them); nop after every mtc1 and FP compare.
lui   $t0, 0x40a0              # 5.0
mtc1  $t0, $f1
nop
add.s $f1, $f1, $f0            # f1 = water + 5
lwc1  $f0, 0x24($sp)           # hook.y
.word 0x46010036               # c.le.s f0,f1 : hook.y <= water+5 ?
nop
bc1f  high                     # hook rests high -> candidate invalid
nop
lwc1  $f0, 0x14($sp)           # uki.y (bobber)
.word 0x46010036               # c.le.s f0,f1 : uki.y <= water+5 ?
nop
bc1t  valid                    # both at/under the surface -> cast is fine
nop
high:
lui   $t0, 0x1d5
ori   $t0, $t0, 0x63d0         # ukiv[0] (bobber Verlet velocity)
lwc1  $f0, 0x0($t0)
lwc1  $f1, 0x4($t0)
lwc1  $f2, 0x8($t0)
mul.s $f0, $f0, $f0
mul.s $f1, $f1, $f1
add.s $f0, $f0, $f1
mul.s $f2, $f2, $f2
add.s $f0, $f0, $f2            # |ukiv|^2
lui   $t0, 0x3d4c              # 0.0498 -> settled = speed < ~0.22/frame
mtc1  $t0, $f1
nop
.word 0x46010034               # c.lt.s f0,f1 : settled ?
nop
bc1f  valid                    # still moving (in flight) -> don't reject mid-cast
nop
addiu $v0, $zero, 1            # settled out of the water -> INVALID -> auto-uncast
j     0x001aa328
nop
valid:
addu  $v0, $zero, $zero
j     0x001aa328
nop
