# supersteve_icon_copy.s — keeps the sphere weapon's icon in a spare cell of the HUD sheet, the way the game keeps the
# equipped weapon's there. Assembled at 0x01FB3DC0 (ElfCave.SuperSteveIconCopy). Replaces both calls to
# DngActiveWeaponTextureCopy — the overlay's per-frame one (dun 0x1DAE608, hooked by DunPatches) and the one on leaving
# the menu (main 0x226560, where a sphere is attached): performs the call untouched, then — if the `wepicon` sheet is
# registered right now — moves the CURRENT sphere's cell into `itempack` with the game's own routine. It does not wait
# for the mod: the cell is kept current from the moment the game first copies (dungeon entry), so the icon can show the
# instant the mod asks for it.
#
# The icon sheets are TRANSIENT: LoadActiveItemIcon registers `wepicon` and `itemicon` at floor start (and the menu
# registers its own), the game's copies land while they exist, and only the cells moved into `itempack` — resident all
# floor — survive. A copy opportunity is rare and never lines up with the mod's tick, so the cave reads the sphere
# ITSELF from the equipped record each time, the way cat_palette.s reads the element: no mailbox round trip in the path.
# setItemToReserved(srcName, u, v, dstName, dstX, dstY) is a GS VRAM-to-VRAM move of 32×32 texels; the sheets share
# one palette, so the indices land exact.
#
# INPUTS — static: 0x01CD954C DngStatusData (+4 the current character; +0x4340+ch its bag slot; the record at
#   +ch·0xAA8 + 0x450C + slot·0xF8: weapon id at +0, six 0x20 attachments from +0x28, a SynthSphere = 0x5A with its
#   source weapon id at +2); 0x0027DE50 ComItemInfo (8 B from item 81; +4 the icon index → cell (i&7)·32, (i>>3)·32).
# The spare cell is (64, 32) — blank in the art and outside every game copy target — a constant here and in the draw
# cave, so nothing in this path depends on the mod having run: a save loaded straight into a dungeon gets its copy at
# the entry-time call. The only mailbox words touched are the diagnostic counters.

    addiu $sp, $sp, -0x20
    sw    $ra, 0x0010($sp)
    jal   0x0022A6B0               # DngActiveWeaponTextureCopy(), as before
    nop
    lui   $t0, 0x01F1
    lw    $t9, 0x00C8($t0)         # SsIconDiagCopyCalls++
    addiu $t9, $t9, 1
    sw    $t9, 0x00C8($t0)
    lui   $a0, 0x01C7
    ori   $a0, $a0, 0x5870         # the texture manager
    lui   $a1, 0x002A
    ori   $a1, $a1, 0x2190         # "wepicon" — the ELF's own string
    jal   0x001312D0               # GetTexture(manager, name, -1)
    addiu $a2, $zero, -1           # (delay slot)
    beq   $v0, $zero, ret          # the sheet is not registered right now
    nop
    lui   $t0, 0x01F1
    lw    $t9, 0x00CC($t0)         # SsIconDiagSheetSeen++
    addiu $t9, $t9, 1
    sw    $t9, 0x00CC($t0)
    lui   $t1, 0x01CD
    ori   $t1, $t1, 0x954C         # DngStatusData
    lbu   $t2, 0x0004($t1)         # the current character
    addu  $t3, $t1, $t2
    lbu   $t3, 0x4340($t3)         # its equipped bag slot
    sltiu $t4, $t3, 10
    beq   $t4, $zero, ret          # nothing equipped
    nop
    sll   $t4, $t2, 11             # ch * 0xAA8 = ch * (2048 + 512 + 128 + 32 + 8)
    sll   $t6, $t2, 9
    addu  $t4, $t4, $t6
    sll   $t6, $t2, 7
    addu  $t4, $t4, $t6
    sll   $t6, $t2, 5
    addu  $t4, $t4, $t6
    sll   $t6, $t2, 3
    addu  $t4, $t4, $t6
    addu  $t4, $t1, $t4
    sll   $t6, $t3, 8              # slot * 0xF8 = slot * (256 - 8)
    sll   $t7, $t3, 3
    subu  $t6, $t6, $t7
    addu  $t4, $t4, $t6
    addiu $t4, $t4, 0x450C         # the equipped weapon's record
    lhu   $t6, 0x0000($t4)
    addiu $t7, $zero, 312          # Super Steve
    bne   $t6, $t7, ret
    nop
    addiu $t5, $t4, 0x28           # attachment 0
    addiu $t8, $zero, 6
sphere:
    lhu   $t6, 0x0000($t5)
    addiu $t7, $zero, 0x5A         # a SynthSphere
    beq   $t6, $t7, found
    nop
    addiu $t8, $t8, -1
    bne   $t8, $zero, sphere
    addiu $t5, $t5, 0x20           # (delay slot) the next attachment
    b     ret                      # none attached
    nop
found:
    lhu   $t6, 0x0002($t5)         # its source weapon
    addiu $t6, $t6, -81            # ComItemInfo is indexed from item 81
    bltz  $t6, ret
    nop
    sltiu $t7, $t6, 296
    beq   $t7, $zero, ret
    nop
    sll   $t6, $t6, 3
    lui   $t7, 0x0027
    ori   $t7, $t7, 0xDE50
    addu  $t7, $t7, $t6
    lhu   $t6, 0x0004($t7)         # the icon index
    andi  $a1, $t6, 7
    sll   $a1, $a1, 5              # u = (i & 7) * 32
    srl   $a2, $t6, 3
    sll   $a2, $a2, 5              # v = (i >> 3) * 32
    lui   $a0, 0x002A
    ori   $a0, $a0, 0x2190         # "wepicon"
    lui   $a3, 0x0029
    ori   $a3, $a3, 0xF040         # "itempack"
    lui   $t0, 0x01F1
    lw    $t9, 0x00D0($t0)         # SsIconDiagCopies++
    addiu $t9, $t9, 1
    sw    $t9, 0x00D0($t0)
    addiu $t0, $zero, 64           # dstX (the fifth argument): the spare cell (64, 32)
    addiu $t1, $zero, 32           # dstY (the sixth)
    jal   0x001B1EF0               # setItemToReserved(src, u, v, dst, dstX, dstY)
    nop
ret:
    lw    $ra, 0x0010($sp)
    jr    $ra
    addiu $sp, $sp, 0x20           # (delay slot)
