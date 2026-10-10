# monster_bank_io.s — the helpers monster_bank.s calls. Assembled at 0x0010BEB8 (DeadStreamCave.MonsterBankIo: the dead sceCdSt*
# streaming routines' bodies, after the claim words). Entries:
#   +0x00 AddUnit(a0 = unit)      appended to the caller's list (its frame's 0x30, a halfword each, s4 entries) unless 0xFFFF,
#                                 past the library's 512 units, absent from it (directory entry at s3 + 0x10 + unit × 16, its
#                                 records' offset 0) or listed already; at most 16. Leaf: t0–t3.
#   +0x08 Copy(a0 = to, a1 = from, a2 = halfwords > 0)   records sit on 2-byte boundaries. Leaf: t0.
#   +0x10 Fix(a0 = a halfword index, a1 = base)          the index moved by the base unless it is 0xFFFF (none). Leaf: t0, t1.
#   +0x18 Read(a0 = disc sector, a1 = sectors, a2 = buffer)   sceCdRead and wait, retried until it reads cleanly (the game's
#                                 CDRead does the same).
#   +0x20 Gate() → v0 ≠ 0 when this floor's bank is built: the floor's bit is set in CodeCaves.MonsterBankFloors (a floor the
#                                 randomizer staged), or the monster port still holds the last bank built here (its header's
#                                 IOP address, CodeCaves.MonsterBankStats + 0x10, still midi_state.port[3].bank) — a floor
#                                 after a randomized one gets its own species' sounds back. A sound-set load replaces the bank,
#                                 and with it this claim. Leaf: t0–t5.
#   +0x28 Steve() (called with the caller's s0 = the library's first sector, s1 = the layout row, s2 = read_buffer, s3 = the
#                                 library's front): the floor's Steve lines. Steve's lock-on lines are messages 4000 + the
#                                 monster's name number × 10 + 0..9 of the dungeon's Steve file, which carries only its natives'
#                                 (BtMapJumpLoad loads it into Steve's window, DngMesStb). Each species of the row with a block in
#                                 the library (its Steve directory at front + 0x2350, 8 B an index: sector, bytes) has it read
#                                 to read_buffer + 0xB0000 once, and Steve's buffer (DngMesStb.buff) is rewritten as a message
#                                 file of their lines — the count, its text's end, an (id, offset) pair a line, the texts — then
#                                 SetBuff given it again and the window's laid-out message (mes_made) dropped. The blocks are
#                                 at most 6 KB each, so nine fit the buffer's 56,000 B.

    j     add_unit
    nop
    j     copy
    nop
    j     fix
    nop
    j     read
    nop
    j     gate
    nop
    j     steve
    nop

add_unit:
    sltiu $t0, $a0, 512
    beq   $t0, $zero, add_unit_ret
    sll   $t1, $a0, 4              # (delay)
    addu  $t1, $t1, $s3
    lw    $t1, 0x0010($t1)         # its records' offset (0 = absent)
    beq   $t1, $zero, add_unit_ret
    move  $t2, $zero               # (delay)
add_unit_seen:
    beq   $t2, $s4, add_unit_add
    sll   $t3, $t2, 1              # (delay)
    addu  $t3, $t3, $sp
    lhu   $t3, 0x0030($t3)
    beq   $t3, $a0, add_unit_ret
    nop
    b     add_unit_seen
    addiu $t2, $t2, 1              # (delay)
add_unit_add:
    slti  $t3, $s4, 16
    beq   $t3, $zero, add_unit_ret
    sll   $t3, $s4, 1              # (delay)
    addu  $t3, $t3, $sp
    sh    $a0, 0x0030($t3)
    addiu $s4, $s4, 1
add_unit_ret:
    jr    $ra
    nop

copy:
    lhu   $t0, 0x0000($a1)
    addiu $a1, $a1, 2
    sh    $t0, 0x0000($a0)
    addiu $a2, $a2, -1
    bne   $a2, $zero, copy
    addiu $a0, $a0, 2              # (delay)
    jr    $ra
    nop

fix:
    lhu   $t0, 0x0000($a0)
    ori   $t1, $zero, 0xFFFF
    beq   $t0, $t1, fix_ret
    addu  $t0, $t0, $a1            # (delay)
    sh    $t0, 0x0000($a0)
fix_ret:
    jr    $ra
    nop

read:
    addiu $sp, $sp, -0x20
    sw    $ra, 0x0000($sp)
    sw    $a0, 0x0004($sp)
    sw    $a1, 0x0008($sp)
    sw    $a2, 0x000C($sp)
    addiu $t0, $zero, 0x0100       # sceCdRMode: trycount 0, spindlctrl 1, datapattern 0
    sw    $t0, 0x0010($sp)
read_try:
    lw    $a0, 0x0004($sp)
    lw    $a1, 0x0008($sp)
    lw    $a2, 0x000C($sp)
    jal   0x0010A4C0               # sceCdRead
    addiu $a3, $sp, 0x10           # (delay)
    beq   $v0, $zero, read_try
    nop
    jal   0x0010AD98               # sceCdSync(0)
    move  $a0, $zero               # (delay)
    jal   0x0010B6F8               # sceCdGetError
    nop
    bne   $v0, $zero, read_try
    nop
    lw    $ra, 0x0000($sp)
    jr    $ra
    addiu $sp, $sp, 0x20           # (delay)

gate:
    lui   $t0, 0x002A
    lw    $t1, 0x3468($t0)         # UserStatus
    lb    $t2, 0x0002($t1)         # cur_floor
    bltz  $t2, gate_bank
    sra   $t3, $t2, 3              # (delay)
    lui   $t4, 0x01FB
    addiu $t4, $t4, -0x0800        # 0x01FAF800 CodeCaves.MonsterBankFloors
    addu  $t3, $t3, $t4
    lbu   $t3, 0x0000($t3)
    andi  $t5, $t2, 7
    srlv  $t3, $t3, $t5
    andi  $v0, $t3, 1
    bne   $v0, $zero, gate_ret
    nop
gate_bank:
    lui   $t0, 0x01FB
    lw    $t1, -0x07E0($t0)        # 0x01FAF820: the last bank built here
    beq   $t1, $zero, gate_ret
    move  $v0, $zero               # (delay)
    lui   $t2, 0x01CF
    lw    $t2, -0x7C40($t2)        # midi_state.port[3].bank (0x01CE8240 + 0x180)
    xor   $v0, $t1, $t2
    sltiu $v0, $v0, 1
gate_ret:
    jr    $ra
    nop

steve:
    addiu $sp, $sp, -0x70
    sw    $ra, 0x0000($sp)
    sw    $s4, 0x0004($sp)
    sw    $s5, 0x0008($sp)
    sw    $s6, 0x000C($sp)
    sw    $s7, 0x0010($sp)
    sw    $fp, 0x0014($sp)
    lui   $t0, 0x01EC
    lw    $fp, -0x54C0($t0)        # DngMesStb.buff (0x01EB93A0 + 0x17A0)
    beq   $fp, $zero, steve_ret
    lui   $t0, 0x000B              # (delay)
    addu  $s6, $s2, $t0            # read_buffer + 0xB0000: the blocks
    move  $s7, $zero               # blocks read
    move  $s5, $zero               # their bytes
    move  $s4, $zero
steve_entry:
    sll   $t0, $s4, 1
    addu  $t0, $t0, $s4
    sll   $t0, $t0, 2              # × 12
    addu  $t0, $t0, $s1
    lw    $t1, 0x0004($t0)         # its species index
    addiu $t2, $zero, -1
    beq   $t1, $t2, steve_compose
    sltiu $t2, $t1, 176            # (delay)
    beq   $t2, $zero, steve_next
    sll   $t1, $t1, 3              # (delay)
    addu  $t1, $t1, $s3
    lw    $t2, 0x2350($t1)         # its block's sector
    lw    $t3, 0x2354($t1)         # and bytes
    beq   $t3, $zero, steve_next
    move  $t4, $zero               # (delay)
steve_seen:
    beq   $t4, $s7, steve_read
    sll   $t5, $t4, 2              # (delay)
    addu  $t5, $t5, $sp
    lw    $t5, 0x0020($t5)
    beq   $t5, $t2, steve_next     # read already (one name, several species)
    nop
    b     steve_seen
    addiu $t4, $t4, 1              # (delay)
steve_read:
    sll   $t5, $s7, 2
    addu  $t5, $t5, $sp
    sw    $t2, 0x0020($t5)
    sw    $s5, 0x0044($t5)
    addiu $s7, $s7, 1
    addu  $a0, $s0, $t2
    srl   $a1, $t3, 11
    addu  $a2, $s6, $s5
    jal   read
    addu  $s5, $s5, $t3            # (delay)
steve_next:
    addiu $s4, $s4, 1
    slti  $t0, $s4, 9
    bne   $t0, $zero, steve_entry
    nop
steve_compose:
    beq   $s7, $zero, steve_ret
    move  $t9, $zero               # (delay) the lines
    move  $t8, $zero
steve_count:
    sll   $t0, $t8, 2
    addu  $t0, $t0, $sp
    lw    $t0, 0x0044($t0)
    addu  $t0, $t0, $s6
    lhu   $t0, 0x0000($t0)
    addu  $t9, $t9, $t0
    addiu $t8, $t8, 1
    bne   $t8, $s7, steve_count
    nop
    sh    $t9, 0x0000($fp)         # the count
    sll   $s5, $t9, 2
    addiu $s5, $s5, 4              # the texts' start (bytes)
    addiu $t7, $fp, 4              # the next (id, offset) pair
    move  $s4, $zero
steve_block:
    sll   $t0, $s4, 2
    addu  $t0, $t0, $sp
    lw    $t0, 0x0044($t0)
    addu  $t6, $t0, $s6            # the block
    lhu   $t8, 0x0000($t6)         # its lines
    lhu   $a2, 0x0002($t6)         # its text's halfwords
    sll   $t0, $t8, 2
    addiu $t0, $t0, 4
    addu  $a1, $t6, $t0            # its text
    jal   copy
    addu  $a0, $fp, $s5            # (delay)
    srl   $t5, $s5, 1
    subu  $t5, $t5, $t9
    addiu $t5, $t5, -1             # the offset of this block's text (text at 2 × (count + offset + 1))
    addiu $t4, $t6, 4
steve_line:
    lhu   $t0, 0x0000($t4)         # id
    lhu   $t1, 0x0002($t4)         # its text's halfword in the block
    addu  $t1, $t1, $t5
    sh    $t0, 0x0000($t7)
    sh    $t1, 0x0002($t7)
    addiu $t7, $t7, 4
    addiu $t8, $t8, -1
    bne   $t8, $zero, steve_line
    addiu $t4, $t4, 4              # (delay)
    lhu   $t0, 0x0002($t6)
    sll   $t0, $t0, 1
    addu  $s5, $s5, $t0
    addiu $s4, $s4, 1
    bne   $s4, $s7, steve_block
    nop
    sh    $s5, 0x0002($fp)         # the texts' end
    lui   $a0, 0x01EC
    addiu $a0, $a0, -0x6C60        # DngMesStb (0x01EB93A0)
    jal   0x0014DA00               # ClsMes::SetBuff
    move  $a1, $fp                 # (delay)
    lui   $t0, 0x01EC
    addiu $t1, $zero, -1
    sw    $t1, -0x55A4($t0)        # DngMesStb.mes_made (+ 0x16BC): lay the line out afresh
steve_ret:
    lw    $ra, 0x0000($sp)
    lw    $s4, 0x0004($sp)
    lw    $s5, 0x0008($sp)
    lw    $s6, 0x000C($sp)
    lw    $s7, 0x0010($sp)
    lw    $fp, 0x0014($sp)
    jr    $ra
    addiu $sp, $sp, 0x70           # (delay)
