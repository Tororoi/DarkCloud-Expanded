# eye_tint.s — a node drawn with a tint of its own: the Crystal Gemron's eyes and the Atla Gemron's forehead Atlamillia. Assembled at 0x0010A3C0
# (DeadChainCave.EyeTint: the dead sceCdReadChain body after freeze_break).
#
# A mesh reaches the screen through the virtual DrawVu1 slots of its visual: CrystalGemron / AtlaGemron give the node's visual (one object
# every unit of the species draws) a private copy of its class vtable whose two DrawVu1 slots enter here (NodeDrawHook: slot 6 at the
# entry, slot 7 at the entry + 0x10), the stock targets saved beside it. This adds the node's tint (three floats, on the 0–255 scale of
# mgRenderInfo.ambient, the vector MGSetAmbient writes) to the ambient's red, green and blue for this one draw and puts them back:
#  · +0x00 / +0x10 the eyes: CodeCaves.EyeTintStock, EyeTintColour (45, 70, 50);
#  · +0x20 / +0x30 the Atlamillia: CodeCaves.AtlamilliaTintStock, AtlamilliaTintColour (50, 50, 50).
# Arguments 5–8 ride in t0–t3 in this build (see cat_mask_tint.s), so only t4–t9, v0, v1 and f-registers are used before the call.

eyes6:
    lui   $t8, 0x01FB
    lw    $t9, -0x0840($t8)        # 0x01FAF7C0 CodeCaves.EyeTintStock: slot 6's own DrawVu1 (the uint* overload)
    j     body
    addiu $t8, $t8, -0x0888        # (delay) 0x01FAF778 CodeCaves.EyeTintColour
eyes7:
    lui   $t8, 0x01FB
    lw    $t9, -0x083C($t8)        # 0x01FAF7C4: slot 7's (the packet overload)
    j     body
    addiu $t8, $t8, -0x0888        # (delay)
lens6:
    lui   $t8, 0x01FB
    lw    $t9, -0x1AF0($t8)        # 0x01FAE510 CodeCaves.AtlamilliaTintStock: slot 6's
    j     body
    addiu $t8, $t8, -0x087C        # (delay) 0x01FAF784 CodeCaves.AtlamilliaTintColour
lens7:
    lui   $t8, 0x01FB
    lw    $t9, -0x1AEC($t8)        # 0x01FAE514: slot 7's
    j     body
    addiu $t8, $t8, -0x087C        # (delay)
body:
    addiu $sp, $sp, -0x20
    sw    $ra, 0x0000($sp)
    lui   $v1, 0x01C7
    lwc1  $f0, 0x56B0($v1)         # mgRenderInfo.ambient: red,
    lwc1  $f1, 0x56B4($v1)         # green,
    lwc1  $f2, 0x56B8($v1)         # blue
    swc1  $f0, 0x0004($sp)
    swc1  $f1, 0x0008($sp)
    swc1  $f2, 0x000C($sp)
    lwc1  $f3, 0x0000($t8)         # the tint: red,
    lwc1  $f4, 0x0004($t8)         # green,
    lwc1  $f5, 0x0008($t8)         # blue
    add.s $f0, $f0, $f3
    add.s $f1, $f1, $f4
    add.s $f2, $f2, $f5
    swc1  $f0, 0x56B0($v1)
    swc1  $f1, 0x56B4($v1)
    jalr  $t9                      # the visual's own DrawVu1
    swc1  $f2, 0x56B8($v1)         # (delay) tinted for this draw
    lui   $v1, 0x01C7
    lwc1  $f0, 0x0004($sp)
    lwc1  $f1, 0x0008($sp)
    lwc1  $f2, 0x000C($sp)
    swc1  $f0, 0x56B0($v1)         # the ambient's own back
    swc1  $f1, 0x56B4($v1)
    swc1  $f2, 0x56B8($v1)
    lw    $ra, 0x0000($sp)
    jr    $ra
    addiu $sp, $sp, 0x20
