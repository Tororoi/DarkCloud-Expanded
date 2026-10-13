# ally_brain.s — the Pocklekul's pockle, once a frame: the guard charge, the summon, its target, its end. Assembled at 0x0010C7D0 (DeadDmaCave:
# the dead sceDmaSendN..sceDmaSendM bodies), its summon (after #SPLIT) at 0x001203D0 (DeadPadPressCave: the dead scePadInfoPressMode..
# scePadGetSlotMax bodies). ally_manager.s calls it after the floor's CMonstorUnit::Step, a1 = Step's pause flag (nothing
# happens on a paused frame). The weapon thread only arms it (CodeCaves.AllyArmed while the Pocklekul is in Ruby's hand); events go to
# CodeCaves.AllyEvent ((count << 8) | code) for the app's log.
#
# The guard charge: Ruby (cur_chara 3) holding R1 in her guard pose (motion 9 or 33) counts frames (AllyGuard); at 60 the charge latches
# (AllyLatch, until the guard drops) — a summon if none stands, on a quiet floor (walking mode, no menu opening, no event).
# Its pockle is one of eight at random: the seven days' (table index 10 + day) and Hell Pockle (54).
# The summon (into Ruby's pockle block: the end of her character pool, PockleBlock): the floor's manager copied to block + 0x100, its script
# table pointed at the block's own arena (entry 0; the rest null, so CleanViewMonstor resets only it) and reset; the pockle loaded by SetupBaseModel into the block's model allocator — its textures join the monsters' group 42, and the
# entries it added and the group's VRAM ends before it are kept (CodeCaves.AllyTex) for its end — then stood 15 u in front of Ruby by
# SetupViewMonstor (both with NowMonstorUnit on its manager). Its block is stamped ("ALLY", the floor) and the manager word set: the game
# rides it from then on.
# While it stands: its end (ally_despawn.s) when its block is gone or another floor's, when it is disarmed or Ruby is not in hand, when
# an event takes the floor, when its script has killed it (unit 0 back to state −1), when Ruby is more than 300 u from it. Else its
# target: the locked-on enemy within 200 u of Ruby, else the one it had while it lives within 200 u, else the live enemy nearest her
# within 150 u. An enemy: its pointer-table
# entry (16) names a point inside that enemy's nearest hit sphere (ally_aim.s) and its AI runs (from label 100's top when it was idling). None: its AI is parked (AllyHold:
# its script resumed on a YIELD each frame while Step goes on moving, animating and hitting it) and it walks beside Ruby
# (ally_follow.s), its entry naming the idle point there. Falling (HP 0): left to its death label.
#
# The block: +0x00 model allocator, +0x10 script allocator, +0x20 spawn point, +0x30 idle point, +0x40 "ALLY", +0x44 floor stamp, +0x48
# its target slot (−1 none), +0x54 the group-42 entry mask (7 words, the summon's own), +0x70 its pockle (0..7), +0x74 its frames
# (ally_grow.s), +0x100 its manager (0x60750 B),
# +0x60850 its script arena (1,875 units), +0x67D80 its model (0xBD20 units, to the block's end at +0x124F80).
# Data words (lui 0x01FB): −0x808 AllyManager, −0x3C AllyHold, −0x38 AllyArmed, −0x34 AllyGuard, −0x30 AllyLatch, −0x2C AllyEvent;
# CodeCaves.AllyTex 0x01FAFCA0: +0 floor stamp, +4 / +8 group 42's VRAM top / end, +0xC (first entry << 16) | entry end.

brain:
    addiu $sp, $sp, -0x20
    sw    $ra, 0x00($sp)
    sw    $s0, 0x04($sp)
    sw    $s1, 0x08($sp)
    sw    $s2, 0x0C($sp)
    sw    $s3, 0x10($sp)
    bne   $a1, $zero, bdone        # a paused frame
    lui   $s0, 0x01FB              # (delay) the data words
    # ── the guard charge
    lw    $t1, -0x38($s0)          # AllyArmed
    beq   $t1, $zero, nocharge
    lui   $t0, 0x01CE
    lbu   $t1, -0x6AB0($t0)        # cur_chara (0x01CD9550)
    addiu $t2, $zero, 3
    bne   $t1, $t2, nocharge       # not Ruby
    lui   $t0, 0x01CC
    lw    $t1, -0x3ABC($t0)        # the pad (0x01CBC544)
    andi  $t1, $t1, 0x0008         # R1
    beq   $t1, $zero, nocharge
    lui   $t0, 0x01EA
    lw    $t1, 0x2988($t0)         # Ruby's motion
    addiu $t2, $zero, 9
    beq   $t1, $t2, guarding
    addiu $t2, $zero, 33
    bne   $t1, $t2, nocharge
    nop
guarding:
    lw    $t1, -0x34($s0)
    addiu $t1, $t1, 1
    sw    $t1, -0x34($s0)          # AllyGuard: frames held
    slti  $t2, $t1, 60
    bne   $t2, $zero, alive
    lw    $t3, -0x30($s0)          # (delay) AllyLatch
    bne   $t3, $zero, alive        # this guard's charge spent
    lw    $t4, -0x808($s0)         # (delay)
    beq   $t4, $zero, maysummon
    addiu $t3, $zero, 1            # (delay)
    sw    $t3, -0x30($s0)          # one stands: latched, nothing summoned
    bal   note
    addiu $a0, $zero, 9            # (delay)
    b     alive
    nop
maysummon:
    lhu   $t1, -0x6294($gp)        # dungeonMode (0x2A355C): 1 = walking
    bne   $t1, $t3, alive          # not quiet: tried again next frame
    lw    $t1, -0x628C($gp)        # (delay) the drive-step hold (0x2A3564: a menu opening)
    lw    $t2, -0x6288($gp)        # the frame capture (0x2A3568)
    or    $t1, $t1, $t2
    lw    $t2, -0x61E8($gp)        # BtEventMode (0x2A3608)
    or    $t1, $t1, $t2
    bne   $t1, $zero, alive
    nop
    sw    $t3, -0x30($s0)          # latched
    jal   summon
    nop
    b     alive
    nop
nocharge:
    sw    $zero, -0x34($s0)
    sw    $zero, -0x30($s0)
    # ── while it stands
alive:
    lw    $s1, -0x808($s0)         # its manager
    beq   $s1, $zero, bdone
    addiu $s3, $s1, -0x100         # (delay) its block
    bal   blockaddr
    nop
    bne   $v0, $s3, gone4          # Ruby's pool is not what it was
    lw    $t0, 0x40($s3)           # (delay)
    lui   $t1, 0x594C
    ori   $t1, $t1, 0x4C41
    bne   $t0, $t1, gone4          # its block overwritten
    nop
    bal   stamp
    nop
    lw    $t0, 0x44($s3)
    bne   $v0, $t0, gone4          # another floor
    lw    $t0, -0x38($s0)          # (delay) AllyArmed
    beq   $t0, $zero, gone
    addiu $a0, $zero, 3            # (delay) disarmed
    lui   $t0, 0x01CE
    lbu   $t0, -0x6AB0($t0)
    addiu $t1, $zero, 3
    bne   $t0, $t1, gone           # Ruby not in hand (a0 still 3)
    lw    $t0, -0x61E8($gp)        # (delay) BtEventMode
    bne   $t0, $zero, gone4
    lui   $t0, 0x0002              # (delay)
    addu  $t0, $s1, $t0
    lw    $t0, -0x1C30($t0)        # its unit 0's state (manager + 0x1E3D0)
    addiu $t1, $zero, -1
    beq   $t0, $t1, gone
    addiu $a0, $zero, 2            # (delay) fallen
    jal   0x0011EF4C               # ally_grow.s: its first frames' growth
    or    $a0, $s3, $zero          # (delay)
    jal   0x00119348               # ally_hooks.s's pin: the summon smoke kept on it
    or    $a0, $s1, $zero          # (delay)
    lui   $t0, 0x0002
    addu  $t0, $s1, $t0
    lw    $t0, -0x1C0C($t0)        # its unit 0's HP (manager + 0x1E3F4)
    blez  $t0, bdone               # falling: its death label plays out untouched (no park, no restart, no walk)
    nop
    # ── its target
    lui   $t0, 0x01EA
    lwc1  $f0, 0x1D30($t0)         # Ruby x
    lwc1  $f1, 0x1D38($t0)         #      y
    lui   $t1, 0x47AF
    ori   $t1, $t1, 0xC800
    mtc1  $t1, $f4                 # 300²: past this from Ruby it is left behind (out of the game's view of it)
    lui   $t0, 0x0002
    addu  $t0, $s1, $t0
    lwc1  $f2, -0x0320($t0)        # its x (manager + 0x1FCE0: unit 0's CCharacter + 0x10)
    lwc1  $f3, -0x0318($t0)        #     y
    sub.s $f2, $f2, $f0
    sub.s $f3, $f3, $f1
    mul.s $f2, $f2, $f2
    mul.s $f3, $f3, $f3
    add.s $f2, $f2, $f3
    .word 0x46022034               # c.olt.s f4, f2: farther than 300
    nop
    bc1t  gone
    addiu $a0, $zero, 11           # (delay) left behind
    lui   $t0, 0x471C
    ori   $t0, $t0, 0x4000
    mtc1  $t0, $f12                # 200²
    lw    $a0, -0x626C($gp)        # the locked-on slot (0x2A3584)
    bal   cand
    nop
    bne   $v0, $zero, picked
    nop
    bal   cand
    lw    $a0, 0x48($s3)           # (delay) the one it had
    bne   $v0, $zero, picked
    lui   $t0, 0x46AF              # (delay)
    ori   $t0, $t0, 0xC800
    mtc1  $t0, $f12                # 150²
    addiu $s2, $zero, -1
    or    $a0, $zero, $zero
nearest:
    bal   cand
    nop
    beq   $v0, $zero, nnext
    nop
    mov.s $f12, $f13               # nearer than any so far
    or    $s2, $a0, $zero
nnext:
    addiu $a0, $a0, 1
    slti  $t0, $a0, 16
    bne   $t0, $zero, nearest
    nop
    b     ptarget
    nop
picked:
    or    $s2, $a0, $zero
ptarget:
    sw    $s2, 0x48($s3)
    bltz  $s2, idle
    lw    $t3, -0x3C($s0)          # (delay) AllyHold: it was idling
    beq   $t3, $zero, tpos
    sw    $zero, -0x3C($s0)        # (delay) its AI on the enemy
    jal   0x001178D0               # ally_follow.s's release: label 100 afresh
    or    $a0, $s1, $zero          # (delay)
tpos:
    or    $a0, $s2, $zero
    jal   0x0010CD20               # ally_aim.s: the point in its body the AI goes for
    or    $a1, $s3, $zero          # (delay)
    b     setp
    or    $t2, $v0, $zero          # (delay)
idle:
    addiu $t3, $zero, 1
    sw    $t3, -0x3C($s0)          # AllyHold: its AI parked
    jal   0x001178A0               # ally_follow.s's park (each frame: Step resumes it there)
    or    $a0, $s1, $zero          # (delay)
    jal   0x001176D8               # ally_follow.s
    or    $a0, $s3, $zero          # (delay)
    addiu $t2, $s3, 0x30           # the idle point
setp:
    lui   $t1, 0x01F2
    b     bdone
    sw    $t2, -0x6FC0($t1)        # (delay) PtrTable[16] (0x01F19040)
gone4:
    addiu $a0, $zero, 4
gone:
    jal   0x00117080               # ally_despawn.s (a0 = the event code)
    nop
bdone:
    lw    $ra, 0x00($sp)
    lw    $s0, 0x04($sp)
    lw    $s1, 0x08($sp)
    lw    $s2, 0x0C($sp)
    lw    $s3, 0x10($sp)
    jr    $ra
    addiu $sp, $sp, 0x20

# a0 = a floor slot, f12 = a range², f0/f1 = Ruby's x/y: v0 = 1 when it is a live enemy that near (f13 = its distance²). a0 kept.
cand:
    sltiu $t5, $a0, 16
    beq   $t5, $zero, cno
    addiu $t6, $zero, 0x190
    mult  $a0, $t6
    mflo  $t6
    lui   $t7, 0x01E1
    ori   $t7, $t7, 0x6BA0         # the floor's monster[] (0x01E16BA0)
    addu  $t6, $t6, $t7
    lhu   $t7, 0x42($t6)           # its species
    beq   $t7, $zero, cno
    ori   $t8, $zero, 0xFFFF
    beq   $t7, $t8, cno
    lw    $t7, 0x00($t6)           # (delay) state
    blez  $t7, cno
    lw    $t7, 0x24($t6)           # (delay) hp
    blez  $t7, cno
    ori   $t6, $zero, 0x3510
    mult  $a0, $t6
    mflo  $t6
    lui   $t7, 0x01E1
    ori   $t7, $t7, 0x84B0
    addu  $t6, $t6, $t7
    lwc1  $f2, 0x00($t6)
    lwc1  $f3, 0x08($t6)
    sub.s $f2, $f2, $f0
    sub.s $f3, $f3, $f1
    mul.s $f2, $f2, $f2
    mul.s $f3, $f3, $f3
    add.s $f13, $f2, $f3
    .word 0x460D6034               # c.olt.s f12, f13: out of range
    nop
    bc1t  cno
    nop
    jr    $ra
    addiu $v0, $zero, 1
cno:
    jr    $ra
    or    $v0, $zero, $zero

# v0 = Ruby's pockle block (her character pool's last 0x124F80 B) when the pool ends where the weapon pool starts and her effect copy
# sits below it, else 0. t0..t7.
blockaddr:
    lui   $t0, 0x01F0
    lw    $t1, 0x6660($t0)         # the chara pool's base (0x01F06660)
    lw    $t2, 0x6668($t0)         #               used units
    lw    $t4, 0x6670($t0)         # the weapon pool's base
    sll   $t2, $t2, 4
    addu  $t3, $t1, $t2            # its end
    bne   $t4, $t3, bno
    lui   $t5, 0x0012              # (delay)
    ori   $t5, $t5, 0x4F80
    subu  $v0, $t3, $t5
    lw    $t6, -0x6214($gp)        # Ruby's effect copy (0x2A35DC)
    beq   $t6, $zero, bno
    sltu  $t7, $t6, $v0            # (delay)
    bne   $t7, $zero, bok
    nop
bno:
    or    $v0, $zero, $zero
bok:
    jr    $ra
    nop

# v0 = the floor stamp: (dungeon << 16) | (floor << 8) | back floor. t0..t3.
stamp:
    lbu   $t0, -0x625C($gp)        # checkDungeon (0x2A3594)
    lui   $t1, 0x01CE
    lbu   $t1, -0x6AB2($t1)        # checkFloor (0x01CD954E)
    lbu   $t2, -0x633C($gp)        # the back-floor flag (0x2A34B4)
    sll   $t0, $t0, 16
    sll   $t1, $t1, 8
    or    $v0, $t0, $t1
    sltu  $t2, $zero, $t2
    jr    $ra
    or    $v0, $v0, $t2

# a0 = an event code: AllyEvent = ((count + 1) << 8) | code. t0, t1.
note:
    lui   $t0, 0x01FB
    lw    $t1, -0x2C($t0)
    srl   $t1, $t1, 8
    addiu $t1, $t1, 1
    sll   $t1, $t1, 8
    or    $t1, $t1, $a0
    jr    $ra
    sw    $t1, -0x2C($t0)

# The texture entries of group 42 (s1 = the block, its mask at +0x54): a0 = 0 marks them; a0 = 1 finds the unmarked ones,
# v0 = (first << 16) | (last + 1), 0 for none.
scan42:
    lui   $t0, 0x01C7
    ori   $t0, $t0, 0x6968         # the entries (texture manager + 0x10F8)
    or    $t1, $zero, $zero
    ori   $t8, $zero, 0xFFFF       # first
    addiu $t9, $zero, -1           # last
    bne   $a0, $zero, sloop
    addiu $t2, $s1, 0x54           # (delay)
    addiu $t3, $s1, 0x70
sclr:
    sw    $zero, 0x00($t2)
    addiu $t2, $t2, 4
    bne   $t2, $t3, sclr
    nop
sloop:
    lhu   $t2, 0x00($t0)
    xori  $t2, $t2, 42
    bne   $t2, $zero, snext
    srl   $t3, $t1, 5              # (delay)
    sll   $t3, $t3, 2
    addu  $t3, $s1, $t3
    lw    $t4, 0x54($t3)
    addiu $t5, $zero, 1
    sllv  $t5, $t5, $t1
    bne   $a0, $zero, sfind
    nop
    or    $t4, $t4, $t5
    b     snext
    sw    $t4, 0x54($t3)           # (delay) marked
sfind:
    and   $t4, $t4, $t5
    bne   $t4, $zero, snext
    slt   $t6, $t1, $t8            # (delay)
    movn  $t8, $t1, $t6
    or    $t9, $t1, $zero
snext:
    addiu $t1, $t1, 1
    slti  $t2, $t1, 196
    bne   $t2, $zero, sloop
    addiu $t0, $t0, 0x50           # (delay)
    bltz  $t9, snone
    addiu $t9, $t9, 1              # (delay)
    sll   $v0, $t8, 16
    jr    $ra
    or    $v0, $v0, $t9
snone:
    jr    $ra
    or    $v0, $zero, $zero
#SPLIT
# The summon (s0 = the data words). Events: 0x10 + pockle stood, 5 no block, 6 no VRAM room, 7 not loaded, 8 not stood.
summon:
    addiu $sp, $sp, -0x30
    sw    $ra, 0x00($sp)
    sw    $s1, 0x04($sp)
    sw    $s2, 0x08($sp)
    sw    $s3, 0x0C($sp)
    sw    $s4, 0x10($sp)
    jal   blockaddr
    nop
    beq   $v0, $zero, snote
    addiu $a0, $zero, 5            # (delay)
    or    $s1, $v0, $zero          # the block
    addiu $s2, $s1, 0x100          # its manager
    # VRAM room in the monsters' group: it grows by the pockle's bank
    lui   $t0, 0x01C7
    ori   $t0, $t0, 0x5870         # the texture manager
    lw    $t1, 0x14($t0)           # its cursor
    lw    $t2, 0xA10($t0)          # group 42's VRAM top
    lw    $t3, 0xA14($t0)          #          VRAM end
    addiu $t4, $t3, 0x180
    sltu  $t4, $t1, $t4
    bne   $t4, $zero, snote
    addiu $a0, $zero, 6            # (delay)
    lui   $t5, 0x01FB
    sw    $t2, -0x35C($t5)         # AllyTex + 4 (0x01FAFCA4)
    sw    $t3, -0x358($t5)         # AllyTex + 8
    sw    $zero, -0x354($t5)       # AllyTex + 0xC: no entries yet
    # the floor's manager copied, its script table its own, reset
    or    $a0, $s2, $zero
    lui   $a1, 0x01DF
    ori   $a1, $a1, 0x87D0         # MainMonstorUnit
    lui   $a2, 0x0006
    jal   0x001033B0               # memcpy
    ori   $a2, $a2, 0x0750         # (delay)
    addiu $t0, $s1, 0x10
    sw    $t0, 0x00($s2)           # script[0]: the block's allocator
    addiu $t1, $s2, 0x04
    addiu $t2, $s2, 0x40
stab:
    sw    $zero, 0x00($t1)         # the rest null
    addiu $t1, $t1, 4
    bne   $t1, $t2, stab
    nop
    lui   $t0, 0x0006
    ori   $t0, $t0, 0x0850
    addu  $t0, $s1, $t0            # its arena
    sw    $t0, 0x10($s1)
    sw    $t0, 0x14($s1)
    sw    $zero, 0x18($s1)
    addiu $t1, $zero, 1875
    sw    $t1, 0x1C($s1)
    lui   $t0, 0x0006
    ori   $t0, $t0, 0x7D80
    addu  $t0, $s1, $t0            # its model room
    sw    $t0, 0x00($s1)
    sw    $t0, 0x04($s1)
    sw    $zero, 0x08($s1)
    ori   $t1, $zero, 0xBD20
    sw    $t1, 0x0C($s1)
    or    $a0, $s2, $zero
    jal   0x001DF9F0               # CleanViewMonstor
    or    $a1, $zero, $zero        # (delay)
    sw    $zero, 0x48($s2)         # model_count
    sw    $zero, 0x90($s2)         # current_monster
    addiu $t0, $zero, -1
    sw    $t0, 0x94($s2)           # requested_event
    sw    $zero, 0x98($s2)         # paused
    # the group-42 entries before, a random pockle, its load
    jal   scan42
    or    $a0, $zero, $zero        # (delay) mark
    jal   0x001046F8               # rand
    nop
    addiu $t0, $zero, 8
    .word 0x0048001B               # divu v0, t0
    mfhi  $s3                      # the pockle (0 Sunday .. 6 Saturday, 7 Hell Pockle)
    sw    $s3, 0x70($s1)
    sw    $zero, 0x74($s1)         # its frames (ally_grow.s)
    jal   0x001191A0               # ally_shots.s before: the shot pack's empty slots noted
    nop
    lw    $s4, -0x6320($gp)        # NowMonstorUnit
    sw    $s2, -0x6320($gp)
    or    $a0, $s2, $zero
    or    $a1, $zero, $zero
    slti  $t1, $s3, 7
    bne   $t1, $zero, sday
    addiu $a2, $s3, 10             # (delay) a day's table index (10 + day)
    addiu $a2, $zero, 54           # Hell Pockle's
sday:
    addiu $a3, $zero, 0x26
    jal   0x001DFE90               # SetupBaseModel
    or    $t0, $s1, $zero          # (delay) the block's model allocator
    sw    $s4, -0x6320($gp)
    beq   $v0, $zero, snote
    addiu $a0, $zero, 7            # (delay)
    jal   scan42
    addiu $a0, $zero, 1            # (delay) find the new
    lui   $t5, 0x01FB
    sw    $v0, -0x354($t5)         # AllyTex + 0xC: its entries
    jal   0x001191E0               # ally_shots.s after: the shot configs it entered, kept for its end
    nop
    jal   stamp
    nop
    lui   $t5, 0x01FB
    sw    $v0, -0x360($t5)         # AllyTex + 0: this floor's
    sw    $v0, 0x44($s1)
    lui   $t0, 0x594C
    ori   $t0, $t0, 0x4C41
    sw    $t0, 0x40($s1)           # "ALLY"
    addiu $t0, $zero, -1
    sw    $t0, 0x48($s1)           # no target yet
    # 15 u in front of Ruby
    lui   $t0, 0x01EA
    jal   0x0011D8A0               # sinf
    lwc1  $f12, 0x1D84($t0)        # (delay) Ruby's yaw
    swc1  $f0, 0x14($sp)
    lui   $t0, 0x01EA
    jal   0x0011D6B0               # cosf
    lwc1  $f12, 0x1D84($t0)        # (delay)
    lui   $t0, 0x01EA
    lwc1  $f1, 0x1D30($t0)
    lwc1  $f2, 0x1D34($t0)
    lwc1  $f3, 0x1D38($t0)
    lui   $t1, 0x4170
    mtc1  $t1, $f4                 # 15.0
    lwc1  $f5, 0x14($sp)
    mul.s $f5, $f5, $f4
    add.s $f1, $f1, $f5            # x + 15 sin
    mul.s $f6, $f0, $f4
    add.s $f3, $f3, $f6            # y + 15 cos
    lui   $t1, 0x3F80
    swc1  $f1, 0x20($s1)
    swc1  $f2, 0x24($s1)
    swc1  $f3, 0x28($s1)
    sw    $t1, 0x2C($s1)
    swc1  $f1, 0x30($s1)           # its idle point, till it first idles
    swc1  $f2, 0x34($s1)
    swc1  $f3, 0x38($s1)
    sw    $t1, 0x3C($s1)
    addiu $t0, $s1, 0x30
    lui   $t1, 0x01F2
    sw    $t0, -0x6FC0($t1)        # PtrTable[16]
    sw    $s2, -0x6320($gp)
    or    $a0, $s2, $zero
    or    $a1, $zero, $zero
    addiu $a2, $s1, 0x20
    jal   0x001E02B0               # SetupViewMonstor
    addiu $a3, $zero, -1           # (delay)
    sw    $s4, -0x6320($gp)
    sw    $s2, -0x808($s0)         # the game rides it from now on
    bne   $v0, $zero, snote
    addiu $a0, $s3, 0x10           # (delay) stood
    jal   0x00117080               # not stood: its end (its textures go)
    addiu $a0, $zero, 8            # (delay)
    b     sdone
    nop
snote:
    jal   note
    nop
sdone:
    lw    $ra, 0x00($sp)
    lw    $s1, 0x04($sp)
    lw    $s2, 0x08($sp)
    lw    $s3, 0x0C($sp)
    lw    $s4, 0x10($sp)
    jr    $ra
    addiu $sp, $sp, 0x30
