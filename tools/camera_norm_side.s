# Camera-cave auxiliary bank @0x228F00 (dead CharaChange region, past fishlineUncastGate 0x228E20+148B).
# Assembles via tools/mips_asm.py to Resources/isoPatch/cameraNormSide.bin (embedded; written by
# IsoPatcher.PatchNativeCameraPostPass alongside the main camera cave). FIXED LAYOUT (the cave jals
# hardcode these VAs — keep the nop padding intact):
#   0x228F00  entry: export the true gather count, return        (cave entry `jal 0x228F00`)
#   0x228F40  SubA: slide-site  normalize N (ONE-SIDED v3)        (cave slide  `jal 0x228F40`)
#   0x229000  SubB: corner-site normalize N2 (ONE-SIDED v3)       (cave corner `jal 0x229000`)
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
# (v3 ONE-SIDED: the per-contact E_prev flip is REMOVED — 25 nops keep the fixed layout. The
# resolution now consumes the AUTHORED normal: front faces block; a camera that ends up BEHIND
# a wall (e.g. a fishing catch placing it inside a building cylinder) resolves to the authored
# side on its first crossing = it ESCAPES instead of being fenced in by a two-sided wall.)
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
# (v3 ONE-SIDED: flip removed — see SubA. 25 nops keep the layout.)
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
jr    $ra
nop

# ===== SubC @0x2290C0: GATED ground-clamp difference (called by the cave's height section) ====
# in:  sp+0x5c = groundY (eye-down ray hit; 0 = miss), sp+0x24 = ref.y
# out: f6 = groundY − ref.y, or -1000.0 (guard inert) when the "floor" is more than GUARD_MAX
#      above the ref's plane — that's a rim/mesa towering over the player, not a floor. Vanilla
#      has no eye-floor hoist at all; hoisting onto Brownboo's crater rim was the warp-arrival
#      pan (see town_camera_collision.s ground clamp). Scratch: f7, $t0 (the call site's own).
# 2 pad nops first: SubB's jr ends @0x2290B4; SubC must start exactly @0x2290C0.
nop
nop
lwc1  $f6, 0x5c($sp)          # groundY
lwc1  $f7, 0x24($sp)          # ref.y
sub.s $f6, $f6, $f7           # groundY − ref.y
lui   $t0, 0x4220             # GUARD_MAX = 40.0 (floors within 40 of the ref plane behave as before)
mtc1  $t0, $f7
nop
.word 0x46063834             # c.OLT.s f7,f6 : GUARD_MAX < (groundY − ref.y) ?
nop
bc1f  scret
nop
lui   $t0, 0xc47a             # -1000.0 -> guard inert (matches the ray-miss path's intent)
mtc1  $t0, $f6
nop
scret:
jr    $ra
nop
