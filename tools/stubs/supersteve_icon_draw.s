# supersteve_icon_draw.s — the icon of the weapon whose SynthSphere Super Steve carries, drawn over Steve on the dungeon
# HUD. Assembled at 0x01FB3CE0 (ElfCave.SuperSteveIconDraw). Replaces the overlay's `jal topStatusInfo` (dun 0x1DB0364,
# hooked by DunPatches): performs the call untouched, then — if the mod says so — draws one 32×32 cell of the HUD sheet
# `itempack` at an absolute screen position, the way topStatusInfo draws its own sprites.
#
# The HUD's bottom-left "Steve" IS the equipped weapon's icon: topStatusInfo's last sprite draws cell (0,0) of
# `itempack` at screen (29, 388). The sphere's icon sits in a spare cell of the same sheet, put there by
# supersteve_icon_copy.s whenever the transient `wepicon` sheet is registered; `itempack` itself is resident all floor.
#
# Mailbox (0x01F10000 + …): +0xA0 SsIconOn int   +0xA4/+0xA8 SsIconX/Y int, screen position   +0xAC SsIconSize int
#                           +0xB0 SsIconDiagDraws int, bumped per draw
# Frame: 0x00 the callee's argument area, 0x10 dst rect, 0x20 src rect, 0x30 ra. a0..a2 pass straight through.

    addiu $sp, $sp, -0x40
    sw    $ra, 0x0030($sp)
    jal   0x001B04F0               # topStatusInfo(a0, a1, a2), as before
    nop
    lui   $t0, 0x01F1
    lw    $t5, 0x00A0($t0)         # SsIconOn
    beq   $t5, $zero, ret
    nop
    lui   $a0, 0x01C7
    ori   $a0, $a0, 0x5870         # the texture manager
    lui   $a1, 0x0029
    ori   $a1, $a1, 0xF040         # "itempack" — the ELF's own string
    jal   0x001312D0               # GetTexture(manager, name, -1)
    addiu $a2, $zero, -1           # (delay slot)
    beq   $v0, $zero, ret          # not registered: nothing to draw from
    nop
    move  $a1, $v0                 # the sheet
    lui   $t0, 0x01F1
    lw    $t9, 0x00B0($t0)         # SsIconDiagDraws++
    addiu $t9, $t9, 1
    sw    $t9, 0x00B0($t0)
    lw    $t3, 0x00A4($t0)         # SsIconX
    sw    $t3, 0x0010($sp)         # dst.x
    lw    $t3, 0x00A8($t0)         # SsIconY
    sw    $t3, 0x0014($sp)         # dst.y
    lw    $t3, 0x00AC($t0)         # SsIconSize
    sw    $t3, 0x0018($sp)         # dst.w
    sw    $t3, 0x001C($sp)         # dst.h
    addiu $t3, $zero, 64           # the spare cell (64, 32) supersteve_icon_copy.s keeps the icon in
    sw    $t3, 0x0020($sp)         # src.x
    addiu $t3, $zero, 32
    sw    $t3, 0x0024($sp)         # src.y
    addiu $t3, $zero, 32
    sw    $t3, 0x0028($sp)         # src.w
    sw    $t3, 0x002C($sp)         # src.h
    lui   $t1, 0x002A
    lw    $a0, 0x23C4($t1)         # Vif1Packet, the HUD's open packet
    addiu $a2, $sp, 0x0010
    addiu $a3, $sp, 0x0020
    jal   0x0015C310               # set2DSprite(packet, tex, &dst, &src, alpha)
    addiu $t0, $zero, 0x80         # (delay slot) the fifth argument: opaque
ret:
    lw    $ra, 0x0030($sp)
    jr    $ra
    addiu $sp, $sp, 0x40           # (delay slot)
