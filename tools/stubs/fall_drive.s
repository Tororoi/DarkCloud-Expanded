# fall_drive.s — the blade fall's MODE 4: falling AND following, with per-frame drive rows. Assembled at 0x001B54B0
# (DebugIfCave.FallDrive, in dead DebugInfomationIF). The blade-fall cave (ElfWeaponPatches.PatchBladeFall) handles modes 1
# (falling) and 3 (following) and leaves through here; this cave acts only on flag 4 and then goes on to the blade-spin cave
# (the chain's next link), so modes 1/2/3 are untouched. Once a dungeon frame, at the end of the camera pass. Caller-saved
# registers only (t0–t8, f0–f9); no calls, no frame.
#
# Mode 4, each frame (CodeCaves.BladeFall: +0 flag, +4 y, +8 vy, +0xC g, +0x10 stop, +0x14 unit, +0x18/+0x1C x/z offsets;
# CodeCaves.FallDrive: +0 stop source, +4 stop offset, rows from +0x10, the armed hop at +0xB0):
#  · the stop follows a float when a source is set (stop = *src + offset: a hurt sphere's centre height + its radius + the
#    dropped thing's — the nut lands ON the head, the frame it reaches it);
#  · vy += g, y −= vy; at or below the stop: y = stop, and flag 2 (landed) — mode 1's fall — unless a HOP is armed (+0xB0):
#    then, the same frame, the fall becomes the hop: +0xB4 set (the mod's signal), vy, the x/z drift and the stop from the hop's
#    words (+0xB8 … +0xC4), no unit, no stop source — still mode 4 (the nut's bounce off a head, with no frame lost to the mod);
#  · the copy's chara slot (BladeProp, slot 3, position guest 0x01EAC250) gets the height, and across the ground the followed
#    point's x/z plus the offsets (unit ≠ 0: a unit's or a sphere's position) — or, with no unit, its own x/z plus the offsets:
#    a drift a frame (the nut's sideways hop);
#  · five DRIVE ROWS (0x20 each: +0 dst guest (0 = off), +4 count ≥ 1, +8 a, +0xC b, +0x10 lo, +0x14 hi): clamp(a + b·y, lo,
#    hi) written as a float to dst, count consecutive words — the dropped thing's scale, its shadow's scale and refresh words,
#    the target's darkness, all linear in the fall height.
# FPU compares are .word: the EE's c.lt.s / c.le.s condition codes are 0x34 / 0x36 (keystone emits the MIPS 0x3C / 0x3E).

    lui   $t0, 0x01FB
    lw    $t1, -0x0750($t0)        # BladeFall flag (0x01FAF8B0)
    addiu $t2, $zero, 4
    bne   $t1, $t2, out
    nop
    lw    $t3, -0x0440($t0)        # FallDrive stop source (0x01FAFBC0)
    beq   $t3, $zero, nosrc
    nop
    lwc1  $f0, 0x0000($t3)         # the followed height
    lwc1  $f1, -0x043C($t0)        # + the stop offset
    add.s $f0, $f0, $f1
    swc1  $f0, -0x0740($t0)        # → stop
nosrc:
    lwc1  $f0, -0x074C($t0)        # y
    lwc1  $f1, -0x0748($t0)        # vy
    lwc1  $f2, -0x0744($t0)        # g
    lwc1  $f3, -0x0740($t0)        # stop
    add.s $f1, $f1, $f2            # vy += g
    sub.s $f0, $f0, $f1            # y −= vy
    .word 0x46030036               # c.le.s $f0,$f3 — y ≤ stop ?
    nop
    bc1f  stored
    nop
    mov.s $f0, $f3                 # y = stop
    lw    $t9, -0x0390($t0)        # a hop armed (FallDrive +0xB0)?
    beq   $t9, $zero, land
    nop
    sw    $zero, -0x0390($t0)      # the hop, this very frame: disarmed…
    addiu $t9, $zero, 1
    sw    $t9, -0x038C($t0)        # …the mod told (+0xB4: bonked)…
    lwc1  $f1, -0x0388($t0)        # …vy the hop's (+0xB8; y stays the contact height)
    lw    $t9, -0x0384($t0)
    sw    $t9, -0x0738($t0)        # x drift a frame (+0xBC)
    lw    $t9, -0x0380($t0)
    sw    $t9, -0x0734($t0)        # z drift a frame (+0xC0)
    lw    $t9, -0x037C($t0)
    sw    $t9, -0x0740($t0)        # stop: where it comes down (+0xC4)
    sw    $zero, -0x073C($t0)      # no unit: it drifts
    b     stored
    sw    $zero, -0x0440($t0)      # no stop source (delay slot)
land:
    addiu $t2, $zero, 2
    sw    $t2, -0x0750($t0)        # landed
stored:
    swc1  $f0, -0x074C($t0)
    swc1  $f1, -0x0748($t0)
    lui   $t4, 0x01EB
    swc1  $f0, -0x3DAC($t4)        # slot 3 height (0x01EAC254)
    lw    $t3, -0x073C($t0)        # the followed point (guest), 0 = drift
    lwc1  $f4, -0x0738($t0)        # x offset
    lwc1  $f5, -0x0734($t0)        # z offset
    bne   $t3, $zero, follow
    nop
    lwc1  $f6, -0x3DB0($t4)        # drift: the slot's own x (0x01EAC250)…
    lwc1  $f7, -0x3DA8($t4)        # …and y (0x01EAC258)
    b     xz
    nop
follow:
    lwc1  $f6, 0x0000($t3)         # the followed point's x
    lwc1  $f7, 0x0008($t3)         #                    and y
xz:
    add.s $f6, $f6, $f4
    add.s $f7, $f7, $f5
    swc1  $f6, -0x3DB0($t4)
    swc1  $f7, -0x3DA8($t4)
    addiu $t5, $t0, -0x0430        # the drive rows (0x01FAFBD0)
    addiu $t6, $zero, 5
row:
    lw    $t7, 0x0000($t5)         # dst
    beq   $t7, $zero, nextrow
    nop
    lw    $t8, 0x0004($t5)         # count
    lwc1  $f8, 0x0008($t5)         # a
    lwc1  $f9, 0x000C($t5)         # b
    mul.s $f9, $f9, $f0            # b·y
    add.s $f8, $f8, $f9            # a + b·y
    lwc1  $f9, 0x0010($t5)         # lo
    .word 0x46094034               # c.lt.s $f8,$f9 — below lo ?
    nop
    bc1f  nolo
    nop
    mov.s $f8, $f9
nolo:
    lwc1  $f9, 0x0014($t5)         # hi
    .word 0x46084834               # c.lt.s $f9,$f8 — above hi ?
    nop
    bc1f  write
    nop
    mov.s $f8, $f9
write:
    swc1  $f8, 0x0000($t7)
    addiu $t8, $t8, -1
    bgtz  $t8, write
    addiu $t7, $t7, 4              # (delay slot)
nextrow:
    addiu $t6, $t6, -1
    bgtz  $t6, row
    addiu $t5, $t5, 0x20           # (delay slot)
out:
    j     0x001B4690               # the follow cave (DebugInfoCave.Follow), then the blade-spin cave
    nop
