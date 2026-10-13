# pockle_move.s — the Pocklekul's pockle moved by the game itself, once a dungeon frame. Assembled at 0x0010A770 (DeadReadIopmCave.PockleMove,
# after atla_dungeon.s in the dead sceCdReadIOPm body). The native-call cave (ElfFrameChainPatches.PatchCallRequest), the tail of the camera-pin
# chain, leaves through `j here` instead of its `jr ra`, so this runs every dungeon frame whether or not a call was posted and returns through
# that ra. CodeCaves.PockleMove (0x01FAFCA0): +0 frames left, +4 / +8 the step a frame on X and Y. While frames are left the pockle's slot
# (DungeonCharaDraw slot 2, its position at 0x01EAADB0) moves by the step and one frame is spent: the app sets the step and a few frames each
# tick, so a pockle whose app stops ticking stands still a few frames later. Caller-saved registers only (the hooked pass returns nothing).

    lui   $t0, 0x01FB
    lw    $t1, -0x0360($t0)        # frames left (0x01FAFCA0)
    blez  $t1, done
    nop
    addiu $t1, $t1, -1
    sw    $t1, -0x0360($t0)
    lwc1  $f0, -0x035C($t0)        # step X
    lwc1  $f1, -0x0358($t0)        # step Y
    lui   $t2, 0x01EB
    lwc1  $f2, -0x5250($t2)        # slot 2's X (0x01EAADB0)
    lwc1  $f3, -0x5248($t2)        # its Y
    add.s $f2, $f2, $f0
    add.s $f3, $f3, $f1
    swc1  $f2, -0x5250($t2)
    swc1  $f3, -0x5248($t2)
done:
    jr    $ra
    nop
