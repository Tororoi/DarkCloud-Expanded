# mirage_haze_draw.s — the Mirage clone's heat shimmer, drawn by the game's own raster routine at the clone itself.
# Assembled at 0x01FB3C40 (ElfCave.MirageHazeDraw). Replaces the dungeon draw loop's `jal DrawRaster__11CDungeonMap`
# (dun 0x1DAEBCC, hooked by DunPatches): performs that call untouched, then — if the mod says the haze is on — draws one
# more raster at the clone's root.
#
# DrawRaster__9CFireOmni (0x162310) is the whole shimmer and knows nothing of tiles: it projects the position at its
# object's +0x20..+0x28 (adding 3.0 to the height itself), fetches "blender" (the framebuffer, always registered) and
# "alpha01" (the fire pack's distortion mask) by name, and blends. Every gate that made the torch hijack unreliable —
# the ±4-tile camera window, the 240-unit distance test, the per-tile rotation — lives in the map's WALKER, which is
# exactly what the walker does per emitter: write a world position into the CFireOmni the map embeds at +0x50 (so the
# position lands at map+0x70..+0x7C) and call DrawRaster on it. This cave does the same once, with the clone's root
# CFrame's posed world translation (+0x180, as of this frame's draw). The wave phase at map+0x54 is stepped every frame
# by the overlay's setTexScroll, fires or not. The PNACH's size / amplitude / wave-speed patches sit inside DrawRaster
# and RasterStep, so they apply here unchanged.
#
# Mailbox (0x01F10000 + …): +0x94 MirageHazeOn int   +0x98 MirageHazeNode uint, the clone's root CFrame (guest)
#                           +0x9C MirageHazeLift float, added to the root's height (negative lowers it)
# A dungeon with no fire pack has no "alpha01": GetTexture returns 0 and nothing is drawn.
# Not a leaf (it calls DrawRaster), so it keeps a frame; a0 (the map) is saved across the original call.

    addiu $sp, $sp, -0x20
    sw    $ra, 0x0010($sp)
    sw    $a0, 0x0014($sp)         # the CDungeonMap
    jal   0x001C4610               # DrawRaster__11CDungeonMap(map, camera), as before
    nop
    lui   $t0, 0x01F1
    lw    $t5, 0x0094($t0)         # MirageHazeOn
    beq   $t5, $zero, ret
    nop
    lw    $t7, 0x0098($t0)         # MirageHazeNode
    beq   $t7, $zero, ret
    nop
    lui   $a0, 0x01C7
    ori   $a0, $a0, 0x5870         # the texture manager
    lui   $a1, 0x0029
    ori   $a1, $a1, 0xA0E8         # "alpha01" — the ELF's own string, the raster's distortion mask
    jal   0x001312D0               # GetTexture(manager, name, -1)
    addiu $a2, $zero, -1           # (delay slot)
    beq   $v0, $zero, ret          # no fire pack in this dungeon: nothing to blend with
    nop
    lui   $t0, 0x01F1
    lw    $t7, 0x0098($t0)         # the root again (the call kept no $t register)
    lwc1  $f2, 0x0180($t7)         # its posed world position
    lwc1  $f4, 0x0184($t7)
    lwc1  $f6, 0x0188($t7)
    lwc1  $f8, 0x009C($t0)         # MirageHazeLift
    add.s $f4, $f4, $f8
    lw    $t6, 0x0014($sp)         # the map
    swc1  $f2, 0x0070($t6)         # → the embedded CFireOmni's position (+0x50 + 0x20)
    swc1  $f4, 0x0074($t6)
    swc1  $f6, 0x0078($t6)
    lui   $t8, 0x3F80
    sw    $t8, 0x007C($t6)         # w = 1.0
    jal   0x00162310               # DrawRaster__9CFireOmni(map + 0x50)
    addiu $a0, $t6, 0x50           # (delay slot)
ret:
    lw    $ra, 0x0010($sp)
    jr    $ra
    addiu $sp, $sp, 0x20           # (delay slot)
