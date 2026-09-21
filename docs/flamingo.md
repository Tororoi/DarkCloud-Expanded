# Flamingo — lock-on reach and the fishing passive

`Weapons/Xiao/Flamingo.cs` (thread `Flamingo.FlamingoEffect`, shared with the weapons that inherit the reach;
the passive from `Fishing.OnSessionStart`).

## Lock-on reach (equipped)

Enemies can be locked on to from twice as far. The reach is the Flamingo's and is inherited by Dragon's Y, Divine Beast
Title, Angel Shooter and Angel Gear (`Flamingo.GrantsReach`), and by Super Steve carrying any of the five's
SynthSphere (driven from `SuperSteveEffect`). The reach is the enemy's lock-on distance (`EnemySlotOffsets.
LockOnDistance`, +0x118; `CleanViewMonstor` writes 120 when the slot is set up, and a species script may set its own
with `_SET_LOCKON_DIST` / `_STATUS_SET_LOCKON_DIST`) times a per-character factor: `SetNearLockOnTarget` (the acquire)
and `setTargetCursor` (the hold) each copy a six-float table to the stack and index it by character id — Toan 1.2, Xiao
1.4, Goro 1.1, Ruby 1.5, Ungaga 1.0, Osmond 1.8 — so Xiao's default reach is 168 units.

The table lives at dun 0x1DC1B20, on a page that also holds overlay code, so it is not written over PINE. Instead the
ISO's dun.bin patch (`DunPatches`, four words: the `lui v0; addiu v0` pair in each routine) points both copies at
`CodeCaves.LockOnFactorTable` (0x1FAF480, runtime data, 16-byte aligned for the `lq`). The PNACH seeds the vanilla six
there every frame while the owner word at +0x20 is 0; the driver sets the owner to 1 and holds Xiao's entry at 2.8 while
the Flamingo is equipped, and writes 1.4 back when it goes. Needs the patched ISO and the updated PNACH.

## Fishing passive (owned)

Every Flamingo in the inventory (Xiao's bag or the storage), up to three, adds 10 units to every bait's notice radius,
the bare hook's too: +10, +20 or +30. The radius is the distance at which a fish turns toward the hook; the game
copies the equipped bait's entry from `BaitDetectionRadiusTable` (0x2026AE8C, `FishingAddresses.cs`) into every fish
each frame, so the table is the lever. It is written at each session's start — the game's figures plus the bonus, or
the game's figures alone, since the table keeps whatever was last written. The game's figures: Evy 128, Mimi 50, every
other bait 25, bare hook 40.
