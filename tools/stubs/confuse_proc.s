# confuse_proc.s — the Confuse weapon ability's on-hit roll, beside the vanilla Poison and Stop rolls in
# CMonstorUnit::CheckDmg (main 0x1D9F10). Assembled at 0x0022B248 (DebugItemCave.ConfuseProc, in the dead body of
# DebugItemGetKey — the item menu's debug sub-mode 5, which nothing ever sets).
# Every path out of the Stop roll meets at 0x1DBAA4 (`lw v1,0x90(s5)`, the slot); that word is `j` here (its delay slot,
# `sll v0,v1,2`, is harmless and runs again after). With s5 = the monster unit:
#   the hit's ability word (unit + slot·0x510 + 0x55754) has 0x4000 (Confuse)
#   AND rand() < 5 % of 2^31 (Stop's own 4 %, Poison's 10 %)
#   AND the monster's type (unit + slot·400 + 0x1E410) is not 2 (a boss) — gated as Critical is: a flat chance, status
#   susceptibility ignored (Poison and Stop scale theirs by it)
#   → CodeCaves.ConfuseProc[slot] = 1 (0x01FAFFE0, a byte per slot): the mod confuses it for 20 s and clears the byte.
# rand() (0x1046F8) is called here as the Poison and Stop blocks call it: nothing caller-saved is live across this point
# (the code after reloads from s5), and CheckDmg restores ra from its frame.

    lw    $v1, 0x0090($s5)         # the slot
    sll   $v0, $v1, 3
    addu  $v1, $v0, $v1
    sll   $v0, $v1, 3
    addu  $v0, $v1, $v0
    sll   $v0, $v0, 4              # slot × 0x510
    addu  $v0, $v0, $s5
    lui   $at, 0x0005
    addu  $at, $v0, $at
    lw    $v0, 0x5754($at)         # the hit's ability word
    andi  $v0, $v0, 0x4000         # Confuse
    beq   $v0, $zero, out
    nop
    jal   0x001046F8               # rand()
    nop
    lui   $at, 0x0666
    ori   $at, $at, 0x6666         # 5 % of 2^31
    sltu  $at, $v0, $at
    beq   $at, $zero, out
    nop
    lw    $v1, 0x0090($s5)
    sll   $v0, $v1, 2
    addu  $v1, $v0, $v1
    sll   $v0, $v1, 2
    addu  $v0, $v1, $v0
    sll   $v0, $v0, 4              # slot × 400
    addu  $v1, $v0, $s5
    lui   $at, 0x0002
    addu  $at, $v1, $at
    lh    $v0, -0x1BF0($at)        # monster type (+0x1E410)
    addiu $v0, $v0, -2
    beq   $v0, $zero, out          # 2 = boss (and boss companions): never confused
    nop
    lw    $v1, 0x0090($s5)
    lui   $at, 0x01FB
    addu  $at, $at, $v1
    addiu $v0, $zero, 1
    sb    $v0, -0x0020($at)        # ConfuseProc[slot] = 1 (0x01FAFFE0 + slot)
out:
    j     0x001DBAA8               # back after the hooked word
    lw    $v1, 0x0090($s5)         # (delay slot) the hooked `lw v1,0x90(s5)`
