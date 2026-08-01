# INCREMENT 2: SCORED PITCH-FAN camera @0x14b838 (generated). Continuous per-boom pull-in + nearest-to-rest.
# L_MAX=82.0 BASE_DIST=80.0 REST_H=17.0 MARGIN=8.0 pitches=[12.0, -4.0, -20.0]
# f20=CH(cos pitch) f21=SH(sin pitch) saved across CheckHit. best: score@0x70 d@0x74 h@0x78. Frame -0x80.
addiu $sp, $sp, -0x80
sw    $ra, 0x50($sp)
swc1  $f20, 0x7c($sp)
swc1  $f21, 0x5c($sp)          # f21 saved @0x5c (safe high slot; NOT the 0x0..0xc arg-spill area)
jal   0x14a080
nop
sw    $v0, 0x54($sp)
lui   $at, 0x1d2
lw    $v1, -0x6988($at)
beq   $v1, $zero, done
nop
sw    $v1, 0x58($sp)
lwc1  $f0, 0x2c0($v1)
swc1  $f0, 0x20($sp)
lwc1  $f0, 0x2c4($v1)
swc1  $f0, 0x24($sp)
lwc1  $f0, 0x2c8($v1)
swc1  $f0, 0x28($sp)
sw    $zero, 0x2c($sp)
sw    $zero, 0x3c($sp)
lwc1  $f12, 0x2dc($v1)
jal   0x11d8a0                 # sin(angT)
nop
swc1  $f0, 0x60($sp)
lw    $v1, 0x58($sp)
lwc1  $f12, 0x2dc($v1)
jal   0x11d6b0                 # cos(angT)
nop
swc1  $f0, 0x64($sp)
lw    $v1, 0x58($sp)
lui   $t0, 0x42a4
ori   $t0, $t0, 0x0000
mtc1  $t0, $f8
nop
lwc1  $f0, 0x60($sp)
mul.s $f0, $f0, $f8
swc1  $f0, 0x68($sp)           # LMsin = L_MAX*sin
lwc1  $f0, 0x64($sp)
mul.s $f0, $f0, $f8
swc1  $f0, 0x6c($sp)           # LMcos = L_MAX*cos
lui   $t0, 0x4e6e
ori   $t0, $t0, 0x6b28
mtc1  $t0, $f0
nop
swc1  $f0, 0x70($sp)           # best_score = +inf
addiu $t0, $sp, 0x40
sw    $t0, 0x10($sp)
addiu $t1, $zero, 1
sw    $t1, 0x14($sp)
sw    $zero, 0x18($sp)
# ===== candidate 0: pitch 12.0 deg  CH=0.9781 SH=0.2079 =====
lui   $t0, 0x3f7a
ori   $t0, $t0, 0x67e2
mtc1  $t0, $f20
nop
lui   $t0, 0x3e54
ori   $t0, $t0, 0xe6cd
mtc1  $t0, $f21
nop
lwc1  $f0, 0x68($sp)
mul.s $f0, $f0, $f20
lwc1  $f1, 0x20($sp)
add.s $f0, $f0, $f1
swc1  $f0, 0x30($sp)           # rayTo.x
lwc1  $f0, 0x6c($sp)
mul.s $f0, $f0, $f20
lwc1  $f1, 0x28($sp)
add.s $f0, $f0, $f1
swc1  $f0, 0x38($sp)           # rayTo.z
lui   $t0, 0x42a4
ori   $t0, $t0, 0x0000
mtc1  $t0, $f7
nop
mul.s $f0, $f7, $f21           # L_MAX*SH
lwc1  $f1, 0x24($sp)
add.s $f0, $f0, $f1
swc1  $f0, 0x34($sp)           # rayTo.y
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addiu $a2, $sp, 0x20
addiu $a3, $sp, 0x30
jal   0x149d50
nop
lw    $v1, 0x58($sp)
lui   $t0, 0x3f7a
ori   $t0, $t0, 0x67e2
mtc1  $t0, $f20
nop
lui   $t0, 0x3e54
ori   $t0, $t0, 0xe6cd
mtc1  $t0, $f21
nop
bltz  $v0, c0clear
nop
sll   $t1, $v0, 6
sll   $t2, $v0, 4
addu  $t1, $t1, $t2            # v0*0x50
addu  $t1, $s5, $t1            # hit poly base
lwc1  $f8, 0x30($t1)          # N.x
lwc1  $f9, 0x34($t1)          # N.y
lwc1  $f10, 0x38($t1)         # N.z
lwc1  $f11, 0x30($sp)
lwc1  $f12, 0x20($sp)
sub.s $f11, $f11, $f12        # rayDir.x
mul.s $f8, $f8, $f11
lwc1  $f11, 0x34($sp)
lwc1  $f12, 0x24($sp)
sub.s $f11, $f11, $f12        # rayDir.y
mul.s $f9, $f9, $f11
add.s $f8, $f8, $f9
lwc1  $f11, 0x38($sp)
lwc1  $f12, 0x28($sp)
sub.s $f11, $f11, $f12        # rayDir.z
mul.s $f10, $f10, $f11
add.s $f8, $f8, $f10          # dot = N . rayDir
mtc1  $zero, $f9
nop
.word 0x46094034              # c.OLT.s f8,f9 : dot < 0 ? (front-facing)
nop
bc1f  c0clear                # dot >= 0 -> backface -> treat boom as clear
nop
lwc1  $f0, 0x60($sp)
mul.s $f0, $f0, $f20           # dir.x = sin*CH
lwc1  $f1, 0x40($sp)
lwc1  $f2, 0x20($sp)
sub.s $f1, $f1, $f2
mul.s $f1, $f1, $f0            # (hit.x-ref.x)*dir.x
lwc1  $f0, 0x64($sp)
mul.s $f0, $f0, $f20           # dir.z = cos*CH
lwc1  $f2, 0x48($sp)
lwc1  $f3, 0x28($sp)
sub.s $f2, $f2, $f3
mul.s $f2, $f2, $f0            # (hit.z-ref.z)*dir.z
add.s $f1, $f1, $f2
lwc1  $f2, 0x44($sp)
lwc1  $f3, 0x24($sp)
sub.s $f2, $f2, $f3
mul.s $f2, $f2, $f21           # (hit.y-ref.y)*dir.y (dir.y=SH)
add.s $f1, $f1, $f2            # L_hit
lui   $t0, 0x4100
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
sub.s $f1, $f1, $f0            # L = L_hit - MARGIN
lui   $t0, 0x4140
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
.word 0x46000834              # c.OLT.s f1,f0 : L < L_MIN ?
nop
bc1f  c0lok
nop
mov.s $f1, $f0                # L = L_MIN
c0lok:
lui   $t0, 0x42a4
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
.word 0x46010034              # c.OLT.s f0,f1 : L_MAX < L ? (fs=f0,ft=f1)
nop
bc1f  c0hik
nop
mov.s $f1, $f0                # L = L_MAX
c0hik:
b     c0haveL
nop
c0clear:
lui   $t0, 0x42a4
ori   $t0, $t0, 0x0000
mtc1  $t0, $f1
nop
nop
c0haveL:
mul.s $f2, $f1, $f20           # d = L*CH
mul.s $f3, $f1, $f21           # h = L*SH
lui   $t0, 0x42a0
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
sub.s $f4, $f2, $f0
mul.s $f4, $f4, $f4           # (d-BASE_DIST)^2
lui   $t0, 0x4188
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
sub.s $f0, $f3, $f0
mul.s $f0, $f0, $f0           # (h-REST_H)^2
add.s $f4, $f4, $f0            # score
lwc1  $f0, 0x70($sp)
.word 0x46002034              # c.OLT.s f4,f0 : score < best ?
nop
bc1f  c0next
nop
swc1  $f4, 0x70($sp)           # best_score
swc1  $f2, 0x74($sp)           # best_d
swc1  $f3, 0x78($sp)           # best_h
c0next:
# ===== candidate 1: pitch -4.0 deg  CH=0.9976 SH=-0.0698 =====
lui   $t0, 0x3f7f
ori   $t0, $t0, 0x605c
mtc1  $t0, $f20
nop
lui   $t0, 0xbd8e
ori   $t0, $t0, 0xdc7b
mtc1  $t0, $f21
nop
lwc1  $f0, 0x68($sp)
mul.s $f0, $f0, $f20
lwc1  $f1, 0x20($sp)
add.s $f0, $f0, $f1
swc1  $f0, 0x30($sp)           # rayTo.x
lwc1  $f0, 0x6c($sp)
mul.s $f0, $f0, $f20
lwc1  $f1, 0x28($sp)
add.s $f0, $f0, $f1
swc1  $f0, 0x38($sp)           # rayTo.z
lui   $t0, 0x42a4
ori   $t0, $t0, 0x0000
mtc1  $t0, $f7
nop
mul.s $f0, $f7, $f21           # L_MAX*SH
lwc1  $f1, 0x24($sp)
add.s $f0, $f0, $f1
swc1  $f0, 0x34($sp)           # rayTo.y
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addiu $a2, $sp, 0x20
addiu $a3, $sp, 0x30
jal   0x149d50
nop
lw    $v1, 0x58($sp)
lui   $t0, 0x3f7f
ori   $t0, $t0, 0x605c
mtc1  $t0, $f20
nop
lui   $t0, 0xbd8e
ori   $t0, $t0, 0xdc7b
mtc1  $t0, $f21
nop
bltz  $v0, c1clear
nop
sll   $t1, $v0, 6
sll   $t2, $v0, 4
addu  $t1, $t1, $t2            # v0*0x50
addu  $t1, $s5, $t1            # hit poly base
lwc1  $f8, 0x30($t1)          # N.x
lwc1  $f9, 0x34($t1)          # N.y
lwc1  $f10, 0x38($t1)         # N.z
lwc1  $f11, 0x30($sp)
lwc1  $f12, 0x20($sp)
sub.s $f11, $f11, $f12        # rayDir.x
mul.s $f8, $f8, $f11
lwc1  $f11, 0x34($sp)
lwc1  $f12, 0x24($sp)
sub.s $f11, $f11, $f12        # rayDir.y
mul.s $f9, $f9, $f11
add.s $f8, $f8, $f9
lwc1  $f11, 0x38($sp)
lwc1  $f12, 0x28($sp)
sub.s $f11, $f11, $f12        # rayDir.z
mul.s $f10, $f10, $f11
add.s $f8, $f8, $f10          # dot = N . rayDir
mtc1  $zero, $f9
nop
.word 0x46094034              # c.OLT.s f8,f9 : dot < 0 ? (front-facing)
nop
bc1f  c1clear                # dot >= 0 -> backface -> treat boom as clear
nop
lwc1  $f0, 0x60($sp)
mul.s $f0, $f0, $f20           # dir.x = sin*CH
lwc1  $f1, 0x40($sp)
lwc1  $f2, 0x20($sp)
sub.s $f1, $f1, $f2
mul.s $f1, $f1, $f0            # (hit.x-ref.x)*dir.x
lwc1  $f0, 0x64($sp)
mul.s $f0, $f0, $f20           # dir.z = cos*CH
lwc1  $f2, 0x48($sp)
lwc1  $f3, 0x28($sp)
sub.s $f2, $f2, $f3
mul.s $f2, $f2, $f0            # (hit.z-ref.z)*dir.z
add.s $f1, $f1, $f2
lwc1  $f2, 0x44($sp)
lwc1  $f3, 0x24($sp)
sub.s $f2, $f2, $f3
mul.s $f2, $f2, $f21           # (hit.y-ref.y)*dir.y (dir.y=SH)
add.s $f1, $f1, $f2            # L_hit
lui   $t0, 0x4100
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
sub.s $f1, $f1, $f0            # L = L_hit - MARGIN
lui   $t0, 0x4140
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
.word 0x46000834              # c.OLT.s f1,f0 : L < L_MIN ?
nop
bc1f  c1lok
nop
mov.s $f1, $f0                # L = L_MIN
c1lok:
lui   $t0, 0x42a4
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
.word 0x46010034              # c.OLT.s f0,f1 : L_MAX < L ? (fs=f0,ft=f1)
nop
bc1f  c1hik
nop
mov.s $f1, $f0                # L = L_MAX
c1hik:
b     c1haveL
nop
c1clear:
lui   $t0, 0x42a4
ori   $t0, $t0, 0x0000
mtc1  $t0, $f1
nop
nop
c1haveL:
mul.s $f2, $f1, $f20           # d = L*CH
mul.s $f3, $f1, $f21           # h = L*SH
lui   $t0, 0x42a0
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
sub.s $f4, $f2, $f0
mul.s $f4, $f4, $f4           # (d-BASE_DIST)^2
lui   $t0, 0x4188
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
sub.s $f0, $f3, $f0
mul.s $f0, $f0, $f0           # (h-REST_H)^2
add.s $f4, $f4, $f0            # score
lwc1  $f0, 0x70($sp)
.word 0x46002034              # c.OLT.s f4,f0 : score < best ?
nop
bc1f  c1next
nop
swc1  $f4, 0x70($sp)           # best_score
swc1  $f2, 0x74($sp)           # best_d
swc1  $f3, 0x78($sp)           # best_h
c1next:
# ===== candidate 2: pitch -20.0 deg  CH=0.9397 SH=-0.3420 =====
lui   $t0, 0x3f70
ori   $t0, $t0, 0x8fb2
mtc1  $t0, $f20
nop
lui   $t0, 0xbeaf
ori   $t0, $t0, 0x1d44
mtc1  $t0, $f21
nop
lwc1  $f0, 0x68($sp)
mul.s $f0, $f0, $f20
lwc1  $f1, 0x20($sp)
add.s $f0, $f0, $f1
swc1  $f0, 0x30($sp)           # rayTo.x
lwc1  $f0, 0x6c($sp)
mul.s $f0, $f0, $f20
lwc1  $f1, 0x28($sp)
add.s $f0, $f0, $f1
swc1  $f0, 0x38($sp)           # rayTo.z
lui   $t0, 0x42a4
ori   $t0, $t0, 0x0000
mtc1  $t0, $f7
nop
mul.s $f0, $f7, $f21           # L_MAX*SH
lwc1  $f1, 0x24($sp)
add.s $f0, $f0, $f1
swc1  $f0, 0x34($sp)           # rayTo.y
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addiu $a2, $sp, 0x20
addiu $a3, $sp, 0x30
jal   0x149d50
nop
lw    $v1, 0x58($sp)
lui   $t0, 0x3f70
ori   $t0, $t0, 0x8fb2
mtc1  $t0, $f20
nop
lui   $t0, 0xbeaf
ori   $t0, $t0, 0x1d44
mtc1  $t0, $f21
nop
bltz  $v0, c2clear
nop
sll   $t1, $v0, 6
sll   $t2, $v0, 4
addu  $t1, $t1, $t2            # v0*0x50
addu  $t1, $s5, $t1            # hit poly base
lwc1  $f8, 0x30($t1)          # N.x
lwc1  $f9, 0x34($t1)          # N.y
lwc1  $f10, 0x38($t1)         # N.z
lwc1  $f11, 0x30($sp)
lwc1  $f12, 0x20($sp)
sub.s $f11, $f11, $f12        # rayDir.x
mul.s $f8, $f8, $f11
lwc1  $f11, 0x34($sp)
lwc1  $f12, 0x24($sp)
sub.s $f11, $f11, $f12        # rayDir.y
mul.s $f9, $f9, $f11
add.s $f8, $f8, $f9
lwc1  $f11, 0x38($sp)
lwc1  $f12, 0x28($sp)
sub.s $f11, $f11, $f12        # rayDir.z
mul.s $f10, $f10, $f11
add.s $f8, $f8, $f10          # dot = N . rayDir
mtc1  $zero, $f9
nop
.word 0x46094034              # c.OLT.s f8,f9 : dot < 0 ? (front-facing)
nop
bc1f  c2clear                # dot >= 0 -> backface -> treat boom as clear
nop
lwc1  $f0, 0x60($sp)
mul.s $f0, $f0, $f20           # dir.x = sin*CH
lwc1  $f1, 0x40($sp)
lwc1  $f2, 0x20($sp)
sub.s $f1, $f1, $f2
mul.s $f1, $f1, $f0            # (hit.x-ref.x)*dir.x
lwc1  $f0, 0x64($sp)
mul.s $f0, $f0, $f20           # dir.z = cos*CH
lwc1  $f2, 0x48($sp)
lwc1  $f3, 0x28($sp)
sub.s $f2, $f2, $f3
mul.s $f2, $f2, $f0            # (hit.z-ref.z)*dir.z
add.s $f1, $f1, $f2
lwc1  $f2, 0x44($sp)
lwc1  $f3, 0x24($sp)
sub.s $f2, $f2, $f3
mul.s $f2, $f2, $f21           # (hit.y-ref.y)*dir.y (dir.y=SH)
add.s $f1, $f1, $f2            # L_hit
lui   $t0, 0x4100
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
sub.s $f1, $f1, $f0            # L = L_hit - MARGIN
lui   $t0, 0x4140
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
.word 0x46000834              # c.OLT.s f1,f0 : L < L_MIN ?
nop
bc1f  c2lok
nop
mov.s $f1, $f0                # L = L_MIN
c2lok:
lui   $t0, 0x42a4
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
.word 0x46010034              # c.OLT.s f0,f1 : L_MAX < L ? (fs=f0,ft=f1)
nop
bc1f  c2hik
nop
mov.s $f1, $f0                # L = L_MAX
c2hik:
b     c2haveL
nop
c2clear:
lui   $t0, 0x42a4
ori   $t0, $t0, 0x0000
mtc1  $t0, $f1
nop
nop
c2haveL:
mul.s $f2, $f1, $f20           # d = L*CH
mul.s $f3, $f1, $f21           # h = L*SH
lui   $t0, 0x42a0
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
sub.s $f4, $f2, $f0
mul.s $f4, $f4, $f4           # (d-BASE_DIST)^2
lui   $t0, 0x4188
ori   $t0, $t0, 0x0000
mtc1  $t0, $f0
nop
sub.s $f0, $f3, $f0
mul.s $f0, $f0, $f0           # (h-REST_H)^2
add.s $f4, $f4, $f0            # score
lwc1  $f0, 0x70($sp)
.word 0x46002034              # c.OLT.s f4,f0 : score < best ?
nop
bc1f  c2next
nop
swc1  $f4, 0x70($sp)           # best_score
swc1  $f2, 0x74($sp)           # best_d
swc1  $f3, 0x78($sp)           # best_h
c2next:
lui   $t0, 0x14
ori   $t0, $t0, 0xc200
lwc1  $f5, 0x74($sp)
swc1  $f5, 0x0($t0)            # DBG asm best_d
lwc1  $f5, 0x78($sp)
swc1  $f5, 0x4($t0)            # DBG asm best_h
# ===== ease dist(+0x2D0)->best_d, height(+0x2D4)->best_h =====
lwc1  $f6, 0x2d0($v1)
lwc1  $f7, 0x74($sp)
sub.s $f7, $f7, $f6
lui   $t0, 0x3e99
ori   $t0, $t0, 0x999a
mtc1  $t0, $f3
nop
mul.s $f7, $f7, $f3
add.s $f6, $f6, $f7
swc1  $f6, 0x2d0($v1)
lwc1  $f6, 0x2d4($v1)
lwc1  $f7, 0x78($sp)
sub.s $f7, $f7, $f6
lui   $t0, 0x3e4c
ori   $t0, $t0, 0xcccd
mtc1  $t0, $f3
nop
mul.s $f7, $f7, $f3
add.s $f6, $f6, $f7
swc1  $f6, 0x2d4($v1)
done:
lwc1  $f20, 0x7c($sp)
lwc1  $f21, 0x5c($sp)
lw    $v0, 0x54($sp)
lw    $ra, 0x50($sp)
jr    $ra
addiu $sp, $sp, 0x80
