# flash_slot.s — the machine-gun hit flash per slot: size, alpha, and for a pinned slot the whole fuse spark, timed in game frames.
# Assembled at 0x0027D320 (SmoothRestCave.FlashSlot, zero words of the dead SmoothRest body after the species lookup).
# CHIT_MACHINGUN_EFFECT::Draw (0x1AEA20) loops its 16 slots — s0 = the slot, s1 = the pool, a0 = &position[slot], v1 = the cell
# (2 − timer / 6) — and draws each live one (timer ≥ 0) with set3DCellModel(position, "basefx00", 5.0, cell << 4, 0x70, 16, 16, 0x80).
# ElfSpeciesPatches.PatchFlashSlot turns `lui v0, 0x40A0; mtc1 v0, f12` (0x1AEA7C) into `jal here; nop` and the alpha's
# `addiu t2, zero, 0x80` (0x1AEA9C) into `or t2, t3, zero`, so for every slot
#  · f12 = CodeCaves.FlashSizeTable[slot] (16 floats, baked 5.0) and t3 = CodeCaves.FlashAlphaTable[slot] (16 bytes, baked 0x80) —
#    Osmond's flashes as before;
# and for a slot 12–15 that CodeCaves.FlashPinTable pins (entry (slot − 12) × 0x20: +0 the CFrame, +4 the unit's model block, +8
# frames pinned, +0xC bursts asked for, +0x10 the point x, y, z in the frame's space), once a drawn frame:
#  · past frame 122 of the unit's death motion (11) — its blast —: the slot's timer out, the pin cleared, drawn invisible this last time;
#  · otherwise kept alight (timer 17), its frame count stepped, the cell = (count mod 12) / 4 (three cells, 4 frames each), a burst
#    asked for every 15 frames (+0xC, which the app answers with the guard spark), and the point placed through the frame's world
#    matrix (+0x150, row vectors: p · M) into position[slot] — the flash on the bone as this frame drew it.
# Clobbers v0, t3 (the alpha, read at 0x1AEA9C), t5–t9, f0–f4, and v1 for a pinned slot (its cell); all free here.

    lui   $t9, 0x01FB
    sll   $v0, $s0, 2
    addu  $v0, $v0, $t9
    lwc1  $f12, -0x09C0($v0)       # 0x01FAF640 CodeCaves.FlashSizeTable[slot]
    addu  $v0, $s0, $t9
    lbu   $t3, -0x0900($v0)        # 0x01FAF700 CodeCaves.FlashAlphaTable[slot]
    slti  $v0, $s0, 12
    bne   $v0, $zero, done         # only slots 12–15 can be pinned
    sll   $v0, $s0, 5
    addu  $v0, $v0, $t9            # v0 − 0xB00 = 0x01FAF680 + (slot − 12) × 0x20: the pin entry
    lw    $t9, -0x0B00($v0)        # the frame, or 0
    beq   $t9, $zero, done
    sll   $t7, $s0, 2
    addu  $t7, $t7, $s1            # t7 + 0x100 = &timer[slot]
    lw    $t8, -0x0AFC($v0)        # the unit's model block
    lw    $t6, 0x0BD8($t8)         # its playing motion
    addiu $t5, $zero, 11
    bne   $t6, $t5, live
    lw    $t6, 0x0260($t8)         # (delay) its frame: a positive float, so it compares as an int
    lui   $t5, 0x42F4              # 122.0
    slt   $t5, $t6, $t5
    bne   $t5, $zero, live
    addiu $t6, $zero, -1           # (delay)
    sw    $t6, 0x0100($t7)         # the blast: the flash out,
    sw    $zero, -0x0B00($v0)      # unpinned (the app sees it and ends its burst),
    b     done
    addu  $t3, $zero, $zero        # (delay) and drawn invisible this last time
live:
    addiu $t6, $zero, 17
    sw    $t6, 0x0100($t7)         # kept alight
    lw    $t8, -0x0AF8($v0)
    addiu $t8, $t8, 1
    sw    $t8, -0x0AF8($v0)        # frames pinned
    addiu $t7, $zero, 12
    divu  $zero, $t8, $t7
    mfhi  $t7
    srl   $v1, $t7, 2              # the cell: 0, 1, 2, four frames each
    addiu $t7, $zero, 15
    divu  $zero, $t8, $t7
    mfhi  $t7
    bne   $t7, $zero, place
    nop
    lw    $t7, -0x0AF4($v0)
    addiu $t7, $t7, 1
    sw    $t7, -0x0AF4($v0)        # a burst asked for
place:
    lwc1  $f0, -0x0AF0($v0)        # the point in the frame
    lwc1  $f1, -0x0AEC($v0)
    lwc1  $f2, -0x0AE8($v0)
    lwc1  $f3, 0x0150($t9)         # x = px·M00 + py·M10 + pz·M20 + M30
    mul.s $f3, $f0, $f3
    lwc1  $f4, 0x0160($t9)
    mul.s $f4, $f1, $f4
    add.s $f3, $f3, $f4
    lwc1  $f4, 0x0170($t9)
    mul.s $f4, $f2, $f4
    add.s $f3, $f3, $f4
    lwc1  $f4, 0x0180($t9)
    add.s $f3, $f3, $f4
    swc1  $f3, 0x0000($a0)
    lwc1  $f3, 0x0154($t9)         # height
    mul.s $f3, $f0, $f3
    lwc1  $f4, 0x0164($t9)
    mul.s $f4, $f1, $f4
    add.s $f3, $f3, $f4
    lwc1  $f4, 0x0174($t9)
    mul.s $f4, $f2, $f4
    add.s $f3, $f3, $f4
    lwc1  $f4, 0x0184($t9)
    add.s $f3, $f3, $f4
    swc1  $f3, 0x0004($a0)
    lwc1  $f3, 0x0158($t9)         # y
    mul.s $f3, $f0, $f3
    lwc1  $f4, 0x0168($t9)
    mul.s $f4, $f1, $f4
    add.s $f3, $f3, $f4
    lwc1  $f4, 0x0178($t9)
    mul.s $f4, $f2, $f4
    add.s $f3, $f3, $f4
    lwc1  $f4, 0x0188($t9)
    add.s $f3, $f3, $f4
    swc1  $f3, 0x0008($a0)
done:
    jr    $ra
    nop
