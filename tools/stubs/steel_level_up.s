# steel_level_up.s — the Steel Slingshot (item 301) gains a level-up bonus of +2 endurance instead of +1, and twice the max
# WHP roll. Assembled at 0x001B42E0 (DebugInfoCave.SteelLevelUp), after the pellet-sprite cave in DebugInfomationDraw's
# body. The attached items' sums are untouched. A level-up's bonus is spread over two routines, each hooked at one add
# with a `jal` here (the instruction after the hook stays as the delay slot; where it is the store, the cave stores again
# with the right value):
#   B  SetLevelUpWeaponData 0x2367CC `addiu v1,v1,1` — the inventory record (a0) gains the level's +1 endurance   → +2
#   C  SetLevelUpWeaponData 0x236880 `addu v1,v1,a0` — its max WHP (s1 = &record+0xC) gains 1 + rand%3 (v1 = old + 1,
#      a0 = the roll)                                                                                        → twice that
#   D  WeaponLevelUpValueCalc 0x235D94 `lh v0,0xAA(sp)` — the item-use path's preview (out s5, source s6): endurance
#      gains the attachments' sum + 1 (v1)                                                                  → the sum + 2
#   E  WeaponLevelUpValueCalc 0x235EE4 `addu v1,v1,a0` — its max WHP gains 1 + rand%3                          → twice that
# Each entry reads the weapon id off the record it is growing and does the vanilla add for any other weapon.
# Clobbers at only beyond the registers the site itself computes into.
    b     ent_b
    nop
    b     ent_c
    nop
    b     ent_d
    nop
    b     ent_e
    nop
ent_b:
    lh    $at, 0x0000($a0)         # the inventory record's item id
    addiu $at, $at, -301
    bne   $at, $zero, b_ret
    addiu $v1, $v1, 1              # (delay) the level's +1
    addiu $v1, $v1, 1              # the Steel Slingshot: +2
b_ret:
    sh    $v1, 0x0006($a0)
    jr    $ra
    nop
ent_c:
    addu  $v1, $v1, $a0            # old + 1 + the roll
    lh    $at, -0x000C($s1)        # the record's item id (s1 = &max WHP)
    addiu $at, $at, -301
    bne   $at, $zero, c_ret
    nop
    addu  $v1, $v1, $a0            # the Steel Slingshot: + (1 + the roll) again
    addiu $v1, $v1, 1
c_ret:
    sh    $v1, 0x0000($s1)
    jr    $ra
    nop
ent_d:
    lh    $v0, 0x00AA($sp)         # the attachments' endurance sum
    lh    $at, 0x0000($s6)         # the source record's item id
    addiu $at, $at, -301
    bne   $at, $zero, d_ret
    addiu $v1, $v0, 1              # (delay) the sum + 1
    addiu $v1, $v1, 1              # the Steel Slingshot: the sum + 2
d_ret:
    jr    $ra
    nop
ent_e:
    addu  $v1, $v1, $a0            # old + 1 + the roll
    lh    $at, 0x0000($s6)
    addiu $at, $at, -301
    bne   $at, $zero, e_ret
    nop
    addu  $v1, $v1, $a0            # the Steel Slingshot: + (1 + the roll) again
    addiu $v1, $v1, 1
e_ret:
    sh    $v1, 0x000C($s5)
    jr    $ra
    nop
