# OCCLUSION AUTOROTATE v2 (clearance FAN) @0x27D090 — the historical camera-helper cave (SmoothRest/predictive
# era; 262 zero words @0x27D084, execution-proven, ISO-baked). Called from tools/pullin2.s after the LOS cast:
# a0 = LOS raw hit index (unused in v2 — the fan decides), a1 = caller sp (pivot @0x20, sin/cos(angS) @0x60/0x64,
# stick flag @0x84, hitOut scratch @0x40, probe-to scratch @0x10 — all in the CALLER frame).
# Port of TownCamera.cs: obscured = fan-min clearance² < AUTO_CLEAR2 ((restArm − pad)²); probe fans at
# yaw±AUTO_PROBE decide direction; swing angT at AUTO_RATE·blend, blend eased @0x14C21C (BLEND_EASE).
# Fan = min of centre + yaw±AUTO_FAN rays, so a grazing peek reads low and the swing continues until there is
# real lateral room — the C# "padding" feel.
# EE rules: CheckHit args 5-7 in t0/t1/t2 EXPLICITLY; no FP held across any jal; nop after mtc1/FP-compare.
addiu $sp, $sp, -0x60
sw    $ra, 0x58($sp)
sw    $s0, 0x54($sp)
addu  $s0, $a1, $zero         # s0 = caller sp (callee-saved)
# gate 1: user steering -> reset the idle timer, decay blend, no rotate
lw    $t0, 0x84($s0)
beq   $t0, $zero, aridle
nop
lui   $t3, 0x0028
sw    $zero, -0x2b6c($t3)     # idle timer @0x27D494 := 0 (cave tail; zero in the ISO image)
b     arblend0
nop
aridle:
# (contact gate removed by user request — the swing now runs during slide contact too; the flag @0x14C208
#  is still written by the camera function each frame if we want this gate back)
# gate 3: idle delay -- swing only after AUTO_DELAY frames of stick idleness
lui   $t3, 0x0028
lwc1  $f1, -0x2b6c($t3)       # idle timer (float frames)
lui   $t0, 0x3f80             # 1.0
mtc1  $t0, $f2
nop
add.s $f1, $f1, $f2
swc1  $f1, -0x2b6c($t3)       # timer++
lui   $t0, 0x42f0             # AUTO_DELAY (PutVal; frames of idleness before the swing may start; 120 = 2 s)
mtc1  $t0, $f2
nop
.word 0x46020834             # c.OLT.s f1,f2 : timer < delay ?
nop
bc1t  arblend0
nop
# ---- CENTER fan: dir = (sinY, cosY)
lwc1  $f8, 0x60($s0)
lwc1  $f9, 0x64($s0)
jal   arfan
nop
swc1  $f1, 0x34($sp)          # C2 = centre fan clearance²
# obscured? C2 < AUTO_CLEAR2 = (restArm − AUTO_CLEAR_PAD)²
lui   $t0, 0x4573             # AUTO_CLEAR2 (PutEase; derived in C#)
ori   $t0, $t0, 0xd83f
mtc1  $t0, $f2
nop
.word 0x46020834             # c.OLT.s f1,f2 : C2 < clear2 ? (occluded)
nop
bc1f  arblend0
nop
# ---- LEFT fan: dir = compose(yaw + AUTO_PROBE)
lui   $t0, 0x3eaf             # AUTO_PROBE_SIN (PutEase)
ori   $t0, $t0, 0x904d
mtc1  $t0, $f4
nop
lui   $t0, 0x3f70             # AUTO_PROBE_COS (PutEase)
ori   $t0, $t0, 0x7abb
mtc1  $t0, $f5
nop
lwc1  $f6, 0x60($s0)          # sinY
lwc1  $f7, 0x64($s0)          # cosY
mul.s $f8, $f6, $f5
mul.s $f1, $f7, $f4
add.s $f8, $f8, $f1           # sin(y+p)
mul.s $f9, $f7, $f5
mul.s $f1, $f6, $f4
sub.s $f9, $f9, $f1           # cos(y+p)
swc1  $f4, 0x40($sp)          # stash sinP/cosP for the R path (one PutEase pair instead of two)
swc1  $f5, 0x44($sp)
jal   arfan
nop
swc1  $f1, 0x38($sp)          # L2
# ---- RIGHT fan: dir = compose(yaw - AUTO_PROBE)
lwc1  $f4, 0x40($sp)          # sinP (stashed)
lwc1  $f5, 0x44($sp)          # cosP
lwc1  $f6, 0x60($s0)
lwc1  $f7, 0x64($s0)
mul.s $f8, $f6, $f5
mul.s $f1, $f7, $f4
sub.s $f8, $f8, $f1           # sin(y-p)
mul.s $f9, $f7, $f5
mul.s $f1, $f6, $f4
add.s $f9, $f9, $f1           # cos(y-p)
jal   arfan
nop
swc1  $f1, 0x3c($sp)          # R2
# ---- direction + canRotate (best side must beat the centre fan)
lwc1  $f1, 0x38($sp)          # L2
lwc1  $f2, 0x3c($sp)          # R2
lwc1  $f3, 0x34($sp)          # C2
.word 0x46020834             # c.OLT.s f1,f2 : L2 < R2 ?
nop
bc1t  arright
nop
.word 0x46011834             # c.OLT.s f3,f1 : C2 < L2 ? (left improves)
nop
bc1f  arblend0
nop
addu  $t1, $zero, $zero       # dir 0 = left (+yaw)
b     arswing
nop
arright:
.word 0x46021834             # c.OLT.s f3,f2 : C2 < R2 ? (right improves)
nop
bc1f  arblend0
nop
addiu $t1, $zero, 1           # dir 1 = right (-yaw)
arswing:
# blend += (1 - blend)·BLEND_EASE  @0x14C21C
lui   $t3, 0x0014
ori   $t3, $t3, 0xc21c
lwc1  $f2, 0x0($t3)
lui   $t0, 0x3f80             # 1.0
mtc1  $t0, $f3
nop
sub.s $f3, $f3, $f2
lui   $t0, 0x3df5             # BLEND_EASE (PutEase)
ori   $t0, $t0, 0xc28f
mtc1  $t0, $f4
nop
mul.s $f3, $f3, $f4
add.s $f2, $f2, $f3
swc1  $f2, 0x0($t3)
# angT += ±AUTO_RATE·blend
lui   $t0, 0x3dcc             # AUTO_RATE (PutEase)
ori   $t0, $t0, 0xcccd
mtc1  $t0, $f3
nop
beq   $t1, $zero, arsgn
nop
neg.s $f3, $f3                # right = -yaw
arsgn:
mul.s $f3, $f3, $f2
lui   $at, 0x1d2
lw    $v1, -0x6988($at)
lwc1  $f4, 0x2d8($v1)
add.s $f4, $f4, $f3
swc1  $f4, 0x2d8($v1)
b     ardone
nop
arblend0:
# decay blend by BLEND_EASE
lui   $t3, 0x0014
ori   $t3, $t3, 0xc21c
lwc1  $f2, 0x0($t3)
lui   $t0, 0x3df5             # BLEND_EASE (decay slot)
ori   $t0, $t0, 0xc28f
mtc1  $t0, $f3
nop
mul.s $f3, $f2, $f3
sub.s $f2, $f2, $f3
swc1  $f2, 0x0($t3)
ardone:
lw    $s0, 0x54($sp)
lw    $ra, 0x58($sp)
jr    $ra
addiu $sp, $sp, 0x60
# ---- arfan(f8=sin, f9=cos) -> f1 = min clearance² of centre + dir±AUTO_FAN. Spills across the probe jals.
arfan:
addiu $sp, $sp, -0x30
sw    $ra, 0x28($sp)
swc1  $f8, 0x10($sp)          # dir sin
swc1  $f9, 0x14($sp)          # dir cos
jal   arprobe                 # centre ray (f8/f9 live)
nop
swc1  $f1, 0x18($sp)          # running min
# +FAN
lui   $t0, 0x3e04             # AUTO_FAN_SIN (PutEase)
ori   $t0, $t0, 0xbed0
mtc1  $t0, $f4
nop
lui   $t0, 0x3f7d             # AUTO_FAN_COS (PutEase)
ori   $t0, $t0, 0xd700
mtc1  $t0, $f5
nop
lwc1  $f6, 0x10($sp)
lwc1  $f7, 0x14($sp)
mul.s $f8, $f6, $f5
mul.s $f1, $f7, $f4
add.s $f8, $f8, $f1
mul.s $f9, $f7, $f5
mul.s $f1, $f6, $f4
sub.s $f9, $f9, $f1
swc1  $f4, 0x1c($sp)          # stash sinF/cosF for the -FAN leg (one PutEase pair instead of two)
swc1  $f5, 0x20($sp)
jal   arprobe
nop
lwc1  $f2, 0x18($sp)
.word 0x46020834             # c.OLT.s f1,f2 : new < min ?
nop
bc1f  arfmin1
nop
swc1  $f1, 0x18($sp)
arfmin1:
# -FAN
lwc1  $f4, 0x1c($sp)          # sinF (stashed)
lwc1  $f5, 0x20($sp)          # cosF
lwc1  $f6, 0x10($sp)
lwc1  $f7, 0x14($sp)
mul.s $f8, $f6, $f5
mul.s $f1, $f7, $f4
sub.s $f8, $f8, $f1
mul.s $f9, $f7, $f5
mul.s $f1, $f6, $f4
add.s $f9, $f9, $f1
jal   arprobe
nop
lwc1  $f2, 0x18($sp)
.word 0x46020834             # c.OLT.s f1,f2 : new < min ?
nop
bc1t  arfret
nop
mov.s $f1, $f2                # keep the running min
arfret:
lw    $ra, 0x28($sp)
jr    $ra
addiu $sp, $sp, 0x30
# ---- arprobe(f8=sin, f9=cos) -> f1 = clearance² along dir at the rest arm (huge if clear).
arprobe:
addiu $sp, $sp, -0x20
sw    $ra, 0x18($sp)
lui   $t0, 0x42a0             # AUTO_ARM (PutVal; probe arm length = BASE_DIST)
mtc1  $t0, $f1
nop
mul.s $f8, $f8, $f1
mul.s $f9, $f9, $f1
lwc1  $f2, 0x20($s0)          # pivot.x
add.s $f8, $f2, $f8
swc1  $f8, 0x10($s0)          # probe-to quad in caller frame @0x10 (16-aligned)
lwc1  $f2, 0x24($s0)
lwc1  $f3, 0x68($s0)          # h_e: probe at the camera's ACTUAL eased height
add.s $f2, $f2, $f3
swc1  $f2, 0x14($s0)
lwc1  $f2, 0x28($s0)
add.s $f9, $f2, $f9
swc1  $f9, 0x18($s0)
sw    $zero, 0x1c($s0)
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addiu $a2, $s0, 0x20          # from = pivot: the ray runs PLAYER -> camera-side, so the winding cull below
                              #   gives authored one-sided semantics (backface crossings = exiting a shell)
addiu $a3, $s0, 0x10          # to = probe endpoint
addiu $t0, $s0, 0x40          # hitOut (caller scratch; the sweep rewrites it later)
addiu $t1, $zero, 1           # mode = nearest
addu  $t2, $zero, $zero       # skip = 0
jal   0x149d50
nop
bltz  $v0, arclear
nop
sll   $t1, $v0, 6             # hit poly = s5 + v0*0x50
sll   $t2, $v0, 4
addu  $t1, $t1, $t2
addu  $t1, $s5, $t1
lwc1  $f4, 0x30($t1)          # N (unnormalized — only the dot's SIGN is used)
lwc1  $f5, 0x34($t1)
lwc1  $f6, 0x38($t1)
lwc1  $f1, 0x40($s0)          # per-component delta d = hit − pivot (∝ ray direction: hit lies ON the ray)
lwc1  $f2, 0x20($s0)
sub.s $f1, $f1, $f2           # d.x
mul.s $f7, $f1, $f4           # dot += d·N — accumulated alongside clearance²
mul.s $f1, $f1, $f1           # c2 = d.x²
lwc1  $f2, 0x44($s0)
lwc1  $f3, 0x24($s0)
sub.s $f2, $f2, $f3           # d.y
mul.s $f8, $f2, $f5
add.s $f7, $f7, $f8
mul.s $f2, $f2, $f2
add.s $f1, $f1, $f2
lwc1  $f2, 0x48($s0)
lwc1  $f3, 0x28($s0)
sub.s $f2, $f2, $f3           # d.z
mul.s $f8, $f2, $f6
add.s $f7, $f7, $f8           # dot = (hit−pivot)·N
mul.s $f2, $f2, $f2
add.s $f1, $f1, $f2           # clearance²
mfc1  $t0, $f7                # WINDING CULL: dot > 0 = the ray EXITS through this face's back — the player is
nop                           #   INSIDE that shell, and `_c` is invisible geometry: not a real occluder
bgtz  $t0, arclear
nop
b     arpret
nop
arclear:
lui   $t0, 0x4c00             # miss -> huge clearance²
mtc1  $t0, $f1
nop
arpret:
lw    $ra, 0x18($sp)
jr    $ra
addiu $sp, $sp, 0x20
