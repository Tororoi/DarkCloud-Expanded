# ally_hooks.s — the summoned ally's small hooks into the engine's monster code, one host for all (assembled at 0x001192E4: DeadSifElfCave,
# after ally_shots.s). Each opens with the ally test (s5 or a0 = the stepping manager against CodeCaves.AllyManager) and leaves the
# floor's units as vanilla:
#   swing   (+0x00) CheckDmg's attack sphere into the pool (0x1DA098): the ally's goes in with mask 0 — it never hits the player.
#   mask    (+0x1C) CheckDmg's incoming-hit test (0x1DAA7C): the ally's unit also takes bit 8, the hits the mod plants on it.
#   blocked (+0x3C) ally_collide.s's stop: a floor unit the ally's body stopped noted in CodeCaves.AllyBlocked.
#   pin     (+0x64) ally_brain.s each frame: the ally's position copied to CodeCaves.AllyPin's (the summon smoke kept on it).

# CheckDmg's `jal Set__14CCollisionData` (s5 = the stepping manager), where a unit's open attack sphere goes into the collision pool
# each frame: the ally's goes in with mask 0 (t0 → entry +0x48; bit 0 = hurts the player), so the player's hit test passes it by — its
# run (the tackle clip) beside Ruby included. Its owner stays slot 0's (200); owner 200 with mask 0 is the ally's alone, which the
# mod's friendly fire lands on enemies (ConfusionAlly). t9 only.
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

# ally_collide.s's stop (a0 = the moving unit's manager, t1 = CodeCaves.AllyManager): a FLOOR unit the ally's body stopped gets its
# bit in CodeCaves.AllyBlocked (0x01FAF7FC; the mod reads and clears it — an enemy it blocks goes after it, ConfusionAlly); then back
# to ally_collide.s's cdone (0x00118E1C), which returns.
blocked:
    beq   $a0, $t1, bdone          # the ally's own unit stopped: nothing to note
    lw    $t4, 0x90($a0)           # (delay) the stopped unit's slot
    addiu $t5, $zero, 1
    sllv  $t5, $t5, $t4
    lui   $t6, 0x01FB
    lw    $t7, -0x0804($t6)
    or    $t7, $t7, $t5
    sw    $t7, -0x0804($t6)
bdone:
    j     0x00118E1C               # ally_collide.s cdone
    nop

# ally_brain.s each frame while the ally stands, a0 = its manager: while CodeCaves.AllyPin (0x01FAFFD8) names a position — the summon
# smoke's sub-shot, set by the mod as the pockle appears in it and cleared when it is gone — its unit 0's position (the manager's
# +0x1FCE0) is copied there, the smoke kept on it frame by frame. t0..t5.
pin:
    lui   $t0, 0x01FB
    lw    $t1, -0x0028($t0)        # AllyPin
    beq   $t1, $zero, pdone
    lui   $t2, 0x0002              # (delay)
    addu  $t2, $a0, $t2
    lw    $t3, -0x0320($t2)        # x
    lw    $t4, -0x031C($t2)        # height
    lw    $t5, -0x0318($t2)        # y
    sw    $t3, 0x00($t1)
    sw    $t4, 0x04($t1)
    sw    $t5, 0x08($t1)
pdone:
    jr    $ra
    nop
