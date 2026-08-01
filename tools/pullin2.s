# REWORK v1 @0x14b838 — pull-in + ceiling-duck + right-stick height. NO ground-relative baseline / wall climb
# (those were the false-rise-under-bridges bug). Height = REST_H + stick, clamped DOWN by a ceiling probe (tunnel duck).
# dist = pivot->resting-eye first hit − MARGIN (standard spring-arm: nearest occluder from the player). Constants
# HARDCODED. Symmetric ease for now (asymmetric + HOLD + yaw come next). Frame -0x70. CheckHit 0x149d50 vs s5/s8.
# R5900: c.OLT.s = .word ...0x34; nop after every mtc1 & FP compare.
addiu $sp, $sp, -0x90
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
# (two-sided by design: CheckHit mode-1 returns the NEAREST surface above the eye = the true ceiling
#  regardless of winding — the old backface cull here was redundant and its 27 words fund the drift)
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
# ===== RIGHT-STICK X: is the user steering? flag @sp+0x84 (1 = active) suppresses the reacquisition drift =====
jal   0x1699f0                # GetRXf (same keylock/calibration path as GetRYf)
nop
mfc1  $t2, $f0
nop
sll   $t2, $t2, 1             # drop the sign bit: |x| compare on raw float bits
lui   $t3, 0x7d00             # 0.25² threshold trick: 0x3E800000 (0.25) << 1
sltu  $t2, $t3, $t2           # 1 if |x| > 0.25
sw    $t2, 0x84($sp)
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
ori   $t3, $t3, 0xc200        # persistent smoothed-stick scratch @0x14C200 (past func slack; freed 0x14C020 for code growth)
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
# WALL CLASSIFIER: pull-in (and its climb) reacts only to WALL-like polys. A downhill SLOPE behind the player also
# crosses the horizontal ray and was driving pull-in + full climb (the "slope rise"). Floor-like hits
# (N.y² >= WALL_MAX_NY²·|N|²) fall through to nohit — the swept-slide still keeps the eye off the slope surface.
lwc1  $f4, 0x30($t1)          # reload N (the backface cull consumed it)
lwc1  $f5, 0x34($t1)
lwc1  $f6, 0x38($t1)
mul.s $f4, $f4, $f4
mul.s $f7, $f5, $f5           # N.y²
add.s $f4, $f4, $f7
mul.s $f6, $f6, $f6
add.s $f4, $f4, $f6           # |N|²
lui   $t0, 0x3eb8             # WALL_MAX_NY² (PutEase; 0.36 → |N̂.y| < 0.6 counts as wall)
ori   $t0, $t0, 0x51ec
mtc1  $t0, $f8
nop
mul.s $f4, $f4, $f8           # thresh = WALL_MAX_NY²·|N|²
.word 0x46043834             # c.OLT.s f7,f4 : N.y² < thresh ? (wall-like)
nop
bc1f  nohit                  # floor/slope-like -> no pull-in, no climb
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
# ===== eases: compute h_e/d_e but DO NOT store yet — the swept-slide constrains them first =====
lwc1  $f6, 0x2d4($v1)
sub.s $f7, $f5, $f6
lui   $t0, 0x3e99            # HEIGHT_EASE
ori   $t0, $t0, 0x999a
mtc1  $t0, $f3
nop
mul.s $f7, $f7, $f3
add.s $f6, $f6, $f7
swc1  $f6, 0x68($sp)          # h_e (eased height target; slot free after stick was consumed)
lwc1  $f1, 0x2d0($v1)
lui   $t0, 0x3e19            # DIST_EASE
ori   $t0, $t0, 0x999a
mtc1  $t0, $f3
nop
sub.s $f2, $f0, $f1
mul.s $f2, $f2, $f3
add.s $f0, $f1, $f2
swc1  $f0, 0x5c($sp)          # d_e (eased dist target; groundY slot free after the clamp)
# ===== SWEPT-SLIDE v2: sweep from LAST FRAME'S constrained eye target (persisted @0x14C210) to this frame's E1.
# The old origin was rebuilt from THIS frame's ref, so the whole segment teleported with the player and follow/
# walk-driven crossings were invisible (both endpoints already past the wall). A persisted origin captures EVERY
# motion source: ref translation, follow angle, stick, dist, height. On a crossing: push E1 to SLIDE_MARGIN on the
# AUTHORED-normal side via the 3-axis control decomposition (dist/height/angle) — slide, not stop.
# E1 = ref + d_e·(sinT,_,cosT) + (0,h_e,0)
lwc1  $f1, 0x5c($sp)          # d_e
lwc1  $f2, 0x60($sp)          # sinT
mul.s $f2, $f2, $f1
lwc1  $f3, 0x2c0($v1)
add.s $f2, $f3, $f2
swc1  $f2, 0x30($sp)          # E1.x
lwc1  $f2, 0x2c4($v1)
lwc1  $f3, 0x68($sp)          # h_e
add.s $f2, $f2, $f3
swc1  $f2, 0x34($sp)          # E1.y
lwc1  $f2, 0x64($sp)          # cosT
mul.s $f2, $f2, $f1
lwc1  $f3, 0x2c8($v1)
add.s $f2, $f3, $f2
swc1  $f2, 0x38($sp)          # E1.z
sw    $zero, 0x3c($sp)
# E_prev @0x14C210 (16-aligned quad, w @+0xC). All-zero triple = never stored (patch zero-inits) -> skip.
# |E1 − E_prev|² > 16384 (128u jump) = teleport/area change -> skip (don't drag the camera across the map).
lui   $t3, 0x0014
ori   $t3, $t3, 0xc210
lw    $t0, 0x0($t3)
lw    $t1, 0x4($t3)
lw    $t2, 0x8($t3)
or    $t0, $t0, $t1
or    $t0, $t0, $t2
beq   $t0, $zero, sskip
nop
lwc1  $f7, 0x0($t3)
lwc1  $f8, 0x30($sp)
sub.s $f7, $f7, $f8
mul.s $f9, $f7, $f7
lwc1  $f7, 0x4($t3)
lwc1  $f8, 0x34($sp)
sub.s $f7, $f7, $f8
mul.s $f7, $f7, $f7
add.s $f9, $f9, $f7
lwc1  $f7, 0x8($t3)
lwc1  $f8, 0x38($sp)
sub.s $f7, $f7, $f8
mul.s $f7, $f7, $f7
add.s $f9, $f9, $f7           # |E1 − E_prev|²
lui   $t0, 0x4680             # TELEPORT² = 16384 (= 128²)
mtc1  $t0, $f8
nop
.word 0x46094034             # c.OLT.s f8,f9 : TELEPORT² < |Δ|² ?
nop
bc1t  sskip
nop
# PROXIMITY EXTENSION: extend the cast TIP past E1 by SLIDE_MARGIN along the motion, so the constraint fires within
# the margin BAND (small continuous corrections) instead of only on a plane crossing — the binary hit/miss was the
# shimmer against head-on walls. Tip @sp+0x70 (16-aligned quad); E1 @0x30 stays the point p/persist measure.
# Skip when nearly stationary (|Δ|² < 1 — direction too noisy, and a static eye needs no proximity push).
lui   $t0, 0x3f80             # 1.0
mtc1  $t0, $f8
nop
.word 0x46084834             # c.OLT.s f9,f8 : |Δ|² < 1 ?
nop
bc1t  snoext
nop
.word 0x46090244             # sqrt.s f9,f9 : L = |E1 − E_prev|
nop
lui   $t0, 0x40e0             # SLIDE_MARGIN (extension reach; PutVal slot #1)
mtc1  $t0, $f8
nop
add.s $f8, $f9, $f8           # L + M
div.s $f8, $f8, $f9           # s = (L+M)/L
nop
nop
lwc1  $f7, 0x30($sp)
lwc1  $f1, 0x0($t3)
sub.s $f7, $f7, $f1
mul.s $f7, $f7, $f8
add.s $f7, $f1, $f7
swc1  $f7, 0x70($sp)          # tip.x
lwc1  $f7, 0x34($sp)
lwc1  $f1, 0x4($t3)
sub.s $f7, $f7, $f1
mul.s $f7, $f7, $f8
add.s $f7, $f1, $f7
swc1  $f7, 0x74($sp)          # tip.y
lwc1  $f7, 0x38($sp)
lwc1  $f1, 0x8($t3)
sub.s $f7, $f7, $f1
mul.s $f7, $f7, $f8
add.s $f7, $f1, $f7
swc1  $f7, 0x78($sp)          # tip.z
sw    $zero, 0x7c($sp)
b     sext
nop
snoext:
lwc1  $f7, 0x30($sp)
swc1  $f7, 0x70($sp)
lwc1  $f7, 0x34($sp)
swc1  $f7, 0x74($sp)
lwc1  $f7, 0x38($sp)
swc1  $f7, 0x78($sp)
sw    $zero, 0x7c($sp)
sext:
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addu  $a2, $t3, $zero         # from = E_prev (persisted; the aligned scratch quad)
addiu $a3, $sp, 0x70          # to = extended tip (E1 + margin reach)
addiu $t0, $sp, 0x40
sw    $t0, 0x10($sp)
addiu $t1, $zero, 1
sw    $t1, 0x14($sp)
sw    $zero, 0x18($sp)
jal   0x149d50               # CheckHit E_prev->E1 (TWO-SIDED — no winding cull here by design)
nop
lw    $v1, 0x58($sp)
bltz  $v0, nocorr
nop
sll   $t1, $v0, 6
sll   $t2, $v0, 4
addu  $t1, $t1, $t2
addu  $t1, $s5, $t1           # hit poly base
lwc1  $f4, 0x30($t1)          # N.x
lwc1  $f5, 0x34($t1)          # N.y
lwc1  $f6, 0x38($t1)          # N.z
# normalize N (gathered CCPoly normals are NOT unit) so `need` is a true distance and the basis decomposition is exact
mul.s $f7, $f4, $f4
mul.s $f8, $f5, $f5
add.s $f7, $f7, $f8
mul.s $f8, $f6, $f6
add.s $f7, $f7, $f8           # N·N
.word 0x460701C4             # sqrt.s f7,f7 (R5900 ft-operand form)
nop
lui   $t0, 0x3f80             # 1.0
mtc1  $t0, $f8
nop
div.s $f8, $f8, $f7           # 1/|N|
nop
nop
mul.s $f4, $f4, $f8
mul.s $f5, $f5, $f8
mul.s $f6, $f6, $f8
# SIDE RULE: the eye must stay on the AUTHORED-normal side (all _c faces point into the play area). No flip — the
# safe side is a per-poly constant, so a penetrated eye can't switch the constraint's allegiance (the old E0-derived
# side did exactly that once the stick leaked the eye past the plane), and a wrong-side eye is pushed BACK (recovery).
# p = N̂·(E1 − P): signed height of E1 on the authored side (p <= 0 means at/behind the wall)
lwc1  $f7, 0x30($sp)
lwc1  $f8, 0x40($sp)
sub.s $f7, $f7, $f8
mul.s $f10, $f7, $f4
lwc1  $f7, 0x34($sp)
lwc1  $f8, 0x44($sp)
sub.s $f7, $f7, $f8
mul.s $f7, $f7, $f5
add.s $f10, $f10, $f7
lwc1  $f7, 0x38($sp)
lwc1  $f8, 0x48($sp)
sub.s $f7, $f7, $f8
mul.s $f7, $f7, $f6
add.s $f10, $f10, $f7         # p
lui   $t0, 0x40e0             # SLIDE_MARGIN (need standoff; PutVal slot #2 — keep <= pull-in MARGIN or the two setpoints oscillate)
mtc1  $t0, $f11
nop
sub.s $f11, $f11, $f10        # need = SLIDE_MARGIN − p
mtc1  $zero, $f10
nop
.word 0x460B5034             # c.OLT.s f10,f11 : 0 < need ?
nop
bc1f  nocorr
nop
# WEIGHTED control-space slide (free glide): decompose N̂ on the orthonormal basis boom/vertical/orbit-tangent, but
# DOWN-WEIGHT the angle axis by SLIDE_BIAS=B2 so dist/height do the resolving and the user's/follow's rotation is
# barely resisted — the eye glides along oblique walls (pulls in / backs off), instead of the min-norm push that
# cancelled part of the rotation every frame (the "constrained" feel). Weighted min-norm:
#   k = need/(n_d² + n_h² + B2·n_t²);  Δd = k·n_d,  Δh = k·n_h,  Δθ = k·B2·n_t/d.
# B2=1 -> old neutral behavior; small B2 -> free slide. Tangential walls (n_d,n_h≈0) still resolve FULLY through
# the angle (B2 cancels in that limit). L2w ∈ [B2, 1] since |N̂|=1 — never zero, no guard needed.
lwc1  $f7, 0x60($sp)          # sinT
mul.s $f7, $f7, $f4
lwc1  $f8, 0x64($sp)          # cosT
mul.s $f8, $f8, $f6
add.s $f7, $f7, $f8           # n_d = N.x·sinT + N.z·cosT
swc1  $f7, 0x88($sp)          # stash n_d for the reacquisition slide
lwc1  $f8, 0x64($sp)
mul.s $f8, $f8, $f4
lwc1  $f9, 0x60($sp)
mul.s $f9, $f9, $f6
sub.s $f12, $f8, $f9          # n_t = N.x·cosT − N.z·sinT
mul.s $f8, $f7, $f7           # n_d²
mul.s $f9, $f5, $f5           # n_h²
add.s $f8, $f8, $f9
mul.s $f9, $f12, $f12         # n_t²
lui   $t0, 0x3d80             # SLIDE_BIAS B2 (angle-axis weight²; PutEase)
ori   $t0, $t0, 0x0000
mtc1  $t0, $f10
nop
mul.s $f9, $f9, $f10
add.s $f8, $f8, $f9           # L2w = n_d² + n_h² + B2·n_t²
div.s $f11, $f11, $f8         # k = need / L2w
nop
nop
mul.s $f4, $f11, $f7          # a = k·n_d       (dist push; N.x no longer needed)
mul.s $f5, $f11, $f5          # c = k·n_h       (height push)
mul.s $f6, $f11, $f12
mul.s $f6, $f6, $f10          # b = k·B2·n_t    (tangent push = Δθ·d; N.z no longer needed)
lwc1  $f0, 0x5c($sp)          # d_e
add.s $f0, $f0, $f4           # d' = d_e + a
lwc1  $f2, 0x68($sp)          # h_e
add.s $f2, $f2, $f5           # h' = h_e + c
lwc1  $f9, 0x5c($sp)          # d_e = the angle's lever arm
div.s $f1, $f6, $f9           # Δθ = b / d_e
nop
nop
lwc1  $f3, 0x2d8($v1)
add.s $f3, $f3, $f1
swc1  $f3, 0x2d8($v1)         # nudge the TARGET angle out along the wall (the follow re-bases it next frame)
# FRICTION (contact frames only): damp the target angle's LEAD over the rendered angle so the slide along the wall
# slows while touching it (and target churn on head-on walls is damped). Lead wrapped to [−π,π] before scaling.
lwc1  $f7, 0x2dc($v1)         # angS (rendered)
sub.s $f8, $f3, $f7           # lead = angT' − angS
lui   $t0, 0x4049
ori   $t0, $t0, 0x0fdb
mtc1  $t0, $f9                # π
nop
.word 0x46084834             # c.OLT.s f9,f8 : π < lead ?
nop
bc1f  fw1
nop
lui   $t0, 0x40c9
ori   $t0, $t0, 0x0fdb
mtc1  $t0, $f10               # 2π
nop
sub.s $f8, $f8, $f10
fw1:
neg.s $f10, $f9               # −π
.word 0x460A4034             # c.OLT.s f8,f10 : lead < −π ?
nop
bc1f  fw2
nop
lui   $t0, 0x40c9
ori   $t0, $t0, 0x0fdb
mtc1  $t0, $f10
nop
add.s $f8, $f8, $f10
fw2:
lui   $t0, 0x3f19             # SLIDE_FRICTION keep-factor (PutEase; 1.0 = no friction, lower = more drag)
ori   $t0, $t0, 0x999a
mtc1  $t0, $f10
nop
mul.s $f8, $f8, $f10
add.s $f3, $f7, $f8           # angT'' = angS + keep·lead
swc1  $f3, 0x2d8($v1)
# ===== REACQUISITION SLIDE (contact frames, stick idle): project the restoring pull toward the RESTING DISTANCE
# onto the wall's tangent plane and move along it — a POSITION slide; the rotation is a side effect
# (Δθ = tangential motion / lever arm), not a turn rate. The projection of the pull can never point away from
# rest (deviation is monotone non-increasing) and a head-on wall projects to ZERO -> no back-and-forth.
lw    $t0, 0x84($sp)
bne   $t0, $zero, drfx        # user steering -> no auto-slide
nop
lui   $t0, 0x42a0             # BASE_DIST (the resting distance)
mtc1  $t0, $f7
nop
sub.s $f7, $f7, $f0           # W = BASE_DIST − d'  (restoring pull along the boom; − = stretched, + = pinned short)
lwc1  $f9, 0x88($sp)          # n_d (wall normal's boom component)
mul.s $f11, $f7, $f9          # dot = W·n_d  (the pull's into-wall part)
lui   $t0, 0x3e00             # SLIDE_GAIN (PutVal; fraction of the projected pull applied per frame)
mtc1  $t0, $f10
nop
mul.s $f3, $f11, $f9
sub.s $f3, $f7, $f3           # S_d = W − dot·n_d  (tangent-plane projection, boom part)
mul.s $f3, $f3, $f10
add.s $f0, $f0, $f3           # d' += gain·S_d
mul.s $f3, $f11, $f12         # dot·n_t
neg.s $f3, $f3                # S_t = −dot·n_t  (tangent-plane projection, orbit part)
mul.s $f3, $f3, $f10
div.s $f3, $f3, $f0           # Δθ = gain·S_t / d  — the rotation EMERGES from the slide
nop
nop
lwc1  $f9, 0x2d8($v1)
add.s $f9, $f9, $f3
swc1  $f9, 0x2d8($v1)
drfx:
# persist E1' = E1 + a·b̂ + c·ŷ + b·t̂ — the ACTUAL world displacement incl. the tangential slide — as next frame's
# sweep origin (sits at the margin -> a target still pushed inward crosses EVERY frame -> continuous, no jitter)
lui   $t3, 0x0014
ori   $t3, $t3, 0xc210
lwc1  $f7, 0x60($sp)          # sinT
lwc1  $f8, 0x64($sp)          # cosT
mul.s $f1, $f4, $f7           # a·sinT
mul.s $f3, $f6, $f8           # b·cosT
add.s $f1, $f1, $f3
lwc1  $f3, 0x30($sp)
add.s $f1, $f3, $f1
swc1  $f1, 0x0($t3)           # E1'.x
lwc1  $f3, 0x34($sp)
add.s $f3, $f3, $f5
swc1  $f3, 0x4($t3)           # E1'.y
mul.s $f1, $f4, $f8           # a·cosT
mul.s $f3, $f6, $f7           # b·sinT
sub.s $f1, $f1, $f3
lwc1  $f3, 0x38($sp)
add.s $f1, $f3, $f1
swc1  $f1, 0x8($t3)           # E1'.z
sw    $zero, 0xc($t3)
b     sfin
nop
sskip:
nocorr:
# no constraint this frame: persist raw E1 as the next sweep origin, targets unchanged
lui   $t3, 0x0014
ori   $t3, $t3, 0xc210
lwc1  $f7, 0x30($sp)
swc1  $f7, 0x0($t3)
lwc1  $f7, 0x34($sp)
swc1  $f7, 0x4($t3)
lwc1  $f7, 0x38($sp)
swc1  $f7, 0x8($t3)
sw    $zero, 0xc($t3)
lwc1  $f0, 0x5c($sp)
lwc1  $f2, 0x68($sp)
sfin:
swc1  $f0, 0x2d0($v1)
swc1  $f2, 0x2d4($v1)
done:
lw    $v0, 0x54($sp)
lw    $ra, 0x50($sp)
jr    $ra
addiu $sp, $sp, 0x90
