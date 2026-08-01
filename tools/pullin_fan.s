# INCREMENT 1: FAN-CAST camera @0x14b838. Vertical fan of booms ref(head)->(ex, ref.y+H_i, ez) at BASE_DIST.
# Cascade candidate heights [17,11,5,-1] from rest DOWN; the HIGHEST clear one wins at full distance (BASE_DIST).
# If NONE clear (a wall blocks every height) -> rest height, dist = rest-boom hit - MARGIN (pull in).
# Fan gives BOTH height and distance. Ease dist(+0x2D0) & height(+0x2D4). Constants HARDCODED (no C# injection).
# NO stick / clip-guards / slope-rise yet (Increment 1). Frame -0x70. CheckHit 0x149d50 vs s5/s8. bltz v0 = clear (no hit).
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
swc1  $f0, 0x20($sp)          # ref.x  (rayFrom)
lwc1  $f0, 0x2c4($v1)
swc1  $f0, 0x24($sp)          # ref.y
lwc1  $f0, 0x2c8($v1)
swc1  $f0, 0x28($sp)          # ref.z
sw    $zero, 0x2c($sp)
sw    $zero, 0x3c($sp)
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
# ex,ez = ref + BASE_DIST*dir  -> rayTo.x(0x30), rayTo.z(0x38) (shared by every fan boom)
lui   $t0, 0x42a0
mtc1  $t0, $f8                # BASE_DIST = 80
nop
lwc1  $f0, 0x60($sp)
mul.s $f0, $f0, $f8
lwc1  $f1, 0x20($sp)
add.s $f0, $f1, $f0
swc1  $f0, 0x30($sp)          # ex
lwc1  $f0, 0x64($sp)
mul.s $f0, $f0, $f8
lwc1  $f1, 0x28($sp)
add.s $f0, $f1, $f0
swc1  $f0, 0x38($sp)          # ez
# CheckHit stack args (shared): hitOut ptr @sp+0x40, mode=1 (nearest), skip=0
addiu $t0, $sp, 0x40
sw    $t0, 0x10($sp)
addiu $t1, $zero, 1
sw    $t1, 0x14($sp)
sw    $zero, 0x18($sp)
# ===== candidate REST_H = 17 =====
lwc1  $f1, 0x24($sp)          # ref.y
lui   $t0, 0x4188            # 17.0
mtc1  $t0, $f2
nop
add.s $f0, $f1, $f2
swc1  $f0, 0x34($sp)          # rayTo.y = ref.y + 17
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addiu $a2, $sp, 0x20
addiu $a3, $sp, 0x30
jal   0x149d50
nop
lw    $v1, 0x58($sp)
bltz  $v0, fc0                # clear -> use 17 at full distance
nop
# rest boom hit: stash its horizontal distance for the wall fallback
lwc1  $f0, 0x40($sp)
lwc1  $f1, 0x20($sp)
sub.s $f0, $f0, $f1
mul.s $f0, $f0, $f0
lwc1  $f2, 0x48($sp)
lwc1  $f3, 0x28($sp)
sub.s $f2, $f2, $f3
mul.s $f2, $f2, $f2
add.s $f0, $f0, $f2
sqrt.s $f0, $f0
swc1  $f0, 0x6c($sp)          # rest hit horizontal distance
# ===== candidate 11 =====
lwc1  $f1, 0x24($sp)
lui   $t0, 0x4130            # 11.0
mtc1  $t0, $f2
nop
add.s $f0, $f1, $f2
swc1  $f0, 0x34($sp)
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addiu $a2, $sp, 0x20
addiu $a3, $sp, 0x30
jal   0x149d50
nop
lw    $v1, 0x58($sp)
bltz  $v0, fc1
nop
# ===== candidate 5 =====
lwc1  $f1, 0x24($sp)
lui   $t0, 0x40a0            # 5.0
mtc1  $t0, $f2
nop
add.s $f0, $f1, $f2
swc1  $f0, 0x34($sp)
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addiu $a2, $sp, 0x20
addiu $a3, $sp, 0x30
jal   0x149d50
nop
lw    $v1, 0x58($sp)
bltz  $v0, fc2
nop
# ===== candidate -1 =====
lwc1  $f1, 0x24($sp)
lui   $t0, 0xbf80            # -1.0
mtc1  $t0, $f2
nop
add.s $f0, $f1, $f2
swc1  $f0, 0x34($sp)
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addiu $a2, $sp, 0x20
addiu $a3, $sp, 0x30
jal   0x149d50
nop
lw    $v1, 0x58($sp)
bltz  $v0, fc3
nop
# ===== none clear (wall) -> H_win = 17, dist = rest hit - MARGIN =====
lui   $t0, 0x4188
mtc1  $t0, $f5                # H_win = 17
lwc1  $f4, 0x6c($sp)          # rest hit horizontal
lui   $t0, 0x4100
mtc1  $t0, $f3                # MARGIN = 8
nop
sub.s $f4, $f4, $f3           # dist = rest_hit - MARGIN
b     fdone
nop
fc0:
lui   $t0, 0x4188
mtc1  $t0, $f5                # H_win = 17
lui   $t0, 0x42a0
mtc1  $t0, $f4                # dist = BASE_DIST
b     fdone
nop
fc1:
lui   $t0, 0x4130
mtc1  $t0, $f5                # 11
lui   $t0, 0x42a0
mtc1  $t0, $f4
b     fdone
nop
fc2:
lui   $t0, 0x40a0
mtc1  $t0, $f5                # 5
lui   $t0, 0x42a0
mtc1  $t0, $f4
b     fdone
nop
fc3:
lui   $t0, 0xbf80
mtc1  $t0, $f5                # -1
lui   $t0, 0x42a0
mtc1  $t0, $f4
fdone:
# f5 = target height, f4 = target dist. Ease +0x2D0 (dist) and +0x2D4 (height).
lwc1  $f6, 0x2d0($v1)         # current dist
sub.s $f7, $f4, $f6
lui   $t0, 0x3e99            # DIST_EASE 0.3
ori   $t0, $t0, 0x999a
mtc1  $t0, $f3
nop
mul.s $f7, $f7, $f3
add.s $f6, $f6, $f7
swc1  $f6, 0x2d0($v1)
lwc1  $f6, 0x2d4($v1)         # current height
sub.s $f7, $f5, $f6
lui   $t0, 0x3e4c            # HEIGHT_EASE 0.2
ori   $t0, $t0, 0xcccd
mtc1  $t0, $f3
nop
mul.s $f7, $f7, $f3
add.s $f6, $f6, $f7
swc1  $f6, 0x2d4($v1)
done:
lw    $v0, 0x54($sp)
lw    $ra, 0x50($sp)
jr    $ra
addiu $sp, $sp, 0x70
