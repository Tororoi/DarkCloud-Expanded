# xiao_melee_flinch.s — CheckDmg__12CMonstorUnit (main ELF 0x1D9F10 — NOT the dun overlay) hard-codes "a hit owned by Xiao (entry +0x58 == 1)
# never staggers": it clears the flinch result (s0 = iVar14) so the monster step never starts the enemy's damage
# reaction (script label 110). This stub, reached by a `j` from ELF 0x1DB410 (the `li v0,1; bne s3,v0` that began that
# test), keeps the rule for plain pellets but lets a Xiao-owned entry that carries a MELEE-TYPE kick (+0x98 == 2 — the
# Divine Beast cat's; pellets have 0) go through the normal decision. It returns to ELF 0x1DB420, the untouched tail
# that (re)computes s4 = entry and loads the kick type for the shove test that follows — so the two other branches
# that land on 0x1DB420 (the two boss floors where Xiao's shots always flinch) are unchanged. Registers as at the
# hook: s3 = owner, s6 = entry offset, gp-relative NowColData, v0/v1 scratch (the tail reloads both).
lw    $v0, -0x6210($gp)        # NowColData
addu  $v0, $v0, $s6            # the entry
lw    $v1, 0x98($v0)           # kick type
addiu $v0, $zero, 1
bne   $s3, $v0, ret            # not Xiao's → the normal decision
addiu $v0, $zero, 2            # (delay slot)
bnel  $v1, $v0, ret            # Xiao's, and not a melee-type kick → no flinch (branch-likely: the slot runs only when taken)
.word 0x70008628               # clear s0  (iVar14 = 0 — the compiler's own 128-bit clear)
ret:
j     0x001DB420               # back to the tail: lw v0,NowColData; addu s4,v0,s6; lw v1,0x98(s4); li v0,2; bne …
nop
