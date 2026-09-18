# Matador — Charging Bull

Hold the shot for half a second (the game's charge-complete flash marks it) and the pellet released is charged: 1.5×
damage, passes an enemy's guard window with a hammer-swing kick behind it, and flies as a projection of the slingshot — a copy of
the model, tinted orange, riding the pellet at its own size, wrapped in the cat's glow; the pellet itself is untouched
and hidden inside. `Weapons/Xiao/ChargingBull.cs` drives it from the Matador
thread (`CustomXiaoEffects.MatadorEffect`).

## What rides on the pellet

The pellet is the game's own (`step__5CSHOT`, 0x1ABD10): it flies, collides and plants its damage entry untouched.

- **Damage / guard crush.** The pool slot's damage word is scaled once at the bind and made distinct (×1.5, or +1 if
  that changes nothing). The pellet's entry carries kick type 0 and nothing that names the pellet, so the guard bypass
  (`ElfCave.CatGuardBypass`, hooked at CheckDmg's window load 0x1DAC78) now also passes a Xiao-owned entry whose base
  damage (+0x34) equals `Mailbox.PelletCrushDamage`, and stamps that entry's kick: an ordinary pellet has none (`Set`
  zeroes +0x80..+0x98 and only the melee planters call `SetKickBack(strength, decay, origin, type)` — Goro's hammer
  swing 2.5 / 0.1 / the player's position / 2, Toan's combo 1.2/0.2 → 3.0/0.3), so the cave writes
  `Mailbox.PelletKickStrength`/`PelletKickDecay`, type 2 (the Xiao flinch stub lets it stagger) and
  `Mailbox.PelletKickOrigin`. CheckDmg shoves along (enemy point − origin), so the origin is a point 30 units BEHIND
  the pellet on its flight line — the impact point itself (a point on the enemy's surface) shoved sideways on a side
  hit. The cave has grown twice and now sits at 0x1FB1ED0; the patcher re-hooks an ISO pointing at either old one.
- **Glow.** The Divine Beast cat's glow cave (`ElfCave.CatGlowDraw`) hung on the copy's root (both anchors the root),
  its `catglowp` disc painted by the palette cave in the Fire element's ramp (row 1). On the shot's end the row is handed
  back to None so the cat repaints on its next spawn. (A tenth, orange row cannot be baked: the palette tables end where
  the palette cave begins.) (For the record, `draw__5CSHOT` 0x1ABC40 draws every pellet
  as one 32 × 32 cell of `dun\effect\basefx01.img`, cell `id − 300`, at the pool's sprite scale +0x310 — the Matador's
  is an orange ball, a free glow if ever wanted.)
- **Model.** `SlingshotProp.SpawnProjectile`: the live weapon copied into chara slot 3 as for the Guardian Reflector, but
  world-rooted (no weld to her model root), slid down its own tree so the POUCH sits on the root (the point the cave
  puts on the pellet) with the spare node set at the model's centre for the glow, yaw set from the pellet's velocity, the grip bake at its own preset (the shield's pose flew upside down), and placed on the pellet every frame by
  `ElfCave.PropPelletFollow` — the new first stop of the cat follower hook (dun 0x1DB874C): it calls `CatCopyQueue` (the
  cat's chain, which performs the displaced `step__5CSHOT`) then writes slot 3's position and adds `PropFollowSpin` (0 for the Matador) to its
  yaw; when the pellet ends it clears `PropFollowSlot` and raises `PropFollowEnded`, and the mod fades the copy out.

## The VU packet (RE, for the record)

`CreateVUdataFromMDT` (0x135AA0) builds a weapon's VU packet once at load: per material a TEX0 resolved by name through
the texture manager plus the material's colour rows, then per strip an 8-word header `{count | 0x8000, program, 0x412, 0,
count, prim, colour flag, 0}` followed by count × 16 B of positions, UVs, normals (and colours). The per-frame remake
(`CreateVUdataFromMDTRemake`, the VERTEX_ANIME pouch morph) rewrites positions and colours only, so UVs edited in a
private copy stay; PRIM (texturing on/off) lives in the VU1 program, not the packet. A flat-shaded projectile was tried
by collapsing every UV onto one texel and dropped in favour of the textured model under the orange tint.
