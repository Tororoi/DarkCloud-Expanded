# Camera-cave auxiliary bank @0x228F00 (dead CharaChange region, past fishlineUncastGate 0x228E20+148B).
# Assembles via tools/mips_asm.py to Resources/isoPatch/cameraNormSide.bin (embedded; written by
# IsoPatcher.PatchNativeCameraPostPass alongside the main camera cave). FIXED LAYOUT (the cave jals
# hardcode these VAs — keep the nop padding intact):
#   0x228F00  entry: export the true gather count, return        (cave entry `jal 0x228F00`)
#   0x228F40  SubA: slide-site  normalize N + E_prev-side flip   (cave slide  `jal 0x228F40`)
#   0x229000  SubB: corner-site normalize N2 + E_prev-side flip  (cave corner `jal 0x229000`)
#
# WINDING-AGNOSTIC, PER-CONTACT (v2 — the v1 gather-time "flip every normal to the REF's side" pass was
# WRONG for closed shells: the eye orbits THROUGH the far half of e.g. Brownboo's 75-radius cylinder, whose
# far walls face away from the ref; flipping them inward made the slide resolve far-wall crossings to the
# ref side = INSIDE the shell — the camera was actively pulled through. Verified: CheckHit's detection chain
# (straddle test, IntersectionPoint_line_poly3, Check_Point_Poly3_XYZ) is fully sign-agnostic, so the ONLY
# sign consumer is the slide/corner RESOLUTION — flip there, per contact, to the side the sweep CAME FROM
# (E_prev). Correct on both halves of any shell regardless of authored winding. Caveat (the old E0-derived
# side's known flaw): after a genuine breach the constraint sides with the breached position — acceptable;
# E_prev is the CONSTRAINED eye (breaches are rare) and the 128u warp-skip resets it across teleports.)
#
# ⚠ EE rules: nop after mtc1 and FP compares; sqrt.s/c.OLT.s hand-encoded (.word, ft-operand forms).

# ===== entry @0x228F00: gather-count export ==================================================
# in: $s8 = poly count. The WorkBuffer `used` field is the 2000-unit per-frame Alloc RESERVATION,
# not a fill level — this word (Mailbox.CamGatherCount @0x01F10068) is the real count.
lui   $t2, 0x1f1
sw    $s8, 0x68($t2)
jr    $ra
nop
nop
nop
nop
nop
nop
nop
nop
nop
nop
nop
nop
nop

# ===== SubA @0x228F40: slide-site normal prep ================================================
# in:  $t1 = hit CCPoly base (+0x30 N, unnormalized), sp+0x40 = hit point P (quad)
# out: f4/f5/f6 = N̂, flipped so it faces E_prev's side of the hit plane (the side the sweep came from)
# scratch: f7/f8/f10, $t2/$t3 (all dead at the call site; the cave re-materializes t0-t3 after)
lwc1  $f4, 0x30($t1)
lwc1  $f5, 0x34($t1)
lwc1  $f6, 0x38($t1)
mul.s $f7, $f4, $f4
mul.s $f8, $f5, $f5
add.s $f7, $f7, $f8
mul.s $f8, $f6, $f6
add.s $f7, $f7, $f8           # N·N
.word 0x460701C4             # sqrt.s f7,f7 (EE ft-operand form)
nop
lui   $t2, 0x3f80             # 1.0
mtc1  $t2, $f8
nop
div.s $f8, $f8, $f7           # 1/|N|
nop
nop
mul.s $f4, $f4, $f8
mul.s $f5, $f5, $f8
mul.s $f6, $f6, $f8           # N̂
lui   $t3, 0x1f1
ori   $t3, $t3, 0x0050        # E_prev (mailbox; not yet updated this frame = last CONSTRAINED eye)
lwc1  $f7, 0x0($t3)
lwc1  $f8, 0x40($sp)
sub.s $f7, $f7, $f8
mul.s $f10, $f7, $f4
lwc1  $f7, 0x4($t3)
lwc1  $f8, 0x44($sp)
sub.s $f7, $f7, $f8
mul.s $f7, $f7, $f5
add.s $f10, $f10, $f7
lwc1  $f7, 0x8($t3)
lwc1  $f8, 0x48($sp)
sub.s $f7, $f7, $f8
mul.s $f7, $f7, $f6
add.s $f10, $f10, $f7         # p_prev = N̂·(E_prev − P)
mtc1  $zero, $f8
nop
.word 0x46085034             # c.OLT.s f10,f8 : p_prev < 0 ?  (sweep origin on the anti-normal side)
nop
bc1f  saret
nop
sub.s $f4, $f8, $f4           # flip N̂ to E_prev's side
sub.s $f5, $f8, $f5
sub.s $f6, $f8, $f6
saret:
jr    $ra
nop
nop
nop

# ===== SubB @0x229000: corner-verify-site normal prep ========================================
# in:  $t1 = verify-hit CCPoly base, sp+0x40 = hit point P
# out: f7/f8/f9 = N̂2, flipped to E_prev's side
# scratch: f3/f10/f11, $t2/$t3 — MUST NOT touch f0(d')/f2(h')/f4/f5/f6 (the restored spills)
lwc1  $f7, 0x30($t1)
lwc1  $f8, 0x34($t1)
lwc1  $f9, 0x38($t1)
mul.s $f10, $f7, $f7
mul.s $f11, $f8, $f8
add.s $f10, $f10, $f11
mul.s $f11, $f9, $f9
add.s $f10, $f10, $f11        # |N2|²
.word 0x460A0284             # sqrt.s f10,f10
nop
lui   $t2, 0x3f80             # 1.0
mtc1  $t2, $f11
nop
div.s $f11, $f11, $f10        # 1/|N2|
nop
nop
mul.s $f7, $f7, $f11
mul.s $f8, $f8, $f11
mul.s $f9, $f9, $f11          # N̂2
lui   $t3, 0x1f1
ori   $t3, $t3, 0x0050        # OLD E_prev (the corner cast's own `from`)
lwc1  $f10, 0x0($t3)
lwc1  $f11, 0x40($sp)
sub.s $f10, $f10, $f11
mul.s $f3, $f10, $f7
lwc1  $f10, 0x4($t3)
lwc1  $f11, 0x44($sp)
sub.s $f10, $f10, $f11
mul.s $f10, $f10, $f8
add.s $f3, $f3, $f10
lwc1  $f10, 0x8($t3)
lwc1  $f11, 0x48($sp)
sub.s $f10, $f10, $f11
mul.s $f10, $f10, $f9
add.s $f3, $f3, $f10          # p_prev2 = N̂2·(E_prev − P)
mtc1  $zero, $f10
nop
.word 0x460A1834             # c.OLT.s f3,f10 : p_prev2 < 0 ?
nop
bc1f  sbret
nop
sub.s $f7, $f10, $f7          # flip N̂2 to E_prev's side
sub.s $f8, $f10, $f8
sub.s $f9, $f10, $f9
sbret:
jr    $ra
nop
