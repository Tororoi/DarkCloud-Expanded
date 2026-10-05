# element_menu_ruby.s — the element picker's effect reload (ElfElementMenuPatches): Ruby's armlet shot is a different effect model per
# element, loaded with the character; after a pick her shot would draw nothing until the next menu visit. Assembled at 0x0027D270
# (SmoothRestCave.ElementMenuRuby, zero words of the dead SmoothRest body after the bait keep). element_menu.s's close cave jumps
# here on the way out of the picker when the leader is Ruby, with CharaChangeLoop's `jal MenuTextureReload` argument in a0 and
# its return in ra: the reload the dungeon's own character load does — the effect for the live weapon's element read with the
# synchronous LoadFile into the dungeon's staging buffer and entered with MainChara_Effect — then MenuTextureReload as before.
# Overlay functions (dungeon only, which is where the picker lives): Get_Main_EffectPtr 0x01DBA060, MainChara_Effect 0x01DBA230
# (the ELF symbols are the overlay's run-time addresses: dun.bin loads at 0x01DABD00). Clobbers caller-saved registers only.

    addiu $sp, $sp, -0x70
    sw    $ra, 0x6C($sp)
    sw    $a0, 0x68($sp)           # MenuTextureReload's texture block
    addiu $a0, $zero, 3            # Ruby
    lui   $t0, 0x01EA
    lb    $a1, 0x75A6($t0)         # the live weapon's element
    jal   0x01DBA060               # Get_Main_EffectPtr(chara, element) → the effect (its name first)
    nop
    sw    $v0, 0x64($sp)
    addu  $a0, $sp, $zero          # path[96]
    lui   $a1, 0x002A
    addiu $a1, $a1, -0x3AB0        # "dun/mainchara/wep_eff/%s.chr" (0x0029C550)
    addu  $a2, $v0, $zero
    jal   0x00105058               # sprintf
    nop
    addu  $a0, $sp, $zero
    lui   $t0, 0x002A
    lw    $a1, 0x2384($t0)         # read_buffer (the dungeon's staging buffer; the ring's pack in it is spent)
    addiu $a2, $sp, 0x60           # &size
    jal   0x0013F360               # LoadFile(path, buffer, &size) — synchronous
    nop
    jal   0x00153F70               # wait_now_loading_vsync, as the character load does
    nop
    lw    $a0, 0x64($sp)
    lui   $t0, 0x002A
    lw    $a1, 0x2384($t0)
    addu  $a2, $zero, $zero
    jal   0x01DBA230               # MainChara_Effect(effect, buffer, 0): the resident shot effect re-entered
    nop
    lw    $a0, 0x68($sp)
    lw    $ra, 0x6C($sp)
    addiu $sp, $sp, 0x70
    j     0x0022D0E0               # MenuTextureReload, the call the close cave stood in for
    nop
