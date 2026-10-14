# cat_load.s — the Divine Beast cat loaded natively from its own pack (dun/monstor/dbcat.chr) into chara slot 1, inside a region of
# the character heap reserved behind Xiao's data. Assembled at 0x01FB2480 (ElfCave.CatLoad), two entries:
#
#   step (+0x00)  — the dungeon step loop's once-per-frame chain (PropPelletFollow → here → CatPelletFollow). Idle unless the request
#                   word asks: 1 = load (→ 2 loaded, −1 failed), 3 = unload (→ 0). Its tail calls CatPalette and CatGlowPalette and
#                   jumps on to CatPelletFollow, a0 and ra as the chain gave them.
#   reserve       — LoadChara2's `lui at,0x01F0; lw a0,0x6668(at)` (dun 0x1DBA894, just before the weapon base is set; DunPatches makes
#                   it `jal reserve; nop`): when Xiao is the character loaded (s5 = 1) it allocates the region from CharaModelBuffer —
#                   XiaoRegion.Units, or less when the pool has less left past the weapons' and effects' allowance (XiaoRegion.Spare
#                   units; an overrun of that pool is a silent spin) — so the weapons and effects that follow start after it, and
#                   records its base and size; any other character, or no room, leaves no region (base 0, loads refused). Either
#                   way a cat loaded before is released — a floor load or a party switch has just rebuilt the heap under it. Redoes
#                   the two words.
#
# Slot 1 is also one of the six NPC slots the floor's event scripts load characters into (NPCUnit, texture groups 0x20 + i): a load is
# refused while slot 1 holds a model that is not the cat's, and a release touches slot 1 and block 0x21 only while slot 1 still holds
# the cat it loaded (+0x28) — an event's character there is left alone.
#
# The load is SetupBaseModel's own sequence on slot 1 with the region's allocator: Initialize, InitializeTexAnime (none), LoadFile into
# the read buffer + wait, LoadPackData3 (texture block 0x21 — slot 1's group — with dont_delete, so IMG_END keeps the block where it
# was put), then the main character's frame attribute (SetAttr mask 4, +8 = 0). The textures get a VRAM span of their own: block
# 0x21's base and top are set to W = (cursor − S) & ~0x1F under the texture manager's downward cursor (+0x14) — S = the pack's
# VRAM blocks — and the cursor is lowered to W after, so nothing allocated later lands on them. (The mod checks first that no
# texture block reaches above W, which would make the game's own EndEnterTextureBlock hang.) An unload deletes block 0x21 and puts
# the cursor back if it still stands at W. t0..t9 only (constants rebuilt after each call), a0 kept on the stack.
#
# Request block, guest 0x01FAE620 (CodeCaves.CatLoad), addressed off t0 = 0x01FB0000 (block = t0 − 0x19E0):
#   +0x00 request/status   +0x04 region base (reserve)   +0x08 region units (reserve)   +0x0C W (0 = no VRAM held)
#   +0x10 the region's CDataAlloc2 {base, —, used, max}  +0x20 the cursor before W     +0x24 S (the mod)
#   +0x28 the cat's root (slot 1 + 0xBC when it loaded; 0 = none)   +0x40 the pack's path (the mod)
# TexManager (0x01C75870) is addressed off t4 = 0x01C70000: cursor 0x5884, block 0x21's base/top 0x6064/0x6068.

step:
    lui   $t0, 0x01FB
    lw    $t1, -0x19E0($t0)        # the request
    addiu $t2, $zero, 1
    beq   $t1, $t2, load
    addiu $t2, $zero, 3            # (delay)
    bne   $t1, $t2, tail           # every frame but a request's: nothing touched
    nop
    addiu $sp, $sp, -0x20          # ── unload ──
    sw    $ra, 0x00($sp)
    jal   release
    sw    $a0, 0x04($sp)           # (delay)
    b     done
    sw    $zero, -0x19E0($t0)      # (delay) unloaded

load:
    addiu $sp, $sp, -0x20
    sw    $ra, 0x00($sp)
    sw    $a0, 0x04($sp)
    lui   $t2, 0x01EB
    lw    $t3, -0x6644($t2)        # slot 1's model
    lw    $t1, -0x19B8($t0)        # the cat's
    beq   $t3, $t1, lfree          # the cat's own (or none of either)
    nop
    bne   $t3, $zero, fail         # an event's character holds slot 1
    nop
lfree:
    jal   release                  # whatever an earlier load held
    nop
    lw    $t3, -0x19DC($t0)        # the region (0 = Xiao is not the one loaded)
    beq   $t3, $zero, fail
    lui   $t4, 0x01C7              # (delay)
    lw    $t5, 0x5884($t4)         # the downward cursor
    lw    $t6, -0x19BC($t0)        # S
    subu  $t6, $t5, $t6
    addiu $t7, $zero, -0x20
    and   $t6, $t6, $t7            # W
    sw    $t5, -0x19C0($t0)        # the cursor, for the unload
    sw    $t6, -0x19D4($t0)        # W
    sw    $t6, 0x6064($t4)         # block 0x21's base
    sw    $t6, 0x6068($t4)         #             top
    sw    $t3, -0x19D0($t0)        # the region's allocator: base,
    sw    $zero, -0x19C8($t0)      #   used,
    lw    $t1, -0x19D8($t0)
    sw    $t1, -0x19C4($t0)        #   max
    lui   $a0, 0x01EB
    jal   0x001399D0               # Initialize__10CCharacterFv (slot 1)
    addiu $a0, $a0, -0x6700        # (delay) 0x01EA9900
    lui   $a0, 0x01EB
    addiu $a0, $a0, -0x6700
    jal   0x00137FF0               # InitializeTexAnime__10CCharacterFP13CTexAnimeDatai (a1 any buffer: none of them)
    or    $a2, $zero, $zero        # (delay)
    lui   $a0, 0x01FB
    addiu $a0, $a0, -0x19A0        # the path (+0x40)
    lw    $a1, -0x746C($gp)        # the read buffer
    jal   0x0013F360               # LoadFile__FPcPvPi
    or    $a2, $zero, $zero        # (delay)
    jal   0x00153F70               # wait_now_loading_vsync__Fv
    nop
    lui   $a0, 0x01EB
    addiu $a0, $a0, -0x6700        # slot 1
    lw    $a1, -0x746C($gp)
    lui   $a2, 0x002A
    addiu $a2, $a2, -0x3028        # "info.cfg" (0x29CFD8)
    lui   $a3, 0x01FB
    addiu $a3, $a3, -0x19D0        # the region's allocator (+0x10): models
    addiu $t0, $zero, 0x21         # texture block
    or    $t1, $a3, $zero          # …and pictures
    addiu $t2, $zero, 1            # dont_delete
    jal   0x00139890               # LoadPackData3__10CCharacterFPUiPcP14CDataAlloc2_1_iP14CDataAlloc2_1_ii
    or    $t3, $zero, $zero        # (delay) def_vtype
    lui   $t0, 0x01EB
    lw    $a0, -0x6644($t0)        # the root (slot 1 + 0xBC)
    beq   $a0, $zero, lfail
    sb    $zero, 0x18($sp)         # (delay) a CFrameAttr whose +8 is 0 (mask 4 reads nothing else)
    lui   $t0, 0x01FB
    sw    $a0, -0x19B8($t0)        # the cat's root
    addiu $a1, $sp, 0x10
    addiu $a2, $zero, 1
    jal   0x00128B50               # SetAttr__6CFrameFR10CFrameAttrii
    addiu $a3, $zero, 4            # (delay)
    lui   $t0, 0x01FB
    lw    $t6, -0x19D4($t0)
    lui   $t4, 0x01C7
    sw    $t6, 0x5884($t4)         # the cursor down to W
    addiu $t1, $zero, 2
    b     done
    sw    $t1, -0x19E0($t0)        # (delay) loaded
lfail:
    jal   release
    nop
fail:
    addiu $t1, $zero, -1
    sw    $t1, -0x19E0($t0)
done:
    lw    $ra, 0x00($sp)
    lw    $a0, 0x04($sp)
    addiu $sp, $sp, 0x20
tail:
    # The cape/mask element colour and the glow disc's palette ride this once-per-frame hook too (both leaves through ra).
    addiu $sp, $sp, -0x10
    sw    $ra, 0x0($sp)
    jal   0x01FB2718               # CatPalette's entry
    nop
    jal   0x01FB3A80               # CatGlowPalette
    nop
    lw    $ra, 0x0($sp)
    j     0x01FB0D90               # CatPelletFollow — a0 and ra untouched
    addiu $sp, $sp, 0x10           # (delay)

# While slot 1 still holds the cat: slot 1 without a model and block 0x21 deleted (its textures gone from the list). Then the
# cursor back if it still stands at W. Returns t0 = 0x01FB0000.
release:
    addiu $sp, $sp, -0x10
    sw    $ra, 0x00($sp)
    lui   $t0, 0x01FB
    lw    $t1, -0x19B8($t0)        # the cat's root
    lui   $t2, 0x01EB
    lw    $t3, -0x6644($t2)        # slot 1's model
    bne   $t1, $t3, rvram          # not the cat (an event's character, or nothing): leave slot 1 and block 0x21 alone
    sw    $zero, -0x19B8($t0)      # (delay)
    beq   $t1, $zero, rvram
    sw    $zero, -0x6644($t2)      # (delay) slot 1's model
    lui   $a0, 0x01C7
    ori   $a0, $a0, 0x5870
    jal   0x00133700               # DeleteTextureBlock__15CTextureManagerFi
    addiu $a1, $zero, 0x21         # (delay)
    lui   $t0, 0x01FB
rvram:
    lw    $t6, -0x19D4($t0)
    beq   $t6, $zero, rnone
    sw    $zero, -0x19D4($t0)      # (delay)
    lui   $t4, 0x01C7
    lw    $t5, 0x5884($t4)         # the cursor
    bne   $t5, $t6, rnone
    lw    $t5, -0x19C0($t0)        # (delay)
    sw    $t5, 0x5884($t4)
rnone:
    lw    $ra, 0x00($sp)
    jr    $ra
    addiu $sp, $sp, 0x10           # (delay)

reserve:
    addiu $sp, $sp, -0x10
    sw    $ra, 0x00($sp)
    jal   release                  # the cat that was
    nop
    sw    $zero, -0x19E0($t0)      # nothing loaded now
    addiu $t2, $zero, 1
    bne   $s5, $t2, rdone
    sw    $zero, -0x19DC($t0)      # (delay) no region
    lui   $a0, 0x01F0
    ori   $a0, $a0, 0x6660         # CharaModelBuffer
    lw    $t1, 0x08($a0)
    lw    $a1, 0x0C($a0)
    subu  $a1, $a1, $t1
    addiu $a1, $a1, -28000         # the room left past the weapons' and effects' allowance (XiaoRegion.Spare)
    lui   $t1, 0x0001
    ori   $t1, $t1, 0x0C8E         # at most XiaoRegion.Units (68750)
    slt   $t2, $t1, $a1
    movn  $a1, $t1, $t2
    blez  $a1, rdone
    sw    $a1, -0x19D8($t0)        # (delay) its units
    jal   0x001278A0               # Alloc__14CDataAlloc2_1_Fi
    nop
    lui   $t0, 0x01FB
    sw    $v0, -0x19DC($t0)        # the region
rdone:
    lw    $ra, 0x00($sp)
    addiu $sp, $sp, 0x10
    lui   $at, 0x01F0
    jr    $ra
    lw    $a0, 0x6668($at)         # (delay) the two words this replaced
