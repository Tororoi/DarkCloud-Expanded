# ally_swing.s — the summoned ally's attack spheres never hit the player. Assembled at 0x001192E4 (DeadSifElfCave, after ally_shots.s).
# ElfAllyPatches points CMonstorUnit::CheckDmg's `jal Set__14CCollisionData` (0x1DA098, s5 = the stepping manager) here, where a unit's
# open attack sphere goes into the collision pool each frame: the ally's goes in with mask 0 (t0 → entry +0x48; bit 0 = hurts the
# player), so the player's hit test passes it by — its run (the tackle clip) beside Ruby included. Its owner stays slot 0's (200);
# owner 200 with mask 0 is the ally's alone, which the mod's friendly fire lands on enemies (ConfusionAlly). t9 only.

swing:
    lui   $t9, 0x01FB
    lw    $t9, -0x0808($t9)        # CodeCaves.AllyManager
    bne   $s5, $t9, sgo
    nop
    or    $t0, $zero, $zero        # hurts no one
sgo:
    j     0x001B57A0               # Set__14CCollisionDataFPfiiffiiii
    nop
