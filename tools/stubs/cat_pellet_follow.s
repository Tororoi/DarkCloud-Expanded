# Divine Beast cat — pellet catcher + follower (v2, 2026-09-10), the mod's ELF cave segment @0x1FB0D90
# (ElfCave.CatPelletFollow).
#
# WHY NATIVE: the charged shot's cat must appear ON THE FRAME its pellet is born, grow from nothing to full size over
# a handful of frames, and keep its head on the pellet's point every frame. A mod-thread follower (PINE, ~16 ms
# polls, plus the time it takes to notice the pellet) starts late and trails; the user ruled it out ("fast action
# that requires precision — make it engine-driven"). So this cave takes the dungeon step loop's once-per-frame
# `jal step__5CSHOT` (dun 0x1DB874C, a0 = the player shot pool), performs that call, and then:
#   1. keeps a 12-bit "seen" mask of active pellet slots (every frame, always) so a pellet is NEW exactly once;
#   2. when the mod has armed it (state 3 = waiting, set at the charge threshold, the cat copy already resident and
#      hidden in chara slot 1), binds to the first new pellet on its birth frame: slot, frames = 0, state = 1, and
#      makes the cat visible (opacity 128) that same frame;
#   3. while bound (state 1): root = pellet − k·headOffset, k = min(1, frames/N); the head offset is the cat's rest
#      offset (cat space: x, height, z) turned by the pellet's own horizontal direction (sin/cos from its velocity);
#      scale = k; pellet sprite = 1 − k (draw only, hitbox untouched). When the pellet ends: slot = 0, state = 2.
#
# MAILBOX (guest 0x01F10000 page; CodeCaveAddresses.Mailbox):
#   +0x94 CatPelletSlot  int   bound pellet slot + 1 (0 = none; the page boots zero-filled so zero must mean off)
#   +0x98 CatState       int   0 idle, 1 following, 2 pellet ended (mod fades the cat), 3 waiting for a new pellet
#   +0x9C CatGrowFrames  int   frames since bound (this cave)
#   +0xA0 CatGrowInv     float 1 / growth frames (mod)
#   +0xA4 CatHeadX / +0xA8 CatHeadH / +0xAC CatHeadZ   float  head rest offset in CAT space (mod, × cat scale)
#   +0xB0 CatSeenMask    int   active-slot bits from the previous frame (this cave)
# Chara slot 1 (CNPCharacter @guest 0x01EA9900): pos +0x10/+0x14/+0x18, scale +0x90..+0x98, opacity +0xCEC (float 0..128).
# Shot pool (*0x002A35D4): pos +0x40 + slot*0x10, vel +0x1C0 + slot*0x10, sprite scale +0x310 + slot*4, active +0x3D0 + slot*4.
#
# ⚠ EE rules (tools/lib/mips_asm.py): c.lt.s and sqrt.s hand-encoded (.word), ≥1 insn between a compare and its
# bc1x, ≥3 insns between an mtc1 and a dependent FP op. Clobbers t0-t9, f0-f20 (all caller-saved at this site: the
# next vanilla insn rebuilds v0/a0). ra kept on the stack; s-regs and gp untouched.
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
addu  $t4, $t2, $t3
lw    $t5, 0x03D0($t4)         # pellet still active?
bne   $t5, $zero, alive
nop
sw    $zero, 0x0094($t0)       # pellet ended: unbind …
addiu $t6, $zero, 2
b     done
sw    $t6, 0x0098($t0)         # … and tell the mod (delay slot)
alive:
lw    $t6, 0x009C($t0)         # frames since bound
addiu $t7, $t6, 1
sw    $t7, 0x009C($t0)
mtc1  $t6, $f0
lwc1  $f2, 0x00A0($t0)         # 1 / growth frames
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
addu  $t4, $t2, $t3            # pellet vec row (pos +0x40, vel +0x1C0)
lwc1  $f6, 0x01C0($t4)         # vx
lwc1  $f8, 0x01C8($t4)         # vz (the pool's y)
mul.s $f10, $f6, $f6
mul.s $f12, $f8, $f8
add.s $f10, $f10, $f12         # h²
.word 0x460A0284               # sqrt.s $f10, $f10   (EE ft-operand form: 0x46000004 | ft<<16 | fd<<6)
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
div.s $f16, $f16, $f10         # offX = (headX·cos + headZ·sin), sin = vx/h, cos = vz/h
mul.s $f18, $f14, $f8          # headZ·vz
mul.s $f2,  $f12, $f6          # headX·vx
sub.s $f18, $f18, $f2
div.s $f18, $f18, $f10         # offY = (−headX·sin + headZ·cos)
offdone:
lwc1  $f2, 0x00A8($t0)         # headH
mul.s $f16, $f16, $f0          # × growth
mul.s $f18, $f18, $f0
mul.s $f2,  $f2,  $f0
lwc1  $f6, 0x0040($t4)         # pellet x
lwc1  $f8, 0x0044($t4)         # pellet height
lwc1  $f12, 0x0048($t4)        # pellet y
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
sub.s $f14, $f4, $f0           # pellet sprite = 1 − k (draw only)
sll   $t3, $t1, 2
addu  $t4, $t2, $t3
swc1  $f14, 0x0310($t4)
done:
lw    $ra, 0x0($sp)
jr    $ra
addiu $sp, $sp, 0x10
