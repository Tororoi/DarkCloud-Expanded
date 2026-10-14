# ally_manager.s — a summoned ally monster run by the game's own monster manager code: a PRIVATE CMonstorUnit (the Pocklekul's pockle,
# its unit 0) stepped, drawn and shadowed beside the floor's. Assembled at 0x0011EDC8 (DeadMcCave: the dead sceMcSetFileInfo body).
# DunPatches points the dungeon's three calls on the floor's manager here — motionDrive's `jal Step__12CMonstorUnitFi` (0x1DB749C), the
# draw's `jal DrawMonstor__12CMonstorUnitFv` (0x1DAE7E8) and the shadow pass's `jal DrawShadowMonstor__12CMonstorUnitFv` (0x1DADD44):
# each makes the call as before; after the floor's Step the ally's brain runs (tools/stubs/ally_brain.s, once a frame: the guard charge,
# the summon, its target, its end, its walk beside the summoner); then — while CodeCaves.AllyManager (0x01FAF7F8) holds a manager — the call is
# made again on that manager with NowMonstorUnit (gp−0x6320) pointing at it, since every script command reads NowMonstorUnit.

step_entry:
    lui   $t9, 0x001D
    ori   $t9, $t9, 0xD540         # Step__12CMonstorUnitFi (0x1DD540: ori is zero-extended, so the plain upper half)
    j     ride
    nop
draw_entry:
    lui   $t9, 0x001D
    ori   $t9, $t9, 0x8CD0         # DrawMonstor__12CMonstorUnitFv
    j     ride
    nop
shadow_entry:
    lui   $t9, 0x001D
    ori   $t9, $t9, 0x9800         # DrawShadowMonstor__12CMonstorUnitFv
    j     ride
    nop

# a0 = the floor's manager, a1 = Step's pause flag (the draws ignore it), t9 = the function.
ride:
    addiu $sp, $sp, -0x20
    sw    $ra, 0x00($sp)
    sw    $a1, 0x04($sp)
    sw    $t9, 0x08($sp)
    jalr  $t9                      # the floor's, as before
    nop
    lw    $t3, 0x08($sp)
    lui   $t4, 0x001D
    ori   $t4, $t4, 0xD540
    bne   $t3, $t4, rdraw          # a draw: no brain, never held
    nop
    lw    $a1, 0x04($sp)
    jal   0x0010C7D0               # the ally's brain (ally_brain.s; a1 = the pause flag)
    nop
rdraw:
    lui   $t0, 0x01FB
    lw    $t1, -0x0808($t0)        # CodeCaves.AllyManager (0x01FAF7F8)
    beq   $t1, $zero, done
    nop
    lw    $t3, -0x00C0($t1)        # its block's magic (block + 0x40, the manager at + 0x100)
    lui   $t4, 0x594C
    ori   $t4, $t4, 0x4C41         # "ALLY": its block overwritten → not ridden
    bne   $t3, $t4, done
    nop
    lw    $t2, -0x6320($gp)        # NowMonstorUnit
    sw    $t2, 0x0C($sp)
    sw    $t1, -0x6320($gp)        # the ally's manager is "now" for its scripts
    or    $a0, $t1, $zero
    lw    $a1, 0x04($sp)
    lw    $t9, 0x08($sp)
    jalr  $t9                      # the ally's
    nop
    lw    $t2, 0x0C($sp)
    sw    $t2, -0x6320($gp)        # the floor's again
done:
    lw    $ra, 0x00($sp)
    jr    $ra
    addiu $sp, $sp, 0x20
