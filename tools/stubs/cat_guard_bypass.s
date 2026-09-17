# cat_guard_bypass.s — CheckDmg__12CMonstorUnit (main ELF 0x1D9F10): the Divine Beast cat's hit ignores an enemy's
# GUARD WINDOW.
#
# WHY IT CANNOT BE DONE FROM THE MOD. The guard is entirely the defender's: for each of the 3 windows, its flag
# (MainMonstorUnit + slot*0x20 + 0x60550) is non-zero and the enemy's live motion frame sits inside [start +0x60558,
# end +0x60564] → the hit becomes a guard reaction and the damage block is skipped. CheckDmg reads nothing off the
# attacking entry to decide it, so the only lever is zeroing the window — and a script re-registers its windows with
# `_SET_GUARD_FRAME` whenever its label runs. The mod's 20 Hz Guard Crush kept losing that race against chest mimics,
# whose wake label (our own disc patch, patch_monster_scripts.py) IS a guard: the log showed the crush landing, the
# window coming straight back, and the pounce clinking off.
#
# THE HOOK. 0x1DAC7C `lh v0,0x550(at)` — the window-flag load, with at = (a2*2 + v1) + 0x60000 (a2 = window index,
# v1 = slot*0x20 + monster base). The next instruction branches to 0x1DAFC8 when the flag is zero, i.e. "no guard on
# this window", so handing back v0 = 0 is exactly "this attacker is not guarded". s2 = the damage entry's index
# (0x1DACE4 forms s2*0xA0 from it) and gp is intact, so the entry is reachable: NowColData (gp−0x6210) + s2*0xA0,
# owner +0x58 == 1 (Xiao) and kick type +0x98 == 2 (the cat stamps it; her pellets carry 0 — cat_pellet_follow.s).
# Clobbers at and v0 only, both dead at the hook (v0 is re-loaded with 0x3510 at 0x1DAC88 on the taken path, and at is
# re-formed by the `lui at,0x6` at 0x1DACA8).
lw    $at, -0x6210($gp)        # NowColData
sll   $v0, $s2, 5              # entry index × 0x20
addu  $at, $at, $v0
sll   $v0, $s2, 7              #            × 0x80  → × 0xA0 together
addu  $at, $at, $v0            # the damage entry
lw    $v0, 0x0098($at)         # its kick type
addiu $at, $zero, 2            # 2 = the cat (a melee-style kick; pellets carry 0)
bne   $v0, $at, vanilla
nop
lw    $at, -0x6210($gp)        # re-form the entry: at was the comparand
sll   $v0, $s2, 5
addu  $at, $at, $v0
sll   $v0, $s2, 7
addu  $at, $at, $v0
lw    $v0, 0x0058($at)         # its owner
addiu $at, $zero, 1            # 1 = Xiao
bne   $v0, $at, vanilla
nop
j     0x001DAC80               # the cat's hit: report "no window here" and let the damage through
move  $v0, $zero               # (delay slot)
vanilla:
sll   $v0, $a2, 1              # rebuild the vanilla address: (window × 2 + slot base) + 0x60000
addu  $v0, $v0, $v1
lui   $at, 0x6
addu  $at, $v0, $at
lh    $v0, 0x0550($at)         # the vanilla load, verbatim
j     0x001DAC80
nop
