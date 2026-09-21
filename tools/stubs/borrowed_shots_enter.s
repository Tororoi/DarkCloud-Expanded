# borrowed_shots_enter.s — a shot effect borrowed by one of Xiao's abilities, kept entered in the MAIN-CHARACTER effect
# instance (CharaMainEffect, main-ELF BSS 0x01E8DA60 — the CSHOT_EFFECT the floor loader fills with the active character's
# wep_eff effect and the dungeon loop steps and draws through gp−0x6304; Xiao's is loaded with mgan01 and never fired).
# Assembled in two pieces (the band has no 484 B gap): the HEAD at 0x01FB1ED0 (ElfCave.BorrowedShotsEnter, ≤ 304 B) and,
# past the `#SPLIT` marker, the TAIL at 0x01FB3F40 (ElfCave.BorrowedShotsEnterTail, ≤ 192 B) — build_ee_stubs.py assembles
# the two as one image with the gap blanked and writes each piece to its own .bin; the head ends in a `b tail`, so
# nothing ever runs through the gap (other caves live there). The head is the dungeon step loop's `jal step__5CSHOT` chain
# (dun 0x1DB874C, DunPatches): calls PropPelletFollow — which runs the cat's chain and the displaced step__5CSHOT — with
# a0 (the pool) passed through, then, once a frame, checks the instance. Its config pointer is ours and the state word says
# entered → nothing to do. Its config pointer is NOT ours → the loader ran: a new floor, the first time — or a dungeon menu,
# whose character reload refills the instance with her own effect. The region is reused when it is still ours: its first
# 16 bytes (kept out of the allocator) still hold the signature written at the carve — "BSHT" and the pool's used counter
# right after it — and the pool's counter is at or past that mark (the pool is a bump allocator that only ever grows
# within a dungeon run; a rewind that then regrows over the region overwrites the signature). Otherwise a fresh one is
# carved (used += the mod's reserve; no room → quiet). A region is only ever reused when its capacity
# covers the reserve the mod wrote for the config now wanted: the game's allocator answers an overflow with an endless loop,
# so a config needing more than the region holds gets a fresh, larger region instead (the old one is left behind).
# Then, and when the mod asks for a re-entry (state 0: another config in the same copy), the instance is re-entered the way
# MainChara_Effect enters a character's effect on a weapon change: Initialize (the instance emptied), the texture manager's
# block 0x10 (the main-character effect's) cleared, the file LoadFile'd into the loader's read buffer, and
# Entry2(instance, cfg, read_buffer, 0x10, OUR allocator, 6) — the region is a CDataAlloc2 the block owns, reset to 0 used
# before every entry, so a re-entry reuses the same memory and the monster pool's bump counter is bumped once per floor.
# The pack's five slots are never touched: Xiao's effects live beside them.
#
# Block (0x01FAEF40): +0x00 "SHOT" (else nothing is done)   +0x10 the BT_SHOT_EFFECT copy   +0x250 state (mod writes 0 =
#   enter; cave writes 1 = entered, −1 = no room / failed, and drops the magic)   +0x258 the file path (≤ 63 chars)
#   +0x298 our CDataAlloc2 {base, 0, used, cap} (cave)   +0x2A8 LoadFile's size out   +0x2AC the reserve, units of 16 B (mod)
#   +0x2B0 the pool's used counter right after our carve (cave) — the mark, also stored in the region's signature
#   +0x2B4 the instance to enter (mod): CharaMainEffect for Xiao's abilities; the SECOND instance (0x01E97BC0, the
#   broken-weapon shot's, otherwise idle) for Ruby's stolen shot, which fires beside her own   +0x2B8 1 when it is the
#   main instance (mod): only then is texture block 0x10 cleared before the entry and the live pointer set after it
# Globals: 0x01E8DA60 CharaMainEffect   *0x002A2384 read_buffer   0x01F066D0 the monster pool's CDataAlloc2 (+0 base, +8 used,
#   +0xC cap)   0x01C75870 the texture manager   gp−0x6304 the live main-character effect pointer

    addiu $sp, $sp, -0x20
    sw    $ra, 0x0010($sp)
    sw    $s0, 0x0014($sp)
    sw    $s1, 0x0018($sp)
    jal   0x01FB1E30               # PropPelletFollow → CatCopyQueue → CatPelletFollow → step__5CSHOT(pool), as before
    nop
    lui   $s0, 0x01FA
    ori   $s0, $s0, 0xEF40         # the block
    lw    $t1, 0x0000($s0)
    lui   $t2, 0x544F
    ori   $t2, $t2, 0x4853         # "SHOT"
    bne   $t1, $t2, ret            # nothing seeded
    nop
    lw    $s1, 0x02B4($s0)         # the instance the mod named (CharaMainEffect, or the second one for Ruby's stolen shot)
    lw    $t6, 0x0000($s1)         # its config pointer
    addiu $t7, $s0, 0x10           # ours
    lw    $t4, 0x0250($s0)         # the state word
    bne   $t6, $t7, native         # not ours: the loader ran since we last entered
    nop
    addiu $t5, $zero, 1
    beq   $t4, $t5, ret            # ours and entered: the common frame
    nop
    b     fits                     # ours, but the mod wants the copy re-entered (another config): the region must hold it
    nop
native:
    lw    $t1, 0x0298($s0)         # our allocator's base …
    beq   $t1, $zero, carve        # (none yet)
    nop
    addiu $t1, $t1, -0x10          # … sits 16 bytes above the region's signature
    lw    $t2, 0x0000($t1)
    lui   $t3, 0x5448
    ori   $t3, $t3, 0x5342         # "BSHT"
    bne   $t2, $t3, carve          # overwritten: the pool was rewound and regrown over it
    nop
    lw    $t2, 0x0004($t1)
    lw    $t3, 0x02B0($s0)         # the mark
    bne   $t2, $t3, carve
    nop
    lui   $t8, 0x01F0
    ori   $t8, $t8, 0x66D0         # the monster pool
    lw    $t9, 0x0008($t8)         # its used counter
    slt   $t5, $t9, $t3
    bne   $t5, $zero, carve        # below the mark: rewound, the region is above the pool's top — carve at the new top
    nop
fits:
    lw    $t2, 0x02AC($s0)         # what the wanted config needs (the reserve it was, or will be, carved with)
    lw    $t3, 0x02A4($s0)         # what the region holds …
    addiu $t3, $t3, 1              # … plus the unit its signature took: the reserve it was carved with
    slt   $t5, $t3, $t2
    beq   $t5, $zero, reenter      # it fits: reuse the region
    nop
carve:
    lui   $t8, 0x01F0
    ori   $t8, $t8, 0x66D0         # the monster pool
    lw    $t9, 0x0008($t8)         # its used counter
    lw    $t1, 0x000C($t8)         # its capacity
    lw    $t2, 0x02AC($s0)         # the reserve
    addu  $t3, $t9, $t2            # what the pool would hold with our region
    slt   $t5, $t1, $t3
    bne   $t5, $zero, noroom       # over capacity: leave the pool alone (the game's own overflow is a hang)
    nop
    sw    $t3, 0x0008($t8)         # the region is ours for the floor
    sw    $t3, 0x02B0($s0)         # … and this is where the pool stands with it (the mark)
    lw    $t1, 0x0000($t8)         # pool base
    sll   $t9, $t9, 4              # used × 16
    addu  $t1, $t1, $t9            # the region
    lui   $t5, 0x5448
    ori   $t5, $t5, 0x5342         # "BSHT"
    sw    $t5, 0x0000($t1)         # its signature: the magic …
    sw    $t3, 0x0004($t1)         # … and the mark
    addiu $t1, $t1, 0x10           # the allocator starts above the signature
    sw    $t1, 0x0298($s0)         # our allocator: base
    sw    $zero, 0x029C($s0)
    addiu $t2, $t2, -1
    b     tail                     # the rest sits in the tail piece
    nop
#SPLIT
tail:
    sw    $t2, 0x02A4($s0)         #                cap (one unit is the signature's)
reenter:
    sw    $zero, 0x02A0($s0)       #                used = 0: the region is reused whole
    jal   0x001AE440               # Initialize__12CSHOT_EFFECT(instance): emptied, config pointer 0
    move  $a0, $s1
    lw    $t1, 0x02B8($s0)         # the main instance's (1): its texture block cleared and the live pointer set; else neither
    beq   $t1, $zero, load
    lui   $a0, 0x01C7              # (delay slot; harmless either way)
    ori   $a0, $a0, 0x5870         # the texture manager
    jal   0x00133700               # DeleteTextureBlock(mgr, 0x10): the previous effect's textures
    addiu $a1, $zero, 0x10
    lui   $a0, 0x01C7
    jal   0x001337F0               # CleanUpBuffer(mgr)
    ori   $a0, $a0, 0x5870
    lui   $a0, 0x01C7
    jal   0x00133A60               # CleanUpTextureList(mgr)
    ori   $a0, $a0, 0x5870
load:
    addiu $a0, $s0, 0x258          # the path
    lui   $a1, 0x002A
    lw    $a1, 0x2384($a1)         # read_buffer
    jal   0x0013F360               # LoadFile(path, read_buffer, &size)
    addiu $a2, $s0, 0x2A8
    jal   0x00153F70               # wait_now_loading_vsync()
    nop
    move  $a0, $s1                 # the instance
    addiu $a1, $s0, 0x10           # the config copy
    lui   $a2, 0x002A
    lw    $a2, 0x2384($a2)         # read_buffer
    addiu $a3, $zero, 0x10         # the main-character effect's texture block
    addiu $t0, $s0, 0x298          # our allocator (the fifth argument)
    jal   0x001AD260               # Entry2__12CSHOT_EFFECT → 0 = failed
    addiu $t1, $zero, 6            # (delay slot) six sub-shots
    beq   $v0, $zero, noroom
    addiu $t1, $zero, 1            # (delay slot; noroom writes its own)
    sw    $t1, 0x0250($s0)         # entered
    lw    $t1, 0x02B8($s0)
    beq   $t1, $zero, ret          # the second instance is stepped and drawn as it is; the live pointer stays the character's
    nop
    sw    $s1, -0x6304($gp)        # the live main-character effect pointer names the instance (the loop steps and draws it)
    b     ret
    nop
noroom:
    addiu $t1, $zero, -1
    sw    $t1, 0x0250($s0)
    sw    $zero, 0x0000($s0)       # the block goes quiet until the mod seeds again
ret:
    lw    $ra, 0x0010($sp)
    lw    $s0, 0x0014($sp)
    lw    $s1, 0x0018($sp)
    jr    $ra
    addiu $sp, $sp, 0x20           # (delay slot)
