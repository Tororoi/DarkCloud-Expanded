# fuse_capture.s — each Bomb Gemron's wick, captured while its pose is the one drawn. Assembled at 0x0010A910 (DeadCdCave.FuseCapture,
# after the blast-radius cave in the dead sceCdGetToc body). Every unit of a species shares ONE frame tree (SpawnMonstor copies the
# base character, frame pointer and all) and CMonstorUnit::DrawMonstor poses it for each unit in turn, then takes that unit's points
# (its shot launch frames, its lock-on point) with CFrame::GetWorldPosition before the next unit re-poses it — so a bone's matrix read
# anywhere later is the LAST unit's. ElfSpeciesPatches.PatchFuseCapture points DrawMonstor's per-unit `jal MGSetAmbient` (0x1D8F84,
# after the unit's Step, before its captures; s2 = the unit block, s3 = the unit) here: the call as before, then for the
# CodeCaves.FlashPinTable entry whose model block is this unit's (*s2 + 0x1FD60 + unit × 0x3510), GetWorldPosition(its frame,
# &capture, &its point) into CodeCaves.FlashCapture (entry + 0x90), stamped (+0x9C) with the pin's frame count — the flash's draw
# (flash_slot.s) shows a pinned flash only where it was captured this frame.
# Clobbers what the two calls do (a0–a2, t-registers); the loop reloads everything it needs after.

    addiu $sp, $sp, -0x30
    sw    $ra, 0x0020($sp)
    jal   0x0012DD00               # MGSetAmbient, as before
    nop
    addiu $t1, $zero, 0x3510
    mult  $s3, $t1
    mflo  $t1
    addu  $t1, $t1, $s2
    lui   $t2, 0x0001
    ori   $t2, $t2, 0xFD60         # 0x1FD60: the unit block → its model block (ModelScaleOffsets.ModelFromUnit)
    addu  $t1, $t1, $t2            # this unit's model block
    lui   $t3, 0x01FB
    addiu $t3, $t3, -0x0980        # 0x01FAF680 CodeCaves.FlashPinTable, entry 0
    addiu $t4, $zero, 4
loop:
    lw    $t5, 0x0000($t3)         # the frame, or 0
    beq   $t5, $zero, next
    lw    $t6, 0x0004($t3)         # (delay) its unit's model block
    bne   $t6, $t1, next
    nop
    sw    $t3, 0x0028($sp)
    addu  $a0, $t5, $zero
    addiu $a1, $t3, 0x0090         # the capture
    jal   0x00128D60               # CFrame::GetWorldPosition(frame, world, local)
    addiu $a2, $t3, 0x0010         # (delay) the point in the frame
    lw    $t3, 0x0028($sp)
    lw    $t7, 0x0008($t3)         # the pin's frame count
    b     ret
    sw    $t7, 0x009C($t3)         # (delay) the capture's stamp
next:
    addiu $t4, $t4, -1
    bne   $t4, $zero, loop
    addiu $t3, $t3, 0x0020         # (delay)
ret:
    lw    $ra, 0x0020($sp)
    jr    $ra
    addiu $sp, $sp, 0x30
