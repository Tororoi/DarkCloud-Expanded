# WORLD-SPACE HEIGHT subroutine @0x27D090 (free cave, ISO-baked). Called by the camera function after the
# height ease. The eye's DESCENT is bounded in WORLD Y (not the ref-relative offset): a falling player drops
# at fall speed while the camera descends at most H_FALL_RATE/frame — they no longer "fall together" (the
# offset-based limit tracked ref.y straight down into terrain = the ground clip).
# While ground-PINNED and the player OCCLUDED, the pull toward the PLAYER — diagonal, not perpendicular to
# the flat top — projects onto the ground as a horizontal glide: the dist target shrinks by GROUND_GLIDE_K x
# the current boom length per frame, sliding the eye toward/over the lip (the building slide, ground axis).
# Visible-but-pinned = no glide. Stack contract (caller sp in a1): 0x70 h_e in/out (ref-relative offset),
# 0x74 dist target in/out, 0x24 ref.y, 0x8c h_min (offset), 0x98 last-frame LOS flag.
# Clobbers v1/at/t-regs/f2-f8 — the CALLER reloads v1.
addiu $sp, $sp, -0x20
sw    $ra, 0x18($sp)
addu  $t9, $a1, $zero
# ===== REACQUISITION GATE (HEIGHT only): when wall-pinched strictly inside rest, freeze the eased
# height target h_e @0x70 at the CURRENT height — no automatic vertical motion while pinned (the
# zeroed glide/climb/θ-slide tunables kill the other auto-movers). The DIST target @0x74 is left
# ALONE deliberately: distance always seeks BASE_DIST, so a camera pulled in by a wall recovers back
# out to resting distance once the wall stops constraining it (the swept-slide caps it meanwhile).
# Once d recovers past the gate, height unfreezes and eases back to rest too. Runs FIRST so the
# fall-bound below still acts on the frozen value. Lives HERE not in the main fn: that one is wedged
# against set2DSprite_Start with 8 bytes of slack.
lwc1  $f4, 0x2d0($v1)         # current dist
lui   $t0, 0x429e             # REACQ_GATE = BASE_DIST - 1 (PutValIn). STRICT inside-rest threshold: at open
mtc1  $t0, $f5                #   rest d eases asymptotically to BASE (can sit at 79.99), and freezing there
nop                           #   would kill the right-stick height control — only truly pinched freezes.
.word 0x46052034              # c.OLT.s f4,f5 : d_cur < GATE ? (pinched inside rest -> freeze height)
nop
bc1f  rqgok
nop
lwc1  $f4, 0x2d4($v1)         # freeze height target at current (offset space, same as h_e)
swc1  $f4, 0x70($t9)
rqgok:
lwc1  $f6, 0x70($t9)          # h_e (offset)
lwc1  $f2, 0x24($t9)          # ref.y
add.s $f6, $f6, $f2           # desired eye WORLD y
lui   $t3, 0x01f1
ori   $t3, $t3, 0x0050
lwc1  $f3, 0x4($t3)           # E_prev.y (last frame's constrained eye, world)
# warp-continuity: only bound the descent when last frame's eye is plausibly continuous with this one
sub.s $f4, $f3, $f6
abs.s $f4, $f4
lui   $t0, 0x43c8             # WARP_BREAK (PutValC; 400 — bigger jumps = area change/warp, no bound)
mtc1  $t0, $f7
nop
.word 0x46072034             # c.OLT.s f4,f7 : |E_prev.y − desired| < break ?
nop
bc1f  hnobound
nop
lui   $t0, 0x4040             # H_FALL_RATE (PutValC; max WORLD-space height drop per frame)
mtc1  $t0, $f7
nop
sub.s $f3, $f3, $f7           # lowest world y allowed this frame
.word 0x460331a8             # max.s f6,f6,f3 — the ABSOLUTE descent bound (upward stays instant)
hnobound:
# hard ground floor in world space (supreme), with the pinned test taken BEFORE the clamp
lwc1  $f4, 0x8c($t9)          # h_min (offset form)
add.s $f4, $f4, $f2           # -> world floor = groundY + MIN_GROUND_CLEAR
.word 0x46043034             # c.OLT.s f6,f4 : below the floor ? (= pinned)
nop
.word 0x460431a8             # max.s f6,f6,f4 — apply the floor (does not touch the compare flag)
bc1f  hnoglide
nop
# pinned: glide toward the player if OCCLUDED (last frame's LOS flag @0x98)
lw    $t0, 0x54($t9)          # last-frame LOS flag (0x54 — NOT 0x98, which the corner verify spills over)
bltz  $t0, hnoglide
nop
lui   $at, 0x1d2
lw    $v1, -0x6988($at)
lwc1  $f7, 0x2d0($v1)         # current boom length
lui   $t0, 0x3d00             # GROUND_GLIDE_K (PutValC; boom fraction glided toward the player per frame)
mtc1  $t0, $f8
nop
mul.s $f8, $f7, $f8           # glide = K·current
sub.s $f5, $f7, $f8           # dist target := CURRENT − glide — a PROGRESSIVE ratchet toward the player
                              #   (subtracting from the constant BASE target made the glide a fixed 2.5-unit
                              #   offset that converged and PARKED — the "missing reacquisition")
lui   $t0, 0x4140             # GLIDE_MIN_DIST (PutValC; the glide never pulls closer than this)
mtc1  $t0, $f8
nop
.word 0x46082968             # max.s f5,f5,f8
swc1  $f5, 0x74($t9)
hnoglide:
# DESCENT HOLD: while the eye is still descending (world height above rest by more than DESCENT_HOLD), the
# boom may shorten but never EXTEND — the outward dist recovery at the lip pushed the eye back over the
# cliff top, re-pinning it = the crossover bounce (a pinned/unpinned limit cycle). Recovery to BASE resumes
# once the height has arrived near rest.
lwc1  $f4, 0x94($t9)          # rest target offset (REST_H + stick, pre-clamp)
add.s $f4, $f4, $f2           # rest world y
sub.s $f4, $f6, $f4           # descent excess
lui   $t0, 0x4170             # DESCENT_HOLD (PutValC; excess above rest that freezes outward recovery)
mtc1  $t0, $f7
nop
.word 0x46072034             # c.OLT.s f4,f7 : excess < hold ? -> normal recovery
nop
bc1t  hnohold
nop
lwc1  $f5, 0x74($t9)          # pending dist target
lui   $at, 0x1d2
lw    $v1, -0x6988($at)
lwc1  $f7, 0x2d0($v1)         # current boom length
.word 0x46053834             # c.OLT.s f7,f5 : current < target ? (target would extend the boom)
nop
bc1f  hnohold
nop
swc1  $f7, 0x74($t9)          # hold: target := current — never extend mid-descent
hnohold:
sub.s $f6, $f6, $f2           # back to ref-relative offset
swc1  $f6, 0x70($t9)
lw    $ra, 0x18($sp)
jr    $ra
addiu $sp, $sp, 0x20
