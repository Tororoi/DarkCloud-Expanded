# species_lookup.s — the species table's extension rows. Assembled at 0x0027D2F8 (SmoothRestCave.SpeciesLookup, zero words of
# the dead SmoothRest body). MonstorTable (0x27FB00, 167 × 0x9C) has no slack — BtEnemyLayout starts 12 bytes after it — so
# the mod's species (EnemySpecies.BombGemron, index 167 on) are rows in SmoothRestCave.SpeciesRows (0x27D364). The table's one
# reader, CMonstorUnit::SetupBaseModel, forms `&MonstorTable[model_no]` as lui/addiu/addu at 0x1DFEE0; ElfSpeciesPatches turns
# the lui/addiu into `jal here; nop` and keeps the addu, so this hands back the base the addu adds the row offset to.
# in:  a2 = model_no, v1 = model_no * 0x9C.   out: v0 = MonstorTable, or SpeciesRows - 167 * 0x9C so that row 167 lands first.
# Clobbers v0 only (the register the replaced lui/addiu wrote); ra was saved by the prologue.

    slti  $v0, $a2, 167
    beq   $v0, $zero, ext
    lui   $v0, 0x0028              # (delay slot) MonstorTable's upper half
    jr    $ra
    addiu $v0, $v0, -0x0500        # 0x0027FB00 MonstorTable
ext:
    lui   $v0, 0x0027
    jr    $ra
    addiu $v0, $v0, 0x6DA0         # 0x00276DA0 = SpeciesRows (0x27D364) - 167 * 0x9C
