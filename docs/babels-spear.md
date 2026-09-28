# Babel's Spear — "Curse of Babel"

Ungaga holds the guard for 5 s and a giant copy of the spear rises point-up out of the ground under the locked-on
enemy, most of it still buried. The enemy above it is struck and thrown off it; it and every enemy within 150 units of
the spear are confused for the 20 s the spear stands, tinted pale purple. Once risen the spear turns slowly on the spot.
Code: `Weapons/Ungaga/BabelsSpear.cs` (thread from `WeaponThreads`, `case Items.babelsspear`). No new ISO patch. The
old on-hit "stop" proc is gone. Research that led here: `game_data/docs/babels-spear-confusion-re.md`.

## Pieces

| piece | how |
|---|---|
| charge | R1 held in Ungaga's guard poses (motions 9 / 33, the Mirage's) for `GuardChargeMs` 5000, then `FlashChargeComplete` and the summon; one per guard hold |
| target | `PlayerAction.LockOnTargetSlot` if live; else the nearest live enemy within 60; else a spot 15 ahead of Ungaga (no strike, no confusion) |
| spear | `BladeProp.Spawn(4, pointUp: true)` — the equipped weapon's engine-drawn copy in chara slot 3, baked with `BakeUpward` (R_x −90°). The c10w10 mesh runs −9.6 … +16.0 on its axis (tip at 16, dcol0). Root height = ground − 16·4 + h, h from −4 (tip buried) to +40 (exposed) over 0.6 s, then yaw += 120°/s (the slot's Euler yaw wrapped to ±π: past that the engine's angle-to-matrix diverged). At 20 s it fades out over 1 s (opacity; taking the slot down while drawn showed a stretched frame), then comes down; also down on maintain failure or unequip |
| strike | one `CollisionPool.PlayerHitEntry` at the target's body (Big Bang's `BodyCentre`), damage = the weapon's attack, kick words at the spear's position, strength 2.475 fading 0.12 (≈ 25 units), kick type 2 |
| confusion | the target, then every enemy within 150 of the spear — at the summon and every tick after while it stands (not during the fade), so newcomers are confused too — until the spear's 20 s are up. Rides the Mirage's per-slot target-pointer table (`CodeCaves.PtrTable`, read by the cold-hosted `_GET_POSITION` / `_GET_DISTANCE`): a confused slot's entry points, every tick, at the live position of the NEAREST candidate in the confusion area — another live enemy's `CharObjects` position (guest address), or the player global while the player is inside the area. With no candidate it points at the slot's own wander quadword (`CodeCaves.BabelWander`, 16 × 16 B in the free data band): a random spot within 40 of it, renewed every 4 s or once reached. Requires `Mirage.Armed` |
| table ownership | `BabelsSpear.OwnsTable` while any slot is confused; Mirage's loop skips its per-tick `WriteTable()` then (as it does for Angel Gear's ring). `Release` puts the player pointer back |
| friendly fire | each tick, every OPEN attack entry a confused enemy planted (owner slot·5 + 200, +0x70 == +0x74) is tested against every other live enemy's body spheres (`BigBang.NearestHitSphereEdge`); on contact a 1-unit hit entry is planted on the victim's body: the attack's damage, reaction (+0x4C) and kick (+0x80..+0x98), owner −1, +0x60/+0x68 −1, +0x64/+0x6C 0. One per attacker-victim pair per 0.5 s; withdrawn after 3 ticks if unconsumed. The attacker's own entry is never widened (mask 1): its swing sphere overlaps its own body, and CheckDmg has no owner exclusion — +0x5C is not one (any value but −1 makes the entry non-damaging for everyone) |
| tint | the unit's ambient add (`CCharacter` +0xCE0) written (40, 16, 56) each tick, zero at release |

## To verify in game

- `BakeUpward` sign: the sword's grip carries a half-turn, the spear's may not — if it points down, flip the sign.
- Slot 3's texture pass gives the copy the weapon's own atlas for Toan's swords; Ungaga's spear should follow (the
  Mirage clone already grafts his weapon into a chara slot), but check.
- Enemy draw honouring +0xCE0 (Draw__10CCharacter folds it in for the player and clone; enemies draw through
  DrawMonstor). Fallback: the Solar Flash's `_STATUS_SET_PALLET` stub programs.
- The contact test's catch rate at a 50 ms tick (an attack window of a few frames can fall between ticks).
- Whether pointing an enemy at another enemy's live position produces swings (attack-range checks read the redirected
  `_GET_DISTANCE`). First test: Rockanoff wandered in random directions — the victim choice then included the player at
  even odds and re-rolled every 2 s; now enemies only, 6 s. If it persists, the redirected read may not be the one its
  chase uses.
