# blow_dir.s — an item bomb throws the player away from its blast. Assembled at 0x0010A7D8 (DeadCdCave.BlowDir: the body of
# sceCdGetToc, a libcdvd routine the game links but never calls — no jal, j, pointer or lui/addiu reference anywhere).
# BtCheckDamageProc copies the hit entry's velocity into blowVelo (sceVu0CopyVector(&blowVelo, &entry.velocity), v1 = the entry)
# before a knockdown (dun 0x1DBB9B4: unitBlowActionRot turns him against it, and it is the speed he flies at, decaying 0.018 a
# frame) and before a guarded hit's slide (dun 0x1DBB82C: blowVelo / 10). DunPatches points both calls here. SetBombEffect plants
# its entry with velocity (1, 0, 0) — every bomb threw him toward world +X, forward or back as it happened — and is the only planter
# that sets the entry's ready phase to 10, so for such an entry blowVelo becomes the unit vector from the blast to him (level);
# a blast centred on him (a shot that burst on contact) throws him straight back from the way he faces (his frame's world z row,
# negated). Every other entry is copied as before.
# Clobbers t0–t3, f0–f3 (free at both call sites).

    lw    $t0, 0x0074($v1)         # the entry's ready phase
    addiu $t1, $zero, 10
    bne   $t0, $t1, copy
    lui   $t2, 0x01EA              # (delay) CharaMain 0x01EA1D20
    lwc1  $f0, 0x1D30($t2)         # his x (CharaMain.pos)
    lwc1  $f1, 0x0000($v1)
    sub.s $f0, $f0, $f1            # dx
    lwc1  $f2, 0x1D38($t2)         # his z
    lwc1  $f1, 0x0008($v1)
    sub.s $f2, $f2, $f1            # dz
    mul.s $f1, $f0, $f0
    mul.s $f3, $f2, $f2
    add.s $f1, $f1, $f3            # d², a positive float: compares as an int
    mfc1  $t0, $f1
    lui   $t1, 0x3C23
    ori   $t1, $t1, 0xD70A         # 0.01
    slt   $t1, $t0, $t1
    beq   $t1, $zero, norm
    nop
    lui   $t3, 0x002A
    lw    $t3, 0x3500($t3)         # CharaFrame (0x002A3500): his frame
    lwc1  $f0, 0x0170($t3)         # its world z row: the way he faces
    lwc1  $f2, 0x0178($t3)
    neg.s $f0, $f0
    neg.s $f2, $f2
    mul.s $f1, $f0, $f0
    mul.s $f3, $f2, $f2
    add.s $f1, $f1, $f3
norm:
    lui   $t1, 0x3F80
    mtc1  $t1, $f3                 # 1.0
    .word 0x460118D6               # rsqrt.s f3, f3, f1 (EE: fd = fs / sqrt(ft)) — 1 / |d|
    mul.s $f0, $f0, $f3
    mul.s $f2, $f2, $f3
    swc1  $f0, 0x0000($a0)
    sw    $zero, 0x0004($a0)
    jr    $ra
    swc1  $f2, 0x0008($a0)
copy:
    j     0x00121830               # sceVu0CopyVector, as before
    nop
