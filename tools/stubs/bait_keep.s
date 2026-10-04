# bait_keep.s — BLESSED BAIT (Blessing Gun): bait is only spent on a fight. Assembled at 0x0027D230 (SmoothRestCave.BaitKeep, zero
# words of the dead SmoothRest body after the camera-height cave). EdMoveChara deletes a cast's bait on two rolls —
# `rand() % 100 < 20` when the float sinks (0x16C8DC) and `rand() % 100 < 30` when a hooked fish gets off (0x16C9F0) — and
# ElfFishingPatches.PatchBaitKeep points both `jal rand` here: rand() as before, but 99 (neither roll passes) while
# CodeCaves.BaitKeep (0x01FAF4D0) is non-zero. The mod holds the word non-zero through a fishing session while a Blessing Gun is
# owned (BlessingGun.FishingTick), zero otherwise — without the gun every bait is lost as in retail.
# Clobbers t0 (caller-saved across the call it replaces).

    addiu $sp, $sp, -16
    sw    $ra, 0($sp)
    jal   0x001046F8               # rand
    nop
    lw    $ra, 0($sp)
    addiu $sp, $sp, 16
    lui   $t0, 0x01FB
    lw    $t0, -0x0B30($t0)        # CodeCaves.BaitKeep (0x01FAF4D0)
    beq   $t0, $zero, done
    nop
    addiu $v0, $zero, 99           # 99 % 100 = 99: the roll fails, the bait stays
done:
    jr    $ra
    nop
