# follow.s — points carried with units, every frame: CodeCaves.FollowTable's 16 entries, each a source vec3 (a unit's
# position: x, height, y) plus offsets copied to a destination (a sub-shot's position) — the stars over a bonked or confused
# enemy, moved on the engine's own frame (a mod tick jittered). Assembled at 0x001B4690 (DebugInfoCave.Follow, the last 96 B
# of dead DebugInfomationDraw), entered from the fall-drive cave's exit, leaving for the blade-spin cave.
# Entry (0x14 B, table 0x01FAFDC0): +0 source (guest; 0 = off), +4 destination (guest), +8/+0xC/+0x10 x/height/y offsets.
# Caller-saved registers only.

    lui   $t0, 0x01FB
    addiu $t1, $t0, -0x0240        # the table (0x01FAFDC0)
    addiu $t2, $zero, 16
loop:
    lw    $t3, 0x0000($t1)         # source
    beq   $t3, $zero, next
    nop
    lw    $t4, 0x0004($t1)         # destination
    lwc1  $f0, 0x0000($t3)
    lwc1  $f1, 0x0008($t1)
    add.s $f0, $f0, $f1
    swc1  $f0, 0x0000($t4)
    lwc1  $f0, 0x0004($t3)
    lwc1  $f1, 0x000C($t1)
    add.s $f0, $f0, $f1
    swc1  $f0, 0x0004($t4)
    lwc1  $f0, 0x0008($t3)
    lwc1  $f1, 0x0010($t1)
    add.s $f0, $f0, $f1
    swc1  $f0, 0x0008($t4)
next:
    addiu $t2, $t2, -1
    bgtz  $t2, loop
    addiu $t1, $t1, 0x14           # (delay slot) the next entry
    j     0x001B4FC0               # the blade-spin cave (then the WHP bill, the chain's tail)
    nop
