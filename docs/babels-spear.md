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
| spear | `BladeProp.Spawn(4, pointUp: true)` — the equipped weapon's engine-drawn copy in chara slot 3, baked with `BakeUpward` (R_x −90°). The c10w10 mesh runs −9.6 … +16.0 on its axis (tip at 16, dcol0). Root height = ground − 16·4 + h, h from −4 (tip buried) to +26 (exposed) over 0.6 s, then yaw += 30°/s. Down at 20 s, on maintain failure, or on unequip |
| strike | one `CollisionPool.PlayerHitEntry` at the target's body (Big Bang's `BodyCentre`), damage = the weapon's attack, kick words at the spear's position, strength 2.475 fading 0.12 (≈ 25 units), kick type 2 |
| confusion | the Mirage's per-slot target-pointer table (`CodeCaves.PtrTable`, read by the cold-hosted `_GET_POSITION` / `_GET_DISTANCE`): a confused slot's entry points at the live position of a target rolled every 2 s — the player global, or another enemy's `CharObjects` position (guest address), which tracks it with no upkeep. Re-rolled when the victim dies. Requires `Mirage.Armed` |
| table ownership | `BabelsSpear.OwnsTable` while any slot is confused; Mirage's loop skips its per-tick `WriteTable()` then (as it does for Angel Gear's ring). `Release` puts the player pointer back |
| friendly fire | each tick, every active pool entry whose owner is slot·5 + 200 for a confused slot has its victim mask (+0x48) OR'd with 2, so the swing hurts enemies too |
| tint | the unit's ambient add (`CCharacter` +0xCE0) written (40, 16, 56) each tick, zero at release |

## To verify in game

- `BakeUpward` sign: the sword's grip carries a half-turn, the spear's may not — if it points down, flip the sign.
- Slot 3's texture pass gives the copy the weapon's own atlas for Toan's swords; Ungaga's spear should follow (the
  Mirage clone already grafts his weapon into a chara slot), but check.
- Enemy draw honouring +0xCE0 (Draw__10CCharacter folds it in for the player and clone; enemies draw through
  DrawMonstor). Fallback: the Solar Flash's `_STATUS_SET_PALLET` stub programs.
- Mask-3 entries: self-hit exclusion, and the catch rate at a 50 ms tick.
- Whether pointing an enemy at another enemy's live position produces swings (attack-range checks read the redirected
  `_GET_DISTANCE`).
