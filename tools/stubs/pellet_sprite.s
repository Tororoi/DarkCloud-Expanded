# pellet_sprite.s — the cell of dun\effect\basefx01.img a player pellet is drawn as comes from the item id the mod names,
# not the equipped weapon's. draw__5CSHOT (main 0x1ABC40) draws every live pellet as one 32 × 32 cell, index = weapon
# id − 300, reading the id off *NowWeaponHave at 0x1ABC74 (`lw v0,-0x62FC(gp); lh v0,0(v0)`); that pair is the hook
# (`jal` here + nop) and this returns v0 = the id to draw as: Mailbox.PelletSpriteId (0x01F10000 + 0xF4) when it is
# non-zero — Super Steve carrying a slingshot's SynthSphere shows that slingshot's pellet — else the vanilla read.
# Assembled at 0x001B42C0 (DebugInfoCave.PelletSprite), after the shot-slot sharing cave in DebugInfomationDraw's body.
# Clobbers at and v0 only (v1 is set right after the return).
    lui   $at, 0x01F1
    lw    $v0, 0x00F4($at)         # PelletSpriteId (mod; 0 = the equipped weapon's)
    bne   $v0, $zero, ret
    nop
    lw    $v0, -0x62FC($gp)        # NowWeaponHave
    lh    $v0, 0x0000($v0)         # its item id
ret:
    jr    $ra
    nop
