# ally_hooks.s — the summoned ally's small hooks into the engine's monster code, one host for all (assembled at 0x001192E4: DeadSifElfCave,
# after ally_shots.s). Each opens with the ally test (s5 or a0 = the stepping manager against CodeCaves.AllyManager) and leaves the
# floor's units as vanilla:
#   swing   (+0x00) CheckDmg's attack sphere into the pool (0x1DA098): while an ally stands, every sphere is recorded and the ally's
#           goes in with mask 0 — it never hits the player (ally_record.s).
#   mask    (+0x1C) CheckDmg's incoming-hit test (0x1DAA7C): the ally's unit also takes bit 8, the hits the mod plants on it.
#   blocked (+0x3C) ally_collide.s's stop: a floor unit the ally's body stopped noted in CodeCaves.AllyBlocked.
#   pin     (+0x64) ally_brain.s each frame: the ally's position copied to CodeCaves.AllyPin's (the summon smoke kept on it).
#   full    (+0x98) a full event's start (dun 0x1DB4A30): the ally ends before the event empties the monsters' textures and shot pack.

# CheckDmg's `jal Set__14CCollisionData` (s5 = the stepping manager), where a unit's open attack sphere goes into the collision pool
# each frame. While an ally stands, ally_record.s records every one (the friendly fire tests each frame's) and gives the ally's mask 0
# (t0 → entry +0x48; bit 0 = hurts the player), so the player's hit test passes it by — its run (the tackle clip) beside the summoner
# included; its owner stays slot 0's (200). With none standing, straight to Set. t9 only.
swing:
    lui   $t9, 0x01FB
    lw    $t9, -0x0808($t9)        # CodeCaves.AllyManager
    .word 0x1720C8D6               # bne t9, zero, 0x0010B648: an ally stands — ally_record.s
    nop
    j     0x001B57A0               # Set__14CCollisionDataFPfiiffiiii
    nop
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

# A full event's start (the floor system script's kind 1, dun 0x1DB4A30: its debug printf's `jal`, a0 = the message): the engine then
# empties the monsters' texture group 42, the shot pack and the floor's manager, and hands the monster pool to the event's script. A
# standing ally ends first (ally_despawn.s, event 4), while its texture entries and pack slots are still its own. The printf is not
# made. t0.
full:
    lui   $t0, 0x01FB
    lw    $t0, -0x0808($t0)        # CodeCaves.AllyManager
    beq   $t0, $zero, fdone
    nop
    j     0x00117080               # ally_despawn.s, returning to the event
    addiu $a0, $zero, 4            # (delay)
fdone:
    jr    $ra
    nop
