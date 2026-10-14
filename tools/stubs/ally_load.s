# ally_load.s — the summoned ally's load spread over its block: its textures and its script copy go into the manager's unused monster
# slots 1..15 (the side allocator, block + 0x90 → manager + 0x231E0), while its meshes, motions and shot data stay in the model room.
# Assembled at 0x00117180 (DeadSifDataCave, after ally_despawn.s). SetupBaseModel (0x1DFE90) is hooked at three instructions (ElfAllyPatches);
# each entry tells the ally's load from a floor's by its allocator — s1 = the allocator, s3 = the manager: the ally's model allocator
# (block + 0x00) sits 0x100 below its manager, which the floor's (the monster pool, 0x01F066D0) never does — and leaves a floor's as vanilla:
#   img  (+0x00) at 0x1DFF74 (`move t1, s1`: the pack's IMG allocator for its loader; delay slot `li t2, 1` already run). t3 (cleared next).
#   stb  (+0x18) at 0x1E00D4 (`lw v0, 8(s1)`; its delay slot `sll v1, v0, 4` is redone here): s1 → the side allocator for the script copy.
#   back (+0x34) at 0x1E0164 (`li s0, 2`; delay slot `lh v0, 0x78(s4)` already run): s1 back to the model allocator for the shot entries.

img:
    addiu $t3, $s1, 0x100
    bne   $t3, $s3, idone
    or    $t1, $s1, $zero          # (delay) a floor's: its own allocator
    addiu $t1, $s3, -0x70          # the ally's side allocator
idone:
    jr    $ra
    nop

stb:
    addiu $t9, $s1, 0x100
    bne   $t9, $s3, sload
    nop
    addiu $s1, $s3, -0x70          # the ally's side allocator
sload:
    lw    $v0, 0x08($s1)
    jr    $ra
    sll   $v1, $v0, 4              # (delay)

back:
    addiu $t9, $s1, 0x70
    bne   $t9, $s3, bdone
    addiu $s0, $zero, 2            # (delay) the replaced `li s0, 2`
    addiu $s1, $s3, -0x100         # the ally's model allocator again
bdone:
    jr    $ra
    nop
