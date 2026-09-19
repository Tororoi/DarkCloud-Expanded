# prop_pellet_follow.s — a chara-slot prop rides one of Xiao's pellets: the Matador's charged shot, a copy of the slingshot
# (SlingshotProp in projectile mode, chara slot 3) placed on the pellet every frame. Assembled at 0x01FB1E30
# (ElfCave.PropPelletFollow). Takes the dungeon step loop's `jal step__5CSHOT` (dun 0x1DB874C, DunPatches) in front of the
# cat's chain: it calls ElfCave.CatCopyQueue — which services the cat's copy queue and jumps on to CatPelletFollow, where
# the displaced step__5CSHOT is performed — with a0 (the pool) passed straight through, then does its own work.
#
# Mailbox (0x01F10000 + …): +0xC0 PropFollowSlot int, the pellet slot + 1 (mod, written LAST; 0 = off)
#   +0xC4 PropFollowLift float added to the pellet's height   +0xC8 PropFollowSpin float, yaw added per frame
#   +0xCC PropFollowEnded int, 1 when the pellet ended (cave; the cave also clears the slot — the mod fades the prop)
# Shot pool (*0x002A35D4): pos +0x40+slot*0x10, active +0x3D0+slot*4. Chara slot 3 = 0x01EAC240 (CharaArray + 3 × 0x14A0):
# pos +0x10/+0x14/+0x18, yaw +0x64. The draw seeds the copy's root from those every frame; the root is world-parented.

    addiu $sp, $sp, -0x20
    sw    $ra, 0x0010($sp)
    jal   0x01FB2480               # CatCopyQueue → CatPelletFollow → step__5CSHOT(pool), as before
    nop
    lui   $t0, 0x01F1
    lw    $t1, 0x00C0($t0)         # PropFollowSlot
    beq   $t1, $zero, ret
    nop
    addiu $t1, $t1, -1             # zero-based slot
    lui   $t2, 0x002A
    lw    $t2, 0x35D4($t2)         # pool base
    sll   $t3, $t1, 2
    addu  $t3, $t2, $t3
    lw    $t4, 0x03D0($t3)         # the pellet still active?
    bne   $t4, $zero, alive
    nop
    sw    $zero, 0x00C0($t0)       # ended: unbind …
    addiu $t4, $zero, 1
    b     ret
    sw    $t4, 0x00CC($t0)         # … and tell the mod (delay slot)
alive:
    sll   $t3, $t1, 4
    addu  $t3, $t2, $t3            # the pellet's vec row
    lwc1  $f2, 0x0040($t3)         # x
    lwc1  $f4, 0x0044($t3)         # height
    lwc1  $f6, 0x0048($t3)         # y
    lwc1  $f8, 0x00C4($t0)         # PropFollowLift
    add.s $f4, $f4, $f8
    lui   $t5, 0x01EA
    ori   $t5, $t5, 0xC240         # chara slot 3
    swc1  $f2, 0x0010($t5)         # CharPos
    swc1  $f4, 0x0014($t5)
    swc1  $f6, 0x0018($t5)
    lwc1  $f10, 0x0064($t5)        # its yaw
    lwc1  $f12, 0x00C8($t0)        # PropFollowSpin
    add.s $f10, $f10, $f12
    swc1  $f10, 0x0064($t5)
ret:
    lw    $ra, 0x0010($sp)
    jr    $ra
    addiu $sp, $sp, 0x20           # (delay slot)
