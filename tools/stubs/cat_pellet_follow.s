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
# MAILBOX (guest 0x01F10000 page; CodeCaveAddresses.Mailbox):
#   +0x94 CatPelletSlot int  bound pellet slot + 1 (0 = none; page boots zero-filled)      +0x9C CatGrowFrames int
#   +0xA0 CatGrowInv float 1/N (mod)   +0xA4/+0xA8/+0xAC CatHeadX/H/Z float head rest offset, CAT space (mod)
#   +0xB0 CatSeenMask int (cave)   +0xB4/+0xB8/+0xBC CatVx/Vh/Vz float fall velocity (cave, captured at breakaway)
#   +0xC0 CatGravity float (mod)   +0xC4 CatFloorH float landing height (mod)   +0xC8 CatRunSpeed float = ½|v| (cave)
#   +0xCC CatTargetPtr uint guest address of the target's position vector (x @+0, z @+8) or 0 (mod)
#   +0xD0/+0xD4 CatDirX/Z float run direction, unit (cave; the mod turns the cat to face it)   +0xD8 CatGrowN int (mod)
#   +0xDC CatLandStopFrame float clip frame where the paws touch (momentum stops; 219)   +0xE0 CatLandEndFrame float
#   clip end (227 → run)   +0xE4 CatPrevFrame float the frame seen last time (wrap detection)   +0xE8 CatLandLead float
#   frames before the predicted touchdown at which the land clip starts ((219−215)/clip speed) — the copy's live motion
#   frame is read through slot 1's channel pointer (+0xC20 → MOTION_TYPE, frame @+0x10). Key switches set the motion
#   flags restart bit (slot +0xC64 |= 4), or the engine would finish the current clip before changing.
# Chara slot 1 (CNPCharacter @guest 0x01EA9900): pos +0x10/+0x14/+0x18, scale +0x90..+0x98, motion key +0xC68
# (68 leap / 69 land / 66 run — the cave writes it while it owns the cat), opacity +0xCEC (float 0..128).
# Shot pool (*0x002A35D4): pos +0x40+slot*0x10, vel +0x1C0+slot*0x10, nocollide +0x280+slot*4, lifetime +0x2B0+slot*4,
# sprite scale +0x310+slot*4 (draw only), active +0x3D0+slot*4.
#
# ⚠ EE rules (tools/lib/mips_asm.py): c.lt.s and sqrt.s hand-encoded (.word), ≥1 insn between a compare and its
# bc1x, ≥3 insns between an mtc1 and a dependent FP op. Clobbers t0-t9, f0-f20 (caller-saved at this site: the next
# vanilla insn rebuilds v0/a0). ra kept on the stack; s-regs and gp untouched.
addiu $sp, $sp, -0x10
sw    $ra, 0x0($sp)
jal   0x001ABD10               # step__5CSHOT(pool) — the displaced vanilla call (a0 still the pool)
nop
lui   $t0, 0x01F1              # mailbox page
lui   $t2, 0x002A
lw    $t2, 0x35D4($t2)         # pool base
lw    $t9, 0x00B0($t0)         # seen mask (previous frame)
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
sw    $t7, 0x00B0($t0)         # seen mask ← this frame
lw    $t3, 0x0098($t0)         # state
addiu $t4, $zero, 4
beq   $t3, $t4, falling
addiu $t4, $zero, 6
beq   $t3, $t4, running
addiu $t4, $zero, 5
beq   $t3, $t4, landing
addiu $t4, $zero, 3
bne   $t3, $t4, notwaiting
nop
bltz  $t8, done                # waiting, no new pellet yet
nop
addiu $t5, $t8, 1              # ── BIND on the birth frame ──
sw    $t5, 0x0094($t0)         # slot + 1
sw    $zero, 0x009C($t0)       # frames = 0
addiu $t5, $zero, 1
sw    $t5, 0x0098($t0)         # state = following
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
lw    $t1, 0x0094($t0)
beq   $t1, $zero, done
addiu $t1, $t1, -1             # → zero-based slot (delay slot; dead when the branch is taken)
follow:
sll   $t3, $t1, 2
addu  $t4, $t2, $t3            # scalar row
lw    $t5, 0x03D0($t4)         # pellet still active?
bne   $t5, $zero, alive
nop
sw    $zero, 0x0094($t0)       # pellet ended early: unbind …
addiu $t6, $zero, 2
b     done
sw    $t6, 0x0098($t0)         # … and tell the mod (delay slot)
alive:
lw    $t8, 0x009C($t0)         # frames since bound (t8 kept for the breakaway test)
addiu $t7, $t8, 1
sw    $t7, 0x009C($t0)
mtc1  $t8, $f0
lwc1  $f2, 0x00A0($t0)         # 1 / N
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
lwc1  $f12, 0x00A4($t0)        # headX (cat space)
lwc1  $f14, 0x00AC($t0)        # headZ
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
lwc1  $f2, 0x00A8($t0)         # headH
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
lw    $t5, 0x00D8($t0)         # N
slt   $t3, $t8, $t5            # frames (before this one) < N → still growing
bne   $t3, $zero, done
nop
# ── BREAKAWAY at full size: keep the pellet's velocity, expire the pellet, start the fall ──
lwc1  $f6, 0x01C0($t9)         # vx
lwc1  $f7, 0x01C4($t9)         # vh
lwc1  $f8, 0x01C8($t9)         # vz
swc1  $f6, 0x00B4($t0)
swc1  $f7, 0x00B8($t0)
swc1  $f8, 0x00BC($t0)
lui   $t7, 0x3F00
mtc1  $t7, $f2                 # 0.5
addiu $t5, $zero, 1
sw    $t5, 0x02B0($t4)         # pellet lifetime = 1 (expires on its next step) …
sw    $t5, 0x0280($t4)         # … and no collision meanwhile
mul.s $f2, $f10, $f2           # run speed = ½·|v_xz|   (f10 = h, still valid)
swc1  $f2, 0x00C8($t0)
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
swc1  $f16, 0x00D0($t0)        # dirX
swc1  $f18, 0x00D4($t0)        # dirZ
sw    $zero, 0x0094($t0)       # unbound from the pellet
addiu $t5, $zero, 4
b     done
sw    $t5, 0x0098($t0)         # state = falling (delay slot)
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
lwc1  $f16, 0x00B4($t0)        # vx
lwc1  $f18, 0x00B8($t0)        # vh
lwc1  $f14, 0x00BC($t0)        # vz
lwc1  $f2,  0x00C0($t0)        # gravity
lwc1  $f20, 0x00C4($t0)        # floor height
add.s $f6, $f6, $f16           # forward speed kept
add.s $f12, $f12, $f14
add.s $f8, $f8, $f18
sub.s $f18, $f18, $f2
swc1  $f18, 0x00B8($t0)        # vh −= g
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
lwc1  $f4, 0x00E8($t0)         # lead (frames)
add.s $f10, $f18, $f10         # vh + sqrt(…)
div.s $f10, $f10, $f2          # t = frames until the floor
nop
.word 0x460A2034               # c.lt.s $f4, $f10   (lead < t ?)  fs=f4 ft=f10
nop
bc1t  keepfalling
nop
startland:
lw    $t5, 0x0C64($t6)
ori   $t5, $t5, 4
sw    $t5, 0x0C64($t6)         # motion flags |= restart → the land clip starts on this very frame
addiu $t5, $zero, 69
sw    $t5, 0x0C68($t6)         # key = land
addiu $t5, $zero, 5
sw    $t5, 0x0098($t0)         # state = landing (clip running; physics continues until the floor)
b     storepos
sw    $zero, 0x00E4($t0)       # last-seen frame = 0 (delay slot)
keepfalling:
addiu $t5, $zero, 68
sw    $t5, 0x0C68($t6)         # key = leap
storepos:
swc1  $f6, 0x0010($t6)
swc1  $f8, 0x0014($t6)
swc1  $f12, 0x0018($t6)
b     done
nop
landing:
lui   $t6, 0x01EA
ori   $t6, $t6, 0x9900
lw    $t7, 0x0C20($t6)         # slot 1's channel 0 = the copy's cat channel (MOTION_TYPE)
lwc1  $f2, 0x0010($t7)         # its live motion frame (absolute clip frame: land = 215..227)
lwc1  $f4, 0x00DC($t0)         # paws-touch frame (momentum stops)
lwc1  $f6, 0x00E0($t0)         # clip end frame (→ run)
lwc1  $f10, 0x00E4($t0)        # frame seen last time
swc1  $f2, 0x00E4($t0)
nop
.word 0x46061034               # c.lt.s $f2, $f6   (frame < end ?)  fs=f2 ft=f6
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
lwc1  $f16, 0x00B4($t0)        # vx
lwc1  $f18, 0x00B8($t0)        # vh
lwc1  $f14, 0x00BC($t0)        # vz
lwc1  $f7,  0x00C0($t0)        # gravity
lwc1  $f20, 0x00C4($t0)        # floor height
move  $t8, $zero               # airborne this frame? (momentum is kept while so)
nop
.word 0x4608A034               # c.lt.s $f20, $f8   (floor < height ?)
nop
bc1f  onfloor
nop
addiu $t8, $zero, 1            # still in the air: keep falling
add.s $f8, $f8, $f18
sub.s $f18, $f18, $f7
swc1  $f18, 0x00B8($t0)
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
b     done
nop
landdone:
lw    $t5, 0x0C64($t6)
ori   $t5, $t5, 4
sw    $t5, 0x0C64($t6)         # restart bit: the run clip starts on this very frame
addiu $t5, $zero, 6
b     running                  # run from this very frame
sw    $t5, 0x0098($t0)         # state = running (delay slot)
running:
lui   $t6, 0x01EA
ori   $t6, $t6, 0x9900
lwc1  $f6, 0x0010($t6)         # x
lwc1  $f12, 0x0018($t6)        # y
lwc1  $f16, 0x00D0($t0)        # dirX
lwc1  $f14, 0x00D4($t0)        # dirZ
lw    $t5, 0x00CC($t0)         # target position vector (0 = run straight)
beq   $t5, $zero, runmove
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
swc1  $f16, 0x00D0($t0)
swc1  $f14, 0x00D4($t0)
runmove:
lwc1  $f2, 0x00C8($t0)         # run speed
mul.s $f16, $f16, $f2
mul.s $f14, $f14, $f2
add.s $f6, $f6, $f16
add.s $f12, $f12, $f14
lwc1  $f8, 0x00C4($t0)         # on the floor
swc1  $f6, 0x0010($t6)
swc1  $f8, 0x0014($t6)
swc1  $f12, 0x0018($t6)
addiu $t5, $zero, 66
sw    $t5, 0x0C68($t6)         # key = run
done:
lw    $ra, 0x0($sp)
jr    $ra
addiu $sp, $sp, 0x10
