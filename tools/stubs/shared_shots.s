# shared_shots.s — the monster shot pack's FIVE slots shared among every shot config a floor needs, so a roster is no
# longer limited to five configs. Assembled at 0x001B3788 (DebugInfoCave.SharedShots + 8): the body of the main ELF's
# DebugInfomationDraw, the developers' on-screen debug overlay (3,952 B) whose first word ElfPatches turns into `jr ra`
# — a caller (dun.bin's DrawProcess, behind a debug flag) returns at once and never reaches these bytes.
#
# THE PACK. The species loader (SetupBaseModel) enters each species' one or two shot configs into the pack at floor load;
# a config gets a slot (0..4) that is written to the species row (+0x68/+0x6A) and copied to every unit of the species
# (+0xAC/+0xAE of its 400-byte block); the STB `_SET_SHOT` posts a request and Step fires Set on `pack + slot × 0xA160`.
# A sixth config got −1 from the pack and the loader left the row's raw config index in place — read as a slot number.
#
# THE SCHEME. ENTER hooks the loader's two pack calls: a refused config is stored as −(index + 2), so the units of the
# species carry the config they want. FIRE0/FIRE1 hook Step's two fire sites: a unit firing from a negative number has
# the config ACQUIRED first — a victim slot is picked (empty; else quiescent and least recently fired, a slot no living
# unit wants first), its config's slot IMAGE (the 0xA160 bytes) is saved to an image store, and the wanted config is
# put in: from its own image store when it has been in the pack on this floor already (a copy), else from disc through
# Entry__12CSHOT_EFFECT into a fresh REGION of the monster pool — [16 B signature "SHRG" + mark][image][the config's
# data], carved at the pool's top and kept for the floor, so every config is read from disc at most once per floor and a
# later swap is two copies. Every reference in the species rows and the unit blocks is rewritten: the victim's config's
# slot number → its negative form, the wanted config's negative form → the slot. No slot to take (every one has a
# sub-shot in flight) or no room → the request is cleared and that shot is skipped. STEP, the dungeon step loop's chain
# head (dun 0x1DB874C; it calls the borrowed-shots keeper at 0x1FB1ED0 first, which runs the earlier chain), preloads
# one config a frame while a slot is FREE (empty, or wanted by no living unit and quiescent) so a shot is usually in the
# pack before it is first fired. An image store or a region is proven by its signature: the magic, the pool's used
# counter right after the carve (kept in the block's table too), and the live counter at or past it — a new floor
# rewinds the pool and every store goes stale by itself.
#
# THE BLOCK (0x01FAF200, SharedShotBlock): +0x00 "SHRE" (mod; nothing is done without it — a refused config is then
#   skipped at fire)  +0x04 frame counter (cave)  +0x08 disc entries  +0x0C restores  +0x10 skipped fires  +0x14 no room
#   +0x18/+0x1C the pool headroom an entry needs, units of 16 B, for two / six sub-shots (mod; 0 = 24,000 / 48,000)
#   +0x20 stamps[5]: the frame each slot last fired (cave)  +0x40 config table [34] {image store, mark}
#   +0x150 event ring [16] × {kind, slot, config in, config out (bytes), frame, data units} (kinds: 1 entered from
#   disc, 2 restored, 3 skipped fire, 4 no room, 5 the entry failed)  +0x250 the ring's write index  +0x260 the
#   allocator handed to Entry__12 {base, 0, used, cap}
# Globals: 0x01DF87D0 CMonstorUnit (+0x48 species rows on the floor, rows at +0x1DE30 × 0x9C, unit blocks at +0x1E3D0
#   × 0x190: +0 state, +0x24 HP, +0x40 monster type, +0xAC/+0xAE the two slot numbers)   *0x002A35D8 the pack
#   0x0027FA70 the 34 config pointers   0x01F066D0 the monster pool {base, 0, used, cap}   *0x002A2384 read_buffer
# Helpers clobber at, v0, v1, t8, t9 (and hi/lo); find_victim / find_free / rewrite_refs also t0..t7.

    b     step
    nop
    b     fire0
    nop
    b     fire1
    nop
    b     enter
    nop

# ───────── enter: SetupBaseModel's `jal Entry__17CSHOT_EFFECT_PACK` (0x1E01B0 and 0x1E0224) ─────────
# a0..t1 are the pack call's arguments; v1 = the config's index × 4 (the loader's own table offset). The pack refuses
# (−1) → −(index + 2): not −1, so the loader stores it in the row like a slot number and skips its error print.
enter:
    addiu $sp, $sp, -0x10
    sw    $ra, 0x0000($sp)
    sw    $v1, 0x0004($sp)
    jal   0x001AE4C0               # Entry__17CSHOT_EFFECT_PACK
    nop
    lw    $v1, 0x0004($sp)
    lw    $ra, 0x0000($sp)
    addiu $at, $zero, -1
    bne   $v0, $at, enter_ret
    nop
    sra   $v1, $v1, 2              # the config's index
    addiu $v1, $v1, 2
    subu  $v0, $zero, $v1          # −(index + 2)
enter_ret:
    jr    $ra
    addiu $sp, $sp, 0x10

# ───────── fire0 / fire1: Step__12CMonstorUnit's fire sites (0x1DEED0 / 0x1DEFD8: the `lui at,0x6` before the request load)
# a0 = unit + idx × 0x30 (the unit's shot records), a3 = idx, s5 = the unit; at, v0, v1 are dead there. Returns with
# at = a0 + 0x60000, the pair it replaced, to the request load. A firing slot is stamped with the frame (the LRU key).
fire0:
    lui   $at, 0x0006
    addu  $at, $a0, $at
    lw    $v1, -0x008C($at)        # the request for shot 0
    addiu $v0, $zero, 2
    bne   $v1, $v0, fire_ret       # not firing this frame
    nop
    sll   $v0, $a3, 8
    sll   $v1, $a3, 7
    addu  $v0, $v0, $v1
    sll   $v1, $a3, 4
    addu  $v0, $v0, $v1            # idx × 400
    addu  $v0, $v0, $s5
    lui   $v1, 0x0002
    addu  $v0, $v0, $v1
    lh    $v1, -0x1B84($v0)        # the unit's slot number for shot 0
    bltz  $v1, fire0_slow          # negative: the config is not in the pack
    nop
fire_stamp:
    sll   $v0, $v1, 2
    lui   $v1, 0x01FA
    ori   $v1, $v1, 0xF200         # the block
    addu  $v0, $v0, $v1
    lw    $v1, 0x0004($v1)         # the frame
    sw    $v1, 0x0020($v0)         # stamps[slot]
fire_ret:
    lui   $at, 0x0006
    jr    $ra
    addu  $at, $a0, $at
fire0_slow:
    b     fire_slow
    addiu $v0, $zero, 0            # shot 0
fire1:
    lui   $at, 0x0006
    addu  $at, $a0, $at
    lw    $v1, 0x0274($at)         # the request for shot 1
    addiu $v0, $zero, 2
    bne   $v1, $v0, fire_ret
    nop
    sll   $v0, $a3, 8
    sll   $v1, $a3, 7
    addu  $v0, $v0, $v1
    sll   $v1, $a3, 4
    addu  $v0, $v0, $v1
    addu  $v0, $v0, $s5
    lui   $v1, 0x0002
    addu  $v0, $v0, $v1
    lh    $v1, -0x1B82($v0)        # the unit's slot number for shot 1
    bgez  $v1, fire_stamp
    nop
    b     fire_slow
    addiu $v0, $zero, 1            # shot 1

# v0 = which shot, v1 = −(config + 2). Everything Step still holds is kept.
fire_slow:
    addiu $sp, $sp, -0x60
    sw    $ra, 0x0010($sp)
    sw    $a0, 0x0014($sp)
    sw    $a1, 0x0018($sp)
    sw    $a2, 0x001C($sp)
    sw    $a3, 0x0020($sp)
    sw    $t0, 0x0024($sp)
    sw    $t1, 0x0028($sp)
    sw    $t2, 0x002C($sp)
    sw    $t3, 0x0030($sp)
    sw    $t4, 0x0034($sp)
    sw    $t5, 0x0038($sp)
    sw    $t6, 0x003C($sp)
    sw    $t7, 0x0040($sp)
    sw    $t8, 0x0044($sp)
    sw    $t9, 0x0048($sp)
    sw    $s0, 0x004C($sp)
    sw    $s1, 0x0050($sp)
    sw    $s2, 0x0054($sp)
    sw    $s3, 0x0058($sp)
    sw    $s4, 0x005C($sp)
    move  $s0, $v0                 # which shot
    move  $s1, $a3                 # the unit's index
    subu  $s2, $zero, $v1
    addiu $s2, $s2, -2             # the config wanted
    lui   $s3, 0x01FA
    ori   $s3, $s3, 0xF200         # the block
    lw    $t0, 0x0000($s3)
    lui   $t1, 0x4552
    ori   $t1, $t1, 0x4853         # "SHRE"
    bne   $t0, $t1, fire_skip      # no block: the shot is skipped, never fired from a number that is no slot
    nop
    move  $a0, $s1
    jal   find_victim              # (unit, which) → the slot to take, −1 = none
    move  $a1, $s0
    bltz  $v0, fire_skip
    nop
    move  $s4, $v0
    move  $a0, $s1
    jal   unit_block
    nop
    lh    $t0, 0x0040($v0)         # the unit's monster type
    addiu $t1, $zero, 2
    addiu $a2, $zero, 2            # two sub-shots …
    bne   $t0, $t1, fire_acquire
    move  $a0, $s2                 # (delay) the config
    addiu $a2, $zero, 6            # … six for a boss-class unit (the loader's rule)
fire_acquire:
    jal   acquire                  # (config, slot, count) → the slot, −1 = failed
    move  $a1, $s4
    bgez  $v0, fire_done           # the unit's number is the slot now: Step fires from it
    nop
fire_skip:
    lw    $t0, 0x0014($sp)         # a0: the unit's shot records
    lui   $at, 0x0006
    addu  $at, $t0, $at
    bne   $s0, $zero, fire_skip1
    nop
    b     fire_skip_note
    sw    $zero, -0x008C($at)      # the request for shot 0, cleared
fire_skip1:
    sw    $zero, 0x0274($at)       # the request for shot 1, cleared
fire_skip_note:
    lw    $t0, 0x0000($s3)
    lui   $t1, 0x4552
    ori   $t1, $t1, 0x4853
    bne   $t0, $t1, fire_done      # (no block: nothing to note)
    nop
    lw    $t0, 0x0010($s3)
    addiu $t0, $t0, 1
    sw    $t0, 0x0010($s3)         # skipped fires
    addiu $a0, $zero, 3
    addiu $a1, $zero, -1
    move  $a2, $s2
    addiu $a3, $zero, -1
    jal   ring_put
    move  $t0, $zero
fire_done:
    lw    $ra, 0x0010($sp)
    lw    $a0, 0x0014($sp)
    lw    $a1, 0x0018($sp)
    lw    $a2, 0x001C($sp)
    lw    $a3, 0x0020($sp)
    lw    $t0, 0x0024($sp)
    lw    $t1, 0x0028($sp)
    lw    $t2, 0x002C($sp)
    lw    $t3, 0x0030($sp)
    lw    $t4, 0x0034($sp)
    lw    $t5, 0x0038($sp)
    lw    $t6, 0x003C($sp)
    lw    $t7, 0x0040($sp)
    lw    $t8, 0x0044($sp)
    lw    $t9, 0x0048($sp)
    lw    $s0, 0x004C($sp)
    lw    $s1, 0x0050($sp)
    lw    $s2, 0x0054($sp)
    lw    $s3, 0x0058($sp)
    lw    $s4, 0x005C($sp)
    b     fire_ret
    addiu $sp, $sp, 0x60

# ───────── step: the dungeon step loop's chain head (dun 0x1DB874C `jal`); a0 = the pool the chain passes on ─────────
step:
    addiu $sp, $sp, -0x20
    sw    $ra, 0x0010($sp)
    sw    $s0, 0x0014($sp)
    sw    $s1, 0x0018($sp)
    sw    $s2, 0x001C($sp)
    jal   0x01FB1ED0               # the borrowed-shots keeper: PropPelletFollow → … → step__5CSHOT(pool), then Xiao's shot
    nop
    lui   $s0, 0x01FA
    ori   $s0, $s0, 0xF200         # the block
    lw    $t0, 0x0000($s0)
    lui   $t1, 0x4552
    ori   $t1, $t1, 0x4853         # "SHRE"
    bne   $t0, $t1, step_ret
    nop
    lw    $t0, 0x0004($s0)
    addiu $t0, $t0, 1
    sw    $t0, 0x0004($s0)         # the frame
    # preload: the first living unit whose shot is not in the pack
    lui   $t2, 0x01DF
    ori   $t2, $t2, 0x87D0
    lui   $t3, 0x0002
    addu  $t2, $t2, $t3
    addiu $t2, $t2, -0x1C30        # unit block 0
    addiu $t3, $zero, 16
step_unit:
    lw    $t4, 0x0000($t2)         # state: spawned or active
    blez  $t4, step_next
    nop
    lw    $t4, 0x0024($t2)         # HP
    blez  $t4, step_next
    nop
    lh    $t4, 0x00AC($t2)
    slti  $t5, $t4, -1             # below −1: a config's negative form
    bne   $t5, $zero, step_found
    nop
    lh    $t4, 0x00AE($t2)
    slti  $t5, $t4, -1
    bne   $t5, $zero, step_found
    nop
step_next:
    addiu $t3, $t3, -1
    bne   $t3, $zero, step_unit
    addiu $t2, $t2, 0x190
    b     step_ret
    nop
step_found:
    subu  $s1, $zero, $t4
    addiu $s1, $s1, -2             # the config wanted
    lh    $t5, 0x0040($t2)         # the unit's monster type
    addiu $t6, $zero, 2
    addiu $s2, $zero, 2
    bne   $t5, $t6, step_free
    nop
    addiu $s2, $zero, 6
step_free:
    jal   find_free                # a slot no living unit wants, −1 = none
    nop
    bltz  $v0, step_ret
    move  $a1, $v0
    move  $a0, $s1
    jal   acquire
    move  $a2, $s2
step_ret:
    lw    $ra, 0x0010($sp)
    lw    $s0, 0x0014($sp)
    lw    $s1, 0x0018($sp)
    lw    $s2, 0x001C($sp)
    jr    $ra
    addiu $sp, $sp, 0x20

# ───────── acquire(a0 = the config, a1 = the slot, a2 = sub-shot count) → v0 = the slot, −1 = failed ─────────
acquire:
    addiu $sp, $sp, -0x30
    sw    $ra, 0x0000($sp)
    sw    $s0, 0x0004($sp)
    sw    $s1, 0x0008($sp)
    sw    $s2, 0x000C($sp)
    sw    $s3, 0x0010($sp)
    sw    $s4, 0x0014($sp)
    sw    $s5, 0x0018($sp)
    sw    $s6, 0x001C($sp)
    sw    $s7, 0x0020($sp)
    move  $s0, $a0                 # W: the config wanted
    move  $s1, $a1                 # the slot
    move  $s2, $a2                 # the count
    lui   $s3, 0x01FA
    ori   $s3, $s3, 0xF200         # the block
    sltiu $t0, $s0, 34
    beq   $t0, $zero, acq_fail     # no such config (a number that is neither a slot nor a config's negative form)
    nop
    move  $a0, $s1
    jal   slot_addr
    nop
    move  $s4, $v0                 # the slot's bytes
    lw    $a0, 0x0000($s4)
    jal   cfg_index_of
    nop
    move  $s5, $v0                 # A: the config in the slot (−1: empty)
    bne   $s5, $s0, acq_images
    nop
    addiu $s5, $zero, -1           # the slot holds W already: treated as empty (its references alone are rewritten)
acq_images:
    move  $a0, $s0
    jal   valid_image
    nop
    move  $s6, $v0                 # W's image store (0: not on this floor yet — from disc)
    move  $s7, $zero
    bltz  $s5, acq_need
    nop
    move  $a0, $s5
    jal   valid_image
    nop
    move  $s7, $v0                 # A's image store (0: none yet)
acq_need:
    # the pool units this takes: an image store for A when it has none; W's region when it comes from disc
    move  $t0, $zero
    bltz  $s5, acq_need_w
    nop
    bne   $s7, $zero, acq_need_w
    nop
    addiu $t0, $zero, 2583         # signature + image
acq_need_w:
    bne   $s6, $zero, acq_check
    nop
    lw    $t1, 0x0018($s3)         # the headroom an entry with two sub-shots needs
    addiu $t2, $zero, 6
    bne   $s2, $t2, acq_need_2
    nop
    lw    $t1, 0x001C($s3)         # … with six
    bne   $t1, $zero, acq_need_sum
    nop
    b     acq_need_sum
    ori   $t1, $zero, 48000
acq_need_2:
    bne   $t1, $zero, acq_need_sum
    nop
    ori   $t1, $zero, 24000
acq_need_sum:
    addu  $t0, $t0, $t1
    addiu $t0, $t0, 2583           # + its signature and image
acq_check:
    lui   $t1, 0x01F0
    ori   $t1, $t1, 0x66D0         # the monster pool
    lw    $t2, 0x0008($t1)         # used
    lw    $t3, 0x000C($t1)         # cap
    addu  $t2, $t2, $t0
    slt   $t3, $t3, $t2
    bne   $t3, $zero, acq_noroom   # the pool cannot hold it (its own overflow is a hang)
    nop
    # A's image is saved before the slot changes
    bltz  $s5, acq_in
    nop
    bne   $s7, $zero, acq_save
    nop
    jal   carve                    # an image store for A: signature + 0xA160
    addiu $a0, $zero, 2583
    beq   $v0, $zero, acq_noroom
    nop
    addiu $s7, $v0, 0x10           # the image sits above the signature
    sll   $t0, $s5, 3
    addu  $t0, $s3, $t0
    sw    $s7, 0x0040($t0)         # table[A] = {image, mark}
    lw    $t1, 0x0004($v0)
    sw    $t1, 0x0044($t0)
acq_save:
    move  $a0, $s4
    jal   copy_image
    move  $a1, $s7
acq_in:
    bne   $s6, $zero, acq_restore
    nop
    # W from disc: a region at the pool's top — [signature][image][data]; the allocator gets the rest of the pool
    lui   $t1, 0x01F0
    ori   $t1, $t1, 0x66D0
    lw    $t2, 0x0000($t1)         # base
    lw    $t3, 0x0008($t1)         # used
    sll   $t4, $t3, 4
    addu  $t2, $t2, $t4            # the region
    ori   $t4, $zero, 0xA170
    addu  $t4, $t2, $t4            # its data
    sw    $t4, 0x0260($s3)
    sw    $zero, 0x0264($s3)
    sw    $zero, 0x0268($s3)
    lw    $t4, 0x000C($t1)         # cap
    subu  $t4, $t4, $t3
    addiu $t4, $t4, -2583
    sw    $t4, 0x026C($s3)         # what the data may take
    move  $a0, $s4
    jal   0x001AE440               # Initialize__12CSHOT_EFFECT(slot): emptied
    nop
    move  $a0, $s4
    lui   $a1, 0x0028
    addiu $a1, $a1, -0x0590        # the config pointers
    sll   $t0, $s0, 2
    addu  $a1, $a1, $t0
    lw    $a1, 0x0000($a1)         # the config
    lui   $a2, 0x002A
    lw    $a2, 0x2384($a2)         # read_buffer
    addiu $a3, $zero, 0x26         # the pack's texture block
    addiu $t0, $s3, 0x260          # our allocator
    jal   0x001ACC70               # Entry__12CSHOT_EFFECT(slot, cfg, buffer, block, alloc, count) → 1 entered
    move  $t1, $s2
    bne   $v0, $zero, acq_entered
    nop
    # not entered (the slot is empty now): A comes back from its image so the floor loses nothing
    bltz  $s5, acq_failed
    nop
    move  $a0, $s7
    jal   copy_image
    move  $a1, $s4
acq_failed:
    lw    $t0, 0x0014($s3)
    addiu $t0, $t0, 1
    sw    $t0, 0x0014($s3)
    addiu $a0, $zero, 5
    move  $a1, $s1
    move  $a2, $s0
    move  $a3, $s5
    jal   ring_put
    move  $t0, $zero
    b     acq_fail
    nop
acq_entered:
    lw    $t0, 0x0268($s3)         # the data it took
    addiu $t0, $t0, 2583
    jal   carve                    # the region is the pool's now: used moves past it, the signature and mark go in
    move  $a0, $t0
    addiu $s6, $v0, 0x10           # W's image store
    sll   $t0, $s0, 3
    addu  $t0, $s3, $t0
    sw    $s6, 0x0040($t0)         # table[W] = {image, mark}
    lw    $t1, 0x0004($v0)
    sw    $t1, 0x0044($t0)
    move  $a0, $s4
    jal   copy_image               # the pristine copy every later restore starts from
    move  $a1, $s6
    lw    $t0, 0x0008($s3)
    addiu $t0, $t0, 1
    sw    $t0, 0x0008($s3)         # disc entries
    addiu $a0, $zero, 1
    move  $a1, $s1
    move  $a2, $s0
    move  $a3, $s5
    jal   ring_put
    lw    $t0, 0x0268($s3)
    b     acq_refs
    nop
acq_restore:
    move  $a0, $s6
    jal   copy_image               # W back in the slot from its store
    move  $a1, $s4
    lw    $t0, 0x000C($s3)
    addiu $t0, $t0, 1
    sw    $t0, 0x000C($s3)         # restores
    addiu $a0, $zero, 2
    move  $a1, $s1
    move  $a2, $s0
    move  $a3, $s5
    jal   ring_put
    move  $t0, $zero
acq_refs:
    # every reference: the slot → A's negative form (when the slot held one); W's negative form → the slot
    addiu $a1, $zero, 0x7FFF       # (matches no field)
    bltz  $s5, acq_refs2
    move  $a2, $zero
    move  $a1, $s1
    addiu $a2, $s5, 2
    subu  $a2, $zero, $a2          # −(A + 2)
acq_refs2:
    addiu $a3, $s0, 2
    subu  $a3, $zero, $a3          # −(W + 2)
    jal   rewrite_refs
    move  $t0, $s1
    lw    $t1, 0x0004($s3)         # the frame
    sll   $t0, $s1, 2
    addu  $t0, $s3, $t0
    sw    $t1, 0x0020($t0)         # stamps[slot]
    b     acq_ret
    move  $v0, $s1
acq_noroom:
    lw    $t1, 0x0014($s3)
    addiu $t1, $t1, 1
    sw    $t1, 0x0014($s3)         # no room
    addiu $a0, $zero, 4
    move  $a1, $s1
    move  $a2, $s0
    jal   ring_put
    move  $a3, $s5                 # (t0 = the units it needed)
acq_fail:
    addiu $v0, $zero, -1
acq_ret:
    lw    $ra, 0x0000($sp)
    lw    $s0, 0x0004($sp)
    lw    $s1, 0x0008($sp)
    lw    $s2, 0x000C($sp)
    lw    $s3, 0x0010($sp)
    lw    $s4, 0x0014($sp)
    lw    $s5, 0x0018($sp)
    lw    $s6, 0x001C($sp)
    lw    $s7, 0x0020($sp)
    jr    $ra
    addiu $sp, $sp, 0x30

# ───────── find_victim(a0 = the unit's index, a1 = which shot) → v0 = the slot to take, −1 = none ─────────
# An empty slot is taken at once. Otherwise a slot must be quiescent (no sub-shot in flight) and not the same unit's
# other shot; among those the least recently fired wins, a slot no living unit wants counting as never fired.
find_victim:
    move  $t5, $ra
    move  $t7, $a1
    jal   unit_block
    nop
    lh    $t0, 0x00AE($v0)         # the other shot's slot number (shot 1's, when shot 0 fires)
    beq   $t7, $zero, fv_mask
    nop
    lh    $t0, 0x00AC($v0)         # (shot 0's, when shot 1 fires)
fv_mask:
    jal   ref_mask
    nop
    move  $t1, $v0                 # the slots living units want
    move  $t2, $zero               # the slot
    addiu $t3, $zero, -1           # the best
    addiu $t4, $zero, -1           # its key (unsigned: none yet)
fv_loop:
    move  $a0, $t2
    jal   slot_addr
    nop
    move  $t6, $v0
    lw    $t7, 0x0000($t6)         # the slot's config
    beq   $t7, $zero, fv_take      # empty: taken at once
    nop
    move  $a0, $t6
    jal   quiescent
    nop
    beq   $v0, $zero, fv_next      # a sub-shot in flight
    nop
    beq   $t2, $t0, fv_next        # the same unit's other shot
    nop
    addiu $t7, $zero, 1
    sllv  $t7, $t7, $t2
    and   $t7, $t7, $t1
    beq   $t7, $zero, fv_key       # wanted by no living unit: key 0
    move  $t7, $zero
    lui   $t7, 0x01FA
    ori   $t7, $t7, 0xF200
    sll   $t8, $t2, 2
    addu  $t7, $t7, $t8
    lw    $t7, 0x0020($t7)         # its stamp
fv_key:
    sltu  $t8, $t7, $t4
    beq   $t8, $zero, fv_next
    nop
    move  $t4, $t7
    move  $t3, $t2
fv_next:
    addiu $t2, $t2, 1
    slti  $t7, $t2, 5
    bne   $t7, $zero, fv_loop
    nop
    jr    $t5
    move  $v0, $t3
fv_take:
    jr    $t5
    move  $v0, $t2

# ───────── find_free() → v0 = a slot that is empty, or quiescent and wanted by no living unit; −1 = none ─────────
find_free:
    move  $t5, $ra
    jal   ref_mask
    nop
    move  $t1, $v0
    move  $t2, $zero
ff_loop:
    move  $a0, $t2
    jal   slot_addr
    nop
    lw    $t7, 0x0000($v0)
    beq   $t7, $zero, ff_take      # empty
    move  $a0, $v0
    addiu $t7, $zero, 1
    sllv  $t7, $t7, $t2
    and   $t7, $t7, $t1
    bne   $t7, $zero, ff_next      # wanted
    nop
    jal   quiescent
    nop
    bne   $v0, $zero, ff_take
    nop
ff_next:
    addiu $t2, $t2, 1
    slti  $t7, $t2, 5
    bne   $t7, $zero, ff_loop
    nop
    jr    $t5
    addiu $v0, $zero, -1
ff_take:
    jr    $t5
    move  $v0, $t2

# ───────── ref_mask() → v0 = bit s set for every slot a living unit (state > 0, HP > 0) refers to ─────────
ref_mask:
    lui   $v1, 0x01DF
    ori   $v1, $v1, 0x87D0
    lui   $t8, 0x0002
    addu  $v1, $v1, $t8
    addiu $v1, $v1, -0x1C30        # unit block 0
    addiu $t8, $zero, 16
    move  $v0, $zero
rm_unit:
    lw    $t9, 0x0000($v1)
    blez  $t9, rm_next
    nop
    lw    $t9, 0x0024($v1)
    blez  $t9, rm_next
    nop
    lh    $t9, 0x00AC($v1)
    bltz  $t9, rm_f1
    nop
    slti  $at, $t9, 5
    beq   $at, $zero, rm_f1
    nop
    addiu $at, $zero, 1
    sllv  $at, $at, $t9
    or    $v0, $v0, $at
rm_f1:
    lh    $t9, 0x00AE($v1)
    bltz  $t9, rm_next
    nop
    slti  $at, $t9, 5
    beq   $at, $zero, rm_next
    nop
    addiu $at, $zero, 1
    sllv  $at, $at, $t9
    or    $v0, $v0, $at
rm_next:
    addiu $t8, $t8, -1
    bne   $t8, $zero, rm_unit
    addiu $v1, $v1, 0x190
    jr    $ra
    nop

# ───────── rewrite_refs(a1 = slot to match or 0x7FFF, a2 = its replacement, a3 = W's negative form, t0 = the slot) ─────────
# Every species row on the floor (+0x68/+0x6A) and every unit block (+0xAC/+0xAE).
rewrite_refs:
    move  $t4, $ra
    lui   $t1, 0x01DF
    ori   $t1, $t1, 0x87D0         # the unit
    lw    $t2, 0x0048($t1)         # species rows on the floor
    slti  $t3, $t2, 33
    bne   $t3, $zero, rr_rows
    lui   $t3, 0x0002
    addiu $t2, $zero, 32           # (never more)
rr_rows:
    addu  $t3, $t1, $t3
    addiu $t3, $t3, -0x21D0        # row 0 (+0x1DE30)
    blez  $t2, rr_units
    nop
rr_row:
    jal   rewrite_field
    addiu $a0, $t3, 0x68
    jal   rewrite_field
    addiu $a0, $t3, 0x6A
    addiu $t2, $t2, -1
    bne   $t2, $zero, rr_row
    addiu $t3, $t3, 0x9C
rr_units:
    lui   $t3, 0x0002
    addu  $t3, $t1, $t3
    addiu $t3, $t3, -0x1C30        # unit block 0
    addiu $t2, $zero, 16
rr_unit:
    jal   rewrite_field
    addiu $a0, $t3, 0xAC
    jal   rewrite_field
    addiu $a0, $t3, 0xAE
    addiu $t2, $t2, -1
    bne   $t2, $zero, rr_unit
    addiu $t3, $t3, 0x190
    jr    $t4
    nop
rewrite_field:
    lh    $t8, 0x0000($a0)
    bne   $t8, $a1, rf_w
    nop
    jr    $ra
    sh    $a2, 0x0000($a0)
rf_w:
    bne   $t8, $a3, rf_ret
    nop
    sh    $t0, 0x0000($a0)
rf_ret:
    jr    $ra
    nop

# ───────── unit_block(a0 = the unit's index) → v0 = its 400-byte block ─────────
unit_block:
    sll   $v0, $a0, 8
    sll   $v1, $a0, 7
    addu  $v0, $v0, $v1
    sll   $v1, $a0, 4
    addu  $v0, $v0, $v1            # × 400
    lui   $v1, 0x01DF
    ori   $v1, $v1, 0x87D0
    addu  $v0, $v0, $v1
    lui   $v1, 0x0002
    addu  $v0, $v0, $v1
    jr    $ra
    addiu $v0, $v0, -0x1C30        # + 0x1E3D0

# ───────── slot_addr(a0 = the slot) → v0 = pack + slot × 0xA160 ─────────
slot_addr:
    lui   $v1, 0x002A
    lw    $v1, 0x35D8($v1)         # NowShotEffect
    ori   $t8, $zero, 0xA160
    mult  $a0, $t8
    mflo  $v0
    jr    $ra
    addu  $v0, $v0, $v1

# ───────── quiescent(a0 = a slot's bytes) → v0 = 1 when none of its eight sub-shots is active ─────────
quiescent:
    ori   $v1, $zero, 0xA000
    addu  $v1, $a0, $v1            # the active flags
    addiu $t8, $zero, 8
    addiu $v0, $zero, 1
q_loop:
    lh    $t9, 0x0000($v1)
    bne   $t9, $zero, q_no
    addiu $t8, $t8, -1
    bne   $t8, $zero, q_loop
    addiu $v1, $v1, 2
    jr    $ra
    nop
q_no:
    jr    $ra
    move  $v0, $zero

# ───────── cfg_index_of(a0 = a config pointer) → v0 = its index in the table, −1 = none / null ─────────
cfg_index_of:
    beq   $a0, $zero, ci_none
    move  $v0, $zero
    lui   $v1, 0x0028
    addiu $v1, $v1, -0x0590        # the config pointers
ci_loop:
    lw    $t8, 0x0000($v1)
    beq   $t8, $a0, ci_ret
    nop
    addiu $v0, $v0, 1
    slti  $t8, $v0, 34
    bne   $t8, $zero, ci_loop
    addiu $v1, $v1, 4
ci_none:
    addiu $v0, $zero, -1
ci_ret:
    jr    $ra
    nop

# ───────── valid_image(a0 = a config's index) → v0 = its image store when it is still the pool's, else 0 ─────────
valid_image:
    lui   $v1, 0x01FA
    ori   $v1, $v1, 0xF200
    sll   $t8, $a0, 3
    addu  $v1, $v1, $t8
    lw    $v0, 0x0040($v1)         # the image
    beq   $v0, $zero, vi_ret
    nop
    lw    $t9, 0x0044($v1)         # its mark
    lw    $t8, -0x0010($v0)
    lui   $at, 0x4752
    ori   $at, $at, 0x4853         # "SHRG"
    bne   $t8, $at, vi_no          # overwritten: the pool was rewound and regrown over it
    nop
    lw    $t8, -0x000C($v0)
    bne   $t8, $t9, vi_no
    nop
    lui   $at, 0x01F0
    ori   $at, $at, 0x66D0
    lw    $t8, 0x0008($at)         # the pool's used counter
    slt   $t8, $t8, $t9
    beq   $t8, $zero, vi_ret       # at or past the mark: the store is above the pool's data
    nop
vi_no:
    move  $v0, $zero
vi_ret:
    jr    $ra
    nop

# ───────── carve(a0 = units) → v0 = the region's base (its signature is written), 0 = no room ─────────
carve:
    lui   $v1, 0x01F0
    ori   $v1, $v1, 0x66D0         # the monster pool
    lw    $t8, 0x0008($v1)         # used
    lw    $t9, 0x000C($v1)         # cap
    addu  $at, $t8, $a0
    slt   $t9, $t9, $at
    bne   $t9, $zero, carve_no
    nop
    sw    $at, 0x0008($v1)         # taken
    lw    $v0, 0x0000($v1)         # base
    sll   $t8, $t8, 4
    addu  $v0, $v0, $t8            # the region
    lui   $t9, 0x4752
    ori   $t9, $t9, 0x4853
    sw    $t9, 0x0000($v0)         # "SHRG"
    jr    $ra
    sw    $at, 0x0004($v0)         # the mark: the pool's used counter with the region in it
carve_no:
    jr    $ra
    move  $v0, $zero

# ───────── copy_image(a0 = from, a1 = to): the 0xA160 bytes of a slot ─────────
copy_image:
    ori   $t8, $zero, 0xA160
cp_loop:
    lw    $t9, 0x0000($a0)
    lw    $at, 0x0004($a0)
    sw    $t9, 0x0000($a1)
    sw    $at, 0x0004($a1)
    lw    $t9, 0x0008($a0)
    lw    $at, 0x000C($a0)
    sw    $t9, 0x0008($a1)
    sw    $at, 0x000C($a1)
    addiu $a0, $a0, 0x10
    addiu $t8, $t8, -0x10
    bne   $t8, $zero, cp_loop
    addiu $a1, $a1, 0x10
    jr    $ra
    nop

# ───────── ring_put(a0 = kind, a1 = slot, a2 = config in, a3 = config out, t0 = data units): one event ─────────
ring_put:
    lui   $v1, 0x01FA
    ori   $v1, $v1, 0xF200
    lw    $v0, 0x0250($v1)         # the write index
    andi  $t8, $v0, 15
    sll   $t8, $t8, 4
    addu  $t8, $v1, $t8
    addiu $t8, $t8, 0x150
    sb    $a0, 0x0000($t8)
    sb    $a1, 0x0001($t8)
    sb    $a2, 0x0002($t8)
    sb    $a3, 0x0003($t8)
    lw    $t9, 0x0004($v1)
    sw    $t9, 0x0004($t8)         # the frame
    sw    $t0, 0x0008($t8)
    addiu $v0, $v0, 1
    jr    $ra
    sw    $v0, 0x0250($v1)
