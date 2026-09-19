# gemron_shots_enter.s — the Gemrons' elemental shot, kept entered in the floor's shot pack. Assembled at 0x01FB3F40
# (ElfCave.GemronShotsEnter). The head of the dungeon step loop's `jal step__5CSHOT` chain (dun 0x1DB874C, DunPatches):
# calls PropPelletFollow — which runs the cat's chain and the displaced step__5CSHOT — with a0 (the pool) passed through,
# then, once a frame, makes sure the config the mod keeps in GemronShotBlock is in the pack: if the slot recorded for it no
# longer holds it (a floor load rebuilt the pack) or none is recorded (the mod seeded a new element), it records the
# monster pool's fill level and enters the config the way the loader enters a species' —
# Entry__17CSHOT_EFFECT_PACK(NowShotEffect, cfg, read_buffer, 0x26, the monster pool, 6) — recording the slot. The fill
# level is what lets the mod REUSE that slot for the next element: it empties the slot, rewinds the pool to the level,
# and writes −1. −1 back from Entry (the pack's five slots taken) clears the block's magic, so nothing is retried until
# the mod seeds again (a floor change or a new element).
#
# Block (0x01FAEF40): +0x00 "GEMS" (else nothing is done)   +0x10 the BT_SHOT_EFFECT copy   +0x250 its slot (cave;
#   −1 = enter it, ≥ 0 = entered)   +0x254 the monster pool's used counter before the entry (cave)
# Globals: *0x002A35D8 NowShotEffect   *0x002A2384 read_buffer   0x01F066D0 the monster pool's CDataAlloc2 (+8 used)

    addiu $sp, $sp, -0x20
    sw    $ra, 0x0010($sp)
    jal   0x01FB1E30               # PropPelletFollow → CatCopyQueue → CatPelletFollow → step__5CSHOT(pool), as before
    nop
    lui   $t0, 0x01FA
    ori   $t0, $t0, 0xEF40         # the block
    lw    $t1, 0x0000($t0)
    lui   $t2, 0x534D
    ori   $t2, $t2, 0x4547         # "GEMS"
    bne   $t1, $t2, ret            # nothing seeded
    nop
    lui   $t3, 0x002A
    lw    $t3, 0x35D8($t3)         # NowShotEffect (set at dungeon init, before this loop ever runs)
    lw    $t4, 0x0250($t0)         # the slot recorded
    bltz  $t4, enter               # none: enter it
    nop
    ori   $t5, $zero, 0xA160
    .word 0x018D0018               # mult  $t4, $t5   (slot × 0xA160)
    .word 0x00006812               # mflo  $t5
    addu  $t5, $t3, $t5            # that slot
    lw    $t6, 0x0000($t5)         # its config pointer
    addiu $t7, $t0, 0x10           # ours
    beq   $t6, $t7, ret            # still entered: the common frame
    nop
enter:
    lui   $t8, 0x01F0
    ori   $t8, $t8, 0x66D0         # the monster pool
    lw    $t9, 0x0008($t8)         # its used counter …
    sw    $t9, 0x0254($t0)         # … recorded for the mod's rewind
    move  $a0, $t3                 # the pack
    addiu $a1, $t0, 0x10           # the config copy
    lui   $a2, 0x002A
    lw    $a2, 0x2384($a2)         # read_buffer
    addiu $a3, $zero, 0x26
    move  $t0, $t8                 # the monster pool (the fifth argument)
    jal   0x001AE4C0               # Entry__17CSHOT_EFFECT_PACK → the slot, or −1
    addiu $t1, $zero, 6            # (delay slot) the sixth
    lui   $t0, 0x01FA
    ori   $t0, $t0, 0xEF40
    bgez  $v0, record
    nop
    sw    $zero, 0x0000($t0)       # no room: the block goes quiet until the mod seeds again
record:
    sw    $v0, 0x0250($t0)
ret:
    lw    $ra, 0x0010($sp)
    jr    $ra
    addiu $sp, $sp, 0x20           # (delay slot)
