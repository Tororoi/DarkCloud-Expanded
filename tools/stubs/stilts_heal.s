# Brownboo stilts heal (v4, 2026-09-02) — dead CharaChange region (jal-legal), @0x229780.
#
# ROOT CAUSE (settled from the clean-walking vs garbled-fishing GS-dump pair, same visit): the posts
# above the water are painted by the WATERSIDE REDRAW — scene geometry re-drawn AFTER DrawWater so poles
# crossing the surface show in front of it (the ~658-vert pass sampling scene bank 0x01's first atlas,
# GS 0x1A40 T8 512x512 cbp 0x1E40). During fishing, FishingDrawFish (a fish tile) and FishLineDraw's
# rod/bobber MGDraws (which carry the c01d_turi player model's PACKET-BAKED texture uploads at
# 0x1A40..0x20C0) clobber that atlas BETWEEN its frame-head upload and the waterside redraw. In walking
# the posts draw in scene pass 1, pre-clobber — why walking is always clean, and why the quit menu
# (which pauses the fishing draws) heals them. Block parks can never fix this: the turi/rod/bobber
# uploads are baked into model VIF packets, not block descriptors ("no change ever").
#
# HOOK CHAINING: 0x17BB48 is vanilla `jal ReloadTexture(mgr,pkt,0x15)`, but PatchWaterRedraw retargets
# it to its EARLY_STUB (0x17BBA4), which does the optional early-player draw (canal-wading mailbox) and
# ALWAYS performs the displaced group-0x15 bind before exiting via a constant `j 0x17BB50`. This cave
# takes the jal instead: when a Brownboo fishing session is drawing, ReloadTexture(block 1) first
# (re-upload the scene bank chain, restoring the atlas the waterside redraw samples), then continue
# into EARLY_STUB. Entered by jal but exits by constant j (EARLY_STUB never returns either), so $ra is
# dead and no frame is needed. v2 proved this exact re-upload mechanically safe one call earlier
# (hook 0x17BB24) — it only failed because that point precedes the rod/bobber clobber.
#
# ⚠ REGISTER CONTRACT (v4.1 fix — the "water/boardwalk/ladder/fence garbled in both modes" bug):
# EARLY_STUB's unarmed fast-path does NOT rebuild its arguments — it branches straight onto its
# `jal ReloadTexture`, relying on the CALLER's a0=mgr / a1=Vif1Packet / a2=0x15 (the vanilla setup at
# 0x17BB38..44) still being live. This cave's calls destroy them (FishingDrawCheck alone loads GameMode
# into $a0!), which made the water-block reload run with a garbage manager pointer (walking) or with
# a2=1 (fishing) — block 0x15 (s04b02/w01: water surface, ladders, fences) never reloaded. So the exit
# path MUST rebuild all three registers before jumping into EARLY_STUB.
lui   $t0, 0x2a
lw    $t0, 0x2518($t0)         # MapNo @0x002A2518
addiu $t1, $zero, 14
bne   $t0, $t1, out            # Brownboo only — vanilla fishing towns keep the stock frame
nop
jal   0x001775C0               # FishingDrawCheck() — nonzero while a fishing session draws
nop
beq   $v0, $zero, out
nop
jal   0x0012E280               # v0 = GetVif1Packet()
nop
lui   $a0, 0x1c7
addiu $a0, $a0, 0x5870         # a0 = CTextureManager 0x01C75870
move  $a1, $v0                 # a1 = packet
jal   0x00133070               # ReloadTexture(mgr, packet, 1) — heal the scene bank atlas
addiu $a2, $zero, 1            # (delay slot)
out:
lui   $a0, 0x1c7
addiu $a0, $a0, 0x5870         # a0 = CTextureManager 0x01C75870   (rebuild EARLY_STUB's contract)
lw    $a1, -0x742c($gp)        # a1 = Vif1Packet (gp-relative global, same load as vanilla 0x17BB40)
addiu $a2, $zero, 0x15         # a2 = water block group
j     0x0017BBA4               # continue into PatchWaterRedraw's EARLY_STUB (early player + 0x15 bind)
nop
