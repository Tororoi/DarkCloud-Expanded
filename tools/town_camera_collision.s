# Town-camera collision function @0x14B838 (SOURCE OF TRUTH — assembles via tools/mips_asm.py to
# Resources/isoPatch/townCameraCollision.bin, embedded in the app; formerly pullin2.s) (reclaimed CheckCameraWidth slack; code may grow to 0x14C20C).
# Hooked by retargeting EdMoveChara's `jal CheckHitVertical` @0x16B5DC; we run the vanilla CheckHitVertical
# ourselves (args still live at entry) and return its verdict. Per frame: ceiling probe (tunnel duck), ground
# probe (floor guard), right-stick height, dist target = BASE_DIST (no pull-in), SWEPT-SLIDE from the persisted
# origin E_prev @0x14C210 (weighted d/h/θ resolution + |n_t|-scaled friction + θ reacquisition), occlusion-gated
# geometric climb h = REST_H + CLIMB_K·(BASE−d')², corner-verify second resolution. Tunables injected by
# IsoPatcher (PutVal/PutEase, auto-located slots).
# ⚠ R5900/EE rules (hard-won): c.OLT.s/sqrt.s/max.s/min.s are .word-encoded; nop after every mtc1 and FP compare;
# NEVER hold an FP reg across a jal (CheckHit tramples f0-f20); every branch path must set a0-a3 before its jal;
# ⚠⚠ args 5-8 of any call go in REGISTERS t0-t3 (EE ABI) — set t0/t1/t2 explicitly before EVERY CheckHit cast
# (stack stores at 0x10+(sp) are NOT read; stale t2 = the mask-skipping saga, see native-camera-functions memory).
addiu $sp, $sp, -0xa0         # frame 0xA0: keeps the 0x90-0x9C stash/spill slots INSIDE our own frame

sw    $ra, 0x50($sp)
lui   $at, 0x1d2
lw    $v1, -0x6988($at)
beq   $v1, $zero, done
nop
sw    $v1, 0x58($sp)
lwc1  $f0, 0x2c0($v1)
swc1  $f0, 0x20($sp)          # ref.x
lwc1  $f0, 0x2c4($v1)
swc1  $f0, 0x24($sp)          # ref.y
lwc1  $f0, 0x2c8($v1)
swc1  $f0, 0x28($sp)          # ref.z
sw    $zero, 0x2c($sp)        # ref.w := 0 — the quad is the LOS cast's `from` endpoint
lwc1  $f12, 0x2dc($v1)        # RENDERED angle (angS) — the pipeline basis (bisect step 3). Was angT (0x2d8):
                              #   the sweep then protected the TARGET's path while the rendered eye lagged on an
                              #   arc through corners. Δθ writes still land on angT — only the basis reads switch.
jal   0x11d8a0                # sin(angS)
nop
swc1  $f0, 0x60($sp)
lw    $v1, 0x58($sp)
lwc1  $f12, 0x2dc($v1)        # angS again for cos
jal   0x11d6b0                # cos(angS)
nop
swc1  $f0, 0x64($sp)
lw    $v1, 0x58($sp)
# ===== eye spot = ref + dist*dir, eye.y = ref.y + height  -> ceiling-probe origin @ sp+0x20 =====
lwc1  $f1, 0x2d0($v1)         # dist
lwc1  $f2, 0x60($sp)
mul.s $f2, $f2, $f1
lwc1  $f3, 0x20($sp)
add.s $f4, $f3, $f2           # camX
lwc1  $f2, 0x64($sp)
mul.s $f2, $f2, $f1
lwc1  $f3, 0x28($sp)
add.s $f5, $f3, $f2           # camZ
lwc1  $f6, 0x24($sp)          # ref.y
lwc1  $f7, 0x2d4($v1)         # height
add.s $f7, $f6, $f7           # eye.y
swc1  $f4, 0x20($sp)          # rayFrom = eye
swc1  $f7, 0x24($sp)
swc1  $f5, 0x28($sp)
sw    $zero, 0x2c($sp)
# ===== CEILING ray: eye -> up CEIL_DIST -> ceilingY @ sp+0x6c (huge sentinel if none) =====
swc1  $f4, 0x30($sp)          # rayTo.x = camX
lui   $t0, 0x42c8             # CEIL_DIST = 100
mtc1  $t0, $f1
nop
add.s $f0, $f7, $f1           # eye.y + 100
swc1  $f0, 0x34($sp)
swc1  $f5, 0x38($sp)          # rayTo.z = camZ
sw    $zero, 0x3c($sp)
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addiu $a2, $sp, 0x20
addiu $a3, $sp, 0x30
addiu $t0, $sp, 0x40          # hitOut  — EE ABI: CheckHit args 5-7 are REGISTERS t0/t1/t2 (the old
addiu $t1, $zero, 1           # mode=1  —   sw stores to 0x10/0x14/0x18 were NEVER read: cargo cult)
addu  $t2, $zero, $zero       # skip=0  — NEVER inherit: stale t2 bit0 skipped every mask-1 `_c` poly (h06!)
jal   0x149d50
nop
lw    $v1, 0x58($sp)
bltz  $v0, cnohit
nop
# (two-sided by design: CheckHit mode-1 returns the NEAREST surface above the eye = the true ceiling
#  regardless of winding — the old backface cull here was redundant and its 27 words fund the drift)
lwc1  $f0, 0x44($sp)          # ceilingY = hit.y
swc1  $f0, 0x6c($sp)
b     cdone
nop
cnohit:
lui   $t0, 0x4800             # 131072 sentinel -> ceiling clamp inert
mtc1  $t0, $f0
nop
swc1  $f0, 0x6c($sp)
cdone:
# ===== GROUND ray: eye (sp+0x20, still set) -> down 500 -> groundY @ sp+0x5c (0 = miss). TWO-SIDED: a floor safety
#       guard for stick-down, not an occlusion test, so it catches any floor regardless of winding. =====
lwc1  $f0, 0x20($sp)          # eye.x
swc1  $f0, 0x30($sp)          # rayTo.x
lwc1  $f0, 0x24($sp)          # eye.y
lui   $t0, 0x43fa             # 500
mtc1  $t0, $f1
nop
sub.s $f0, $f0, $f1           # eye.y - 500
swc1  $f0, 0x34($sp)
lwc1  $f0, 0x28($sp)          # eye.z
swc1  $f0, 0x38($sp)
sw    $zero, 0x3c($sp)
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addiu $a2, $sp, 0x20
addiu $a3, $sp, 0x30
addiu $t0, $sp, 0x40          # hitOut  — EE ABI: CheckHit args 5-7 are REGISTERS t0/t1/t2 (the old
addiu $t1, $zero, 1           # mode=1  —   sw stores to 0x10/0x14/0x18 were NEVER read: cargo cult)
addu  $t2, $zero, $zero       # skip=0  — NEVER inherit: stale t2 bit0 skipped every mask-1 `_c` poly (h06!)
jal   0x149d50
nop
lw    $v1, 0x58($sp)
bltz  $v0, gnohit
nop
lwc1  $f0, 0x44($sp)          # groundY = hit.y
swc1  $f0, 0x5c($sp)
b     gdone
nop
gnohit:
sw    $zero, 0x5c($sp)        # no ground -> 0 (clamp goes inert: height_min becomes very negative)
gdone:
# ===== RIGHT-STICK X: is the user steering? flag @sp+0x84 (1 = active) suppresses the reacquisition drift =====
jal   0x1699f0                # GetRXf (same keylock/calibration path as GetRYf)
nop
mfc1  $t2, $f0
nop
sll   $t2, $t2, 1             # drop the sign bit: |x| compare on raw float bits
lui   $t3, 0x7d00             # 0.25² threshold trick: 0x3E800000 (0.25) << 1
sltu  $t2, $t3, $t2           # 1 if |x| > 0.25
sw    $t2, 0x84($sp)
# ===== RIGHT-STICK Y -> smoothed manual height offset @ sp+0x68 (deadzone + scale, persistent ease) =====
jal   0x169a30                # GetRYf
nop
mov.s $f3, $f0
mul.s $f2, $f0, $f0           # stickY^2
lui   $t0, 0x3e23             # STICK_DZ2 (deadzone^2)
ori   $t0, $t0, 0xd70a
mtc1  $t0, $f1
nop
.word 0x46020834             # c.OLT.s f1,f2 : DZ2 < stickY^2 ?
nop
bc1f  skzero
nop
lui   $t0, 0xc1c8             # STICK_SCALE = -25 (flipped) at full deflection
mtc1  $t0, $f1
nop
mul.s $f0, $f3, $f1
b     skdone
nop
skzero:
mtc1  $zero, $f0
skdone:
lui   $t3, 0x01f1
ori   $t3, $t3, 0x0040        # smoothed-stick scratch @0x01F10040 (mailbox DATA page: scratch writes on the
                              #   old code page forced PCSX2 to re-JIT it every frame; boot-zeroed heap)
lwc1  $f2, 0x0($t3)
sub.s $f1, $f0, $f2
lui   $t0, 0x3da3             # STICK_EASE
ori   $t0, $t0, 0xd70a
mtc1  $t0, $f4
nop
mul.s $f1, $f1, $f4
add.s $f2, $f2, $f1
swc1  $f2, 0x0($t3)
swc1  $f2, 0x68($sp)
lw    $v1, 0x58($sp)          # reload camera (GetRYf clobbered caller-saved)
# restore ref @ sp+0x20/24/28 (ceiling ray used the eye as rayFrom)
lwc1  $f0, 0x2c0($v1)
swc1  $f0, 0x20($sp)
lwc1  $f0, 0x2c4($v1)
swc1  $f0, 0x24($sp)
lwc1  $f0, 0x2c8($v1)
swc1  $f0, 0x28($sp)
# ===== dist target = BASE_DIST (bisect step 5: pull-in REMOVED — no wall ray, no classifier, no margin/floor;
# the swept-slide owns ALL wall response) =====
lui   $t0, 0x42a0             # BASE_DIST (resting dist target)
mtc1  $t0, $f0
nop
# ===== HEIGHT target = REST_H + stick (bisect step 5: the smoothstep climb went with the pull-in — its input
# was the pull-in intrusion), ducked by ceiling, floored by ground =====
lui   $t0, 0x4188             # REST_H (flat baseline)
mtc1  $t0, $f5
nop
lwc1  $f8, 0x68($sp)          # stick offset
add.s $f5, $f5, $f8           # + stick
lwc1  $f6, 0x6c($sp)          # ceilingY
lwc1  $f7, 0x24($sp)          # ref.y
sub.s $f6, $f6, $f7
lui   $t0, 0x4160             # MIN_CEIL_CLEAR = 14
mtc1  $t0, $f7
nop
sub.s $f6, $f6, $f7           # height_max = (ceilingY − ref.y) − MIN_CEIL_CLEAR
swc1  $f6, 0x90($sp)          # stash h_max: the corner verify's post-push re-clamp needs it
.word 0x46053034             # c.OLT.s f6,f5 : height_max < height_target ?
nop
bc1f  cclampok
nop
mov.s $f5, $f6                # duck: clamp height down under the ceiling
cclampok:
# ground clamp: eye stays MIN_GROUND_CLEAR above the ground under the camera (stick-down floor guard)
lwc1  $f6, 0x5c($sp)          # groundY
lwc1  $f7, 0x24($sp)          # ref.y
sub.s $f6, $f6, $f7           # groundY − ref.y
lui   $t0, 0x40c0             # MIN_GROUND_CLEAR = 6
mtc1  $t0, $f7
nop
add.s $f6, $f6, $f7           # height_min = (groundY − ref.y) + MIN_GROUND_CLEAR
swc1  $f6, 0x8c($sp)          # stash h_min: the sweep's post-clamp re-floor needs it (inverted-slope contact)
swc1  $f5, 0x94($sp)          # stash the pre-clamp rest target (REST_H + stick) for the height sub's descent hold
.word 0x46062834             # c.OLT.s f5,f6 : height_target < height_min ?
nop
bc1f  gclampok
nop
mov.s $f5, $f6                # clamp up: never sink into the floor
gclampok:
# ===== eases: compute h_e/d_e but DO NOT store yet — the swept-slide constrains them first =====
lwc1  $f6, 0x2d4($v1)
sub.s $f7, $f5, $f6
lui   $t0, 0x3e99            # HEIGHT_EASE
ori   $t0, $t0, 0x999a
mtc1  $t0, $f3
nop
mul.s $f7, $f7, $f3
add.s $f6, $f6, $f7
# WORLD-SPACE HEIGHT (cave subroutine @0x27D090): absolute descent bound (the eye's WORLD y drops at most
# H_FALL_RATE/frame — a falling player outruns the camera), hard ground floor, and the pinned+occluded
# ground glide toward the player (adjusts the dist target @0x74). See tools/camera_height.s.
swc1  $f6, 0x70($sp)          # h_e in/out
swc1  $f0, 0x74($sp)          # dist target in/out
addu  $a1, $sp, $zero
jal   0x27d090
nop
lw    $v1, 0x58($sp)          # the sub clobbers v1
lwc1  $f6, 0x70($sp)
lwc1  $f0, 0x74($sp)
swc1  $f6, 0x68($sp)          # h_e (eased height target; slot free after stick was consumed)
lwc1  $f1, 0x2d0($v1)
lui   $t0, 0x3e19            # DIST_EASE
ori   $t0, $t0, 0x999a
mtc1  $t0, $f3
nop
sub.s $f2, $f0, $f1
mul.s $f2, $f2, $f3
add.s $f0, $f1, $f2
swc1  $f0, 0x5c($sp)          # d_e (eased dist target; groundY slot free after the clamp)
# ===== SWEPT-SLIDE v2: sweep from LAST FRAME'S constrained eye target (persisted @0x14C210) to this frame's E1.
# The old origin was rebuilt from THIS frame's ref, so the whole segment teleported with the player and follow/
# walk-driven crossings were invisible (both endpoints already past the wall). A persisted origin captures EVERY
# motion source: ref translation, follow angle, stick, dist, height. On a crossing: push E1 to SLIDE_MARGIN on the
# AUTHORED-normal side via the 3-axis control decomposition (dist/height/angle) — slide, not stop.
# E1 = ref + d_e·(sinT,_,cosT) + (0,h_e,0)
lwc1  $f1, 0x5c($sp)          # d_e
lwc1  $f2, 0x60($sp)          # sinT
mul.s $f2, $f2, $f1
lwc1  $f3, 0x2c0($v1)
add.s $f2, $f3, $f2
swc1  $f2, 0x30($sp)          # E1.x
lwc1  $f2, 0x2c4($v1)
lwc1  $f3, 0x68($sp)          # h_e
add.s $f2, $f2, $f3
swc1  $f2, 0x34($sp)          # E1.y
lwc1  $f2, 0x64($sp)          # cosT
mul.s $f2, $f2, $f1
lwc1  $f3, 0x2c8($v1)
add.s $f2, $f3, $f2
swc1  $f2, 0x38($sp)          # E1.z
sw    $zero, 0x3c($sp)        # E1.w := 0 — the stationary path casts this quad directly; CheckHit's tests are
                              #   scalar x/y/z (w unread today), this is insurance against future w consumers
# ===== OCCLUSION TEST (bisect step 7a, 5th cast): true LOS pivot → E_prev. The CLIMB runs only when the player is
# VISIBLE — rising while occluded (sliding around a building corner) just crests open-topped shells; occlusion is
# the reacquisition's job (slide around), not the climb's. Endpoint = E_prev, the CONSTRAINED eye — NOT the raw
# target E1: the ease overshoots the wall by >margin when pinned deep, false-flagging "occluded" and collapsing
# the climb. Raw hit index stashed @0x98: >= 0 means OCCLUDED. (args @0x10/0x14/0x18 persist from the ground cast)
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addiu $a2, $sp, 0x20          # from = pivot (ref)
lui   $a3, 0x01f1
ori   $a3, $a3, 0x0050        # to = E_prev @0x01F10050 (mailbox data page)
addiu $t0, $sp, 0x40          # hitOut — REAL scratch (the era t0=E_prev clobber made CheckHit WRITE the LOS hit
addiu $t1, $zero, 1           #   point INTO the persisted sweep origin every occluded frame = the h11 clip)
addu  $t2, $zero, $zero       # skip=0
jal   0x149d50
nop
sw    $v0, 0x54($sp)          # occlusion flag (raw hit idx; 0x54 free since the CHV passthrough left —
                              #   the old 0x98 slot is REUSED by the corner-verify spill = garbage flag)
lw    $v1, 0x58($sp)          # reload camera ptr (CheckHit tramples v1 — the sskip path needs it fresh)
sw    $zero, 0x3c($sp)
# E_prev @0x14C210 (16-aligned quad, w @+0xC). All-zero triple = never stored (patch zero-inits) -> skip.
# |E1 − E_prev|² > 16384 (128u jump) = teleport/area change -> skip (don't drag the camera across the map).
lui   $t3, 0x01f1
ori   $t3, $t3, 0x0050
lw    $t0, 0x0($t3)
lw    $t1, 0x4($t3)
lw    $t2, 0x8($t3)
or    $t0, $t0, $t1
or    $t0, $t0, $t2
beq   $t0, $zero, sskip
nop
lwc1  $f7, 0x0($t3)
lwc1  $f8, 0x30($sp)
sub.s $f7, $f7, $f8
mul.s $f9, $f7, $f7
lwc1  $f7, 0x4($t3)
lwc1  $f8, 0x34($sp)
sub.s $f7, $f7, $f8
mul.s $f7, $f7, $f7
add.s $f9, $f9, $f7
lwc1  $f7, 0x8($t3)
lwc1  $f8, 0x38($sp)
sub.s $f7, $f7, $f8
mul.s $f7, $f7, $f7
add.s $f9, $f9, $f7           # |E1 − E_prev|²
lui   $t0, 0x4780             # TELEPORT² = 65536 (= 256²) — bisect step 4: must exceed max legit stretch,
                              #   not just motion, or strained contacts vent the constraint at maximum strain
mtc1  $t0, $f8
nop
.word 0x46094034             # c.OLT.s f8,f9 : TELEPORT² < |Δ|² ?
nop
bc1t  sskip
nop
# PROXIMITY EXTENSION: extend the cast TIP past E1 by SLIDE_MARGIN along the motion, so the constraint fires within
# the margin BAND (small continuous corrections) instead of only on a plane crossing — the binary hit/miss was the
# shimmer against head-on walls. Tip @sp+0x70 (16-aligned quad); E1 @0x30 stays the point p/persist measure.
# Skip when nearly stationary (|Δ|² < 1 — direction too noisy, and a static eye needs no proximity push).
mfc1  $t0, $f9                # |Δ|² is non-negative -> raw float bits compare monotonically (int domain,
lui   $t2, 0x3f80             # 1.0 bits
slt   $t0, $t0, $t2
bne   $t0, $zero, snoext      # nearly stationary -> skip the extension
nop
.word 0x46090244             # sqrt.s f9,f9 : L = |E1 − E_prev|
nop
lui   $t0, 0x40e0             # SLIDE_MARGIN (extension reach; PutVal slot #1)
mtc1  $t0, $f8
nop
add.s $f8, $f9, $f8           # L + M
div.s $f8, $f8, $f9           # s = (L+M)/L
nop
nop
lwc1  $f7, 0x30($sp)
lwc1  $f1, 0x0($t3)
sub.s $f7, $f7, $f1
mul.s $f7, $f7, $f8
add.s $f7, $f1, $f7
swc1  $f7, 0x70($sp)          # tip.x
lwc1  $f7, 0x34($sp)
lwc1  $f1, 0x4($t3)
sub.s $f7, $f7, $f1
mul.s $f7, $f7, $f8
add.s $f7, $f1, $f7
swc1  $f7, 0x74($sp)          # tip.y
lwc1  $f7, 0x38($sp)
lwc1  $f1, 0x8($t3)
sub.s $f7, $f7, $f1
mul.s $f7, $f7, $f8
add.s $f7, $f1, $f7
swc1  $f7, 0x78($sp)          # tip.z
sw    $zero, 0x7c($sp)
addiu $a3, $sp, 0x70          # to = extended tip (E1 + margin reach)
b     sext2
nop
snoext:
addiu $a3, $sp, 0x30          # stationary: cast to E1 directly (its w @0x3c is already zeroed)
sext2:
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addu  $a2, $t3, $zero         # from = E_prev (persisted; the aligned scratch quad)
addiu $t0, $sp, 0x40          # hitOut  — EE ABI: CheckHit args 5-7 are REGISTERS t0/t1/t2 (the old
addiu $t1, $zero, 1           # mode=1  —   sw stores to 0x10/0x14/0x18 were NEVER read: cargo cult)
addu  $t2, $zero, $zero       # skip=0  — NEVER inherit: stale t2 bit0 skipped every mask-1 `_c` poly (h06!)
jal   0x149d50               # CheckHit E_prev->E1 (TWO-SIDED — no winding cull here by design)
nop
lw    $v1, 0x58($sp)
bltz  $v0, nocorr
nop
sll   $t1, $v0, 6
sll   $t2, $v0, 4
addu  $t1, $t1, $t2
addu  $t1, $s5, $t1           # hit poly base
lwc1  $f4, 0x30($t1)          # N.x
lwc1  $f5, 0x34($t1)          # N.y
lwc1  $f6, 0x38($t1)          # N.z
# normalize N (gathered CCPoly normals are NOT unit) so `need` is a true distance and the basis decomposition is exact
mul.s $f7, $f4, $f4
mul.s $f8, $f5, $f5
add.s $f7, $f7, $f8
mul.s $f8, $f6, $f6
add.s $f7, $f7, $f8           # N·N
.word 0x460701C4             # sqrt.s f7,f7 (R5900 ft-operand form)
nop
lui   $t0, 0x3f80             # 1.0
mtc1  $t0, $f8
nop
div.s $f8, $f8, $f7           # 1/|N|
nop
nop
mul.s $f4, $f4, $f8
mul.s $f5, $f5, $f8
mul.s $f6, $f6, $f8
# SIDE RULE: the eye must stay on the AUTHORED-normal side (all _c faces point into the play area). No flip — the
# safe side is a per-poly constant, so a penetrated eye can't switch the constraint's allegiance (the old E0-derived
# side did exactly that once the stick leaked the eye past the plane), and a wrong-side eye is pushed BACK (recovery).
# p = N̂·(E1 − P): signed height of E1 on the authored side (p <= 0 means at/behind the wall)
lwc1  $f7, 0x30($sp)
lwc1  $f8, 0x40($sp)
sub.s $f7, $f7, $f8
mul.s $f10, $f7, $f4
lwc1  $f7, 0x34($sp)
lwc1  $f8, 0x44($sp)
sub.s $f7, $f7, $f8
mul.s $f7, $f7, $f5
add.s $f10, $f10, $f7
lwc1  $f7, 0x38($sp)
lwc1  $f8, 0x48($sp)
sub.s $f7, $f7, $f8
mul.s $f7, $f7, $f6
add.s $f10, $f10, $f7         # p
lui   $t0, 0x40e0             # SLIDE_MARGIN (need standoff; PutVal slot #2 — keep <= pull-in MARGIN or the two setpoints oscillate)
mtc1  $t0, $f11
nop
sub.s $f11, $f11, $f10        # need = SLIDE_MARGIN − p
mtc1  $zero, $f10
nop
.word 0x460B5034             # c.OLT.s f10,f11 : 0 < need ?
nop
bc1f  nocorr
nop
# WEIGHTED control-space slide (free glide): decompose N̂ on the orthonormal basis boom/vertical/orbit-tangent, but
# DOWN-WEIGHT the angle axis by SLIDE_BIAS=B2 so dist/height do the resolving and the user's/follow's rotation is
# barely resisted — the eye glides along oblique walls (pulls in / backs off), instead of the min-norm push that
# cancelled part of the rotation every frame (the "constrained" feel). Weighted min-norm:
#   k = need/(n_d² + n_h² + B2·n_t²);  Δd = k·n_d,  Δh = k·n_h,  Δθ = k·B2·n_t/d.
# B2=1 -> old neutral behavior; small B2 -> free slide. Tangential walls (n_d,n_h≈0) still resolve FULLY through
# the angle (B2 cancels in that limit). L2w ∈ [B2, 1] since |N̂|=1 — never zero, no guard needed.
lwc1  $f7, 0x60($sp)          # sinT
mul.s $f7, $f7, $f4
lwc1  $f8, 0x64($sp)          # cosT
mul.s $f8, $f8, $f6
add.s $f7, $f7, $f8           # n_d = N.x·sinT + N.z·cosT
swc1  $f7, 0x88($sp)          # stash n_d for the reacquisition slide
lwc1  $f8, 0x64($sp)
mul.s $f8, $f8, $f4
lwc1  $f9, 0x60($sp)
mul.s $f9, $f9, $f6
sub.s $f12, $f8, $f9          # n_t = N.x·cosT − N.z·sinT
mul.s $f8, $f7, $f7           # n_d²
mul.s $f9, $f5, $f5           # n_h²
add.s $f8, $f8, $f9
mul.s $f9, $f12, $f12         # n_t²
lui   $t0, 0x3d80             # SLIDE_BIAS B2 (angle-axis weight²; PutEase)
ori   $t0, $t0, 0x0000
mtc1  $t0, $f10
nop
mul.s $f9, $f9, $f10
add.s $f8, $f8, $f9           # L2w = n_d² + n_h² + B2·n_t²
div.s $f11, $f11, $f8         # k = need / L2w
nop
nop
mul.s $f4, $f11, $f7          # a = k·n_d       (dist push; N.x no longer needed)
mul.s $f5, $f11, $f5          # c = k·n_h       (height push)
mul.s $f6, $f11, $f12
mul.s $f6, $f6, $f10          # b = k·B2·n_t    (tangent push = Δθ·d; N.z no longer needed)
lwc1  $f0, 0x5c($sp)          # d_e
add.s $f0, $f0, $f4           # d' = d_e + a
lwc1  $f2, 0x68($sp)          # h_e
add.s $f2, $f2, $f5           # h' = h_e + c
lwc1  $f9, 0x5c($sp)          # d_e = the angle's lever arm
div.s $f1, $f6, $f9           # Δθ = b / d_e
nop
nop
lwc1  $f3, 0x2d8($v1)
add.s $f3, $f3, $f1
swc1  $f3, 0x2d8($v1)         # nudge the TARGET angle out along the wall (the follow re-bases it next frame)
# FRICTION (contact frames only): damp the target angle's LEAD over the rendered angle so the slide along the wall
# slows while touching it (and target churn on head-on walls is damped). Lead wrapped to [−π,π] before scaling.
lwc1  $f7, 0x2dc($v1)         # angS (rendered)
sub.s $f8, $f3, $f7           # lead = angT' − angS
lui   $t0, 0x4049
ori   $t0, $t0, 0x0fdb
mtc1  $t0, $f9                # π
nop
.word 0x46084834             # c.OLT.s f9,f8 : π < lead ?
nop
bc1f  fw1
nop
lui   $t0, 0x40c9
ori   $t0, $t0, 0x0fdb
mtc1  $t0, $f10               # 2π
nop
sub.s $f8, $f8, $f10
fw1:
neg.s $f10, $f9               # −π
.word 0x460A4034             # c.OLT.s f8,f10 : lead < −π ?
nop
bc1f  fw2
nop
lui   $t0, 0x40c9
ori   $t0, $t0, 0x0fdb
mtc1  $t0, $f10
nop
add.s $f8, $f8, $f10
fw2:
lui   $t0, 0x3ecc             # SLIDE_FRICTION_INV = 1−F (PutEase, derived in C#; 0 = frictionless)
ori   $t0, $t0, 0xcccd
mtc1  $t0, $f10
nop
abs.s $f11, $f12              # |n_t| — how tangential the contact is
mul.s $f10, $f10, $f11        # (1−F)·|n_t|
mul.s $f10, $f10, $f8         # · lead
sub.s $f8, $f8, $f10          # damped lead = (1 − (1−F)·|n_t|)·lead: head-on ≈ undamped, slide-along = full drag
add.s $f3, $f7, $f8           # angT'' = angS + keep·lead
swc1  $f3, 0x2d8($v1)
# ===== θ REACQUISITION (bisect step 8, reworked: contact frames, stick idle): horizontal restoring pull toward
# the RESTING DISTANCE projected onto the wall tangent — slide around toward rest; the rotation EMERGES as
# displacement / lever arm (Δθ = tangential motion / d). No direct d' slide (the old S_d boom component is gone);
# the tangential displacement is accumulated into b so the persisted origin stays TRUTHFUL.
lw    $t0, 0x84($sp)
bne   $t0, $zero, drfx        # user steering -> no auto-slide
nop
lui   $t0, 0x42a0             # BASE_DIST (the resting distance)
mtc1  $t0, $f7
nop
sub.s $f7, $f7, $f0           # W_d = BASE − d'
lwc1  $f9, 0x88($sp)          # n_d (wall normal's boom component)
mul.s $f11, $f7, $f9          # dot = W_d·n_d  (the pull's into-wall part)
lui   $t0, 0x3d00             # SLIDE_GAIN (PutVal; fraction of the projected pull applied per frame)
mtc1  $t0, $f13
nop
mul.s $f3, $f11, $f12         # dot·n_t
neg.s $f3, $f3                # S_t = −dot·n_t (tangent-plane projection, orbit part)
mul.s $f3, $f3, $f13
add.s $f6, $f6, $f3           # b += (truthful origin; tangent displacement = Δθ·d)
div.s $f3, $f3, $f0           # Δθ = gain·S_t / d — the rotation emerges from the slide
lwc1  $f9, 0x2d8($v1)
add.s $f9, $f9, $f3
swc1  $f9, 0x2d8($v1)
drfx:
# ===== GEOMETRIC HEIGHT (bisect step 7b: the proven bell curve, now OCCLUSION-GATED; NO rim backstop), gentle-onset
# curve, purely relative to geometry: h = REST_H + CLIMB_K·(BASE − d')² — zero slope at touch, steepening toward
# the pinch. Occluded (LOS hit) → no rise: intrusion forced negative so the zero-clamp kills the climb.
# Ground floor and ceiling duck have the last word. Contact frames only (this is the corr path).
lui   $t0, 0x42a0             # BASE_DIST — intrusion reference
mtc1  $t0, $f7
nop
sub.s $f7, $f7, $f0           # intrusion = BASE − d'
lw    $t0, 0x54($sp)          # occlusion (raw LOS hit index)
bltz  $t0, hclamp0            # player VISIBLE → keep the real intrusion
nop
mtc1  $zero, $f7              # OCCLUDED → intrusion := 0 → no rise (climb only when clear)
nop
hclamp0:
mtc1  $zero, $f8
nop
.word 0x460839E8             # max.s f7,f7,f8 — stretched (d' > BASE) or occluded → zero climb
mul.s $f7, $f7, $f7           # intrusion²
lui   $t0, 0x3c0c             # CLIMB_K (PutEase; (CLIMB_PEAK−REST_H)/BASE² — quadratic gain, zero slope at touch)
ori   $t0, $t0, 0xcccd
mtc1  $t0, $f8
nop
mul.s $f7, $f7, $f8           # rise = K·intrusion²
lui   $t0, 0x4188             # REST_H (climb-curve base)
mtc1  $t0, $f8
nop
add.s $f7, $f7, $f8           # h_curve = REST_H + rise — gentle onset → a BELL over a wall traversal
lwc1  $f8, 0x8c($sp)          # ground floor
.word 0x460839E8             # max.s f7,f7,f8
lwc1  $f8, 0x90($sp)          # ceiling duck
.word 0x460839E9             # min.s f7,f7,f8
lwc1  $f8, 0x68($sp)          # h_e
lui   $t0, 0x4000             # CLIMB_RISE (PutVal; max climb rise per frame — the instant snap-up made the
mtc1  $t0, $f9                #   height SAWTOOTH when a grazing LOS flickered the gate near walls: snap up
nop                           #   on visible frames, ease down on occluded ones)
lwc1  $f3, 0x2d4($v1)         # LAST frame's APPLIED height — anchoring the cap to the eased h_e made the
add.s $f9, $f3, $f9           #   ease-decay eat the rise (equilibrium ~12 units); anchored here the climb
                              #   compounds CLIMB_RISE/frame to the full curve
.word 0x460939e9             # min.s f7,f7,f9 — rate-limit the rise
.word 0x460839E8             # max.s f7,f7,f8 — the climb may only RAISE the eye: on a tall-cliff descent the
                              #   curve (<=CLIMB_PEAK) sat far BELOW the eased height and the old unconditional
                              #   override snapped the eye 100+ units down through the cliff face in one frame;
                              #   descents now always go through the height ease
sub.s $f5, $f7, $f8           # c := total vertical displacement (truthful origin — persist uses c)
mov.s $f2, $f7                # h' = the curve height, floored at h_e
# ===== CORNER VERIFY + SECOND RESOLUTION (sequential impulse): the main pass resolves ONE plane; at a corner the
# corrected/slid target can cross the OTHER face (same or different _c mesh — the buffer holds them all). Cast OLD
# origin → FINAL target; on a hit, RESOLVE that plane too (pure min-norm de-penetration, no bias/slide) so both
# corner faces are handled in the same frame and the wedge emerges at the seam. (A revert-instead was tried and
# made it WORSE: reverting d/h/θ freezes the RELATIVE config, so pivot motion drags the eye through the seam.)
lwc1  $f7, 0x60($sp)          # sinT
mul.s $f7, $f7, $f0
lwc1  $f8, 0x20($sp)          # ref.x
add.s $f7, $f8, $f7
swc1  $f7, 0x70($sp)          # F.x
lwc1  $f8, 0x24($sp)          # ref.y
add.s $f8, $f8, $f2
swc1  $f8, 0x74($sp)          # F.y
lwc1  $f7, 0x64($sp)          # cosT
mul.s $f7, $f7, $f0
lwc1  $f8, 0x28($sp)          # ref.z
add.s $f7, $f8, $f7
swc1  $f7, 0x78($sp)          # F.z
sw    $zero, 0x7c($sp)
# ⚠ SPILL across the call: f0=d', f2=h', f4/f5/f6=a/c/b are CALLER-SAVED and CheckHit tramples them — unspilled,
# every contact frame stored garbage dist/height and persisted a garbage origin (the drastic-movement bug).
swc1  $f0, 0x94($sp)
swc1  $f2, 0x98($sp)
swc1  $f4, 0x9c($sp)
swc1  $f5, 0x80($sp)
swc1  $f6, 0x88($sp)
lui   $t3, 0x01f1
ori   $t3, $t3, 0x0050
addu  $a0, $s5, $zero
addu  $a1, $s8, $zero
addu  $a2, $t3, $zero         # from = OLD E_prev (not yet updated)
addiu $a3, $sp, 0x70          # to = final target
addiu $t0, $sp, 0x40          # hitOut
addiu $t1, $zero, 1           # mode=1
addu  $t2, $zero, $zero       # skip=0
jal   0x149d50
nop
lw    $v1, 0x58($sp)
lwc1  $f0, 0x94($sp)
lwc1  $f2, 0x98($sp)
lwc1  $f4, 0x9c($sp)
lwc1  $f5, 0x80($sp)
lwc1  $f6, 0x88($sp)
bltz  $v0, vok                # clear → commit the frame
nop
sll   $t1, $v0, 6
sll   $t2, $v0, 4
addu  $t1, $t1, $t2
addu  $t1, $s5, $t1           # verify-hit poly
lwc1  $f7, 0x30($t1)          # N2 (unnormalized)
lwc1  $f8, 0x34($t1)
lwc1  $f9, 0x38($t1)
mul.s $f10, $f7, $f7
mul.s $f11, $f8, $f8
add.s $f10, $f10, $f11
mul.s $f11, $f9, $f9
add.s $f10, $f10, $f11        # |N2|²
.word 0x460A0284             # sqrt.s f10,f10
nop
lui   $t0, 0x3f80             # 1.0
mtc1  $t0, $f11
nop
div.s $f11, $f11, $f10        # 1/|N2|
mul.s $f7, $f7, $f11
mul.s $f8, $f8, $f11
mul.s $f9, $f9, $f11          # N̂2
lwc1  $f10, 0x70($sp)         # p2 = N̂2·(F − P2)
lwc1  $f11, 0x40($sp)
sub.s $f10, $f10, $f11
mul.s $f10, $f10, $f7
lwc1  $f3, 0x74($sp)
lwc1  $f11, 0x44($sp)
sub.s $f3, $f3, $f11
mul.s $f3, $f3, $f8
add.s $f10, $f10, $f3
lwc1  $f3, 0x78($sp)
lwc1  $f11, 0x48($sp)
sub.s $f3, $f3, $f11
mul.s $f3, $f3, $f9
add.s $f10, $f10, $f3         # p2 (signed height on the authored side)
lui   $t0, 0x40e0             # SLIDE_MARGIN (slot #3)
mtc1  $t0, $f11
nop
sub.s $f11, $f11, $f10        # need2 = margin − p2
mfc1  $t0, $f11               # float sign/int test
nop
blez  $t0, vok
nop
lwc1  $f10, 0x60($sp)         # n_d2 = N̂2.x·sinT + N̂2.z·cosT
mul.s $f10, $f10, $f7
lwc1  $f3, 0x64($sp)
mul.s $f3, $f3, $f9
add.s $f10, $f10, $f3
mul.s $f3, $f11, $f10         # Δd = need2·n_d2
add.s $f0, $f0, $f3
add.s $f4, $f4, $f3           # a += (truthful origin)
mul.s $f3, $f11, $f8          # Δh = need2·n_h2
add.s $f2, $f2, $f3
add.s $f5, $f5, $f3           # c +=
lwc1  $f10, 0x64($sp)         # n_t2 = N̂2.x·cosT − N̂2.z·sinT
mul.s $f10, $f10, $f7
lwc1  $f3, 0x60($sp)
mul.s $f3, $f3, $f9
sub.s $f10, $f10, $f3
mul.s $f3, $f11, $f10         # Δt = need2·n_t2
add.s $f6, $f6, $f3           # b +=
div.s $f3, $f3, $f0           # Δθ = Δt / d
nop
lwc1  $f10, 0x2d8($v1)
add.s $f3, $f10, $f3
swc1  $f3, 0x2d8($v1)
lwc1  $f3, 0x8c($sp)          # re-clamp after the second push: ground floor…
.word 0x460310A8             # max.s f2,f2,f3
lwc1  $f3, 0x90($sp)          # …and ceiling duck
.word 0x460310A9             # min.s f2,f2,f3
vok:
# persist E1' = E1 + a·b̂ + c·ŷ + b·t̂ — the ACTUAL world displacement incl. the tangential slide — as next frame's
# sweep origin (sits at the margin -> a target still pushed inward crosses EVERY frame -> continuous, no jitter)
lui   $t3, 0x01f1
ori   $t3, $t3, 0x0050
lwc1  $f7, 0x60($sp)          # sinT
lwc1  $f8, 0x64($sp)          # cosT
mul.s $f1, $f4, $f7           # a·sinT
mul.s $f3, $f6, $f8           # b·cosT
add.s $f1, $f1, $f3
lwc1  $f3, 0x30($sp)
add.s $f1, $f3, $f1
swc1  $f1, 0x0($t3)           # E1'.x
lwc1  $f3, 0x34($sp)
add.s $f3, $f3, $f5
swc1  $f3, 0x4($t3)           # E1'.y
mul.s $f1, $f4, $f8           # a·cosT
mul.s $f3, $f6, $f7           # b·sinT
sub.s $f1, $f1, $f3
lwc1  $f3, 0x38($sp)
add.s $f1, $f3, $f1
swc1  $f1, 0x8($t3)           # E1'.z (E_prev.w @0xc is left untouched — zero-inited once, never written:
                              #   fewer per-frame writes on this recompiled-code page, and CheckHit's endpoint
                              #   tests are scalar x/y/z)
b     sfin
nop
sskip:
nocorr:
# no constraint this frame: persist raw E1 as the next sweep origin, targets unchanged
lui   $t3, 0x01f1
ori   $t3, $t3, 0x0050
lwc1  $f7, 0x30($sp)
swc1  $f7, 0x0($t3)
lwc1  $f7, 0x34($sp)
swc1  $f7, 0x4($t3)
lwc1  $f7, 0x38($sp)
swc1  $f7, 0x8($t3)
lwc1  $f0, 0x5c($sp)
lwc1  $f2, 0x68($sp)
sfin:
swc1  $f0, 0x2d0($v1)
swc1  $f2, 0x2d4($v1)
done:
addiu $v0, $zero, -1          # report no-hit to the (NOP'd) vertical-adjust branch — the vanilla
                              #   CheckHitVertical passthrough was removed (its branch body is fully dead)
lw    $ra, 0x50($sp)
jr    $ra
addiu $sp, $sp, 0xa0
