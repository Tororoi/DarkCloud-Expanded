# freeze_break.s — a hit breaks the player's freeze. Assembled at 0x0010A360 (DeadChainCave.FreezeBreak: the dead sceCdReadChain body
# after eye_tint). BtCheckDamageProc's one `jal StatusErrCheck` (dun 0x1DBB1F4, a0 = 8: the Stamina test that opens the hit's
# work, after the hit has registered and before its ailments are dealt) comes here (DunPatches): when the active character is
# frozen (UserStatus +0x42C8 + chara × 4, Freeze 0x04), the freeze ends as its timer would end it — the bit gone, the shared
# ailment timer (+0x42E0 + chara × 2: the freeze's own, as freezing clears Stamina and Stamina cannot land on a freeze) at 0 and
# BtActStatus.movement_locked (0x01DC4518) 0 — except that any other ailment (Curse) stays — and the hit itself (s4: the caller's
# collision entry, its flags at +0x50) loses its Freeze bits (0x100, 0x100000), so it only breaks the freeze; its other ailments
# are dealt as before. Then StatusErrCheck(8) runs as before. Uses only t-registers and v0 before the tail call; a0 and s4 kept.

    lui   $v0, 0x002A
    lw    $v0, 0x3468($v0)         # UserStatus
    beq   $v0, $zero, done
    nop
    lb    $t0, 0x0004($v0)         # cur_chara
    sll   $t1, $t0, 2
    addu  $t1, $t1, $v0
    lw    $t2, 0x42C8($t1)         # his ailments
    andi  $t3, $t2, 0x0004
    beq   $t3, $zero, done         # not frozen
    xori  $t2, $t2, 0x0004         # (delay) the freeze gone
    sw    $t2, 0x42C8($t1)
    sll   $t1, $t0, 1
    addu  $t1, $t1, $v0
    sh    $zero, 0x42E0($t1)       # its timer
    lui   $t1, 0x01DC
    sw    $zero, 0x4518($t1)       # BtActStatus.movement_locked
    lw    $t2, 0x0050($s4)         # the hit's flags (s4: the caller's hit entry)
    lui   $t3, 0xFFEF
    ori   $t3, $t3, 0xFEFF         # all but Freeze (0x100) and the sure Freeze (0x100000)
    and   $t2, $t2, $t3
    sw    $t2, 0x0050($s4)         # this hit only breaks the freeze
done:
    j     0x001B1930               # StatusErrCheck(8), as before
    nop
