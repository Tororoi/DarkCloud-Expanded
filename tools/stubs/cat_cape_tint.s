# cat_cape_tint.s — the Super Steve cape gets its OWN colour, separate from the cat's.
#
# HOW A CLOTH IS COLOURED. Draw__10CCharacter (0x139560) saves the global ambient (MGGetAmbient), ADDS the character's own tint
# (+0xCE0, what the mod writes per weapon) to it, sets it, draws the character's meshes AND THEN WALKS ITS CLOTH LIST (+0xC74)
# calling Draw__6CCloth inside that same window, and only afterwards restores the ambient. So a cloth is lit by whatever ambient
# is standing when it draws — the character's tint, never its own. That is why writing the cape's MDT_MATERIAL colour rows did
# nothing: the colour comes from the ambient, not from the material.
#
# THIS CAVE takes the `jal Draw__6CCloth` at 0x139694 (a0 = the CCloth about to draw, the loop's own load). For the ONE cloth whose
# pointer the mod publishes at Mailbox.CatCapeCloth it adds Mailbox.CatCapeTint to the ambient, draws, and puts the ambient back;
# every other cloth in the game (Toan's cape, Ungaga's poncho, the cat's own future ones) draws untouched. The mod writes the tint
# as a DELTA — it knows the cat's tint, so it can aim the sum wherever it likes without the cat's colour leaking in.
#
# Frame 0x50 (16-B aligned, as the EE ABI wants): ra @0x40, the cloth @0x44, the saved ambient @0x10, ours @0x20.
addiu $sp, $sp, -0x50
sw    $ra, 0x40($sp)
sw    $a0, 0x44($sp)
lui   $t0, 0x01FB
lw    $t1, 0x4288($t0)         # Mailbox.CatCapeCloth: the cape's CCloth (guest), 0 while there is no cape
beq   $t1, $zero, plain
nop
bne   $t1, $a0, plain          # some other character's cloth: leave it alone
nop
addiu $a0, $sp, 0x10
jal   0x0012DD30               # MGGetAmbient(&saved)
nop
lwc1  $f0, 0x0010($sp)         # ours = saved + the mod's delta
lwc1  $f1, 0x0014($sp)
lwc1  $f2, 0x0018($sp)
lwc1  $f3, 0x001C($sp)
lui   $t0, 0x01FB
lwc1  $f4, 0x428C($t0)         # Mailbox.CatCapeTint r
lwc1  $f5, 0x4290($t0)         #                     g
lwc1  $f6, 0x4294($t0)         #                     b
add.s $f0, $f0, $f4
add.s $f1, $f1, $f5
add.s $f2, $f2, $f6
swc1  $f0, 0x0020($sp)
swc1  $f1, 0x0024($sp)
swc1  $f2, 0x0028($sp)
swc1  $f3, 0x002C($sp)         # the fourth component rides along untouched
addiu $a0, $sp, 0x20
jal   0x0012DD00               # MGSetAmbient(&ours)
nop
lw    $a0, 0x0044($sp)
jal   0x0013B640               # Draw__6CCloth — the call this cave replaced
nop
addiu $a0, $sp, 0x10
jal   0x0012DD00               # MGSetAmbient(&saved) — the character's own tint is back for whatever draws next
nop
b     out
nop
plain:
lw    $a0, 0x0044($sp)
jal   0x0013B640
nop
out:
lw    $ra, 0x0040($sp)
jr    $ra
addiu $sp, $sp, 0x50
