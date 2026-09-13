# cat_sphere_percent.s — CheckDmg__12CMonstorUnit (main ELF 0x1D9F10) scales every hit by the hurt sphere's per-attacker
# percentage: `dmg = dmg/100 * pct[owner]`, pct at MainMonstorUnit + slot*0x510 + sphere*0x18 + 0x555D0 + owner*4
# (script `_SET_BODY_COL_PARA(10+char, %)`). Minotaur Joe's face is 0 % for Xiao, so her pellets bounce off it — but
# the Divine Beast cat should be able to hit it (user 2026-09-12). Each sphere also owns a SPARE 5-int table at
# +0x55490 + sphere*0x14 (`_SET_BODY_COL_PARA(0..4, v)`; every _SET_BODY_COL resets it to 100; no vanilla engine
# reader, no vanilla script writer). This stub, reached by a `j` from ELF 0x1DC084 (the `lui at,5; addu at,v1,at`
# that forms the pct address), keeps the vanilla address unless the hit is Xiao's (s3 == 1) AND its kick type
# (entry +0x98) equals spare[1] — then it points `at` so the untouched `lw a2,0x55d0(at)` at 0x1DC08C reads spare[0]
# instead. The disc script (tools/iso_patch/patch_monster_scripts.py) arms Joe's face with spare[0] = 100,
# spare[1] = 2 (the cat's kick type; pellets carry 0, and the vanilla 100 never matches a kick type).
# Registers at the hook: v1 = &pct[owner] − 0x50000 (= a3 + j*0x18 + s3*4, recomputed below), a3 = MainMonstorUnit +
# slot*0x510, 0xB0(sp) = sphere index j, s3 = owner, s6 = entry offset. a2 and v1 are dead (reloaded at 0x1DC08C and
# 0x1DC090); v0 is LIVE (0x1DC110) and a3 stays intact.
addiu $v1, $zero, 1
bne   $s3, $v1, vanilla        # not Xiao's hit → vanilla percentage
nop
lw    $a2, 0xB0($sp)           # j
sll   $v1, $a2, 2
addu  $a2, $a2, $v1            # 5j
sll   $a2, $a2, 2              # j * 0x14
addu  $a2, $a2, $a3            # + MainMonstorUnit + slot*0x510
lui   $v1, 0x5
addu  $a2, $a2, $v1            # + 0x50000  (spare table − 0x5490)
lw    $v1, 0x5494($a2)         # spare[1]: the kick type this sphere admits for Xiao
lw    $at, -0x6210($gp)        # NowColData
addu  $at, $at, $s6            # the entry
lw    $at, 0x98($at)           # its kick type
bne   $at, $v1, vanilla        # not the armed kick → vanilla percentage
nop
j     0x001DC08C               # back to `lw a2,0x55d0(at)` + the divide
addiu $at, $a2, -0x140         # (delay slot) at + 0x55D0 = spare[0]
vanilla:
lw    $v1, 0xB0($sp)           # j
sll   $at, $v1, 1
addu  $v1, $v1, $at            # 3j
sll   $v1, $v1, 3              # j * 0x18
addu  $v1, $v1, $a3            # + MainMonstorUnit + slot*0x510
sll   $at, $s3, 2              # owner * 4
addu  $v1, $v1, $at
lui   $at, 0x5
j     0x001DC08C
addu  $at, $v1, $at            # (delay slot) at + 0x55D0 = pct[owner] — the two vanilla words
