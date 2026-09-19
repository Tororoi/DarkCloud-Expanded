# cat_glow_palette.s — the Divine Beast cat's GLOW takes the equipped weapon's element colour, natively.
# Assembled at 0x01FB3A80 (ElfCave.CatGlowPalette); the copy-queue cave calls it once per dungeon frame, right after
# cat_palette.s does the same job for the cape and mask.
#
# WHY A PALETTE AND NOT MORE DISCS. A 64x64 RGBA32 glow disc is 16,448 B inside Xiao's character pack, and the weapons
# and effects pools are only what the character heap has left after her character data. Four per-element discs took the
# free space down to 45,024 B and opening the weapon menu tipped CDataAlloc2 into its silent spin.
# One 8-bit disc is 5,184 B and holds EVERY colour: the pixels are luminance ranks, the palette carries the hue.
#
# WHY THE TABLE IS COPIED STRAIGHT DOWN. The GS reads a PSMT8 palette in CSM1 order, which exchanges bits 3 and 4 of the
# index — and nothing de-swizzles it on the way: EnterTexture (0x1313B0) memcpy's the file's 1024 B to CTexture+0x48 and
# ReloadTexture blits them to VRAM verbatim (qwc 0x40). So build_cat_pack gives the disc only indices the permutation
# LEAVES ALONE — the 128 whose bits 3 and 4 match — and the 128 words of each table land at those same CLUT words:
# eight words, skip sixteen, eight words, repeated eight times. The disc needs 115 levels, so they all fit.
#
# THE TABLES are NINE 512-byte blocks at 0x01FB2880 (ElfCave.CatGlowPalTables) — rows 0-5 the elements, 6-8 the Divine
# Beast Title / Angel Shooter / Angel Gear looks — written at PATCH time by
# ElfPatches.PatchCatGlowPalettes from a blob build_cat_pack --palettes bakes off the same index map as the disc itself.
# Patch-time data in a code page is fine; a RUNTIME write here would SIGBUS PCSX2 (see the Mailbox note).
#
# INPUTS — static addresses, clear of the dungeon pools that move with the character heap (see DungeonPools):
#   0x01CDD88D  Xiao's equipped bag slot        (DngStatusData 0x01CD954C + 0x4340 + XiaoId 1)
#   0x01CDE516  her weapon-slot-0 element byte, + 0xF8 per bag slot — 00 Fire, 01 Ice, 02 Thunder, 03 Wind, 04 Holy,
#               05 None (Player.Xiao.WeaponSlot0.elementHUD)
#   0x01C75870  CTextureManager: last entry index at +0, entries at +0x10F8 stride 0x50, name +8, palette ptr +0x48
#   0x01FB42A0  the cat mailbox word caching the CTexture entry we found (verified by name every frame)
#
# ⚠ THE NAME WORDS ARE LITTLE-ENDIAN. "catglowp" sits at entry+8 as the bytes 63 61 74 67 6C 6F 77 70, so the two words
# to compare are 0x67746163 ("catg") and 0x70776F6C ("lowp") — the LAST byte of each group is the HIGH half of the word.
# Getting the second one wrong costs nothing visible: the scan matches "catg", fails, finds no entry and returns, and the
# glow just stays the colour it was baked with. It was written 0x706F776C ("lwop") first time and did exactly that
# ElfPatches.PatchCatGlowPalette refuses a stub that does not contain the right word.
#
# ONE PALETTE WORD IS THE STATE, the way the cape cave uses its first. It cannot BE the first here: word 0 is the disc's
# transparent rim and is identical in all six ramps, so this reads the BRIGHTEST level instead — table slot 114, which the
# index map puts at CLUT index 226. Holding the element's core colour already means there is nothing to do, and an entry
# rebuilt by a script-event wipe comes back carrying the baked "None" ramp and repaints itself. ⚠ Both offsets are baked
# into the two `lw`s below; build_cat_pack.glow_palettes asserts the level count and the six colours that make them valid.
#
# Leaf routine — calls nothing, touches only $t registers, returns through $ra, so it needs no frame.

    lui   $t5, 0x01FB
    lw    $t4, 0x42A4($t5)       # Mailbox.CatGlowPalRow — the row the mod asked for, ONE-based …
    beq   $t4, $zero, byelement  # … and 0 (a zero-filled page) means "work it out from the element" instead
    nop
    addiu $t4, $t4, -1
    sltiu $t2, $t4, 9            # nine rows: six elements, then the three weapon looks
    bne   $t2, $zero, resolve
    nop
    addiu $t4, $zero, 5          # a bogus row falls back to "None" rather than reading past the tables
    b     resolve
    nop
byelement:
    lui   $t0, 0x01CD
    ori   $t0, $t0, 0xD88D
    lbu   $t1, 0x0($t0)          # Xiao's equipped bag slot
    sltiu $t2, $t1, 10
    beq   $t2, $zero, none       # nothing equipped: the None ramp
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
    lw    $t6, 0x42A0($t5)       # the cached CTexture entry
    beq   $t6, $zero, search
    nop
    lw    $t7, 0x8($t6)          # still named catglowp?
    lui   $t8, 0x6774
    ori   $t8, $t8, 0x6163       # "catg"
    bne   $t7, $t8, search
    nop
    lw    $t7, 0xC($t6)
    lui   $t8, 0x7077
    ori   $t8, $t8, 0x6F6C       # "lowp"
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
    lui   $t8, 0x6774
    ori   $t8, $t8, 0x6163
    bne   $t7, $t8, scan_next
    nop
    lw    $t7, 0xC($t6)
    lui   $t8, 0x7077
    ori   $t8, $t8, 0x6F6C
    beq   $t7, $t8, found
    nop
scan_next:
    addiu $t3, $t3, 1
    slt   $t8, $t1, $t3
    bne   $t8, $zero, ret        # not registered — the cat's pack is not loaded, so there is no disc to colour
    nop
    b     scan
    addiu $t6, $t6, 0x50         # (delay slot)
found:
    lui   $t5, 0x01FB
    sw    $t6, 0x42A0($t5)       # cache it for the frames after this one
paint:
    lw    $t7, 0x48($t6)         # the manager's palette copy (native pointer)
    lui   $t8, 0x1FFF
    ori   $t8, $t8, 0xFFFF
    and   $t7, $t7, $t8
    beq   $t7, $zero, ret
    nop
    lui   $t9, 0x01FB
    ori   $t9, $t9, 0x2880       # the six tables
    sll   $t2, $t4, 9            # element * 512
    addu  $t9, $t9, $t2
    lw    $t3, 0x1C8($t9)        # the element's BRIGHTEST colour: table slot 114 …
    lw    $t8, 0x388($t7)        # … and the CLUT word the index map puts it at, 226
    beq   $t8, $t3, ret          # already painted: most frames end here
    nop
    or    $t0, $zero, $zero      # run = 0, of eight 32-entry runs
run:
    sll   $t1, $t0, 6
    addu  $t2, $t9, $t1          # src = table + run * 64   (16 words per run)
    sll   $t1, $t0, 7
    addu  $t3, $t7, $t1          # dst = clut  + run * 128  (32 words per run)
    or    $t5, $zero, $zero
lo:
    addu  $t6, $t2, $t5          # the run's first eight: indices 0..7, which the permutation fixes
    lw    $t8, 0x0($t6)
    addu  $t6, $t3, $t5
    sw    $t8, 0x0($t6)
    addiu $t5, $t5, 4
    sltiu $t6, $t5, 32
    bne   $t6, $zero, lo
    nop
    or    $t5, $zero, $zero
hi:
    addu  $t6, $t2, $t5          # and its last eight: indices 24..31, likewise fixed (8..23 are the moved ones)
    lw    $t8, 0x20($t6)
    addu  $t6, $t3, $t5
    sw    $t8, 0x60($t6)
    addiu $t5, $t5, 4
    sltiu $t6, $t5, 32
    bne   $t6, $zero, hi
    nop
    addiu $t0, $t0, 1
    sltiu $t6, $t0, 8
    bne   $t6, $zero, run
    nop
ret:
    jr    $ra
    nop
