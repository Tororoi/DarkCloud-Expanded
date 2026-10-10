# monster_bank.s — each randomized floor's monster bank built from the sounds of the species on it. Assembled at 0x00110848
# (DeadFloatCave.MonsterBank: the dead soft-float routines' bodies, after the claim words): the library's path, then the code at
# +0x18 (0x00110860). DunPatches points BtLoadMonstor's closing `jal MemoryMapDump` (dun 0x01DB9568) here: every species of the
# floor is loaded by then, the floor's layout row is still in s1 and read_buffer holds nothing the game needs. The dump itself
# never runs (its body hosts the guard-bypass cave). Its helpers are monster_bank_io.s (AddUnit, Copy, Fix, Read, Gate, Steve —
# which also writes the floor's Steve lines).
#
# Only where Gate (monster_bank_io.s) says so: a floor the randomizer staged, or one after it while the monster port still holds
# the bank built here. The library `sound/set/monlib.snd` (MonsterSoundBake) is found as the game finds any file (SearchFile, its sector + data_sector)
# and its front read whole to read_buffer + 0x80000. The floor's units — program × 4 + 30-note window — are the dungeon map's
# kept ones, then each species' of the layout row (9 entries of 12 B, the species index at +4, −1 ends it), each once. In that
# order each unit's samples are read to read_buffer, one after another, while they fit under the cap: the lesser of 456,720 B
# (the largest monster bank the game itself loads) and the room between the dungeon's E bank (53,872 B in every dungeon set) and
# the I bank in sound memory (midi_state ports 2 and 4). A unit that does not fit is left out, its species silent as before.
# The bank's header is built at read_buffer + 0xA0000 in the layout of the game's own banks — version, head, then the VAG,
# sample, sample-set and program chunks (a 16-byte tag/size/max header, the offset table, the records, each chunk padded to 16)
# — each unit's records copied with their indices moved past the units before it and their VAG offsets past the samples before
# it, the units of one program under one program record. CSound::LoadHdBd_G swaps it in (the monster port's sounds stop, the old
# bank's memory is freed, the port's volume is set to 0) and CSound::SetVol turns the port back up to 0x100, as SetSoundFile does
# after loading a set. CodeCaves.MonsterBankStats takes the samples' bytes, the units taken and left out, a count of the
# banks built and the bank's header address in IOP memory (what Gate compares).
#
# Frame: 0x28 units left out, 0x30 the units (16 halfwords), 0x50 where each unit's samples went, 0x90 each unit's first sample
# set in the bank, 0xD0 VAG entries / samples / sample sets / the highest program, 0xE0 the sample, sample-set and program
# chunks' offsets and the header's size, 0xF0 the VAG entries, samples and sample sets placed so far and the next program
# record's offset, 0x100 the current unit's sample sets and samples, 0x108 the current program record's splits, 0x10C the kept
# units' count.

    .word 0x6E756F73               # "sound/set/monlib.snd"
    .word 0x65732F64
    .word 0x6F6D2F74
    .word 0x62696C6E
    .word 0x646E732E
    .word 0x00000000

    addiu $sp, $sp, -0x110
    sw    $ra, 0x0000($sp)
    sw    $s0, 0x0004($sp)
    sw    $s1, 0x0008($sp)
    sw    $s2, 0x000C($sp)
    sw    $s3, 0x0010($sp)
    sw    $s4, 0x0014($sp)
    sw    $s5, 0x0018($sp)
    sw    $s6, 0x001C($sp)
    sw    $s7, 0x0020($sp)
    sw    $fp, 0x0024($sp)
    sw    $zero, 0x0028($sp)
    jal   0x0010BED8               # Gate
    nop
    beq   $v0, $zero, done
    nop
# ── the library and its front ──
    lui   $a0, 0x0011
    jal   0x0013E980               # SearchFile__FPc
    ori   $a0, $a0, 0x0848         # (delay) the path
    beq   $v0, $zero, done
    lw    $t0, 0x000C($v0)         # (delay) DATA_HEADER.sector
    lui   $t1, 0x002A
    lw    $t2, 0x24F0($t1)         # data_sector
    addu  $s0, $t0, $t2
    lw    $s2, 0x2384($t1)         # read_buffer: the samples
    lui   $t0, 0x0008
    addu  $s3, $s2, $t0            # + 0x80000: the library's front
    lui   $t0, 0x000A
    addu  $fp, $s2, $t0            # + 0xA0000: the header
    move  $a0, $s0
    addiu $a1, $zero, 1
    jal   0x0010BED0               # Read
    move  $a2, $s3                 # (delay)
    lw    $t0, 0x0000($s3)
    lui   $t1, 0x534D
    ori   $t1, $t1, 0x4344         # 'DCMS'
    bne   $t0, $t1, done
    move  $a0, $s0                 # (delay)
    lw    $a1, 0x000C($s3)         # its front's sectors
    jal   0x0010BED0               # Read
    move  $a2, $s3                 # (delay)
    jal   0x0010BEE0               # Steve: the floor's Steve lines
    nop
# ── the units: the dungeon map's kept ones, then each species' ──
    move  $s4, $zero
    lui   $t0, 0x002A
    lw    $t0, 0x3594($t0)         # selectMapNo
    sltiu $t1, $t0, 16
    beq   $t1, $zero, species
    sll   $t0, $t0, 3              # (delay)
    addu  $s5, $s3, $t0
    lhu   $a0, 0x22D0($s5)
    jal   0x0010BEB8               # AddUnit
    nop
    lhu   $a0, 0x22D2($s5)
    jal   0x0010BEB8
    nop
    lhu   $a0, 0x22D4($s5)
    jal   0x0010BEB8
    nop
    lhu   $a0, 0x22D6($s5)
    jal   0x0010BEB8
    nop
species:
    sw    $s4, 0x010C($sp)         # the kept units
    move  $s5, $zero
species_entry:
    sll   $t0, $s5, 1
    addu  $t0, $t0, $s5
    sll   $t0, $t0, 2              # × 12
    addu  $t0, $t0, $s1
    lw    $t1, 0x0004($t0)         # its species index
    addiu $t2, $zero, -1
    beq   $t1, $t2, budget
    sltiu $t2, $t1, 176            # (delay)
    beq   $t2, $zero, species_next
    sll   $t1, $t1, 2              # (delay)
    addu  $s6, $s3, $t1
    lhu   $a0, 0x2010($s6)
    jal   0x0010BEB8
    nop
    lhu   $a0, 0x2012($s6)
    jal   0x0010BEB8
    nop
species_next:
    addiu $s5, $s5, 1
    slti  $t0, $s5, 9
    bne   $t0, $zero, species_entry
    nop
# ── the cap, then each unit's samples read while they fit (none of the species in the library: the bank left as it is — a
#    boss floor, whose sounds its own set brings) ──
budget:
    lw    $t0, 0x010C($sp)
    beq   $t0, $s4, done
    nop
    lui   $t0, 0x01CF
    lw    $t1, -0x7BBC($t0)        # midi_state.port[4].spu_address (0x01CE8240 + 0x204)
    lw    $t2, -0x7CBC($t0)        # midi_state.port[2].spu_address (+ 0x104)
    subu  $t1, $t1, $t2
    ori   $t3, $zero, 0xD290       # the E bank's 53,872 B and two 0x10 gaps
    subu  $s5, $t1, $t3
    lui   $t3, 0x0006
    ori   $t3, $t3, 0xF810         # 456,720
    slt   $t4, $t3, $s5
    movn  $s5, $t3, $t4
    move  $s6, $zero               # samples read
    move  $s7, $zero               # units taken
    move  $s1, $zero
place:
    beq   $s1, $s4, compose
    sll   $t0, $s1, 1              # (delay)
    addu  $t0, $t0, $sp
    lhu   $t1, 0x0030($t0)         # the unit
    sll   $t2, $t1, 4
    addu  $t2, $t2, $s3            # its directory entry − 0x10
    lw    $t3, 0x0018($t2)         # its samples' bytes
    addu  $t4, $s6, $t3
    slt   $t5, $s5, $t4
    bne   $t5, $zero, place_skip
    sll   $t5, $s7, 1              # (delay)
    addu  $t5, $t5, $sp
    sh    $t1, 0x0030($t5)         # taken: kept in order in the same list
    sll   $t6, $s7, 2
    addu  $t6, $t6, $sp
    sw    $s6, 0x0050($t6)         # where its samples go
    addiu $s7, $s7, 1
    lw    $t7, 0x0014($t2)         # its first sector
    addu  $a0, $s0, $t7
    srl   $a1, $t3, 11
    addu  $a2, $s2, $s6
    jal   0x0010BED0               # Read
    move  $s6, $t4                 # (delay)
    b     place
    addiu $s1, $s1, 1              # (delay)
place_skip:
    lw    $t0, 0x0028($sp)
    addiu $t0, $t0, 1
    sw    $t0, 0x0028($sp)
    b     place
    addiu $s1, $s1, 1              # (delay)
# ── the header's sizes ──
compose:
    beq   $s7, $zero, done
    move  $t3, $zero               # (delay) program records' bytes
    move  $t4, $zero               # the highest program
    move  $t5, $zero               # sample sets
    move  $t6, $zero               # samples
    move  $t7, $zero               # VAG entries
    move  $t8, $zero
totals:
    sll   $t0, $t8, 1
    addu  $t0, $t0, $sp
    lhu   $t1, 0x0030($t0)         # the unit
    srl   $t2, $t1, 2              # its program
    slt   $t9, $t4, $t2
    movn  $t4, $t2, $t9
    move  $t9, $zero               # a header unless an earlier unit has this program
totals_seen:
    beq   $t9, $t8, totals_header
    sll   $t0, $t9, 1              # (delay)
    addu  $t0, $t0, $sp
    lhu   $t0, 0x0030($t0)
    srl   $t0, $t0, 2
    beq   $t0, $t2, totals_counts
    nop
    b     totals_seen
    addiu $t9, $t9, 1              # (delay)
totals_header:
    addiu $t3, $t3, 36
totals_counts:
    sll   $t0, $t1, 4
    addu  $t0, $t0, $s3
    lbu   $t2, 0x001C($t0)         # splits
    sll   $t9, $t2, 2
    addu  $t9, $t9, $t2
    sll   $t9, $t9, 2              # × 20
    addu  $t3, $t3, $t9
    lbu   $t2, 0x001D($t0)
    addu  $t5, $t5, $t2
    lbu   $t2, 0x001E($t0)
    addu  $t6, $t6, $t2
    lbu   $t2, 0x001F($t0)
    addu  $t7, $t7, $t2
    addiu $t8, $t8, 1
    bne   $t8, $s7, totals
    nop
    sw    $t7, 0x00D0($sp)
    sw    $t6, 0x00D4($sp)
    sw    $t5, 0x00D8($sp)
    sw    $t4, 0x00DC($sp)
    lui   $t9, 0x5343
    ori   $t9, $t9, 0x4549         # 'IECS'
    sll   $t0, $t7, 1              # the VAG chunk at 0x50: 16 + 12 B an entry
    addu  $t0, $t0, $t7
    sll   $t0, $t0, 2
    addiu $t0, $t0, 31
    srl   $t0, $t0, 4
    sll   $t0, $t0, 4
    sw    $t9, 0x0050($fp)
    lui   $t8, 0x5661
    ori   $t8, $t8, 0x6769         # 'igaV'
    sw    $t8, 0x0054($fp)
    sw    $t0, 0x0058($fp)
    addiu $t8, $t7, -1
    sw    $t8, 0x005C($fp)
    addiu $t1, $t0, 0x50           # the sample chunk: 16 + 46 B an entry
    sw    $t1, 0x00E0($sp)
    sll   $t0, $t6, 5
    sll   $t2, $t6, 3
    addu  $t0, $t0, $t2
    sll   $t2, $t6, 2
    addu  $t0, $t0, $t2
    sll   $t2, $t6, 1
    addu  $t0, $t0, $t2
    addiu $t0, $t0, 31
    srl   $t0, $t0, 4
    sll   $t0, $t0, 4
    addu  $t2, $fp, $t1
    sw    $t9, 0x0000($t2)
    lui   $t8, 0x536D
    ori   $t8, $t8, 0x706C         # 'lpmS'
    sw    $t8, 0x0004($t2)
    sw    $t0, 0x0008($t2)
    addiu $t8, $t6, -1
    sw    $t8, 0x000C($t2)
    addu  $t1, $t1, $t0            # the sample-set chunk: 16 + 10 B an entry
    sw    $t1, 0x00E4($sp)
    sll   $t0, $t5, 3
    sll   $t2, $t5, 1
    addu  $t0, $t0, $t2
    addiu $t0, $t0, 31
    srl   $t0, $t0, 4
    sll   $t0, $t0, 4
    addu  $t2, $fp, $t1
    sw    $t9, 0x0000($t2)
    lui   $t8, 0x5373
    ori   $t8, $t8, 0x6574         # 'tesS'
    sw    $t8, 0x0004($t2)
    sw    $t0, 0x0008($t2)
    addiu $t8, $t5, -1
    sw    $t8, 0x000C($t2)
    addu  $t1, $t1, $t0            # the program chunk: 16 + a table to the highest program + the records
    sw    $t1, 0x00E8($sp)
    addiu $t0, $t4, 1
    sll   $t0, $t0, 2
    addu  $t0, $t0, $t3
    addiu $t0, $t0, 31
    srl   $t0, $t0, 4
    sll   $t0, $t0, 4
    addu  $t2, $fp, $t1
    sw    $t9, 0x0000($t2)
    lui   $t8, 0x5072
    ori   $t8, $t8, 0x6F67         # 'gorP'
    sw    $t8, 0x0004($t2)
    sw    $t0, 0x0008($t2)
    sw    $t4, 0x000C($t2)
    addu  $t1, $t1, $t0            # the header's size
    sw    $t1, 0x00EC($sp)
    addiu $t3, $t2, 16             # every program absent until its record goes in
    addiu $t0, $t4, 1
    addiu $t8, $zero, -1
prog_table:
    sw    $t8, 0x0000($t3)
    addiu $t0, $t0, -1
    bne   $t0, $zero, prog_table
    addiu $t3, $t3, 4              # (delay)
    sw    $t9, 0x0000($fp)         # version
    lui   $t8, 0x5665
    ori   $t8, $t8, 0x7273         # 'sreV'
    sw    $t8, 0x0004($fp)
    addiu $t8, $zero, 0x10
    sw    $t8, 0x0008($fp)
    lui   $t8, 0x0101
    sw    $t8, 0x000C($fp)
    sw    $t9, 0x0010($fp)         # head
    lui   $t8, 0x4865
    ori   $t8, $t8, 0x6164         # 'daeH'
    sw    $t8, 0x0014($fp)
    addiu $t8, $zero, 0x40
    sw    $t8, 0x0018($fp)
    sw    $t1, 0x001C($fp)         # the header's size
    sw    $s6, 0x0020($fp)         # the samples'
    lw    $t8, 0x00E8($sp)
    sw    $t8, 0x0024($fp)
    lw    $t8, 0x00E4($sp)
    sw    $t8, 0x0028($fp)
    lw    $t8, 0x00E0($sp)
    sw    $t8, 0x002C($fp)
    addiu $t8, $zero, 0x50
    sw    $t8, 0x0030($fp)
    addiu $t8, $zero, -1
    sw    $t8, 0x0034($fp)
    sw    $t8, 0x0038($fp)
    sw    $t8, 0x003C($fp)
    sw    $t8, 0x0040($fp)
    sw    $t8, 0x0044($fp)
    sw    $t8, 0x0048($fp)
    sw    $t8, 0x004C($fp)
    sw    $zero, 0x00F0($sp)       # VAG entries before this unit
    sw    $zero, 0x00F4($sp)       # samples before it
    sw    $zero, 0x00F8($sp)       # sample sets before it
    addiu $t0, $t4, 1
    sll   $t0, $t0, 2
    addiu $t0, $t0, 16
    sw    $t0, 0x00FC($sp)         # the next program record's offset in the program chunk
# ── each unit's VAG entries, samples and sample sets ──
    move  $s0, $zero
unit:
    sll   $t0, $s0, 1
    addu  $t0, $t0, $sp
    lhu   $t0, 0x0030($t0)
    sll   $t0, $t0, 4
    addu  $s5, $t0, $s3            # its directory entry − 0x10
    lw    $t0, 0x0010($s5)
    addu  $s1, $s3, $t0            # its records: header, splits, sample sets, samples, VAG entries
    lbu   $t2, 0x001C($s5)
    sll   $t3, $t2, 2
    addu  $t3, $t3, $t2
    sll   $t3, $t3, 2
    addiu $t3, $t3, 36
    addu  $v0, $s1, $t3            # the sample sets
    lbu   $t2, 0x001D($s5)
    sll   $t3, $t2, 2
    sll   $t2, $t2, 1
    addu  $t3, $t3, $t2            # × 6
    addu  $v1, $v0, $t3            # the samples
    lbu   $t2, 0x001E($s5)
    sll   $t3, $t2, 5
    sll   $t4, $t2, 3
    addu  $t3, $t3, $t4
    sll   $t4, $t2, 1
    addu  $t3, $t3, $t4            # × 42
    addu  $t9, $v1, $t3            # the VAG entries
    sw    $v0, 0x0100($sp)
    sw    $v1, 0x0104($sp)
    sll   $t0, $s0, 2
    addu  $t0, $t0, $sp
    lw    $t1, 0x00F8($sp)
    sw    $t1, 0x0090($t0)         # this unit's first sample set in the bank
# its VAG entries: offsets moved past the samples placed before it
    lbu   $t8, 0x001F($s5)
    lw    $t7, 0x00F0($sp)
    lw    $t6, 0x00D0($sp)
    sll   $t6, $t6, 2
    addiu $t6, $t6, 16             # the entries' offset in the chunk
vagi:
    sll   $t5, $t7, 3
    addu  $t5, $t5, $t6            # this entry's offset
    sll   $t4, $t7, 2
    addu  $t4, $t4, $fp
    sw    $t5, 0x0060($t4)         # its table slot (0x50 + 16 + index × 4)
    addu  $a0, $fp, $t5
    addiu $a0, $a0, 0x50
    move  $a1, $t9
    jal   0x0010BEC0               # Copy
    addiu $a2, $zero, 4            # (delay)
    addu  $a0, $fp, $t5
    lw    $t0, 0x0050($a0)
    sll   $t1, $s0, 2
    addu  $t1, $t1, $sp
    lw    $t1, 0x0050($t1)         # where this unit's samples went
    addu  $t0, $t0, $t1
    sw    $t0, 0x0050($a0)
    addiu $t9, $t9, 8
    addiu $t7, $t7, 1
    addiu $t8, $t8, -1
    bne   $t8, $zero, vagi
    nop
# its samples: VAG index moved past the VAG entries before it
    lw    $t9, 0x0104($sp)
    lbu   $t8, 0x001E($s5)
    lw    $t7, 0x00F4($sp)
    lw    $t6, 0x00D4($sp)
    sll   $t6, $t6, 2
    addiu $t6, $t6, 16
    lw    $v0, 0x00E0($sp)
    addu  $v0, $v0, $fp            # the sample chunk
smpl:
    sll   $t5, $t7, 5
    sll   $t4, $t7, 3
    addu  $t5, $t5, $t4
    sll   $t4, $t7, 1
    addu  $t5, $t5, $t4            # × 42
    addu  $t5, $t5, $t6
    sll   $t4, $t7, 2
    addu  $t4, $t4, $v0
    sw    $t5, 0x0010($t4)
    addu  $a0, $v0, $t5
    move  $a1, $t9
    jal   0x0010BEC0               # Copy
    addiu $a2, $zero, 21           # (delay)
    addu  $a0, $v0, $t5
    jal   0x0010BEC8               # Fix
    lw    $a1, 0x00F0($sp)         # (delay)
    addiu $t9, $t9, 42
    addiu $t7, $t7, 1
    addiu $t8, $t8, -1
    bne   $t8, $zero, smpl
    nop
# its sample sets: sample index moved past the samples before it
    lw    $t9, 0x0100($sp)
    lbu   $t8, 0x001D($s5)
    lw    $t7, 0x00F8($sp)
    lw    $t6, 0x00D8($sp)
    sll   $t6, $t6, 2
    addiu $t6, $t6, 16
    lw    $v0, 0x00E4($sp)
    addu  $v0, $v0, $fp            # the sample-set chunk
sset:
    sll   $t5, $t7, 2
    sll   $t4, $t7, 1
    addu  $t5, $t5, $t4            # × 6
    addu  $t5, $t5, $t6
    sll   $t4, $t7, 2
    addu  $t4, $t4, $v0
    sw    $t5, 0x0010($t4)
    addu  $a0, $v0, $t5
    move  $a1, $t9
    jal   0x0010BEC0               # Copy
    addiu $a2, $zero, 3            # (delay)
    addu  $a0, $v0, $t5
    addiu $a0, $a0, 4
    jal   0x0010BEC8               # Fix
    lw    $a1, 0x00F4($sp)         # (delay)
    addiu $t9, $t9, 6
    addiu $t7, $t7, 1
    addiu $t8, $t8, -1
    bne   $t8, $zero, sset
    nop
    lw    $t0, 0x00F0($sp)         # the bases move past this unit
    lbu   $t1, 0x001F($s5)
    addu  $t0, $t0, $t1
    sw    $t0, 0x00F0($sp)
    lw    $t0, 0x00F4($sp)
    lbu   $t1, 0x001E($s5)
    addu  $t0, $t0, $t1
    sw    $t0, 0x00F4($sp)
    lw    $t0, 0x00F8($sp)
    lbu   $t1, 0x001D($s5)
    addu  $t0, $t0, $t1
    sw    $t0, 0x00F8($sp)
    addiu $s0, $s0, 1
    bne   $s0, $s7, unit
    nop
# ── one record a program: the first unit's header, then every unit's splits, each split's sample set moved past the sample
#    sets before its unit ──
    move  $s0, $zero
program:
    sll   $t0, $s0, 1
    addu  $t0, $t0, $sp
    lhu   $s4, 0x0030($t0)
    srl   $s4, $s4, 2              # the program
    move  $t1, $zero
program_seen:
    beq   $t1, $s0, program_new
    sll   $t0, $t1, 1              # (delay)
    addu  $t0, $t0, $sp
    lhu   $t0, 0x0030($t0)
    srl   $t0, $t0, 2
    beq   $t0, $s4, program_next   # written with an earlier unit
    nop
    b     program_seen
    addiu $t1, $t1, 1              # (delay)
program_new:
    lw    $v0, 0x00E8($sp)
    addu  $v0, $v0, $fp            # the program chunk
    lw    $t5, 0x00FC($sp)
    sll   $t4, $s4, 2
    addu  $t4, $t4, $v0
    sw    $t5, 0x0010($t4)         # its table slot
    addu  $s1, $v0, $t5            # its record
    sll   $t0, $s0, 1
    addu  $t0, $t0, $sp
    lhu   $t0, 0x0030($t0)
    sll   $t0, $t0, 4
    addu  $t0, $t0, $s3
    lw    $t0, 0x0010($t0)
    addu  $a1, $s3, $t0            # the first unit's header
    move  $a0, $s1
    jal   0x0010BEC0               # Copy
    addiu $a2, $zero, 18           # (delay)
    addiu $t9, $s1, 36             # the next split
    sw    $zero, 0x0108($sp)       # its splits so far
    move  $s5, $s0                 # every unit of this program, from this one on
program_unit:
    sll   $t0, $s5, 1
    addu  $t0, $t0, $sp
    lhu   $t0, 0x0030($t0)
    srl   $t1, $t0, 2
    bne   $t1, $s4, program_unit_next
    sll   $t0, $t0, 4              # (delay)
    addu  $t0, $t0, $s3            # its directory entry − 0x10
    lbu   $t8, 0x001C($t0)         # its splits
    lw    $t1, 0x0010($t0)
    addu  $a1, $s3, $t1
    addiu $a1, $a1, 36
    sll   $a2, $t8, 2
    addu  $a2, $a2, $t8
    sll   $a2, $a2, 1              # × 10 halfwords
    lw    $t1, 0x0108($sp)
    addu  $t1, $t1, $t8
    sw    $t1, 0x0108($sp)
    jal   0x0010BEC0               # Copy (keeps t7–t9)
    move  $a0, $t9                 # (delay)
    sll   $t0, $s5, 2
    addu  $t0, $t0, $sp
    lw    $t7, 0x0090($t0)         # its first sample set in the bank
program_split:
    move  $a0, $t9
    jal   0x0010BEC8               # Fix
    move  $a1, $t7                 # (delay)
    addiu $t9, $t9, 20
    addiu $t8, $t8, -1
    bne   $t8, $zero, program_split
    nop
program_unit_next:
    addiu $s5, $s5, 1
    bne   $s5, $s7, program_unit
    nop
    lw    $t1, 0x0108($sp)
    sb    $t1, 0x0004($s1)         # its splits
    subu  $t0, $t9, $s1            # the record's bytes
    lw    $t5, 0x00FC($sp)
    addu  $t5, $t5, $t0
    sw    $t5, 0x00FC($sp)
program_next:
    addiu $s0, $s0, 1
    bne   $s0, $s7, program
    nop
# ── the swap ──
    lui   $a0, 0x002A
    ori   $a0, $a0, 0x252C         # CSnd
    move  $a1, $fp
    lw    $a2, 0x00EC($sp)
    move  $a3, $s2
    jal   0x00146EF0               # CSound::LoadHdBd_G(hd, hd_size, bd, bd_size)
    move  $t0, $s6                 # (delay) the fifth argument rides in t0
    lui   $a0, 0x002A
    ori   $a0, $a0, 0x252C         # CSnd
    addiu $a1, $zero, 10
    jal   0x00146960               # CSound::SetVol(10, 0x100): the load leaves the monster port at 0 (SetSoundFile turns it back up)
    addiu $a2, $zero, 0x0100       # (delay)
    lui   $t0, 0x01FB
    addiu $t0, $t0, -0x07F0        # 0x01FAF810 CodeCaves.MonsterBankStats
    lui   $t1, 0x01CF
    lw    $t1, -0x7C40($t1)        # midi_state.port[3].bank: this bank's header
    sw    $t1, 0x0010($t0)
    sw    $s6, 0x0000($t0)
    sw    $s7, 0x0004($t0)
    lw    $t1, 0x0028($sp)
    sw    $t1, 0x0008($t0)
    lw    $t1, 0x000C($t0)
    addiu $t1, $t1, 1
    sw    $t1, 0x000C($t0)
done:
    lw    $ra, 0x0000($sp)
    lw    $s0, 0x0004($sp)
    lw    $s1, 0x0008($sp)
    lw    $s2, 0x000C($sp)
    lw    $s3, 0x0010($sp)
    lw    $s4, 0x0014($sp)
    lw    $s5, 0x0018($sp)
    lw    $s6, 0x001C($sp)
    lw    $s7, 0x0020($sp)
    lw    $fp, 0x0024($sp)
    jr    $ra
    addiu $sp, $sp, 0x110          # (delay)
