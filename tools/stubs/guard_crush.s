# guard_crush.s — the GUARD GATE's first link: a hit marked as CRUSHING passes every guard window. Assembled at 0x001B4650 (DebugInfoCave.GuardCrush,
# in the free tail of dead DebugInfomationDraw). CheckDmg's guard-window hook (main 0x1DAC78, `j` in place of `addu at,v0,at`; its `lh` delay
# slot nop'd) lands here first: an entry whose +0x9C holds CodeCaves.CrushMark ("CRIK") reports "no window" — the cat cave's
# pass — and everything else jumps on to the gate's second link, guard_mask.s (0x1B4390: the windows the mod switches off per
# enemy), and from there to the cat's guard-bypass cave (dun 0x1DAC070: the pellet rules), each re-forming what it needs from
# a2/v1/s2 itself. The marked hits: the Divine Beast cat's (cat_pellet_follow.s and the mod's), the Terra Sword's drops, Big
# Bang's guard-breaking falloff (Hercules' strike). The mark shares SPIK's high half (0x4B49), which is all the no-drain caves test: a crushing
# hit of Ungaga's bills no weapon HP either. Clobbers at and v0 only, both dead at the hook (the cat cave's own rule).
#
# Zeroing an enemy's guard windows from the mod is a race a script can win: _SET_GUARD_FRAME re-registers them whenever its
# label runs. Here the attacker's entry decides, inside CheckDmg, on the frame the hit is judged.

    lw    $at, -0x6210($gp)        # NowColData
    sll   $v0, $s2, 5              # entry index × 0x20
    addu  $at, $at, $v0
    sll   $v0, $s2, 7              #            × 0x80  → × 0xA0 together
    addu  $at, $at, $v0            # the damage entry
    lw    $v0, 0x009C($at)         # the mod's mark
    lui   $at, 0x4B49
    ori   $at, $at, 0x5243         # CrushMark "CRIK"
    beq   $v0, $at, pass
    nop
    j     0x001B4390               # the gate's second link (guard_mask.s)
    nop
pass:
    j     0x001DAC80               # report "no window here" and let the damage through
    move  $v0, $zero               # (delay slot)
