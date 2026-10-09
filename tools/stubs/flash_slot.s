# flash_slot.s — the machine-gun hit flash per slot: size, alpha, and for a pinned slot the whole fuse spark, timed in game frames.
# Assembled at 0x0027D320 (SmoothRestCave.FlashSlot, zero words of the dead SmoothRest body after the species lookup).
# CHIT_MACHINGUN_EFFECT::Draw (0x1AEA20) loops its 16 slots — s0 = the slot, s1 = the pool, a0 = &position[slot], v1 = the cell
# (2 − timer / 6) — and draws each live one (timer ≥ 0) with set3DCellModel(position, "basefx00", 5.0, cell << 4, 0x70, 16, 16, 0x80).
# ElfSpeciesPatches.PatchFlashSlot turns `lui v0, 0x40A0; mtc1 v0, f12` (0x1AEA7C) into `jal here; nop` and the alpha's
# `addiu t2, zero, 0x80` (0x1AEA9C) into `or t2, t3, zero`, so for every slot
#  · f12 = CodeCaves.FlashSizeTable[slot] (16 floats, baked 5.0) and t3 = CodeCaves.FlashAlphaTable[slot] (16 bytes, baked 0x80) —
#    Osmond's flashes as before;
# and for a slot 12–15 that CodeCaves.FlashPinTable pins (entry (slot − 12) × 0x20: +0 the CFrame, +4 the unit's model block, +8
# frames pinned, +0xC bursts asked for, +0x10 the point x, y, z in the frame's space), once a drawn frame (the count is stepped
# after the capture compares, so a capture stamped this frame equals count − 1 here):
#  · past its blast — frame 122 of the death motion (11), or the final pose (217) of the self-destruct (key 14, frames 200–218: the
#    guard's return reversed, then the guard loop once) —: the slot's timer out, the pin cleared, drawn invisible this last time;
#  · otherwise kept alight (timer 17), its frame count stepped, the cell = (count mod 12) / 4 (three cells, 4 frames each), a burst
#    asked for every 15 frames (+0xC, which the app answers with the guard spark), and position[slot] = the wick as the monster draw
#    captured it for this unit this frame (CodeCaves.FlashCapture, fuse_capture.s: the species' frame tree is shared, so its matrix
#    here is the last unit's) — drawn invisible when the unit was not drawn this frame; the slot's guard burst (HitMark[slot]) is
#    carried to the same point, so it travels with the Gemron instead of trailing where it went off.
# Clobbers v0, t3 (the alpha, read at 0x1AEA9C), t5–t9, and v1 for a pinned slot (its cell); all free here.

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
    beq   $t6, $t5, blast          # the death motion: its blast at frame 122
    lui   $t9, 0x42F4              # (delay) 122.0
    addiu $t5, $zero, 14
    bne   $t6, $t5, live           # the self-destruct (key 14, frames 200–218): its blast on its final pose
    lui   $t9, 0x4359              # (delay) 217.0 (BombGemronBake.SelfDestructEnd − 1)
blast:
    lw    $t6, 0x0260($t8)         # its frame: a positive float, so it compares as an int
    slt   $t5, $t6, $t9
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
    lw    $t7, -0x0A64($v0)        # the capture's stamp (CodeCaves.FlashCapture + 0xC; fuse_capture.s, in the monster draw)
    lw    $t8, -0x0AF8($v0)        # this frame's count
    addiu $t8, $t8, -1
    bne   $t7, $t8, unseen         # not drawn this frame (off screen, culled): no flash where it was
    nop
    lw    $t7, -0x0A70($v0)        # the wick where this unit was drawn
    sw    $t7, 0x0000($a0)
    lw    $t7, -0x0A6C($v0)
    sw    $t7, 0x0004($a0)
    lw    $t7, -0x0A68($v0)
    sw    $t7, 0x0008($a0)
    addiu $t6, $zero, 0x0660       # the slot's guard burst (HitMark[slot], 0x01EBE140 + slot × 0x660) follows the wick: its marks are
    mult  $s0, $t6                 # drawn at offsets from its origin, so the origin carried along carries the burst
    mflo  $t6
    lui   $t5, 0x01EC
    addiu $t5, $t5, -0x1EC0        # 0x01EBE140 HitMark
    addu  $t6, $t6, $t5
    lw    $t5, 0x0658($t6)         # its kind
    addiu $t9, $zero, 2            # a guard burst (a hit may have taken the entry: then it stays where it is)
    bne   $t5, $t9, done
    lw    $t5, 0x0654($t6)         # (delay) its marks still drawing
    blez  $t5, done
    lw    $t7, -0x0A70($v0)        # (delay)
    sw    $t7, 0x0010($t6)
    lw    $t7, -0x0A6C($v0)
    sw    $t7, 0x0014($t6)
    lw    $t7, -0x0A68($v0)
    b     done
    sw    $t7, 0x0018($t6)         # (delay)
unseen:
    addu  $t3, $zero, $zero        # drawn invisible
done:
    jr    $ra
    nop
