# ally_manager.s — a summoned ally monster run by the game's own monster manager code: a PRIVATE CMonstorUnit (Pocklekul's pockle, its
# unit 0) stepped, drawn and shadowed beside the floor's. Assembled at 0x0011EDC8 (DeadMcCave: the dead sceMcSetFileInfo and sceMcRename
# bodies). DunPatches points the dungeon's three calls on the floor's manager here — motionDrive's `jal Step__12CMonstorUnitFi`
# (0x1DB749C), the draw's `jal DrawMonstor__12CMonstorUnitFv` (0x1DAE7E8) and the shadow pass's `jal DrawShadowMonstor__12CMonstorUnitFv`
# (0x1DADD44): each makes the call as before, then — while CodeCaves.AllyManager (0x01FAF7F8) holds a manager — makes it again on that
# manager with NowMonstorUnit (gp−0x6320) pointing at it, since every script command reads NowMonstorUnit, and puts it back.
# call_priv: a native call (Core/NativeCall) on the ally's manager — CodeCaves.AllyCallFunc (0x01FAF7FC) called with the request's
# arguments and NowMonstorUnit pointing at the ally's manager around it (SetupBaseModel, SetupViewMonstor: label 1 runs inside).

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
    lui   $t0, 0x01FB
    lw    $t1, -0x0808($t0)        # CodeCaves.AllyManager (0x01FAF7F8)
    beq   $t1, $zero, done
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

# a0..t3 = the call's arguments (left as they are); the function and the manager from CodeCaves.
call_priv:
    addiu $sp, $sp, -0x20
    sw    $ra, 0x00($sp)
    lw    $t8, -0x6320($gp)
    sw    $t8, 0x04($sp)
    lui   $t9, 0x01FB
    lw    $t8, -0x0808($t9)        # CodeCaves.AllyManager
    sw    $t8, -0x6320($gp)
    lw    $t9, -0x0804($t9)        # CodeCaves.AllyCallFunc (0x01FAF7FC)
    jalr  $t9
    nop
    lw    $t8, 0x04($sp)
    sw    $t8, -0x6320($gp)
    lw    $ra, 0x00($sp)
    jr    $ra
    addiu $sp, $sp, 0x20
