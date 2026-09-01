# Spray velocity-bias shim — dead CharaChange region (jal-legal). Hooked into EffectWaterSpray @0x165184,
# REPLACING its final `jal EnterEffect` (0x164980). At that point the CEffectParam is fully built: a0 = the
# CEffectGroup, a1 = &CEffectParam (sp+0x60), and its initial velocity is at a1+0x30/0x34/0x38 (vx,vy,vz —
# SetEffect copies +0x18 halfword=byte 0x30 into the particle, Step integrates pos += vel). We ADD the global
# bias vec (0x01F18300: bx,by,bz) to that velocity, then TAIL-JUMP to EnterEffect — $ra is still EffectWaterSpray's
# post-call address, so EnterEffect returns there and the function finishes normally (no stack frame needed).
#
# The bias is 0 except during the Queens spray cave, which sets it per emitter and re-zeros it after — so
# Matataki's own waterfall spray (same EffectWaterSpray, called earlier in MainDraw) is unaffected. by<0 lowers
# the plume (the vanilla up-velocity is a fixed ~1.0-1.5, not a parameter); bx/bz aim the mist horizontally
# (vx is otherwise symmetric scatter with no net direction).
lui   $t0, 0x1f2
lwc1  $f4, -0x7d00($t0)        # bx  @0x01F18300
lwc1  $f5, -0x7cfc($t0)        # by  @0x01F18304
lwc1  $f6, -0x7cf8($t0)        # bz  @0x01F18308
lwc1  $f0, 0x30($a1)           # vx
lwc1  $f1, 0x34($a1)           # vy
lwc1  $f2, 0x38($a1)           # vz
add.s $f0, $f0, $f4
add.s $f1, $f1, $f5
add.s $f2, $f2, $f6
swc1  $f0, 0x30($a1)
swc1  $f1, 0x34($a1)
swc1  $f2, 0x38($a1)
j     0x00164980               # EnterEffect (tail; $ra unchanged → returns into EffectWaterSpray)
nop
