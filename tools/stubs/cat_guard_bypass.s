# cat_guard_bypass.s — CheckDmg__12CMonstorUnit (main ELF 0x1D9F10): the Divine Beast cat's hit — and the Matador's charged
# pellet — ignore an enemy's GUARD WINDOW; a second marked pellet (Dragon's Y's ball) gets the kick WITHOUT the bypass.
# Assembled at 0x01FB1ED0 (ElfCave.CatGuardBypass).
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
# THE PELLET. A pellet's entry carries kick type 0 and nothing that names the pellet, so the charged shot is told apart by
# its BASE DAMAGE (+0x34): the mod gives the charged pellet a damage no ordinary pellet has and writes it to the mailbox
# (0x01F10000 + 0xD0 PelletCrushDamage; 0 = no charged pellet out). A Xiao-owned entry with that base damage passes too,
# and — since step__5CSHOT plants pellets with no kick at all (Set zeroes +0x80..+0x98 and only the melee planters call
# SetKickBack) — it is given one here: strength +0xD4 PelletKickStrength and decay +0xD8 PelletKickDecay from the mailbox,
# the melee type 2 (the Xiao flinch stub lets it stagger), and the kick ORIGIN +0xDC/+0xE0/+0xE4 PelletKickOrigin — CheckDmg
# shoves along (enemy point − origin), so the mod puts it well behind the pellet on its flight line and the shove follows
# the flight whichever side was struck (the melee planters use the player's position for the same reason). The window
# test comes first in CheckDmg; the kick is read in the damage block.
# A Xiao-owned entry whose base damage equals +0xF0 PelletKickDamage instead (Dragon's Y's shot) is given the same
# strength, decay and type, but its ORIGIN is the entry's own sphere centre (+0x00/+0x04/+0x08 — the shot's impact
# sphere, planted where it burst), so every enemy caught in the burst is shoved straight out of it; it then takes the
# VANILLA window test — knockback, no guard crush.
lw    $at, -0x6210($gp)        # NowColData
sll   $v0, $s2, 5              # entry index × 0x20
addu  $at, $at, $v0
sll   $v0, $s2, 7              #            × 0x80  → × 0xA0 together
addu  $at, $at, $v0            # the damage entry
lw    $v0, 0x0058($at)         # its owner
addiu $v0, $v0, -1             # 1 = Xiao
bne   $v0, $zero, vanilla
nop
lw    $v0, 0x0098($at)         # its kick type
addiu $v0, $v0, -2             # 2 = the cat (a melee-style kick; pellets carry 0)
beq   $v0, $zero, pass
nop
lw    $v0, 0x0034($at)         # its base damage
lui   $at, 0x01F1
lw    $at, 0x00D0($at)         # PelletCrushDamage (mod)
beq   $at, $zero, kickq        # no crushing pellet out
nop
beq   $v0, $at, crush          # the crushing pellet
nop
kickq:
lui   $at, 0x01F1
lw    $at, 0x00F0($at)         # PelletKickDamage (mod)
beq   $at, $zero, vanilla      # no kicking pellet out either
nop
bne   $v0, $at, vanilla        # an ordinary pellet
nop
lw    $at, -0x6210($gp)        # the kicking shot: re-form the entry and give it its kick, out of its own sphere
sll   $v0, $s2, 5
addu  $at, $at, $v0
sll   $v0, $s2, 7
addu  $at, $at, $v0
lui   $v0, 0x01F1
lw    $v0, 0x00D4($v0)         # PelletKickStrength (mod)
sw    $v0, 0x0090($at)
lui   $v0, 0x01F1
lw    $v0, 0x00D8($v0)         # PelletKickDecay (mod)
sw    $v0, 0x0094($at)
addiu $v0, $zero, 2            # melee-type kick
sw    $v0, 0x0098($at)
lw    $v0, 0x0000($at)         # origin = the entry's sphere centre
sw    $v0, 0x0080($at)
lw    $v0, 0x0004($at)
sw    $v0, 0x0084($at)
lw    $v0, 0x0008($at)
sw    $v0, 0x0088($at)
vanilla:                       # (the kicking shot falls through: its guard test is the game's)
sll   $v0, $a2, 1              # rebuild the vanilla address: (window × 2 + slot base) + 0x60000
addu  $v0, $v0, $v1
lui   $at, 0x6
addu  $at, $v0, $at
lh    $v0, 0x0550($at)         # the vanilla load, verbatim
j     0x001DAC80
nop
crush:
lw    $at, -0x6210($gp)        # the charged pellet: re-form the entry and give it its kick, from behind it
sll   $v0, $s2, 5
addu  $at, $at, $v0
sll   $v0, $s2, 7
addu  $at, $at, $v0
lui   $v0, 0x01F1
lw    $v0, 0x00D4($v0)         # PelletKickStrength (mod)
sw    $v0, 0x0090($at)
lui   $v0, 0x01F1
lw    $v0, 0x00D8($v0)         # PelletKickDecay (mod)
sw    $v0, 0x0094($at)
addiu $v0, $zero, 2            # melee-type kick
sw    $v0, 0x0098($at)
lui   $v0, 0x01F1
lw    $v0, 0x00DC($v0)         # PelletKickOrigin x (mod)
sw    $v0, 0x0080($at)
lui   $v0, 0x01F1
lw    $v0, 0x00E0($v0)         #   height
sw    $v0, 0x0084($at)
lui   $v0, 0x01F1
lw    $v0, 0x00E4($v0)         #   y
sw    $v0, 0x0088($at)
pass:
j     0x001DAC80               # the cat's or the charged pellet's hit: report "no window here" and let the damage through
move  $v0, $zero               # (delay slot)
