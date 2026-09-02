# Water-redraw ORDER GATE (2026-09-02) — dead CharaChange region, @0x229800.
#
# WHY: PatchWaterRedraw defers the water refraction pass (fb capture + DrawWaterSurface) from its
# vanilla mid-frame position to AFTER EdDrawCharacter, so the capture contains the player (the Queens
# low-tide wading look). But towns that run DEPTH OF FIELD around water — the Matataki waterfall —
# need the vanilla order: DOF blurs the frame mid-frame, and a deferred SHARP refraction quad then
# composites over the blurred pool = a "floating water surface" (confirmed by A/B ISO bisect,
# 2026-09-02). The deferral is only ever NEEDED while the wading mailbox is armed (Queens low tide),
# so gate it on exactly that.
#
# STRUCTURE (three pieces, fixed offsets — keep instruction counts exact):
#   +0x00 COND  — payload entry (PatchWaterRedraw's stub at 0x17BC00 jumps here on a GameMode match):
#                 mailbox 0x01FAE608 (MizuRedrawFramePtr) unarmed -> run the relocated payload NOW at
#                 the vanilla position (return slot = 0x17BCC4, the gate's no-match join);
#                 armed -> set the pending flag 0x01FAE600 and skip (hook cave draws after chars).
#   +0x30 SHIM  — the hook cave's draw call lands here instead of FLUSH_STUB directly: set the return
#                 slot to the hook cave's continuation (0x1A3870), then FLUSH_STUB.
#   +0x48 RET   — the relocated payload's final jump (was hardwired `j 0x1A3870`): jr [return slot].
# Return slot = 0x01FAE610 (mailbox page, documented free). FLUSH_STUB = 0x1A370C (VIF FLUSH ->
# capture-half 0x17BC0C -> ... -> RET).
cond:
lui   $at, 0x1fb
lw    $t0, -0x19f8($at)        # [0x01FAE608] wading mailbox — armed only at Queens low tide
bne   $t0, $zero, defer
nop
lui   $t1, 0x17
ori   $t1, $t1, 0xbcc4         # 0x0017BCC4 = the gate's no-match join (vanilla-position return)
sw    $t1, -0x19f0($at)        # [0x01FAE610] payload return slot
j     0x001a370c               # FLUSH_STUB -> capture + water pass at the VANILLA position
nop
defer:
sw    $at, -0x1a00($at)        # [0x01FAE600] pending flag (nonzero) -> hook cave draws after chars
j     0x0017bcc4
nop
shim:
lui   $at, 0x1fb
lui   $t1, 0x1a
ori   $t1, $t1, 0x3870         # 0x001A3870 = hook-cave continuation (the old hardwired return)
sw    $t1, -0x19f0($at)        # [0x01FAE610] payload return slot
j     0x001a370c               # FLUSH_STUB -> capture + water pass at the DEFERRED position
nop
ret_thunk:
lui   $at, 0x1fb
lw    $t9, -0x19f0($at)        # [0x01FAE610]
jr    $t9
nop
