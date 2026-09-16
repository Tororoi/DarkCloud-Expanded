# cat_palette.s — the Super Steve cape and mask take the EQUIPPED WEAPON'S ELEMENT colour, natively.
# Assembled at 0x01FB2700 (ElfCave.CatPalette). The six colours are the first six words; the code starts at +0x18,
# which is what the copy-queue cave calls once per dungeon frame.
#
# WHY NATIVE. Nothing crosses PINE per frame and the colour is right on the cat's first drawn frame. The C#
# version re-walked the texture manager BY NAME every half second — up to 195 round trips — and wrote a kilobyte
# whenever the element changed (user 2026-09-15).
#
# HOW. build_cat_pack.flat_tim2 bakes the cape texture as 32x32 pixels that are ALL palette index 0 followed by 256
# identical entries, so the whole cape is ONE palette entry — and the mask SHARES catcape (wing_bake.MASK_TEX), so
# both change together. Repaint the manager's own palette copy (CTexture entry +0x48) and the dungeon draw loop
# re-uploads the cat's texture group before it draws the slot, so the colour lands on the next frame. This is the
# same live-recolour path WeaponTextureSwap uses on Super Steve.
#
# The GLOW now gets the same treatment next door, in cat_glow_palette.s (0x01FB3480, called immediately after this
# cave): its disc is 8-bit too, so ONE `catglowp` carries all six element colours in its palette. It used to take a
# separate 32-bit disc per element — four of those exhausted the character heap and froze the weapon menu
# (2026-09-16). Tinting the sprite through DrawFire's shared colour word was tried first and did not take (2026-09-15).
#
# The palette's FIRST WORD IS THE STATE: holding the element's colour already means there is nothing to do, and a
# rebuilt entry (a script event wipes them) comes back carrying the baked red and repaints itself. The only thing
# cached is the entry pointer, in the cat mailbox at +0x29C — verified by name every frame, so a stale one is free
# to detect and costs one re-scan.
#
# INPUTS — all static addresses, clear of the dungeon pools that move with the character heap (see DungeonPools):
#   0x01CDD88D  Xiao's equipped bag slot        (DngStatusData 0x01CD954C + 0x4340 + XiaoId 1)
#   0x01CDE516  her weapon-slot-0 element byte, + 0xF8 per bag slot — 00 Fire, 01 Ice, 02 Thunder, 03 Wind,
#               04 Holy, 05 None (Player.Xiao.WeaponSlot0.elementHUD)
#   0x01C75870  CTextureManager: last entry index at +0, entries at +0x10F8 stride 0x50, name +8, palette ptr +0x48
#
# Leaf routine: it calls nothing, touches only $t registers, and returns through $ra — so it needs no frame.

# +0x00 — the colours, RGBA with the PS2's 0x80 = opaque, indexed by the element byte. Tuned by the user against
# the element bars in the weapon menu (2026-09-15); keep in step with DivineBeastCat.ElementLooks.
.word 0x80000F80               # 0 Fire    128, 15,   0
.word 0x80682D09               # 1 Ice       9, 45, 104
.word 0x800094B4               # 2 Thunder 180,148,   0
.word 0x800F641E               # 3 Wind     30,100,  15
.word 0x80A04FC1               # 4 Holy    193, 79, 160
.word 0x80000000               # 5 None      0,  0,   0

# +0x18 — the entry point.
    lui   $t0, 0x01CD
    ori   $t0, $t0, 0xD88D
    lbu   $t1, 0x0($t0)          # Xiao's equipped bag slot
    sltiu $t2, $t1, 10
    beq   $t2, $zero, none       # nothing equipped: the None row
    nop
    lui   $t0, 0x01CD
    ori   $t0, $t0, 0xE516       # her weapon-slot-0 element byte
    sll   $t2, $t1, 8
    sll   $t3, $t1, 3
    subu  $t2, $t2, $t3          # slot * 0xF8
    addu  $t0, $t0, $t2
    lbu   $t4, 0x0($t0)
    sltiu $t2, $t4, 6
    bne   $t2, $zero, resolve
    nop
none:
    addiu $t4, $zero, 5
resolve:
    lui   $t5, 0x01FB
    lw    $t6, 0x429C($t5)       # the cached CTexture entry
    beq   $t6, $zero, search
    nop
    lw    $t7, 0x8($t6)          # still named catcape?
    lui   $t8, 0x6374
    ori   $t8, $t8, 0x6163       # "catc"
    bne   $t7, $t8, search
    nop
    lw    $t7, 0xC($t6)
    lui   $t8, 0x0065
    ori   $t8, $t8, 0x7061       # "ape\0"
    beq   $t7, $t8, paint
    nop
search:
    lui   $t0, 0x01C7
    ori   $t0, $t0, 0x5870       # the texture manager
    lw    $t1, 0x0($t0)          # last entry index
    sltiu $t2, $t1, 0xC4         # the table holds 196 entries and no more
    bne   $t2, $zero, capped
    nop
    addiu $t1, $zero, 0xC3
capped:
    addiu $t6, $t0, 0x10F8       # entry 0
    or    $t3, $zero, $zero      # i
scan:
    lw    $t7, 0x8($t6)
    lui   $t8, 0x6374
    ori   $t8, $t8, 0x6163
    bne   $t7, $t8, scan_next
    nop
    lw    $t7, 0xC($t6)
    lui   $t8, 0x0065
    ori   $t8, $t8, 0x7061
    beq   $t7, $t8, found
    nop
scan_next:
    addiu $t3, $t3, 1
    slt   $t8, $t1, $t3
    bne   $t8, $zero, ret        # not registered — Xiao's pack is not loaded, so there is no cape to colour
    nop
    b     scan
    addiu $t6, $t6, 0x50         # (delay slot)
found:
    lui   $t5, 0x01FB
    sw    $t6, 0x429C($t5)       # cache it for the frames after this one
paint:
    lw    $t7, 0x48($t6)         # the manager's palette copy (native pointer)
    lui   $t8, 0x1FFF
    ori   $t8, $t8, 0xFFFF
    and   $t7, $t7, $t8
    beq   $t7, $zero, ret
    nop
    lui   $t9, 0x01FB
    ori   $t9, $t9, 0x2700       # this cave's base = the colour table
    sll   $t2, $t4, 2
    addu  $t9, $t9, $t2
    lw    $t9, 0x0($t9)          # the element's colour
    lw    $t8, 0x0($t7)
    beq   $t8, $t9, ret          # the palette already holds it: most frames end here
    nop
    addiu $t2, $zero, 256        # every entry, so the pixels' index never matters
fill:
    sw    $t9, 0x0($t7)
    addiu $t7, $t7, 4
    addiu $t2, $t2, -1
    bne   $t2, $zero, fill
    nop
ret:
    jr    $ra
    nop
