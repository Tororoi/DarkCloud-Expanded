# Divine Beast cat — pellet catcher, follower, fall and run (v3, 2026-09-10), the mod's ELF cave segment
# @0x1FB0D90 (ElfCave.CatPelletFollow).
#
# WHY NATIVE: the charged shot's cat must appear ON THE FRAME its pellet is born, grow over a handful of frames with
# its head on the pellet, break away at full size into a ballistic fall that keeps the pellet's forward speed, stop
# dead on the landing frame, and run at half the pellet's speed — all frame-exact, which a PINE thread cannot be
# (user: "fast action that requires precision — make it engine-driven"). This cave takes the dungeon step loop's
# once-per-frame `jal step__5CSHOT` (dun 0x1DB874C, a0 = the player shot pool), performs that call, then runs the
# state machine below on chara slot 1 (the cat copy, resident and hidden until bound). The mod arms it, times the
# landing pause (state 5 → 6) and ends the run; everything per-frame is here.
#
# STATES (Mailbox.CatState +0x98):
#   0 idle   3 waiting (armed: bind the next NEW pellet)   1 following the pellet (growing)   2 pellet ended early
#   4 falling (broke away at full size)   5 landing (land clip playing; momentum until the paws touch)   6 running
#   10 ready (in place)   7 take-off (in place until CatRampStart, then forward momentum ramps to full by CatRampEnd)
#   8 ground leap (full momentum, leap clip; ends into 5 — the clips carry the height)   9 hit (frozen; mod deals damage)
#   Flying targets (target height − floor > CatFlyThreshold): straight from the ready into the ballistic arc of state 4
#   (vh = Δh/T + g·T/2) in the float-up pose (CatFloatKey, +0x154) until the apex, the fall pose after — the town
#   ladder jump's ready → vertical leap → fall → land sequence.
# MAILBOX (the cat's own block at guest 0x01FB4000 + the offsets below — CodeCaveAddresses.Mailbox.CatBase; the
# offsets 0x94.. are historical: the words FIRST sat in the PNACH mailbox page, whose 0x100+ span turned out to be
# the AI-stub table (2026-09-11: clobbered clip frames, range, hit slot). $t0 = 0x01FB0000, offsets 0x40xx.):
#   +0x94 CatPelletSlot int  bound pellet slot + 1 (0 = none; page boots zero-filled)      +0x9C CatGrowFrames int
#   +0xA0 CatGrowInv float 1/N (mod)   +0xA4/+0xA8/+0xAC CatHeadX/H/Z float head rest offset, CAT space (mod)
#   +0xB0 CatSeenMask int (cave)   +0xB4/+0xB8/+0xBC CatVx/Vh/Vz float fall velocity (cave, captured at breakaway)
#   +0xC0 CatGravity float (mod)   +0xC4 CatFloorH float landing height (mod)   +0xC8 CatRunSpeed float = ½|v| (cave)
#   +0xCC CatTargetPtr uint guest address of the target's position vector (x @+0, z @+8) or 0 (mod)
#   +0xD0/+0xD4 CatDirX/Z float run direction, unit (cave; the mod turns the cat to face it)   +0xD8 CatGrowN int (mod)
#   +0xDC CatLandStopFrame float clip frame where the paws touch (momentum stops; 219)   +0xE0 CatLandEndFrame float
#   clip end (227 → run)   +0xE4 CatPrevFrame float the frame seen last time (wrap detection)   +0xE8 CatLandLead float
#   frames before the predicted touchdown at which the land clip starts ((219−215)/clip speed) — the copy's live motion
#   frame is read through slot 1's channel pointer (+0xC20 → MOTION_TYPE, frame @+0x10). A key switch with the motion
#   flags restart bit CLEAR is cross-faded by the engine (SetMotionEX 0x148D00: the outgoing clip freezes at its frame
#   and every bone slerps to the new key's first frame over 1/increment steps; increment = MOTION_STATE+0x08, seeded 0.1
#   by the .chr loader → 10 steps). The live frame stays at the OUTGOING clip's until the fade completes — which is why
#   every clip test below ignores frames outside its own range. The restart bit (+0xC64 |= 4) makes it a hard cut to the
#   key's first frame; it is used at the seams of the one authored jump (take-off 204 → leap 205, leap 214 → land 215,
#   where a fade would freeze the cat mid-air for 10 steps between two near-identical poses) and at fall → land, whose
#   lead-in is tuned for an instant start. Every other switch (land → walk, walk → take-off/ready/sit, sit → walk,
#   ready → float-up, float-up → fall) fades (2026-09-11).
#   +0xEC CatMoveKey int the key played while moving after the landing (mod: the brisk walk, 70).
#   +0xF0 CatMoveFrac float ground speed after the landing as a fraction of the pellet's horizontal speed (mod);
#   +0x194 CatMoveAbs float overrides it with an absolute units/frame when > 0 (mod).
#   +0x198 CatLeapTravel float momentum-frames from the leap's start to the paws-touch frame (mod, ≈29): at the take-off's
#   end the leap's momentum is re-sized to the target's LIVE distance ÷ this, and the take-off ramp re-aims every frame.
#   +0x19C CatHitSphere int the enemy body sphere the touch test met (cave → mod: the hit entry is planted on it).
#   +0x1A0 CatSitKey int the sit clip's key: with no target the cat sits in place instead of walking straight (mod, 72).
#   (+0x1A4/+0x1A8/+0x1AC and the take-off words +0x124/+0x134..+0x148/+0x164..+0x16C are unused since the take-off path
#   was removed: every pounce is the ready crouch + float-up vertical leap, user 2026-09-11.)
#   +0x1B0 CatFloatLaunch float the float-up frame where the feet leave the ground (mod, 297): state 11 stands in the
#   float-up's wind-up turning to the target until then, and the vertical leap is computed THERE   +0x1B4 CatFloatStart.
#   +0x1B8 CatFloatRate float (raw) the float-up's motion-speed override (mod)   +0x1BC CatFallBlend float the channel's
#   blend increment (MOTION_STATE+0x08) for the float-up → fall fade (mod, 1/steps)   +0x1C0 CatBlendDefault float 0.1,
#   put back when the land clip starts (mod).
#   +0x1C4 CatHitEntry int the damage entry the cave planted, index + 1 (cave → mod: consumed by CheckDmg = the hit
#   landed → fade; expired = it never connected → the mod clears the latch)   +0x1CC CatHitDamage int (mod: pellet +
#   attack)   +0x1D0 CatHitAttr int element attribute (mod)   +0x1D4/+0x1D8 CatKickStrength/Decay float (mod).
#   (The stagger is the engine's own: ELF 0x1DB410 (CheckDmg) → ElfCave.XiaoMeleeFlinch lets a Xiao-owned entry with a
#   melee-type kick take the normal flinch decision. +0x128/+0x12C/+0x19C — the old touch radius/slot/sphere — unused.)
#   +0x1DC CatHeadNode uint the copy's cat_kao frame (mod, at spawn): the contact point = its posed world position.
#   +0x1E0 CatFallBlendFrames float the float-up → fall fade length in steps (mod): the switch is timed so the fade ends
#   as the land clip starts.
#   +0x1C8 CatHitLatch int 1 after the first touch of a flight (cave): the hit lands once; the flight follows through.
#   +0x11C CatPounceRange float (mod)  +0x120 CatPounceFrames float leap flight frames (mod)  +0x124 CatTakeoffEnd float
#   take-off clip end frame (mod, 204)  +0x128 CatHitRadius float the cat's touch radius (mod)  +0x12C CatHitSlot int
#   enemy slot + 1 the cat touched (cave → mod; 0 none). +0x130 CatReadyEnd / +0x134 CatRampStart / +0x138 CatRampInv
#   (1/(rampEnd−rampStart)) / +0x13C CatRampEnd / +0x140 CatLeapEnd float clip frames (mod); +0x144 CatPounceTravel
#   float momentum-frames the pounce covers (V = dist / travel) (mod); +0x148 CatFlyThreshold float (mod);
#   +0x16C CatPounceMaxDist float: at the end of the ready the pounce launches only if the target is still within this
#   (mod, 2× the range) — the mod may have re-targeted during the ready clip, and a far target means walk, not leap.
#   +0x14C CatPounceV float (cave); +0x150 CatPounceFly int (cave); +0x158/+0x15C CatDbgDist/CatDbgRange float the
#   distance and range compared when the pounce was decided (cave, diagnostics); +0x160 CatReadyStart / +0x164
#   CatTakeoffStart / +0x168 CatLeapStart float clip start frames (mod): a clip-end test only trusts a frame inside
#   [start, end+1] — on a state's first frame the live frame still belongs to the PREVIOUS clip. The touch test
#   (states 1/4/5/8) uses
#   the pellet code's own enemy body-sphere table (*NowMonstorUnit: enemy i active @i*400+0x1E3D0 != −1 and
#   @+0x1E4A4 != 0; spheres @i*0x510 + 0x55250 + j*0x10, radius +0x55390 + j*4, active +0x55450 + j*4) against the
#   root and a point 3 ahead, both 2 above the root.
#   +0xF4 CatProbeUp / +0xF8 CatProbeDown float the floor probe's reach above / below the cat (mod, 8 / 40).
#   +0x104 CatRateBase / +0x108 CatRatePerSpeed / +0x10C CatRateMax float: while moving the cave writes slot +0xC60
#   (motion-speed override) = min(base + perSpeed · ground speed, max) — the TOWN's own walk mapping (EdMoveChara:
#   rate = 0.8·(0.2 + stick), ground = 1.6·stick → rate = 0.16 + 0.5·ground, cap 0.85). It is not planted feet (the
#   walk clip's real stride is 0.196 u/clip-frame — a planted-feet rate ran 5× too fast to the eye); it is the ratio
#   the designers tuned for this very rig in town. +0x110 reserved; +0x114 CatIdleKey int stand key; +0x118
#   CatBlocked int (cave: 1 while a wall stops it → idle key and −1.0, the KEY's own rate).
#   +0xFC CatProbeFront / +0x100 CatProbeBack float how far ahead / behind the root (along CatDirX/Z) the extra
#   floor casts land — the cat stands on the HIGHEST of root / front / back, so a long body on stairs rides on its
#   uphill end instead of sinking into the next step (mod, 3 / 2).
#   CatFloorH (+0xC4) is refreshed every frame of states 4/5/6 by the floor probe below (real ground, not Xiao's).
# Chara slot 1 (CNPCharacter @guest 0x01EA9900): pos +0x10/+0x14/+0x18, scale +0x90..+0x98, motion key +0xC68
# (68 leap / 69 land / 66 run — the cave writes it while it owns the cat), opacity +0xCEC (float 0..128).
# Shot pool (*0x002A35D4): pos +0x40+slot*0x10, vel +0x1C0+slot*0x10, nocollide +0x280+slot*4, lifetime +0x2B0+slot*4,
# sprite scale +0x310+slot*4 (draw only), active +0x3D0+slot*4.
#
# ⚠ EE rules (tools/lib/mips_asm.py): c.lt.s and sqrt.s hand-encoded (.word), ≥1 insn between a compare and its
# bc1x, ≥3 insns between an mtc1 and a dependent FP op. Clobbers t0-t9, f0-f20 (caller-saved at this site: the next
# vanilla insn rebuilds v0/a0). ra kept on the stack; s-regs and gp untouched.
# Frame: 0x80 — sp+0x00..0x0F left free for callees' argument spills; s0/s1/s2 saved FULL WIDTH (sq, as every
# engine prologue does — the caller may hold 64/128-bit values in them) @0x10/0x20/0x30, ra @0x40, and the floor
# probe's 16-B-aligned vectors: from @0x50, to @0x60, hit @0x70.
addiu $sp, $sp, -0x80
.word 0x7FB00010               # sq $s0, 0x10($sp)
.word 0x7FB10020               # sq $s1, 0x20($sp)
.word 0x7FB20030               # sq $s2, 0x30($sp)
sw    $ra, 0x40($sp)
jal   0x001ABD10               # step__5CSHOT(pool) — the displaced vanilla call (a0 still the pool)
nop
lui   $t0, 0x01FB              # mailbox page
lui   $t2, 0x002A
lw    $t2, 0x35D4($t2)         # pool base
lw    $t9, 0x40B0($t0)         # seen mask (previous frame)
move  $t1, $zero               # i
move  $t7, $zero               # new mask
addiu $t8, $zero, -1           # first NEW slot this frame (-1 = none)
scan:
sll   $t3, $t1, 2
addu  $t4, $t2, $t3
lw    $t5, 0x03D0($t4)         # active flag
beq   $t5, $zero, next
nop
addiu $t6, $zero, 1
sllv  $t6, $t6, $t1            # bit i
or    $t7, $t7, $t6
and   $t6, $t9, $t6            # seen last frame?
bne   $t6, $zero, next
nop
bgez  $t8, next                # already have a new one
nop
move  $t8, $t1
next:
addiu $t1, $t1, 1
slti  $t3, $t1, 12
bne   $t3, $zero, scan
nop
sw    $t7, 0x40B0($t0)         # seen mask ← this frame
lw    $t3, 0x4098($t0)         # state
addiu $t4, $zero, 4
beq   $t3, $t4, probe          # states 4/5/6 first refresh the floor height under the cat, then run their block
addiu $t4, $zero, 6
beq   $t3, $t4, probe
addiu $t4, $zero, 5
beq   $t3, $t4, probe
addiu $t4, $zero, 10
beq   $t3, $t4, ready
addiu $t4, $zero, 11
beq   $t3, $t4, floatwait
addiu $t4, $zero, 3
bne   $t3, $t4, notwaiting
nop
bltz  $t8, done                # waiting, no new pellet yet
nop
addiu $t5, $t8, 1              # ── BIND on the birth frame ──
sw    $t5, 0x4094($t0)         # slot + 1
sw    $zero, 0x409C($t0)       # frames = 0
sll   $t3, $t8, 2
addu  $t4, $t2, $t3
addiu $t5, $zero, 1
sw    $t5, 0x0280($t4)         # the pellet passes through everything now: the cat's touch is the hit
addiu $t5, $zero, 1
sw    $t5, 0x4098($t0)         # state = following
lui   $t6, 0x01EA
ori   $t6, $t6, 0x9900         # chara slot 1
lui   $t5, 0x4300              # 128.0f
sw    $t5, 0x0CEC($t6)         # opacity = 128: visible from this very frame
b     follow
move  $t1, $t8                 # (delay) t1 = slot
notwaiting:
addiu $t4, $zero, 1
bne   $t3, $t4, done           # only state 1 follows
nop
lw    $t1, 0x4094($t0)
beq   $t1, $zero, done
addiu $t1, $t1, -1             # → zero-based slot (delay slot; dead when the branch is taken)
follow:
sll   $t3, $t1, 2
addu  $t4, $t2, $t3            # scalar row
lw    $t5, 0x03D0($t4)         # pellet still active?
bne   $t5, $zero, alive
nop
sw    $zero, 0x4094($t0)       # pellet ended early: unbind …
addiu $t6, $zero, 2
b     done
sw    $t6, 0x4098($t0)         # … and tell the mod (delay slot)
alive:
lw    $t8, 0x409C($t0)         # frames since bound (t8 kept for the breakaway test)
addiu $t7, $t8, 1
sw    $t7, 0x409C($t0)
mtc1  $t8, $f0
lwc1  $f2, 0x40A0($t0)         # 1 / N
lui   $t7, 0x3F80
mtc1  $t7, $f4                 # 1.0
mtc1  $zero, $f20              # 0.0
cvt.s.w $f0, $f0               # frames as float (4 insns after its mtc1)
mul.s $f0, $f0, $f2            # k = frames / N
nop
.word 0x46002034               # c.lt.s $f4, $f0   (1.0 < k ?)  fs=f4 ft=f0
nop
bc1f  kok
nop
mov.s $f0, $f4                 # k = 1.0 (fully grown)
kok:
sll   $t3, $t1, 4
addu  $t9, $t2, $t3            # pellet vec row (pos +0x40, vel +0x1C0) — kept in t9
lwc1  $f6, 0x01C0($t9)         # vx
lwc1  $f8, 0x01C8($t9)         # vz (the pool's y)
mul.s $f10, $f6, $f6
mul.s $f12, $f8, $f8
add.s $f10, $f10, $f12         # h²
.word 0x460A0284               # sqrt.s $f10, $f10   (EE ft-operand form)
lwc1  $f12, 0x40A4($t0)        # headX (cat space)
lwc1  $f14, 0x40AC($t0)        # headZ
nop
.word 0x460AA034               # c.lt.s $f20, $f10   (0 < h ?)  fs=f20 ft=f10
nop
bc1t  hok
nop
mov.s $f16, $f20               # no horizontal speed: no turn, no offset
b     offdone
mov.s $f18, $f20
hok:
mul.s $f16, $f12, $f8          # headX·vz
mul.s $f2,  $f14, $f6          # headZ·vx
add.s $f16, $f16, $f2
div.s $f16, $f16, $f10         # offX = headX·cos + headZ·sin   (sin = vx/h, cos = vz/h)
mul.s $f18, $f14, $f8          # headZ·vz
mul.s $f2,  $f12, $f6          # headX·vx
sub.s $f18, $f18, $f2
div.s $f18, $f18, $f10         # offY = −headX·sin + headZ·cos
offdone:
lwc1  $f2, 0x40A8($t0)         # headH
mul.s $f16, $f16, $f0          # × growth
mul.s $f18, $f18, $f0
mul.s $f2,  $f2,  $f0
lwc1  $f6, 0x0040($t9)         # pellet x
lwc1  $f8, 0x0044($t9)         # pellet height
lwc1  $f12, 0x0048($t9)        # pellet y
sub.s $f6, $f6, $f16           # root = pellet point − head offset
sub.s $f8, $f8, $f2
sub.s $f12, $f12, $f18
lui   $t6, 0x01EA
ori   $t6, $t6, 0x9900         # chara slot 1
swc1  $f6, 0x0010($t6)         # CharPos x
swc1  $f8, 0x0014($t6)         # CharPos height
swc1  $f12, 0x0018($t6)        # CharPos y
swc1  $f0, 0x0090($t6)         # scale x/y/z = k
swc1  $f0, 0x0094($t6)
swc1  $f0, 0x0098($t6)
addiu $t5, $zero, 68
sw    $t5, 0x0C68($t6)         # key = leap (fall pose)
sub.s $f14, $f4, $f0           # pellet sprite = 1 − k (draw only)
swc1  $f14, 0x0310($t4)
lw    $t5, 0x40D8($t0)         # N
slt   $t3, $t8, $t5            # frames (before this one) < N → still growing
bne   $t3, $zero, touch        # (growing: test the touch, then done)
nop
# ── BREAKAWAY at full size: keep the pellet's velocity, expire the pellet, start the fall ──
lwc1  $f6, 0x01C0($t9)         # vx
lwc1  $f7, 0x01C4($t9)         # vh
lwc1  $f8, 0x01C8($t9)         # vz
swc1  $f6, 0x40B4($t0)
swc1  $f7, 0x40B8($t0)
swc1  $f8, 0x40BC($t0)
lwc1  $f2, 0x40F0($t0)         # ground-speed fraction
addiu $t5, $zero, 1
sw    $t5, 0x02B0($t4)         # pellet lifetime = 1 (expires on its next step) …
sw    $t5, 0x0280($t4)         # … and no collision meanwhile
mul.s $f2, $f10, $f2           # ground speed = fraction · |v_xz|   (f10 = h, still valid)
lwc1  $f4, 0x4194($t0)         # … unless the mod gave an ABSOLUTE ground speed (a full-charge pellet flies 5.0,
nop                            #     a lighter one 3.5 — a fraction made the walk jump between 0.56 and 0.80)
.word 0x4604A034               # c.lt.s $f20, $f4   (0 < abs ?)  fs=f20 ft=f4
nop
bc1f  speedset
nop
mov.s $f2, $f4
speedset:
swc1  $f2, 0x40C8($t0)
nop
.word 0x460AA034               # c.lt.s $f20, $f10   (0 < h ?)
nop
bc1f  dirzero
nop
div.s $f16, $f6, $f10          # unit direction from the pellet's velocity
b     dirstore
div.s $f18, $f8, $f10          # (delay slot)
dirzero:
mov.s $f16, $f20               # no horizontal speed: direction (0, 0)
mov.s $f18, $f20
dirstore:
swc1  $f16, 0x40D0($t0)        # dirX
swc1  $f18, 0x40D4($t0)        # dirZ
sw    $zero, 0x4094($t0)       # unbound from the pellet
addiu $t5, $zero, 4
b     done
sw    $t5, 0x4098($t0)         # state = falling (delay slot)
# ── FLOOR PROBE: the real ground under the cat, the way a thrown item finds it (ItemThrowStep / checkCollision):
# reset the per-call work allocator, take a 0x500-unit CCPoly scratch from it, gather the map's collision polygons
# around the cat (setCollisionData(NowDngMap, polys, pos, 20.0, 1.5) → count), then cast a vertical segment from
# CatProbeUp above the cat to CatProbeDown below (CheckHit(polys, count, from, to, hit, nearest=1, skip=1) → poly
# index or −1). The last argument is a polygon-ATTRIBUTE SKIP MASK: characters (GetFootPoly) skip attribute 1,
# pellets and thrown items skip attribute 4 — ramps and stairs are character-only collision, so the cat must use
# the character mask or it sees the base floor under a ramp. On a hit the floor height (hit.y) replaces CatFloorH;
# with no hit (over a pit) the last known floor stands. Clobbers everything caller-saved; the state blocks below
# reload from memory. s0 = polys, s1 = count, s2 = state.
probe:
move  $s2, $t3                 # state
lui   $t7, 0x002A
lw    $t7, 0x2388($t7)         # WorkBuffer → the CDataAlloc2 object (the global is a POINTER: `lw v0,-0x7468(gp)` in checkCollision)
sw    $zero, 0x8($t7)          # used = 0 (every engine user of it does this first)
move  $a0, $t7
jal   0x001278A0               # Alloc__14CDataAlloc2<1>Fi(WorkBuffer, 0x500) → v0 = polys
addiu $a1, $zero, 0x500
move  $s0, $v0
lui   $t7, 0x002A
lw    $a0, 0x34B8($t7)         # NowDngMap
move  $a1, $s0
lui   $a2, 0x01EA
ori   $a2, $a2, 0x9910         # &slot 1 CharPos (x, h, y, 1.0)
lui   $t7, 0x41A0
mtc1  $t7, $f12                # 20.0
lui   $t7, 0x3FC0
mtc1  $t7, $f13                # 1.5
jal   0x001C0FC0               # setCollisionData(map, polys, pos, 20.0, 1.5) → v0 = count
nop
move  $s1, $v0
# Three vertical casts share the gathered polygons: at the root, CatProbeFront ahead and CatProbeBack behind along
# the cat's direction. The highest hit becomes the floor (frame slots: 0x48 running max, 0x4C hit flag, 0x44 the
# cast helper's return address). Vector y/w are common to all three; x/z are set per cast.
lui   $t6, 0x01EA
ori   $t6, $t6, 0x9900
lui   $t0, 0x01FB              # mailbox (the calls clobbered t0)
lwc1  $f8, 0x0014($t6)         # height
lwc1  $f2, 0x40F4($t0)         # probe reach up
lwc1  $f4, 0x40F8($t0)         # probe reach down
lui   $t7, 0x3F80
mtc1  $t7, $f10                # 1.0 (w)
add.s $f14, $f8, $f2           # from.y = height + reach up
sub.s $f16, $f8, $f4           # to.y   = height − reach down
swc1  $f14, 0x54($sp)
swc1  $f16, 0x64($sp)
swc1  $f10, 0x5C($sp)
swc1  $f10, 0x6C($sp)
sw    $zero, 0x4C($sp)         # no hit yet
# cast 1: the root
lwc1  $f6, 0x0010($t6)
lwc1  $f12, 0x0018($t6)
swc1  $f6, 0x50($sp)
swc1  $f12, 0x58($sp)
swc1  $f6, 0x60($sp)
jal   castone
swc1  $f12, 0x68($sp)          # (delay slot)
# cast 2: ahead of the root
lui   $t6, 0x01EA
ori   $t6, $t6, 0x9900
lui   $t0, 0x01FB
lwc1  $f6, 0x0010($t6)
lwc1  $f12, 0x0018($t6)
lwc1  $f2, 0x40D0($t0)         # dirX
lwc1  $f4, 0x40D4($t0)         # dirZ
lwc1  $f14, 0x40FC($t0)        # front distance
mul.s $f2, $f2, $f14
mul.s $f4, $f4, $f14
add.s $f6, $f6, $f2
add.s $f12, $f12, $f4
swc1  $f6, 0x50($sp)
swc1  $f12, 0x58($sp)
swc1  $f6, 0x60($sp)
jal   castone
swc1  $f12, 0x68($sp)          # (delay slot)
# cast 3: behind the root
lui   $t6, 0x01EA
ori   $t6, $t6, 0x9900
lui   $t0, 0x01FB
lwc1  $f6, 0x0010($t6)
lwc1  $f12, 0x0018($t6)
lwc1  $f2, 0x40D0($t0)
lwc1  $f4, 0x40D4($t0)
lwc1  $f14, 0x4100($t0)        # back distance
mul.s $f2, $f2, $f14
mul.s $f4, $f4, $f14
sub.s $f6, $f6, $f2
sub.s $f12, $f12, $f4
swc1  $f6, 0x50($sp)
swc1  $f12, 0x58($sp)
swc1  $f6, 0x60($sp)
jal   castone
swc1  $f12, 0x68($sp)          # (delay slot)
lw    $t5, 0x4C($sp)
beq   $t5, $zero, probed       # nothing under any of the three (over a pit): the last known floor stands
nop
lui   $t0, 0x01FB
lwc1  $f2, 0x48($sp)           # the highest hit
b     probed
swc1  $f2, 0x40C4($t0)         # → CatFloorH (delay slot)
castone:                       # CheckHit(polys, count, from, to, hit, nearest=1, skip=1); keep the highest hit.y
sw    $ra, 0x44($sp)
move  $a0, $s0
move  $a1, $s1
addiu $a2, $sp, 0x50           # from
addiu $a3, $sp, 0x60           # to
addiu $t0, $sp, 0x70           # hit point (out)
addiu $t1, $zero, 1            # nearest hit to `from` (the highest surface below it)
jal   0x00149D50               # → v0 = poly index or −1
addiu $t2, $zero, 1            # skip attribute-1 polys — the CHARACTER rule (ramps/stairs are attribute 4)
bltz  $v0, castdone
nop
lwc1  $f2, 0x74($sp)           # hit.y
lw    $t5, 0x4C($sp)
beq   $t5, $zero, casttake     # first hit: take it
nop
lwc1  $f4, 0x48($sp)           # running max
nop
.word 0x46022034               # c.lt.s $f4, $f2   (max < hit ?)  fs=f4 ft=f2
nop
bc1f  castdone
nop
casttake:
swc1  $f2, 0x48($sp)
addiu $t5, $zero, 1
sw    $t5, 0x4C($sp)
castdone:
lw    $ra, 0x44($sp)
jr    $ra
nop
probed:
lui   $t0, 0x01FB              # rebuild the state blocks' registers
lui   $t2, 0x002A
lw    $t2, 0x35D4($t2)
addiu $t4, $zero, 4
beq   $s2, $t4, falling
addiu $t4, $zero, 6
beq   $s2, $t4, running
nop
b     landing
nop
falling:
# The land clip (215..227) brings the cat down itself: its hips sink from 5.5 to 4.6 by frame 219, where the paws
# touch. So the clip must START about (219−215)/speed frames BEFORE the physical touchdown (CatLandLead) — from the
# predicted time to the floor, t = (vh + sqrt(vh² + 2·g·d)) / g — and a key change only takes effect at once when
# the motion-flags restart bit (+0xC64 |= 4) is set with it; without it the engine finishes the current clip first.
lui   $t6, 0x01EA
ori   $t6, $t6, 0x9900
lwc1  $f6, 0x0010($t6)         # x
lwc1  $f8, 0x0014($t6)         # height
lwc1  $f12, 0x0018($t6)        # y
lwc1  $f16, 0x40B4($t0)        # vx
lwc1  $f18, 0x40B8($t0)        # vh
lwc1  $f14, 0x40BC($t0)        # vz
lwc1  $f2,  0x40C0($t0)        # gravity
lwc1  $f20, 0x40C4($t0)        # floor height
add.s $f6, $f6, $f16           # forward speed kept
add.s $f12, $f12, $f14
add.s $f8, $f8, $f18
sub.s $f18, $f18, $f2
swc1  $f18, 0x40B8($t0)        # vh −= g
nop
.word 0x4608A034               # c.lt.s $f20, $f8   (floor < height ?)  fs=f20 ft=f8
nop
bc1t  midair
nop
b     startland                # touched down before the clip could lead in (a very low fall): snap and start it now
mov.s $f8, $f20                # (delay slot) height = floor
midair:
sub.s $f10, $f8, $f20          # d = height above the floor
mul.s $f4, $f18, $f18          # vh²
add.s $f7, $f2, $f2            # 2g
mul.s $f7, $f7, $f10           # 2·g·d
add.s $f10, $f4, $f7
.word 0x460A0284               # sqrt.s $f10, $f10
lwc1  $f4, 0x40E8($t0)         # lead (frames)
add.s $f10, $f18, $f10         # vh + sqrt(…)
div.s $f10, $f10, $f2          # t = frames until the floor
nop
.word 0x460A2034               # c.lt.s $f4, $f10   (lead < t ?)  fs=f4 ft=f10
nop
bc1t  keepfalling
nop
startland:
sw    $zero, 0x4150($t0)       # the flying pounce (if any) is over
lw    $t7, 0x0C20($t6)
lw    $t8, 0x41C0($t0)
sw    $t8, 0x0018($t7)         # the channel's blend increment back to the default (the fall fade may have slowed it)
lui   $t7, 0xBF80
sw    $t7, 0x0C60($t6)         # the land clip plays at its KEY rate (the leap may have run at CatLeapRate)
lw    $t5, 0x0C64($t6)
ori   $t5, $t5, 6              # restart = HARD CUT + play once: fall → land never fades (user 2026-09-11: the lead-in is tuned for an
sw    $t5, 0x0C64($t6)         # instant start and the poses already meet); the clip runs to its end and HOLDS
addiu $t5, $zero, 69
sw    $t5, 0x0C68($t6)         # key = land
addiu $t5, $zero, 5
sw    $t5, 0x4098($t0)         # state = landing (clip running; physics continues until the floor)
b     storepos
sw    $zero, 0x40E4($t0)       # last-seen frame = 0 (delay slot)
keepfalling:
lw    $t5, 0x4150($t0)         # flying pounce?
beq   $t5, $zero, fallpose
nop
# The float-up hands over to the fall so that the fade (CatFallBlendFrames steps) ENDS exactly as the land clip starts:
# the land clip starts at t = lead frames before the floor (f10 = t, f4 = lead, both live from the prediction above),
# so the switch comes at t = lead + blend frames. Dynamic in the blend length (user 2026-09-11).
lwc1  $f7, 0x41E0($t0)         # CatFallBlendFrames
add.s $f7, $f7, $f4            # lead + blend
nop
.word 0x460A3834               # c.lt.s $f7, $f10   (lead + blend < t ? too early: keep the float pose)  fs=f7 ft=f10
nop
bc1f  fallpose
nop
lw    $t5, 0x4154($t0)         # rising: hold the float-up pose
b     storepos
sw    $t5, 0x0C68($t6)         # (delay slot)
fallpose:
lw    $t5, 0x0C68($t6)
lw    $t7, 0x4154($t0)
bne   $t5, $t7, leapkey        # switching from the float-up: fade into the fall clip once
nop
lw    $t5, 0x0C64($t6)
addiu $t7, $zero, -3
and   $t5, $t5, $t7            # clear play-once: this clip LOOPS; no restart → the engine cross-fades
sw    $t5, 0x0C64($t6)
lw    $t7, 0x0C20($t6)         # the copy's channel: fade into the fall over 1/CatFallBlend steps (slower than the 0.1 default)
lw    $t8, 0x41BC($t0)
sw    $t8, 0x0018($t7)         # MOTION_STATE blend increment (Step saves/restores it around each step, so it persists)
lui   $t7, 0xBF80
sw    $t7, 0x0C60($t6)         # the fall clip at its own KEY rate again
leapkey:
addiu $t5, $zero, 68
sw    $t5, 0x0C68($t6)         # key = leap (the fall)
storepos:
swc1  $f6, 0x0010($t6)
swc1  $f8, 0x0014($t6)
swc1  $f12, 0x0018($t6)
b     touch
nop
landing:
lui   $t6, 0x01EA
ori   $t6, $t6, 0x9900
lw    $t7, 0x0C20($t6)         # slot 1's channel 0 = the copy's cat channel (MOTION_TYPE)
lwc1  $f2, 0x0010($t7)         # its live motion frame (absolute clip frame: land = 215..227)
lwc1  $f4, 0x40DC($t0)         # paws-touch frame (momentum stops)
lwc1  $f6, 0x40E0($t0)         # clip end frame (→ run)
lwc1  $f10, 0x40E4($t0)        # frame seen last time
swc1  $f2, 0x40E4($t0)
nop
lui   $t7, 0x3F80
mtc1  $t7, $f12
nop
nop
nop
sub.s $f12, $f6, $f12          # end − 1: a play-once clip HOLDS just short of its end
nop
.word 0x460C1034               # c.lt.s $f2, $f12  (frame < end−1 ?)  fs=f2 ft=f12
nop
bc1f  landdone                 # clip finished → straight into the run
nop
.word 0x460A1034               # c.lt.s $f2, $f10  (frame < last ? → the clip wrapped)  fs=f2 ft=f10
nop
bc1t  landdone
nop
lwc1  $f6, 0x0010($t6)         # x
lwc1  $f8, 0x0014($t6)         # height
lwc1  $f12, 0x0018($t6)        # y
lwc1  $f16, 0x40B4($t0)        # vx
lwc1  $f18, 0x40B8($t0)        # vh
lwc1  $f14, 0x40BC($t0)        # vz
lwc1  $f7,  0x40C0($t0)        # gravity
lwc1  $f20, 0x40C4($t0)        # floor height
move  $t8, $zero               # airborne this frame? (momentum is kept while so)
nop
.word 0x4608A034               # c.lt.s $f20, $f8   (floor < height ?)
nop
bc1f  onfloor
nop
addiu $t8, $zero, 1            # still in the air: keep falling
add.s $f8, $f8, $f18
sub.s $f18, $f18, $f7
swc1  $f18, 0x40B8($t0)
nop
.word 0x4608A034               # c.lt.s $f20, $f8   (still above the floor ?)
nop
bc1t  onfloor
nop
mov.s $f8, $f20                # touchdown: snap to the floor
onfloor:
bne   $t8, $zero, momentum     # airborne → momentum regardless of the clip
nop
nop
.word 0x46041034               # c.lt.s $f2, $f4   (frame < paws-touch ?)  fs=f2 ft=f4
nop
bc1f  landhold                 # paws down: momentum gone, the clip plays on
nop
momentum:
add.s $f6, $f6, $f16           # forward momentum continues
add.s $f12, $f12, $f14
landhold:
addiu $t5, $zero, 69
sw    $t5, 0x0C68($t6)         # key = land (no restart bit: the clip never restarts)
swc1  $f6, 0x0010($t6)
swc1  $f8, 0x0014($t6)
swc1  $f12, 0x0018($t6)
b     touch
nop
landdone:
lw    $t5, 0x0C64($t6)
addiu $t7, $zero, -3
and   $t5, $t5, $t7            # clear play-once: the walk LOOPS; no restart → the landing pose fades into it
sw    $t5, 0x0C64($t6)
addiu $t5, $zero, 6
b     running                  # walk from this very frame (the pose catches up over the fade)
sw    $t5, 0x4098($t0)         # state = running (delay slot)
running:
lui   $t6, 0x01EA
ori   $t6, $t6, 0x9900
lwc1  $f6, 0x0010($t6)         # x
lwc1  $f12, 0x0018($t6)        # y
lwc1  $f16, 0x40D0($t0)        # dirX
lwc1  $f14, 0x40D4($t0)        # dirZ
lw    $t5, 0x40CC($t0)         # target position vector (0 = nothing in range → sit)
beq   $t5, $zero, sitdown
nop
lwc1  $f2, 0x0000($t5)         # target x
lwc1  $f4, 0x0008($t5)         # target y
sub.s $f2, $f2, $f6
sub.s $f4, $f4, $f12
mul.s $f10, $f2, $f2
mul.s $f18, $f4, $f4
add.s $f10, $f10, $f18
.word 0x460A0284               # sqrt.s $f10, $f10
mtc1  $zero, $f20
nop
nop
nop
.word 0x460AA034               # c.lt.s $f20, $f10   (0 < dist ?)
nop
bc1f  runmove
nop
div.s $f16, $f2, $f10          # steer at the target
div.s $f14, $f4, $f10
swc1  $f16, 0x40D0($t0)
swc1  $f14, 0x40D4($t0)
lwc1  $f2, 0x411C($t0)         # pounce range
nop
.word 0x460A1034               # c.lt.s $f2, $f10   (range < dist ?)  fs=f2 ft=f10
nop
bc1t  runmove                  # too far: keep walking
nop
swc1  $f10, 0x4158($t0)        # diagnostics: the distance …
swc1  $f2, 0x415C($t0)         # … and the range this decision used
swc1  $f6, 0x4170($t0)         # … cat x
swc1  $f12, 0x4174($t0)        # … cat y
lwc1  $f2, 0x0000($t5)
lwc1  $f4, 0x0008($t5)
swc1  $f2, 0x4178($t0)         # … target x
swc1  $f4, 0x417C($t0)         # … target y
readystart:                    # ── every pounce: the ready crouch first, standing (user 2026-09-11: the vertical leap for all) ──
lw    $t5, 0x0C64($t6)
ori   $t5, $t5, 2              # play once, no restart: the walk fades into the crouch, which runs to its end and HOLDS
sw    $t5, 0x0C64($t6)
addiu $t5, $zero, 65
sw    $t5, 0x0C68($t6)
lui   $t7, 0xBF80
sw    $t7, 0x0C60($t6)
sw    $zero, 0x40E4($t0)
addiu $t5, $zero, 10
b     done
sw    $t5, 0x4098($t0)         # state = ready (delay slot)
runmove:
lwc1  $f2, 0x40C8($t0)         # ground speed
mul.s $f16, $f16, $f2          # this frame's move vector
mul.s $f14, $f14, $f2
swc1  $f16, 0x48($sp)
swc1  $f14, 0x4C($sp)
# Wall probe (same gathered polygons, character mask): a horizontal segment 6 above the floor from the root to three
# frames of travel ahead. A hit means a wall (a ramp cannot rise 6 units in that distance; a low crate is climbed).
lwc1  $f8, 0x40C4($t0)         # floor
lui   $t7, 0x40C0
mtc1  $t7, $f4                 # 6.0
lui   $t7, 0x4040
mtc1  $t7, $f10                # 3.0
lui   $t7, 0x3F80
mtc1  $t7, $f18                # 1.0 (w)
add.s $f8, $f8, $f4            # probe height
mul.s $f16, $f16, $f10         # three frames ahead
mul.s $f14, $f14, $f10
add.s $f16, $f6, $f16          # to.x
add.s $f14, $f12, $f14         # to.z
swc1  $f6, 0x50($sp)
swc1  $f8, 0x54($sp)
swc1  $f12, 0x58($sp)
swc1  $f18, 0x5C($sp)
swc1  $f16, 0x60($sp)
swc1  $f8, 0x64($sp)
swc1  $f14, 0x68($sp)
swc1  $f18, 0x6C($sp)
move  $a0, $s0
move  $a1, $s1
addiu $a2, $sp, 0x50
addiu $a3, $sp, 0x60
addiu $t0, $sp, 0x70
addiu $t1, $zero, 1
jal   0x00149D50               # CheckHit(polys, count, from, to, hit, 1, 1)
addiu $t2, $zero, 1
lui   $t0, 0x01FB
lui   $t6, 0x01EA
ori   $t6, $t6, 0x9900
lwc1  $f6, 0x0010($t6)
lwc1  $f12, 0x0018($t6)
lwc1  $f8, 0x40C4($t0)         # floor
bltz  $v0, runfree
nop
addiu $t5, $zero, 1            # ── blocked by a wall: hold, idle, the KEY's own rate ──
sw    $t5, 0x4118($t0)
lw    $t5, 0x4114($t0)         # idle key
sw    $t5, 0x0C68($t6)
lui   $t7, 0xBF80
sw    $t7, 0x0C60($t6)         # motion-speed override = −1.0 (use the KEY's rate)
b     runstore
nop
runfree:
sw    $zero, 0x4118($t0)
lwc1  $f16, 0x48($sp)
lwc1  $f14, 0x4C($sp)
add.s $f6, $f6, $f16           # move
add.s $f12, $f12, $f14
# Clip rate from the ground speed, the town's way: rate = base + perSpeed · speed, capped.
lwc1  $f2, 0x40C8($t0)         # ground speed
lwc1  $f4, 0x4104($t0)         # base
lwc1  $f10, 0x4108($t0)        # per unit of speed
lwc1  $f16, 0x410C($t0)        # cap
mul.s $f10, $f10, $f2
add.s $f10, $f10, $f4          # rate
nop
.word 0x460A8034               # c.lt.s $f16, $f10   (cap < rate ?)  fs=f16 ft=f10
nop
bc1f  rateok
nop
mov.s $f10, $f16               # capped
rateok:
lw    $t5, 0x40EC($t0)         # walk key
sw    $t5, 0x0C68($t6)
swc1  $f10, 0x0C60($t6)        # motion-speed override
runstore:
swc1  $f6, 0x0010($t6)
swc1  $f8, 0x0014($t6)
swc1  $f12, 0x0018($t6)
b     done                     # (2026-09-11: this branch was missing — every walking frame fell through into the
nop                            #  ready block below: ready pose while walking, pounces launched with no trigger)
sitdown:                       # ── no enemy in range: sit in place (the sit clip loops) until the mod names one ──
lw    $t7, 0x0C68($t6)
lw    $t5, 0x41A0($t0)         # sit key
beq   $t7, $t5, done           # already sitting: nothing to do
nop
lw    $t8, 0x0C64($t6)
addiu $t9, $zero, -3
and   $t8, $t8, $t9            # let it loop; no restart → the walk fades into the sit
sw    $t8, 0x0C64($t6)
sw    $t5, 0x0C68($t6)
lui   $t7, 0xBF80
sw    $t7, 0x0C60($t6)         # the sit plays at its KEY rate
sw    $zero, 0x4118($t0)       # not "blocked"
b     done
nop
ready:
lui   $t6, 0x01EA
ori   $t6, $t6, 0x9900
lw    $t7, 0x0C20($t6)
lwc1  $f2, 0x0010($t7)         # live motion frame (ready = 95..105)
lwc1  $f6, 0x4130($t0)         # ready clip end
lwc1  $f4, 0x4160($t0)         # ready clip start
lwc1  $f10, 0x40E4($t0)
lui   $t7, 0x3F80
mtc1  $t7, $f8                 # 1.0
nop
.word 0x46041034               # c.lt.s $f2, $f4   (frame < start ? → not this clip yet)  fs=f2 ft=f4
nop
bc1t  readyhold
nop
add.s $f8, $f6, $f8            # end + 1
nop
.word 0x46024034               # c.lt.s $f8, $f2   (end+1 < frame ?)  fs=f8 ft=f2
nop
bc1t  readyhold                # frame > end+1 → a stale frame from an earlier clip: hold
nop
swc1  $f2, 0x40E4($t0)         # a frame of THIS clip: remember it
nop
lui   $t7, 0x3F80
mtc1  $t7, $f12
nop
nop
nop
sub.s $f12, $f6, $f12          # end − 1: a play-once clip HOLDS just short of its end
nop
.word 0x460C1034               # c.lt.s $f2, $f12  (frame < end−1 ?)  fs=f2 ft=f12
nop
bc1f  readydone
nop
.word 0x460A1034               # c.lt.s $f2, $f10  (wrapped ?)
nop
bc1t  readydone
nop
readyhold:                     # ── every frame of the crouch: face the target where it is NOW (user 2026-09-11) ──
lw    $t5, 0x40CC($t0)         # target position vector
beq   $t5, $zero, readykey
nop
lwc1  $f2, 0x0000($t5)
lwc1  $f4, 0x0008($t5)
lwc1  $f6, 0x0010($t6)
lwc1  $f12, 0x0018($t6)
sub.s $f2, $f2, $f6
sub.s $f4, $f4, $f12
mul.s $f10, $f2, $f2
mul.s $f14, $f4, $f4
add.s $f10, $f10, $f14
.word 0x460A0284               # sqrt.s $f10, $f10
mtc1  $zero, $f20
nop
nop
nop
.word 0x460AA034               # c.lt.s $f20, $f10   (0 < dist ?)
nop
bc1f  readykey
nop
div.s $f2, $f2, $f10
div.s $f4, $f4, $f10
swc1  $f2, 0x40D0($t0)         # the mod turns the cat to CatDirX/Z in every cave-owned state
swc1  $f4, 0x40D4($t0)
readykey:
addiu $t5, $zero, 65
sw    $t5, 0x0C68($t6)         # hold the ready clip, in place
b     done
nop
readydone:                     # ── crouch done: the float-up starts IN PLACE (state 11); the jump is decided at its feet-off frame ──
lw    $t5, 0x0C64($t6)
ori   $t5, $t5, 2              # play once, no restart: the crouch fades into the float-up, which runs to its end and HOLDS
sw    $t5, 0x0C64($t6)
lw    $t5, 0x4154($t0)
sw    $t5, 0x0C68($t6)         # key = float-up
lw    $t7, 0x41B8($t0)
sw    $t7, 0x0C60($t6)         # at CatFloatRate (user 2026-09-11: a touch faster than its KEY rate)
sw    $zero, 0x40E4($t0)
addiu $t5, $zero, 11
b     done
sw    $t5, 0x4098($t0)         # state = float wind-up (delay slot)
floatwait:                     # ── state 11: standing in the float-up's wind-up, turning to the target every frame (user 2026-09-11) ──
lui   $t6, 0x01EA
ori   $t6, $t6, 0x9900
lw    $t7, 0x0C20($t6)
lwc1  $f2, 0x0010($t7)         # live motion frame
lwc1  $f4, 0x41B4($t0)         # float clip start
lwc1  $f6, 0x41B0($t0)         # feet-off frame = the launch
nop
.word 0x46041034               # c.lt.s $f2, $f4   (frame < start ? still fading in from the crouch: hold)
nop
bc1t  floathold
nop
.word 0x46061034               # c.lt.s $f2, $f6   (frame < launch ? keep turning)
nop
bc1f  launch                   # the feet leave the ground: jump at where the target is NOW
nop
floathold:
lw    $t5, 0x40CC($t0)         # target position vector
beq   $t5, $zero, floatkey
nop
lwc1  $f2, 0x0000($t5)
lwc1  $f4, 0x0008($t5)
lwc1  $f6, 0x0010($t6)
lwc1  $f12, 0x0018($t6)
sub.s $f2, $f2, $f6
sub.s $f4, $f4, $f12
mul.s $f10, $f2, $f2
mul.s $f14, $f4, $f4
add.s $f10, $f10, $f14
.word 0x460A0284               # sqrt.s $f10, $f10
mtc1  $zero, $f20
nop
nop
nop
.word 0x460AA034               # c.lt.s $f20, $f10   (0 < dist ?)
nop
bc1f  floatkey
nop
div.s $f2, $f2, $f10
div.s $f4, $f4, $f10
swc1  $f2, 0x40D0($t0)         # face the target (the mod turns the cat to CatDirX/Z)
swc1  $f4, 0x40D4($t0)
floatkey:
lw    $t5, 0x4154($t0)
sw    $t5, 0x0C68($t6)         # hold the float-up
b     done
nop
launch:                        # ── the jump is decided here, from the target's live position ──
lw    $t5, 0x40CC($t0)         # target position vector
beq   $t5, $zero, walkagain    # target gone: back to walking
nop
lwc1  $f2, 0x0000($t5)         # target x
lwc1  $f8, 0x0004($t5)         # target height
lwc1  $f4, 0x0008($t5)         # target y
lwc1  $f6, 0x0010($t6)         # cat x
lwc1  $f12, 0x0018($t6)        # cat y
swc1  $f6, 0x4180($t0)         # diagnostics: the ready's view — cat x
swc1  $f12, 0x4184($t0)        # cat y
swc1  $f2, 0x4188($t0)         # target x
swc1  $f4, 0x418C($t0)         # target y
sub.s $f2, $f2, $f6            # dx
sub.s $f4, $f4, $f12           # dz
mul.s $f0, $f2, $f2
mul.s $f1, $f4, $f4
add.s $f0, $f0, $f1
.word 0x46000004               # sqrt.s $f0, $f0  → distance
swc1  $f0, 0x4190($t0)         # distance
mtc1  $zero, $f20
nop
nop
nop
.word 0x4600A034               # c.lt.s $f20, $f0   (0 < dist ?)
nop
bc1f  jumpdir                  # on top of it: keep the old direction
nop
lwc1  $f10, 0x416C($t0)        # farthest a pounce may launch at (the target may have changed during the ready)
nop
.word 0x46005034               # c.lt.s $f10, $f0   (max < dist ?)  fs=f10 ft=f0
nop
bc1t  walkagain                # too far now: walk instead
nop
div.s $f2, $f2, $f0
div.s $f4, $f4, $f0
swc1  $f2, 0x40D0($t0)         # face the target
swc1  $f4, 0x40D4($t0)
jumpdir:
lwc1  $f10, 0x40C4($t0)        # floor
sub.s $f8, $f8, $f10           # Δh = target height above the floor (the ready only ever precedes a vertical leap)
flyjump:                       # ── flying target: vertical leap now — arc at its live position, v = d/T, vh = Δh/T + g·T/2 ──
addiu $t5, $zero, 1
sw    $t5, 0x4150($t0)         # CatPounceFly = 1 (the fall block holds the float-up pose while rising)
lwc1  $f14, 0x4120($t0)        # flight frames T
div.s $f16, $f2, $f14          # (f2/f4 are the unit direction here; recompute the displacement)
lw    $t5, 0x40CC($t0)
lwc1  $f2, 0x0000($t5)
lwc1  $f4, 0x0008($t5)
lwc1  $f6, 0x0010($t6)
lwc1  $f12, 0x0018($t6)
sub.s $f2, $f2, $f6            # dx
sub.s $f4, $f4, $f12           # dz
div.s $f16, $f2, $f14          # vx
div.s $f18, $f4, $f14          # vz
div.s $f8, $f8, $f14           # Δh / T   (f8 = Δh from above)
lwc1  $f10, 0x40C0($t0)        # g
mul.s $f10, $f10, $f14         # g·T
lui   $t7, 0x3F00
mtc1  $t7, $f0
nop
nop
nop
mul.s $f10, $f10, $f0          # g·T/2
add.s $f8, $f8, $f10           # vh
swc1  $f16, 0x40B4($t0)
swc1  $f8, 0x40B8($t0)
swc1  $f18, 0x40BC($t0)
lw    $t5, 0x0C64($t6)
ori   $t5, $t5, 2              # play once, no restart: the crouch fades into the float-up, which runs to its end and HOLDS
sw    $t5, 0x0C64($t6)
lw    $t5, 0x4154($t0)
sw    $t5, 0x0C68($t6)         # key = float-up
addiu $t5, $zero, 4
b     done
sw    $t5, 0x4098($t0)         # state = falling (the arc; landing lead-in as usual)
walkagain:
addiu $t5, $zero, 6
b     done
sw    $t5, 0x4098($t0)
# ── TOUCH: the pellet's own recipe (step__5CSHOT 0x1ABD10) — checkCollision sweeps a 2.0 point against every live
# enemy's active body spheres; on contact a 3.0 damage entry is planted at that point with the pellet's stamps (owner
# Xiao, kind 0, element, weapon flags/anti-enemy pointer) plus the cat's melee-type kick, and the mod is told which
# entry (CatHitEntry) so it can fade the cat when — and only when — the enemy's CheckDmg consumes it (user
# 2026-09-11: contact with an invulnerable enemy must not spend the cat; the hitspark is CheckDmg's own). Reached only
# from the pellet ride (fall pose), the fall/float-up (state 4) and the landing (state 5). Clobbers everything.
touch:
lui   $t0, 0x01FB
lw    $t5, 0x41C8($t0)         # CatHitLatch: one planted entry at a time (the mod clears it if it never connects)
bne   $t5, $zero, done
nop
lui   $t6, 0x01EA
ori   $t6, $t6, 0x9900
lw    $t7, 0x41DC($t0)         # CatHeadNode: the copy's cat_kao frame (mod) — the "pellet" is the middle of the head (user 2026-09-11)
beq   $t7, $zero, rootpoint
nop
lwc1  $f6, 0x0180($t7)         # its world matrix's translation row (+0x150 + 0x30): the posed head, as of the last draw
lwc1  $f8, 0x0184($t7)
lwc1  $f14, 0x0188($t7)
b     havepoint
nop
rootpoint:
lwc1  $f6, 0x0010($t6)         # no head frame known: the root, 2 up
lwc1  $f8, 0x0014($t6)
lwc1  $f14, 0x0018($t6)
lui   $t7, 0x4000
mtc1  $t7, $f2
nop
nop
nop
add.s $f8, $f8, $f2
havepoint:
lui   $t7, 0x4000
mtc1  $t7, $f12                # 2.0 = the contact radius (the pellet's)
lui   $t7, 0x3F80
mtc1  $t7, $f4                 # 1.0
swc1  $f6, 0x0050($sp)         # pos vector (x, h, y, 1)
swc1  $f8, 0x0054($sp)
swc1  $f14, 0x0058($sp)
swc1  $f4, 0x005C($sp)
lw    $t5, 0x4098($t0)         # riding the pellet (state 1)? sweep along ITS velocity; else the cat's own
addiu $t7, $zero, 1
bne   $t5, $t7, ownvel
addiu $a2, $t0, 0x40B4         # (delay slot) CatVx/Vh/Vz
lw    $t1, 0x4094($t0)         # pellet slot + 1
beq   $t1, $zero, done
nop
lui   $t2, 0x002A
lw    $t2, 0x35D4($t2)         # pool base
addiu $t1, $t1, -1
sll   $t3, $t1, 4
addu  $a2, $t2, $t3
addiu $a2, $a2, 0x01C0         # the pellet's velocity vector
ownvel:
addiu $a0, $sp, 0x0060         # out (the sphere centre it met)
addiu $a1, $sp, 0x0050         # pos
addiu $a3, $zero, 2            # mask 2: enemies and walls, never the player
jal   0x001AB740               # checkCollision(f12 = 2.0, out, pos, vel, 2) → 3 = an enemy body sphere
nop
addiu $t7, $zero, 3
bne   $v0, $t7, done           # nothing, or a wall: no plant
nop
lui   $s0, 0x01FB              # mailbox base for the plant ($t0 is an argument register below)
lui   $t7, 0x4040
mtc1  $t7, $f12                # 3.0 = the damage entry's radius (the pellet's)
mtc1  $zero, $f13
lui   $a0, 0x002A
lw    $a0, 0x35E0($a0)         # NowColData (this)
addiu $a1, $sp, 0x0050         # the entry sits at the contact point, like a pellet's
lw    $a2, 0x41CC($s0)         # CatHitDamage (mod: pellet damage + attack)
addiu $a3, $zero, 1
addiu $t0, $zero, 2            # +0x48 victim mask: enemies
addiu $t1, $zero, 2            # +0x4C hit reaction type
lw    $t2, 0x41D0($s0)         # +0x50 element attribute (mod: the weapon's one pure element bit, or 0)
move  $t3, $zero               # +0x54
jal   0x001B57A0               # CCollisionData::Set — takes the first free entry and records its index at +0x3D80
nop
lui   $t9, 0x002A
lw    $t9, 0x35E0($t9)         # NowColData
lw    $t8, 0x3D80($t9)         # the entry index
sll   $t7, $t8, 7
sll   $t5, $t8, 5
addu  $t7, $t7, $t5            # × 0xA0
addu  $t7, $t9, $t7            # the entry
addiu $t5, $zero, 1
sw    $t5, 0x0058($t7)         # owner = Xiao: her magic/element/body-part table, kill credit and ranged falloff, as a pellet
sw    $zero, 0x0060($t7)       # attack kind 0
lui   $t5, 0x01EA
ori   $t5, $t5, 0x7590         # the battle weapon record (what NowWeaponHave points at)
lh    $t6, 0x00EE($t5)
sw    $t6, 0x006C($t7)         # weapon ability flags
addiu $t5, $t5, 0x001C
sw    $t5, 0x0064($t7)         # anti-enemy byte array
lwc1  $f6, 0x0050($sp)         # kick origin = the contact point: the enemy is shoved away from the cat
lwc1  $f8, 0x0054($sp)
lwc1  $f14, 0x0058($sp)
swc1  $f6, 0x0080($t7)
swc1  $f8, 0x0084($t7)
swc1  $f14, 0x0088($t7)        # (+0x8C = 1.0 from Set)
lw    $t5, 0x41D4($s0)
sw    $t5, 0x0090($t7)         # kick strength
lw    $t5, 0x41D8($s0)
sw    $t5, 0x0094($t7)         # kick decay
addiu $t5, $zero, 2
sw    $t5, 0x0098($t7)         # kick type 2 = melee-style → the patched flinch rule lets it stagger
addiu $t5, $t8, 1
sw    $t5, 0x41C4($s0)         # CatHitEntry = index + 1 → the mod watches it: consumed = the hit landed = fade
addiu $t5, $zero, 1
sw    $t5, 0x41C8($s0)         # latch until the mod resolves the entry
lw    $t5, 0x4098($s0)         # riding the pellet? full size next frame → the follow block breaks it away
addiu $t6, $zero, 1
bne   $t5, $t6, done
nop
lw    $t5, 0x40D8($s0)
b     done
sw    $t5, 0x409C($s0)         # frames = N (delay slot)
done:
lw    $ra, 0x40($sp)
.word 0x7BB00010               # lq $s0, 0x10($sp)
.word 0x7BB10020               # lq $s1, 0x20($sp)
.word 0x7BB20030               # lq $s2, 0x30($sp)
jr    $ra
addiu $sp, $sp, 0x80
