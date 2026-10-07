# bomb_tint.s — the Bomb Gemron's big bomb reddens as its fuse burns, the bomb alone. Assembled at 0x0010A1D0 (DeadChainCave.BombTint:
# sceCdReadChain, which nothing in the ELF or its overlays calls or points at).
#
# A monster's tint (its palette, CMonstorUnit::PalletSet) is the ambient for its WHOLE draw, and a mesh's material colour lives in
# one buffer every unit of the species draws by reference, so neither can redden one bomb. A mesh does reach the screen through the
# virtual DrawVu1 slots of its visual, though: BombGemron gives the bomb's visual (the tama1__m frame's, shared by every Bomb Gemron)
# a private copy of its class vtable (CodeCaves.BombTintVtable) whose two DrawVu1 slots enter here, with the stock targets in
# CodeCaves.BombTintStock. The monster draw sets NowMonstorUnit->current_monster before each unit's Step and Draw, so this reads
# the unit being drawn, takes the red from its motion's frame, adds it to the ambient's red (mgRenderInfo.ambient, the vector
# MGSetAmbient writes) for this one draw, and puts the ambient back:
#  · the self-destruct (motion 14): 0 before its guard loop (207), 150 at its blast (217), linear between;
#  · the death (motion 11): 0 before 105, 150 at its blast (122), linear between; the death loop (12): 150;
#  · anything else: 0.
# 150 is the Big Bang hanging bomb's red (BigBangShot.PulseRed), on the ambient's 0–255 scale. Frames are positive floats,
# compared as ints. Arguments 5–8 ride in t0–t3 in this build (see cat_mask_tint.s), so only t4–t8, v0, v1 and f-registers
# are used before the call.

entry6:
    lui   $t9, 0x01FB
    j     body
    lw    $t9, -0x0870($t9)        # (delay) 0x01FAF790 CodeCaves.BombTintStock: slot 6's own DrawVu1 (the uint* overload)
entry7:
    lui   $t9, 0x01FB
    j     body
    lw    $t9, -0x086C($t9)        # (delay) 0x01FAF794: slot 7's (the packet overload)
body:
    addiu $sp, $sp, -0x10
    sw    $ra, 0x0000($sp)
    mtc1  $zero, $f1               # the red to add
    lui   $v1, 0x002A
    lw    $v1, 0x34D0($v1)         # NowMonstorUnit
    beq   $v1, $zero, add
    nop
    lw    $t4, 0x0090($v1)         # current_monster
    addiu $t5, $zero, 0x3510
    mult  $t4, $t5
    mflo  $t4
    addu  $t4, $t4, $v1
    lui   $t5, 0x0001
    ori   $t5, $t5, 0xFD60         # 0x1FD60: the unit block → its model block (ModelScaleOffsets.ModelFromUnit)
    addu  $t4, $t4, $t5
    lw    $t5, 0x0BD8($t4)         # the motion playing
    lw    $t6, 0x0260($t4)         # its frame
    addiu $t7, $zero, 12
    beq   $t5, $t7, full           # the death loop: red to the end
    addiu $t7, $zero, 14           # (delay)
    bne   $t5, $t7, death
    lui   $t7, 0x434F              # (delay) 207.0: the self-destruct's guard loop, its fuse
    lui   $t8, 0x4359              # 217.0: its blast
    b     window
    lui   $v0, 0x4170              # (delay) 15.0 = 150 / 10 frames
death:
    addiu $t8, $zero, 11
    bne   $t5, $t8, add
    lui   $t7, 0x42D2              # (delay) 105.0: the death's fuse
    lui   $t8, 0x42F4              # 122.0: its blast
    lui   $v0, 0x410D
    ori   $v0, $v0, 0x2D2D         # 8.8235 = 150 / 17 frames
window:
    slt   $t5, $t6, $t7
    bne   $t5, $zero, add          # before the fuse: none
    slt   $t5, $t6, $t8            # (delay)
    beq   $t5, $zero, full         # at or past the blast: all of it
    mtc1  $t6, $f2                 # (delay)
    mtc1  $t7, $f3
    mtc1  $v0, $f4
    sub.s $f2, $f2, $f3
    b     add
    mul.s $f1, $f2, $f4            # (delay) (frame − fuse start) × red per frame
full:
    lui   $t5, 0x4316              # 150.0
    mtc1  $t5, $f1
add:
    lui   $v1, 0x01C7
    lwc1  $f0, 0x56B0($v1)         # mgRenderInfo.ambient, red
    swc1  $f0, 0x0004($sp)
    add.s $f0, $f0, $f1
    jalr  $t9                      # the visual's own DrawVu1
    swc1  $f0, 0x56B0($v1)         # (delay) reddened for this draw
    lui   $v1, 0x01C7
    lwc1  $f0, 0x0004($sp)
    swc1  $f0, 0x56B0($v1)         # the ambient's own red back
    lw    $ra, 0x0000($sp)
    jr    $ra
    addiu $sp, $sp, 0x10
