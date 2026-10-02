# guard_mask.s — the GUARD GATE's second link: the guard windows the mod switches off, per enemy. Assembled at 0x001B4390
# (DebugInfoCave.GuardMask, in dead DebugInfomationDraw after the Steel Slingshot cave). guard_crush.s jumps here for every
# entry it does not pass; this passes the window when CodeCaves.GuardMask's byte for the enemy (0x01FAFCB0 + slot) has the
# window's bit set (bit w = window w; 7 = no window of that enemy blocks), else it goes on to the cat's guard-bypass cave.
# The mod writes the bytes (GuardGate): every enemy's 7 while Dark Cloud, 7th Heaven, a Solar Shot's blinding or Big Bang's
# whirl breaks every guard; a mimic's wake window while the Dusack is out. The windows themselves are never touched, so a
# script re-registering them (`_SET_GUARD_FRAME`) changes nothing.
# At the hook a2 = the window index and v1 = MainMonstorUnit (0x01DF87D0) + slot × 0x20. Clobbers at and v0 only.

    lui   $at, 0x01DF
    ori   $at, $at, 0x87D0         # MainMonstorUnit
    subu  $v0, $v1, $at            # slot × 0x20
    srl   $v0, $v0, 5              # the slot
    lui   $at, 0x01FB
    addu  $at, $at, $v0
    lbu   $v0, -0x0350($at)        # GuardMask[slot] (0x01FAFCB0)
    srlv  $v0, $v0, $a2            # this window's bit
    andi  $v0, $v0, 1
    bne   $v0, $zero, pass
    nop
    j     0x01DAC070               # the cat's guard-bypass cave (the pellet rules, then the vanilla window load)
    nop
pass:
    j     0x001DAC80               # report "no window here" and let the damage through
    move  $v0, $zero               # (delay slot)
