# cat_copy_queue.s — the cat's mesh copy, done INSIDE the machine.
#
# WHY. Building the cat copies ~300 KB into the mod's cave, and reading her model to do it makes ~600 KB of traffic. Over PINE
# that is about 100 KB/s, so the build measured 8.7 s (stamps, 2026-09-15) and the player waits that long after every switch to
# Xiao before the charged shot works. The console would memcpy the same bytes in well under a frame. Nothing about the work is
# expensive — only the fact that it was being done from outside the emulator, a word at a time down a debug socket.
#
# HOW. The mod writes a QUEUE of copy jobs into free heap-tail data (CodeCaves.CatCopyQueue) and sets the job count LAST. This
# cave sits in front of the cat's existing once-per-frame hook: if the count is zero — every frame but the one or two after a
# spawn — it falls straight through to CatPelletFollow having touched nothing. Otherwise it performs every job and clears the
# count, and the mod's poll sees it go to zero.
#
# A job is copy-then-rebase, the same two steps Memory.RebaseRange does in C#: move `size` bytes, then twice over the
# DESTINATION re-point every word that looks like a pointer into a given source range so it points into the copy instead.
# ⚠ The pointer test is the whole reason this is not a plain memcpy: a float can alias a perfectly plausible address (3.3f is
# 0x4053651E → 0x0053651E masked), and re-pointing one bends geometry. Only segments 0/2/3/8/A count as pointers, and the test
# is on the RAW word, before masking — exactly as MemoryFunctions.LooksLikePointer has it.
#
# Queue layout (guest 0x01FAE620): +0x00 job count (0 = idle), jobs from +0x10, 0x30 bytes each. +0x24 is the job's OP:
#   op 0 — copy and re-point.  +0x00 src  +0x04 dst  +0x08 size, then two rebase specs of (src, size, dst) at +0x0C and +0x18.
#          A rebase spec with size 0 is skipped.
#   op 1 — find and replace 64-bit values in place, which is how the cat's textures are relocated: the copy's draw packets
#          carry absolute TEX0 register words, and moving the cat's VRAM block means rewriting each one. +0x04 block, +0x08 its
#          length, +0x0C how many old→new pairs, +0x10 where the pair table is (16 B each: old lo/hi, new lo/hi). ALL the pairs
#          are tried at each position, so a block is walked ONCE — one job per block per texture walked the same 300 KB five
#          times over, 3.8 MB of scanning for 300 KB of data (2026-09-15).
    lui   $t0, 0x01FA
    ori   $t0, $t0, 0xE620
    lw    $t1, 0x0($t0)            # how many jobs are waiting
    beq   $t1, $zero, tail         # the common case: nothing to do, and nothing touched
    nop
    addiu $sp, $sp, -0x50
    sw    $ra, 0x00($sp)
    sw    $a0, 0x04($sp)
    sw    $s0, 0x08($sp)
    sw    $s1, 0x0C($sp)
    sw    $s2, 0x10($sp)
    sw    $s3, 0x14($sp)
    sw    $s4, 0x18($sp)
    sw    $s5, 0x1C($sp)
    sw    $s6, 0x20($sp)
    sw    $t0, 0x24($sp)
    or    $s1, $t1, $zero          # jobs remaining
    addiu $s0, $t0, 0x10           # the first job
job:
    lw    $t1, 0x24($s0)           # op
    bne   $t1, $zero, findrep
    nop
    lw    $s2, 0x00($s0)           # src
    lw    $s3, 0x04($s0)           # dst
    lw    $s4, 0x08($s0)           # size, a multiple of 4
    or    $t2, $zero, $zero
copy:
    beq   $t2, $s4, rebase_init
    nop
    addu  $t3, $s2, $t2
    lw    $t4, 0x0($t3)
    addu  $t5, $s3, $t2
    sw    $t4, 0x0($t5)
    addiu $t2, $t2, 4
    b     copy
    nop
rebase_init:
    addiu $s5, $s0, 0x0C           # the first rebase spec
    addiu $s6, $zero, 2            # two of them
spec:
    lw    $t6, 0x04($s5)           # its size
    beq   $t6, $zero, spec_next    # 0 = unused
    nop
    lw    $t7, 0x00($s5)           # its src
    lw    $t8, 0x08($s5)           # its dst
    or    $t2, $zero, $zero
word:
    beq   $t2, $s4, spec_next
    nop
    addu  $t3, $s3, $t2
    lw    $t4, 0x0($t3)
    srl   $t5, $t4, 28             # LooksLikePointer: the RAW word's segment, before any masking
    beq   $t5, $zero, ptr
    nop
    addiu $t9, $zero, 2
    beq   $t5, $t9, ptr
    nop
    addiu $t9, $zero, 3
    beq   $t5, $t9, ptr
    nop
    addiu $t9, $zero, 8
    beq   $t5, $t9, ptr
    nop
    addiu $t9, $zero, 10
    beq   $t5, $t9, ptr
    nop
    b     word_next
    nop
ptr:
    lui   $t9, 0x1FFF
    ori   $t9, $t9, 0xFFFF
    and   $t9, $t4, $t9            # v = word & PhysAddrMask
    sltu  $v1, $t9, $t7
    bne   $v1, $zero, word_next    # below the range
    nop
    addu  $v0, $t7, $t6
    sltu  $v1, $t9, $v0
    beq   $v1, $zero, word_next    # at or past its end
    nop
    lui   $v0, 0xE000
    and   $v0, $t4, $v0            # keep the word's own segment bits
    subu  $v1, $t9, $t7
    addu  $v1, $t8, $v1
    or    $t4, $v0, $v1
    sw    $t4, 0x0($t3)
word_next:
    addiu $t2, $t2, 4
    b     word
    nop
spec_next:
    addiu $s5, $s5, 0x0C
    addiu $s6, $s6, -1
    bne   $s6, $zero, spec
    nop
    b     job_next
    nop
findrep:
    lw    $s2, 0x04($s0)           # the block
    lw    $s4, 0x08($s0)           # its length…
    addiu $s4, $s4, -4             # …less one word, so the 64-bit read never runs off the end
    lw    $s5, 0x0C($s0)           # how many pairs
    lw    $s6, 0x10($s0)           # where they are
    or    $t2, $zero, $zero
fr:
    beq   $t2, $s4, job_next
    nop
    addu  $t3, $s2, $t2
    lw    $v0, 0x0($t3)
    lw    $v1, 0x4($t3)
    or    $t4, $zero, $zero        # every pair is tried here, so the block is walked once
    or    $t5, $s6, $zero
pair:
    beq   $t4, $s5, fr_next
    nop
    lw    $t6, 0x0($t5)
    bne   $v0, $t6, pair_next
    nop
    lw    $t7, 0x4($t5)
    bne   $v1, $t7, pair_next
    nop
    lw    $t8, 0x8($t5)
    lw    $t9, 0xC($t5)
    sw    $t8, 0x0($t3)
    sw    $t9, 0x4($t3)
    b     fr_next
    nop
pair_next:
    addiu $t4, $t4, 1
    addiu $t5, $t5, 16
    b     pair
    nop
fr_next:
    addiu $t2, $t2, 4
    b     fr
    nop
job_next:
    addiu $s0, $s0, 0x30
    addiu $s1, $s1, -1
    bne   $s1, $zero, job
    nop
    lw    $t0, 0x24($sp)
    sw    $zero, 0x0($t0)          # serviced — the mod's poll is watching this word
    lw    $ra, 0x00($sp)
    lw    $a0, 0x04($sp)
    lw    $s0, 0x08($sp)
    lw    $s1, 0x0C($sp)
    lw    $s2, 0x10($sp)
    lw    $s3, 0x14($sp)
    lw    $s4, 0x18($sp)
    lw    $s5, 0x1C($sp)
    lw    $s6, 0x20($sp)
    addiu $sp, $sp, 0x50
tail:
    # The cape/mask element colour rides this same once-per-frame hook (cat_palette.s @0x01FB2700, entry +0x18).
    # It is a leaf that returns through $ra, so $ra is saved across it — the `j` below still needs the caller's.
    addiu $sp, $sp, -0x10
    sw    $ra, 0x0($sp)
    jal   0x01FB2718
    nop
    lw    $ra, 0x0($sp)
    addiu $sp, $sp, 0x10
    j     0x01FB0D90               # CatPelletFollow — a0 and ra untouched, so it behaves exactly as before
    nop
