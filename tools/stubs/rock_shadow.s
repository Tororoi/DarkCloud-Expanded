# rock_shadow.s — one extra shadow in the dungeon's shadow pass: the Terra Sword's boulder. Assembled at 0x001B5460
# (DebugIfCave.RockShadow, in dead DebugInfomationIF). Takes Draw_MainUnitShadow's `jal MGEndDrawShadow` (dun 0x1DADDD4,
# DunPatches; a0 = 0x40 already loaded): while the mailbox flag is set, MGDrawShadowFast(frame, &plane, &dir) draws the frame
# named there flattened onto the plane — inside the pass, after the player's, the enemies' and the NPCs' shadows, with the same
# GS state they use — then the displaced MGEndDrawShadow runs with its own a0 and returns to the caller.
#
# Mailbox (0x01FAFB80, CodeCaves.RockShadow): +0 flag  +4 the frame (guest)  +0x10 the plane point (x, h, y, 1)  +0x20 the
# projection direction (0, 1, 0, 0) — both quadword-aligned (MGDrawShadowFast copies them with sceVu0CopyVector).

    addiu $sp, $sp, -0x20
    sw    $ra, 0x0010($sp)
    sw    $a0, 0x0014($sp)
    lui   $t0, 0x01FB
    lw    $t1, -0x0480($t0)        # the flag (0x01FAFB80)
    beq   $t1, $zero, done
    nop
    lw    $a0, -0x047C($t0)        # the frame
    beq   $a0, $zero, done
    nop
    addiu $a1, $t0, -0x0470        # &plane (0x01FAFB90)
    jal   0x001303B0               # MGDrawShadowFast
    addiu $a2, $t0, -0x0460        # &dir (0x01FAFBA0), in the delay slot
done:
    lw    $a0, 0x0014($sp)
    lw    $ra, 0x0010($sp)
    j     0x00130B30               # MGEndDrawShadow, the displaced call
    addiu $sp, $sp, 0x20
