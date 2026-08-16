# Canal tide-evict fade hook — lives in the dead CharaChange region @0x228BB0 (reclaimed ELF code space, so a
# jal from EdFadeInOut is legal — heap caves crash the recompiler). Replaces EdFadeInOut's `sw $v1,-0x6df4($gp)`
# (fade_end=1) @0x189970: does that store, then if the canal-evict flag (mailbox 0x01F10040) is set, requests
# the map-jump to East Harbor dock (NextMapNo=19 @0x2a1e90, StartEventNo=404 @0x2a2524, _MAP_JUMP return code
# DAT_01d3d618=8 @0x1d3d618) and clears the flag — so it fires exactly on the fully-black frame, natively.
# On entry $v1=1 (li @0x18996c), $gp=global ptr, $ra=0x189978. Uses only $t0-$t3 (caller-saved); preserves gp/s*/ra.
sw    $v1, -0x6df4($gp)        # displaced: fade_end = 1
lui   $t0, 0x1f1
lw    $t1, 0x40($t0)          # t1 = canal-evict flag @0x01F10040
beq   $t1, $zero, done
nop
lui   $t2, 0x2a
ori   $t3, $zero, 19
sw    $t3, 0x1e90($t2)        # NextMapNo = 19
ori   $t3, $zero, 404
sw    $t3, 0x2524($t2)        # StartEventNo (arrival event) = 404
lui   $t2, 0x1d4
ori   $t3, $zero, 8
sw    $t3, -0x29e8($t2)       # DAT_01d3d618 = 8  (_MAP_JUMP)
sw    $zero, 0x40($t0)        # clear the flag (one-shot)
done:
jr    $ra
nop
