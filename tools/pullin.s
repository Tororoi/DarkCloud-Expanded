# REWORK v1 @0x14b838 — pull-in + ceiling-duck + right-stick height. NO ground-relative baseline / wall climb
# (those were the false-rise-under-bridges bug). Height = REST_H + stick, clamped DOWN by a ceiling probe (tunnel duck).
# dist = pivot->resting-eye first hit − MARGIN (standard spring-arm: nearest occluder from the player). Constants
# HARDCODED. Symmetric ease for now (asymmetric + HOLD + yaw come next). Frame -0x70. CheckHit 0x149d50 vs s5/s8.
# R5900: c.OLT.s = .word ...0x34; nop after every mtc1 & FP compare.
addiu $sp, $sp, -0x70
sw    $ra, 0x50($sp)
jal   0x14a080
nop
sw    $v0, 0x54($sp)
lui   $at, 0x1d2
lw    $v1, -0x6988($at)
beq   $v1, $zero, done
nop
sw    $v1, 0x58($sp)
lwc1  $f0, 0x2c0($v1)
swc1  $f0, 0x20($sp)          # ref.x
lwc1  $f0, 0x2c4($v1)
swc1  $f0, 0x24($sp)          # ref.y
lwc1  $f0, 0x2c8($v1)
swc1  $f0, 0x28($sp)          # ref.z
lwc1  $f12, 0x2d8($v1)
jal   0x11d8a0                # sin(angT)
nop
swc1  $f0, 0x60($sp)
lw    $v1, 0x58($sp)
lwc1  $f12, 0x2d8($v1)
jal   0x11d6b0                # cos(angT)
nop
swc1  $f0, 0x64($sp)
lw    $v1, 0x58($sp)
# ===== eye spot = ref + dist*dir, eye.y = ref.y + height  -> ceiling-probe origin @ sp+0x20 =====
lwc1  $f1, 0x2d0($v1)         # dist
lwc1  $f2, 0x60($sp)
mul.s $f2, $f2, $f1
lwc1  $f3, 0x20($sp)
add.s $f4, $f3, $f2           # camX
lwc1  $f2, 0x64($sp)
mul.s $f2, $f2, $f1
lwc1  $f3, 0x28($sp)
add.s $f5, $f3, $f2           # camZ
lwc1  $f6, 0x24($sp)          # ref.y
lwc1  $f7, 0x2d4($v1)         # height
add.s $f7, $f6, $f7           # eye.y
swc1  $f4, 0x20($sp)          # rayFrom = eye
swc1  $f7, 0x24($sp)
swc1  $f5, 0x28($sp)
sw    $zero, 0x2c($sp)
# ===== CEILING ray: eye -> up CEIL_DIST -> ceilingY @ sp+0x6c (huge sentinel if none) =====
swc1  $f4, 0x30($sp)          # rayTo.x = camX
lui   $t0, 0x42c8             # CEIL_DIST = 100
mtc1  $t0, $f1
nop
add.s $f0, $f7, $f1           # eye.y + 100
swc1  $f0, 0x34($sp)
swc1  $f5, 0x38($sp)          # rayTo.z = camZ
sw    $zero, 0x3c($sp)
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addiu $a2, $sp, 0x20
addiu $a3, $sp, 0x30
addiu $t0, $sp, 0x40
sw    $t0, 0x10($sp)
addiu $t1, $zero, 1
sw    $t1, 0x14($sp)
sw    $zero, 0x18($sp)
jal   0x149d50
nop
lw    $v1, 0x58($sp)
bltz  $v0, cnohit
nop
# ONE-SIDED: cull backface — only duck under a roof whose normal faces DOWN toward the eye (N·rayUp < 0).
sll   $t1, $v0, 6
sll   $t4, $v0, 4
addu  $t1, $t1, $t4
addu  $t1, $s5, $t1
lwc1  $f4, 0x30($t1)          # N.x
lwc1  $f5, 0x34($t1)          # N.y
lwc1  $f6, 0x38($t1)          # N.z
lwc1  $f7, 0x30($sp)
lwc1  $f10, 0x20($sp)
sub.s $f7, $f7, $f10          # rayDir.x
mul.s $f4, $f4, $f7
lwc1  $f7, 0x34($sp)
lwc1  $f10, 0x24($sp)
sub.s $f7, $f7, $f10          # rayDir.y
mul.s $f5, $f5, $f7
add.s $f4, $f4, $f5
lwc1  $f7, 0x38($sp)
lwc1  $f10, 0x28($sp)
sub.s $f7, $f7, $f10          # rayDir.z
mul.s $f6, $f6, $f7
add.s $f4, $f4, $f6           # dot = N·rayDir
mtc1  $zero, $f5
nop
.word 0x46052034             # c.OLT.s f4,f5 : dot < 0 ? (front-facing)
nop
bc1f  cnohit                 # dot >= 0 -> backface -> ignore this ceiling
nop
lwc1  $f0, 0x44($sp)          # ceilingY = hit.y
swc1  $f0, 0x6c($sp)
b     cdone
nop
cnohit:
lui   $t0, 0x4800             # 131072 sentinel -> ceiling clamp inert
mtc1  $t0, $f0
nop
swc1  $f0, 0x6c($sp)
cdone:
# ===== GROUND ray: eye (sp+0x20, still set) -> down 500 -> groundY @ sp+0x5c (0 = miss). TWO-SIDED: a floor safety
#       guard for stick-down, not an occlusion test, so it catches any floor regardless of winding. =====
lwc1  $f0, 0x20($sp)          # eye.x
swc1  $f0, 0x30($sp)          # rayTo.x
lwc1  $f0, 0x24($sp)          # eye.y
lui   $t0, 0x43fa             # 500
mtc1  $t0, $f1
nop
sub.s $f0, $f0, $f1           # eye.y - 500
swc1  $f0, 0x34($sp)
lwc1  $f0, 0x28($sp)          # eye.z
swc1  $f0, 0x38($sp)
sw    $zero, 0x3c($sp)
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addiu $a2, $sp, 0x20
addiu $a3, $sp, 0x30
addiu $t0, $sp, 0x40
sw    $t0, 0x10($sp)
addiu $t1, $zero, 1
sw    $t1, 0x14($sp)
sw    $zero, 0x18($sp)
jal   0x149d50
nop
lw    $v1, 0x58($sp)
bltz  $v0, gnohit
nop
lwc1  $f0, 0x44($sp)          # groundY = hit.y
swc1  $f0, 0x5c($sp)
b     gdone
nop
gnohit:
sw    $zero, 0x5c($sp)        # no ground -> 0 (clamp goes inert: height_min becomes very negative)
gdone:
# ===== RIGHT-STICK Y -> smoothed manual height offset @ sp+0x68 (deadzone + scale, persistent ease) =====
jal   0x169a30                # GetRYf
nop
mov.s $f3, $f0
mul.s $f2, $f0, $f0           # stickY^2
lui   $t0, 0x3e23             # STICK_DZ2 (deadzone^2)
ori   $t0, $t0, 0xd70a
mtc1  $t0, $f1
nop
.word 0x46020834             # c.OLT.s f1,f2 : DZ2 < stickY^2 ?
nop
bc1f  skzero
nop
lui   $t0, 0xc1c8             # STICK_SCALE = -25 (flipped) at full deflection
mtc1  $t0, $f1
nop
mul.s $f0, $f3, $f1
b     skdone
nop
skzero:
mtc1  $zero, $f0
skdone:
lui   $t3, 0x0014
ori   $t3, $t3, 0xc020        # persistent smoothed-stick scratch @0x14C020
lwc1  $f2, 0x0($t3)
sub.s $f1, $f0, $f2
lui   $t0, 0x3da3             # STICK_EASE
ori   $t0, $t0, 0xd70a
mtc1  $t0, $f4
nop
mul.s $f1, $f1, $f4
add.s $f2, $f2, $f1
swc1  $f2, 0x0($t3)
swc1  $f2, 0x68($sp)
lw    $v1, 0x58($sp)          # reload camera (GetRYf clobbered caller-saved)
# restore ref @ sp+0x20/24/28 (ceiling ray used the eye as rayFrom)
lwc1  $f0, 0x2c0($v1)
swc1  $f0, 0x20($sp)
lwc1  $f0, 0x2c4($v1)
swc1  $f0, 0x24($sp)
lwc1  $f0, 0x2c8($v1)
swc1  $f0, 0x28($sp)
# ===== WALL ray: pivot -> (ref + BASE_DIST*dir, ref.y+REST_H) : first hit − MARGIN = desired horiz =====
lui   $t0, 0x42a0             # BASE_DIST = 80
mtc1  $t0, $f8
nop
lwc1  $f0, 0x60($sp)
mul.s $f0, $f0, $f8
lwc1  $f1, 0x20($sp)
add.s $f0, $f1, $f0
swc1  $f0, 0x30($sp)          # rayTo.x
lui   $t0, 0x4188             # REST_H = 17 (cast at the RESTING eye height, not the ducked one)
mtc1  $t0, $f1
nop
lwc1  $f2, 0x24($sp)
add.s $f1, $f2, $f1
swc1  $f1, 0x34($sp)          # rayTo.y = ref.y + REST_H
lwc1  $f0, 0x64($sp)
mul.s $f0, $f0, $f8
lwc1  $f1, 0x28($sp)
add.s $f0, $f1, $f0
swc1  $f0, 0x38($sp)          # rayTo.z
sw    $zero, 0x3c($sp)
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addiu $a2, $sp, 0x20          # rayFrom = ref (pivot)
addiu $a3, $sp, 0x30
addiu $t0, $sp, 0x40
sw    $t0, 0x10($sp)
addiu $t1, $zero, 1
sw    $t1, 0x14($sp)
sw    $zero, 0x18($sp)
jal   0x149d50
nop
lw    $v1, 0x58($sp)
bltz  $v0, nohit
nop
# ONE-SIDED: cull backface — hit poly @ s5+v0*0x50, normal @ +0x30. N·rayDir >= 0 (faces away) -> not a real wall.
sll   $t1, $v0, 6
sll   $t4, $v0, 4
addu  $t1, $t1, $t4
addu  $t1, $s5, $t1
lwc1  $f4, 0x30($t1)          # N.x
lwc1  $f5, 0x34($t1)          # N.y
lwc1  $f6, 0x38($t1)          # N.z
lwc1  $f7, 0x30($sp)
lwc1  $f10, 0x20($sp)
sub.s $f7, $f7, $f10          # rayDir.x
mul.s $f4, $f4, $f7
lwc1  $f7, 0x34($sp)
lwc1  $f10, 0x24($sp)
sub.s $f7, $f7, $f10          # rayDir.y
mul.s $f5, $f5, $f7
add.s $f4, $f4, $f5
lwc1  $f7, 0x38($sp)
lwc1  $f10, 0x28($sp)
sub.s $f7, $f7, $f10          # rayDir.z
mul.s $f6, $f6, $f7
add.s $f4, $f4, $f6           # dot = N·rayDir
mtc1  $zero, $f5
nop
.word 0x46052034             # c.OLT.s f4,f5 : dot < 0 ? (front-facing)
nop
bc1f  nohit                  # dot >= 0 -> backface -> treat as no wall
nop
lwc1  $f0, 0x40($sp)          # hit.x
lwc1  $f1, 0x20($sp)          # ref.x
sub.s $f0, $f0, $f1
mul.s $f0, $f0, $f0
lwc1  $f2, 0x48($sp)          # hit.z
lwc1  $f3, 0x28($sp)          # ref.z
sub.s $f2, $f2, $f3
mul.s $f2, $f2, $f2
add.s $f0, $f0, $f2
sqrt.s $f0, $f0              # horizontal dist ref->hit  (f0,f0 -> operand unambiguous)
lui   $t0, 0x4100             # MARGIN = 8
mtc1  $t0, $f2
nop
sub.s $f0, $f0, $f2           # desired = hit − MARGIN
b     havetarget
nop
nohit:
lui   $t0, 0x42a0             # desired = BASE_DIST (no wall)
mtc1  $t0, $f0
nop
havetarget:
# floor desired at HFLOOR
lui   $t0, 0x4080             # HFLOOR = 4
mtc1  $t0, $f2
nop
.word 0x46020034             # c.OLT.s f0,f2 : desired < HFLOOR ?
nop
bc1f  hfok
nop
mov.s $f0, $f2
hfok:
# ===== HEIGHT target = REST_H + climb(horiz) + stick, ducked by ceiling, floored by ground =====
# baseline = REST_H (FLAT). Slope-rise removed — the ground-relative baseline fought the sloped boardwalk pieces near the
# Brownboo tunnel and hurt the clean tunnel duck. The ground probe stays ONLY for the stick-down floor clamp below.
lui   $t0, 0x4188             # REST_H (flat baseline)
mtc1  $t0, $f2
nop
lui   $t0, 0x4270             # MAX_HEIGHT
mtc1  $t0, $f4
nop
sub.s $f4, $f4, $f2           # AMP = MAX_HEIGHT − REST_H
lui   $t0, 0x41f0             # CLIMB_START
mtc1  $t0, $f3
nop
sub.s $f5, $f3, $f0           # intrusion = CLIMB_START − horiz   (f0 = desired horiz, preserved for dist ease)
mtc1  $zero, $f6
nop
.word 0x46062834             # c.OLT.s f5,f6 : intrusion < 0 ?
nop
bc1f  iok
nop
mov.s $f5, $f6                # intrusion = 0
iok:
lui   $t0, 0x3c6a             # INV_RANGE = 1/CLIMB_RANGE
ori   $t0, $t0, 0x0ea1
mtc1  $t0, $f3
nop
mul.s $f5, $f5, $f3           # t
lui   $t0, 0x3f80             # 1.0
mtc1  $t0, $f6
nop
.word 0x46053034             # c.OLT.s f6,f5 : 1 < t ?
nop
bc1f  tok
nop
mov.s $f5, $f6                # t = 1
tok:
mul.s $f7, $f5, $f5           # t^2
add.s $f6, $f5, $f5           # 2t
lui   $t0, 0x4040             # 3.0
mtc1  $t0, $f8
nop
sub.s $f6, $f8, $f6           # 3 − 2t
mul.s $f5, $f7, $f6           # smoothstep s
mul.s $f5, $f5, $f4           # AMP·s
add.s $f5, $f5, $f2           # height_target = REST_H + AMP·s
lwc1  $f8, 0x68($sp)          # stick offset
add.s $f5, $f5, $f8           # + stick
lwc1  $f6, 0x6c($sp)          # ceilingY
lwc1  $f7, 0x24($sp)          # ref.y
sub.s $f6, $f6, $f7
lui   $t0, 0x4160             # MIN_CEIL_CLEAR = 14
mtc1  $t0, $f7
nop
sub.s $f6, $f6, $f7           # height_max = (ceilingY − ref.y) − MIN_CEIL_CLEAR
.word 0x46053034             # c.OLT.s f6,f5 : height_max < height_target ?
nop
bc1f  cclampok
nop
mov.s $f5, $f6                # duck: clamp height down under the ceiling
cclampok:
# ground clamp: eye stays MIN_GROUND_CLEAR above the ground under the camera (stick-down floor guard)
lwc1  $f6, 0x5c($sp)          # groundY
lwc1  $f7, 0x24($sp)          # ref.y
sub.s $f6, $f6, $f7           # groundY − ref.y
lui   $t0, 0x40c0             # MIN_GROUND_CLEAR = 6
mtc1  $t0, $f7
nop
add.s $f6, $f6, $f7           # height_min = (groundY − ref.y) + MIN_GROUND_CLEAR
.word 0x46062834             # c.OLT.s f5,f6 : height_target < height_min ?
nop
bc1f  gclampok
nop
mov.s $f5, $f6                # clamp up: never sink into the floor
gclampok:
# ===== HEIGHT ease =====
lwc1  $f6, 0x2d4($v1)
sub.s $f7, $f5, $f6
lui   $t0, 0x3e99            # HEIGHT_EASE = 0.3
ori   $t0, $t0, 0x999a
mtc1  $t0, $f3
nop
mul.s $f7, $f7, $f3
add.s $f6, $f6, $f7
swc1  $f6, 0x2d4($v1)
# ===== DIST ease =====
lwc1  $f1, 0x2d0($v1)
lui   $t0, 0x3e19            # DIST_EASE = 0.15
ori   $t0, $t0, 0x999a
mtc1  $t0, $f3
nop
sub.s $f2, $f0, $f1
mul.s $f2, $f2, $f3
add.s $f0, $f1, $f2
swc1  $f0, 0x2d0($v1)
done:
lw    $v0, 0x54($sp)
lw    $ra, 0x50($sp)
jr    $ra
addiu $sp, $sp, 0x70
