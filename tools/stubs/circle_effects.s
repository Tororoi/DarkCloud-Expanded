# circle_effects.s — MAGIC CIRCLES, driven by data. Replaces dun.bin's Run_TrapCircle (0x1DBFA70, hooked by DunPatches:
# `j` here, its own body never runs): the effect a circle carries (MAP_TRAP_CIRCLE +0x14, rolled 0..9 by SetupTrapCircle) is
# applied with every magnitude read from the circle table (CodeCaves.CircleTable, 0x01FAF900) instead of the immediates the
# engine bakes across Run_TrapCircle, WeaponDataChangeByRGate's six handlers, BtSetStatusErr and AllBin2. The pnach seeds
# the vanilla figures every frame while the table's owner word is 0; a sword that changes the circles (the Crystal Knife's
# doubling — Weapons/MagicCircles.cs) sets the owner and writes its own. New effect ids go on the end of the chain.
#
# Assembled at 0x01B47C8 (DebugIfCave.CircleEffects): the body of the main ELF's DebugInfomationIF, the developers' debug
# overlay input handler, reached only from the overlay's debug key (dun 0x1DB5150, mode 0xDD); ElfWeaponPatches.PatchCircleEffects
# turns its first two words into `jr ra; li v0,0` so that caller sees "nothing pressed", and writes this from +8.
#
# THE EFFECTS (vanilla figure in brackets; "good" = SE 0xE1, "bad" = 0xE2, as the engine plays them):
#   0 attack ×2 — BtSetStatusErr(8), then the status timer (UserStatus+0x42E0+char×2) set to AttackFrames [0x708]     good
#   1 gilda += trunc(gilda × GildaUpMult) + GildaUpAdd, capped 0xFFFF [1.2, 10]                                       good
#   2 ABS to max — WeaponDataChangeByRGate(record,0) — and RewardCount × AbsFullItem into the bag while there is room [none] good
#   3 max WHP += MaxWhpUpMin + rand % MaxWhpUpRange, capped 99 [3, 3]                                                  good
#   4 WHP to max — WeaponDataChangeByRGate(record,4) — and RewardCount × WhpCureItem into the bag while there is room [none] good
#   5 every enemy slot's rage timer (MonstorUnit + i×400 + 0x1E3E0, 16 slots) = RageFrames [300]                        bad
#   6 gilda = trunc(gilda − gilda × GildaDownFrac), floor 0 [0.2]                                                       bad
#   7 one of attack/endurance/speed/magic (rand & 3) −= StatDownMin + rand % StatDownRange [2, 3] (attack floors at 1,
#     the rest at 0), then one byte of each element/anti group (+0x17 ×5, +0x1C ×3, +0x1F ×3, +0x22 ×4) −=
#     (2 + rand % {3,2,2,2}) × ElemDownMult [1], floor 0                                                                bad
#   8 max WHP −= MaxWhpDownMin + rand % MaxWhpDownRange [3, 3]; WHP clamped down to it; max floors at 1                 bad
#   9 WHP = WHP / WhpDivisor [4.0], floor 1.0                                                                            bad
# A starter weapon in hand (the character's default or its broken form, dun table 0x1DC1B00) always takes effect 0 — the
# engine's own rule, kept. Effects 2/3/4/7/8/9 rebuild the battle record (SetWeaponAttachStatus) as the engine does.
#
# FAVOUR (the Secret Armlet, Weapons/Ruby/SecretArmlet.cs): while the table's Favour word is set the bad circles turn good —
#   5 every enemy SLOWED instead: its gooey timer (+0x1E3E4) = SlowFrames                                                good
#   6 dealt as 1 (gilda up)   8 dealt as 3 (max WHP up)   9 dealt as 4 (WHP to max)
#   7 every loss becomes a GAIN of the same roll: the stat and the element/anti bytes go up, capped 99                  good
#
# THE TABLE (all words; floats where said): +0x00 owner, +0x04 AttackFrames, +0x08 GildaUpMult f, +0x0C GildaUpAdd,
# +0x10 GildaDownFrac f, +0x14 MaxWhpUpMin, +0x18 MaxWhpUpRange, +0x1C StatDownMin, +0x20 StatDownRange, +0x24 MaxWhpDownMin,
# +0x28 MaxWhpDownRange, +0x2C WhpDivisor f, +0x30 RageFrames, +0x34 AbsFullItem, +0x38 WhpCureItem, +0x3C ElemDownMult,
# +0x40 Favour, +0x44 SlowFrames, +0x48 RewardCount (how many of the reward item effects 2 and 4 give).
# Ranges must be ≥ 1 (a `divu` by zero is not trapped). Divisions are raw `.word`s: keystone expands the `divu` mnemonic
# into a zero-check macro (bne/divu/break/mflo) whose branch offset lands on the break — never use the mnemonic here.
#
# Frame: 0x90 — s0..s6 and ra as the engine keeps them (sq/lq, raw words: keystone's MIPS32 has no sq/lq), 0x80..0x8F scratch
# for the local subroutines' return address and arguments. s0 UserStatus, s1 the equipped inventory record, s2 the table,
# s3 the effect id, s4 the character, s5 scratch, s6 the circle.
    .word 0x27BDFF70               # addiu sp,sp,-0x90
    .word 0x7FBF0070               # sq ra,0x70(sp)
    .word 0x7FB60060               # sq s6,0x60(sp)
    .word 0x7FB50050               # sq s5,0x50(sp)
    .word 0x7FB40040               # sq s4,0x40(sp)
    .word 0x7FB30030               # sq s3,0x30(sp)
    .word 0x7FB20020               # sq s2,0x20(sp)
    .word 0x7FB10010               # sq s1,0x10(sp)
    .word 0x7FB00000               # sq s0,0x00(sp)
    beq   $a0, $zero, done         # no circle: nothing (the engine's own guard)
    or    $s6, $a0, $zero
    lw    $s0, -0x6388($gp)        # UserStatus
    lui   $s2, 0x1FB
    addiu $s2, $s2, -0x700         # the circle table, 0x01FAF900
    lb    $s4, 4($s0)              # the active character
    addiu $t0, $zero, 0xAA8
    mult  $s4, $t0
    addu  $t2, $s0, $s4
    lb    $t2, 0x4340($t2)         # its equipped bag slot
    mflo  $t1
    addu  $t1, $t1, $s0            # + char × 0xAA8
    addiu $t0, $zero, 0xF8
    mult  $t2, $t0
    lw    $s3, 0x14($s6)           # the circle's effect
    mflo  $t3
    addu  $s1, $t1, $t3
    addiu $s1, $s1, 0x450C         # the equipped inventory record
    # a starter weapon only ever takes effect 0
    lh    $t0, 0($s1)
    lui   $t1, 0x1DC
    sll   $t2, $s4, 2
    addu  $t1, $t1, $t2
    lw    $t1, 0x1B00($t1)         # the character's default weapon id
    beq   $t0, $t1, force0
    addiu $t1, $t1, 1
    bne   $t0, $t1, dispatch
    nop
force0:
    or    $s3, $zero, $zero
dispatch:
    lw    $t9, 0x40($s2)           # Favour: the rerolled bad circles are dealt as their good counterparts
    beq   $t9, $zero, chain
    addiu $t0, $s3, -6
    bne   $t0, $zero, fav8
    nop
    b     chain
    addiu $s3, $zero, 1            # 6 → gilda up
fav8:
    addiu $t0, $s3, -8
    bne   $t0, $zero, fav9
    nop
    b     chain
    addiu $s3, $zero, 3            # 8 → max WHP up
fav9:
    addiu $t0, $s3, -9
    bne   $t0, $zero, chain
    nop
    addiu $s3, $zero, 4            # 9 → WHP to max
chain:
    beq   $s3, $zero, e0
    addiu $t0, $s3, -1
    beq   $t0, $zero, e1
    addiu $t0, $s3, -2
    beq   $t0, $zero, e2
    addiu $t0, $s3, -3
    beq   $t0, $zero, e3
    addiu $t0, $s3, -4
    beq   $t0, $zero, e4
    addiu $t0, $s3, -5
    beq   $t0, $zero, e5
    addiu $t0, $s3, -6
    beq   $t0, $zero, e6
    addiu $t0, $s3, -7
    beq   $t0, $zero, e7
    addiu $t0, $s3, -8
    beq   $t0, $zero, e8
    addiu $t0, $s3, -9
    beq   $t0, $zero, e9
    nop
    b     done                     # an id this cave does not know: nothing
    nop

# ── 0: attack ×2 for AttackFrames ────────────────────────────────────────────────────
e0:
    addiu $a0, $zero, 8
    jal   0x1B1BB0                 # BtSetStatusErr(8): the flag, and its own 0x708 on the timer
    nop
    sll   $t0, $s4, 2
    addu  $t0, $t0, $s0
    lw    $t0, 0x42C8($t0)         # the status flags
    andi  $t0, $t0, 8
    beq   $t0, $zero, snd_good     # held off by another status: the timer is not ours to set
    nop
    lw    $t0, 4($s2)              # AttackFrames
    sll   $t1, $s4, 1
    addu  $t1, $t1, $s0
    b     snd_good
    sh    $t0, 0x42E0($t1)

# ── 1: gilda up ──────────────────────────────────────────────────────────────────────
e1:
    lhu   $t0, 0x4346($s0)         # gilda
    mtc1  $t0, $f0
    lwc1  $f1, 8($s2)              # GildaUpMult (and the mtc1 gap)
    cvt.s.w $f0, $f0
    mul.s $f0, $f0, $f1
    cvt.w.s $f0, $f0
    nop
    mfc1  $t1, $f0
    lw    $t2, 0xC($s2)            # GildaUpAdd
    addu  $t1, $t1, $t2
    addu  $t1, $t1, $t0
    ori   $t3, $zero, 0xFFFF
    slt   $t4, $t1, $t3
    bne   $t4, $zero, e1w
    nop
    or    $t1, $t3, $zero          # capped
e1w:
    b     snd_good
    sh    $t1, 0x4346($s0)

# ── 2: ABS to max, and a reward item ─────────────────────────────────────────────────
e2:
    or    $a0, $s1, $zero
    jal   0x20FCE0                 # WeaponDataChangeByRGate(record, 0)
    or    $a1, $zero, $zero
    jal   attach
    nop
    lw    $a0, 0x34($s2)           # AbsFullItem
    jal   give
    nop
    b     snd_good
    nop

# ── 3: max WHP up ────────────────────────────────────────────────────────────────────
e3:
    jal   0x1046F8                 # rand
    nop
    lw    $t0, 0x18($s2)           # MaxWhpUpRange
    .word 0x0048001B               # divu v0,t0 — raw: keystone turns the mnemonic into a 4-word check-and-break macro whose branch lands ON the break
    lw    $t2, 0x14($s2)           # MaxWhpUpMin
    mfhi  $t1
    addu  $t1, $t1, $t2
    lh    $t3, 0xC($s1)
    addu  $t3, $t3, $t1
    slti  $t4, $t3, 100
    bne   $t4, $zero, e3w
    nop
    addiu $t3, $zero, 99
e3w:
    sh    $t3, 0xC($s1)
    jal   attach
    nop
    b     snd_good
    nop

# ── 4: WHP to max, and a reward item ─────────────────────────────────────────────────
e4:
    or    $a0, $s1, $zero
    jal   0x20FCE0                 # WeaponDataChangeByRGate(record, 4)
    addiu $a1, $zero, 4
    jal   attach
    nop
    lw    $a0, 0x38($s2)           # WhpCureItem
    jal   give
    nop
    b     snd_good
    nop

# ── 5: every enemy enraged — or, favoured, slowed ────────────────────────────────────
e5:
    lw    $t0, -0x6320($gp)        # MonstorUnit
    lw    $t9, 0x40($s2)           # Favour
    lw    $t1, 0x30($s2)           # RageFrames
    lui   $t3, 0x1
    ori   $t3, $t3, 0xE3E0
    beq   $t9, $zero, e5s
    addu  $t0, $t0, $t3            # slot 0's rage timer…
    lw    $t1, 0x44($s2)           # …or, favoured, SlowFrames
    addiu $t0, $t0, 4              # into its gooey timer, the next word
e5s:
    addiu $t2, $zero, 16
e5l:
    sw    $t1, 0($t0)
    addiu $t2, $t2, -1
    bne   $t2, $zero, e5l
    addiu $t0, $t0, 400
    beq   $t9, $zero, snd_bad
    nop
    b     snd_good
    nop

# ── 6: gilda down ────────────────────────────────────────────────────────────────────
e6:
    lhu   $t0, 0x4346($s0)
    mtc1  $t0, $f0
    lwc1  $f1, 0x10($s2)           # GildaDownFrac (and the mtc1 gap)
    cvt.s.w $f0, $f0
    mul.s $f1, $f0, $f1
    sub.s $f0, $f0, $f1
    cvt.w.s $f0, $f0
    nop
    mfc1  $t1, $f0
    bgez  $t1, e6w
    nop
    or    $t1, $zero, $zero        # below 1: 0
e6w:
    b     snd_bad
    sh    $t1, 0x4346($s0)

# ── 7: a stat down, and the element/anti bytes ───────────────────────────────────────
e7:
    jal   0x1046F8
    nop
    andi  $s5, $v0, 3              # which stat: 0 attack, 1 endurance, 2 speed, 3 magic
    jal   0x1046F8
    nop
    lw    $t0, 0x20($s2)           # StatDownRange
    .word 0x0048001B               # divu v0,t0 — raw: keystone turns the mnemonic into a 4-word check-and-break macro whose branch lands ON the break
    lw    $t2, 0x1C($s2)           # StatDownMin
    mfhi  $t1
    addu  $t1, $t1, $t2            # the loss
    sll   $t3, $s5, 1
    addu  $t3, $t3, $s1
    lh    $t4, 4($t3)
    lw    $t9, 0x40($s2)           # Favour: the loss is a gain
    bne   $t9, $zero, e7g
    nop
    subu  $t4, $t4, $t1
    or    $t5, $zero, $zero        # the floor: 0…
    bne   $s5, $zero, e7f
    nop
    addiu $t5, $zero, 1            # …1 for attack
e7f:
    slt   $t6, $t4, $t5
    beq   $t6, $zero, e7w
    nop
    b     e7w
    or    $t4, $t5, $zero
e7g:
    addu  $t4, $t4, $t1
    slti  $t6, $t4, 100
    bne   $t6, $zero, e7w
    nop
    addiu $t4, $zero, 99           # capped
e7w:
    sh    $t4, 4($t3)
    addiu $a0, $s1, 0x17           # the five elements
    addiu $a1, $zero, 5
    jal   elem
    addiu $a2, $zero, 3
    addiu $a0, $s1, 0x1C           # anti bytes, three
    addiu $a1, $zero, 3
    jal   elem
    addiu $a2, $zero, 2
    addiu $a0, $s1, 0x1F           # …three more
    addiu $a1, $zero, 3
    jal   elem
    addiu $a2, $zero, 2
    addiu $a0, $s1, 0x22           # …and four
    addiu $a1, $zero, 4
    jal   elem
    addiu $a2, $zero, 2
    jal   attach
    nop
    lw    $t9, 0x40($s2)
    beq   $t9, $zero, snd_bad
    nop
    b     snd_good
    nop

# ── 8: max WHP down ──────────────────────────────────────────────────────────────────
e8:
    jal   0x1046F8
    nop
    lw    $t0, 0x28($s2)           # MaxWhpDownRange
    .word 0x0048001B               # divu v0,t0 — raw: keystone turns the mnemonic into a 4-word check-and-break macro whose branch lands ON the break
    lw    $t2, 0x24($s2)           # MaxWhpDownMin
    mfhi  $t1
    addu  $t1, $t1, $t2
    lh    $t3, 0xC($s1)
    subu  $t3, $t3, $t1            # the new max (before its floor)
    mtc1  $t3, $f0
    lwc1  $f1, 0x10($s1)           # WHP (and the mtc1 gap)
    cvt.s.w $f0, $f0
    nop
    .word 0x46010034               # c.olt.s f0,f1 — new max < WHP ?  (EE ordered compare; keystone's c.lt.s is not one the R5900 has)
    nop
    bc1f  e8f
    nop
    swc1  $f0, 0x10($s1)           # WHP down to it
e8f:
    slti  $t4, $t3, 1
    beq   $t4, $zero, e8w
    nop
    addiu $t3, $zero, 1
e8w:
    sh    $t3, 0xC($s1)
    jal   attach
    nop
    b     snd_bad
    nop

# ── 9: WHP divided ───────────────────────────────────────────────────────────────────
e9:
    lwc1  $f0, 0x10($s1)           # WHP
    lwc1  $f1, 0x2C($s2)           # WhpDivisor
    lui   $t0, 0x3F80
    mtc1  $t0, $f2                 # 1.0
    div.s $f0, $f0, $f1
    nop
    .word 0x46020034               # c.olt.s f0,f2 — below 1 ?
    nop
    bc1f  e9w
    nop
    mov.s $f0, $f2
e9w:
    swc1  $f0, 0x10($s1)
    jal   attach
    nop
    b     snd_bad
    nop

# ── the engine's sound for the outcome, and out ──────────────────────────────────────
snd_good:
    b     snd
    addiu $a0, $zero, 0xE1
snd_bad:
    addiu $a0, $zero, 0xE2
snd:
    addiu $a1, $zero, -1
    jal   0x15A6B0                 # SndSePlay(id, -1, 0)
    or    $a2, $zero, $zero
done:
    .word 0x7BBF0070               # lq ra,0x70(sp)
    .word 0x7BB60060               # lq s6,0x60(sp)
    .word 0x7BB50050               # lq s5,0x50(sp)
    .word 0x7BB40040               # lq s4,0x40(sp)
    .word 0x7BB30030               # lq s3,0x30(sp)
    .word 0x7BB20020               # lq s2,0x20(sp)
    .word 0x7BB10010               # lq s1,0x10(sp)
    .word 0x7BB00000               # lq s0,0x00(sp)
    jr    $ra
    addiu $sp, $sp, 0x90

# ── attach: the battle record rebuilt from the inventory one (what the engine does after a weapon change) ──
attach:
    sw    $ra, 0x80($sp)
    lw    $a0, -0x62FC($gp)        # NowWeaponHave
    jal   0x225AA0                 # SetWeaponAttachStatus
    nop
    lw    $ra, 0x80($sp)
    jr    $ra
    nop

# ── give: a0 = item id (0 = none), RewardCount of them into the bag, each only if the bag has room (GetItem's own count: the
# bag halfwords holding an item plus the active items' counts, against the capacity byte) — so no overflow slot is ever used ──
give:
    beq   $a0, $zero, giver
    sw    $ra, 0x80($sp)
    sw    $a0, 0x84($sp)
    lw    $t7, 0x48($s2)           # RewardCount
    sw    $t7, 0x88($sp)
gloop:
    lw    $t7, 0x88($sp)
    blez  $t7, giver               # all given
    addiu $t7, $t7, -1
    sw    $t7, 0x88($sp)
    or    $t0, $zero, $zero        # the count
    or    $t1, $zero, $zero        # i
    addiu $t2, $s0, 0x436E         # the bag's item halfwords, 0x67 of them
gcount:
    lh    $t3, 0($t2)
    slti  $t4, $t3, 0x84
    bne   $t4, $zero, gnext        # < 0x84: no item there
    addiu $t1, $t1, 1
    addiu $t0, $t0, 1
gnext:
    slti  $t4, $t1, 0x67
    bne   $t4, $zero, gcount
    addiu $t2, $t2, 2
    addiu $t2, $s0, 0x4362         # the three active-item slots: id, and its count six bytes on
    addiu $t1, $zero, 3
gact:
    lh    $t3, 0($t2)
    addiu $t4, $zero, -1
    beq   $t3, $t4, gactn
    nop
    lh    $t3, 6($t2)
    addu  $t0, $t0, $t3
gactn:
    addiu $t1, $t1, -1
    bne   $t1, $zero, gact
    addiu $t2, $t2, 2
    lb    $t5, 0x4360($s0)         # the bag's capacity
    addiu $t0, $t0, 1
    slt   $t6, $t5, $t0            # capacity < count + 1: full
    bne   $t6, $zero, giver
    nop
    or    $a0, $s0, $zero
    lw    $a1, 0x84($sp)
    jal   0x1BE060                 # GetItem(UserStatus, item, 0)
    or    $a2, $zero, $zero
    b     gloop
    nop
giver:
    lw    $ra, 0x80($sp)
    jr    $ra
    nop

# ── elem: one byte of the group at a0 (a1 bytes) −= (2 + rand % a2) × ElemDownMult, floor 0 — or, favoured, += it, capped 99 ──
elem:
    sw    $ra, 0x80($sp)
    sw    $a0, 0x84($sp)
    sw    $a1, 0x88($sp)
    sw    $a2, 0x8C($sp)
    jal   0x1046F8                 # which byte
    nop
    lw    $t0, 0x88($sp)
    .word 0x0048001B               # divu v0,t0 — raw: keystone turns the mnemonic into a 4-word check-and-break macro whose branch lands ON the break
    lw    $t2, 0x84($sp)
    mfhi  $t1
    addu  $t2, $t2, $t1
    sw    $t2, 0x84($sp)           # its address
    jal   0x1046F8                 # how much
    nop
    lw    $t0, 0x8C($sp)
    .word 0x0048001B               # divu v0,t0 — raw: keystone turns the mnemonic into a 4-word check-and-break macro whose branch lands ON the break
    lw    $t3, 0x3C($s2)           # ElemDownMult
    mfhi  $t1
    addiu $t1, $t1, 2
    mult  $t1, $t3
    lw    $t2, 0x84($sp)
    mflo  $t1
    lb    $t4, 0($t2)
    lw    $t9, 0x40($s2)           # Favour
    bne   $t9, $zero, elemg
    nop
    subu  $t4, $t4, $t1
    bgez  $t4, elemw
    nop
    b     elemw
    or    $t4, $zero, $zero
elemg:
    addu  $t4, $t4, $t1
    slti  $t6, $t4, 100
    bne   $t6, $zero, elemw
    nop
    addiu $t4, $zero, 99
elemw:
    sb    $t4, 0($t2)
    lw    $ra, 0x80($sp)
    jr    $ra
    nop
