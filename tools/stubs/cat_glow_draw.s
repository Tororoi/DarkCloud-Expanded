# cat_glow_draw.s — the Divine Beast cat's blue glow, drawn by the game's own torch routine (CFireOmni).
# Hooks BOTH of the dungeon draw loop's torch passes (dun 0x1DAEBF8 `jal DrawFire__11CDungeonMap` and 0x1DAEC10
# `jal DrawFireFreeStyle__11CDungeonMap`): each entry performs the original call untouched, then — if the mod says the
# cat is up (CatGlowOn) — draws our glow at the middle of the cat's torso with DrawFire__9CFireOmni (0x161AC0), exactly
# as the wall torches are drawn, using the glow disc the cat pack bakes (the Gallery of Time's purple torch disc re-tinted:
# `catglowp`, the ONE 8-bit disc every look now shares — the per-weapon and per-element colours are palette ROWS that
# cat_glow_palette.s paints into it, not discs of their own; the mod still names it in the mailbox,
# CatGlowName +0x258, 16 B NUL-terminated) for BOTH sprite layers, so no
# dungeon flame haze leaks in. (This cave's own first 8 bytes still spell "catglow": the ELF patcher checks them.) A CFireOmni object (0x40 B) lives in the
# cat mailbox at 0x01FB4200; its textures are bound once per charge (CatGlowReady) since the copy's texture entries are
# re-made per spawn. Runs inside the map's own draw pass, so the VIF1 packet is already open for sprites.
# Layout: +0x00 "catglow\0"   +0x08 entry A   +0x20 entry B   (DunPatches jumps to the entries by these offsets.)
# Mailbox (0x01FB4000 + …): +0x1E4 CatGlowOn int (mod)   +0x1E8 CatGlowScale float (mod; f12 of the draw)
#   +0x1EC CatGlowFlags int (mod; 1 = the glow pair, 2 = the flickering flame sprite, 3 = both)
#   +0x1F0/+0x1F4 CatGlowNodeA/B uint the two torso frames (mod, at spawn): the glow sits at the midpoint of their posed
#   world positions (world matrix +0x150, translation row +0x180)   +0x1F8 CatGlowReady int (cave; mod clears per charge)
#   +0x1FC CatGlowPull float how far toward the camera the sprite is pulled (mod; the torches use 15.0)
#   +0x240 CatGlowLift float added to the glow's height (mod; negative lowers it)
#   +0x258 CatGlowName char[16] the texture entry to bind (mod, before clearing CatGlowReady); empty = nothing drawn
#   +0x200..+0x240 the CFireOmni object.
.word 0x67746163               # +0x00 "catg"
.word 0x00776F6C               # +0x04 "low\0"
entryA:                        # +0x08 — in place of `jal DrawFire__11CDungeonMap`
addiu $sp, $sp, -0x40
sw    $ra, 0x0030($sp)
jal   0x001C40C0               # the map's torches, as before (a0 map, a1 frame, a2 camera pass straight through)
nop
b     common
nop
entryB:                        # +0x20 — in place of `jal DrawFireFreeStyle__11CDungeonMap`
addiu $sp, $sp, -0x40
sw    $ra, 0x0030($sp)
jal   0x001C3CC0
nop
common:
lui   $t0, 0x01FB
lw    $t5, 0x41E4($t0)         # CatGlowOn
beq   $t5, $zero, ret
nop
lw    $t5, 0x41F8($t0)         # CatGlowReady: textures bound this charge?
bne   $t5, $zero, ready
nop
lui   $t6, 0x01FB
ori   $t6, $t6, 0x4200         # the CFireOmni object — cleared, then the constructor's one non-zero field
sw    $zero, 0x0000($t6)
sw    $zero, 0x0004($t6)
sw    $zero, 0x0008($t6)
sw    $zero, 0x000C($t6)
sw    $zero, 0x0010($t6)
sw    $zero, 0x0014($t6)
sw    $zero, 0x0018($t6)
sw    $zero, 0x001C($t6)
sw    $zero, 0x0020($t6)
sw    $zero, 0x0024($t6)
sw    $zero, 0x0028($t6)
sw    $zero, 0x002C($t6)
sw    $zero, 0x0030($t6)
sw    $zero, 0x0034($t6)
sw    $zero, 0x0038($t6)
sw    $zero, 0x003C($t6)
lui   $t7, 0x4170
sw    $t7, 0x000C($t6)         # +0x0C = 15.0 (__ct__9CFireOmni)
lui   $a0, 0x01C7
ori   $a0, $a0, 0x5870         # the texture manager
lui   $a1, 0x01FB
ori   $a1, $a1, 0x4258         # the name the mod wrote to the mailbox (CatGlowName)
jal   0x001312D0               # GetTexture(manager, name, -1)
addiu $a2, $zero, -1           # (delay slot)
beq   $v0, $zero, ret          # not registered (the copy is down): nothing to draw
nop
sw    $v0, 0x0010($sp)
lui   $a0, 0x01FB
ori   $a0, $a0, 0x4200
lw    $a1, 0x0010($sp)
jal   0x00161AA0               # SetTexture(obj, disc, disc): both sprite layers are our disc
move  $a2, $a1                 # (delay slot)
lui   $t0, 0x01FB
addiu $t5, $zero, 1
sw    $t5, 0x41F8($t0)         # ready
ready:
lui   $t0, 0x01FB
lui   $t6, 0x01FB
ori   $t6, $t6, 0x4200
lw    $t7, 0x41F0($t0)         # torso frame A (cat_kosibone, the hips)
lw    $t8, 0x41F4($t0)         # torso frame B (cat_sebone2, the upper spine)
beq   $t7, $zero, rootglow
nop
beq   $t8, $zero, rootglow
nop
lui   $t9, 0x3F00
mtc1  $t9, $f10                # 0.5
lwc1  $f2, 0x0180($t7)         # the frames' posed world positions (as of the last draw)
lwc1  $f4, 0x0180($t8)
lwc1  $f6, 0x0184($t7)
lwc1  $f8, 0x0184($t8)
add.s $f2, $f2, $f4
add.s $f6, $f6, $f8
lwc1  $f4, 0x0188($t7)
lwc1  $f8, 0x0188($t8)
add.s $f4, $f4, $f8
mul.s $f2, $f2, $f10           # the midpoint = the middle of the torso
mul.s $f6, $f6, $f10
mul.s $f4, $f4, $f10
swc1  $f2, 0x0020($t6)
swc1  $f6, 0x0024($t6)
b     posdone
swc1  $f4, 0x0028($t6)         # (delay slot)
rootglow:                      # no frames known: the cat's root, 2 up
lui   $t7, 0x01EA
ori   $t7, $t7, 0x9900
lwc1  $f2, 0x0010($t7)
lwc1  $f6, 0x0014($t7)
lwc1  $f4, 0x0018($t7)
lui   $t9, 0x4000
mtc1  $t9, $f10
nop
nop
nop
add.s $f6, $f6, $f10
swc1  $f2, 0x0020($t6)
swc1  $f6, 0x0024($t6)
swc1  $f4, 0x0028($t6)
posdone:
lui   $t9, 0x002A
lwc1  $f8, 0x19DC($t9)         # DrawFire adds this torch lift (4.6) to the height: cancel it so the glow centres on the torso
lwc1  $f6, 0x0024($t6)
sub.s $f6, $f6, $f8
lwc1  $f8, 0x4240($t0)         # CatGlowLift: the mod's height nudge (negative = lower; it sat just above the cat)
add.s $f6, $f6, $f8
swc1  $f6, 0x0024($t6)
lwc1  $f12, 0x41E8($t0)        # CatGlowScale → f12, the draw's scale (the torches use 1.0; the flame sprite is 45 × 22.5 units at 1.0)
lwc1  $f13, 0x41FC($t0)        # CatGlowPull → f13: how far toward the camera the sprite is pulled (the torches use 15 to clear their wall)
lw    $t1, 0x41EC($t0)         # CatGlowFlags → the 8th argument
lui   $a0, 0x01FB
ori   $a0, $a0, 0x4200         # obj
addiu $a1, $zero, 1
addiu $a2, $zero, 1
lui   $a3, 0x002A
lw    $a3, 0x3498($a3)         # the dungeon camera (what the map passes its own DrawFire)
addiu $t0, $a0, 0x0020         # the 7th argument: the position vector
jal   0x00161AC0               # DrawFire__9CFireOmni(f12 scale, f13 15.0, obj, 1, 1, camera, pos, flags)
nop
ret:
lw    $ra, 0x0030($sp)
jr    $ra
addiu $sp, $sp, 0x40
