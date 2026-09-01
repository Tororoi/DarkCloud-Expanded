# Cape early-draw cave — dead CharaChange region (jal-legal). Reached by redirecting the low-tide refraction
# EARLY_STUB's `jal MGDraw` (@0x17BBD0, inside PatchWaterRedraw's patchedGate) here. On entry $a0 = the player's
# model root (the EARLY_STUB loaded it from FramePtr @0x01FAE608). We do the DISPLACED MGDraw(body) first, then
# walk the player's cloth list and Draw__6CCloth each piece — so the CAPE is drawn EARLY too (before the water/
# waterfall pass) and survives the falls' Z-write, exactly like the body. This replicates Draw__CCharacter's own
# `MGDraw(+0xbc); for i<4: if *(*(+0xc74)+i*4): Draw__6CCloth(piece)`. The char ptr comes from the mailbox
# CapeCharPtr @0x01F10044 (armed by CanalTide when it arms the body's model root). Cloth sim is already stepped
# this frame → early draw uses current verts. s0=i, s1=char/list (callee-saved; preserved across the calls).
addiu $sp, $sp, -0x20
sw    $ra, 0x1c($sp)
sw    $s0, 0x18($sp)
sw    $s1, 0x14($sp)
jal   0x0012ED80               # MGDraw($a0 = model root) — the displaced body draw
nop
lui   $s1, 0x1f1
lw    $s1, 0x44($s1)           # s1 = char (mailbox CapeCharPtr @0x01F10044)
beq   $s1, $zero, done
nop
lw    $s1, 0xc74($s1)          # s1 = cloth-list ptr (char+0xC74)
beq   $s1, $zero, done
nop
move  $s0, $zero               # i = 0
loop:
sll   $t0, $s0, 2              # i*4
addu  $t0, $s1, $t0
lw    $a0, 0x0($t0)            # a0 = cloth piece i
beq   $a0, $zero, skip
nop
jal   0x0013B640               # Draw__6CCloth(a0 = piece)
nop
skip:
addiu $s0, $s0, 1
slti  $t0, $s0, 4              # i < 4 (ClothMaxPieces)
bne   $t0, $zero, loop
nop
done:
lw    $ra, 0x1c($sp)
lw    $s0, 0x18($sp)
lw    $s1, 0x14($sp)
addiu $sp, $sp, 0x20
jr    $ra
nop
