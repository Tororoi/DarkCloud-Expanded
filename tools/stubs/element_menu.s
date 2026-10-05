# element_menu.s — the dungeon quick-change menu (SELECT: CharaChangeLoop/Key/Draw) doubling as the weapon's element picker when
# opened with D-pad UP. One image in two pieces (build_ee_stubs.py `#SPLIT`): the HEAD at 0x0022B410 (DebugItemCave.ElementMenuHead,
# the free tail of the dead DebugItemGetKey body, ≤ 416 B) holds the hook caves; the TAIL at 0x0022B5B8 (DebugItemCave.ElementMenuSheetName,
# the dead DebugItemGetDraw body after its `jr ra`, ≤ 508 B) holds the sheet name, the draw cave, the element test, pre and close. Nothing runs
# through the gap. ElfElementMenuPatches writes both and points five main-ELF sites + DunPatches two overlay words here.
#
# Words (CodeCaves, the runtime-data span): 0x01FAF4E0 ElementMenuMode (1 while the menu is the element picker),
# 0x01FAF4E4 ElementMenuPick (element + 1 the player confirmed; the mod applies it and zeroes it), 0x01FAF4E8 ElementMenuTex (the
# `wepicon` CTexture of the current opening, looked up once), 0x01FAF4EC ElementMenuMap (up to six bytes: the elements on offer, in
# order — the ring shows only these, as the character ring shows only the party). Elements: 0..4 = the five stones (items 81..85:
# wepicon cells 120..124 — the attachment icons start at cell 120, row 15 — 32×32 at u = element*32, v = 480), 5 = None (cell 125,
# the dummy item 86's cell right after them, where ElementMenuIconBake writes a grey copy of the synth sphere's cell 129).
# ChangeMenu @0x01DA8D30: +0 selected, +2 party_size, +3 step, +0x4C unk_4c.
# Clobbers only caller-saved registers (t0..t8, a-regs the hooked calls already pass); `start` restores at for its caller's delay slot.

# ── trig: dun MoveChara's `jal Down(SELECT)` (0x1DB14A8; a0 = &GamePad, a1 = 0x100). Returns non-zero for SELECT (character
#    menu) or D-pad UP (element menu) and records which in ElementMenuMode. DunPatches also relaxes the "party of two" test. ──
trig:
    addiu $sp, $sp, -16
    sw    $ra, 12($sp)
    sw    $a0, 8($sp)
    jal   0x0012B870               # CGamePad::Down(SELECT)
    nop
    lui   $t0, 0x01FB
    bne   $v0, $zero, trig_sel
    nop
    lw    $a0, 8($sp)
    addiu $a1, $zero, 0x1000       # D-pad UP
    jal   0x0012B870
    nop
    lui   $t0, 0x01FB
    sltu  $t1, $zero, $v0
    sw    $t1, -0x0B20($t0)        # ElementMenuMode = UP pressed
    b     trig_done
    nop
trig_sel:
    sw    $zero, -0x0B20($t0)      # SELECT: the character menu
trig_done:
    lw    $ra, 12($sp)
    addiu $sp, $sp, 16
    jr    $ra
    nop

# ── xkey: CharaChangeKey's SELECT-step `jal Down(X)` (0x229360). In element mode a press records the highlighted element, writes
#    it to the live weapon and sends the menu into its LOADED step (closes next frame, as a character change does); returns 0 then,
#    so the key handler sees no press. ──
xkey:
    addiu $sp, $sp, -16
    sw    $ra, 12($sp)
    jal   0x0012B870               # CGamePad::Down(X)
    nop
    beq   $v0, $zero, xkey_ret
    nop
    lui   $t0, 0x01FB
    lw    $t1, -0x0B20($t0)
    beq   $t1, $zero, xkey_ret     # character mode: X as in retail
    nop
    lui   $t2, 0x01DB
    lb    $t3, -0x72D0($t2)        # ChangeMenu.selected = a position on the ring
    addu  $t0, $t0, $t3
    lb    $t3, -0x0B14($t0)        # ElementMenuMap[position] = the element
    lui   $t0, 0x01FB
    addiu $t4, $t3, 1
    sw    $t4, -0x0B1C($t0)        # ElementMenuPick = element + 1
    lui   $t5, 0x01EA
    sb    $t3, 0x75A6($t5)         # the live weapon's selected element (5 = none)
    addiu $t4, $zero, 6
    sb    $t4, -0x72CD($t2)        # step = LOADED
    addiu $t4, $zero, 0x10
    sw    $t4, -0x7284($t2)        # unk_4c = 16: the LOADED step closes on its next frame
    addiu $a0, $zero, 1
    jal   0x0022CEF0               # ComMenuSePlay(confirm)
    nop
    addu  $v0, $zero, $zero
xkey_ret:
    lw    $ra, 12($sp)
    addiu $sp, $sp, 16
    jr    $ra
    nop

# ── start: StartQuickChange's `lb v0,0x5(v0)` (0x2289D0, the party size into v0; its delay slot is `lui at,0x1db` for the
#    store that follows). Element mode: ElementMenuMap = the elements on offer (stones the weapon has an amount of, None where
#    allowed — None alone if nothing else), the ring sized to them and opened on the weapon's current element. ──
start:
    lb    $v0, 5($v0)              # party_size
    lui   $t0, 0x01FB
    lw    $t1, -0x0B20($t0)
    beq   $t1, $zero, start_done
    nop
    sw    $zero, -0x0B18($t0)      # ElementMenuTex = 0
    addiu $sp, $sp, -16
    sw    $ra, 12($sp)
    lui   $t2, 0x01EA
    lb    $t9, 0x75A6($t2)         # the live weapon's element
    sltiu $t4, $t9, 5
    bne   $t4, $zero, start_cur
    nop
    addiu $t9, $zero, 5            # none, or unset (-1) → None
start_cur:
    addu  $t6, $zero, $zero        # offered so far
    addu  $t7, $zero, $zero        # the element under test
    addu  $t5, $zero, $zero        # the current element's position (0 when it is not offered)
start_loop:
    addu  $a0, $t7, $zero
    jal   valid
    nop
    beq   $v0, $zero, start_next
    nop
    lui   $t0, 0x01FB
    addu  $t0, $t0, $t6
    sb    $t7, -0x0B14($t0)        # ElementMenuMap[offered] = element
    bne   $t7, $t9, start_count
    nop
    addu  $t5, $t6, $zero
start_count:
    addiu $t6, $t6, 1
start_next:
    addiu $t7, $t7, 1
    sltiu $t8, $t7, 6
    bne   $t8, $zero, start_loop
    nop
    bne   $t6, $zero, start_have
    nop
    lui   $t0, 0x01FB              # nothing at all: None alone
    addiu $t8, $zero, 5
    sb    $t8, -0x0B14($t0)
    addiu $t6, $zero, 1
start_have:
    lw    $ra, 12($sp)
    addiu $sp, $sp, 16
    lui   $t2, 0x01DB
    sb    $t5, -0x72D0($t2)        # ChangeMenu.selected
    addu  $v0, $t6, $zero          # party_size = the elements on offer
start_done:
    lui   $at, 0x01DB
    jr    $ra
    nop

#SPLIT
# ── TAIL @0x0022B5B8: the sheet name (+0), then draw (+0xC), valid, pre and close. ──
.word 0x69706577                   # "wepi"
.word 0x006E6F63                   # "con\0"
.word 0x00000000

# ── draw: CharaChangeDraw's portrait `jal DrawMenu2DSprite` (0x229C24; a0 sheet, a1 &dst{x,y,w,h}, a2 &src{u,v,w,h}, a3/t0/t1 shade,
#    t2 alpha; s2 = the cell). Element mode: the cell's element from ElementMenuMap, drawn from the stone row of wepicon, 32×32
#    centred in the 48 px cell. ──
draw:
    lui   $t3, 0x01FB
    lw    $t4, -0x0B20($t3)
    bne   $t4, $zero, draw_elem
    nop
    j     0x0022CF90               # DrawMenu2DSprite as in retail
    nop
draw_elem:
    addiu $sp, $sp, -48
    sw    $ra, 44($sp)
    sw    $a1, 16($sp)
    sw    $a2, 20($sp)
    sw    $t2, 24($sp)
    lw    $v0, -0x0B18($t3)        # ElementMenuTex
    bne   $v0, $zero, draw_tex
    nop
    lui   $a0, 0x01C7
    addiu $a0, $a0, 0x5870         # &TexManager
    lui   $a1, 0x0022
    ori   $a1, $a1, 0xB5B8         # "wepicon"
    addiu $a2, $zero, -1
    jal   0x001312D0               # CTextureManager::GetTexture(name, -1)
    nop
    lui   $t3, 0x01FB
    sw    $v0, -0x0B18($t3)
draw_tex:
    addu  $a0, $v0, $zero
    lw    $a1, 16($sp)
    lw    $a2, 20($sp)
    lw    $t2, 24($sp)
    addiu $a3, $zero, 0x80         # every offered cell is lit
    addu  $t0, $a3, $zero
    addu  $t1, $a3, $zero
    lui   $t3, 0x01FB
    addu  $t3, $t3, $s2
    lb    $t4, -0x0B14($t3)        # ElementMenuMap[cell] = the element
    sll   $t4, $t4, 5
    sw    $t4, 0($a2)              # src u = element * 32: wepicon cells 120..125
    addiu $t4, $zero, 480
    sw    $t4, 4($a2)              # src v = wepicon row 15
    addiu $t4, $zero, 32
    sw    $t4, 8($a2)
    sw    $t4, 12($a2)
    sw    $t4, 8($a1)              # dst 32×32 …
    sw    $t4, 12($a1)
    lw    $t5, 0($a1)
    addiu $t5, $t5, 8
    sw    $t5, 0($a1)              # … 8 px in from the cell's corner
    lw    $t5, 4($a1)
    addiu $t5, $t5, 8
    sw    $t5, 4($a1)
    lw    $ra, 44($sp)
    addiu $sp, $sp, 48
    j     0x0022CF90
    nop

# ── valid: a0 = element 0..5 → v0 = 1 when it is on offer. Stones need an amount on the weapon; None is barred for Ruby's
#    armlets and Osmond's machine-gun mode (they always carry an element). ──
valid:
    sltiu $t0, $a0, 5
    beq   $t0, $zero, valid_none
    nop
    lui   $t1, 0x01EA
    addu  $t1, $t1, $a0
    lbu   $v0, 0x75A7($t1)         # the weapon's amount of that element
    sltu  $v0, $zero, $v0
    jr    $ra
    nop
valid_none:
    lui   $t1, 0x002A
    lw    $t1, 0x2F80($t1)         # ChangeStatusDataPt
    lb    $t2, 4($t1)              # cur_chara
    addiu $t3, $zero, 3
    beq   $t2, $t3, valid_no       # Ruby
    nop
    addiu $t3, $zero, 5
    bne   $t2, $t3, valid_yes
    nop
    lui   $t1, 0x01DC
    lw    $t1, 0x4520($t1)         # Osmond's machine-gun mode
    bne   $t1, $zero, valid_no
    nop
valid_yes:
    addiu $v0, $zero, 1
    jr    $ra
    nop
valid_no:
    addu  $v0, $zero, $zero
    jr    $ra
    nop

# ── pre: CharaChangeKey's cursor-move `jal CharaChangeInitToGL(buf, selected)` (0x2296D4): no character preload in element mode. ──
pre:
    lui   $t0, 0x01FB
    lw    $t0, -0x0B20($t0)
    bne   $t0, $zero, pre_skip
    nop
    j     0x0020E5B0
    nop
pre_skip:
    jr    $ra
    nop

# ── close: CharaChangeLoop's first `jal MenuTextureReload` on the way out (0x228DD4): the picker flag drops with the menu; when the
#    picker closes on Ruby, element_menu_ruby.s reloads her shot effect first (0x27D270, SmoothRestCave.ElementMenuRuby). ──
close:
    lui   $t0, 0x01FB
    lw    $t1, -0x0B20($t0)
    sw    $zero, -0x0B20($t0)
    beq   $t1, $zero, close_plain
    nop
    lui   $t2, 0x002A
    lw    $t2, 0x2F80($t2)         # ChangeStatusDataPt
    lb    $t2, 4($t2)              # cur_chara
    addiu $t3, $zero, 3
    beq   $t2, $t3, close_ruby
    nop
close_plain:
    j     0x0022D0E0
    nop
close_ruby:
    j     0x0027D270
    nop
