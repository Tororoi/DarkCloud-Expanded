# atla_dungeon.s — Demon Shaft's atlas collected through Gallery of Time's atla tables. Assembled at 0x0010A750 (DeadReadIopmCave.AtlaDungeon,
# after atla_collect.s in the dead sceCdReadIOPm body). ElfSpeciesPatches.PatchAtlaDungeon turns BtAtraGetShort_Loop's `move s3, a0` (0x1D2C88,
# the dungeon it was handed: selectMapNo; its ra is saved by then) into `jal here`. The atla tables hold six dungeons (0–5): Demon Shaft
# (6), which places no atla of its own, would read its contents past the registry and its ceremony name through a null pointer
# (GetEditAtraData's bound). Handed 6, the ceremony works on 5, where AtlaGemron keeps Demon Shaft's atla (its parts entry, sentinel and
# name channel); atla_collect.s skips the slots of a floor past the 40 the table holds.

    addiu $t0, $zero, 6
    bne   $a0, $t0, keep
    or    $s3, $a0, $zero          # (delay) the dungeon, as the move did
    addiu $s3, $zero, 5            # Demon Shaft → Gallery of Time's tables
keep:
    jr    $ra
    nop
