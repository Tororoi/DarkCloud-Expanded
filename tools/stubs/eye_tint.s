# eye_tint.s — the Crystal Gemron's eyes tinted, the eyes alone. Assembled at 0x0010A3C0 (DeadChainCave.EyeTint: the dead
# sceCdReadChain body after freeze_break).
#
# The eyes are a node of their own (CrystalGemronBake splits their triangles out of the head), and a mesh reaches the screen through
# the virtual DrawVu1 slots of its visual: CrystalGemron gives the eyes' visual (one object every Crystal Gemron draws) a private copy
# of its class vtable (CodeCaves.EyeTintVtable) whose two DrawVu1 slots enter here, the stock targets in CodeCaves.EyeTintStock. This
# adds 45, 70 and 50 to the ambient's red, green and blue (mgRenderInfo.ambient, the vector MGSetAmbient writes; its 0–255 scale) for this one
# draw and puts them back. Arguments 5–8 ride in t0–t3 in this build (see cat_mask_tint.s), so only t4–t8, v0, v1 and f-registers
# are used before the call.

entry6:
    lui   $t9, 0x01FB
    j     body
    lw    $t9, -0x0840($t9)        # (delay) 0x01FAF7C0 CodeCaves.EyeTintStock: slot 6's own DrawVu1 (the uint* overload)
entry7:
    lui   $t9, 0x01FB
    j     body
    lw    $t9, -0x083C($t9)        # (delay) 0x01FAF7C4: slot 7's (the packet overload)
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
    lui   $t4, 0x4234              # 45.0: red
    mtc1  $t4, $f3
    lui   $t4, 0x428C              # 70.0: green
    mtc1  $t4, $f4
    lui   $t4, 0x4248              # 50.0: blue
    mtc1  $t4, $f5
    nop                            # (mtc1's latency)
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
