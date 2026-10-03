# confuse_name.s — the weapon status window's SPECIAL list names ability bit 14 (0x4000, "Confuse") from message 0x45. Assembled
# at 0x001B561C (DebugIfCave.ConfuseName, the free tail of dead DebugInfomationIF after the fall-drive cave).
# MenuClsMes::NowWeaponStatus (main 0x20B7F0) walks the ability bits and stores each set bit's name as message (bit + 0x45) of the
# system bank; bit 14 would land on 0x53 ("Slot 1", used elsewhere), so its `addiu a0,a3,0x45` (0x20B8A8) jumps here — its delay
# slot, `lw t0,0x1C(s0)`, does not read a0 — and bit 14 gets message 0x45 instead (free in the bank: the ISO patch adds
# "Confuse" there). The loop bound there and in WeaponOptionStatusDraw (the icons) is raised to reach bit 14.

    addiu $a0, $a3, 0x0045         # the vanilla: the bit's name message
    addiu $at, $zero, 14
    bne   $a3, $at, back
    nop
    addiu $a0, $zero, 0x0045       # bit 14 (Confuse): message 0x45
back:
    j     0x0020B8B0               # past the hook and its delay slot
    nop
