# follow.s — one point the engine carries with a unit every frame. Assembled at 0x001B4690 (DebugInfoCave.Follow, the free tail
# of dead DebugInfomationDraw). The fall-drive cave leaves through here; this leaves for the blade-spin cave. Once a dungeon frame,
# at the end of the camera pass. Caller-saved registers only (t0–t2, f0–f1); no calls, no frame.
#
# CodeCaves.Follow (0x01FAFC90): +0 the source (guest address of a position vec3: x, height, y; 0 = off), +4 the destination
# (guest), +8/+0xC/+0x10 the offsets added. The Terra Sword's stars ride the bonked enemy's own position (CharObjects.PosAddr —
# per slot: a model root is shared by every unit of a species, so a parent link follows whichever was drawn last).

    lui   $t0, 0x01FB
    lw    $t1, -0x0370($t0)        # source
    beq   $t1, $zero, out
    nop
    lw    $t2, -0x036C($t0)        # destination
    lwc1  $f0, 0x0000($t1)
    lwc1  $f1, -0x0368($t0)
    add.s $f0, $f0, $f1
    swc1  $f0, 0x0000($t2)
    lwc1  $f0, 0x0004($t1)
    lwc1  $f1, -0x0364($t0)
    add.s $f0, $f0, $f1
    swc1  $f0, 0x0004($t2)
    lwc1  $f0, 0x0008($t1)
    lwc1  $f1, -0x0360($t0)
    add.s $f0, $f0, $f1
    swc1  $f0, 0x0008($t2)
out:
    j     0x001B4FC0               # the blade-spin cave (then the WHP bill, the chain's tail)
    nop
