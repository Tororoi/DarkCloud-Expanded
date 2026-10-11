# atla_collect.s — what collecting an atla marks, made safe for the atlas the mod makes and for floors past the slot table. Assembled at
# 0x0010A690 (DeadReadIopmCave.AtlaCollect: the dead sceCdReadIOPm body). ElfSpeciesPatches.PatchAtlaCollect points getAtraToSaveData's
# `jal CDngStatusData::GetAtraData` (0x1B7500; a0 = the status, a1 = the dungeon, a2 = the floor, a3 = the atla's parts entry) here.
# GetAtraData marks the floor's slot holding the entry collected (-3) — or, failing that, a random atla's (-2) — and frees the entry
# once its count runs out. Its slot table holds 40 floors a dungeon, so on Demon Shaft's floors past it the search reads (and writes)
# what follows the table; and an atla the mod spawned on the spot (AtlaGemron) has no slot, so it took a random atla's.
#  · a parts entry with a sentinel part id (24–39, the mod's: AtlaSystem): GetAtraData as before when its floor's slots hold it (the
#    Atlamillia Insurance's atlas are placed there), else only its count taken down and the entry freed at 0;
#  · any other atla: GetAtraData as before on floors 0–39, nothing past them.
# t-registers only.

    slti  $t0, $a1, 6
    beq   $t0, $zero, ret          # (GetAtraData's own bound)
    sll   $t0, $a1, 4              # (delay)
    subu  $t0, $t0, $a1            # 15 × dungeon
    sll   $t0, $t0, 4              # 0xF0 × dungeon
    sll   $t1, $t0, 2
    addu  $t0, $t0, $t1            # 0x4B0 × dungeon
    sll   $t1, $a3, 1
    addu  $t1, $t1, $a3
    sll   $t1, $t1, 2              # 0xC × entry
    addu  $t0, $t0, $t1
    addu  $t0, $t0, $a0
    addiu $t0, $t0, 0x2078         # the parts entry {part id, half, count}
    lw    $t1, 0x0000($t0)
    addiu $t1, $t1, -24
    sltiu $t1, $t1, 16
    beq   $t1, $zero, native       # not a sentinel
    slti  $t2, $a2, 40             # (delay) a floor the slot table holds
    beq   $t2, $zero, own
    sll   $t3, $a1, 8              # (delay)
    sll   $t4, $a1, 10
    addu  $t3, $t3, $t4            # 0x500 × dungeon
    sll   $t4, $a2, 5
    addu  $t3, $t3, $t4            # + 0x20 × floor
    addu  $t3, $t3, $a0
    addiu $t3, $t3, 0x0278         # its floor's 8 slots
    addiu $t4, $zero, 8
scan:
    lw    $t5, 0x0000($t3)
    beq   $t5, $a3, gad            # placed in a slot: GetAtraData marks it
    addiu $t4, $t4, -1             # (delay)
    bne   $t4, $zero, scan
    addiu $t3, $t3, 4              # (delay)
own:
    lw    $t1, 0x0008($t0)         # spawned on the spot: its count down, the entry freed at 0
    addiu $t1, $t1, -1
    bgtz  $t1, ret
    sw    $t1, 0x0008($t0)         # (delay)
    addiu $t1, $zero, -1
    jr    $ra
    sw    $t1, 0x0000($t0)         # (delay)
native:
    bne   $t2, $zero, gad
    nop
ret:
    jr    $ra
    nop
gad:
    j     0x001BF950               # GetAtraData
    nop
