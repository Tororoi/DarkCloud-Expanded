# SWEPT-WALL STOP stub. Replaces `jal AddAngle` at the two right-stick rotate sites (0x16B884 / 0x16B8BC) in
# EdMoveChara. Entered with a0 = camera (CCameraFollow), f12 = rotation delta. Applies the rotation via the real
# AddAngle, then if the eye at the NEW target angle (+0x2D8) would sweep through a _c wall from the CURRENT eye
# (+0x2DC) it UNDOES the rotation — so the stick can't rotate the camera through walls. TWO-SIDED (no backface cull:
# the wall blocks regardless of which side the player is on). _c buffer/count stashed by the pull-in @ 0x14C100/4.
addiu $sp, $sp, -0x80
sw    $ra, 0x50($sp)
sw    $a0, 0x54($sp)          # camera
lwc1  $f0, 0x2d8($a0)         # old target angle (pre-rotation)
swc1  $f0, 0x5c($sp)
jal   0x124b50                # AddAngle(this=a0, f12) — apply the stick rotation (with the game's angle wrap)
nop
lw    $a0, 0x54($sp)
# ===== cur_eye = ref + dist*(sin,_,cos)(angS +0x2DC) + (0,height,0)  -> sp+0x30 =====
lwc1  $f12, 0x2dc($a0)
jal   0x11d8a0                # sin(angS)
nop
swc1  $f0, 0x60($sp)
lw    $a0, 0x54($sp)
lwc1  $f12, 0x2dc($a0)
jal   0x11d6b0                # cos(angS)
nop
swc1  $f0, 0x64($sp)
lw    $a0, 0x54($sp)
lwc1  $f1, 0x2d0($a0)         # dist
lwc1  $f2, 0x60($sp)
mul.s $f2, $f2, $f1
lwc1  $f3, 0x2c0($a0)
add.s $f2, $f3, $f2
swc1  $f2, 0x30($sp)          # cur.x
lwc1  $f2, 0x2c4($a0)
lwc1  $f3, 0x2d4($a0)
add.s $f2, $f2, $f3
swc1  $f2, 0x34($sp)          # cur.y = ref.y + height
lwc1  $f2, 0x64($sp)
mul.s $f2, $f2, $f1
lwc1  $f3, 0x2c8($a0)
add.s $f2, $f3, $f2
swc1  $f2, 0x38($sp)          # cur.z
sw    $zero, 0x3c($sp)
# ===== tgt_eye at angT (+0x2D8) -> sp+0x40 =====
lwc1  $f12, 0x2d8($a0)
jal   0x11d8a0                # sin(angT)
nop
swc1  $f0, 0x60($sp)
lw    $a0, 0x54($sp)
lwc1  $f12, 0x2d8($a0)
jal   0x11d6b0                # cos(angT)
nop
swc1  $f0, 0x64($sp)
lw    $a0, 0x54($sp)
lwc1  $f1, 0x2d0($a0)
lwc1  $f2, 0x60($sp)
mul.s $f2, $f2, $f1
lwc1  $f3, 0x2c0($a0)
add.s $f2, $f3, $f2
swc1  $f2, 0x40($sp)          # tgt.x
lwc1  $f2, 0x2c4($a0)
lwc1  $f3, 0x2d4($a0)
add.s $f2, $f2, $f3
swc1  $f2, 0x44($sp)          # tgt.y
lwc1  $f2, 0x64($sp)
mul.s $f2, $f2, $f1
lwc1  $f3, 0x2c8($a0)
add.s $f2, $f3, $f2
swc1  $f2, 0x48($sp)          # tgt.z
sw    $zero, 0x4c($sp)
# ===== CheckHit(buffer, count, cur@0x30, tgt@0x40, hitOut@0x70, mode=1, skip=0) — TWO-SIDED =====
lui   $t0, 0x0014
ori   $t0, $t0, 0xc100
lw    $a0, 0x0($t0)           # _c buffer (stashed by the pull-in this frame)
lw    $a1, 0x4($t0)           # count
addiu $a2, $sp, 0x30
addiu $a3, $sp, 0x40
addiu $t0, $sp, 0x70
sw    $t0, 0x10($sp)
addiu $t1, $zero, 1
sw    $t1, 0x14($sp)
sw    $zero, 0x18($sp)
jal   0x149d50
nop
bltz  $v0, sdone             # no wall crossed -> keep the rotation
nop
# BACKFACE GATE: only stop if this is a wall the pull-in can't handle (backface: N·(tgt_eye − ref) >= 0). Front-
# facing walls are handled by the pull-in's dist slide, so leave those alone — stopping them killed the normal slide.
lui   $t0, 0x0014
ori   $t0, $t0, 0xc100
lw    $t1, 0x0($t0)           # _c buffer base
sll   $t2, $v0, 6
sll   $t3, $v0, 4
addu  $t2, $t2, $t3
addu  $t1, $t1, $t2           # hit poly = buffer + v0*0x50
lw    $a0, 0x54($sp)          # camera
lwc1  $f4, 0x30($t1)          # N.x
lwc1  $f5, 0x34($t1)          # N.y
lwc1  $f6, 0x38($t1)          # N.z
lwc1  $f7, 0x40($sp)
lwc1  $f8, 0x2c0($a0)
sub.s $f7, $f7, $f8           # tgt.x − ref.x
mul.s $f4, $f4, $f7
lwc1  $f7, 0x44($sp)
lwc1  $f8, 0x2c4($a0)
sub.s $f7, $f7, $f8
mul.s $f5, $f5, $f7
add.s $f4, $f4, $f5
lwc1  $f7, 0x48($sp)
lwc1  $f8, 0x2c8($a0)
sub.s $f7, $f7, $f8
mul.s $f6, $f6, $f7
add.s $f4, $f4, $f6           # dot = N·(tgt_eye − ref)
mtc1  $zero, $f5
nop
.word 0x46052034             # c.OLT.s f4,f5 : dot < 0 ? (front-facing)
nop
bc1t  sdone                  # front-facing -> pull-in slides it, don't stop
nop
lwc1  $f0, 0x5c($sp)          # old target angle
swc1  $f0, 0x2d8($a0)         # backface -> UNDO the rotation (stop at the wrong-side wall)
sdone:
lw    $ra, 0x50($sp)
jr    $ra
addiu $sp, $sp, 0x80
