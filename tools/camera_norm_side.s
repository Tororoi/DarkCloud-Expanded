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

# ===== FishLineClamp @0x229100 (v4): Queens canal cast physics ================================
# Wraps the single FishLineStep call site (EdMoveChara @0x16D314). After the real Verlet step, in
# QUEENS during the CAST FLIGHT (chara_fishing == 3):
#  • CANAL-WALL z clamp (|z| <= 48): LOW TIDE ONLY (water < 15 — the user call: wall-hit behavior is
#    a low-tide problem), and per wall ONLY IF THE ROD IS INSIDE IT (point[0].z < 47 enables the +48
#    wall, point[0].z > -47 the -48 wall) — v3 clamped unconditionally and INSTANTLY SNAPPED a
#    bank-cast bobber (starting at the rod, z=-70) into the band = the "line lengthens immediately".
#  • BRIDGE BOXES (all tides): the REAL bridges are obj40/obj44 — arched walkable crossings at
#    x 774..826 and -74..-22 (v3's 187/590/1089 bands were the waterfall/pipe gates — wrong).
#    Table @0x2294C0: per box (xa,xb,za,zb,ylo,yhi) — 2 bridges x { legs S (z -41..-29, y<50),
#    legs N (z 29..41, y<50), arch/deck (|z|<28, y 52..86) }; the under-arch passage stays open.
#    Inside a box -> push out along the least-penetration HORIZONTAL axis, pos AND old = stop dead.
# R5900: c.lt.s .word-encoded; nop after mtc1/compares. 1 pad nop (SubC ends @0x2290F8).
nop
addiu $sp, $sp, -0x10
sw    $ra, 0x8($sp)
jal   0x1aa340                # the real FishLineStep(a0, a1)
nop
lui   $t0, 0x2a
lw    $t1, 0x2518($t0)        # town MapNo
addiu $t2, $zero, 2
bne   $t1, $t2, flc_done
nop
lw    $t1, 0x26e8($t0)        # chara_fishing
addiu $t2, $zero, 3
bne   $t1, $t2, flc_done
nop
lui   $t3, 0x4240             # +48
mtc1  $t3, $f10
nop
lui   $t3, 0xc240             # -48
mtc1  $t3, $f11
nop
# wall enables: t8 = +48 wall active, t9 = -48 wall active
addu  $t8, $zero, $zero
addu  $t9, $zero, $zero
lwc1  $f4, 0x2b28($t0)        # water level @0x2A2B28 (gp-0x6cc8)
lui   $t3, 0x4170             # 15.0 — below this = low tide
mtc1  $t3, $f5
nop
.word 0x46052034             # c.lt.s f4,f5 : low tide ?
nop
bc1f  flc_wallsset            # not low tide -> both walls stay OFF
nop
lui   $t1, 0x1d5
lwc1  $f6, 0x5e38($t1)        # point[0].z — the rod side
lui   $t3, 0x423c             # 47.0
mtc1  $t3, $f7
nop
.word 0x46073034             # c.lt.s f6,f7 : rod inside the +48 wall ?
nop
bc1f  flc_chklo
nop
addiu $t8, $zero, 1
flc_chklo:
lui   $t3, 0xc23c             # -47.0
mtc1  $t3, $f5
nop
.word 0x46062834             # c.lt.s f5,f6 : rod inside the -48 wall ?
nop
bc1f  flc_wallsset
nop
addiu $t9, $zero, 1
flc_wallsset:
lui   $t0, 0x1d5
addiu $t5, $t0, 0x5f50        # point + 18*0x10  (bobber + hang-down line)
addiu $t6, $t0, 0x60d0        # old_p + 18*0x10
jal   flc_pass
addiu $t7, $zero, 6
lui   $t0, 0x1d5
addiu $t5, $t0, 0x6350        # ukip
addiu $t6, $t0, 0x6390        # ukiop
jal   flc_pass
addiu $t7, $zero, 4
lui   $t0, 0x1d5
addiu $t5, $t0, 0x62b0        # hookp
addiu $t6, $t0, 0x62e0        # hookop
jal   flc_pass
addiu $t7, $zero, 3
flc_done:
lw    $ra, 0x8($sp)
jr    $ra
addiu $sp, $sp, 0x10

# pass: per point (t5 pos / t6 old, t7 count, stride 0x10): gated wall clamp, then bridge boxes.
# Uses t2 (table ptr), t3/t4 (counter/scratch) — t8/t9 wall enables preserved.
flc_pass:
lwc1  $f8, 0x8($t5)           # pos.z
beq   $t8, $zero, flc_skiphi
nop
.word 0x46085034             # c.lt.s f10,f8 : +48 < z ?
nop
bc1f  flc_skiphi
nop
swc1  $f10, 0x8($t5)
swc1  $f10, 0x8($t6)
mov.s $f8, $f10
flc_skiphi:
beq   $t9, $zero, flc_boxes
nop
.word 0x460B4034             # c.lt.s f8,f11 : z < -48 ?
nop
bc1f  flc_boxes
nop
swc1  $f11, 0x8($t5)
swc1  $f11, 0x8($t6)
mov.s $f8, $f11
flc_boxes:
lwc1  $f1, 0x0($t5)           # pos.x
lwc1  $f2, 0x4($t5)           # pos.y
lui   $t2, 0x22
ori   $t2, $t2, 0x93c0        # box table @0x2293C0
addiu $t4, $zero, 4
flc_bx:
lwc1  $f5, 0x0($t2)           # xa
.word 0x46050834             # c.lt.s f1,f5 : x < xa -> outside
nop
bc1t  flc_bxnext
nop
lwc1  $f6, 0x4($t2)           # xb
.word 0x46013034             # c.lt.s f6,f1 : xb < x -> outside
nop
bc1t  flc_bxnext
nop
lwc1  $f5, 0x8($t2)           # za
.word 0x46054034             # c.lt.s f8,f5 : z < za -> outside
nop
bc1t  flc_bxnext
nop
lwc1  $f6, 0xc($t2)           # zb
.word 0x46083034             # c.lt.s f6,f8 : zb < z -> outside
nop
bc1t  flc_bxnext
nop
lwc1  $f5, 0x10($t2)          # ylo
.word 0x46051034             # c.lt.s f2,f5 : y < ylo -> outside
nop
bc1t  flc_bxnext
nop
lwc1  $f6, 0x14($t2)          # yhi
.word 0x46023034             # c.lt.s f6,f2 : yhi < y -> outside
nop
bc1t  flc_bxnext
nop
# inside: px = min(x-xa, xb-x), pz = min(z-za, zb-z); resolve the smaller axis
lwc1  $f5, 0x0($t2)
sub.s $f7, $f1, $f5           # x - xa
lwc1  $f6, 0x4($t2)
sub.s $f9, $f6, $f1           # xb - x
.word 0x460939E9             # min.s f7,f7,f9 (px)
lwc1  $f5, 0x8($t2)
sub.s $f4, $f8, $f5           # z - za
lwc1  $f6, 0xc($t2)
sub.s $f3, $f6, $f8           # zb - z
.word 0x46032129             # min.s f4,f4,f3 (pz)
.word 0x46072034             # c.lt.s f4,f7 : pz < px -> resolve z
nop
bc1f  flc_resx
nop
lwc1  $f5, 0x8($t2)           # za
sub.s $f9, $f8, $f5           # z - za
lwc1  $f6, 0xc($t2)           # zb
sub.s $f3, $f6, $f8           # zb - z
.word 0x46034834             # c.lt.s f9,f3 : za side nearer ?
nop
bc1t  flc_z_a
nop
swc1  $f6, 0x8($t5)           # pos.z = zb
swc1  $f6, 0x8($t6)
b     flc_next
nop
flc_z_a:
swc1  $f5, 0x8($t5)           # pos.z = za
swc1  $f5, 0x8($t6)
b     flc_next
nop
flc_resx:
lwc1  $f5, 0x0($t2)           # xa
sub.s $f9, $f1, $f5           # x - xa
lwc1  $f6, 0x4($t2)           # xb
sub.s $f3, $f6, $f1           # xb - x
.word 0x46034834             # c.lt.s f9,f3 : xa side nearer ?
nop
bc1t  flc_x_a
nop
swc1  $f6, 0x0($t5)           # pos.x = xb
swc1  $f6, 0x0($t6)
b     flc_next
nop
flc_x_a:
swc1  $f5, 0x0($t5)           # pos.x = xa
swc1  $f5, 0x0($t6)
b     flc_next
nop
flc_bxnext:
addiu $t2, $t2, 0x18
addiu $t4, $t4, -1
bne   $t4, $zero, flc_bx
nop
flc_next:
addiu $t5, $t5, 0x10
addiu $t6, $t6, 0x10
addiu $t7, $t7, -1
bne   $t7, $zero, flc_pass
nop
jr    $ra
nop

# ---- bridge box table @0x2293C0 (flc code ends @0x2293BC): (xa,xb,za,zb,ylo,yhi) x4 ----
# USER-AUTHORED bridge-support boxes (2026-08): legs only, no arch/deck slab — the under/over-arch
# space stays open. Inner faces at z +35 / -36 hug the walls behind them; y 0..47.
.word 0xC29223D7              # W south xa -73.07
.word 0xC1B770A4              #         xb -22.93
.word 0x420C0000              #         za  35
.word 0x42480000              #         zb  50
.word 0x00000000              #         ylo 0
.word 0x423C0000              #         yhi 47
.word 0xC29223D7              # W north xa -73.07
.word 0xC1B770A4              #         xb -22.93
.word 0xC2480000              #         za -50
.word 0xC2100000              #         zb -36
.word 0x00000000              #         ylo 0
.word 0x423C0000              #         yhi 47
.word 0x4441BB85              # E south xa 774.93
.word 0x444E447B              #         xb 825.07
.word 0x420C0000              #         za  35
.word 0x42480000              #         zb  50
.word 0x00000000              #         ylo 0
.word 0x423C0000              #         yhi 47
.word 0x4441BB85              # E north xa 774.93
.word 0x444E447B              #         xb 825.07
.word 0xC2480000              #         za -50
.word 0xC2100000              #         zb -36
.word 0x00000000              #         ylo 0
.word 0x423C0000              #         yhi 47

# ===== QueensDragCheck @0x229460 (v4): waiting-state drag into wall/bridge -> UNCAST ==========
# Entered via the CheckUkiHook tail `j` @0x1AA2D4 (IsoPatcher routes it here); falls through into
# the settled-height cave @0x228E20 unmodified. Gates: chara_fishing == 4 and Queens. Fires when the
# floating uki is dragged past the canal wall (|z| > 49.5 — a wall-rest at 48 stays fishable), into
# a bridge box (table above, 1.5 horizontal inset so face-rest positions stay), or when the LINE
# PIERCES a bridge: rod (point[0]) standing ON a deck (y > 50, x in that bridge's band) with the uki
# under the bridge footprint (x in band, y < 50) — casting off the bridge's side (uki x outside the
# band) stays legit. 16 pad words: table ends @0x229420; dragcheck stays fixed @0x229460.
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
lui   $t0, 0x2a
lw    $t1, 0x26e8($t0)        # chara_fishing
addiu $t2, $zero, 4
bne   $t1, $t2, qdc_pass
nop
lw    $t1, 0x2518($t0)        # town MapNo
addiu $t2, $zero, 2
bne   $t1, $t2, qdc_pass
nop
lwc1  $f1, 0x18($sp)          # uki.z
abs.s $f2, $f1
lui   $t0, 0x4246             # 49.5
mtc1  $t0, $f1
nop
.word 0x46020834             # c.lt.s f1,f2 : dragged past the canal wall ?
nop
bc1t  qdc_uncast
nop
lwc1  $f1, 0x10($sp)          # uki.x
lwc1  $f2, 0x14($sp)          # uki.y
lwc1  $f8, 0x18($sp)          # uki.z
lui   $t3, 0x3fc0             # 1.5 inset
mtc1  $t3, $f4
nop
lui   $t0, 0x22
ori   $t0, $t0, 0x93c0        # box table
addiu $t3, $zero, 4
qdc_bx:
lwc1  $f5, 0x0($t0)           # xa
add.s $f5, $f5, $f4
.word 0x46050834             # c.lt.s f1,f5 : x < xa+1.5 -> outside
nop
bc1t  qdc_bxnext
nop
lwc1  $f6, 0x4($t0)           # xb
sub.s $f6, $f6, $f4
.word 0x46013034             # c.lt.s f6,f1 : xb-1.5 < x -> outside
nop
bc1t  qdc_bxnext
nop
lwc1  $f5, 0x8($t0)           # za
add.s $f5, $f5, $f4
.word 0x46054034             # c.lt.s f8,f5 : z < za+1.5 -> outside
nop
bc1t  qdc_bxnext
nop
lwc1  $f6, 0xc($t0)           # zb
sub.s $f6, $f6, $f4
.word 0x46083034             # c.lt.s f6,f8 : zb-1.5 < z -> outside
nop
bc1t  qdc_bxnext
nop
lwc1  $f5, 0x10($t0)          # ylo
.word 0x46051034             # c.lt.s f2,f5 : y < ylo -> outside
nop
bc1t  qdc_bxnext
nop
lwc1  $f6, 0x14($t0)          # yhi
.word 0x46023034             # c.lt.s f6,f2 : yhi < y -> outside
nop
bc1t  qdc_bxnext
nop
b     qdc_uncast              # dragged inside a bridge box
nop
qdc_bxnext:
addiu $t0, $t0, 0x18
addiu $t3, $t3, -1
bne   $t3, $zero, qdc_bx
nop
# line-pierces-bridge: rod on a deck + uki under that bridge's footprint
lui   $t1, 0x1d5
lwc1  $f9, 0x5e30($t1)        # rod point[0].x
lwc1  $f3, 0x5e34($t1)        # rod point[0].y
lui   $t2, 0x4248             # 50.0
mtc1  $t2, $f5
nop
.word 0x46032834             # c.lt.s f5,f3 : rod above 50 (standing on a deck) ?
nop
bc1f  qdc_pass
nop
lui   $t0, 0x22
ori   $t0, $t0, 0x93c0
addiu $t3, $zero, 2           # bridge bands from leg rows: W @+0x00, E @+0x30 (xa,xb)
qdc_lp:
lwc1  $f5, 0x0($t0)           # xa
.word 0x46054834             # c.lt.s f9,f5 : rod x < xa -> not this bridge
nop
bc1t  qdc_lpnext
nop
lwc1  $f6, 0x4($t0)           # xb
.word 0x46093034             # c.lt.s f6,f9 : xb < rod x -> not this bridge
nop
bc1t  qdc_lpnext
nop
.word 0x46050834             # c.lt.s f1,f5 : uki x < xa -> off the side, legit
nop
bc1t  qdc_pass
nop
.word 0x46060834             # c.lt.s f1,f6 : uki x < xb (inside the band) ?
nop
bc1f  qdc_pass
nop
lui   $t2, 0x4248             # 50.0
mtc1  $t2, $f5
nop
.word 0x46051034             # c.lt.s f2,f5 : uki y < 50 (under the deck) ?
nop
bc1t  qdc_uncast              # rod on deck, uki under it -> the line pierces the bridge
nop
b     qdc_pass
nop
qdc_lpnext:
addiu $t0, $t0, 0x30
addiu $t3, $t3, -1
bne   $t3, $zero, qdc_lp
nop
qdc_pass:
j     0x00228e20              # fall through: the settled-height uncast cave, unmodified
nop
qdc_uncast:
addiu $v0, $zero, 1           # invalid -> native auto-uncast (chara_fishing = 5)
j     0x001aa328              # CheckUkiHook epilogue
nop

# ===== QueensUkiGroundGate (v5): no bobber lift onto OVERHEAD floors =========================
# Hooked over FishLineStep's ground-store head (`lui v0,0x3f80` @0x1AA538 -> j here, mtc1 -> nop).
# v4's rule (skip floors above water+5) ALSO killed legit bank/walkway landings ("no floor
# collision from the top of the canal") — a real landing approaches from ABOVE its floor, while the
# teleport bug lifts the bobber UP onto a poly overhead (bridge deck over the under-arch bobber,
# pipe top over the water). v5 rule: in QUEENS, skip the lift only when the found floor is above
# the BOBBER ITSELF (hit.y > point[18].y + 2). Bank/canal-floor landings (bobber at/above the
# floor) lift exactly as vanilla, at every tide. Other towns fully vanilla.
ug_entry:
lui   $t0, 0x2a
lw    $t1, 0x2518($t0)        # town MapNo
addiu $t2, $zero, 2
bne   $t1, $t2, ug_store
nop
lwc1  $f1, 0xc4($sp)          # hit.y (the found floor)
lui   $t0, 0x1d5
lwc1  $f0, 0x5f54($t0)        # point[18].y — the bobber
lui   $t0, 0x4000             # 2.0
mtc1  $t0, $f2
nop
add.s $f0, $f0, $f2           # bobber.y + 2
.word 0x46010034             # c.lt.s f0,f1 : floor ABOVE the bobber -> overhead structure, skip
nop
bc1t  ug_skip
nop
ug_store:
lui   $v0, 0x3f80             # displaced vanilla head: ground := hit.y + 1.0
mtc1  $v0, $f1
nop
lwc1  $f0, 0xc4($sp)
add.s $f0, $f1, $f0
swc1  $f0, -0x6cc0($gp)
ug_skip:
j     0x001aa54c
nop
