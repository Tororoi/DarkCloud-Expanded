# ally_swing.s — the summoned ally's attack spheres never hit the player. Assembled at 0x001192E4 (DeadSifElfCave, after ally_shots.s).
# ElfAllyPatches points CMonstorUnit::CheckDmg's `jal Set__14CCollisionData` (0x1DA098, s5 = the stepping manager) here, where a unit's
# open attack sphere goes into the collision pool each frame: the ally's goes in with mask 0 (t0 → entry +0x48; bit 0 = hurts the
# player), so the player's hit test passes it by — its run (the tackle clip) beside Ruby included. Its owner stays slot 0's (200);
# owner 200 with mask 0 is the ally's alone, which the mod's friendly fire lands on enemies (ConfusionAlly). t9 only. And mask (+0x1C)
# below: what the ally's unit takes.

swing:
    lui   $t9, 0x01FB
    lw    $t9, -0x0808($t9)        # CodeCaves.AllyManager
    bne   $s5, $t9, sgo
    nop
    or    $t0, $zero, $zero        # hurts no one
sgo:
    j     0x001B57A0               # Set__14CCollisionDataFPfiiffiiii
    nop

# CheckDmg's incoming-hit test (0x1DAA7C: `jal mask` with the `lw v0,0x48(a1)` it replaced in the delay slot, so v0 = the entry's
# mask): v0 = mask & 2 for a floor unit (the player's hits) as before; the ally's unit also takes bit 8 (0x100 — the hits the mod
# plants on its body, ConfusionAlly), which no floor unit does, so an enemy overlapping it never takes a hit meant for it. t8/t9
# (CheckDmg uses neither).
mask:
    lui   $t9, 0x01FB
    lw    $t9, -0x0808($t9)        # CodeCaves.AllyManager
    beq   $s5, $t9, mally
    andi  $t8, $v0, 0x0102         # (delay) the player's hits and its own
    jr    $ra
    andi  $v0, $v0, 2              # (delay)
mally:
    jr    $ra
    or    $v0, $t8, $zero
